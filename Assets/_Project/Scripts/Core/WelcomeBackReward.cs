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
        BoosterInventory.StageAddition(BoardController.BoosterMode.Single, 1);
        BoosterInventory.StageAddition(BoardController.BoosterMode.Shuffle, 1);
        PreLevelSpecialInventory.Add(TileSpecial.LineH, 1);
        PreLevelSpecialInventory.Add(TileSpecial.PulseCore, 1);
        // Inventory, coins, offline delta and receipt are committed by the same Save.
        PlayerWallet.AddCoinsWithCheckpoint(Coins, ClaimedKey, receipt);
        BoosterInventory.NotifyCountChanged(BoardController.BoosterMode.Single);
        BoosterInventory.NotifyCountChanged(BoardController.BoosterMode.Shuffle);
        return true;
    }

    public static DailySlotReward[] CreatePresentation() => new[]
    {
        new DailySlotReward { type = DailySlotRewardType.Coins, amount = Coins },
        new DailySlotReward { type = DailySlotRewardType.Booster_Hammer, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Booster_Shuffle, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Joker_LineH, amount = 1 },
        new DailySlotReward { type = DailySlotRewardType.Joker_PulseCore, amount = 1 }
    };

    private static long ReadTicks(string key)
        => long.TryParse(PlayerPrefs.GetString(key, ""), out long ticks) ? ticks : 0;
}
