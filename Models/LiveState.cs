namespace TimeTracker.Models;

public sealed class LiveSession
{
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ProjectColor { get; set; } = "#2EE59D";
    public int PlannedSeconds { get; set; }
    public long CompletedSeconds { get; set; }
    public DateTimeOffset? SliceStart { get; set; }
    public DateTimeOffset? PauseStart { get; set; }
    public string? PauseKind { get; set; }
    public string? PauseReason { get; set; }
    public bool ReasonCaptured { get; set; }
}

public sealed class LiveBreak
{
    public string Reason { get; set; } = "";
    public int PlannedSeconds { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public int AlarmPlays { get; set; }
    public bool AlarmPending { get; set; }
}

public sealed class LiveState
{
    public LiveSession? Session { get; set; }
    public LiveBreak? Break { get; set; }
}
