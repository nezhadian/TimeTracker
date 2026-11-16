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

        var segments = Segments?.Where(segment => segment.Seconds > 0).ToList() ?? [];
        var labelRoom = ShowLabels ? 68 : 12;
        const double thickness = 20;
        var radius = Math.Min(width, height) / 2 - labelRoom - thickness / 2;
        if (radius < 36)
            radius = 36;
        var center = new Point(width / 2, height / 2);

        Surface.Children.Add(new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = ColorUtil.ToBrush("#F3F4F6"),
            StrokeThickness = thickness,
            Fill = null
        });
        Canvas.SetLeft(Surface.Children[^1], center.X - radius);
        Canvas.SetTop(Surface.Children[^1], center.Y - radius);

        var total = segments.Sum(segment => segment.Seconds);
        if (total <= 0)
            return;

        var angle = -Math.PI / 2;
        foreach (var segment in segments)
        {
            var sweep = segment.Seconds / total * Math.PI * 2;
            Surface.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = ColorUtil.ToBrush(segment.StrokeHex),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                Data = CreateArc(center, radius, angle, sweep)
            });

            if (ShowLabels && segment.ShowLabel && !string.IsNullOrWhiteSpace(segment.Label) && sweep >= 14 * Math.PI / 180)
                AddLabel(segment, center, radius + thickness / 2 + 8, angle + sweep / 2);

            angle += sweep;
        }
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
        if (sweep >= Math.PI * 2 - 0.001)
        {
            var group = new GeometryGroup();
            group.Children.Add(CreateArc(center, radius, start, Math.PI));
            group.Children.Add(CreateArc(center, radius, start + Math.PI, Math.PI - 0.001));
            return group;
        }

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

    static Point OnCircle(Point center, double radius, double angle)
        => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
}