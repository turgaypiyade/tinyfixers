using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pençe Makinesi kalıcı durumu (CloudSaveManifest: clawmachine_state_v1). Bir deneme jetonu harcar ve sonucu,
/// merhamet sayaçlarını, makine içeriğini ve ödül teslimini SUNUMDAN ÖNCE tek kayıtta yazar (Hediye Şenliği kalıbı):
/// kapatma/çökme jetonu yakmaz, ödülü çiftlemez. Altın/can kalemi cüzdanla aynı checkpoint'le yazılır.
/// Döngü oyuncuya özeldir (ilk açılış anı = çapa): N saat açık, M saat ara. Yeni döngüde jeton ve koleksiyon sıfırlanır.
/// </summary>
public static class ClawMachineState
{
    public const string SaveKey = "clawmachine_state_v1";
    public const int CapsuleBase = 100;   // item < 100 = peluş id; ≥ 100 = kapsül (renk = item - 100)
    private static SaveData data;
    private static string lastJson;
    private static bool delivering;
    public static event Action OnChanged;

    [Serializable]
    public sealed class SaveData
    {
        public long anchorTicks;
        public int cycle = -1;
        public bool joined;
        public int tokens, lastWinLevel;
        public int collectionMask;            // 6 bit
        public bool collectionRewarded;
        public int attempts, misses, sinceBig; // sezon denemesi, art arda kaçırış, büyük ödülden beri deneme
        public int rng;
        public int[] items = Array.Empty<int>();
        // Kapsülün içindeki ödül (cfg.prizes indeksi; peluşta -1). Önceden belli ve görünür (2026-11-28: sürpriz
        // kapsül kalktı — oyuncu hedefini seçer, altınla alınan jeton rastgele ödüle gitmez).
        public int[] itemPrize = Array.Empty<int>();
        public int buyDay = -1, buysToday;
        public bool tutorialDone;               // ömür boyu
        // Sezon sonu (2026-11-28): kalan jeton sessizce silinmez — özet bir kez gösterilir.
        public bool endSummaryDone, summaryPending;
        // Açık günde oyun oturumu (kullanıcı 2026-11-28): ilk denemeyle 30 dk başlar, bitince 2 sa mola.
        public long sessionStartTicks, cooldownUntilTicks;
        public int winsTowardToken;   // normal seviye kazanışları (winsPerToken'da 1 jeton)
        public int summaryPlush, summaryTokens;
        public int delivered;
        public DailySlotReward[] delivery = Array.Empty<DailySlotReward>();
    }

    public readonly struct Attempt
    {
        public readonly ClawMachineRules.Outcome Outcome;
        public readonly int Slot, Item, NewItem, PrizeIndex, PlushId, BonusTokens;
        public readonly bool Strong, NewPlush, CollectionCompleted;
        public readonly float Accuracy;
        public bool IsPlush => Item >= 0 && Item < CapsuleBase;
        public Attempt(ClawMachineRules.Outcome outcome, int slot, int item, int newItem, int prizeIndex, int plushId,
            int bonusTokens, bool strong, bool newPlush, bool completed, float accuracy)
        {
            Outcome = outcome; Slot = slot; Item = item; NewItem = newItem; PrizeIndex = prizeIndex; PlushId = plushId;
            BonusTokens = bonusTokens; Strong = strong; NewPlush = newPlush; CollectionCompleted = completed;
            Accuracy = accuracy;
        }
    }

    public static SaveData Data { get { if (data == null) Reload(); return data; } }
    public static int Tokens => Data.tokens;
    public static bool HasJoined => Data.joined;
    public static int CollectionMask => Data.collectionMask;
    public static int Misses => Data.misses;
    public static bool TutorialDone => Data.tutorialDone;
    public static IReadOnlyList<int> Items => Data.items;
    /// Yuvadaki kapsülün ödülü (cfg.prizes indeksi) — peluş/geçersizse -1.
    public static int PrizeAt(int slot) => Data.itemPrize != null && slot >= 0 && slot < Data.itemPrize.Length ? Data.itemPrize[slot] : -1;
    public static bool HasPlush(int id) => (Data.collectionMask & (1 << id)) != 0;
    public static int CollectedCount { get { int n = 0; for (int i = 0; i < ClawMachineConfig.PlushCount; i++) if (HasPlush(i)) n++; return n; } }

    public static bool IsUnlocked(ClawMachineConfig cfg)
    {
        if (cfg == null) return false;
#if UNITY_EDITOR
        if (cfg.debugForceAvailable) return true;
#endif
        return CurrentLevel.Global >= cfg.unlockLevel;
    }

    public static GiftFestivalRules.Window Window(ClawMachineConfig cfg, DateTime now)
    {
        if (cfg == null || Data.anchorTicks <= 0 || Data.anchorTicks > DateTime.MaxValue.Ticks) return default;
        return GiftFestivalRules.GetWindow(new DateTime(Data.anchorTicks, DateTimeKind.Utc), now, cfg.activeHours, cfg.restHours);
    }
    /// Sezon aktif (jeton birikir; kapalı günlerde de). Haftalık sezon + kısa ara (config activeHours/restHours).
    public static bool IsSeasonActive(ClawMachineConfig cfg, DateTime now) => IsUnlocked(cfg) && Window(cfg, now).Active;

    /// Makine bugün açık mı: sezon içinde gün aşırı (sezon başlangıcından sayılan çift günler).
    public static bool IsOpenDay(ClawMachineConfig cfg, DateTime now)
    {
        if (!IsSeasonActive(cfg, now)) return false;
        if (!cfg.everyOtherDay) return true;
        return DayInSeason(cfg, now) % 2 == 0;
    }
    private static long DayInSeason(ClawMachineConfig cfg, DateTime now) => (now - Window(cfg, now).Start).Ticks / TimeSpan.TicksPerDay;

    /// Ekran/katılım/altınla alım: açık gün. Deneme ayrıca molaya bakar (CanGrab).
    public static bool IsLive(ClawMachineConfig cfg, DateTime now) => IsOpenDay(cfg, now);
    public static bool InCooldown(DateTime now) => Data.cooldownUntilTicks > now.Ticks;
    public static DateTime CooldownUntil => new DateTime(Math.Max(0, Data.cooldownUntilTicks), DateTimeKind.Utc);
    public static bool CanGrab(ClawMachineConfig cfg, DateTime now) => IsLive(cfg, now) && !InCooldown(now);

    /// Süren oturumun kalan süresi (oturum yoksa sıfır).
    public static TimeSpan SessionLeft(ClawMachineConfig cfg, DateTime now)
    {
        if (cfg == null || Data.sessionStartTicks <= 0) return TimeSpan.Zero;
        var left = new DateTime(Data.sessionStartTicks, DateTimeKind.Utc).AddMinutes(cfg.sessionMinutes) - now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// Açık günün bitişi (bugün kapanış) — sezon bitişinden önceyse o.
    public static DateTime OpenDayEnd(ClawMachineConfig cfg, DateTime now)
    {
        var w = Window(cfg, now);
        var end = w.Start.AddDays(DayInSeason(cfg, now) + 1);
        return end < w.End ? end : w.End;
    }

    /// Makinenin bir sonraki açılışı: molada mola sonu, kapalı günde sıradaki açık gün, ara dönemde yeni sezon.
    public static DateTime NextOpen(ClawMachineConfig cfg, DateTime now)
    {
        if (cfg == null) return DateTime.MinValue;
        var w = Window(cfg, now);
        if (!w.Active) return w.NextStart;
        if (IsOpenDay(cfg, now)) return InCooldown(now) ? CooldownUntil : now;
        var next = w.Start.AddDays(DayInSeason(cfg, now) + 1);
        return next < w.End ? next : w.NextStart;
    }

    /// Sıradaki denemede pençe güçlü mü (Pençe Gücü dolu ya da öğretici denemesi). Yakalamayı garanti etmez:
    /// kenar bölgesi de tutar, nişan yine şart (2026-11-28, beceri tabanlı).
    public static bool NextIsStrong(ClawMachineConfig cfg) => cfg != null
        && (!Data.tutorialDone || Data.misses >= cfg.missesForPower);
    /// Ömür boyu ilk deneme: geniş tolerans + yönlendirme yazısı.
    public static bool IsTutorialAttempt => !Data.tutorialDone;
    /// Sezonun ilk untimedAttempts denemesi süresiz; sonrası süre sınırlı.
    public static bool NextIsTimed(ClawMachineConfig cfg) => cfg != null && Data.attempts >= cfg.untimedAttempts;
    public static int BuysLeftToday(ClawMachineConfig cfg, DateTime now)
        => cfg == null ? 0 : Math.Max(0, cfg.coinTokensPerDay - (Data.buyDay == DayIndex(now) ? Data.buysToday : 0));

    private static int DayIndex(DateTime now) => (int)(now.Ticks / TimeSpan.TicksPerDay);

    public static void Sync(ClawMachineConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || cfg == null || delivering) return;
        bool restored = Reload();
        Deliver();
        if (!IsUnlocked(cfg)) { if (restored) OnChanged?.Invoke(); return; }
        bool changed = EnsureCycle(cfg, now);
        changed |= CloseEndedSeason(cfg, now);
        changed |= CloseSession(cfg, now);
        changed |= EnsureItems(cfg);
        if (changed) Save();
        if (changed || restored) OnChanged?.Invoke();
    }

    private static bool EnsureCycle(ClawMachineConfig cfg, DateTime now)
    {
        bool changed = false;
        if (Data.anchorTicks <= 0 || Data.anchorTicks > DateTime.MaxValue.Ticks)
        {
            Data.anchorTicks = now.Ticks; Data.cycle = -1; changed = true;
        }
        var window = Window(cfg, now);
        if (window.Active && window.Cycle != Data.cycle)
        {
            // Yeni sezon: kalan jeton ve koleksiyon silinir (plan §3/§7; giriş ekranında açıkça yazar).
            Data.cycle = window.Cycle;
            Data.joined = false; Data.tokens = 0;
            Data.collectionMask = 0; Data.collectionRewarded = false;
            Data.attempts = Data.misses = Data.sinceBig = 0;
            Data.items = Array.Empty<int>();
            Data.endSummaryDone = false;   // bekleyen (gösterilmemiş) eski özet korunur
            Data.sessionStartTicks = Data.cooldownUntilTicks = 0;
            changed = true;
        }
        return changed;
    }

    /// Oturum süresi dolduysa mola başlar (oturum başı + süre + mola). Kapalı güne geçince oturum da kapanır.
    private static bool CloseSession(ClawMachineConfig cfg, DateTime now)
    {
        if (Data.sessionStartTicks <= 0) return false;
        var end = new DateTime(Data.sessionStartTicks, DateTimeKind.Utc).AddMinutes(cfg.sessionMinutes);
        if (now < end && IsOpenDay(cfg, now)) return false;
        Data.cooldownUntilTicks = end.AddMinutes(cfg.sessionRestMinutes).Ticks;
        Data.sessionStartTicks = 0;
        return true;
    }

    /// Sezon bitti (ara dönem başladı): kalan jetonlar silinir, özet ana menüde bir kez gösterilmek üzere saklanır.
    private static bool CloseEndedSeason(ClawMachineConfig cfg, DateTime now)
    {
        if (!Data.joined || Data.endSummaryDone || Data.cycle < 0 || Window(cfg, now).Active) return false;
        Data.endSummaryDone = true;
        Data.summaryPending = true;
        Data.summaryPlush = CollectedCount;
        Data.summaryTokens = Data.tokens;
        Data.tokens = 0;
        return true;
    }

    /// Bekleyen sezon sonu özetini bir kez verir (gösterildi sayılır).
    public static bool TryTakeSummary(out int plush, out int tokens)
    {
        plush = tokens = 0;
        if (!Data.summaryPending) return false;
        plush = Data.summaryPlush; tokens = Data.summaryTokens;
        Data.summaryPending = false;
        Save();
        return true;
    }

    private static bool EnsureItems(ClawMachineConfig cfg)
    {
        int count = cfg.ItemCount;
        if (Data.items != null && Data.items.Length == count)
        {
            // Eski kayıt (ödülsüz kapsüller): içerikleri bir kez çekilip görünür yapılır.
            if (Data.itemPrize != null && Data.itemPrize.Length == count) return false;
            Data.itemPrize = new int[count];
            for (int i = 0; i < count; i++) Data.itemPrize[i] = Data.items[i] < CapsuleBase ? -1 : PickPrize(cfg, -1);
            EnsureBigVisible(cfg, -1);
            return true;
        }
        if (Data.rng == 0) { Data.rng = Guid.NewGuid().GetHashCode(); if (Data.rng == 0) Data.rng = 7; }
        Data.items = new int[count];
        Data.itemPrize = new int[count];
        for (int i = 0; i < count; i++) RollSlot(cfg, i);
        EnsurePlush(cfg, -1);
        EnsureBigVisible(cfg, -1);
        return true;
    }

    // Makinede her zaman en az minPlushVisible peluş görünsün (koleksiyon hedefi okunur olsun).
    // preferSlot: yeni yakalanıp yenilenen yuva — eksik peluş önce oraya düşer.
    private static void EnsurePlush(ClawMachineConfig cfg, int preferSlot)
    {
        int count = Data.items.Length, plush = 0;
        foreach (int it in Data.items) if (it < CapsuleBase) plush++;
        int min = Mathf.Clamp(cfg.minPlushVisible, 0, count);
        if (plush >= min) return;
        if (preferSlot >= 0 && preferSlot < count && Data.items[preferSlot] >= CapsuleBase)
        { Data.items[preferSlot] = ClawMachineRules.Next(ref Data.rng) % ClawMachineConfig.PlushCount; Data.itemPrize[preferSlot] = -1; plush++; }
        for (int i = 0; plush < min && i < count; i++)
            if (Data.items[i] >= CapsuleBase) { Data.items[i] = ClawMachineRules.Next(ref Data.rng) % ClawMachineConfig.PlushCount; Data.itemPrize[i] = -1; plush++; }
    }

    /// Yuvaya yeni oyuncak: peluş ya da kapsül + içindeki ödül (önceden belli).
    private static void RollSlot(ClawMachineConfig cfg, int slot)
    {
        int item = RollItem(cfg);
        Data.items[slot] = item;
        Data.itemPrize[slot] = item < CapsuleBase ? -1 : PickPrize(cfg, -1);
    }

    /// Makinede her an en az bir büyük ödüllü kapsül görünür (eski "N. denemede büyük ödül" garantisinin dürüst
    /// karşılığı: oyuncu görür ve hedefler). preferSlot: yeni yenilenen yuva önce denenir; peluşlara dokunulmaz.
    private static void EnsureBigVisible(ClawMachineConfig cfg, int preferSlot)
    {
        int count = Data.items.Length;
        for (int i = 0; i < count; i++)
        {
            var p = cfg.Prize(Data.itemPrize[i]);
            if (p != null && p.big) return;
        }
        int target = preferSlot >= 0 && preferSlot < count && Data.items[preferSlot] >= CapsuleBase ? preferSlot : -1;
        for (int i = 0; target < 0 && i < count; i++) if (Data.items[i] >= CapsuleBase) target = i;
        if (target >= 0) Data.itemPrize[target] = PickPrize(cfg, 1);
    }

    private static int RollItem(ClawMachineConfig cfg)
    {
        int capsuleWeight = 0;
        if (cfg.prizes != null) foreach (var p in cfg.prizes) if (p != null) capsuleWeight += Math.Max(0, p.weight);
        int pick = ClawMachineRules.Weighted(ref Data.rng, new[] { Math.Max(0, cfg.plushWeight), Math.Max(1, capsuleWeight) });
        if (pick == 0) return ClawMachineRules.Next(ref Data.rng) % ClawMachineConfig.PlushCount;
        int colors = cfg.capsuleColors != null && cfg.capsuleColors.Length > 0 ? cfg.capsuleColors.Length : 1;
        return CapsuleBase + ClawMachineRules.Next(ref Data.rng) % colors;
    }

    public static bool TryJoin(ClawMachineConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (!IsLive(cfg, now)) return false;
        if (HasJoined) return true;
        Data.joined = true;
        Data.tokens = Math.Min(cfg.tokenCap, Data.tokens + cfg.joinTokens);
        // Katılımdan önceki kazanımlar sonradan jeton yazdırmaz.
        Data.lastWinLevel = Math.Max(Data.lastWinLevel, CurrentLevel.Global - 1);
        Save(); OnChanged?.Invoke();
        GameAnalytics.LogEvent("claw_join", ("level", CurrentLevel.Global));
        return true;
    }

    public static bool TryBuyToken(ClawMachineConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (!IsLive(cfg, now) || !HasJoined || Data.tokens >= cfg.tokenCap || BuysLeftToday(cfg, now) <= 0) return false;
        if (!PlayerWallet.SpendCoins(cfg.coinTokenPrice)) return false;
        int day = DayIndex(now);
        if (Data.buyDay != day) { Data.buyDay = day; Data.buysToday = 0; }
        Data.buysToday++;
        Data.tokens++;
        Save(); OnChanged?.Invoke();
        GameAnalytics.LogEvent("claw_token_buy", ("price", cfg.coinTokenPrice), ("level", CurrentLevel.Global));
        return true;
    }

    /// Bir deneme: jeton düşer, sonuç + ödül + yeni oyuncak + sayaçlar tek kayıtta yazılır, sonra sunum başlar.
    public static bool TryGrab(ClawMachineConfig cfg, float clawU, float clawW, DateTime now, out Attempt attempt)
    {
        attempt = default;
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (!CanGrab(cfg, now) || !HasJoined || Data.tokens <= 0 || Data.items.Length == 0) return false;
        if (Data.sessionStartTicks <= 0) Data.sessionStartTicks = now.Ticks;   // oturum ilk denemeyle başlar

        int count = Data.items.Length;
        bool tutorial = !Data.tutorialDone;
        float tolerance = tutorial ? cfg.tutorialTolerance : 1f;
        int slot = ClawMachineRules.Nearest(Mathf.Clamp01(clawU), Mathf.Clamp01(clawW), count,
            cfg.itemHalfWidth * tolerance, cfg.itemHalfDepth * tolerance, out float accuracy);
        if (slot < 0) return false;
        bool strong = tutorial || Data.misses >= cfg.missesForPower;
        bool plushItem = Data.items[slot] < CapsuleBase;
        var outcome = ClawMachineRules.Resolve(accuracy,
            plushItem ? cfg.plushCoreAccuracy : cfg.coreAccuracy, cfg.slipAccuracy,
            plushItem ? cfg.plushEdgeGrip : cfg.edgeGrip, strong, ClawMachineRules.NextDouble(ref Data.rng));

        Data.tokens--;
        Data.attempts++;
        Data.tutorialDone = true;
        int item = Data.items[slot], newItem = item, prizeIndex = -1, plushId = -1, bonusTokens = 0;
        bool newPlush = false, completed = false, gotBig = false;
        var rewards = new List<DailySlotReward>(8);

        if (outcome == ClawMachineRules.Outcome.Catch)
        {
            Data.misses = 0;
            if (item < CapsuleBase)
            {
                plushId = item; gotBig = true;
                if (!HasPlush(plushId)) { Data.collectionMask |= 1 << plushId; newPlush = true; }
                else bonusTokens = cfg.duplicatePlushTokens;
                if (newPlush && CollectedCount >= ClawMachineConfig.PlushCount && !Data.collectionRewarded)
                {
                    Data.collectionRewarded = true; completed = true;
                    if (cfg.collectionRewards != null) foreach (var r in cfg.collectionRewards) if (r != null) rewards.Add(r);
                }
            }
            else
            {
                // İlk deneme küçük kapsül, büyük sırası büyük kapsül; aksi halde ağırlıklı çekiliş.
                prizeIndex = PrizeAt(slot) >= 0 ? PrizeAt(slot) : PickPrize(cfg, -1);   // görünen ödül
                var prize = cfg.Prize(prizeIndex);
                if (prize != null) { gotBig = prize.big; rewards.Add(prize.reward); }
            }
            RollSlot(cfg, slot);
            EnsurePlush(cfg, slot);
            EnsureBigVisible(cfg, slot);
            newItem = Data.items[slot];
        }
        else Data.misses++;

        Data.sinceBig = gotBig ? 0 : Data.sinceBig + 1;
        if (bonusTokens > 0) Data.tokens = Math.Min(cfg.tokenCap, Data.tokens + bonusTokens);
        Data.delivery = rewards.ToArray();
        Data.delivered = 0;
        if (Data.delivery.Length > 0) Deliver(); else Save();

        attempt = new Attempt(outcome, slot, item, newItem, prizeIndex, plushId, bonusTokens, strong, newPlush, completed, accuracy);
        // Denge ölçümü: isabet dağılımı + sonuç; toleranslar config'ten uzaktan ayarlanır.
        GameAnalytics.LogEvent("claw_attempt", ("result", (long)outcome), ("accuracy_pct", (long)Mathf.RoundToInt(accuracy * 100)),
            ("strong", strong ? 1 : 0), ("plush", plushItem ? 1 : 0), ("row", ClawMachineRules.Row(slot)),
            ("attempt", Data.attempts), ("level", CurrentLevel.Global));
        if (completed) GameAnalytics.LogEvent("claw_collection_complete", ("attempts", Data.attempts));
        OnChanged?.Invoke();
        return true;
    }

    // Oyuncunun henüz açmadığı joker kapsüle konmaz (L16'da açılır; jokerler 12/14/22/27/32'de).
    private static bool Unlocked(DailySlotReward r)
    {
        if (r == null) return false;
        int index = r.type switch
        {
            DailySlotRewardType.Booster_Hammer => 0, DailySlotRewardType.Booster_Row => 1,
            DailySlotRewardType.Booster_Column => 2, DailySlotRewardType.Booster_Shuffle => 3,
            DailySlotRewardType.Booster_PaintBucket => 4, _ => -1,
        };
        return index < 0 || BoosterAccessService.IsUnlocked(index);
    }

    // tier: 0 = yalnız küçük, 1 = yalnız büyük, -1 = hepsi.
    private static int PickPrize(ClawMachineConfig cfg, int tier)
    {
        if (cfg.prizes == null || cfg.prizes.Length == 0) return -1;
        var weights = new int[cfg.prizes.Length];
        bool any = false;
        for (int i = 0; i < weights.Length; i++)
        {
            var p = cfg.prizes[i];
            bool ok = p != null && (tier < 0 || p.big == (tier == 1)) && Unlocked(p.reward);
            weights[i] = ok ? Math.Max(0, p.weight) : 0;
            any |= weights[i] > 0;
        }
        if (!any) for (int i = 0; i < weights.Length; i++) weights[i] = cfg.prizes[i] != null ? Math.Max(0, cfg.prizes[i].weight) : 0;
        return ClawMachineRules.Weighted(ref Data.rng, weights);
    }

    private static bool Reload()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (data != null && json == lastJson) return false;
        lastJson = json;
        try { data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { data = new SaveData(); }
        data ??= new SaveData();
        data.tokens = Mathf.Max(0, data.tokens);
        data.collectionMask &= (1 << ClawMachineConfig.PlushCount) - 1;
        data.items ??= Array.Empty<int>();
        data.itemPrize ??= Array.Empty<int>();
        data.delivery ??= Array.Empty<DailySlotReward>();
        data.delivered = Mathf.Clamp(data.delivered, 0, data.delivery.Length);
        return true;
    }

    private static string Encode() => lastJson = JsonUtility.ToJson(Data);
    private static void Save()
    {
        PlayerPrefs.SetString(SaveKey, Encode());
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
    }

    private static void Deliver()
    {
        if (delivering || Data.delivery.Length == 0) return;
        delivering = true;
        try
        {
            while (Data.delivered < Data.delivery.Length)
            {
                var reward = Data.delivery[Data.delivered++];
                string checkpoint = Encode();
                if (reward != null && reward.type == DailySlotRewardType.Coins && reward.amount > 0)
                    PlayerWallet.AddCoinsWithCheckpoint(reward.amount, SaveKey, checkpoint);
                else if (reward != null && reward.type == DailySlotRewardType.Lives && !reward.IsTimed && reward.amount > 0)
                    LivesManager.AddLivesWithCheckpoint(reward.amount, SaveKey, checkpoint);
                else
                {
                    PlayerPrefs.SetString(SaveKey, checkpoint);
                    if (reward != null) DailySlotRewardService.Grant(reward);
                    PlayerPrefs.Save();
                }
            }
            Data.delivery = Array.Empty<DailySlotReward>();
            Data.delivered = 0;
            Save();
        }
        finally { delivering = false; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterHooks()
    {
        data = null; lastJson = null; delivering = false;
        PlayerStats.OnLevelCleared -= HandleLevelCleared;
        PlayerStats.OnLevelCleared += HandleLevelCleared;
        LevelLossRegistry.Register("clawmachine", Loss);
    }

    // 2026-11-28 (ekonomi): normal seviyede her winsPerToken kazanışta 1 jeton, zor seviyede hemen tokensPerHardWin.
    private static bool IsHard(int level) => LevelDifficultyTag.For(level) != LevelDifficulty.Normal;
    private static int TokensForWin(ClawMachineConfig cfg, int level)
        => IsHard(level) ? cfg.tokensPerHardWin
            : Data.winsTowardToken + 1 >= Math.Max(1, cfg.winsPerToken) ? 1 : 0;

    private static IEnumerable<LevelLossItem> Loss()
    {
        var cfg = ClawMachineConfig.Shared;
        DateTime now = DateTime.UtcNow;
        if (!IsUnlocked(cfg)) return null;
        Sync(cfg, now);
        if (!HasJoined || !IsSeasonActive(cfg, now) || CurrentLevel.Global <= Data.lastWinLevel || Data.tokens >= cfg.tokenCap) return null;
        int amount = Math.Min(TokensForWin(cfg, CurrentLevel.Global), cfg.tokenCap - Data.tokens);
        return amount > 0 ? new[] { new LevelLossItem(cfg.menuIcon, GameLocalization.GetOr("claw_token", "Jeton"), amount, false) } : null;
    }

    private static void HandleLevelCleared()
    {
        if (RuntimeSimulationSession.IsActive) return;
        var cfg = ClawMachineConfig.Shared;
        if (cfg == null || !IsUnlocked(cfg)) return;
        var now = DateTime.UtcNow;
        Reload();
        Deliver();
        // OnLevelCleared, CurrentLevel ilerlemeden ÖNCE gelir.
        bool changed = EnsureCycle(cfg, now);
        if (HasJoined && Window(cfg, now).Active && CurrentLevel.Global > Data.lastWinLevel)
        {
            Data.lastWinLevel = CurrentLevel.Global;
            int earned = TokensForWin(cfg, CurrentLevel.Global);
            if (!IsHard(CurrentLevel.Global)) Data.winsTowardToken = earned > 0 ? 0 : Data.winsTowardToken + 1;
            Data.tokens = Math.Min(cfg.tokenCap, Math.Max(Data.tokens, Data.tokens + earned));
            changed = true;
        }
        if (changed) { Save(); OnChanged?.Invoke(); }
    }

#if UNITY_EDITOR
    public static void DebugAddTokens(int count)
    {
        Sync(ClawMachineConfig.Shared, DateTime.UtcNow);
        Data.tokens = Math.Max(0, Data.tokens + count);
        Save(); OnChanged?.Invoke();
    }
    /// Koleksiyonun 5/6'sı işaretlenir, eksik peluş makinenin ön sırasına konur (şöleni tek yakalamayla görmek için).
    public static void DebugFillToLastPlush()
    {
        var cfg = ClawMachineConfig.Shared;
        Sync(cfg, DateTime.UtcNow);
        if (!HasJoined) { Data.joined = true; Data.tokens = Math.Max(Data.tokens, 3); }
        int last = ClawMachineConfig.PlushCount - 1;
        Data.collectionMask = ((1 << ClawMachineConfig.PlushCount) - 1) & ~(1 << last);
        Data.collectionRewarded = false;
        if (Data.items.Length > 0) { Data.items[0] = last; Data.itemPrize[0] = -1; }   // slot 0 = ön sıra, sol
        Data.tokens = Math.Max(Data.tokens, 1);
        Save(); OnChanged?.Invoke();
    }

    /// Kapalı günü ve molayı atlar: sezon şimdi başlar (bugün açık gün), oturum/mola sıfırlanır.
    public static void DebugOpenNow()
    {
        var now = DateTime.UtcNow;
        Sync(ClawMachineConfig.Shared, now);
        long cycleTicks = Window(ClawMachineConfig.Shared, now).Start.Ticks;
        long shift = now.Ticks - cycleTicks;   // çapayı kaydırıp bugünü sezonun 1. günü yap (koleksiyon korunur)
        Data.anchorTicks += shift;
        Data.sessionStartTicks = Data.cooldownUntilTicks = 0;
        Save(); OnChanged?.Invoke();
    }

    public static void DebugReset()
    {
        PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); data = null; lastJson = null;
        OnChanged?.Invoke();
    }
#endif
}
