using System.Globalization;
using TimeTracker.Models;

namespace TimeTracker.Services;

public static class HistoryTimelineBuilder
{
    public const string PauseColor = "#94A3B8";
    public const string BreakColor = "#8B5CF6";

    public static HistoryDayView Build(DayFile day, DateTimeOffset now)
    {
        if (!DateOnly.TryParse(day.Date, out var date))
            date = TimeMath.LocalDate(now);

        var events = BuildEvents(day, now);
        var projects = events
            .Where(item => item.Kind == HistoryEventKind.Work)
            .GroupBy(item => (item.ProjectId, item.Title, item.Color))
            .Select(group =>
            {
                var seconds = group.Sum(item => item.Seconds);
                return new HistoryProjectTotal
                {
                    ProjectId = group.Key.ProjectId,
                    Name = group.Key.Title,
                    Color = group.Key.Color,
                    Seconds = seconds,
                    DurationText = TimeMath.FormatShort(seconds)
                };
            })
            .OrderByDescending(item => item.Seconds)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var work = events.Where(item => item.Kind == HistoryEventKind.Work).Sum(item => item.Seconds);
        var stopped = events.Where(item => item.Kind != HistoryEventKind.Work).Sum(item => item.Seconds);
        return new HistoryDayView
        {
            DateText = FormatFullDate(date, TimeMath.LocalDate(now)),
            SummaryText = $"{TimeMath.FormatShort(work)} worked · {TimeMath.FormatShort(stopped)} stopped",
            Events = events,
            Projects = projects,
            BarSegments = BuildBar(events)
        };
    }

    public static string FormatListTitle(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue).ToString("ddd, d MMM yyyy", CultureInfo.CurrentCulture);

    public static string FormatListSubtitle(DayFile day, DateTimeOffset now)
    {
        var view = Build(day, now);
        var work = view.Events.Where(item => item.Kind == HistoryEventKind.Work).Sum(item => item.Seconds);
        var stopped = view.Events.Where(item => item.Kind != HistoryEventKind.Work).Sum(item => item.Seconds);
        var projects = view.Projects.Count;
        if (work > 0)
        {
            var projectLabel = projects == 1 ? "1 project" : $"{projects} projects";
            return $"{TimeMath.FormatShort(work)} · {projectLabel}";
        }

        return stopped > 0
            ? $"{TimeMath.FormatShort(stopped)} stopped"
            : "No activity";
    }

    public static string FormatBadge(DateOnly date, DateOnly today)
    {
        if (date == today)
            return "Today";
        if (date == today.AddDays(-1))
            return "Yesterday";
        return "";
    }

    public static string Stamp(DayFile day)
    {
        var slices = string.Join(",", day.Slices.Select(item => $"{item.Start.UtcTicks}:{item.Open}:{item.ProjectId}"));
        var stops = string.Join(",", day.Stops.Select(item => $"{item.Start.UtcTicks}:{item.Open}:{item.Kind}"));
        var breaks = string.Join(",", day.Breaks.Select(item => $"{item.Start.UtcTicks}:{item.Open}"));
        return slices + "|" + stops + "|" + breaks;
    }

    public static void RefreshTiming(HistoryEvent item, DateTimeOffset? end, long seconds, bool open)
    {
        item.End = end;
        item.Seconds = seconds;
        item.TimeRangeText = FormatRange(item.Start, end, open);
        item.DurationText = FormatDuration(seconds, open);
        item.ToolTip = FormatToolTip(item.Kind, item.Title, item.Reason, item.TimeRangeText, item.DurationText);
    }

    static List<HistoryEvent> BuildEvents(DayFile day, DateTimeOffset now)
    {
        var events = new List<HistoryEvent>();
        foreach (var slice in day.Slices)
        {
            var end = slice.End ?? (slice.Open ? now : slice.Start);
            var seconds = slice.Seconds > 0 ? slice.Seconds : TimeMath.SecondsBetween(slice.Start, end);
            if (seconds <= 0 && !slice.Open)
                continue;
            events.Add(Create(
                HistoryEventKind.Work,
                slice.ProjectId,
                string.IsNullOrWhiteSpace(slice.Name) ? "Project" : slice.Name,
                "",
                string.IsNullOrWhiteSpace(slice.Color) ? "#2EE59D" : slice.Color,
                slice.Start,
                slice.Open ? null : slice.End,
                seconds,
                slice.Open));
        }

        foreach (var stop in day.Stops)
        {
            var kind = string.Equals(stop.Kind, "break", StringComparison.OrdinalIgnoreCase)
                ? HistoryEventKind.Break
                : HistoryEventKind.Pause;
            var end = stop.End ?? (stop.Open ? now : stop.Start);
            var seconds = stop.Seconds > 0 ? stop.Seconds : TimeMath.SecondsBetween(stop.Start, end);
            if (seconds <= 0 && !stop.Open)
                continue;
            events.Add(Create(
                kind,
                "",
                kind == HistoryEventKind.Break ? "Break" : "Pause",
                stop.Reason?.Trim() ?? "",
                kind == HistoryEventKind.Break ? BreakColor : PauseColor,
                stop.Start,
                stop.Open ? null : stop.End,
                seconds,
                stop.Open));
        }

        foreach (var entry in day.Breaks)
        {
            if (CoveredByStop(entry, day.Stops))
                continue;

            var end = entry.End ?? (entry.Open ? now : entry.Start);
            var seconds = TimeMath.SecondsBetween(entry.Start, end);
            if (seconds <= 0 && !entry.Open)
                continue;
            events.Add(Create(
                HistoryEventKind.Break,
                "",
                "Break",
                entry.Reason?.Trim() ?? "",
                BreakColor,
                entry.Start,
                entry.Open ? null : entry.End,
                seconds,
                entry.Open));
        }

        return events
            .OrderBy(item => item.Start)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    static List<HistoryBarSegment> BuildBar(IReadOnlyList<HistoryEvent> events)
    {
        var segments = new List<HistoryBarSegment>();
        HistoryEvent? previous = null;
        foreach (var item in events)
        {
            if (previous is not null)
            {
                var previousEnd = previous.End ?? item.Start;
                var gap = TimeMath.SecondsBetween(previousEnd, item.Start);
                if (gap > 0)
                {
                    segments.Add(new HistoryBarSegment
                    {
                        Seconds = gap,
                        IsGap = true,
                        ToolTip = "Untracked " + TimeMath.FormatShort(gap)
                    });
                }
            }

            segments.Add(new HistoryBarSegment
            {
                Seconds = Math.Max(item.Seconds, 1),
                Color = item.Color,
                ToolTip = item.ToolTip
            });
            previous = item;
        }

        return segments;
    }

    static HistoryEvent Create(
        HistoryEventKind kind,
        string projectId,
        string title,
        string reason,
        string color,
        DateTimeOffset start,
        DateTimeOffset? end,
        long seconds,
        bool open)
    {
        var item = new HistoryEvent
        {
            Kind = kind,
            ProjectId = projectId,
            Title = title,
            Reason = reason,
            Color = color,
            Start = start,
            Open = open
        };
        RefreshTiming(item, end, seconds, open);
        return item;
    }

    static bool CoveredByStop(BreakEntry entry, IEnumerable<StopEntry> stops)
    {
        var entryEnd = entry.End ?? entry.Start;
        foreach (var stop in stops)
        {
            if (!string.Equals(stop.Kind, "break", StringComparison.OrdinalIgnoreCase))
                continue;
            var stopEnd = stop.End ?? stop.Start;
            if (stop.Start == entry.Start)
                return true;
            if (stop.Start < entryEnd && stopEnd > entry.Start)
                return true;
        }

        return false;
    }

    static string FormatFullDate(DateOnly date, DateOnly today)
    {
        var text = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
        var badge = FormatBadge(date, today);
        return string.IsNullOrEmpty(badge) ? text : $"{text} · {badge}";
    }

    static string FormatRange(DateTimeOffset start, DateTimeOffset? end, bool open)
    {
        var from = start.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        if (open || end is null)
            return from + " – now";
        return from + " – " + end.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    static string FormatDuration(long seconds, bool open)
    {
        var text = TimeMath.FormatShort(seconds);
        return open ? text + " · in progress" : text;
    }

    static string FormatToolTip(HistoryEventKind kind, string title, string reason, string range, string duration)
    {
        var label = kind == HistoryEventKind.Work ? title : title;
        if (!string.IsNullOrWhiteSpace(reason))
            return $"{label} {range} ({duration}) · {reason}";
        return $"{label} {range} ({duration})";
    }
}
