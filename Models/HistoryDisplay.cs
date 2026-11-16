using CommunityToolkit.Mvvm.ComponentModel;

namespace TimeTracker.Models;

public enum HistoryEventKind
{
    Work,
    Pause,
    Break
}

public sealed partial class HistoryEvent : ObservableObject
{
    public HistoryEventKind Kind { get; init; }
    public string ProjectId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Color { get; init; } = "#94A3B8";
    public DateTimeOffset Start { get; init; }
    public bool Open { get; init; }
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
    public bool IsWork => Kind == HistoryEventKind.Work;

    [ObservableProperty]
    private DateTimeOffset? _end;

    [ObservableProperty]
    private long _seconds;

    [ObservableProperty]
    private string _timeRangeText = "";

    [ObservableProperty]
    private string _durationText = "";

    [ObservableProperty]
    private string _toolTip = "";
}

public sealed class HistoryProjectTotal
{
    public string ProjectId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Color { get; init; } = "#2EE59D";
    public long Seconds { get; init; }
    public string DurationText { get; init; } = "0m";
}

public sealed class HistoryBarSegment
{
    public long Seconds { get; init; }
    public string Color { get; init; } = "#334155";
    public bool IsGap { get; init; }
    public string ToolTip { get; init; } = "";
}

public sealed class HistoryDayView
{
    public string DateText { get; init; } = "";
    public string SummaryText { get; init; } = "";
    public IReadOnlyList<HistoryEvent> Events { get; init; } = [];
    public IReadOnlyList<HistoryProjectTotal> Projects { get; init; } = [];
    public IReadOnlyList<HistoryBarSegment> BarSegments { get; init; } = [];
}
