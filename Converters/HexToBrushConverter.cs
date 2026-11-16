using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TimeTracker.Services;

namespace TimeTracker.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string hex ? ColorUtil.ToBrush(hex) : Brushes.LightGray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
