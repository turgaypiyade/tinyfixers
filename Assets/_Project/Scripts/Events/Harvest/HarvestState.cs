using System;
using UnityEngine;

/// <summary>
/// Bostan Hasadı kalıcı durumu (PlayerPrefs; anahtarlar CloudSaveManifest'te → cloud-save'li).
/// Kazı mantığı burada: UI yalnızca sonucu oynatır. Kat ödülü KAZI ANINDA (kayıtla birlikte) verilir —
/// tören ekranı açılmadan uygulama kapansa bile ödül kaybolmaz.
/// </summary>
public static class HarvestState
{
    private const string KeyTrowels = "harvest_trowels";
    private const string KeyFloor   = "harvest_floor";
    private const string KeyDug     = "harvest_dug";     // bu kattaki kazılmış hücreler (bit maskesi)
    private const string KeyFound   = "harvest_found";   // bu kattaki bulunmuş yerleşimler (bit maskesi)
    private const string KeyCycle   = "harvest_cycle";   // sezon kimliği; değişince ilerleme sıfırlanır
    // Sezon sonu: altına çevrilen kürek/altın — ana menüde bir kez duyurulur (altın çevrildiği an verilir).
    private const string KeyPendingTrowels = "harvest_pending_trowels";
    private const string KeyPendingGold    = "harvest_pending_gold";

    public static event Action OnChanged;

    public static int Trowels => PlayerPrefs.GetInt(KeyTrowels, 0);
    public static int Floor   => PlayerPrefs.GetInt(KeyFloor, 0);
    public static int DugMask => PlayerPrefs.GetInt(KeyDug, 0);
    public static int FoundMask => PlayerPrefs.GetInt(KeyFound, 0);

    public static bool IsFinished(HarvestConfig cfg) => cfg == null || Floor >= cfg.FloorCount;

    // ── Sezon / görünürlük ───────────────────────────────────────

    /// Event şu an oyuncu için açık mı (takvim + level kapısı; editörde debugForceAvailable).
    public static bool IsLive(HarvestConfig cfg, DateTime utcNow)
    {
        if (cfg == null) return false;
#if UNITY_EDITOR
        if (cfg.debugForceAvailable) return true;
#endif
        return CurrentLevel.Global >= cfg.minLevelGate && HarvestSchedule.IsActiveNow(cfg, utcNow);
    }

    /// Level kapısı geçildi mi — ikon bundan sonra hep görünür (açık değilse açılışa geri sayım).
    public static bool IsUnlocked(HarvestConfig cfg)
    {
        if (cfg == null) return false;
#if UNITY_EDITOR
        if (cfg.debugForceAvailable) return true;
#endif
        return CurrentLevel.Global >= cfg.minLevelGate;
    }

    public static DateTime WindowEnd(HarvestConfig cfg, DateTime utcNow)
    {
#if UNITY_EDITOR
        if (cfg != null && cfg.debugForceAvailable && !HarvestSchedule.IsActiveNow(cfg, utcNow))
            return utcNow.Date.AddDays(1);
#endif
        return HarvestSchedule.GetWindowEnd(cfg, utcNow);
    }

    /// Yeni sezon başladıysa önceki sezonun kürek/hasat ilerlemesini sıfırlar. Her giriş noktasında çağrılır.
    public static void SyncCycle(HarvestConfig cfg, DateTime utcNow)
    {
        if (cfg == null) return;
        string current = HarvestSchedule.GetCycleKey(cfg, utcNow);
        string stored = PlayerPrefs.GetString(KeyCycle, "");
        if (current == "idle")
        {
            // Pencere kapandı: sezon bitti → kalan kürekler altına (ilerleme yeni sezonda sıfırlanır).
            if (stored.Length > 0 && Trowels > 0)
            {
                ConvertLeftoverTrowels(cfg);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
            return;
        }
        if (stored == current) return;
        if (stored.Length > 0) ConvertLeftoverTrowels(cfg);   // önceki sezondan kalan kürekler altına
        foreach (var k in new[] { KeyTrowels, KeyFloor, KeyDug, KeyFound }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.SetString(KeyCycle, current);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// Harcanmamış kürekleri altına çevirir (altın hemen verilir; duyuru ana menüde bir kez gösterilir).
    /// Kaydetmeyi çağıran yapar.
    private static void ConvertLeftoverTrowels(HarvestConfig cfg)
    {
        int trowels = Trowels;
        if (trowels <= 0) return;
        PlayerPrefs.SetInt(KeyTrowels, 0);
        int gold = cfg != null ? trowels * cfg.trowelGoldValue : 0;
        if (gold <= 0) return;
        DailySlotRewardService.Grant(new DailySlotReward { type = DailySlotRewardType.Coins, amount = gold });
        PlayerPrefs.SetInt(KeyPendingTrowels, PlayerPrefs.GetInt(KeyPendingTrowels, 0) + trowels);
        PlayerPrefs.SetInt(KeyPendingGold, PlayerPrefs.GetInt(KeyPendingGold, 0) + gold);
        GameAnalytics.LogEvent("harvest_trowels_to_gold", ("trowels", trowels), ("gold", gold));
        Debug.Log($"[Harvest] Event bitti → {trowels} kürek = {gold} altın.");
    }

    /// Duyurulmamış sezon sonu dönüşümü varsa alır ve temizler (ana menü bir kez gösterir).
    public static bool TryTakeLeftoverAnnouncement(out int trowels, out int gold)
    {
        trowels = PlayerPrefs.GetInt(KeyPendingTrowels, 0);
        gold = PlayerPrefs.GetInt(KeyPendingGold, 0);
        if (gold <= 0) return false;
        PlayerPrefs.DeleteKey(KeyPendingTrowels);
        PlayerPrefs.DeleteKey(KeyPendingGold);
        PlayerPrefs.Save();
        return true;
    }

    /// Bu level kazanılırsa verilecek kürek (fail balonunda "elinden gidecek" miktarı da budur).
    public static int TrowelsForWin(HarvestConfig cfg, bool firstTry, bool boss)
    {
        if (cfg == null) return 0;
        if (boss) return cfg.bossWinTrowels;
        return cfg.trowelsPerWin + (firstTry ? cfg.firstTryBonus : 0);
    }

    // ── Kancalar: galibiyette kürek + fail balonu ────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterHooks()
    {
        PlayerStats.OnLevelCleared -= HandleLevelCleared;
        PlayerStats.OnLevelCleared += HandleLevelCleared;

        // Vazgeçince bu level'ın vereceği kürek gider → fail balonunda görünsün (riskteki kazanım kuralı).
        LevelLossRegistry.Register("harvest", () =>
        {
            var cfg = HarvestConfig.Shared;
            DateTime now = DateTime.UtcNow;
            if (!IsLive(cfg, now)) return null;
            SyncCycle(cfg, now);
            if (IsFinished(cfg)) return null;
            int amount = TrowelsForWin(cfg, PlayerStats.IsCurrentLevelFirstTry, IsBossLevelActive());
            if (amount <= 0) return null;
            string label = GameLocalization.Get("harvest_loss_trowel");
            if (string.IsNullOrEmpty(label) || label == "harvest_loss_trowel") label = "Kürek";
            return new[] { new LevelLossItem(cfg.trowelIcon, label, amount, false) };
        });
    }

    private static void HandleLevelCleared()
    {
        var cfg = HarvestConfig.Shared;
        DateTime now = DateTime.UtcNow;
        if (!IsLive(cfg, now)) return;
        SyncCycle(cfg, now);              // önce sezon: eski sezonun "bitti" durumu yeni sezonu engellemesin
        if (IsFinished(cfg)) return;
        int amount = TrowelsForWin(cfg, PlayerStats.LastClearWasFirstTry, IsBossLevelActive());
        AddTrowels(amount);
        Debug.Log($"[Harvest] Level kazanıldı → +{amount} kürek (toplam {Trowels}).");
    }

    // Kazanılan/oynanan level boss düellosu mu? (oyun sahnesindeki board'dan okunur)
    private static bool IsBossLevelActive()
    {
        var board = UnityEngine.Object.FindFirstObjectByType<BoardController>();
        var level = board != null ? board.ActiveLevelData : null;
        return level != null && level.levelKind == LevelKind.BossDuel;
    }

    public static bool IsDug(int cell) => (DugMask & (1 << cell)) != 0;
    public static bool IsFound(int placement) => (FoundMask & (1 << placement)) != 0;

    public static void AddTrowels(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(KeyTrowels, Trowels + amount);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public struct DigResult
    {
        public int cell;
        public int placement;          // kazılan hücrenin ait olduğu ürün yerleşimi (-1 = boş toprak)
        public bool cropCompleted;     // bu kazıyla ürünün son karesi açıldı
        public bool floorCompleted;    // bu kazıyla kattaki son ürün bulundu
        public int completedFloor;     // floorCompleted ise biten katın indeksi
    }

    /// Hücreyi kazar. Kürek yoksa, hücre zaten kazılmışsa ya da event bittiyse false.
    public static bool TryDig(HarvestConfig cfg, int cell, out DigResult result)
    {
        result = default;
        result.cell = cell;
        result.placement = -1;
        if (cfg == null || IsFinished(cfg) || cell < 0 || cell >= cfg.CellCount) return false;
        if (Trowels <= 0 || IsDug(cell)) return false;

        var floor = cfg.GetFloor(Floor);
        int dug = DugMask | (1 << cell);
        int found = FoundMask;
        result.placement = cfg.PlacementAt(floor, cell);

        if (result.placement >= 0)
        {
            int mask = cfg.PlacementMask(floor.placements[result.placement]);
            if ((dug & mask) == mask && (found & (1 << result.placement)) == 0)
            {
                found |= 1 << result.placement;
                result.cropCompleted = true;
            }
        }

        int allFound = (1 << floor.placements.Length) - 1;
        if (result.cropCompleted && (found & allFound) == allFound)
        {
            result.floorCompleted = true;
            result.completedFloor = Floor;
            foreach (var reward in floor.rewards)
                DailySlotRewardService.Grant(reward);
            GameAnalytics.LogEvent("harvest_round_complete", ("round", Floor + 1), ("trowels_left", Trowels - 1));
            PlayerPrefs.SetInt(KeyFloor, Floor + 1);
            dug = 0;
            found = 0;
        }

        PlayerPrefs.SetInt(KeyTrowels, Trowels - 1);
        PlayerPrefs.SetInt(KeyDug, dug);
        PlayerPrefs.SetInt(KeyFound, found);
        // Son hasat da bitti: event bu oyuncu için tamam → kalan kürekler altına.
        if (result.floorCompleted && IsFinished(cfg)) ConvertLeftoverTrowels(cfg);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

#if UNITY_EDITOR
    public static void DebugReset()
    {
        foreach (var k in new[] { KeyTrowels, KeyFloor, KeyDug, KeyFound, KeyCycle, KeyPendingTrowels, KeyPendingGold })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
#endif
}
