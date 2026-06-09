using System;
using System.Globalization;
using UnityEngine;

public enum ProgressEventScheduleType
{
    Always,    // Config'deki durationHours dolana kadar aktif (mevcut davranış)
    Weekly,    // Her hafta belirli bir günden N gün aktif
    FixedDate  // Belirli UTC tarih aralığında aktif
}

[Serializable]
public class ProgressEventSchedule
{
    public bool enabled = true;

    [Header("Level Kapısı")]
    [Tooltip("Bu event için oyuncunun ulaşmış olması gereken minimum level.")]
    [Min(1)] public int minLevel = 1;

    [Header("Zamanlama")]
    public ProgressEventScheduleType scheduleType = ProgressEventScheduleType.Always;

    [Tooltip("Her hafta hangi gün başlasın. (Weekly)")]
    public DayOfWeek weekStartDay = DayOfWeek.Monday;

    [Tooltip("Haftada kaç gün aktif olsun. (Weekly)")]
    [Min(1)] public int eventDurationDays = 3;

    [Tooltip("Başlangıç tarihi UTC (yyyy-MM-dd). Örn: 2026-06-09 (FixedDate)")]
    public string fixedStartDateUtc = "";

    [Tooltip("Bitiş tarihi UTC (yyyy-MM-dd). Örn: 2026-06-12 (FixedDate)")]
    public string fixedEndDateUtc = "";

    // ── Public API ────────────────────────────────────────────────

    public bool IsActiveNow()
    {
        if (!enabled) return false;
        return scheduleType switch
        {
            ProgressEventScheduleType.Always    => true,
            ProgressEventScheduleType.Weekly    => IsWeeklyActive(),
            ProgressEventScheduleType.FixedDate => IsFixedDateActive(),
            _                                   => false
        };
    }

    /// Mevcut aktif pencerenin bitiş zamanı.
    /// Always → DateTime.MaxValue (ProgressEventConfig.durationHours kullanılır)
    public DateTime GetWindowEnd()
    {
        return scheduleType switch
        {
            ProgressEventScheduleType.Weekly    => GetWeeklyWindowEnd(),
            ProgressEventScheduleType.FixedDate => ParseDate(fixedEndDateUtc),
            _                                   => DateTime.MaxValue
        };
    }

    /// Her yeni event döngüsünde değişen anahtar. Farklılaşınca goal'ler sıfırlanır.
    public string GetCycleKey()
    {
        return scheduleType switch
        {
            ProgressEventScheduleType.Weekly    => $"weekly_{GetWeeklyWindowStart():yyyy_MM_dd}_{(int)weekStartDay}",
            ProgressEventScheduleType.FixedDate => $"fixed_{fixedStartDateUtc}",
            _                                   => "always"
        };
    }

    // ── Weekly ───────────────────────────────────────────────────

    private bool IsWeeklyActive()
    {
        int daysSince = DaysSinceWeekStart();
        return daysSince < eventDurationDays;
    }

    private DateTime GetWeeklyWindowStart()
    {
        return DateTime.UtcNow.Date.AddDays(-DaysSinceWeekStart());
    }

    private DateTime GetWeeklyWindowEnd()
    {
        return GetWeeklyWindowStart().AddDays(eventDurationDays);
    }

    private int DaysSinceWeekStart()
    {
        return ((int)DateTime.UtcNow.DayOfWeek - (int)weekStartDay + 7) % 7;
    }

    // ── Fixed Date ───────────────────────────────────────────────

    private bool IsFixedDateActive()
    {
        DateTime now   = DateTime.UtcNow;
        DateTime start = ParseDate(fixedStartDateUtc);
        DateTime end   = ParseDate(fixedEndDateUtc);
        return now >= start && now < end;
    }

    // ── Helper ───────────────────────────────────────────────────

    public static DateTime ParseDate(string dateStr)
    {
        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
            return result;
        return DateTime.MaxValue;
    }
}

[Serializable]
public class ScheduledProgressEvent
{
    [Tooltip("Editor'da ayırt etmek için etiket.")]
    public string label = "Event";

    public ProgressEventConfig config;
    public ProgressEventSchedule schedule;

    [Tooltip("Aynı anda birden fazla event aktifse yüksek öncelikli olan seçilir.")]
    [Min(0)] public int priority = 0;
}
