using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haftalık Yarış ödülü (kullanıcı onayı 2026-11-24). Ana yol haftalık puanı (PlayerWallet.WeeklyScore) hafta
/// değişince "bekleyen ödül" KUYRUĞUNA eklenir (yeni haftanın ilk kazancı onu ezmeden önce); kesin sıra ana menüde
/// hesaplanır (FirebaseLeaderboardService.FetchWeeklyFinalRank: gerçek oyuncular + botlar, ekrandaki sırayla aynı eşitlik
/// kuralı) ve ilk 3 ödülü WeeklyRaceRewardFlow ile verilir.
/// Kuyruk: bağlantı/ödül birkaç hafta gecikse de her hafta ayrı saklanır, sırayla (eskiden yeniye) teslim edilir.
/// Çift teslim yok: KeyClaimedWeek = teslim edilen EN SON hafta; ISO hafta kimlikleri ("2026-W47") metin olarak doğru
/// sıralanır → o haftaya eşit/eski haftalar atlanır. Altın + checkpoint tek kayıtta yazılır. Usta Ligi kalıbı.
/// </summary>
public static class WeeklyRaceRewards
{
    /// Kuyruk: "2026-W47=12345|2026-W48=999" (eskiden yeniye). Cloud save'de STRING.
    public const string KeyPendingWeek = "weekly_race_pending_week";
    /// Eski tek-kayıt anahtarı (yayınlanmadı; manifest uyumu için tutulur, okunmaz).
    public const string KeyPendingPoints = "weekly_race_pending_points";
    public const string KeyClaimedWeek = "weekly_race_claimed_week";

    public const int RewardedRankLimit = 3;
    private const int MaxQueued = 6;   // aşırı eski haftalar biriktirilmez

    /// Sıraya göre ödül listesi (yalnız 1-3; diğerleri boş). Kürsü balonu ve teslim aynı listeyi kullanır.
    public static List<DailySlotReward> RewardsFor(int rank)
    {
        var list = new List<DailySlotReward>(10);
        switch (rank)
        {
            case 1:
                Add(list, DailySlotRewardType.Coins, 500);
                Add(list, DailySlotRewardType.Booster_Hammer, 1);
                Add(list, DailySlotRewardType.Booster_Row, 1);
                Add(list, DailySlotRewardType.Booster_Column, 1);
                Add(list, DailySlotRewardType.Booster_Shuffle, 1);
                Add(list, DailySlotRewardType.Booster_PaintBucket, 1);
                Add(list, DailySlotRewardType.Joker_Line, 1);
                Add(list, DailySlotRewardType.Joker_PulseCore, 1);
                Add(list, DailySlotRewardType.Joker_SystemOverride, 1);
                Add(list, DailySlotRewardType.Lives, 1, 60);
                break;
            case 2:
                Add(list, DailySlotRewardType.Coins, 300);
                Add(list, DailySlotRewardType.Booster_Hammer, 1);
                Add(list, DailySlotRewardType.Booster_Row, 1);
                Add(list, DailySlotRewardType.Booster_Shuffle, 1);
                Add(list, DailySlotRewardType.Joker_Line, 1);
                Add(list, DailySlotRewardType.Joker_PulseCore, 1);
                Add(list, DailySlotRewardType.Lives, 1, 30);
                break;
            case 3:
                Add(list, DailySlotRewardType.Coins, 150);
                Add(list, DailySlotRewardType.Booster_Hammer, 1);
                Add(list, DailySlotRewardType.Booster_Shuffle, 1);
                Add(list, DailySlotRewardType.Joker_Line, 1);
                break;
        }
        return list;
    }

    private static void Add(List<DailySlotReward> list, DailySlotRewardType type, int amount, int minutes = 0)
        => list.Add(new DailySlotReward { type = type, amount = amount, durationMinutes = minutes });

    // ── Kuyruk ─────────────────────────────────────────────────────

    private static List<(string week, int points)> ReadQueue()
    {
        var list = new List<(string, int)>(2);
        string raw = PlayerPrefs.GetString(KeyPendingWeek, "");
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var part in raw.Split('|'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0 || !int.TryParse(part.Substring(eq + 1), out int pts)) continue;
            list.Add((part.Substring(0, eq), pts));
        }
        return list;
    }

    private static void WriteQueue(List<(string week, int points)> q)
    {
        if (q.Count == 0) { PlayerPrefs.DeleteKey(KeyPendingWeek); return; }
        var parts = new string[q.Count];
        for (int i = 0; i < q.Count; i++) parts[i] = q[i].week + "=" + q[i].points;
        PlayerPrefs.SetString(KeyPendingWeek, string.Join("|", parts));
    }

    private static bool IsClaimed(string week)
        => string.CompareOrdinal(week, PlayerPrefs.GetString(KeyClaimedWeek, "")) <= 0;

    // ── Hafta devri ────────────────────────────────────────────────

    /// Kayıtlı haftalık puan geçmiş bir haftaya aitse kuyruğa ekler (bekleyen varken bile — kayıp yok). Haftalık puan
    /// yazılmadan ÖNCE (PlayerWallet) ve ana menüde çağrılır.
    public static void RollWeek()
    {
        var (week, points) = PlayerWallet.StoredWeekly;
        if (string.IsNullOrEmpty(week) || week == FirebaseLeaderboardService.CurrentWeekId || points <= 0) return;
        if (IsClaimed(week)) return;
        var q = ReadQueue();
        foreach (var e in q) if (e.week == week) return;   // zaten kuyrukta
        q.Add((week, points));
        q.Sort((a, b) => string.CompareOrdinal(a.week, b.week));
        while (q.Count > MaxQueued) q.RemoveAt(0);
        WriteQueue(q);
        PlayerPrefs.Save();
    }

    public static bool HasPending
    {
        get
        {
            var q = ReadQueue();
            foreach (var e in q) if (!IsClaimed(e.week)) return true;
            return false;
        }
    }

    /// Sıradaki (en eski, teslim edilmemiş) hafta.
    public static string PendingWeek
    {
        get { foreach (var e in ReadQueue()) if (!IsClaimed(e.week)) return e.week; return ""; }
    }

    public static int PendingPoints
    {
        get { foreach (var e in ReadQueue()) if (!IsClaimed(e.week)) return e.points; return 0; }
    }

    /// Kalem bazında teslim ilerlemesi ("2026-W47#1#3" = o haftanın 1.'lik paketinden ilk 3 kalem verildi). Sıra da
    /// saklanır: devamda yeniden hesaplanan sıra değişse bile BAŞLANAN paket tamamlanır. Yarıda kesilen teslim
    /// (çökme/kapatma) bir sonraki açılışta KALDIĞI yerden sürer: kalem eksik ya da çift verilmez.
    public const string KeyClaimStep = "weekly_race_claim_step";

    // Gösterim ve teslim aynı paketi seçer; devam eden teslimde sunucunun yeni sırası paketi değiştirmez.
    internal static int ResolveClaimRank(string week, int fetchedRank, out int nextStep)
    {
        nextStep = StepFor(week, out int startedRank);
        return startedRank > 0 ? startedRank : fetchedRank;
    }

    /// Verilen HAFTANIN ödülünü verir (ilk 3) ya da ödülsüz sırada yalnız kapatır. Sorgulanan hafta artık sıradaki
    /// değilse (arada bulut kaydı değişti, zaten teslim edildi) hiçbir şey yapmaz.
    /// Kalem başına: önce ilerleme işareti (bellekte), sonra kalem → herhangi bir Save ikisini BİRLİKTE yazar; Save'den
    /// önce kesilirse ikisi de kaybolur ve kalem yeniden verilir (çift yok, eksik yok). Altın kalemi işaretiyle aynı
    /// kayıtta (defter). Sonda tek Save.
    public static void Claim(string week, int rank)
    {
        if (string.IsNullOrEmpty(week) || IsClaimed(week) || PendingWeek != week) return;

        rank = ResolveClaimRank(week, rank, out int start);
        var rewards = rank >= 1 && rank <= RewardedRankLimit ? RewardsFor(rank) : new List<DailySlotReward>();
        for (int i = start; i < rewards.Count; i++)
        {
            var r = rewards[i];
            string step = week + "#" + rank + "#" + (i + 1);
            if (r.type == DailySlotRewardType.Coins && !r.IsTimed)
            {
                PlayerWallet.AddCoinsWithCheckpoint(Mathf.Max(1, r.amount), KeyClaimStep, step);
                continue;
            }
            PlayerPrefs.SetString(KeyClaimStep, step);
            DailySlotRewardService.Grant(r);
        }

        PlayerPrefs.SetString(KeyClaimedWeek, week);
        PlayerPrefs.DeleteKey(KeyClaimStep);
        RemoveFromQueue(week);
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
    }

    private static int StepFor(string week, out int rank)
    {
        rank = 0;
        var parts = PlayerPrefs.GetString(KeyClaimStep, "").Split('#');
        if (parts.Length != 3 || parts[0] != week) return 0;
        if (!int.TryParse(parts[1], out rank) || !int.TryParse(parts[2], out int n)) { rank = 0; return 0; }
        return Mathf.Max(0, n);
    }

    private static void RemoveFromQueue(string week)
    {
        var q = ReadQueue();
        q.RemoveAll(e => e.week == week || IsClaimed(e.week));
        WriteQueue(q);
    }

    /// Test: kuyruğu boşaltır.
    public static void ClearPending()
    {
        PlayerPrefs.DeleteKey(KeyPendingWeek);
        PlayerPrefs.DeleteKey(KeyPendingPoints);
        PlayerPrefs.Save();
    }
}
