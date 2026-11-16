namespace TimeTracker.Models;

public sealed class TrackerSettings
{
    public int SessionLengthMinutes { get; set; } = 40;
    public int ReasonThresholdMinutes { get; set; } = 3;
    public double DailyGoalHours { get; set; } = 12;
    public bool ShowDailyOverview { get; set; }
    public List<string> BreakReasons { get; set; } = [];
    public List<Project> Projects { get; set; } = [];

    public static TrackerSettings CreateDefault()
    {
        return new TrackerSettings
        {
            Projects =
            [
                new Project
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = "Default",
                    Color = "#2EE59D",
                    SessionLengthMinutes = 40
                }
            ]
        };
    }

    public Project? FindProject(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return Projects.FirstOrDefault(project => project.Id == id);
    }
}
