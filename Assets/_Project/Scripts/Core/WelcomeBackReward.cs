using System.Globalization;
using UnityEngine;

/// <summary>One durable gift per detected 72-hour absence, independent of mercy eligibility.</summary>
public static class WelcomeBackReward
{
    // Device-local, like the absence clock. Cloud restores must not erase a newly queued gift
    // or roll its receipt back. The resulting balances use the ordinary cloud manifest.
    private const string PendingKey = "welcome_back_reward_pending_ticks";
    private const string ClaimedKey = "welcome_back_reward_claimed_ticks";
    public const int Coins = 500;
    public static bool HasPending => ReadTicks(PendingKey) > ReadTicks(ClaimedKey);

    internal static void Queue(long returnTicks)
    {
        if (RuntimeSimulationSession.IsActive || HasPending || returnTicks <= ReadTicks(ClaimedKey)) return;
        PlayerPrefs.SetString(PendingKey, returnTicks.ToString(CultureInfo.InvariantCulture));
        // ResumeActivity checkpoints the clock and this pending token together.
    }

    public static bool GrantPending()
    {
        if (RuntimeSimulationSession.IsActive || !HasPending) return false;
        // Complete initialization before staging the bundle: EnsureInit may save preferences.
        CurrencyLedger.EnsureInit();
        string receipt = PlayerPrefs.GetString(PendingKey, "");
        // Coins only: joker use for returning players comes as time from ComebackBoost, never as counts.
        // Coins, offline delta and receipt are committed by the same Save.
        PlayerWallet.AddCoinsWithCheckpoint(Coins, ClaimedKey, receipt);
        return true;
    }

    public static DailySlotReward[] CreatePresentation() => new[]
    {
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = Coins }
    };

    private static long ReadTicks(string key)
        => long.TryParse(PlayerPrefs.GetString(key, ""), out long ticks) ? ticks : 0;
}
