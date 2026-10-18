using UnityEngine;

/// <summary>
/// Daily slot machine ödülünü uygulayan static servis.
/// Tek bir DailySlotReward alır → ilgili sisteme aktarır (wallet, inventory, lives).
/// </summary>
public static class DailySlotRewardService
{
    public static void Grant(DailySlotReward reward)
    {
        if (reward == null) return;
        if (reward.IsTimed)
        {
            TimedRewardService.Grant(reward.type, reward.durationMinutes);
            if (reward.type >= DailySlotRewardType.Booster_Hammer && reward.type <= DailySlotRewardType.Booster_PaintBucket)
                BoosterInventory.NotifyCountChanged(ToBoosterMode(reward.type));
            return;
        }
        int amt = Mathf.Max(1, reward.amount);

        switch (reward.type)
        {
            case DailySlotRewardType.Empty:
                break;
            case DailySlotRewardType.Coins:
                PlayerWallet.AddCoins(amt);
                break;

            case DailySlotRewardType.Lives:
                LivesManager.AddLives(amt);
                break;

            case DailySlotRewardType.Stars:
                PlayerWallet.AddStars(amt);
                break;

            case DailySlotRewardType.Joker_LineH:
                PreLevelSpecialInventory.Add(TileSpecial.LineH, amt);
                break;
            case DailySlotRewardType.Joker_Line:
                var chosen = UnityEngine.Random.Range(0, 2) == 0 ? TileSpecial.LineH : TileSpecial.LineV;
                PreLevelSpecialInventory.Add(chosen, amt);
                break;
            case DailySlotRewardType.Joker_PulseCore:
                PreLevelSpecialInventory.Add(TileSpecial.PulseCore, amt);
                break;
            case DailySlotRewardType.Joker_SystemOverride:
                PreLevelSpecialInventory.Add(TileSpecial.SystemOverride, amt);
                break;

            case DailySlotRewardType.Booster_Hammer:
                BoosterInventory.Add(BoardController.BoosterMode.Single, amt);
                break;
            case DailySlotRewardType.Booster_Row:
                BoosterInventory.Add(BoardController.BoosterMode.Row, amt);
                break;
            case DailySlotRewardType.Booster_Column:
                BoosterInventory.Add(BoardController.BoosterMode.Column, amt);
                break;
            case DailySlotRewardType.Booster_Shuffle:
                BoosterInventory.Add(BoardController.BoosterMode.Shuffle, amt);
                break;
            case DailySlotRewardType.Booster_PaintBucket:
                BoosterInventory.Add(BoardController.BoosterMode.PaintBucket, amt);
                break;
        }
    }

    private static BoardController.BoosterMode ToBoosterMode(DailySlotRewardType type) => type switch
    {
        DailySlotRewardType.Booster_Hammer      => BoardController.BoosterMode.Single,
        DailySlotRewardType.Booster_Row         => BoardController.BoosterMode.Row,
        DailySlotRewardType.Booster_Column      => BoardController.BoosterMode.Column,
        DailySlotRewardType.Booster_Shuffle     => BoardController.BoosterMode.Shuffle,
        DailySlotRewardType.Booster_PaintBucket => BoardController.BoosterMode.PaintBucket,
        _                                       => BoardController.BoosterMode.None
    };
}
