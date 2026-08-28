using System;
using System.Collections.Generic;

/// <summary>
/// Tiny Safari zamanlama mantığı — saf/statik, sahne bağımsız.
///
/// Mevcut <c>ProgressEventSchedule</c> tek başlangıç günü + N-gün penceresi destekler; Safari ise
/// haftada BİRDEN FAZLA gün (Ptesi/Çrş/Ctesi/Pazar) ister ve her gün ayrı bir 24s pencere açar.
/// Bu yüzden ayrı, çoklu-gün penceresi burada modellenir.
///
/// Bir pencere, <see cref="SafariConfig.activeDays"/> içindeki bir günün UTC gün başında (00:00) açılır
/// ve <see cref="SafariConfig.windowHours"/> saat sonra kapanır. Aynı gün içinde en son açılan pencere baz alınır.
/// </summary>
public static class SafariSchedule
{
    /// <summary>Şu an aktif bir Safari penceresi var mı?</summary>
    public static bool IsActiveNow(SafariConfig config, DateTime utcNow)
    {
        return TryGetActiveWindowStart(config, utcNow, out _);
    }

    /// <summary>Aktif pencerenin bitiş zamanı (UTC). Aktif yoksa DateTime.MinValue.</summary>
    public static DateTime GetWindowEnd(SafariConfig config, DateTime utcNow)
    {
        return TryGetActiveWindowStart(config, utcNow, out var start)
            ? start.AddHours(Math.Max(1, config.windowHours))
            : DateTime.MinValue;
    }

    /// <summary>
    /// Aktif pencereyi benzersiz tanımlayan anahtar (başlangıç tarihi). Pencere değişince
    /// SafariState bu anahtardan farkı görüp run state'ini sıfırlar. Aktif yoksa "idle".
    /// </summary>
    public static string GetCycleKey(SafariConfig config, DateTime utcNow)
    {
        return TryGetActiveWindowStart(config, utcNow, out var start)
            ? $"safari_{start:yyyyMMdd_HH}"
            : "idle";
    }

    // ── Çekirdek ─────────────────────────────────────────────────
    // Bugün ve dün açılan pencereleri kontrol et (windowHours 24'ü aşabilir; dünkü pencere
    // hâlâ açık olabilir). En son açılmış ve şu an içinde bulunduğumuz pencereyi döndür.
    private static bool TryGetActiveWindowStart(SafariConfig config, DateTime utcNow, out DateTime start)
    {
        start = default;
        if (config == null) return false;

        int windowHours = Math.Max(1, config.windowHours);
        bool everyDay = config.activeDays == null || config.activeDays.Count == 0;

        // windowHours gün sınırını aşarsa önceki günlerin pencereleri de sürebilir → geriye bak.
        int lookbackDays = Math.Max(1, (windowHours + 23) / 24);
        bool found = false;
        DateTime best = default;

        for (int d = 0; d <= lookbackDays; d++)
        {
            DateTime dayStart = utcNow.Date.AddDays(-d); // UTC 00:00
            if (!everyDay && !config.activeDays.Contains(dayStart.DayOfWeek)) continue;

            DateTime end = dayStart.AddHours(windowHours);
            if (utcNow >= dayStart && utcNow < end)
            {
                if (!found || dayStart > best)
                {
                    best = dayStart;
                    found = true;
                }
            }
        }

        if (found) start = best;
        return found;
    }
}
