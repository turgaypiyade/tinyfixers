using System;
using UnityEngine;

/// Progress event ödülleri için dakika bazlı ücretsiz kullanım servisi.
/// PlayerPrefs'e expiry ticks yazar; sahneden bağımsız, static.
public static class TimedRewardService
{
    private const string KeyPrefix = "timed_reward_";

    // ── Grant ────────────────────────────────────────────────────────────────

    public static bool SupportsTimed(DailySlotRewardType type) => type == DailySlotRewardType.Lives
        || type == DailySlotRewardType.Joker_LineH || type == DailySlotRewardType.Joker_Line
        || type == DailySlotRewardType.Joker_PulseCore || type == DailySlotRewardType.Joker_SystemOverride
        || BoosterRewardType(BoardController.BoosterMode.Single) <= type && type <= BoosterRewardType(BoardController.BoosterMode.PaintBucket);

    public static void Grant(DailySlotRewardType type, int minutes)
    {
        if (minutes <= 0 || !SupportsTimed(type)) return;
        // Aktif süre varsa ÜSTÜNE eklenir (ikinci paket/ödül süreyi ezmesin, uzatsın).
        DateTime start = DateTime.UtcNow + GetRemaining(type);
        long expiry = start.AddMinutes(minutes).Ticks;
        PlayerPrefs.SetString(Key(type), expiry.ToString());
        PlayerPrefs.Save();
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

    /// In-level booster (joker) usable without spending while its timed reward runs.
    public static bool IsBoosterFree(BoardController.BoosterMode mode)
    {
        var type = BoosterRewardType(mode);
        return type != DailySlotRewardType.Empty && IsActive(type);
    }

    public static TimeSpan GetBoosterRemaining(BoardController.BoosterMode mode)
    {
        var type = BoosterRewardType(mode);
        return type != DailySlotRewardType.Empty ? GetRemaining(type) : TimeSpan.Zero;
    }

    public static DailySlotRewardType BoosterRewardType(BoardController.BoosterMode mode) => mode switch
    {
        BoardController.BoosterMode.Single      => DailySlotRewardType.Booster_Hammer,
        BoardController.BoosterMode.Row         => DailySlotRewardType.Booster_Row,
        BoardController.BoosterMode.Column      => DailySlotRewardType.Booster_Column,
        BoardController.BoosterMode.Shuffle     => DailySlotRewardType.Booster_Shuffle,
        BoardController.BoosterMode.PaintBucket => DailySlotRewardType.Booster_PaintBucket,
        _                                       => DailySlotRewardType.Empty
    };

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string Key(DailySlotRewardType type) => $"{KeyPrefix}{(int)type}";
}
