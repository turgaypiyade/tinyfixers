using UnityEngine;

public enum MasterBadge { None = 0, Bronze = 1, Silver = 2, Gold = 3 }

/// <summary>
/// Usta Ligi haftalık ödülü (kullanıcı kararı 2026-11-03: joker YOK, yalnız altın + rozet):
///   1. → 1.500 altın + altın rozet · 2–3. → 1.000 + gümüş · 4–10. → 500 + bronz · 11–50. → 250 · katılım (puan > 0) → 100.
/// Hafta devrinde geçen haftanın lig puanı "bekleyen ödül" olarak saklanır (yeni haftanın puanı onu ezmez); kesin sıra
/// ana menüde hesaplanır (FirebaseLeaderboardService.FetchMasterFinalRank) ve MasterLeagueRewardPopup verir.
/// Teslim tek kayıtta: altın + defter + "bu hafta alındı" checkpoint'i birlikte yazılır → aynı hafta iki kez alınamaz.
/// Rozet sayıları cloud save'de; profil/sıralama/takım görünümü buradan okur.
/// </summary>
public static class MasterLeagueRewards
{
    public const string KeyPendingWeek = "master_league_pending_week";
    public const string KeyPendingPoints = "master_league_pending_points";
    public const string KeyClaimedWeek = "master_league_claimed_week";
    public const string KeyBadgeGold = "master_badge_gold";
    public const string KeyBadgeSilver = "master_badge_silver";
    public const string KeyBadgeBronze = "master_badge_bronze";

    public const int RewardedRankLimit = 50;

    public static int CoinsFor(int rank, int points)
    {
        if (points <= 0) return 0;
        if (rank == 1) return 1500;
        if (rank >= 2 && rank <= 3) return 1000;
        if (rank >= 4 && rank <= 10) return 500;
        if (rank >= 11 && rank <= RewardedRankLimit) return 250;
        return 100;   // katılım
    }

    public static MasterBadge BadgeFor(int rank) =>
        rank == 1 ? MasterBadge.Gold : rank >= 2 && rank <= 3 ? MasterBadge.Silver
        : rank >= 4 && rank <= 10 ? MasterBadge.Bronze : MasterBadge.None;

    // ── Hafta devri ────────────────────────────────────────────────

    /// Kayıtlı lig puanı geçmiş bir haftaya aitse onu bekleyen ödüle taşır. Lig kazancından ÖNCE ve ana menüde çağrılır.
    public static void RollWeek()
    {
        string stored = PlayerPrefs.GetString(MasterLeague.KeyWeeklyWeek, "");
        int points = PlayerPrefs.GetInt(MasterLeague.KeyWeekly, 0);
        if (string.IsNullOrEmpty(stored) || stored == FirebaseLeaderboardService.CurrentWeekId || points <= 0) return;
        if (!HasPending && PlayerPrefs.GetString(KeyClaimedWeek, "") != stored)
        {
            PlayerPrefs.SetString(KeyPendingWeek, stored);
            PlayerPrefs.SetInt(KeyPendingPoints, points);
        }
        PlayerPrefs.SetInt(MasterLeague.KeyWeekly, 0);
        PlayerPrefs.Save();
    }

    public static bool HasPending => !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyPendingWeek, ""));
    public static string PendingWeek => PlayerPrefs.GetString(KeyPendingWeek, "");
    public static int PendingPoints => PlayerPrefs.GetInt(KeyPendingPoints, 0);

    /// Ödülü verir: altın (defter + checkpoint aynı kayıtta), rozet sayacı, bekleyeni temizler.
    public static void Claim(int rank)
    {
        string week = PendingWeek;
        if (string.IsNullOrEmpty(week) || PlayerPrefs.GetString(KeyClaimedWeek, "") == week) { ClearPending(); return; }
        var badge = BadgeFor(rank);
        if (badge != MasterBadge.None) PlayerPrefs.SetInt(KeyOf(badge), Count(badge) + 1);
        int coins = CoinsFor(rank, PendingPoints);
        if (coins > 0) PlayerWallet.AddCoinsWithCheckpoint(coins, KeyClaimedWeek, week);
        else PlayerPrefs.SetString(KeyClaimedWeek, week);
        ClearPending();
        FirebaseCloudSaveService.MarkDirty();
    }

    private static void ClearPending()
    {
        PlayerPrefs.DeleteKey(KeyPendingWeek);
        PlayerPrefs.DeleteKey(KeyPendingPoints);
        PlayerPrefs.Save();
    }

    // ── Rozetler ───────────────────────────────────────────────────

    public static int Count(MasterBadge badge) => badge == MasterBadge.None ? 0 : Mathf.Max(0, PlayerPrefs.GetInt(KeyOf(badge), 0));

    /// En değerli kazanılmış rozet (yoksa None).
    public static MasterBadge Best =>
        Count(MasterBadge.Gold) > 0 ? MasterBadge.Gold : Count(MasterBadge.Silver) > 0 ? MasterBadge.Silver
        : Count(MasterBadge.Bronze) > 0 ? MasterBadge.Bronze : MasterBadge.None;

    public static Sprite SpriteOf(MasterBadge badge) => badge switch
    {
        MasterBadge.Gold => Load("MasterBadge_Gold"),
        MasterBadge.Silver => Load("MasterBadge_Silver"),
        MasterBadge.Bronze => Load("MasterBadge_Bronze"),
        _ => null,
    };

    private static Sprite Load(string name) => Resources.Load<Sprite>("MasterLeague/" + name);

    private static string KeyOf(MasterBadge badge) => badge switch
    {
        MasterBadge.Gold => KeyBadgeGold,
        MasterBadge.Silver => KeyBadgeSilver,
        _ => KeyBadgeBronze,
    };
}
