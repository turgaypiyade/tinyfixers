using System;
using UnityEngine;

/// <summary>
/// Mercy only chooses more helpful NORMAL colors for newly refilling stones.
/// Ad/purchase players qualify after 9 actual lives spent on this level (12/15 raise the tier).
/// Non-monetized players qualify only on returning after 72 inactive hours, until that level is won.
/// Existing tiles are never converted, and no starting special or extra move is granted.
/// Specials may form through the ordinary match resolver after the stones land.
/// </summary>
public static class LevelAssist
{
    private const int Tier1Lives = 9;
    private const int Tier2Lives = 12;
    private const int Tier3Lives = 15;
    private const float Tier1RefillBias = 0.15f;
    private const float Tier2RefillBias = 0.30f;
    private const float Tier3RefillBias = 0.45f;

    internal const string RewardedAdKey = "mercy_has_rewarded_ad";
    internal const string PurchaseKey = "mercy_has_purchase";
    private const string ReturnLevelKey = "level_attempt_return_level";
    private const string ReturnPendingKey = "level_attempt_return_pending";
    private const string LastActiveKey = "mercy_last_active_utc_ticks";
    private static bool foreground;
    private static bool attemptEligible;
    private static long lastCheckpoint;

    // Legacy fields are evidence of completed actions, not an offer/click or a failed request.
    public static bool HasAdOrPurchase => PlayerPrefs.GetInt(RewardedAdKey, 0) != 0
        || PlayerPrefs.GetInt(PurchaseKey, 0) != 0
        || PlayerPrefs.GetInt("ad_continue_used_level", 0) > 0
        || !string.IsNullOrEmpty(PlayerPrefs.GetString("iap_processed_transactions", ""));

    private static bool HasReturnHelp(int level) => level > 0
        && PlayerPrefs.GetInt(ReturnLevelKey, 0) == level;

    private static bool IsEligible(int level) => !RuntimeSimulationSession.IsActive && level > 0
        && (HasAdOrPurchase ? LevelAttemptStats.LivesSpentOn(level) >= Tier1Lives : HasReturnHelp(level));

    public static int TierFor(int level)
    {
        if (!IsEligible(level)) return 0;
        if (!HasAdOrPurchase) return 1;
        int spent = LevelAttemptStats.LivesSpentOn(level);
        return spent >= Tier3Lives ? 3 : spent >= Tier2Lives ? 2 : 1;
    }

    // Simulation must also override a cached tier from a previous player attempt.
    private static int CurrentTier => RuntimeSimulationSession.IsActive ? 0
        : LevelAttemptStats.HasActiveAttempt ? (attemptEligible ? LevelAttemptStats.ActiveAssistTier : 0)
        : TierFor(CurrentLevel.Global);

    public static float RefillBias
    {
        get
        {
            int tier = CurrentTier;
            return tier >= 3 ? Tier3RefillBias : tier >= 2 ? Tier2RefillBias
                : tier >= 1 ? Tier1RefillBias : 0f;
        }
    }

    // Called before the attempt's normal Save. The return token binds after cloud progress resolves.
    internal static void BeginAttempt(int level)
    {
        if (PlayerPrefs.GetInt(ReturnPendingKey, 0) != 0)
        {
            PlayerPrefs.SetInt(ReturnLevelKey, HasAdOrPurchase ? 0 : level);
            PlayerPrefs.SetInt(ReturnPendingKey, 0);
        }
        else if (PlayerPrefs.GetInt(ReturnLevelKey, 0) != level)
            PlayerPrefs.SetInt(ReturnLevelKey, 0);
        RefreshAttemptEligibility(level);
    }

    // Refresh only on attempt/account/debit changes, never per refilling stone.
    internal static void RefreshAttemptEligibility(int level) => attemptEligible = IsEligible(level);

    internal static void ClearAfterWin()
    {
        PlayerPrefs.SetInt(ReturnLevelKey, 0);
        PlayerPrefs.SetInt(ReturnPendingKey, 0);
        attemptEligible = false;
    }

    internal static void RecordRewardedAdCompleted() => RecordEvidence(RewardedAdKey);
    internal static void RecordPurchaseCompleted() => RecordEvidence(PurchaseKey);

    private static void RecordEvidence(string key)
    {
        if (RuntimeSimulationSession.IsActive || PlayerPrefs.GetInt(key, 0) != 0) return;
        PlayerPrefs.SetInt(key, 1);
        if (LevelAttemptStats.HasActiveAttempt) RefreshAttemptEligibility(CurrentLevel.Global);
        PlayerPrefs.Save();
        FirebaseCloudSaveService.MarkDirty();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { foreground = false; attemptEligible = false; lastCheckpoint = 0; }

    internal static void ResumeActivity(DateTime utcNow)
    {
        if (RuntimeSimulationSession.IsActive || foreground) return;
        long now = utcNow.Ticks;
        if (long.TryParse(PlayerPrefs.GetString(LastActiveKey, ""), out long previous)
            && previous > 0 && previous <= now)
        {
            if (now - previous >= TimeSpan.TicksPerDay * 3)
            {
                PlayerPrefs.SetInt(ReturnPendingKey, 1);
                WelcomeBackReward.Queue(now);
            }
            ComebackBoost.Queue(new TimeSpan(now - previous), now);
        }
        foreground = true;
        CheckpointActivity(now);
    }

    internal static void SuspendActivity(DateTime utcNow)
    {
        if (RuntimeSimulationSession.IsActive || !foreground) return;
        CheckpointActivity(utcNow.Ticks);
        foreground = false;
    }

    internal static void TickActivity(DateTime utcNow)
    {
        if (RuntimeSimulationSession.IsActive || !foreground) return;
        if (utcNow.Ticks - lastCheckpoint >= TimeSpan.TicksPerMinute)
            CheckpointActivity(utcNow.Ticks);
    }

    private static void CheckpointActivity(long now)
    {
        lastCheckpoint = now;
        PlayerPrefs.SetString(LastActiveKey, now.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
}
