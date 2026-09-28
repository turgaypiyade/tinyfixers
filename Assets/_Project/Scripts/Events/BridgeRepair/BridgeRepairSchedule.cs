using System;
using System.Collections.Generic;

/// <summary>
/// Bridge Repair takvimi — saf/statik. Haftada <see cref="BridgeRepairConfig.eventsPerWeek"/> gün RASTGELE seçilir,
/// ama rastgelelik hafta numarasından türetildiği için tüm cihazlarda (ve tekrar açılışlarda) aynı günlerdir.
/// Pencere seçilen günün UTC 00:00'ında açılır, <see cref="BridgeRepairConfig.windowHours"/> saat sürer.
/// Mümkünse seçilen günler arka arkaya gelmez (iki pencere arasında en az bir boş gün).
/// </summary>
public static class BridgeRepairSchedule
{
    public static bool IsActiveNow(BridgeRepairConfig config, DateTime utcNow) =>
        TryGetActiveWindowStart(config, utcNow, out _);

    /// <summary>Aktif pencerenin bitişi (UTC). Aktif yoksa DateTime.MinValue.</summary>
    public static DateTime GetWindowEnd(BridgeRepairConfig config, DateTime utcNow) =>
        TryGetActiveWindowStart(config, utcNow, out var start)
            ? start.AddHours(Math.Max(1, config.windowHours))
            : DateTime.MinValue;

    /// <summary>Aktif pencerenin kimliği; pencere değişince state sıfırlanır. Aktif yoksa "idle".</summary>
    public static string GetCycleKey(BridgeRepairConfig config, DateTime utcNow) =>
        TryGetActiveWindowStart(config, utcNow, out var start) ? $"bridge_{start:yyyyMMdd}" : "idle";

    /// <summary>Verilen haftanın (Pazartesi başlangıçlı, UTC) etkinlik günleri.</summary>
    public static List<DateTime> GetWeekDays(BridgeRepairConfig config, DateTime anyDayUtc)
    {
        var result = new List<DateTime>();
        if (config == null) return result;

        DateTime monday = WeekStart(anyDayUtc);
        var candidates = new List<int>();
        for (int d = 0; d < 7; d++)
        {
            var dow = monday.AddDays(d).DayOfWeek;
            if (config.excludedDays == null || !config.excludedDays.Contains(dow))
                candidates.Add(d);
        }
        if (candidates.Count == 0) return result;

        int want = Math.Min(Math.Max(1, config.eventsPerWeek), candidates.Count);
        var rng = new Random(WeekSeed(monday, config.scheduleSalt));
        var picked = new List<int>();

        // Önce birbirine komşu olmayan günlerden seç; yetmezse kalanlardan tamamla.
        for (int pass = 0; pass < 2 && picked.Count < want; pass++)
        {
            var pool = new List<int>();
            foreach (int d in candidates)
            {
                if (picked.Contains(d)) continue;
                bool adjacent = false;
                foreach (int p in picked) adjacent |= Math.Abs(p - d) <= 1;
                if (pass == 1 || !adjacent) pool.Add(d);
            }
            while (picked.Count < want && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                int d = pool[i];
                picked.Add(d);
                pool.RemoveAt(i);
                if (pass == 0) pool.RemoveAll(x => Math.Abs(x - d) <= 1);
            }
        }

        picked.Sort();
        foreach (int d in picked) result.Add(monday.AddDays(d));
        return result;
    }

    // Bugün veya (windowHours > 24 ise) önceki günlerde açılmış ve hâlâ süren en son pencere.
    private static bool TryGetActiveWindowStart(BridgeRepairConfig config, DateTime utcNow, out DateTime start)
    {
        start = default;
        if (config == null) return false;

        int windowHours = Math.Max(1, config.windowHours);
        int lookbackDays = Math.Max(0, (windowHours - 1) / 24);
        for (int back = 0; back <= lookbackDays; back++)
        {
            DateTime day = utcNow.Date.AddDays(-back);
            if (utcNow >= day.AddHours(windowHours)) continue;
            if (!GetWeekDays(config, day).Contains(day)) continue;
            start = day;
            return true;
        }
        return false;
    }

    private static DateTime WeekStart(DateTime utc)
    {
        DateTime day = utc.Date;
        int offset = ((int)day.DayOfWeek + 6) % 7;   // Pazartesi = 0
        return day.AddDays(-offset);
    }

    private static int WeekSeed(DateTime monday, int salt)
    {
        unchecked
        {
            int h = (int)2166136261;
            h = (h ^ monday.Year) * 16777619;
            h = (h ^ monday.DayOfYear) * 16777619;
            h = (h ^ salt) * 16777619;
            return h & 0x7FFFFFFF;
        }
    }
}
