using System.Globalization;
using Microsoft.UI.Xaml.Media;

namespace TimeTracker.Services;

public static class ColorUtil
{
    public static readonly string[] Palette =
    [
        "#2EE59D", "#3B82F6", "#F59E0B", "#EF4444",
        "#8B5CF6", "#EC4899", "#14B8A6", "#F97316",
        "#6366F1", "#84CC16", "#06B6D4", "#64748B"
    ];

    public static bool TryNormalize(string? text, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length == 3)
        {
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }

        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return false;

        normalized = "#" + hex.ToUpperInvariant();
        return true;
    }

    public static Windows.UI.Color ToColor(string hex)
    {
        if (!TryNormalize(hex, out var normalized))
            return Microsoft.UI.Colors.LightGray;

        var value = uint.Parse(normalized[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(
            255,
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }

    public static SolidColorBrush ToBrush(string hex) => new(ToColor(hex));
}