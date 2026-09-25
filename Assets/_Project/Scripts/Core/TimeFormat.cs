using System;

/// <summary>
/// Oyundaki TÜM geri sayımların tek biçimi:
///   ≥ 1 gün  → "2g 23s" (gün + saat; EN: "2d 23h")
///   ≥ 1 saat → "23:59:59"
///   aksi     → "59:59"
/// Saniye yukarı yuvarlanır (0'a inmeden "00:00" görünmez).
/// </summary>
public static class TimeFormat
{
    public static string Countdown(TimeSpan remaining)
    {
        long totalSeconds = remaining > TimeSpan.Zero ? (long)Math.Ceiling(remaining.TotalSeconds) : 0;
        long days = totalSeconds / 86400;
        long hours = totalSeconds / 3600 % 24;
        long minutes = totalSeconds / 60 % 60;
        long seconds = totalSeconds % 60;

        if (days > 0)
            return hours > 0
                ? GameLocalization.GetFormat("progress_timer_days_hours", days, hours)
                : GameLocalization.GetFormat("progress_timer_days", days);

        if (totalSeconds >= 3600)
            return $"{totalSeconds / 3600:00}:{minutes:00}:{seconds:00}";

        return $"{minutes:00}:{seconds:00}";
    }
}
