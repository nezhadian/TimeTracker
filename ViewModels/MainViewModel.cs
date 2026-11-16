using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public MainViewModel(TimeStore store, SessionClock clock, AlarmPlayer alarm)
    {
        _store = store;
        _clock = clock;
        _alarm = alarm;
        BreakMinutesText = "5";
    }

    public ObservableCollection<Project> Projects { get; } = [];

    public IReadOnlyList<RingSegment> RingSegments { get; private set; } = [];

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
    private bool _canEndSession;

    [ObservableProperty]
    private string _yesterdayText = "0m";

    [ObservableProperty]
    private string _streakText = "0";

    [ObservableProperty]
    private string _goalCenterText = "12h";

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

    public void Start()
    {
        _clock.Restore(DateTimeOffset.Now);
        ReloadProjects();
        Refresh();
    }

    public void Tick()
    {
        var now = DateTimeOffset.Now;
        var wasActive = IsLive;
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

        StartAlarmIfDue();
        Refresh();
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
    void ToggleSession()
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
            if (!Resume(now))
                return;
        }
        else if (_clock.IsBreakActive)
        {
            return;
        }
        else
        {
            _clock.StartSession(project, _store.Settings.SessionLengthMinutes * 60, now);
        }

        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanEnd))]
    void EndSession()
    {
        var now = DateTimeOffset.Now;
        var threshold = ThresholdSeconds;
        var action = _clock.EndSession(now, null, threshold);
        if (action == ClockAction.NeedReason)
        {
            var reason = AskReason(endingSession: true);
            if (reason is null)
                return;
            action = _clock.EndSession(DateTimeOffset.Now, reason, threshold);
        }

        if (action == ClockAction.Done)
            Refresh();
    }

    [RelayCommand]
    void OpenSettings()
    {
        var activeId = _clock.State.Session?.ProjectId;
        var window = new SettingsWindow(_store, firstRun: false, activeId)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive)
                     ?? Application.Current?.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            _clock.Restore(DateTimeOffset.Now);
            ReloadProjects();
            Refresh();
        }
    }

    [RelayCommand]
    void StartOrCancelBreak()
    {
        var now = DateTimeOffset.Now;
        if (_clock.IsBreakActive)
        {
            _clock.CancelBreak(now);
            Refresh();
            return;
        }

        if (!int.TryParse(BreakMinutesText.Trim(), out var minutes) || minutes < 1 || minutes > 24 * 60)
        {
            ErrorText = "Break length needs to be a whole number of minutes.";
            return;
        }

        ErrorText = "";
        _clock.StartBreak(minutes * 60, BreakReason, now);
        Refresh();
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (_suppress > 0 || value is null)
            return;
        _clock.ChangeProject(value, DateTimeOffset.Now);
        Refresh();
    }

    partial void OnShowDailyOverviewChanged(bool value)
    {
        if (_suppress > 0)
            return;
        _store.Settings.ShowDailyOverview = value;
        _store.SaveSettings(_store.Settings);
        _store.UpdateTodayGoals(DateOnly.FromDateTime(DateTime.Now));
        UpdateRing(DateTimeOffset.Now, TodaySlices(DateTimeOffset.Now));
    }

    partial void OnCanEndSessionChanged(bool value) => EndSessionCommand.NotifyCanExecuteChanged();

    bool CanEnd() => CanEndSession;

    bool Resume(DateTimeOffset now)
    {
        var threshold = ThresholdSeconds;
        var action = _clock.TryResume(now, null, threshold);
        if (action != ClockAction.NeedReason)
            return action == ClockAction.Done;

        var reason = AskReason(endingSession: false);
        if (reason is null)
            return false;
        return _clock.TryResume(DateTimeOffset.Now, reason, threshold) == ClockAction.Done;
    }

    string? AskReason(bool endingSession)
    {
        var dialog = new ReasonDialog(endingSession)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(item => item.IsActive)
                    ?? Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.ReasonText : null;
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
            RemainingText = TimeMath.FormatCountdown(settings.SessionLengthMinutes * 60L);
            ElapsedFraction = 0;
            SessionStatus = "";
            PrimaryGlyph = "\uE768";
            PrimaryToolTip = "Start";
            CanToggleSession = !_clock.IsBreakActive && SelectedProject is not null;
        }
        else
        {
            var remaining = _clock.SessionRemainingSeconds(now);
            RemainingText = TimeMath.FormatCountdown(TimeMath.CeilSeconds(remaining));
            var elapsed = _clock.SessionElapsedSeconds(now);
            ElapsedFraction = session.PlannedSeconds <= 0 ? 0 : Math.Clamp(elapsed / (double)session.PlannedSeconds, 0, 1);
            SessionStatus = _clock.IsBreakActive ? "On break" : _clock.IsPaused ? "Paused" : "";
            PrimaryGlyph = _clock.IsRunning ? "\uE769" : "\uE768";
            PrimaryToolTip = _clock.IsRunning ? "Pause" : "Resume";
            CanToggleSession = !_clock.IsBreakActive;
        }

        CanEndSession = session is not null;
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
        RingSegments = ShowDailyOverview || SelectedProject is null
            ? DailyRingBuilder.BuildOverview(settings.Projects, slices, settings.DailyGoalHours)
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

    bool IsLive => _clock.State.Session is not null || _clock.State.Break is not null;

    int ThresholdSeconds => _store.Settings.ReasonThresholdMinutes * 60;
}
