namespace TimeTracker.Models;

public sealed class StopReasonChoice
{
    public string Text { get; init; } = "";
    public Project? Project { get; init; }

    public static StopReasonChoice ForText(string text)
        => new() { Text = text.Trim() };

    public static StopReasonChoice ForProject(Project project)
        => new() { Text = project.Name, Project = project };
}
