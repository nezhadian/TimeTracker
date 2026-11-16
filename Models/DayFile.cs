using System.Text.Json.Serialization;

namespace TimeTracker.Models;

public sealed class WorkSlice
{
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2EE59D";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public long Seconds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Open { get; set; }
}

public sealed class StopEntry
{
    public string Kind { get; set; } = "pause";
    public string Reason { get; set; } = "";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public long Seconds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Open { get; set; }
}

public sealed class BreakEntry
{
    public string Reason { get; set; } = "";
    public int PlannedSeconds { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public int AlarmPlays { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Open { get; set; }
}

public sealed class ProjectTotal
{
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2EE59D";
    public long Seconds { get; set; }
}

public sealed class ProjectGoalSnap
{
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2EE59D";
    public double? GoalHours { get; set; }
}

public sealed class DayTotals
{
    public long WorkSeconds { get; set; }
    public long StoppedSeconds { get; set; }
    public List<ProjectTotal> PerProject { get; set; } = [];
    public double DailyGoalHours { get; set; } = 12;
    public List<ProjectGoalSnap> ProjectGoals { get; set; } = [];
}

public sealed class DayFile
{
    public string Date { get; set; } = "";
    public List<WorkSlice> Slices { get; set; } = [];
    public List<StopEntry> Stops { get; set; } = [];
    public List<BreakEntry> Breaks { get; set; } = [];
    public DayTotals Totals { get; set; } = new();
}
