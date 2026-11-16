using TimeTracker.Models;

namespace TimeTracker.Services;

public static class DailyRingBuilder
{
    public const string RemainedDailyLabel = "remained daily";
    const string LightGray = "#E5E7EB";
    const string LightLabel = "#6B7280";
    const string DarkGray = "#4B5563";

    public static IReadOnlyList<RingSegment> BuildOverview(
        IReadOnlyList<Project> projects,
        IReadOnlyList<WorkSlice> todaySlices,
        double dailyGoalHours)
    {
        var worked = todaySlices
            .GroupBy(slice => slice.ProjectId)
            .ToDictionary(group => group.Key, group => group.Sum(slice => (double)slice.Seconds));

        var segments = new List<RingSegment>();
        double used = 0;
        var seen = new HashSet<string>();

        foreach (var project in projects)
        {
            seen.Add(project.Id);
            var seconds = worked.GetValueOrDefault(project.Id);
            if (project.DailyGoalHours is double goalHours && goalHours > 0)
            {
                var goalSeconds = goalHours * 3600d;
                if (seconds > 0)
                    segments.Add(ProjectArc(project.Name, project.Color, seconds));
                var gray = Math.Max(0, goalSeconds - seconds);
                if (gray > 0)
                    segments.Add(RemainderArc($"{project.Name} remained", gray));
                used += seconds + gray;
            }
            else if (seconds > 0)
            {
                segments.Add(ProjectArc(project.Name, project.Color, seconds));
                used += seconds;
            }
        }

        foreach (var extra in todaySlices.Where(slice => !seen.Contains(slice.ProjectId)).GroupBy(slice => slice.ProjectId))
        {
            var seconds = extra.Sum(slice => (double)slice.Seconds);
            if (seconds <= 0)
                continue;
            var sample = extra.Last();
            segments.Add(ProjectArc(sample.Name, sample.Color, seconds));
            used += seconds;
        }

        var remained = Math.Max(0, dailyGoalHours * 3600d - used);
        if (remained > 0)
            segments.Add(new RingSegment(RemainedDailyLabel, DarkGray, remained, DarkGray, true));

        return segments;
    }

    public static IReadOnlyList<RingSegment> BuildSelected(Project project, double workedSeconds, double dailyGoalHours)
    {
        if (project.DailyGoalHours is double goalHours && goalHours > 0)
        {
            var goalSeconds = goalHours * 3600d;
            var colored = Math.Min(Math.Max(0, workedSeconds), goalSeconds);
            var gray = Math.Max(0, goalSeconds - workedSeconds);
            var segments = new List<RingSegment>();
            if (colored > 0)
                segments.Add(ProjectArc(project.Name, project.Color, colored));
            if (gray > 0)
                segments.Add(new RingSegment("", LightGray, gray, LightLabel, false));
            if (segments.Count == 0)
                segments.Add(new RingSegment("", LightGray, Math.Max(goalSeconds, 1), LightLabel, false));
            return segments;
        }

        var daily = Math.Max(dailyGoalHours, 0.0001) * 3600d;
        var shown = Math.Min(Math.Max(0, workedSeconds), daily);
        var rest = Math.Max(0, daily - shown);
        var selected = new List<RingSegment>();
        if (shown > 0)
            selected.Add(ProjectArc(project.Name, project.Color, shown));
        if (rest > 0)
            selected.Add(new RingSegment("", LightGray, rest, LightLabel, false));
        return selected;
    }

    static RingSegment ProjectArc(string name, string color, double seconds)
        => new(name, color, seconds, color, true);

    static RingSegment RemainderArc(string label, double seconds)
        => new(label, LightGray, seconds, LightLabel, true);
}
