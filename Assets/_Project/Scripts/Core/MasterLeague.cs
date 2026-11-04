using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Usta Ligi — ana yol bitince açılan haftalık yarış modu (Docs/MasterLeague_Plan.md). Bu dosya yalnız ÇEKİRDEK mantık:
/// havuz, tur karışımı, hamle kesintisi, puan, tur ilerlemesi ve kayıt. Oyun akışına (seviye seçimi, level sonu, menü)
/// bağlantı ayrı adımda yapılır.
///
/// Kurallar (kullanıcı, 2026-11-03):
///  - Havuz: katalogda 31+, Boss Duel ve Super Hard (numarası 9 ile biten) hariç; herkes için aynı; katalog büyüdükçe büyür.
///  - Tur: havuz her tur başında baştan karışır; aynı turdaki HERKES aynı listeyi oynar (seed = tur + havuz imzası).
///    Listedeki her seviye bir kez geçilince tur biter.
///  - Hamle: 1. tur −%10, her tur ek −%10, tavan −%30; en az 5 hamle.
///  - Puan: (taban + kalan hamle × değer) × tur çarpanı (1. tur ×1, sonra her tur +0.2); bir seviyede o turdaki EN İYİ puan sayılır.
///  - Haftalık puan: ISO hafta (Pazartesi), PlayerWallet.WeeklyScore ile aynı hafta anahtarı.
/// </summary>
public static class MasterLeague
{
    public const int MinPoolLevel = 31;
    public const float CutPerLap = 0.10f;
    public const float MaxCut = 0.30f;
    public const int MinMoves = 5;
    public const int BasePoints = 100;
    public const int PointsPerMove = 20;
    public const float LapBonus = 0.2f;

    // Cloud save manifest'te kayıtlı anahtarlar (CloudSaveManifest).
    public const string KeyLap = "master_league_lap";
    public const string KeyLapSignature = "master_league_lap_sig";
    public const string KeyBest = "master_league_best";          // "level:puan,level:puan" — bu turda geçilenler
    public const string KeyWeekly = "master_league_weekly";
    public const string KeyWeeklyWeek = "master_league_weekly_week";
    public const string KeyReached = "master_league_reached";
    public const string KeyCleared = "master_league_cleared";    // ligde kazanılan seviye sayısı (tüm turlar)
    public const string KeySeason = "master_league_season";      // sezon = açıldığı andaki katalog son seviyesi

    // Lig dönemi istatistikleri (lig açıldı → yeni paket geldi): yeni seviyeler gelince "Usta Ligi Özeti"nde gösterilir,
    // gösterilince sıfırlanır (MasterLeagueSummary). Cloud save'de.
    public const string KeyStintWins = "master_league_stint_wins";
    public const string KeyStintPoints = "master_league_stint_points";
    public const string KeyStintCoins = "master_league_stint_coins";
    public const string KeyStintBestWeekly = "master_league_stint_best_weekly";
    public const string KeyStintBestRank = "master_league_stint_best_rank";
    public static readonly string[] StintKeys = { KeyStintWins, KeyStintPoints, KeyStintCoins, KeyStintBestWeekly, KeyStintBestRank };

    /// Tüm Usta Ligi kayıtları (editör Play sıfırlaması bunları korur: TestLevelProgressionBootstrap).
    public static string[] AllKeys => new[]
    {
        KeyLap, KeyLapSignature, KeyBest, KeyWeekly, KeyWeeklyWeek, KeyReached, KeyCleared, KeySeason,
        KeyStintWins, KeyStintPoints, KeyStintCoins, KeyStintBestWeekly, KeyStintBestRank,
        MasterLeagueIntro.KeySeen,
        MasterLeagueRewards.KeyPendingWeek, MasterLeagueRewards.KeyPendingPoints, MasterLeagueRewards.KeyClaimedWeek,
        MasterLeagueRewards.KeyBadgeGold, MasterLeagueRewards.KeyBadgeSilver, MasterLeagueRewards.KeyBadgeBronze,
    };    // lige bir kez ulaştı (sıralama sekmesi açık kalır)

    /// Oynanmakta olan lig seviyesi (katalog numarası); 0 = lig oturumu yok. Seviye seçimi/level sonu bunu okur.
    public static int ActiveLevel { get; private set; }
    public static bool IsPlaying => ActiveLevel > 0;
    public static event Action OnChanged;

    // ── Havuz ve sıra ───────────────────────────────────────────────

    public static bool IsEligible(LevelCatalog.LevelEntry entry)
        => entry != null && entry.levelData != null && entry.level >= MinPoolLevel
           && entry.level % 10 != 9 && entry.levelData.levelKind != LevelKind.BossDuel;

    /// Katalogdaki uygun seviyeler, numaraya göre sıralı (karıştırmanın deterministik tabanı).
    public static List<int> Pool(LevelCatalog catalog)
    {
        var pool = new List<int>();
        if (catalog?.entries == null) return pool;
        var seen = new HashSet<int>();
        foreach (var entry in catalog.entries)
            if (IsEligible(entry) && seen.Add(entry.level)) pool.Add(entry.level);
        pool.Sort();
        return pool;
    }

    /// Havuz imzası: havuz değişince (yeni paket) yeni tur listesi de değişir; aynı havuzda herkes aynı sırayı alır.
    public static int PoolSignature(List<int> pool)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < pool.Count; i++) h = h * 31 + pool[i];
            return h;
        }
    }

    /// Turun listesi: havuzun tur + imza seed'li Fisher–Yates karışımı (platformdan bağımsız xorshift; UnityEngine.Random değil).
    public static List<int> LapOrder(List<int> pool, int lap)
    {
        var order = new List<int>(pool);
        uint state = unchecked((uint)(PoolSignature(pool) * 2654435761u) ^ (uint)(lap * 40503 + 0x9E3779B9u));
        if (state == 0) state = 0xA341316Cu;
        for (int i = order.Count - 1; i > 0; i--)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            int j = (int)(state % (uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    // ── Zorluk ve puan ──────────────────────────────────────────────

    public static float CutForLap(int lap) => Mathf.Min(MaxCut, CutPerLap * Mathf.Max(1, lap));

    public static int MovesFor(int baseMoves, int lap)
        => Mathf.Max(MinMoves, Mathf.RoundToInt(baseMoves * (1f - CutForLap(lap))));

    public static int PointsFor(int remainingMoves, int lap)
        => Mathf.RoundToInt((BasePoints + Mathf.Max(0, remainingMoves) * PointsPerMove) * (1f + LapBonus * (Mathf.Max(1, lap) - 1)));

    // ── Durum ───────────────────────────────────────────────────────

    public static bool IsUnlocked(LevelCatalog catalog)
    {
        bool unlocked = catalog != null && CurrentLevel.Global > catalog.MaximumGlobalLevel && Pool(catalog).Count > 0;
        if (!unlocked) return false;
        if (!Reached) PlayerPrefs.SetInt(KeyReached, 1);   // yeni paket gelip ana yola dönse de lig sıralaması görünür kalır
        StartSeasonIfNew(catalog.MaximumGlobalLevel);
        return true;
    }

    /// <summary>
    /// Lig SEZONU (kullanıcı kuralı 2026-11-04): ana yol her bittiğinde lig sıfırdan başlar — yeni intro, "LİG 1", 1. tur,
    /// yeni karışık liste (havuz yeni seviyelerle büyüdüğü için sıra da yenidir). Önceki sezondan devam YOK.
    /// Sezon kimliği = lig açıldığı andaki katalog son seviyesi; değiştiyse (yeni paket gelip bitti) sezon kayıtları sıfırlanır.
    /// Haftalık puan (haftalık yarış), bekleyen ödül, rozetler ve özet istatistikleri sezona bağlı DEĞİL, korunur.
    /// </summary>
    private static void StartSeasonIfNew(int catalogMax)
    {
        if (PlayerPrefs.GetInt(KeySeason, 0) == catalogMax) return;
        PlayerPrefs.SetInt(KeySeason, catalogMax);
        PlayerPrefs.DeleteKey(KeyLap);
        PlayerPrefs.DeleteKey(KeyLapSignature);
        PlayerPrefs.DeleteKey(KeyBest);
        PlayerPrefs.DeleteKey(KeyCleared);
        PlayerPrefs.DeleteKey(MasterLeagueIntro.KeySeen);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// Oyuncu lige en az bir kez ulaştı (katalog gerekmeden; liderlik ekranı sekmesi bunu okur).
    public static bool Reached => PlayerPrefs.GetInt(KeyReached, 0) == 1;

    public static int Lap => Mathf.Max(1, PlayerPrefs.GetInt(KeyLap, 1));

    /// Oyuncuya görünen lig seviye numarası ("LİG 12"): ligde kazanılan seviye sayısı + 1. Ana yoldaki seviye numarası gibi
    /// her kazançta 1 artar, turlar arasında sıfırlanmaz (kullanıcı kararı 2026-11-03; tur numarası iç kural olarak kalır).
    public static int LevelNumber => Mathf.Max(0, PlayerPrefs.GetInt(KeyCleared, 0)) + 1;

    public static int WeeklyPoints =>
        PlayerPrefs.GetString(KeyWeeklyWeek, "") == FirebaseLeaderboardService.CurrentWeekId
            ? PlayerPrefs.GetInt(KeyWeekly, 0) : 0;

    /// Bu turda geçilen seviyeler ve en iyi puanları.
    public static Dictionary<int, int> LapBest()
    {
        var best = new Dictionary<int, int>();
        string raw = PlayerPrefs.GetString(KeyBest, "");
        if (string.IsNullOrEmpty(raw)) return best;
        foreach (var part in raw.Split(','))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            if (int.TryParse(part.Substring(0, colon), out int lv) && int.TryParse(part.Substring(colon + 1), out int pts))
                best[lv] = pts;
        }
        return best;
    }

    private static void SaveLapBest(Dictionary<int, int> best)
    {
        var sb = new StringBuilder(best.Count * 8);
        foreach (var kv in best)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
        }
        PlayerPrefs.SetString(KeyBest, sb.ToString());
    }

    /// Havuz değiştiyse (yeni paket) mevcut turun listesi geçersiz: tur aynı kalır, liste yeni havuzla yeniden karışır;
    /// bu turda geçilip hâlâ havuzda olan seviyeler geçilmiş sayılmaya devam eder.
    private static void SyncSignature(List<int> pool)
    {
        int sig = PoolSignature(pool);
        if (PlayerPrefs.GetInt(KeyLapSignature, 0) == sig) return;
        PlayerPrefs.SetInt(KeyLapSignature, sig);
        var best = LapBest();
        var keep = new HashSet<int>(pool);
        var stale = new List<int>();
        foreach (var lv in best.Keys) if (!keep.Contains(lv)) stale.Add(lv);
        foreach (var lv in stale) best.Remove(lv);
        SaveLapBest(best);
        PlayerPrefs.Save();
    }

    /// Turun sıradaki seviyesi: listede bu turda henüz geçilmemiş ilk seviye. Havuz boşsa 0.
    public static int NextLevel(LevelCatalog catalog, out int indexInLap, out int lapSize)
    {
        var pool = Pool(catalog);
        indexInLap = 0; lapSize = pool.Count;
        if (pool.Count == 0) return 0;
        SyncSignature(pool);
        var best = LapBest();
        var order = LapOrder(pool, Lap);
        for (int i = 0; i < order.Count; i++)
        {
            if (best.ContainsKey(order[i])) continue;
            indexInLap = i;
            return order[i];
        }
        indexInLap = order.Count - 1;   // tüm liste geçildi (RecordWin turu ilerletir; buraya normalde düşmez)
        return order[order.Count - 1];
    }

    // ── Oturum ──────────────────────────────────────────────────────

    /// Lig seviyesini oynamaya başla (seviye seçimi ActiveLevel'ı okur). Ana yol current_level DEĞİŞMEZ.
    public static void Begin(int level)
    {
        ActiveLevel = Mathf.Max(0, level);
        OnChanged?.Invoke();
    }

    public static void End()
    {
        if (ActiveLevel == 0) return;
        ActiveLevel = 0;
        OnChanged?.Invoke();
    }

    /// Kazanılan lig seviyesi. Dönüş: bu kazanç için haftalık puana eklenen fark (o turdaki en iyi puanı geçtiyse).
    /// Listedeki son seviye de geçildiyse tur ilerler (yeni karışık liste, ek %10 kesinti, tavan %30).
    public static int RecordWin(LevelCatalog catalog, int level, int remainingMoves, out int levelPoints, out bool lapCompleted)
    {
        MasterLeagueRewards.RollWeek();   // geçen haftanın puanı yeni haftanın ilk kazancıyla ezilmesin (bekleyen ödül)
        lapCompleted = false;
        int lap = Lap;
        levelPoints = PointsFor(remainingMoves, lap);
        var best = LapBest();
        best.TryGetValue(level, out int previous);
        int gained = Mathf.Max(0, levelPoints - previous);
        if (!best.ContainsKey(level))
        {
            PlayerPrefs.SetInt(KeyCleared, LevelNumber);   // bu turda ilk kez geçildi → numara +1
            AddInt(KeyStintWins, 1);
        }
        if (levelPoints > previous) best[level] = levelPoints;
        SaveLapBest(best);

        if (gained > 0)
        {
            string week = FirebaseLeaderboardService.CurrentWeekId;
            PlayerPrefs.SetInt(KeyWeekly, WeeklyPoints + gained);   // WeeklyPoints eski haftada 0 döner
            PlayerPrefs.SetString(KeyWeeklyWeek, week);
            AddInt(KeyStintPoints, gained);
            if (WeeklyPoints > PlayerPrefs.GetInt(KeyStintBestWeekly, 0)) PlayerPrefs.SetInt(KeyStintBestWeekly, WeeklyPoints);
        }

        var pool = Pool(catalog);
        bool allDone = pool.Count > 0;
        foreach (var lv in pool) if (!best.ContainsKey(lv)) { allDone = false; break; }
        if (allDone)
        {
            lapCompleted = true;
            PlayerPrefs.SetInt(KeyLap, lap + 1);
            PlayerPrefs.SetString(KeyBest, "");
        }
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return gained;
    }

    /// Lig kazancında verilen altın (özet için).
    public static void RecordCoins(int coins)
    {
        if (coins > 0) AddInt(KeyStintCoins, coins);
    }

    /// Lig sıralamasında görülen kendi sıran (puanın > 0 iken); dönemin en iyisi saklanır.
    public static void ReportRank(int rank)
    {
        if (rank <= 0) return;
        int best = PlayerPrefs.GetInt(KeyStintBestRank, 0);
        if (best != 0 && best <= rank) return;
        PlayerPrefs.SetInt(KeyStintBestRank, rank);
        PlayerPrefs.Save();
    }

    private static void AddInt(string key, int value) => PlayerPrefs.SetInt(key, Mathf.Max(0, PlayerPrefs.GetInt(key, 0)) + value);
}
