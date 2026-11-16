using TimeTracker.Models;
using TimeTracker.Services;
using Xunit;

namespace TimeTracker.Tests;

public class HistoryTimelineBuilderTests
{
    [Fact]
    public void Timeline_lists_work_pauses_with_reasons_and_breaks_in_order()
    {
        var start = new DateTimeOffset(new DateTime(2026, 11, 16, 9, 0, 0), TimeSpan.FromHours(3.5));
        var day = new DayFile
        {
            Date = "2026-11-16",
            Slices =
            [
                Slice("alpha", "Alpha", "#3B82F6", start, start.AddMinutes(40)),
                Slice("beta", "Beta", "#F59E0B", start.AddMinutes(50), start.AddMinutes(80))
            ],
            Stops =
            [
                Stop("pause", "phone", start.AddMinutes(40), start.AddMinutes(45)),
                Stop("break", "lunch", start.AddMinutes(80), start.AddMinutes(95))
            ],
            Breaks =
            [
                new BreakEntry
                {
                    Reason = "lunch",
                    PlannedSeconds = 15 * 60,
                    Start = start.AddMinutes(80),
                    End = start.AddMinutes(95)
                }
            ]
        };

        var view = HistoryTimelineBuilder.Build(day, start.AddDays(1));

        Assert.Equal(4, view.Events.Count);
        Assert.Equal(new[] { HistoryEventKind.Work, HistoryEventKind.Pause, HistoryEventKind.Work, HistoryEventKind.Break },
            view.Events.Select(item => item.Kind));
        Assert.Equal("Alpha", view.Events[0].Title);
        Assert.Equal("Pause", view.Events[1].Title);
        Assert.Equal("phone", view.Events[1].Reason);
        Assert.Equal("Beta", view.Events[2].Title);
        Assert.Equal("Break", view.Events[3].Title);
        Assert.Equal("lunch", view.Events[3].Reason);
        Assert.Equal(2, view.Projects.Count);
        Assert.Equal("Alpha", view.Projects[0].Name);
        Assert.Equal(40 * 60, view.Projects[0].Seconds);
        Assert.Contains(view.BarSegments, item => item.IsGap);
        Assert.Equal(24 * 3600d, view.BarSegments.Sum(item => item.Seconds), 1);
    }

    [Fact]
    public void Today_timeline_bar_spans_twenty_four_hours()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var start = TimeMath.LocalDayStart(date).AddHours(9);
        var now = start.AddHours(1);
        var day = new DayFile
        {
            Date = date.ToString("yyyy-MM-dd"),
            Slices =
            [
                Slice("alpha", "Alpha", "#3B82F6", start, start.AddMinutes(30))
            ]
        };

        var view = HistoryTimelineBuilder.Build(day, now);
        Assert.Equal(24 * 3600d, view.BarSegments.Sum(item => item.Seconds), 1);
        Assert.True(view.BarSegments[0].IsGap);
        Assert.True(view.BarSegments[^1].IsGap);
        Assert.Contains(view.BarSegments, item => item.Color == "#3B82F6" && !item.IsGap);
    }

    [Fact]
    public void Standalone_break_appears_when_there_is_no_matching_stop()
    {
        var start = new DateTimeOffset(new DateTime(2026, 11, 16, 14, 0, 0), TimeSpan.FromHours(3.5));
        var day = new DayFile
        {
            Date = "2026-11-16",
            Breaks =
            [
                new BreakEntry
                {
                    Reason = "water",
                    PlannedSeconds = 30,
                    Start = start,
                    End = start.AddSeconds(30)
                }
            ]
        };

        var view = HistoryTimelineBuilder.Build(day, start.AddMinutes(1));
        var item = Assert.Single(view.Events);
        Assert.Equal(HistoryEventKind.Break, item.Kind);
        Assert.Equal("water", item.Reason);
        Assert.Equal(30, item.Seconds);
    }

    [Fact]
    public void Open_work_is_marked_in_progress_and_uses_now_as_the_end()
    {
        var start = new DateTimeOffset(new DateTime(2026, 11, 16, 10, 0, 0), TimeSpan.FromHours(3.5));
        var now = start.AddMinutes(12);
        var day = new DayFile
        {
            Date = "2026-11-16",
            Slices =
            [
                new WorkSlice
                {
                    ProjectId = "alpha",
                    Name = "Alpha",
                    Color = "#3B82F6",
                    Start = start,
                    Seconds = 12 * 60,
                    Open = true
                }
            ]
        };

        var item = Assert.Single(HistoryTimelineBuilder.Build(day, now).Events);
        Assert.True(item.Open);
        Assert.Contains("in progress", item.DurationText);
        Assert.EndsWith("now", item.TimeRangeText);
        Assert.Equal(12 * 60, item.Seconds);
    }

    static WorkSlice Slice(string id, string name, string color, DateTimeOffset start, DateTimeOffset end)
        => new()
        {
            ProjectId = id,
            Name = name,
            Color = color,
            Start = start,
            End = end,
            Seconds = TimeMath.SecondsBetween(start, end)
        };

    static StopEntry Stop(string kind, string reason, DateTimeOffset start, DateTimeOffset end)
        => new()
        {
            Kind = kind,
            Reason = reason,
            Start = start,
            End = end,
            Seconds = TimeMath.SecondsBetween(start, end)
        };
}
