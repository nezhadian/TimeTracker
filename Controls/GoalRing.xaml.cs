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
    const string TrackHex = "#F3F4F6";

    static readonly HashSet<string> TrackColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "#E5E7EB", "#F3F4F6", "#4B5563", TickMarkPainter.IdleHex, TrackHex
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

        var labelRoom = ShowLabels ? 68 : 16;
        var radius = Math.Min(width, height) / 2 - labelRoom - Thickness / 2;
        if (radius < 48)
            radius = 48;
        var center = new Point(width / 2, height / 2);
        AddCircle(center, radius, TrackHex, Thickness);

        var segments = Segments?.Where(segment => segment.Seconds > 0).ToList() ?? [];
        var total = segments.Sum(segment => segment.Seconds);
        if (total <= 0)
            return;

        var colored = segments.Count(segment => !IsTrack(segment.StrokeHex));
        var angle = -Math.PI / 2;
        foreach (var segment in segments)
        {
            var sweep = segment.Seconds / total * Math.PI * 2;
            if (!IsTrack(segment.StrokeHex))
            {
                if (sweep >= Math.PI * 2 - 0.001)
                    AddCircle(center, radius, segment.StrokeHex, Thickness);
                else
                {
                    var gap = colored > 1 ? Math.Min(8 / radius, sweep / 4) : 0;
                    var visible = sweep - gap;
                    if (visible > 0.002)
                        AddArc(center, radius, angle + gap / 2, visible, segment.StrokeHex);
                }
            }

            if (ShowLabels && segment.ShowLabel && !string.IsNullOrWhiteSpace(segment.Label) && sweep >= 14 * Math.PI / 180)
                AddLabel(segment, center, radius + Thickness / 2 + 8, angle + sweep / 2);

            angle += sweep;
        }
    }

    void AddCircle(Point center, double radius, string hex, double thickness)
    {
        var circle = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = ColorUtil.ToBrush(hex),
            StrokeThickness = thickness,
            Fill = null
        };
        Surface.Children.Add(circle);
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
    }

    void AddArc(Point center, double radius, double start, double sweep, string hex)
    {
        Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = ColorUtil.ToBrush(hex),
            StrokeThickness = Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = CreateArc(center, radius, start, sweep)
        });
    }

    void AddLabel(RingSegment segment, Point center, double radius, double angle)
    {
        var text = segment.Label.Length > 24 ? segment.Label[..23] + "…" : segment.Label;
        var block = new TextBlock
        {
            Text = text,
            Foreground = ColorUtil.ToBrush(segment.LabelHex),
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 108,
            TextWrapping = TextWrapping.Wrap
        };
        block.Measure(new Size(108, 64));
        var size = block.DesiredSize;
        var anchor = OnCircle(center, radius, angle);
        double x;
        double y;
        if (Math.Abs(anchor.X - center.X) < 18)
        {
            x = anchor.X - size.Width / 2;
            y = anchor.Y < center.Y ? anchor.Y - size.Height : anchor.Y;
        }
        else if (anchor.X > center.X)
        {
            x = anchor.X;
            y = anchor.Y - size.Height / 2;
        }
        else
        {
            x = anchor.X - size.Width;
            y = anchor.Y - size.Height / 2;
        }

        Surface.Children.Add(block);
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
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
