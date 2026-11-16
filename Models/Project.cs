namespace TimeTracker.Models;

public sealed class Project
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2EE59D";
    public int SessionLengthMinutes { get; set; }
    public double? DailyGoalHours { get; set; }
}
