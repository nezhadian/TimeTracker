using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TimeTracker.Models;

namespace TimeTracker.Services;

public sealed class TimeStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    static readonly object IoLock = new();

    public string? DataFolder { get; private set; }
    public TrackerSettings Settings { get; private set; } = TrackerSettings.CreateDefault();

    public bool HasDataFolder => !string.IsNullOrWhiteSpace(DataFolder);

    public static string AppSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimeTracker",
        "appsettings.json");

    public bool TryLoadExistingFolder()
    {
        var path = ReadDataFolderPath();
        if (string.IsNullOrWhiteSpace(path))
            return false;

        Directory.CreateDirectory(path);
        DataFolder = path;
        Settings = ReadSettings(Path.Combine(path, "settings.json")) ?? TrackerSettings.CreateDefault();
        Normalize(Settings);
        if (!File.Exists(SettingsPath))
            SaveSettings(Settings);
        return true;
    }

    public void UseFolder(string path, TrackerSettings settingsIfNew, bool rememberPath = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Choose a folder for the history.");

        Directory.CreateDirectory(path);
        DataFolder = path;
        if (rememberPath)
            WriteDataFolderPath(path);

        var existing = ReadSettings(SettingsPath);
        if (existing is null)
        {
            Normalize(settingsIfNew);
            Settings = settingsIfNew;
            SaveSettings(Settings);
        }
        else
        {
            Normalize(existing);
            Settings = existing;
        }
    }

    public void SaveSettings(TrackerSettings settings)
    {
        Normalize(settings);
        Settings = settings;
        AtomicWrite(SettingsPath, JsonSerializer.Serialize(settings, Json));
    }

    public LiveState LoadState()
    {
        var state = ReadJson<LiveState>(StatePath) ?? new LiveState();
        state.Session ??= null;
        return state;
    }

    public void SaveState(LiveState state)
        => AtomicWrite(StatePath, JsonSerializer.Serialize(state, Json));

    public bool DayExists(DateOnly date) => File.Exists(DayPath(date));

    public DayFile LoadDay(DateOnly date)
    {
        var file = ReadJson<DayFile>(DayPath(date));
        if (file is null)
        {
            return new DayFile
            {
                Date = date.ToString("yyyy-MM-dd"),
                Totals = GoalsFromSettings()
            };
        }

        file.Date = date.ToString("yyyy-MM-dd");
        file.Slices ??= [];
        file.Stops ??= [];
        file.Breaks ??= [];
        file.Totals ??= GoalsFromSettings();
        file.Totals.PerProject ??= [];
        file.Totals.ProjectGoals ??= [];
        return file;
    }

    public void SaveDay(DayFile day)
    {
        if (!DateOnly.TryParse(day.Date, out var date))
            throw new InvalidOperationException("The day file needs a date.");
        Recompute(day, date);
        AtomicWrite(DayPath(date), JsonSerializer.Serialize(day, Json));
    }

    public IEnumerable<DateOnly> ListDays()
    {
        var dir = DaysDirectory;
        if (!Directory.Exists(dir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (DateOnly.TryParse(name, out var date))
                yield return date;
        }
    }

    public int ComputeStreak(DateOnly today, long todayWorkSeconds, double todayGoalHours)
    {
        var streak = 0;
        if (GoalMet(todayWorkSeconds, todayGoalHours))
            streak++;

        for (var day = today.AddDays(-1); streak < 5000; day = day.AddDays(-1))
        {
            if (!File.Exists(DayPath(day)))
                break;

            var file = LoadDay(day);
            var work = file.Slices.Where(slice => !slice.Open).Sum(slice => slice.Seconds);
            if (!GoalMet(work, file.Totals.DailyGoalHours))
                break;
            streak++;
        }

        return streak;
    }

    public long ClosedWork(DateOnly date)
    {
        if (!File.Exists(DayPath(date)))
            return 0;
        return LoadDay(date).Slices.Where(slice => !slice.Open).Sum(slice => slice.Seconds);
    }

    public void UpdateTodayGoals(DateOnly today)
    {
        if (!File.Exists(DayPath(today)))
            return;
        var day = LoadDay(today);
        ApplyGoals(day);
        Recompute(day, today);
        AtomicWrite(DayPath(today), JsonSerializer.Serialize(day, Json));
    }

    string SettingsPath => Path.Combine(RequireFolder(), "settings.json");
    string StatePath => Path.Combine(RequireFolder(), "state.json");
    string DaysDirectory => Path.Combine(RequireFolder(), "days");

    string DayPath(DateOnly date) => Path.Combine(DaysDirectory, date.ToString("yyyy-MM-dd") + ".json");

    string RequireFolder()
    {
        if (string.IsNullOrWhiteSpace(DataFolder))
            throw new InvalidOperationException("Choose a folder for the history.");
        return DataFolder;
    }

    void Recompute(DayFile day, DateOnly date)
    {
        day.Totals ??= new DayTotals();
        var hadGoals = day.Totals.ProjectGoals.Count > 0 || day.Totals.DailyGoalHours > 0;
        if (!hadGoals || date == TimeMath.LocalDate(DateTimeOffset.Now))
            ApplyGoals(day);

        day.Totals.WorkSeconds = day.Slices.Sum(slice => slice.Seconds);
        day.Totals.StoppedSeconds = day.Stops.Sum(stop => stop.Seconds);
        day.Totals.PerProject = day.Slices
            .GroupBy(slice => slice.ProjectId)
            .Select(group =>
            {
                var last = group.Last();
                return new ProjectTotal
                {
                    ProjectId = group.Key,
                    Name = last.Name,
                    Color = last.Color,
                    Seconds = group.Sum(slice => slice.Seconds)
                };
            })
            .ToList();
    }

    void ApplyGoals(DayFile day)
    {
        day.Totals.DailyGoalHours = Settings.DailyGoalHours;
        day.Totals.ProjectGoals = Settings.Projects.Select(project => new ProjectGoalSnap
        {
            ProjectId = project.Id,
            Name = project.Name,
            Color = project.Color,
            GoalHours = project.DailyGoalHours
        }).ToList();
    }

    DayTotals GoalsFromSettings()
    {
        var totals = new DayTotals();
        var day = new DayFile { Totals = totals };
        ApplyGoals(day);
        return totals;
    }

    static bool GoalMet(long workSeconds, double goalHours)
    {
        var goal = (long)Math.Round(goalHours * 3600d, MidpointRounding.AwayFromZero);
        return goal > 0 && workSeconds >= goal;
    }

    static void Normalize(TrackerSettings settings)
    {
        if (settings.SessionLengthMinutes < 1)
            settings.SessionLengthMinutes = 40;
        if (settings.ReasonThresholdMinutes < 0)
            settings.ReasonThresholdMinutes = 3;
        if (settings.DailyGoalHours <= 0)
            settings.DailyGoalHours = 12;
        settings.Projects ??= [];
        foreach (var project in settings.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.Id))
                project.Id = Guid.NewGuid().ToString("D");
            project.Name = project.Name?.Trim() ?? "";
            if (!ColorUtil.TryNormalize(project.Color, out var color))
                color = "#2EE59D";
            project.Color = color;
            if (project.SessionLengthMinutes < 1)
                project.SessionLengthMinutes = settings.SessionLengthMinutes > 0 ? settings.SessionLengthMinutes : 40;
            if (project.DailyGoalHours is <= 0)
                project.DailyGoalHours = null;
        }

        if (settings.Projects.Count == 0 || settings.Projects.All(project => string.IsNullOrWhiteSpace(project.Name)))
            settings.Projects = TrackerSettings.CreateDefault().Projects;

        settings.BreakReasons ??= [];
        settings.BreakReasons = DistinctReasons(settings.BreakReasons);
    }

    public IReadOnlyList<string> ListBreakReasons()
        => DistinctReasons(Settings.BreakReasons.Concat(HistoryBreakReasons()));

    public void RememberBreakReason(string reason)
    {
        reason = reason.Trim();
        if (reason.Length == 0)
            return;

        Settings.BreakReasons ??= [];
        Settings.BreakReasons.RemoveAll(item => string.Equals(item, reason, StringComparison.OrdinalIgnoreCase));
        Settings.BreakReasons.Insert(0, reason);
        if (Settings.BreakReasons.Count > 80)
            Settings.BreakReasons.RemoveRange(80, Settings.BreakReasons.Count - 80);
        SaveSettings(Settings);
    }

    public void ImportBreakReasonsFromHistory()
    {
        var merged = ListBreakReasons();
        if (merged.Count == 0)
            return;
        if (Settings.BreakReasons.Count == merged.Count
            && Settings.BreakReasons.Zip(merged, (left, right) => string.Equals(left, right, StringComparison.Ordinal)).All(same => same))
            return;
        Settings.BreakReasons = merged.ToList();
        SaveSettings(Settings);
    }

    IEnumerable<string> HistoryBreakReasons()
    {
        foreach (var date in ListDays().OrderByDescending(day => day))
        {
            var day = LoadDay(date);
            foreach (var entry in day.Breaks)
                yield return entry.Reason;
            foreach (var stop in day.Stops)
                yield return stop.Reason;
        }
    }

    static List<string> DistinctReasons(IEnumerable<string> reasons)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reason in reasons)
        {
            var text = reason?.Trim() ?? "";
            if (text.Length == 0 || !seen.Add(text))
                continue;
            result.Add(text);
            if (result.Count >= 80)
                break;
        }

        return result;
    }

    T? ReadJson<T>(string path)
    {
        lock (IoLock)
        {
            if (!File.Exists(path))
                return default;
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
            }
            catch (JsonException)
            {
                var backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Move(path, backup, overwrite: true);
                return default;
            }
        }
    }

    static TrackerSettings? ReadSettings(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<TrackerSettings>(File.ReadAllText(path), Json);
        }
        catch (JsonException)
        {
            File.Move(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
            return null;
        }
    }

    static string? ReadDataFolderPath()
    {
        if (!File.Exists(AppSettingsPath))
            return null;
        try
        {
            var stored = JsonSerializer.Deserialize<AppLocation>(File.ReadAllText(AppSettingsPath), Json);
            return string.IsNullOrWhiteSpace(stored?.DataFolder) ? null : stored.DataFolder;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static void WriteDataFolderPath(string path)
    {
        AtomicWrite(AppSettingsPath, JsonSerializer.Serialize(new AppLocation { DataFolder = path }, Json));
    }

    static void AtomicWrite(string path, string content)
    {
        lock (IoLock)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            File.WriteAllText(temp, content);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
    }

    sealed class AppLocation
    {
        public string DataFolder { get; set; } = "";
    }
}
