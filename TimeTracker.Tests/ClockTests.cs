using System.IO;
using TimeTracker.Models;
using TimeTracker.Services;
using Xunit;

namespace TimeTracker.Tests;

public class ClockTests : IDisposable
{
    readonly string _folder;
    readonly TimeStore _store;
    readonly SessionClock _clock;
    readonly Project _alpha;
    readonly Project _beta;

    public ClockTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "timetracker-tests", Guid.NewGuid().ToString("N"));
        _store = new TimeStore();
        _alpha = new Project { Id = "alpha", Name = "Alpha", Color = "#3B82F6", DailyGoalHours = 4 };
        _beta = new Project { Id = "beta", Name = "Beta", Color = "#F59E0B" };
        _store.UseFolder(_folder, new TrackerSettings
        {
            SessionLengthMinutes = 40,
            ReasonThresholdMinutes = 3,
            DailyGoalHours = 12,
            Projects = [_alpha, _beta]
        }, rememberPath: false);
        _clock = new SessionClock(_store);
    }

    [Fact]
    public void Remaining_comes_from_timestamps()
    {
        var start = Local(10, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.CatchUp(start.AddSeconds(90));

        Assert.Equal(40 * 60 - 90, _clock.SessionRemainingSeconds(start.AddSeconds(90)));
        Assert.True(_clock.IsRunning);
    }

    [Fact]
    public void Short_pause_saves_an_empty_reason_and_does_not_count_as_work()
    {
        var start = Local(10, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.Pause(start.AddMinutes(10));
        var resumed = _clock.TryResume(start.AddMinutes(12), null, thresholdSeconds: 180);

        Assert.Equal(ClockAction.Done, resumed);
        _clock.CatchUp(start.AddMinutes(12).AddSeconds(5));
        Assert.Equal(10 * 60 + 5, _clock.SessionElapsedSeconds(start.AddMinutes(12).AddSeconds(5)));

        var day = _store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime));
        var stop = Assert.Single(day.Stops.Where(item => !item.Open));
        Assert.Equal("pause", stop.Kind);
        Assert.Equal("", stop.Reason);
        Assert.Equal(120, stop.Seconds);
    }

    [Fact]
    public void Long_pause_blocks_resume_until_a_reason_is_saved()
    {
        var start = Local(10, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.Pause(start.AddMinutes(1));
        var blocked = _clock.TryResume(start.AddMinutes(5), null, thresholdSeconds: 180);
        Assert.Equal(ClockAction.NeedReason, blocked);
        Assert.True(_clock.IsPaused);
        Assert.Equal(60, _clock.SessionElapsedSeconds(start.AddMinutes(5)));

        var resumed = _clock.TryResume(start.AddMinutes(5).AddSeconds(30), "phone", thresholdSeconds: 180);
        Assert.Equal(ClockAction.Done, resumed);
        var stop = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Stops, item => !item.Open);
        Assert.Equal("phone", stop.Reason);
        Assert.Equal(270, stop.Seconds);
    }

    [Fact]
    public void Threshold_boundary_is_strictly_longer_than_the_setting()
    {
        var start = Local(9, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.Pause(start.AddMinutes(1));

        Assert.Equal(ClockAction.Done, _clock.TryResume(start.AddMinutes(4), null, thresholdSeconds: 180));

        _clock.Pause(start.AddMinutes(4));
        Assert.Equal(ClockAction.NeedReason, _clock.TryResume(start.AddMinutes(4).AddSeconds(181), " ", thresholdSeconds: 180));
        Assert.Equal(ClockAction.Done, _clock.TryResume(start.AddMinutes(4).AddSeconds(181), "stretch", thresholdSeconds: 180));
    }

    [Fact]
    public void Zero_threshold_asks_after_every_measured_stop()
    {
        var start = Local(9, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.Pause(start);
        Assert.Equal(ClockAction.NeedReason, _clock.TryResume(start.AddSeconds(1), null, thresholdSeconds: 0));
        Assert.Equal(ClockAction.Done, _clock.TryResume(start.AddSeconds(1), "any", thresholdSeconds: 0));
    }

    [Fact]
    public void Moving_a_session_gives_the_elapsed_time_to_the_new_project()
    {
        var start = Local(11, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.MoveSessionToProject(_beta, start.AddSeconds(100));
        _clock.Pause(start.AddSeconds(100));

        var slice = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Slices, item => !item.Open);
        Assert.Equal("beta", slice.ProjectId);
        Assert.Equal(100, slice.Seconds);
        Assert.Equal(40 * 60 - 100, _clock.SessionRemainingSeconds(start.AddSeconds(100)));
    }

    [Fact]
    public void Restarting_on_a_project_keeps_the_old_time_and_resets_the_clock()
    {
        var start = Local(11, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.RestartSessionOnProject(_beta, start.AddSeconds(100));

        Assert.Equal("beta", _clock.State.Session!.ProjectId);
        Assert.Equal(40 * 60, _clock.SessionRemainingSeconds(start.AddSeconds(100)));
        var kept = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Slices, item => !item.Open);
        Assert.Equal(("alpha", 100L), (kept.ProjectId, kept.Seconds));
    }

    [Fact]
    public void Hitting_zero_keeps_only_the_planned_time()
    {
        var start = Local(8, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.CatchUp(start.AddHours(2));
        _clock.Persist(start.AddHours(2));

        Assert.Null(_clock.State.Session);
        var slice = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Slices);
        Assert.Equal(2400, slice.Seconds);
        Assert.False(slice.Open);
        Assert.Equal(start.AddMinutes(40), slice.End);
    }

    [Fact]
    public void Ending_early_keeps_only_the_time_actually_run()
    {
        var start = Local(8, 30);
        _clock.StartSession(_alpha, 40 * 60, start);
        Assert.Equal(ClockAction.Done, _clock.EndSession(start.AddMinutes(5), null, thresholdSeconds: 180));
        var slice = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Slices);
        Assert.Equal(300, slice.Seconds);
    }

    [Fact]
    public void Session_crossing_midnight_is_split_across_the_two_days()
    {
        var start = new DateTimeOffset(DateTime.Today.AddHours(23).AddMinutes(50));
        _clock.StartSession(_alpha, 40 * 60, start);
        var later = new DateTimeOffset(DateTime.Today.AddDays(1).AddMinutes(20));
        _clock.CatchUp(later);
        _clock.Persist(later);

        Assert.True(_clock.IsRunning);
        Assert.Equal(10 * 60, _clock.SessionRemainingSeconds(later));
        var first = _store.LoadDay(DateOnly.FromDateTime(DateTime.Today));
        var second = _store.LoadDay(DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        Assert.Equal(10 * 60, first.Slices.Where(slice => !slice.Open).Sum(slice => slice.Seconds));
        Assert.Equal(20 * 60, second.Slices.Sum(slice => slice.Seconds));
    }

    [Fact]
    public void Break_pauses_the_session_and_resume_does_not_ask_again()
    {
        var start = Local(14, 0);
        _clock.StartSession(_alpha, 10 * 60, start);
        _clock.StartBreak(60, "coffee", start.AddSeconds(10));
        Assert.True(_clock.IsPaused);
        Assert.True(_clock.IsBreakActive);
        Assert.Equal(ClockAction.Ignored, _clock.TryResume(start.AddSeconds(20), null, thresholdSeconds: 0));

        _clock.CatchUp(start.AddSeconds(70));
        Assert.False(_clock.IsBreakActive);
        Assert.True(_clock.IsRunning);
        Assert.True(_clock.AlarmDue);
        Assert.Equal(10, _clock.SessionElapsedSeconds(start.AddSeconds(70)));

        var day = _store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime));
        var stop = Assert.Single(day.Stops.Where(item => !item.Open));
        Assert.Equal("break", stop.Kind);
        Assert.Equal("coffee", stop.Reason);
        Assert.Equal(60, stop.Seconds);
    }

    [Fact]
    public void Cancelling_a_break_does_not_arm_the_alarm()
    {
        var start = Local(15, 0);
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.StartBreak(300, "walk", start.AddMinutes(1));
        _clock.CancelBreak(start.AddMinutes(2));

        Assert.False(_clock.AlarmDue);
        Assert.True(_clock.IsRunning);
        Assert.Null(_clock.State.Break);
        var entry = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Breaks);
        Assert.Equal(0, entry.AlarmPlays);
        Assert.False(entry.Open);
        Assert.NotNull(entry.End);
    }

    [Fact]
    public void Alarm_play_count_stops_at_two_including_a_restart()
    {
        var start = Local(16, 0);
        _clock.StartBreak(30, "water", start);
        _clock.CatchUp(start.AddSeconds(30));
        Assert.True(_clock.AlarmDue);

        _clock.MarkAlarmPlayed(start.AddSeconds(31));
        Assert.True(_clock.AlarmDue);
        _clock.MarkAlarmPlayed(start.AddSeconds(32));
        Assert.False(_clock.AlarmDue);
        _clock.MarkAlarmPlayed(start.AddSeconds(33));

        var saved = Assert.Single(_store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Breaks);
        Assert.Equal(2, saved.AlarmPlays);

        var restarted = new SessionClock(_store);
        restarted.Restore(start.AddMinutes(5));
        Assert.False(restarted.AlarmDue);
    }

    [Fact]
    public void Restart_while_running_restores_the_countdown_and_the_day_file()
    {
        var start = Local(7, 15);
        _clock.StartSession(_alpha, 40 * 60, start);
        var open = _store.LoadDay(DateOnly.FromDateTime(start.LocalDateTime)).Slices.Single(slice => slice.Open);
        Assert.Equal("alpha", open.ProjectId);

        var restarted = new SessionClock(_store);
        var later = start.AddMinutes(3);
        restarted.Restore(later);

        Assert.True(restarted.IsRunning);
        Assert.Equal(37 * 60, restarted.SessionRemainingSeconds(later));
        Assert.Equal(180, restarted.CurrentOpenSlice(later)!.Seconds);
    }

    [Fact]
    public void Stop_crossing_midnight_is_split_when_it_closes()
    {
        var start = new DateTimeOffset(DateTime.Today.AddHours(23).AddMinutes(50));
        _clock.StartSession(_alpha, 40 * 60, start);
        _clock.Pause(start.AddMinutes(1));
        var resumeAt = new DateTimeOffset(DateTime.Today.AddDays(1).AddMinutes(20));
        Assert.Equal(ClockAction.Done, _clock.TryResume(resumeAt, "late", thresholdSeconds: 180));

        var first = _store.LoadDay(DateOnly.FromDateTime(DateTime.Today));
        var second = _store.LoadDay(DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        Assert.Equal(9 * 60, Assert.Single(first.Stops).Seconds);
        Assert.Equal(20 * 60, Assert.Single(second.Stops).Seconds);
        Assert.All(first.Stops.Concat(second.Stops), stop => Assert.Equal("late", stop.Reason));
    }

    [Fact]
    public void Fresh_settings_contain_only_the_default_project()
    {
        var folder = Path.Combine(_folder, "fresh");
        var store = new TimeStore();
        store.UseFolder(folder, TrackerSettings.CreateDefault(), rememberPath: false);
        var project = Assert.Single(store.Settings.Projects);
        Assert.Equal("Default", project.Name);
        Assert.Equal("#2EE59D", project.Color);
        Assert.Null(project.DailyGoalHours);
        Assert.Equal(40, store.Settings.SessionLengthMinutes);
        Assert.Equal(3, store.Settings.ReasonThresholdMinutes);
        Assert.Equal(12, store.Settings.DailyGoalHours);
        Assert.False(store.Settings.ShowDailyOverview);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    static DateTimeOffset Local(int hour, int minute)
        => new(DateTime.Today.AddHours(hour).AddMinutes(minute));
}
