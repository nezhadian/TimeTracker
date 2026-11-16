namespace TimeTracker.Services;

public static class TimeMath
{
    public static long SecondsBetween(DateTimeOffset start, DateTimeOffset end)
    {
        var seconds = (long)Math.Floor((end - start).TotalSeconds + 1e-9);
        return seconds < 0 ? 0 : seconds;
    }

    public static DateTimeOffset NextLocalMidnight(DateTimeOffset instant)
    {
        var local = instant.ToLocalTime().DateTime;
        var nextDate = local.Date.AddDays(1);
        var offset = TimeZoneInfo.Local.GetUtcOffset(nextDate);
        return new DateTimeOffset(nextDate, offset);
    }

    public static DateOnly LocalDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(instant.ToLocalTime().DateTime);

    public static DateTimeOffset LocalDayStart(DateOnly day)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }

    public static long Overlap(DateTimeOffset start, DateTimeOffset end, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var from = start > windowStart ? start : windowStart;
        var to = end < windowEnd ? end : windowEnd;
        return SecondsBetween(from, to);
    }

    public static string FormatCountdown(long seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        return h > 0
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", h, m, s)
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}", m, s);
    }

    public static long CeilSeconds(double seconds)
    {
        if (seconds <= 0)
            return 0;
        return (long)Math.Ceiling(seconds - 0.0001);
    }

    public static string FormatShort(long seconds)
    {
        if (seconds <= 0)
            return "0m";
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var remaining = seconds % 60;
        if (hours > 0)
            return remaining > 0 ? $"{hours}h {minutes}m {remaining}s" : minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        if (minutes > 0)
            return remaining > 0 ? $"{minutes}m {remaining}s" : $"{minutes}m";
        return $"{remaining}s";
    }

    public static string FormatCompleted(long seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var remaining = seconds % 60;
        var text = $"Completed: {hours} hours, {minutes} minutes";
        return remaining > 0 ? $"{text}, {remaining} seconds" : text;
    }

    public static string FormatGoal(double hours)
    {
        if (Math.Abs(hours - Math.Round(hours)) < 0.001)
            return $"{(int)Math.Round(hours)}h";
        return hours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "h";
    }
}
