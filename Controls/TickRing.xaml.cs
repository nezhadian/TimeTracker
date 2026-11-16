using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace TimeTracker.Controls;

public sealed partial class TickRing : UserControl
{
    public static readonly DependencyProperty ElapsedFractionProperty = DependencyProperty.Register(
        nameof(ElapsedFraction),
        typeof(double),
        typeof(TickRing),
        new PropertyMetadata(0d, (d, _) => ((TickRing)d).Redraw()));

    public TickRing()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public double ElapsedFraction
    {
        get => (double)GetValue(ElapsedFractionProperty);
        set => SetValue(ElapsedFractionProperty, value);
    }

    void Redraw()
    {
        Surface.Children.Clear();
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 20 || height < 20)
            return;

        const int count = 72;
        var radius = Math.Min(width, height) / 2 - 8;
        var center = new Point(width / 2, height / 2);
        var fraction = Math.Clamp(ElapsedFraction, 0, 1);
        var lit = (int)Math.Round(fraction * count, MidpointRounding.AwayFromZero);
        if (fraction > 0 && fraction < 1)
            lit = Math.Clamp(lit, 1, count - 1);
        else if (fraction >= 1)
            lit = count;

        var elapsedBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x25, 0x63, 0xEB));
        var idleBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xE5, 0xE7, 0xEB));
        for (var i = 0; i < count; i++)
        {
            var angle = -Math.PI / 2 + i * (Math.PI * 2 / count);
            var outer = OnCircle(center, radius, angle);
            var inner = OnCircle(center, radius - 16, angle);
            Surface.Children.Add(new Line
            {
                X1 = inner.X,
                Y1 = inner.Y,
                X2 = outer.X,
                Y2 = outer.Y,
                Stroke = i < lit ? elapsedBrush : idleBrush,
                StrokeThickness = 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }
    }

    static Point OnCircle(Point center, double radius, double angle)
        => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
}