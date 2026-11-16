using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TimeTracker.Models;
using TimeTracker.Services;
using Windows.UI;

namespace TimeTracker.Controls;

public sealed partial class DayTimelineBar : UserControl
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(IEnumerable<HistoryBarSegment>),
        typeof(DayTimelineBar),
        new PropertyMetadata(null, (d, _) => ((DayTimelineBar)d).Redraw()));

    public DayTimelineBar()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public IEnumerable<HistoryBarSegment>? Segments
    {
        get => (IEnumerable<HistoryBarSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    void Redraw()
    {
        Surface.Children.Clear();
        Surface.ColumnDefinitions.Clear();
        Hours.Children.Clear();
        Hours.ColumnDefinitions.Clear();
        DrawHours();

        var items = Segments?.ToList() ?? [];
        if (items.Count == 0)
            return;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            Surface.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(item.Seconds, 1), GridUnitType.Star)
            });

            var fill = item.IsGap
                ? new SolidColorBrush(Color.FromArgb(48, 148, 163, 184))
                : ColorUtil.ToBrush(item.Color);
            var block = new Border
            {
                Background = fill,
                CornerRadius = new CornerRadius(2)
            };
            if (!string.IsNullOrWhiteSpace(item.ToolTip))
                ToolTipService.SetToolTip(block, item.ToolTip);
            Grid.SetColumn(block, i);
            Surface.Children.Add(block);
        }
    }

    void DrawHours()
    {
        for (var hour = 1; hour <= 24; hour++)
        {
            Hours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock
            {
                Text = hour.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 10,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(label, hour - 1);
            Hours.Children.Add(label);
        }
    }
}
