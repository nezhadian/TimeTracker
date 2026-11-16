using TimeTracker.Models;

namespace TimeTracker.Services;

public sealed class SessionClock
{
    readonly TimeStore _store;

    public SessionClock(TimeStore store)
    {
        _store = store;
        State = new LiveState();
    }

    public LiveState State { get; private set; }

    public bool AlarmDue => State.Break is { AlarmPending: true, AlarmPlays: < 2 };

    public bool IsRunning => State.Session?.SliceStart is not null;

    public bool IsPaused => State.Session?.PauseStart is not null;

    public bool IsBreakActive => State.Break is { End: null };

    public void Restore(DateTimeOffset now)
    {
        State = _store.LoadState();
        CatchUp(now);
        Persist(now);
    }

    public void CatchUp(DateTimeOffset now)
    {
        FinishBreakIfDue(now);
        AdvanceSession(now);
    }

    public ClockAction StartSession(Project project, int plannedSeconds, DateTimeOffset now)
    {
        if (State.Session is not null || State.Break is not null)
            return ClockAction.Ignored;
        if (plannedSeconds < 1)
            return ClockAction.Ignored;

        State.Session = new LiveSession
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectColor = project.Color,
            PlannedSeconds = plannedSeconds,
            StartedAt = now,
            SliceStart = now
        };
        Persist(now);
        return ClockAction.Done;
    }

    public ClockAction Pause(DateTimeOffset now)
    {
        if (State.Session?.SliceStart is null)
            return ClockAction.Ignored;

        CatchUp(now);
        if (State.Session?.SliceStart is null)
            return ClockAction.Done;

        CloseOpenSlice(now);
        State.Session.PauseStart = now;
        State.Session.PauseKind = "pause";
        State.Session.PauseReason = "";
        State.Session.ReasonCaptured = false;
        Persist(now);
        return ClockAction.Done;
    }

    public ClockAction TryResume(DateTimeOffset now, string? reason, int thresholdSeconds)
    {
        var session = State.Session;
        if (session?.PauseStart is null || IsBreakActive)
            return ClockAction.Ignored;

        if (NeedsReason(session, now, thresholdSeconds))
        {
            if (string.IsNullOrWhiteSpace(reason))
                return ClockAction.NeedReason;
            session.PauseReason = reason.Trim();
            session.ReasonCaptured = true;
        }

        CloseOpenPause(now);
        session.SliceStart = now;
        session.PauseKind = null;
        session.PauseReason = null;
        session.ReasonCaptured = false;
        Persist(now);
        return ClockAction.Done;
    }

    public ClockAction EndSession(DateTimeOffset now, string? reason, int thresholdSeconds)
    {
        var session = State.Session;
        if (session is null)
            return ClockAction.Ignored;

        if (session.SliceStart is not null)
        {
            CatchUp(now);
            if (State.Session?.SliceStart is not null)
                CloseOpenSlice(now);
            State.Session = null;
            Persist(now);
            return ClockAction.Done;
        }

        if (session.PauseStart is not null)
        {
            if (NeedsReason(session, now, thresholdSeconds))
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return ClockAction.NeedReason;
                session.PauseReason = reason.Trim();
                session.ReasonCaptured = true;
            }

            CloseOpenPause(now);
        }

        State.Session = null;
        Persist(now);
        return ClockAction.Done;
    }

    public void MoveSessionToProject(Project project, DateTimeOffset now)
    {
        var session = State.Session;
        if (session is null || session.ProjectId == project.Id)
            return;

        var began = SessionBegan(session, now);
        ReassignSessionSlices(project, began);
        session.ProjectId = project.Id;
        session.ProjectName = project.Name;
        session.ProjectColor = project.Color;
        Persist(now);
    }

    public void RestartSessionOnProject(Project project, DateTimeOffset now)
    {
        var session = State.Session;
        if (session is null)
            return;

        var planned = session.PlannedSeconds;
        var onBreak = IsBreakActive;
        if (session.SliceStart is not null)
            CloseOpenSlice(now);
        if (State.Session?.PauseStart is not null)
            CloseOpenPause(now);

        State.Session = new LiveSession
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectColor = project.Color,
            PlannedSeconds = planned,
            StartedAt = now,
            SliceStart = onBreak ? null : now,
            PauseStart = onBreak ? now : null,
            PauseKind = onBreak ? "break" : null,
            PauseReason = onBreak ? State.Break?.Reason : null,
            ReasonCaptured = onBreak
        };
        Persist(now);
    }

    public ClockAction StartBreak(int plannedSeconds, string reason, DateTimeOffset now)
    {
        if (State.Break is not null || plannedSeconds < 1)
            return ClockAction.Ignored;

        reason = reason.Trim();
        var session = State.Session;
        if (session?.SliceStart is not null)
        {
            CatchUp(now);
            if (State.Session?.SliceStart is not null)
                CloseOpenSlice(now);
            if (State.Session is not null)
            {
                State.Session.PauseStart = now;
                State.Session.PauseKind = "break";
                State.Session.PauseReason = reason;
                State.Session.ReasonCaptured = true;
            }
        }
        else if (session?.PauseStart is not null)
        {
            session.PauseKind = "break";
            session.PauseReason = reason;
            session.ReasonCaptured = true;
        }

        State.Break = new LiveBreak
        {
            Reason = reason,
            PlannedSeconds = plannedSeconds,
            Start = now
        };
        Persist(now);
        return ClockAction.Done;
    }

    public ClockAction CancelBreak(DateTimeOffset now)
    {
        if (State.Break is not { End: null })
            return ClockAction.Ignored;

        FinishBreak(now, natural: false);
        AdvanceSession(now);
        Persist(now);
        return ClockAction.Done;
    }

    public void MarkAlarmPlayed(DateTimeOffset now)
    {
        var live = State.Break;
        if (live is not { AlarmPending: true })
            return;

        live.AlarmPlays++;
        ReplaceBreak(ToBreak(live, open: false));
        if (live.AlarmPlays >= 2)
        {
            live.AlarmPending = false;
            State.Break = null;
        }

        _store.SaveState(State);
        _ = now;
    }

    public long SessionElapsedSeconds(DateTimeOffset now)
    {
        var session = State.Session;
        if (session is null)
            return 0;
        var open = session.SliceStart is DateTimeOffset start ? TimeMath.SecondsBetween(start, now) : 0;
        return session.CompletedSeconds + open;
    }

    public long SessionRemainingSeconds(DateTimeOffset now)
    {
        var session = State.Session;
        if (session is null)
            return 0;
        var remaining = session.PlannedSeconds - SessionElapsedSeconds(now);
        return remaining > 0 ? remaining : 0;
    }

    public long BreakRemainingSeconds(DateTimeOffset now)
    {
        if (State.Break is not { End: null } live)
            return 0;
        var elapsed = TimeMath.SecondsBetween(live.Start, now);
        var remaining = live.PlannedSeconds - elapsed;
        return remaining > 0 ? remaining : 0;
    }

    public WorkSlice? CurrentOpenSlice(DateTimeOffset now)
    {
        if (State.Session?.SliceStart is not DateTimeOffset start)
            return null;
        ApplyProjectMeta(State.Session);
        return MakeSlice(State.Session, start, now, open: true);
    }

    public StopEntry? CurrentOpenStop(DateTimeOffset now)
    {
        if (State.Session?.PauseStart is not DateTimeOffset start)
            return null;
        return MakeStop(State.Session, start, now, open: true);
    }

    public void Persist(DateTimeOffset now)
    {
        _store.SaveState(State);
        FlushOpen(now);
    }

    public void FlushOpen(DateTimeOffset now)
    {
        if (CurrentOpenSlice(now) is { } slice)
            UpsertOpenSlice(slice);
        else
            ClearOpenSlices(now);

        if (CurrentOpenStop(now) is { } stop)
            UpsertOpenStop(stop);
        else
            ClearOpenStops(now);

        if (State.Break is { End: null } live)
            UpsertOpenBreak(ToBreak(live, open: true));
    }

    void FinishBreakIfDue(DateTimeOffset now)
    {
        if (State.Break is not { End: null } live)
            return;
        var plannedEnd = live.Start.AddSeconds(live.PlannedSeconds);
        if (now >= plannedEnd)
            FinishBreak(plannedEnd, natural: true);
    }

    void FinishBreak(DateTimeOffset end, bool natural)
    {
        var live = State.Break;
        if (live is null)
            return;

        live.End = end;
        live.AlarmPending = natural;
        if (!natural)
            live.AlarmPending = false;

        var session = State.Session;
        if (session?.PauseStart is not null && session.PauseKind == "break")
        {
            CloseOpenPause(end);
            var remaining = session.PlannedSeconds - session.CompletedSeconds;
            if (remaining > 0)
                session.SliceStart = end;
            else
                State.Session = null;
        }

        ReplaceBreak(ToBreak(live, open: false));
        if (!live.AlarmPending)
            State.Break = null;
    }

    void AdvanceSession(DateTimeOffset now)
    {
        var guard = 0;
        while (State.Session?.SliceStart is DateTimeOffset start && guard++ < 8)
        {
            var session = State.Session;
            var remaining = session.PlannedSeconds - session.CompletedSeconds;
            if (remaining <= 0)
            {
                State.Session = null;
                return;
            }

            var hardEnd = start.AddSeconds(remaining);
            var midnight = TimeMath.NextLocalMidnight(start);
            DateTimeOffset? closeAt = null;
            if (midnight < hardEnd && midnight <= now)
                closeAt = midnight;
            else if (hardEnd <= now)
                closeAt = hardEnd;

            if (closeAt is not DateTimeOffset end)
                return;

            CloseSlice(start, end);
            if (end >= hardEnd)
            {
                State.Session = null;
                return;
            }

            session.SliceStart = end;
        }
    }

    void CloseOpenSlice(DateTimeOffset now)
    {
        if (State.Session?.SliceStart is not DateTimeOffset start)
            return;
        var remaining = State.Session.PlannedSeconds - State.Session.CompletedSeconds;
        var hardEnd = start.AddSeconds(Math.Max(0, remaining));
        var end = now < hardEnd ? now : hardEnd;
        if (end > start)
            CloseSlice(start, end);
        State.Session.SliceStart = null;
    }

    void CloseSlice(DateTimeOffset start, DateTimeOffset end)
    {
        var session = State.Session;
        if (session is null)
            return;
        ApplyProjectMeta(session);
        var slice = MakeSlice(session, start, end, open: false);
        if (slice.Seconds <= 0)
            return;
        session.CompletedSeconds += slice.Seconds;
        RemoveOpenSlice(TimeMath.LocalDate(start));
        AppendSlice(slice);
    }

    void CloseOpenPause(DateTimeOffset end)
    {
        var session = State.Session;
        if (session?.PauseStart is not DateTimeOffset start)
            return;

        var cursor = start;
        var guard = 0;
        while (cursor < end && guard++ < 8)
        {
            var midnight = TimeMath.NextLocalMidnight(cursor);
            var partEnd = midnight < end ? midnight : end;
            var stop = MakeStop(session, cursor, partEnd, open: false);
            if (stop.Seconds > 0)
            {
                RemoveOpenStop(TimeMath.LocalDate(cursor));
                AppendStop(stop);
            }
            cursor = partEnd;
        }

        session.PauseStart = null;
        session.PauseKind = null;
        session.PauseReason = null;
        session.ReasonCaptured = false;
    }

    static bool NeedsReason(LiveSession session, DateTimeOffset now, int thresholdSeconds)
    {
        if (session.ReasonCaptured || session.PauseStart is null)
            return false;
        return TimeMath.SecondsBetween(session.PauseStart.Value, now) > thresholdSeconds;
    }

    static DateTimeOffset SessionBegan(LiveSession session, DateTimeOffset now)
        => session.StartedAt != default ? session.StartedAt : session.SliceStart ?? session.PauseStart ?? now;

    void ReassignSessionSlices(Project project, DateTimeOffset began)
    {
        var dates = new[] { TimeMath.LocalDate(began), TimeMath.LocalDate(DateTimeOffset.Now) };
        foreach (var date in dates.Distinct())
        {
            if (!_store.DayExists(date))
                continue;

            var day = _store.LoadDay(date);
            var changed = false;
            foreach (var slice in day.Slices)
            {
                if (slice.Start < began)
                    continue;
                slice.ProjectId = project.Id;
                slice.Name = project.Name;
                slice.Color = project.Color;
                changed = true;
            }

            if (changed)
                _store.SaveDay(day);
        }
    }

    void ApplyProjectMeta(LiveSession session)
    {
        var project = _store.Settings.FindProject(session.ProjectId);
        if (project is null)
            return;
        session.ProjectName = project.Name;
        session.ProjectColor = project.Color;
    }

    static WorkSlice MakeSlice(LiveSession session, DateTimeOffset start, DateTimeOffset end, bool open)
        => new()
        {
            ProjectId = session.ProjectId,
            Name = session.ProjectName,
            Color = session.ProjectColor,
            Start = start,
            End = open ? null : end,
            Seconds = TimeMath.SecondsBetween(start, end),
            Open = open
        };

    static StopEntry MakeStop(LiveSession session, DateTimeOffset start, DateTimeOffset end, bool open)
        => new()
        {
            Kind = string.IsNullOrEmpty(session.PauseKind) ? "pause" : session.PauseKind,
            Reason = session.PauseReason?.Trim() ?? "",
            Start = start,
            End = open ? null : end,
            Seconds = TimeMath.SecondsBetween(start, end),
            Open = open
        };

    static BreakEntry ToBreak(LiveBreak live, bool open)
        => new()
        {
            Reason = live.Reason,
            PlannedSeconds = live.PlannedSeconds,
            Start = live.Start,
            End = open ? null : live.End,
            AlarmPlays = live.AlarmPlays,
            Open = open
        };

    void UpsertOpenSlice(WorkSlice slice)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(slice.Start));
        day.Slices.RemoveAll(item => item.Open);
        day.Slices.Add(slice);
        _store.SaveDay(day);
    }

    void UpsertOpenStop(StopEntry stop)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(stop.Start));
        day.Stops.RemoveAll(item => item.Open);
        day.Stops.Add(stop);
        _store.SaveDay(day);
    }

    void UpsertOpenBreak(BreakEntry entry)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(entry.Start));
        day.Breaks.RemoveAll(item => item.Open);
        day.Breaks.Add(entry);
        _store.SaveDay(day);
    }

    void ClearOpenSlices(DateTimeOffset now) => ClearOpen(now, day => day.Slices.RemoveAll(slice => slice.Open) > 0);

    void ClearOpenStops(DateTimeOffset now) => ClearOpen(now, day => day.Stops.RemoveAll(stop => stop.Open) > 0);

    void ClearOpen(DateTimeOffset now, Func<DayFile, bool> remove)
    {
        var today = TimeMath.LocalDate(now);
        foreach (var date in new[] { today, today.AddDays(-1) })
        {
            if (!_store.DayExists(date))
                continue;
            var day = _store.LoadDay(date);
            if (remove(day))
                _store.SaveDay(day);
        }
    }

    void RemoveOpenSlice(DateOnly date)
    {
        var day = _store.LoadDay(date);
        if (day.Slices.RemoveAll(slice => slice.Open) > 0)
            _store.SaveDay(day);
    }

    void RemoveOpenStop(DateOnly date)
    {
        var day = _store.LoadDay(date);
        if (day.Stops.RemoveAll(stop => stop.Open) > 0)
            _store.SaveDay(day);
    }

    void AppendSlice(WorkSlice slice)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(slice.Start));
        day.Slices.Add(slice);
        _store.SaveDay(day);
    }

    void AppendStop(StopEntry stop)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(stop.Start));
        day.Stops.Add(stop);
        _store.SaveDay(day);
    }

    void ReplaceBreak(BreakEntry entry)
    {
        var day = _store.LoadDay(TimeMath.LocalDate(entry.Start));
        var index = day.Breaks.FindIndex(item => item.Start == entry.Start);
        if (index >= 0)
            day.Breaks[index] = entry;
        else
        {
            day.Breaks.RemoveAll(item => item.Open);
            day.Breaks.Add(entry);
        }
        _store.SaveDay(day);
    }
}
