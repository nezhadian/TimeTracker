namespace TimeTracker.Models;

public sealed record RingSegment(
    string Label,
    string StrokeHex,
    double Seconds,
    string LabelHex,
    bool ShowLabel);
