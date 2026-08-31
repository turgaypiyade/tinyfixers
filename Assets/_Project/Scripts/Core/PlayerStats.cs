using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Profil istatistikleri (PlayerPrefs'te kalıcı):
///   • İlk denemede geçilen level (toplam, kümülatif)
///   • En uzun seri = peş peşe ilk-denemede geçilen level rekoru (güncel seri fail'de sıfırlanır,
///     rekor düşmez; sadece güncel onu geçince güncellenir)
///   • Haftalık geçilen level = son 7 gün (kayan) içindeki tüm geçişler
///   • Oyuna ilk giriş tarihi (ilk açılışta kaydedilir, değişmez)
///
/// Hook'lar: level kazanılınca RecordLevelCleared(), fail olunca MarkCurrentLevelFailed().
/// </summary>
public static class PlayerStats
{
    private const string FirstTryClearsKey = "stats_first_try_clears";
    private const string CurrentStreakKey  = "stats_current_streak";
    private const string LongestStreakKey  = "stats_longest_streak";
    private const string LevelFailedKey    = "stats_current_level_failed";
    private const string LevelFailCountKey = "stats_level_fail_count";      // kümülatif (event ilk-hak tespiti)
    private const string WeeklyTicksKey    = "stats_weekly_clear_ticks";   // CSV of UTC ticks
    private const string FirstLaunchKey    = "stats_first_launch_ticks";

    private const int WeeklyWindowDays = 7;

    public static event Action OnChanged;

    // ── Okunan istatistikler ─────────────────────────────────────────────────

    public static int FirstTryClears => PlayerPrefs.GetInt(FirstTryClearsKey, 0);
    public static int CurrentStreak  => PlayerPrefs.GetInt(CurrentStreakKey, 0);
    public static int LongestStreak  => PlayerPrefs.GetInt(LongestStreakKey, 0);

    /// Kümülatif "level fail (vazgeçme)" sayısı. Global first-try bayrağından bağımsız; event bir turda
    /// oyuncunun fail edip etmediğini bu sayacın snapshot'ıyla anlar (stale LevelFailedKey'e bağlı kalmaz).
    public static int LevelFailCount => PlayerPrefs.GetInt(LevelFailCountKey, 0);

    /// Son 7 gün (kayan) içinde geçilen level sayısı. Eski kayıtları okurken budar.
    public static int WeeklyClears
    {
        get
        {
            var ticks = LoadWeeklyTicks(prune: true, out bool changed);
            if (changed) SaveWeeklyTicks(ticks);
            return ticks.Count;
        }
    }

    public static DateTime FirstLaunchDate
    {
        get
        {
            EnsureInitialized();
            long ticks = ReadLong(FirstLaunchKey, 0L);
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime() : DateTime.Now;
        }
    }

    // ── Başlatma ───────────────────────────────────────────────────────────────

    /// Gerçek ilk açılışta (BootLoader) çağrılır; ilk giriş tarihini bir kez kaydeder.
    public static void EnsureInitialized()
    {
        if (ReadLong(FirstLaunchKey, 0L) <= 0L)
        {
            WriteLong(FirstLaunchKey, DateTime.UtcNow.Ticks);
            PlayerPrefs.Save();
        }
    }

    // ── Hook'lar ─────────────────────────────────────────────────────────────

    /// Bir level kazanılıp ilerlenince çağrılır. İlk deneme miyse seri + ilk-deneme sayacı artar;
    /// her geçiş haftalık sayaca işlenir. "Current level failed" bayrağı temizlenir.
    public static void RecordLevelCleared()
    {
        bool firstTry = PlayerPrefs.GetInt(LevelFailedKey, 0) == 0;

        if (firstTry)
        {
            PlayerPrefs.SetInt(FirstTryClearsKey, FirstTryClears + 1);

            int current = CurrentStreak + 1;
            PlayerPrefs.SetInt(CurrentStreakKey, current);
            if (current > LongestStreak)
                PlayerPrefs.SetInt(LongestStreakKey, current);
        }

        var ticks = LoadWeeklyTicks(prune: true, out _);
        ticks.Add(DateTime.UtcNow.Ticks);
        SaveWeeklyTicks(ticks);

        PlayerPrefs.SetInt(LevelFailedKey, 0);   // sonraki level temiz başlasın
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// Mevcut level fail olunca çağrılır. Güncel seriyi sıfırlar ve "bu level fail oldu" bayrağını
    /// kaldırır → bu level artık ilk-deneme sayılmaz (rekor/longest düşmez).
    public static void MarkCurrentLevelFailed()
    {
        PlayerPrefs.SetInt(CurrentStreakKey, 0);
        PlayerPrefs.SetInt(LevelFailedKey, 1);
        PlayerPrefs.SetInt(LevelFailCountKey, LevelFailCount + 1);   // kümülatif fail sayacı (event tespiti)
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── Haftalık tick yardımcıları ──────────────────────────────────────────────

    private static List<long> LoadWeeklyTicks(bool prune, out bool changed)
    {
        changed = false;
        var result = new List<long>();
        string csv = PlayerPrefs.GetString(WeeklyTicksKey, string.Empty);
        if (string.IsNullOrEmpty(csv)) return result;

        long cutoff = DateTime.UtcNow.AddDays(-WeeklyWindowDays).Ticks;
        foreach (var part in csv.Split(','))
        {
            if (long.TryParse(part, out long t))
            {
                if (prune && t < cutoff) { changed = true; continue; }
                result.Add(t);
            }
            else changed = true;
        }
        return result;
    }

    private static void SaveWeeklyTicks(List<long> ticks)
    {
        PlayerPrefs.SetString(WeeklyTicksKey, string.Join(",", ticks));
        PlayerPrefs.Save();
    }

    private static long ReadLong(string key, long fallback)
        => long.TryParse(PlayerPrefs.GetString(key, fallback.ToString()), out long v) ? v : fallback;

    private static void WriteLong(string key, long value)
        => PlayerPrefs.SetString(key, value.ToString());
}
