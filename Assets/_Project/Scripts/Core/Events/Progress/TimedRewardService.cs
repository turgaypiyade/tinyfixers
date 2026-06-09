using System;
using UnityEngine;

/// Progress event ödülleri için dakika bazlı ücretsiz kullanım servisi.
/// PlayerPrefs'e expiry ticks yazar; sahneden bağımsız, static.
public static class TimedRewardService
{
    private const string KeyPrefix = "timed_reward_";

    // ── Grant ────────────────────────────────────────────────────────────────

    public static void Grant(DailySlotRewardType type, int minutes)
    {
        if (minutes <= 0) return;
        long expiry = DateTime.UtcNow.AddMinutes(minutes).Ticks;
        PlayerPrefs.SetString(Key(type), expiry.ToString());
        PlayerPrefs.Save();
        Debug.Log($"[TimedReward] {type} → {minutes} dk ücretsiz.");
    }

    // ── Sorgulama ─────────────────────────────────────────────────────────────

    public static bool IsActive(DailySlotRewardType type)
    {
        string raw = PlayerPrefs.GetString(Key(type), "");
        return long.TryParse(raw, out long ticks) &&
               DateTime.UtcNow < new DateTime(ticks, DateTimeKind.Utc);
    }

    public static TimeSpan GetRemaining(DailySlotRewardType type)
    {
        string raw = PlayerPrefs.GetString(Key(type), "");
        if (!long.TryParse(raw, out long ticks)) return TimeSpan.Zero;
        var diff = new DateTime(ticks, DateTimeKind.Utc) - DateTime.UtcNow;
        return diff < TimeSpan.Zero ? TimeSpan.Zero : diff;
    }

    /// TileSpecial için herhangi bir aktif timed ödül var mı?
    public static bool IsSpecialFree(TileSpecial special) => special switch
    {
        TileSpecial.LineH          => IsActive(DailySlotRewardType.Joker_Line) ||
                                      IsActive(DailySlotRewardType.Joker_LineH),
        TileSpecial.LineV          => IsActive(DailySlotRewardType.Joker_Line),
        TileSpecial.PulseCore      => IsActive(DailySlotRewardType.Joker_PulseCore),
        TileSpecial.SystemOverride => IsActive(DailySlotRewardType.Joker_SystemOverride),
        _                          => false
    };

    public static bool IsLivesFree() => IsActive(DailySlotRewardType.Lives);

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string Key(DailySlotRewardType type) => $"{KeyPrefix}{(int)type}";
}
