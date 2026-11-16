using System.IO;
using TimeTracker.Models;
using TimeTracker.Services;
using Xunit;

namespace TimeTracker.Tests;

public class RingAndStreakTests
{
    [Fact]
    public void Overview_cuts_project_work_then_project_target_then_daily_remainder()
    {
        var alpha = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6", DailyGoalHours = 4 };
        var beta = new Project { Id = "b", Name = "Beta", Color = "#F59E0B", DailyGoalHours = 3 };
        var gamma = new Project { Id = "c", Name = "Gamma", Color = "#2EE59D" };
        var slices = new List<WorkSlice>
        {
            Slice("a", 5 * 3600),
            Slice("b", 3600),
            Slice("c", 2 * 3600)
        };

        var segments = DailyRingBuilder.BuildOverview([alpha, beta, gamma], slices, dailyGoalHours: 12);

        Assert.Equal(
            [
                ("Alpha", "#3B82F6", 5 * 3600d),
                ("Beta", "#F59E0B", 3600d),
                ("", DailyRingBuilder.ProjectRemainHex, 2 * 3600d),
                ("Gamma", "#2EE59D", 2 * 3600d),
                ("", DailyRingBuilder.DailyRemainHex, 2 * 3600d)
            ],
            segments.Select(segment => (segment.Label, segment.StrokeHex, segment.Seconds)).ToArray());
        Assert.Equal(12 * 3600d, segments.Sum(segment => segment.Seconds), 0.1);
        Assert.DoesNotContain(segments, segment => segment.Label.Contains("remained", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Overview_scales_past_a_full_day_and_drops_the_daily_remainder()
    {
        var alpha = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6", DailyGoalHours = 10 };
        var beta = new Project { Id = "b", Name = "Beta", Color = "#F59E0B", DailyGoalHours = 10 };
        var segments = DailyRingBuilder.BuildOverview(
            [alpha, beta],
            [Slice("a", 10 * 3600), Slice("b", 10 * 3600)],
            dailyGoalHours: 12);

        Assert.Equal(20 * 3600d, segments.Sum(segment => segment.Seconds), 0.1);
        Assert.DoesNotContain(segments, segment => segment.Label == DailyRingBuilder.RemainedDailyLabel);
    }

    [Fact]
    public void Project_without_a_goal_has_no_gray_remainder_slice()
    {
        var project = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6" };
        var segments = DailyRingBuilder.BuildOverview([project], [Slice("a", 2 * 3600)], 12);
        Assert.Equal(["Alpha", ""], segments.Select(segment => segment.Label).ToArray());
        Assert.Equal(DailyRingBuilder.DailyRemainHex, segments[1].StrokeHex);
        Assert.DoesNotContain(segments, segment => segment.Label.Contains("remained", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Overview_uses_only_the_projects_passed_in()
    {
        var alpha = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6", DailyGoalHours = 4 };
        var beta = new Project { Id = "b", Name = "Beta", Color = "#F59E0B", DailyGoalHours = 3 };
        var slices = new List<WorkSlice>
        {
            Slice("a", 3600),
            Slice("b", 3600)
        };

        var segments = DailyRingBuilder.BuildOverview([beta], slices.Where(slice => slice.ProjectId == "b").ToList(), 12);

        Assert.DoesNotContain(segments, segment => segment.Label == "Alpha");
        Assert.Contains(segments, segment => segment.Label == "Beta");
        Assert.Equal(12 * 3600d, segments.Sum(segment => segment.Seconds), 0.1);
    }

    [Fact]
    public void Selected_ring_uses_the_project_goal_when_it_has_one()
    {
        var project = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6", DailyGoalHours = 2 };
        var under = DailyRingBuilder.BuildSelected(project, 3600, 12);
        Assert.Equal([( "#3B82F6", 3600d), ("#E5E7EB", 3600d)], under.Select(segment => (segment.StrokeHex, segment.Seconds)).ToArray());

        var over = DailyRingBuilder.BuildSelected(project, 5 * 3600, 12);
        var only = Assert.Single(over);
        Assert.Equal(2 * 3600d, only.Seconds);
        Assert.Equal("#3B82F6", only.StrokeHex);
    }

    [Fact]
    public void Selected_ring_without_a_project_goal_uses_the_daily_goal()
    {
        var project = new Project { Id = "a", Name = "Alpha", Color = "#3B82F6" };
        var segments = DailyRingBuilder.BuildSelected(project, 3 * 3600, 12);
        Assert.Equal(12 * 3600d, segments.Sum(segment => segment.Seconds), 0.1);
        Assert.Equal(3 * 3600d, segments[0].Seconds);
    }

    [Fact]
    public void Streak_counts_today_only_after_the_goal_and_stops_at_a_gap()
    {
        var folder = Path.Combine(Path.GetTempPath(), "timetracker-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new TimeStore();
            var settings = TrackerSettings.CreateDefault();
            settings.DailyGoalHours = 1;
            store.UseFolder(folder, settings, rememberPath: false);
            var today = DateOnly.FromDateTime(DateTime.Today);
            WriteWork(store, today.AddDays(-1), 2 * 3600);
            WriteWork(store, today.AddDays(-2), 2 * 3600);

            Assert.Equal(2, store.ComputeStreak(today, todayWorkSeconds: 0, todayGoalHours: 1));
            Assert.Equal(3, store.ComputeStreak(today, todayWorkSeconds: 3600, todayGoalHours: 1));

            var yesterday = store.LoadDay(today.AddDays(-1));
            yesterday.Slices[0].Seconds = 60;
            store.SaveDay(yesterday);
            Assert.Equal(0, store.ComputeStreak(today, todayWorkSeconds: 0, todayGoalHours: 1));
            Assert.Equal(1, store.ComputeStreak(today, todayWorkSeconds: 3600, todayGoalHours: 1));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Beep_wav_is_a_playable_wave_header()
    {
        var wav = AlarmPlayer.CreateBeepWav(880, 380);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.True(wav.Length > 1000);
    }

    static WorkSlice Slice(string projectId, long seconds) => new()
    {
        ProjectId = projectId,
        Name = projectId,
        Color = "#000000",
        Seconds = seconds
    };

    static void WriteWork(TimeStore store, DateOnly date, long seconds)
    {
        var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeZoneInfo.Local.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));
        var day = store.LoadDay(date);
        day.Slices.Add(new WorkSlice
        {
            ProjectId = "p",
            Name = "Default",
            Color = "#2EE59D",
            Start = start,
            End = start.AddSeconds(seconds),
            Seconds = seconds
        });
        store.SaveDay(day);
    }
}
