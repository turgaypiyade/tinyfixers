using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Durable event state. A press spends one ball and resolves rewards BEFORE presentation starts.
/// Coins and the corresponding board checkpoint share the central wallet save. Closing/teardown
/// cannot duplicate rewards or leave an unresolved paid shot. This JSON belongs to CloudSaveManifest.
/// </summary>
public static class GiftFestivalState
{
    public const string SaveKey = "giftfestival_state_v1";
    private static SaveData data;
    private static string lastJson;
    private static string lastStorage;
    private static bool delivering;
    public static event Action OnChanged;

    private static string StorageKey
    {
        get
        {
#if UNITY_EDITOR
            if (PreviewOnly) return "giftfestival_editor_preview_v1";
#endif
            return SaveKey;
        }
    }
#if UNITY_EDITOR
    public static bool DebugPreview { get; set; }
    private static bool PreviewOnly => DebugPreview || (GiftFestivalConfig.Shared != null && GiftFestivalConfig.Shared.debugForceAvailable);
#endif

    [Serializable]
    public sealed class SaveData
    {
        public long anchorTicks;
        public int cycle = -1;
        public int balls, round, openMask, lastWinLevel;
        public bool joined;
        public bool invitationShown;
        // Lifetime receipts: never reset when the next festival cycle starts.
        public bool tutorialBallGranted, tutorialCompleted;
        public int rewardSeed;
        public int prizeRound = -1;
        public DailySlotReward[] boxPrizes = Array.Empty<DailySlotReward>();
        public int delivered;
        public DailySlotReward[] delivery = Array.Empty<DailySlotReward>();
    }

    public static SaveData Data
    {
        get { if (data == null) Reload(); return data; }
    }
    public static int Balls => Data.balls;
    public static int Round => Data.round;
    public static int OpenMask => Data.openMask;
    public static bool HasJoined => Data.joined;
    public static bool TutorialCompleted => Data.tutorialCompleted;
    public static bool IsFinished(GiftFestivalConfig cfg) => cfg == null || Round >= cfg.RoundCount;

    public static bool IsUnlocked(GiftFestivalConfig cfg)
    {
        if (cfg == null) return false;
#if UNITY_EDITOR
        if (cfg.debugForceAvailable || DebugPreview) return true;
#endif
        return CurrentLevel.Global > cfg.unlockAfterLevel;
    }

    public static GiftFestivalRules.Window Window(GiftFestivalConfig cfg, DateTime now)
    {
        if (cfg == null || Data.anchorTicks <= 0 || Data.anchorTicks > DateTime.MaxValue.Ticks) return default;
        return GiftFestivalRules.GetWindow(new DateTime(Data.anchorTicks, DateTimeKind.Utc), now, cfg.activeHours, cfg.restHours);
    }

    public static bool IsLive(GiftFestivalConfig cfg, DateTime now) => IsUnlocked(cfg) && Window(cfg, now).Active;

    public static bool CanShowInvitation(GiftFestivalConfig cfg, DateTime now) => !RuntimeSimulationSession.IsActive
        && IsLive(cfg, now) && !HasJoined && !IsFinished(cfg) && !Data.invitationShown;

    // Called only once the actual invitation is visible, for both automatic and icon entry.
    public static bool MarkInvitationShown(GiftFestivalConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive) return false;
        Sync(cfg, now);
        if (!CanShowInvitation(cfg, now)) return false;
        Data.invitationShown = true;
        Save();
        return true;
    }

    public static void Sync(GiftFestivalConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || cfg == null || delivering) return;
        bool restored = Reload();
        Deliver();
        if (!IsUnlocked(cfg)) { if (restored) OnChanged?.Invoke(); return; }
        bool changed = EnsureCycle(cfg, now);
        changed |= EnsurePrizes(cfg);
        if (changed) Save();
        if (changed || restored) OnChanged?.Invoke();
    }

    private static bool EnsureCycle(GiftFestivalConfig cfg, DateTime now)
    {
        bool changed = false;
        if (Data.anchorTicks <= 0 || Data.anchorTicks > DateTime.MaxValue.Ticks)
        {
            Data.anchorTicks = now.Ticks;
            Data.cycle = -1;
            changed = true;
        }
        var window = Window(cfg, now);
        if (window.Active && window.Cycle != Data.cycle)
        {
            Data.cycle = window.Cycle;
            Data.round = Data.openMask = 0;
            // Keep the one reserved introduction shot if the player left before using it.
            Data.balls = Data.tutorialBallGranted && !Data.tutorialCompleted ? 1 : 0;
            Data.joined = false;
            Data.invitationShown = false;
            Data.rewardSeed = 0; Data.prizeRound = -1;
            Data.boxPrizes = Array.Empty<DailySlotReward>();
            changed = true;
        }
        return changed;
    }

    private static bool EnsurePrizes(GiftFestivalConfig cfg)
    {
        var round = cfg.GetRound(Round);
        if (!HasJoined || round == null) return false;
        if (Data.prizeRound == Round && Data.boxPrizes != null && Data.boxPrizes.Length == round.Size * round.Size) return false;
        if (Data.rewardSeed == 0) { Data.rewardSeed = Guid.NewGuid().GetHashCode(); if (Data.rewardSeed == 0) Data.rewardSeed = 1; }
        Data.boxPrizes = GiftFestivalRewards.Create(round, cfg.emptyBoxFraction, Data.rewardSeed, Round, OpenMask);
        Data.prizeRound = Round;
        return true;
    }

    public static DailySlotReward PrizeAt(int board, int cell) => Data.prizeRound == board && Data.boxPrizes != null
        && cell >= 0 && cell < Data.boxPrizes.Length ? Data.boxPrizes[cell] : null;

    public static bool TryJoin(GiftFestivalConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (!IsLive(cfg, now) || IsFinished(cfg)) return false;
        if (HasJoined) return true;
        Data.joined = true;
        // Joining is not a reward. The first tutorial grants its own single ball on screen.
        // Wins from before joining must never be replayed to earn a ball.
        Data.lastWinLevel = Math.Max(Data.lastWinLevel, CurrentLevel.Global - 1);
        EnsurePrizes(cfg);
        Save(); OnChanged?.Invoke();
        return true;
    }

    public static bool TryBeginTutorial(GiftFestivalConfig cfg, DateTime now)
    {
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (TutorialCompleted || !HasJoined || !IsLive(cfg, now) || IsFinished(cfg)) return false;
        if (!Data.tutorialBallGranted)
        {
            Data.tutorialBallGranted = true;
            Data.balls = (int)Math.Min(int.MaxValue, (long)Balls + 1);
            Save(); OnChanged?.Invoke();
        }
        return true;
    }

    private static bool Reload()
    {
        string storage = StorageKey;
        string json = PlayerPrefs.GetString(storage, "");
        if (data != null && storage == lastStorage && json == lastJson) return false;
        lastStorage = storage;
        lastJson = json;
        try { data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { data = new SaveData(); }
        if (data == null) data = new SaveData();
        data.balls = Mathf.Max(0, data.balls);
        data.round = Mathf.Max(0, data.round);
        data.openMask &= 65535;
        data.delivery ??= Array.Empty<DailySlotReward>();
        data.delivered = Mathf.Clamp(data.delivered, 0, data.delivery.Length);
        return true;
    }

    private static string Encode() => lastJson = JsonUtility.ToJson(Data);
    private static void Save()
    {
        PlayerPrefs.SetString(StorageKey, Encode());
        PlayerPrefs.Save();
#if UNITY_EDITOR
        if (PreviewOnly) return;
#endif
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
#if UNITY_EDITOR
                if (PreviewOnly)
                {
                    PlayerPrefs.SetString(StorageKey, checkpoint);
                    PlayerPrefs.Save();
                    continue; // Preview plays the same result without touching the real wallet/inventory.
                }
#endif
                if (reward != null && reward.type == DailySlotRewardType.Coins && reward.amount > 0)
                    PlayerWallet.AddCoinsWithCheckpoint(reward.amount, StorageKey, checkpoint);
                else if (reward != null && reward.type == DailySlotRewardType.Stars && reward.amount > 0)
                    PlayerWallet.AddStarsWithCheckpoint(reward.amount, StorageKey, checkpoint);
                else if (reward != null && reward.type == DailySlotRewardType.Lives && !reward.IsTimed && reward.amount > 0)
                    LivesManager.AddLivesWithCheckpoint(reward.amount, StorageKey, checkpoint);
                else
                {
                    // Existing inventory helpers persist this receipt and their item change together.
                    PlayerPrefs.SetString(StorageKey, checkpoint);
                    DailySlotRewardService.Grant(reward);
                    PlayerPrefs.Save();
                }
            }
            Data.delivery = Array.Empty<DailySlotReward>();
            Data.delivered = 0;
            Save();
        }
        finally { delivering = false; }
    }

    public readonly struct Shot
    {
        public readonly int Round, Selected, Target, Size, Coins;
        public readonly GiftFestivalRules.Hit Hit;
        public readonly bool Opened, Completed;
        public readonly DailySlotReward BoxReward;
        public bool Empty => Opened && GiftFestivalRewards.IsEmpty(BoxReward);
        public Shot(int round, int selected, int target, int size, int coins, GiftFestivalRules.Hit hit, bool opened, bool completed, DailySlotReward boxReward)
        {
            Round = round; Selected = selected; Target = target; Size = size; Coins = coins;
            Hit = hit; Opened = opened; Completed = completed;
            BoxReward = boxReward;
        }
    }

    public static bool TryHit(GiftFestivalConfig cfg, int selected, float position, DateTime now, out Shot shot)
    {
        shot = default;
        if (RuntimeSimulationSession.IsActive || delivering) return false;
        Sync(cfg, now);
        if (!IsLive(cfg, now) || !HasJoined || IsFinished(cfg) || Balls <= 0) return false;
        var round = cfg.GetRound(Round);
        if (round == null || selected < 0 || selected >= round.Size * round.Size || GiftFestivalRules.IsOpen(OpenMask, selected)) return false;
        var hit = GiftFestivalRules.Evaluate(position, cfg.SafeRedWidth, cfg.SafeGreenWidth);
        int target = GiftFestivalRules.TargetCell(selected, round.Size, hit);
        bool opened = target >= 0 && !GiftFestivalRules.IsOpen(OpenMask, target);
        int roundIndex = Round;
        Data.balls--;
        // Commit completion with the spent shot, before reward callbacks/presentation.
        // Closing or an interrupted reward delivery must never grant a second tutorial ball.
        if (Data.tutorialBallGranted) Data.tutorialCompleted = true;
        if (opened) Data.openMask |= 1 << target;
        bool complete = opened && GiftFestivalRules.IsComplete(OpenMask, round.Size);
        var boxReward = opened ? PrizeAt(roundIndex, target) : null;
        int coins = !GiftFestivalRewards.IsEmpty(boxReward) && boxReward.type == DailySlotRewardType.Coins ? boxReward.amount : 0;
        var rewards = new List<DailySlotReward>(4);
        if (opened && !GiftFestivalRewards.IsEmpty(boxReward)) rewards.Add(boxReward);
        if (complete)
        {
            if (round.completionCoins > 0)
            {
                coins += round.completionCoins;
                rewards.Add(new DailySlotReward { type = DailySlotRewardType.Coins, amount = round.completionCoins });
            }
            Data.round++;
            Data.openMask = 0;
        }
        if (complete && round.completionRewards != null)
            for (int i = 0; i < round.completionRewards.Length; i++)
                if (round.completionRewards[i] != null) rewards.Add(round.completionRewards[i]);
        if (complete) EnsurePrizes(cfg);
        Data.delivery = rewards.ToArray();
        Data.delivered = 0;
        // No callbacks between the staged receipt and delivery; presentation starts after this finishes.
        if (Data.delivery.Length > 0) Deliver(); else Save();
        shot = new Shot(roundIndex, selected, target, round.Size, coins, hit, opened, complete, boxReward);
        OnChanged?.Invoke();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterHooks()
    {
        data = null; lastJson = null; delivering = false;
#if UNITY_EDITOR
        DebugPreview = false;
#endif
        PlayerStats.OnLevelCleared -= HandleLevelCleared;
        PlayerStats.OnLevelCleared += HandleLevelCleared;
        LevelLossRegistry.Register("giftfestival", Loss);
    }

    private static IEnumerable<LevelLossItem> Loss()
    {
        var cfg = GiftFestivalConfig.Shared;
        DateTime now = DateTime.UtcNow;
        if (!IsUnlocked(cfg)) return null;
        Sync(cfg, now);
        if (!HasJoined || !IsLive(cfg, now) || IsFinished(cfg)) return null;
        if (CurrentLevel.Global <= Data.lastWinLevel) return null;
        // The banked balls are safe; only the one ball this win would earn is at risk.
        return new[] { new LevelLossItem(cfg.Art != null ? cfg.Art.ball : null,
            GameLocalization.GetOr("giftfestival_ball", "Baseball"), 1, false) };
    }

    private static void HandleLevelCleared()
    {
        if (RuntimeSimulationSession.IsActive) return;
        var cfg = GiftFestivalConfig.Shared;
        if (cfg == null || CurrentLevel.Global < cfg.unlockAfterLevel) return;
        var now = DateTime.UtcNow;
        Reload();
        Deliver();
        // OnLevelCleared occurs BEFORE CurrentLevel is advanced. Winning 100 starts the event now.
        bool changed = EnsureCycle(cfg, now);
        if (HasJoined && Window(cfg, now).Active && !IsFinished(cfg) && CurrentLevel.Global > Data.lastWinLevel)
        {
            Data.lastWinLevel = CurrentLevel.Global;
            Data.balls = (int)Math.Min(int.MaxValue, (long)Balls + 1);
            changed = true;
        }
        if (changed) { Save(); OnChanged?.Invoke(); }
    }

#if UNITY_EDITOR
    public static void DebugAddBalls(int count)
    {
        Sync(GiftFestivalConfig.Shared, DateTime.UtcNow);
        Data.balls = (int)Math.Min(int.MaxValue, (long)Balls + Mathf.Max(0, count));
        Save(); OnChanged?.Invoke();
    }
    public static void DebugReset()
    {
        PlayerPrefs.DeleteKey(StorageKey); PlayerPrefs.Save(); data = null; lastJson = null;
        OnChanged?.Invoke();
    }
#endif
}
