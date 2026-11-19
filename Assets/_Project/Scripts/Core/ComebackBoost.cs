using System.Collections.Generic;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Returning-player boost: after 36+ inactive hours, every unlocked in-level joker and pre-level special is
/// free to use for a limited time (30 min; 45 min after 72+ hours). No counts are granted — only time.
/// The timer starts when the player taps Start, not on arrival. At most once per 7 days.
/// Absence uses the same device clock as LevelAssist (the game has no trusted server time yet).
/// </summary>
public static class ComebackBoost
{
    public const double MinAbsenceHours = 36;
    public const double LongAbsenceHours = 72;
    public const int ShortMinutes = 30;
    public const int LongMinutes = 45;
    private const int MinLevel = 2;           // at least one level played
    private const int SpecialsUnlockLevel = 6; // PreLevelSpecialPopupController.specialsUnlockLevel
    private const double CooldownDays = 7;

    // Device-local like the absence clock (see WelcomeBackReward).
    private const string PendingKey = "comeback_boost_pending_minutes";
    private const string LastGrantKey = "comeback_boost_last_grant_ticks";

    private static readonly DailySlotRewardType[] Grants =
    {
        DailySlotRewardType.Booster_Hammer, DailySlotRewardType.Booster_Row, DailySlotRewardType.Booster_Column,
        DailySlotRewardType.Booster_Shuffle, DailySlotRewardType.Booster_PaintBucket,
        DailySlotRewardType.Joker_Line, DailySlotRewardType.Joker_PulseCore, DailySlotRewardType.Joker_SystemOverride,
    };

    private static bool shownThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => shownThisSession = false;

    /// <summary>Would a return at <paramref name="utc"/> earn the boost? (notification planning)</summary>
    public static bool WouldQualifyAt(DateTime utc)
    {
        if (HasPending) return true;
        if (CurrentLevel.Global < MinLevel) return false;
        long last = ReadTicks(LastGrantKey);
        return last <= 0 || utc.Ticks - last >= TimeSpan.FromDays(CooldownDays).Ticks;
    }

    public static int PendingMinutes => PlayerPrefs.GetInt(PendingKey, 0);
    public static bool HasPending => PendingMinutes > 0;

    internal static void Queue(TimeSpan absence, long nowTicks)
    {
        if (RuntimeSimulationSession.IsActive || HasPending || absence.TotalHours < MinAbsenceHours) return;
        // 2026-11-17: jokers unlock at 12 but most day-1 players stop at 2–15 → they got nothing (and no notification).
        // Now everyone from level 2 gets the window; it always includes infinite lives (Gifts()).
        if (CurrentLevel.Global < MinLevel) return;
        long last = ReadTicks(LastGrantKey);
        if (last > 0 && last <= nowTicks && nowTicks - last < TimeSpan.FromDays(CooldownDays).Ticks) return;
        PlayerPrefs.SetInt(PendingKey, absence.TotalHours >= LongAbsenceHours ? LongMinutes : ShortMinutes);
        // LevelAssist.ResumeActivity saves together with its activity checkpoint.
    }

    /// <summary>Starts the free window now. Stacks on any running timed reward (TimedRewardService extends).</summary>
    public static bool Claim()
    {
        int minutes = PendingMinutes;
        if (RuntimeSimulationSession.IsActive || minutes <= 0) return false;
        foreach (var gift in Gifts(minutes)) TimedRewardService.Grant(gift.type, minutes);
        PlayerPrefs.SetInt(PendingKey, 0);
        PlayerPrefs.SetString(LastGrantKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        foreach (var type in Grants)
            if (type >= DailySlotRewardType.Booster_Hammer && type <= DailySlotRewardType.Booster_PaintBucket)
                BoosterInventory.NotifyCountChanged(ToMode(type));
        return true;
    }

    /// <summary>
    /// What the window contains for THIS player: infinite lives always, plus only the jokers/pre-level specials that are
    /// already unlocked (locked ones would be shown but unusable).
    /// </summary>
    public static List<DailySlotReward> Gifts(int minutes)
    {
        var list = new List<DailySlotReward> { new DailySlotReward { type = DailySlotRewardType.Lives, durationMinutes = minutes } };
        bool specials = CurrentLevel.Global >= SpecialsUnlockLevel;
        foreach (var type in Grants)
        {
            bool open = type >= DailySlotRewardType.Booster_Hammer && type <= DailySlotRewardType.Booster_PaintBucket
                ? BoosterAccessService.IsUnlocked((int)type - (int)DailySlotRewardType.Booster_Hammer)
                : specials;
            if (open) list.Add(new DailySlotReward { type = type, durationMinutes = minutes });
        }
        return list;
    }

    /// <summary>Main-menu popup; driven by the existing one-second lives tick (no extra update loop).</summary>
    public static void TickPresenter()
    {
        if (shownThisSession || !HasPending || RuntimeSimulationSession.IsActive
            || SceneManager.GetActiveScene().name != "MainMenu"
            || !FirebaseCloudSaveService.RestoreResolved
            || LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible || RuntimeChoicePopup.IsShowing
            || ShopPurchaseCelebration.IsShowing || RewardChestRevealOverlay.IsShowing
            || MarketNavigator.IsMenuMarketOpen()) return;

        var popups = UnityEngine.Object.FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++)
            if (popups[i].IsVisible) return;

        if (DailySlotEventButton.WheelHasPriority || ComebackGiftOverlay.IsShowing) return;   // günlük çark önce

        // Once per session: the gift starts only when the player taps; closing the app keeps it for next session.
        shownThisSession = true;
        var gifts = Gifts(PendingMinutes);
        // 72h+ absence: the welcome-back coins join the same ceremony instead of a separate chest.
        bool coins = WelcomeBackReward.HasPending;
        if (coins) gifts.Insert(0, new DailySlotReward { type = DailySlotRewardType.Coins, amount = WelcomeBackReward.Coins });
        ComebackGiftOverlay.Show(gifts, () =>
        {
            if (coins && WelcomeBackReward.GrantPending()) FirebaseCloudSaveService.MarkDirty();
            if (Claim()) EventSfx.Play(x => x.uiTap);
        });
    }

    private static BoardController.BoosterMode ToMode(DailySlotRewardType type) => type switch
    {
        DailySlotRewardType.Booster_Hammer      => BoardController.BoosterMode.Single,
        DailySlotRewardType.Booster_Row         => BoardController.BoosterMode.Row,
        DailySlotRewardType.Booster_Column      => BoardController.BoosterMode.Column,
        DailySlotRewardType.Booster_Shuffle     => BoardController.BoosterMode.Shuffle,
        DailySlotRewardType.Booster_PaintBucket => BoardController.BoosterMode.PaintBucket,
        _                                       => BoardController.BoosterMode.None
    };

    private static long ReadTicks(string key)
        => long.TryParse(PlayerPrefs.GetString(key, ""), out long ticks) ? ticks : 0;
}
