using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TimeTracker.Models;
using TimeTracker.Services;
using Windows.Foundation;

namespace TimeTracker.Controls;

public sealed partial class GoalRing : UserControl
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(IEnumerable<RingSegment>),
        typeof(GoalRing),
        new PropertyMetadata(null, (d, _) => ((GoalRing)d).Redraw()));

    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(
        nameof(ShowLabels),
        typeof(bool),
        typeof(GoalRing),
        new PropertyMetadata(false, (d, _) => ((GoalRing)d).Redraw()));

    const double Thickness = 26;
    const double CornerRadius = 3;
    const string TrackHex = "#F3F4F6";

    static readonly HashSet<string> TrackColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "#E5E7EB", "#F3F4F6", TickMarkPainter.IdleHex, TrackHex
    };

    public GoalRing()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public IEnumerable<RingSegment>? Segments
    {
        get => (IEnumerable<RingSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    void Redraw()
    {
        Surface.Children.Clear();
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 40 || height < 40)
            return;

        var radius = Math.Min(width, height) / 2 - 16 - Thickness / 2;
        if (radius < 48)
            radius = 48;
        var center = new Point(width / 2, height / 2);
        AddCircle(center, radius, TrackHex, Thickness);

        var segments = Segments?.Where(segment => segment.Seconds > 0).ToList() ?? [];
        var total = segments.Sum(segment => segment.Seconds);
        if (total <= 0)
            return;

        if (ShowLabels)
            DrawOverview(center, radius, segments, total);
        else
            DrawSelected(center, radius, segments, total);
    }

    void DrawSelected(Point center, double radius, List<RingSegment> segments, double total)
    {
        var colored = segments.Count(segment => !IsTrack(segment.StrokeHex));
        var angle = -Math.PI / 2;
        foreach (var segment in segments)
        {
            var sweep = segment.Seconds / total * Math.PI * 2;
            if (!IsTrack(segment.StrokeHex))
                DrawRounded(center, radius, angle, sweep, segment.StrokeHex, colored > 1);
            angle += sweep;
        }
    }

    void DrawOverview(Point center, double radius, List<RingSegment> segments, double total)
    {
        var angle = -Math.PI / 2;
        var slots = new List<(double Start, double Sweep, double WorkSweep, string? WorkHex, string? SlotHex)>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var sweep = segment.Seconds / total * Math.PI * 2;
            var next = i + 1 < segments.Count ? segments[i + 1] : null;
            var nextIsProjectRemain = next is not null
                && string.Equals(next.StrokeHex, DailyRingBuilder.ProjectRemainHex, StringComparison.OrdinalIgnoreCase);

            if (segment.ShowLabel && nextIsProjectRemain)
            {
                var remainSweep = next!.Seconds / total * Math.PI * 2;
                slots.Add((angle, sweep + remainSweep, sweep, segment.StrokeHex, next.StrokeHex));
                angle += sweep + remainSweep;
                i++;
                continue;
            }

            if (segment.ShowLabel)
                slots.Add((angle, sweep, sweep, segment.StrokeHex, null));
            else
                slots.Add((angle, sweep, 0, null, segment.StrokeHex));
            angle += sweep;
        }

        var sliceGap = 2.5 / radius;
        foreach (var slot in slots)
        {
            if (slot.SlotHex is not null)
                AddRoundedBand(center, radius, slot.Start + sliceGap / 2, Math.Max(slot.Sweep - sliceGap, 0.002), slot.SlotHex);
            if (slot.WorkHex is not null)
                AddRoundedBand(center, radius, slot.Start + sliceGap / 2, Math.Max(Math.Min(slot.WorkSweep, slot.Sweep - sliceGap), 0.002), slot.WorkHex);
        }
    }

    void DrawRounded(Point center, double radius, double start, double sweep, string hex, bool gapBetween)
    {
        if (sweep >= Math.PI * 2 - 0.001)
        {
            AddCircle(center, radius, hex, Thickness);
            return;
        }

        var gap = gapBetween ? Math.Min(8 / radius, sweep / 4) : 0;
        AddArc(center, radius, start + gap / 2, Math.Max(sweep - gap, 0.001), hex, PenLineCap.Round, PenLineCap.Round);
    }

    void AddRoundedBand(Point center, double radius, double start, double sweep, string hex)
    {
        var geometry = CreateRoundedBand(center, radius, start, sweep, Thickness, CornerRadius);
        if (geometry is null)
        {
            if (sweep >= Math.PI * 2 - 0.001)
                AddCircle(center, radius, hex, Thickness);
            else
                AddArc(center, radius, start, Math.Max(sweep, 0.001), hex, PenLineCap.Flat, PenLineCap.Flat);
            return;
        }

        Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Stretch = Stretch.None,
            Fill = ColorUtil.ToBrush(hex),
            Data = geometry
        });
    }

    static Geometry? CreateRoundedBand(Point center, double radius, double start, double sweep, double thickness, double corner)
    {
        if (sweep >= Math.PI * 2 - 0.001 || sweep <= 0.002)
            return null;

        var outer = radius + thickness / 2;
        var inner = Math.Max(1, radius - thickness / 2);
        corner = Math.Min(corner, (outer - inner) / 2 - 0.25);
        if (corner < 0.75)
            return null;

        var outerDelta = Math.Asin(Math.Clamp(corner / Math.Max(outer - corner, 0.001), 0, 0.999));
        var innerDelta = Math.Asin(Math.Clamp(corner / (inner + corner), 0, 0.999));
        if (outerDelta * 2 + 0.002 >= sweep || innerDelta * 2 + 0.002 >= sweep)
            return null;

        var end = start + sweep;
        var figure = new PathFigure
        {
            StartPoint = OnCircle(center, (outer - corner) * Math.Cos(outerDelta), start),
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(Fillet(OnCircle(center, outer, start + outerDelta), corner));
        figure.Segments.Add(RingArc(OnCircle(center, outer, end - outerDelta), outer, sweep - 2 * outerDelta, clockwise: true));
        figure.Segments.Add(Fillet(OnCircle(center, (outer - corner) * Math.Cos(outerDelta), end), corner));
        figure.Segments.Add(new LineSegment { Point = OnCircle(center, (inner + corner) * Math.Cos(innerDelta), end) });
        figure.Segments.Add(Fillet(OnCircle(center, inner, end - innerDelta), corner));
        figure.Segments.Add(RingArc(OnCircle(center, inner, start + innerDelta), inner, sweep - 2 * innerDelta, clockwise: false));
        figure.Segments.Add(Fillet(OnCircle(center, (inner + corner) * Math.Cos(innerDelta), start), corner));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    static ArcSegment Fillet(Point point, double corner) => new()
    {
        Point = point,
        Size = new Size(corner, corner),
        SweepDirection = SweepDirection.Clockwise,
        IsLargeArc = false
    };

    static ArcSegment RingArc(Point point, double radius, double sweep, bool clockwise) => new()
    {
        Point = point,
        Size = new Size(radius, radius),
        SweepDirection = clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
        IsLargeArc = sweep > Math.PI
    };

    void AddCircle(Point center, double radius, string hex, double thickness)
    {
        Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Stretch = Stretch.None,
            Stroke = ColorUtil.ToBrush(hex),
            StrokeThickness = thickness,
            Data = new EllipseGeometry
            {
                Center = center,
                RadiusX = radius,
                RadiusY = radius
            }
        });
    }

    void AddArc(Point center, double radius, double start, double sweep, string hex, PenLineCap startCap, PenLineCap endCap)
    {
        Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Stretch = Stretch.None,
            Stroke = ColorUtil.ToBrush(hex),
            StrokeThickness = Thickness,
            StrokeStartLineCap = startCap,
            StrokeEndLineCap = endCap,
            Data = CreateArc(center, radius, start, sweep)
        });
    }

    static Geometry CreateArc(Point center, double radius, double start, double sweep)
    {
        var figure = new PathFigure
        {
            StartPoint = OnCircle(center, radius, start),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = OnCircle(center, radius, start + sweep),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = sweep > Math.PI
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    static bool IsTrack(string hex) => TrackColors.Contains(hex);

    static Point OnCircle(Point center, double radius, double angle)
        => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
}
