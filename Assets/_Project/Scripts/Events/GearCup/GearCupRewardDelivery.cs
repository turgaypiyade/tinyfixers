using UnityEngine;

/// <summary>Resumable per-part delivery: checkpoint and the corresponding inventory change share a prefs save.</summary>
public static class GearCupRewardDelivery
{
    public static void Deliver()
    {
        var save = GearCupState.Data;
        if (save.settledRank <= 0) return;
        if (!save.coinDelivered)
        {
            save.coinDelivered = true;
            if (save.rewardCoins > 0)
                PlayerWallet.AddCoinsWithCheckpoint(save.rewardCoins, GearCupState.SaveKey, JsonUtility.ToJson(save));
            else GearCupState.Save();
        }
        if (!save.itemDelivered)
        {
            save.itemDelivered = true;
            // Stage the receipt BEFORE Add: inventory helpers that save immediately persist both.
            PlayerPrefs.SetString(GearCupState.SaveKey, JsonUtility.ToJson(save));
            if (save.rewardAmount > 0 && save.rewardItem != 99)
                DailySlotRewardService.Grant(new DailySlotReward { type = (DailySlotRewardType)save.rewardItem, amount = save.rewardAmount });
            GearCupState.Save();
        }
    }
}
