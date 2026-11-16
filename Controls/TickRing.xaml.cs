using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        var lit = TickMarkPainter.LitCount(ElapsedFraction);
        var colors = Enumerable.Range(0, TickMarkPainter.Count)
            .Select(index => index < lit ? TickMarkPainter.ElapsedHex : TickMarkPainter.IdleHex)
            .ToArray();
        TickMarkPainter.Draw(Surface, new Point(width / 2, height / 2), Math.Min(width, height), colors);
    }
}
