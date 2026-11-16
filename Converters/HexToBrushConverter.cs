using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using TimeTracker.Services;

namespace TimeTracker.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
        => value is string hex ? ColorUtil.ToBrush(hex) : new SolidColorBrush(Microsoft.UI.Colors.LightGray);

    public object ConvertBack(object value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}