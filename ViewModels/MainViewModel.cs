using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using TimeTracker.Models;
using TimeTracker.Services;
using TimeTracker.Views;

namespace TimeTracker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    readonly TimeStore _store;
    readonly SessionClock _clock;
    readonly AlarmPlayer _alarm;
    int _suppress;
    bool _alarmRunning;
    bool _completionPromptShown;
    bool _sessionCompleteAlerted;
    bool _promptingReason;

    public MainViewModel(TimeStore store, SessionClock clock, AlarmPlayer alarm)
    {
        _store = store;
        _clock = clock;
        _alarm = alarm;
        BreakMinutesText = "5";
    }

    public XamlRoot? DialogXamlRoot { get; set; }

    public ObservableCollection<Project> Projects { get; } = [];

    public ObservableCollection<string> SavedBreakReasons { get; } = [];

    public ObservableCollection<OverviewProjectItem> OverviewProjects { get; } = [];

    public ObservableCollection<HistoryDayItem> HistoryDays { get; } = [];

    public ObservableCollection<HistoryEvent> HistoryEvents { get; } = [];

    public ObservableCollection<HistoryProjectTotal> HistoryProjects { get; } = [];

    public ObservableCollection<OverviewProjectItem> HistoryOverviewProjects { get; } = [];

    public IReadOnlyList<RingSegment> RingSegments { get; private set; } = [];

    public IReadOnlyList<RingSegment> HistoryOverviewSegments { get; private set; } = [];

    public IReadOnlyList<HistoryBarSegment> HistoryBarItems { get; private set; } = [];

    string _historyStamp = "";

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _remainingText = "40:00";

    [ObservableProperty]
    private double _elapsedFraction;

    [ObservableProperty]
    private string _sessionStatus = "";

    [ObservableProperty]
    private string _stoppedTodayText = "Stopped today: 0m";

    [ObservableProperty]
    private string _selectedProjectTodayText = "Default today: 0m";

    [ObservableProperty]
    private string _primaryGlyph = "\uE768";

    [ObservableProperty]
    private string _primaryToolTip = "Start";

    [ObservableProperty]
    private bool _canToggleSession = true;

    [ObservableProperty]
    private bool _showResetButton;

    [ObservableProperty]
    private string _yesterdayText = "0m";

    [ObservableProperty]
    private string _streakText = "0";

    [ObservableProperty]
    private string _goalCenterText = "12h";

    [ObservableProperty]
    private string _goalHoursNumber = "12";

    [ObservableProperty]
    private string _historyGoalHoursNumber = "12";

    [ObservableProperty]
    private string _completedText = "Completed: 0 hours, 0 minutes";

    [ObservableProperty]
    private bool _showDailyOverview;

    [ObservableProperty]
    private bool _showRingLabels;

    [ObservableProperty]
    private string _breakMinutesText = "5";

    [ObservableProperty]
    private string _breakReason = "";

    [ObservableProperty]
    private string _breakRemainingText = "";

    [ObservableProperty]
    private string _breakButtonText = "Start break";

    [ObservableProperty]
    private bool _breakFieldsEnabled = true;

    [ObservableProperty]
    private string _errorText = "";

    [ObservableProperty]
    private bool _showHistory;

    [ObservableProperty]
    private HistoryDayItem? _selectedHistoryDay;

    [ObservableProperty]
    private string _historyDateText = "History";

    [ObservableProperty]
    private string _historySummaryText = "Select a day to see its log.";

    [ObservableProperty]
    private bool _hasHistoryEvents;

    public bool ShowFocus => !ShowHistory;

    public bool HasHistoryDays => HistoryDays.Count > 0;

    public bool ShowEmptyDayLog => SelectedHistoryDay is not null && !HasHistoryEvents;

    public bool HasSelectedHistoryDay => SelectedHistoryDay is not null;

    public void Start()
    {
        var wasRunning = _store.LoadState().Session?.SliceStart is not null;
        _clock.Restore(DateTimeOffset.Now);
        _store.ImportBreakReasonsFromHistory();
        ReloadProjects();
        ReloadBreakReasons();
        Refresh();
        if (wasRunning && _clock.IsSessionComplete)
            AlertSessionCompleted();
        else if (_clock.IsSessionComplete)
            _sessionCompleteAlerted = true;
        _ = PromptIfSessionCompletedAsync();
    }

    public void Tick()
    {
        var now = DateTimeOffset.Now;
        var wasActive = IsLive;
        var wasComplete = _clock.IsSessionComplete;
        try
        {
            _clock.CatchUp(now);
            if (wasActive || IsLive)
                _clock.Persist(now);
            ErrorText = "";
        }
        catch (Exception ex)
        {
            ErrorText = "Couldn't save the session. " + ex.Message;
        }

        AlertIfSessionJustCompleted(wasComplete);
        StartAlarmIfDue();
        Refresh();
        RefreshHistoryLive(now);
        _ = PromptIfSessionCompletedAsync();
    }

    public void Flush()
    {
        if (!IsLive)
            return;
        try
        {
            _clock.Persist(DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            ErrorText = "Couldn't save the session. " + ex.Message;
        }
    }

    [RelayCommand]
    async Task ToggleSession()
    {
        var now = DateTimeOffset.Now;
        var project = SelectedProject;
        if (project is null)
            return;

        if (_clock.IsRunning)
        {
            _clock.Pause(now);
        }
        else if (_clock.IsPaused)
        {
            if (!await ResumeAsync(now))
                return;
            if (_clock.State.Session is null)
                _clock.StartSession(project, SessionSeconds(project), DateTimeOffset.Now);
        }
        else if (_clock.IsBreakActive)
        {
            return;
        }
        else
        {
            _clock.StartSession(project, SessionSeconds(project), now);
        }

        Refresh();
    }

    [RelayCommand]
    async Task EndSession()
    {
        if (_promptingReason)
            return;

        var now = DateTimeOffset.Now;
        var threshold = ThresholdSeconds;
        var action = _clock.EndSession(now, null, threshold);
        if (action == ClockAction.NeedReason)
        {
            var reason = await AskReasonAsync(endingSession: true);
            if (reason is null)
                return;
            action = _clock.EndSession(DateTimeOffset.Now, reason.Text, threshold, reason.Project);
        }

        if (action == ClockAction.Done)
            Refresh();
    }

    [RelayCommand]
    async Task OpenSettings() => await OpenSettingsWindowAsync(projectsOnly: false);

    [RelayCommand]
    async Task OpenProjects() => await OpenSettingsWindowAsync(projectsOnly: true);

    async Task OpenSettingsWindowAsync(bool projectsOnly)
    {
        var activeId = _clock.State.Session?.ProjectId;
        var window = new SettingsWindow(_store, firstRun: false, activeId, projectsOnly);
        if (await window.ShowModalAsync())
        {
            _clock.Restore(DateTimeOffset.Now);
            ReloadProjects();
            ReloadBreakReasons();
            Refresh();
            if (ShowHistory)
                LoadHistory();
        }
    }

    [RelayCommand]
    void StartLongBreak() => StartPresetBreak(20, "Long Break");

    [RelayCommand]
    void StartShortBreak() => StartPresetBreak(10, "Short Break");

    [RelayCommand]
    void StartOrCancelBreak()
    {
        if (_clock.IsBreakActive)
        {
            _clock.CancelBreak(DateTimeOffset.Now);
            Refresh();
            return;
        }

        if (!int.TryParse(BreakMinutesText.Trim(), out var minutes) || minutes < 1 || minutes > 24 * 60)
        {
            ErrorText = "Break length needs to be a whole number of minutes.";
            return;
        }

        StartPresetBreak(minutes, BreakReason);
    }

    void StartPresetBreak(int minutes, string reason)
    {
        if (_clock.IsBreakActive)
            return;

        ErrorText = "";
        _clock.StartBreak(minutes * 60, reason, DateTimeOffset.Now);
        _store.RememberBreakReason(reason);
        ReloadBreakReasons();
        Refresh();
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (_suppress > 0 || value is null)
            return;

        _ = HandleProjectChangeAsync(value);
    }

    async Task HandleProjectChangeAsync(Project value)
    {
        var session = _clock.State.Session;
        if (session is null || session.ProjectId == value.Id)
            return;

        if (_promptingReason)
        {
            RevertProject(session.ProjectId);
            return;
        }

        var now = DateTimeOffset.Now;
        _clock.CatchUp(now);
        session = _clock.State.Session;
        if (session is null || session.ProjectId == value.Id)
            return;

        if (_clock.SessionElapsedSeconds(now) <= ThresholdSeconds)
        {
            _clock.MoveSessionToProject(value, now);
            Refresh();
            return;
        }

        var choice = await AskProjectChangeAsync(value.Name);
        if (choice is null)
        {
            RevertProject(session.ProjectId);
            return;
        }

        now = DateTimeOffset.Now;
        if (choice == ProjectChangeChoice.MoveElapsed)
            _clock.MoveSessionToProject(value, now);
        else
            _clock.RestartSessionOnProject(value, now);
        Refresh();
    }

    partial void OnShowHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFocus));
        if (value)
            LoadHistory();
    }

    partial void OnSelectedHistoryDayChanged(HistoryDayItem? value)
    {
        OnPropertyChanged(nameof(ShowEmptyDayLog));
        OnPropertyChanged(nameof(HasSelectedHistoryDay));
        if (_suppress > 0)
            return;
        LoadTimeline();
    }

    partial void OnHasHistoryEventsChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyDayLog));

    partial void OnShowDailyOverviewChanged(bool value)
    {
        if (_suppress > 0)
            return;
        _store.Settings.ShowDailyOverview = value;
        _store.SaveSettings(_store.Settings);
        _store.UpdateTodayGoals(DateOnly.FromDateTime(DateTime.Now));
        UpdateRing(DateTimeOffset.Now, TodaySlices(DateTimeOffset.Now));
    }

    void RevertProject(string projectId)
    {
        _suppress++;
        try
        {
            SelectedProject = Projects.FirstOrDefault(project => project.Id == projectId) ?? SelectedProject;
        }
        finally
        {
            _suppress--;
        }
    }

    async Task<ProjectChangeChoice?> AskProjectChangeAsync(string projectName)
    {
        if (DialogXamlRoot is null)
            return null;
        return await DialogService.AskProjectChangeAsync(DialogXamlRoot, projectName);
    }

    async Task<bool> ResumeAsync(DateTimeOffset now)
    {
        if (_promptingReason)
            return false;

        var threshold = ThresholdSeconds;
        var action = _clock.TryResume(now, null, threshold);
        if (action != ClockAction.NeedReason)
            return action == ClockAction.Done;

        var reason = await AskReasonAsync(endingSession: false);
        if (reason is null)
            return false;
        return _clock.TryResume(DateTimeOffset.Now, reason.Text, threshold, reason.Project) == ClockAction.Done;
    }

    async Task PromptIfSessionCompletedAsync()
    {
        if (!_clock.IsSessionComplete)
        {
            _completionPromptShown = false;
            return;
        }

        if (_promptingReason || _completionPromptShown || DialogXamlRoot is null)
            return;
        if (!_clock.PauseNeedsReason(DateTimeOffset.Now, ThresholdSeconds))
            return;

        _completionPromptShown = true;
        var reason = await AskReasonAsync(endingSession: false);
        if (reason is null)
            return;
        if (_clock.TryResume(DateTimeOffset.Now, reason.Text, ThresholdSeconds, reason.Project) != ClockAction.Done)
            return;
        if (_clock.State.Session is null && SelectedProject is not null)
            _clock.StartSession(SelectedProject, SessionSeconds(SelectedProject), DateTimeOffset.Now);
        Refresh();
    }

    async Task<StopReasonChoice?> AskReasonAsync(bool endingSession, bool startNewSession = false)
    {
        if (DialogXamlRoot is null || _promptingReason)
            return null;

        _promptingReason = true;
        try
        {
            var now = DateTimeOffset.Now;
            var stoppedAt = _clock.State.Session?.PauseStart ?? now;
            var stoppedSeconds = TimeMath.SecondsBetween(stoppedAt, now);
            var reason = await DialogService.AskReasonAsync(
                DialogXamlRoot,
                endingSession,
                stoppedAt,
                stoppedSeconds,
                _store.ListBreakReasons(),
                _store.Settings.Projects,
                startNewSession: startNewSession || (!endingSession && _clock.IsSessionComplete));
            if (reason?.Project is null && !string.IsNullOrWhiteSpace(reason?.Text))
            {
                _store.RememberBreakReason(reason.Text);
                ReloadBreakReasons();
            }

            return reason;
        }
        finally
        {
            _promptingReason = false;
        }
    }

    void ReloadProjects()
    {
        _suppress++;
        try
        {
            var selectedId = _clock.State.Session?.ProjectId ?? SelectedProject?.Id;
            Projects.Clear();
            foreach (var project in _store.Settings.Projects)
                Projects.Add(project);

            if (selectedId is not null && Projects.All(project => project.Id != selectedId) && _clock.State.Session is { } session)
            {
                Projects.Add(new Project
                {
                    Id = session.ProjectId,
                    Name = session.ProjectName,
                    Color = session.ProjectColor
                });
            }

            SelectedProject = Projects.FirstOrDefault(project => project.Id == selectedId) ?? Projects.FirstOrDefault();
            ShowDailyOverview = _store.Settings.ShowDailyOverview;
            SyncOverviewProjects();
        }
        finally
        {
            _suppress--;
        }
    }

    void Refresh()
    {
        var now = DateTimeOffset.Now;
        var settings = _store.Settings;
        var session = _clock.State.Session;
        var slices = TodaySlices(now);
        var stopped = StoppedToday(now);

        if (session is null)
        {
            RemainingText = TimeMath.FormatCountdown(SessionSeconds(SelectedProject));
            ElapsedFraction = 0;
            SessionStatus = "";
            PrimaryGlyph = "\uE768";
            PrimaryToolTip = "Start";
            CanToggleSession = !_clock.IsBreakActive && SelectedProject is not null;
            ShowResetButton = false;
        }
        else
        {
            var remaining = _clock.SessionRemainingSeconds(now);
            RemainingText = TimeMath.FormatCountdown(TimeMath.CeilSeconds(remaining));
            var elapsed = _clock.SessionElapsedSeconds(now);
            ElapsedFraction = session.PlannedSeconds <= 0 ? 0 : Math.Clamp(elapsed / (double)session.PlannedSeconds, 0, 1);
            SessionStatus = _clock.IsBreakActive
                ? "On break"
                : _clock.IsSessionComplete
                    ? "Session complete"
                    : _clock.IsPaused ? "Paused" : "";
            PrimaryGlyph = _clock.IsRunning ? "\uE769" : "\uE768";
            PrimaryToolTip = _clock.IsRunning ? "Pause" : _clock.IsSessionComplete ? "Start new session" : "Resume";
            CanToggleSession = !_clock.IsBreakActive;
            ShowResetButton = _clock.IsPaused && !_clock.IsBreakActive;
        }

        StoppedTodayText = "Stopped today: " + TimeMath.FormatShort(stopped);
        var selectedSeconds = SelectedProject is null
            ? 0
            : slices.Where(slice => slice.ProjectId == SelectedProject.Id).Sum(slice => slice.Seconds);
        var selectedName = SelectedProject?.Name ?? "Project";
        SelectedProjectTodayText = $"{selectedName} today: {TimeMath.FormatShort(selectedSeconds)}";

        var today = TimeMath.LocalDate(now);
        YesterdayText = TimeMath.FormatShort(_store.ClosedWork(today.AddDays(-1)));
        var todayWork = slices.Sum(slice => slice.Seconds);
        StreakText = _store.ComputeStreak(today, todayWork, settings.DailyGoalHours).ToString();
        GoalCenterText = TimeMath.FormatGoal(settings.DailyGoalHours);
        GoalHoursNumber = TimeMath.FormatGoalNumber(settings.DailyGoalHours);
        BreakButtonText = _clock.IsBreakActive ? "Cancel break" : "Start break";
        BreakFieldsEnabled = !_clock.IsBreakActive;
        BreakRemainingText = _clock.IsBreakActive
            ? TimeMath.FormatCountdown(TimeMath.CeilSeconds(_clock.BreakRemainingSeconds(now)))
            : "";

        UpdateRing(now, slices);
    }

    void UpdateRing(DateTimeOffset now, IReadOnlyList<WorkSlice> slices)
    {
        _ = now;
        var settings = _store.Settings;
        ShowRingLabels = ShowDailyOverview;
        var includedIds = OverviewProjects.Where(item => item.IncludeInRing).Select(item => item.Id).ToHashSet();
        var included = OverviewProjects.Count == 0
            ? settings.Projects.ToList()
            : settings.Projects.Where(project => includedIds.Contains(project.Id)).ToList();
        var chartIds = included.Select(project => project.Id).ToHashSet();
        var overviewSlices = slices.Where(slice => chartIds.Contains(slice.ProjectId)).ToList();
        RingSegments = ShowDailyOverview || SelectedProject is null
            ? DailyRingBuilder.BuildOverview(included, overviewSlices, settings.DailyGoalHours)
            : DailyRingBuilder.BuildSelected(SelectedProject, slices.Where(slice => slice.ProjectId == SelectedProject.Id).Sum(slice => slice.Seconds), settings.DailyGoalHours);

        var completed = ShowDailyOverview || SelectedProject is null
            ? slices.Sum(slice => slice.Seconds)
            : slices.Where(slice => slice.ProjectId == SelectedProject.Id).Sum(slice => slice.Seconds);
        CompletedText = TimeMath.FormatCompleted(completed);
        OnPropertyChanged(nameof(RingSegments));
    }

    List<WorkSlice> TodaySlices(DateTimeOffset now)
    {
        var today = TimeMath.LocalDate(now);
        var slices = _store.LoadDay(today).Slices.Where(slice => !slice.Open).ToList();
        if (_clock.CurrentOpenSlice(now) is { } open && TimeMath.LocalDate(open.Start) == today)
            slices.Add(open);
        return slices;
    }

    long StoppedToday(DateTimeOffset now)
    {
        var today = TimeMath.LocalDate(now);
        var dayStart = TimeMath.LocalDayStart(today);
        var dayEnd = TimeMath.NextLocalMidnight(dayStart);
        var total = _store.LoadDay(today).Stops.Where(stop => !stop.Open).Sum(stop => stop.Seconds);
        if (_clock.CurrentOpenStop(now) is { } open)
        {
            var end = open.End ?? now;
            total += TimeMath.Overlap(open.Start, end, dayStart, dayEnd);
        }

        return total;
    }

    void AlertIfSessionJustCompleted(bool wasComplete)
    {
        if (!_clock.IsSessionComplete)
        {
            _sessionCompleteAlerted = false;
            return;
        }

        if (wasComplete)
            return;

        AlertSessionCompleted();
    }

    void AlertSessionCompleted()
    {
        if (_sessionCompleteAlerted)
            return;

        _sessionCompleteAlerted = true;
        var name = _clock.State.Session?.ProjectName ?? SelectedProject?.Name ?? "";
        _ = PlaySessionCompleteAlertAsync(name);
    }

    async Task PlaySessionCompleteAlertAsync(string projectName)
    {
        try
        {
            await _alarm.PlaySessionCompleteAsync();
        }
        catch
        {
        }

        SessionNotifier.ShowSessionComplete(projectName);
    }

    void StartAlarmIfDue()
    {
        if (!_clock.AlarmDue || _alarmRunning)
            return;

        _alarmRunning = true;
        _ = PlayAlarmAsync();
    }

    async Task PlayAlarmAsync()
    {
        try
        {
            while (_clock.AlarmDue)
            {
                try
                {
                    await _alarm.PlayBeepAsync();
                }
                catch
                {
                    // A missing audio device still consumes the play so the cap holds.
                }

                _clock.MarkAlarmPlayed(DateTimeOffset.Now);
                if (!_clock.AlarmDue)
                    break;
                await Task.Delay(600);
            }
        }
        finally
        {
            _alarmRunning = false;
            Refresh();
        }
    }

    void LoadHistory()
    {
        var keep = SelectedHistoryDay?.Date;
        var now = DateTimeOffset.Now;
        var today = TimeMath.LocalDate(now);
        var dates = _store.ListDays().OrderByDescending(date => date).ToList();
        if (_store.DayExists(today) && dates.All(date => date != today))
            dates.Insert(0, today);

        _suppress++;
        try
        {
            HistoryDays.Clear();
            foreach (var date in dates)
                HistoryDays.Add(CreateDayItem(date, today, now));

            SelectedHistoryDay = HistoryDays.FirstOrDefault(item => item.Date == keep)
                ?? HistoryDays.FirstOrDefault();
        }
        finally
        {
            _suppress--;
        }

        OnPropertyChanged(nameof(HasHistoryDays));
        LoadTimeline();
    }

    void RefreshHistoryLive(DateTimeOffset now)
    {
        if (!ShowHistory)
            return;

        var today = TimeMath.LocalDate(now);
        if (!_store.DayExists(today))
            return;

        var item = HistoryDays.FirstOrDefault(day => day.Date == today);
        if (item is null)
        {
            HistoryDays.Insert(0, CreateDayItem(today, today, now));
            OnPropertyChanged(nameof(HasHistoryDays));
            if (SelectedHistoryDay is null)
            {
                _suppress++;
                try
                {
                    SelectedHistoryDay = HistoryDays[0];
                }
                finally
                {
                    _suppress--;
                }
            }
        }
        else
        {
            ApplyDayItem(item, today, now);
        }

        if (SelectedHistoryDay?.Date != today)
            return;

        var day = _store.LoadDay(today);
        var stamp = HistoryTimelineBuilder.Stamp(day);
        if (stamp != _historyStamp)
        {
            LoadTimeline();
            return;
        }

        foreach (var ev in HistoryEvents.Where(item => item.Open))
        {
            if (ev.Kind == HistoryEventKind.Work)
            {
                var slice = day.Slices.FirstOrDefault(item => item.Open && item.Start == ev.Start);
                if (slice is null)
                    continue;
                var seconds = slice.Seconds > 0 ? slice.Seconds : TimeMath.SecondsBetween(slice.Start, now);
                HistoryTimelineBuilder.RefreshTiming(ev, null, seconds, true);
            }
            else
            {
                var stop = day.Stops.FirstOrDefault(item => item.Open && item.Start == ev.Start);
                if (stop is not null)
                {
                    var seconds = stop.Seconds > 0 ? stop.Seconds : TimeMath.SecondsBetween(stop.Start, now);
                    HistoryTimelineBuilder.RefreshTiming(ev, null, seconds, true);
                    continue;
                }

                var entry = day.Breaks.FirstOrDefault(item => item.Open && item.Start == ev.Start);
                if (entry is null)
                    continue;
                HistoryTimelineBuilder.RefreshTiming(ev, null, TimeMath.SecondsBetween(entry.Start, now), true);
            }
        }

        HistorySummaryText = HistoryTimelineBuilder.Build(day, now).SummaryText;
        BindHistoryRings(day);
    }

    void LoadTimeline()
    {
        HistoryEvents.Clear();
        HistoryProjects.Clear();
        HistoryBarItems = [];
        HistoryOverviewSegments = [];
        _historyStamp = "";
        OnPropertyChanged(nameof(HasSelectedHistoryDay));

        if (SelectedHistoryDay is null)
        {
            HistoryDateText = "History";
            HistorySummaryText = HasHistoryDays
                ? "Select a day to see its log."
                : "No days logged yet.";
            HasHistoryEvents = false;
            HistoryOverviewProjects.Clear();
            HistoryGoalHoursNumber = TimeMath.FormatGoalNumber(_store.Settings.DailyGoalHours);
            OnPropertyChanged(nameof(HistoryBarItems));
            OnPropertyChanged(nameof(HistoryOverviewSegments));
            return;
        }

        var now = DateTimeOffset.Now;
        var day = _store.LoadDay(SelectedHistoryDay.Date);
        var view = HistoryTimelineBuilder.Build(day, now);
        HistoryDateText = view.DateText;
        HistorySummaryText = view.SummaryText;
        foreach (var item in view.Events)
            HistoryEvents.Add(item);
        foreach (var project in view.Projects)
            HistoryProjects.Add(project);
        HistoryBarItems = view.BarSegments;
        HasHistoryEvents = HistoryEvents.Count > 0;
        _historyStamp = HistoryTimelineBuilder.Stamp(day);
        SyncHistoryOverviewProjects(ProjectsForDay(day));
        BindHistoryRings(day);
        OnPropertyChanged(nameof(HistoryBarItems));
    }

    HistoryDayItem CreateDayItem(DateOnly date, DateOnly today, DateTimeOffset now)
    {
        var item = new HistoryDayItem(date);
        ApplyDayItem(item, today, now);
        return item;
    }

    void ApplyDayItem(HistoryDayItem item, DateOnly today, DateTimeOffset now)
    {
        var day = _store.LoadDay(item.Date);
        item.Title = HistoryTimelineBuilder.FormatListTitle(item.Date);
        item.Subtitle = HistoryTimelineBuilder.FormatListSubtitle(day, now);
        item.Badge = HistoryTimelineBuilder.FormatBadge(item.Date, today);
    }

    void ReloadBreakReasons()
    {
        SavedBreakReasons.Clear();
        foreach (var reason in _store.ListBreakReasons())
            SavedBreakReasons.Add(reason);
    }

    void SyncOverviewProjects()
    {
        var visible = OverviewProjects.ToDictionary(item => item.Id, item => item.IncludeInRing);
        OverviewProjects.Clear();
        foreach (var project in _store.Settings.Projects)
            OverviewProjects.Add(new OverviewProjectItem(project, visible.GetValueOrDefault(project.Id, true), ApplyOverviewProjectFilter));
    }

    void ApplyOverviewProjectFilter()
    {
        if (_suppress > 0)
            return;
        UpdateRing(DateTimeOffset.Now, TodaySlices(DateTimeOffset.Now));
    }

    void SyncHistoryOverviewProjects(IReadOnlyList<Project> projects)
    {
        var visible = HistoryOverviewProjects.ToDictionary(item => item.Id, item => item.IncludeInRing);
        HistoryOverviewProjects.Clear();
        foreach (var project in projects)
            HistoryOverviewProjects.Add(new OverviewProjectItem(project, visible.GetValueOrDefault(project.Id, true), ApplyHistoryOverviewFilter));
    }

    void ApplyHistoryOverviewFilter()
    {
        if (_suppress > 0 || SelectedHistoryDay is null)
            return;
        BindHistoryRings(_store.LoadDay(SelectedHistoryDay.Date));
    }

    void BindHistoryRings(DayFile day)
    {
        var projects = ProjectsForDay(day);
        var dailyGoal = day.Totals.DailyGoalHours > 0 ? day.Totals.DailyGoalHours : _store.Settings.DailyGoalHours;
        var includedIds = HistoryOverviewProjects.Where(item => item.IncludeInRing).Select(item => item.Id).ToHashSet();
        var included = HistoryOverviewProjects.Count == 0
            ? projects
            : projects.Where(project => includedIds.Contains(project.Id)).ToList();
        var chartIds = included.Select(project => project.Id).ToHashSet();
        var slices = day.Slices.Where(slice => chartIds.Contains(slice.ProjectId)).ToList();
        HistoryOverviewSegments = DailyRingBuilder.BuildOverview(included, slices, dailyGoal);
        HistoryGoalHoursNumber = TimeMath.FormatGoalNumber(dailyGoal);
        OnPropertyChanged(nameof(HistoryOverviewSegments));
    }

    List<Project> ProjectsForDay(DayFile day)
    {
        var projects = new List<Project>();
        var seen = new HashSet<string>();
        foreach (var snap in day.Totals.ProjectGoals)
        {
            if (string.IsNullOrWhiteSpace(snap.ProjectId) || !seen.Add(snap.ProjectId))
                continue;
            projects.Add(new Project
            {
                Id = snap.ProjectId,
                Name = string.IsNullOrWhiteSpace(snap.Name) ? "Project" : snap.Name,
                Color = string.IsNullOrWhiteSpace(snap.Color) ? "#2EE59D" : snap.Color,
                DailyGoalHours = snap.GoalHours
            });
        }

        foreach (var project in _store.Settings.Projects)
        {
            if (!seen.Add(project.Id))
                continue;
            projects.Add(project);
        }

        foreach (var group in day.Slices.GroupBy(slice => slice.ProjectId))
        {
            if (string.IsNullOrWhiteSpace(group.Key) || !seen.Add(group.Key))
                continue;
            var sample = group.Last();
            projects.Add(new Project
            {
                Id = sample.ProjectId,
                Name = string.IsNullOrWhiteSpace(sample.Name) ? "Project" : sample.Name,
                Color = string.IsNullOrWhiteSpace(sample.Color) ? "#2EE59D" : sample.Color
            });
        }

        return projects;
    }

    bool IsLive => _clock.State.Session is not null || _clock.State.Break is not null;

    int ThresholdSeconds => _store.Settings.ReasonThresholdMinutes * 60;

    int SessionSeconds(Project? project)
    {
        var minutes = project?.SessionLengthMinutes > 0
            ? project.SessionLengthMinutes
            : _store.Settings.SessionLengthMinutes;
        return Math.Max(1, minutes) * 60;
    }
}

public partial class OverviewProjectItem : ObservableObject
{
    readonly Action _changed;

    public OverviewProjectItem(Project project, bool includeInRing, Action changed)
    {
        Id = project.Id;
        Name = project.Name;
        Color = project.Color;
        _changed = changed;
        _includeInRing = includeInRing;
    }

    public string Id { get; }
    public string Name { get; }
    public string Color { get; }

    [ObservableProperty]
    private bool _includeInRing;

    partial void OnIncludeInRingChanged(bool value) => _changed();
}