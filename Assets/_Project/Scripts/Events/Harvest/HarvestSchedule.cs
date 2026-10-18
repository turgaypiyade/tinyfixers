using System;
using System.Globalization;

/// <summary>
/// Bostan Hasadı takvimi — saf/statik. epochUtc'den itibaren cycleDays günlük döngüler; her döngünün ilk
/// activeDays günü event açık. Hesap yalnız UTC tarihten türediği için tüm cihazlarda aynıdır.
/// </summary>
public static class HarvestSchedule
{
    public static bool IsActiveNow(HarvestConfig cfg, DateTime utcNow) => TryGetWindow(cfg, utcNow, out _, out _);

    /// Aktif pencerenin bitişi (UTC). Aktif yoksa DateTime.MinValue.
    public static DateTime GetWindowEnd(HarvestConfig cfg, DateTime utcNow) =>
        TryGetWindow(cfg, utcNow, out _, out var end) ? end : DateTime.MinValue;

    /// Bir sonraki pencerenin başlangıcı (UTC); ara günlerde ikon "açılışa kalan" geri sayımını gösterir.
    /// Pencere şu an açıksa DateTime.MinValue.
    public static DateTime GetNextStart(HarvestConfig cfg, DateTime utcNow)
    {
        if (TryGetWindow(cfg, utcNow, out var start, out _)) return DateTime.MinValue;
        if (start == DateTime.MinValue) return start;            // config bozuk / epoch ileride değil
        return start.AddDays(Math.Max(1, cfg.cycleDays));
    }

    /// Aktif sezonun kimliği; sezon değişince ilerleme sıfırlanır. Aktif yoksa "idle".
    public static string GetCycleKey(HarvestConfig cfg, DateTime utcNow) =>
        TryGetWindow(cfg, utcNow, out var start, out _) ? $"harvest_{start:yyyyMMdd}" : "idle";

    private static bool TryGetWindow(HarvestConfig cfg, DateTime utcNow, out DateTime start, out DateTime end)
    {
        start = end = DateTime.MinValue;
        if (cfg == null) return false;
        if (!DateTime.TryParseExact(cfg.epochUtc, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var epoch))
            return false;

        int cycle = Math.Max(1, cfg.cycleDays);
        int active = Math.Clamp(cfg.activeDays, 1, cycle);
        double days = (utcNow - epoch).TotalDays;
        if (days < 0) { start = epoch.AddDays(-Math.Max(1, cfg.cycleDays)); return false; }
        long index = (long)Math.Floor(days / cycle);
        start = epoch.AddDays(index * cycle);
        end = start.AddDays(active);
        return utcNow < end;
    }
}
