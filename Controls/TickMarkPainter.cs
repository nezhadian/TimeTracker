using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TimeTracker.Services;
using Windows.Foundation;

namespace TimeTracker.Controls;

static class TickMarkPainter
{
    public const int Count = 24;
    public const string IdleHex = "#E6E8EB";
    public const string ElapsedHex = "#5B8EBF";
    public const string CircleHex = "#E8E8E8";

    public static void Draw(Canvas surface, Point center, double size, IReadOnlyList<string> colors)
    {
        var (inner, outer, thickness) = Metrics(size);
        AddCircle(surface, center, inner, CircleHex, 1.4);

        for (var i = 0; i < Count; i++)
        {
            var angle = -Math.PI / 2 + i * (Math.PI * 2 / Count);
            var hex = i < colors.Count ? colors[i] : IdleHex;
            var innerPoint = OnCircle(center, inner, angle);
            var outerPoint = OnCircle(center, outer, angle);
            surface.Children.Add(new Line
            {
                X1 = innerPoint.X,
                Y1 = innerPoint.Y,
                X2 = outerPoint.X,
                Y2 = outerPoint.Y,
                Stroke = ColorUtil.ToBrush(hex),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }
    }

    public static (double Inner, double Outer, double Thickness) Metrics(double size)
    {
        var thickness = Math.Clamp(size * 0.031, 6.0, 8.0);
        var outer = size / 2 - thickness / 2 - 2;
        var length = outer * 0.12;
        return (outer - length, outer, thickness);
    }

    public static int LitCount(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var lit = (int)Math.Round(fraction * Count, MidpointRounding.AwayFromZero);
        if (fraction > 0 && fraction < 1)
            return Math.Clamp(lit, 1, Count - 1);
        return fraction >= 1 ? Count : 0;
    }

    public static Point OnCircle(Point center, double radius, double angle)
        => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));

    static void AddCircle(Canvas surface, Point center, double radius, string hex, double thickness)
    {
        var circle = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = ColorUtil.ToBrush(hex),
            StrokeThickness = thickness,
            Fill = null
        };
        surface.Children.Add(circle);
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
    }
}
