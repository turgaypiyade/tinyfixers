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
        if (!BoosterAccessService.IsUnlocked(0)) return false;
        long last = ReadTicks(LastGrantKey);
        return last <= 0 || utc.Ticks - last >= TimeSpan.FromDays(CooldownDays).Ticks;
    }

    public static int PendingMinutes => PlayerPrefs.GetInt(PendingKey, 0);
    public static bool HasPending => PendingMinutes > 0;

    internal static void Queue(TimeSpan absence, long nowTicks)
    {
        if (RuntimeSimulationSession.IsActive || HasPending || absence.TotalHours < MinAbsenceHours) return;
        // No joker unlocked yet → a free window would be meaningless.
        if (!BoosterAccessService.IsUnlocked(0)) return;
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
        foreach (var type in Grants) TimedRewardService.Grant(type, minutes);
        PlayerPrefs.SetInt(PendingKey, 0);
        PlayerPrefs.SetString(LastGrantKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        foreach (var type in Grants)
            if (type >= DailySlotRewardType.Booster_Hammer && type <= DailySlotRewardType.Booster_PaintBucket)
                BoosterInventory.NotifyCountChanged(ToMode(type));
        return true;
    }

    /// <summary>Main-menu popup; driven by the existing one-second lives tick (no extra update loop).</summary>
    public static void TickPresenter()
    {
        if (shownThisSession || !HasPending || RuntimeSimulationSession.IsActive
            || SceneManager.GetActiveScene().name != "MainMenu"
            || WelcomeBackReward.HasPending || !FirebaseCloudSaveService.RestoreResolved
            || LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible || RuntimeChoicePopup.IsShowing
            || ShopPurchaseCelebration.IsShowing || RewardChestRevealOverlay.IsShowing
            || MarketNavigator.IsMenuMarketOpen()) return;

        var popups = UnityEngine.Object.FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++)
            if (popups[i].IsVisible) return;

        // Once per session: closing the popup keeps the gift for the next session instead of nagging.
        shownThisSession = true;
        RuntimeChoicePopup.Show(GameLocalization.GetOr("comeback_boost_title", "WE MISSED YOU!"),
            GameLocalization.GetFormat("comeback_boost_message", PendingMinutes),
            new RuntimeChoicePopup.Choice(GameLocalization.GetOr("comeback_boost_start", "START"), () =>
            {
                if (Claim()) EventSfx.Play(x => x.uiTap);
            }, true));
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
