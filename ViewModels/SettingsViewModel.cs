using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.Models;
using TimeTracker.Services;

namespace TimeTracker.ViewModels;

public partial class ProjectEdit : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _color = "#2EE59D";

    [ObservableProperty]
    private string _goalText = "";

    public static ProjectEdit From(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Color = project.Color,
        GoalText = project.DailyGoalHours?.ToString("0.##", CultureInfo.CurrentCulture) ?? ""
    };
}

public partial class SettingsViewModel : ObservableObject
{
    readonly TimeStore _store;

    public SettingsViewModel(TimeStore store, bool firstRun, string? activeProjectId)
    {
        _store = store;
        FirstRun = firstRun;
        ActiveProjectId = activeProjectId;
        var settings = store.Settings;
        SessionLengthText = settings.SessionLengthMinutes.ToString(CultureInfo.CurrentCulture);
        ReasonThresholdText = settings.ReasonThresholdMinutes.ToString(CultureInfo.CurrentCulture);
        DailyGoalText = settings.DailyGoalHours.ToString("0.##", CultureInfo.CurrentCulture);
        DataFolder = store.DataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TimeTracker");
        foreach (var project in settings.Projects)
            Projects.Add(ProjectEdit.From(project));
        if (Projects.Count == 0)
            Projects.Add(ProjectEdit.From(TrackerSettings.CreateDefault().Projects[0]));
    }

    public bool FirstRun { get; }
    public string? ActiveProjectId { get; }
    public ObservableCollection<ProjectEdit> Projects { get; } = [];

    [ObservableProperty]
    private string _sessionLengthText = "40";

    [ObservableProperty]
    private string _reasonThresholdText = "3";

    [ObservableProperty]
    private string _dailyGoalText = "12";

    [ObservableProperty]
    private string _dataFolder = "";

    [ObservableProperty]
    private string _errorText = "";

    public bool TrySave()
    {
        ErrorText = "";
        if (!int.TryParse(SessionLengthText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var sessionLength)
            || sessionLength < 1
            || sessionLength > 24 * 60)
        {
            ErrorText = "Session length needs to be a whole number of minutes, from 1 to 1440.";
            return false;
        }

        if (!int.TryParse(ReasonThresholdText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var threshold)
            || threshold < 0
            || threshold > 24 * 60)
        {
            ErrorText = "The reason threshold needs to be a whole number of minutes, from 0 up.";
            return false;
        }

        if (!TryParseHours(DailyGoalText, out var dailyGoal) || dailyGoal <= 0 || dailyGoal > 24)
        {
            ErrorText = "The daily goal needs to be a number of hours greater than 0 and at most 24.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DataFolder))
        {
            ErrorText = "Choose a folder for the history.";
            return false;
        }

        if (!TryBuildProjects(out var savedProjects))
            return false;

        return Save(sessionLength, threshold, dailyGoal, DataFolder.Trim(), savedProjects);
    }

    public bool TrySaveProjects()
    {
        ErrorText = "";
        if (!TryBuildProjects(out var savedProjects))
            return false;

        var current = _store.Settings;
        return Save(
            current.SessionLengthMinutes,
            current.ReasonThresholdMinutes,
            current.DailyGoalHours,
            _store.DataFolder ?? DataFolder,
            savedProjects);
    }

    bool TryBuildProjects(out List<Project> savedProjects)
    {
        savedProjects = [];
        if (Projects.Count == 0)
        {
            ErrorText = "Keep at least one project.";
            return false;
        }

        foreach (var row in Projects)
        {
            var name = row.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorText = "Every project needs a name.";
                return false;
            }

            if (!ColorUtil.TryNormalize(row.Color, out var color))
            {
                ErrorText = $"The color for {name} needs to be a hex value such as #2EE59D.";
                return false;
            }

            double? goal = null;
            if (!string.IsNullOrWhiteSpace(row.GoalText))
            {
                if (!TryParseHours(row.GoalText, out var projectGoal) || projectGoal <= 0 || projectGoal > 24)
                {
                    ErrorText = $"The daily goal for {name} needs to be empty, or hours greater than 0.";
                    return false;
                }

                goal = projectGoal;
            }

            savedProjects.Add(new Project
            {
                Id = string.IsNullOrWhiteSpace(row.Id) ? Guid.NewGuid().ToString("D") : row.Id,
                Name = name,
                Color = color,
                DailyGoalHours = goal
            });
        }

        if (ActiveProjectId is not null && savedProjects.All(project => project.Id != ActiveProjectId))
        {
            ErrorText = "The project a live session is counting can't be removed.";
            return false;
        }

        return true;
    }

    bool Save(int sessionLength, int threshold, double dailyGoal, string folder, List<Project> savedProjects)
    {
        var settings = new TrackerSettings
        {
            SessionLengthMinutes = sessionLength,
            ReasonThresholdMinutes = threshold,
            DailyGoalHours = dailyGoal,
            ShowDailyOverview = _store.Settings.ShowDailyOverview,
            Projects = savedProjects
        };

        try
        {
            var switching = !_store.HasDataFolder
                || !string.Equals(_store.DataFolder, folder, StringComparison.OrdinalIgnoreCase);
            var destinationHasHistory = File.Exists(Path.Combine(folder, "settings.json"));
            if (switching)
                _store.UseFolder(folder, settings, rememberPath: true);
            if (!switching || !destinationHasHistory)
                _store.SaveSettings(settings);

            _store.UpdateTodayGoals(DateOnly.FromDateTime(DateTime.Now));
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            return false;
        }

        return true;
    }

    [RelayCommand]
    void AddProject()
    {
        Projects.Add(new ProjectEdit
        {
            Name = "Project " + (Projects.Count + 1),
            Color = ColorUtil.Palette[Projects.Count % ColorUtil.Palette.Length]
        });
    }

    [RelayCommand]
    void RemoveProject(ProjectEdit? row)
    {
        if (row is null || Projects.Count <= 1)
        {
            ErrorText = "Keep at least one project.";
            return;
        }

        if (row.Id == ActiveProjectId)
        {
            ErrorText = "The project a live session is counting can't be removed.";
            return;
        }

        ErrorText = "";
        Projects.Remove(row);
    }

    static bool TryParseHours(string text, out double hours)
    {
        var trimmed = text.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out hours)
            || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out hours);
    }
}
