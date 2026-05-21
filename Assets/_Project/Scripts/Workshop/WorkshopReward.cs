using UnityEngine;

public enum WorkshopRewardType
{
    Coins                  = 0,
    Joker_LineH            = 10,
    Joker_PulseCore        = 11,
    Joker_SystemOverride   = 12,
    Booster_Hammer         = 20,
    Booster_Row            = 21,
    Booster_Column         = 22,
    Booster_Shuffle        = 23,
}

[System.Serializable]
public class WorkshopReward
{
    [Tooltip("Ödülün tipi — coin, joker veya booster.")]
    public WorkshopRewardType type;

    [Tooltip("Adet — coin için altın miktarı, joker/booster için adet.")]
    [Min(1)] public int amount = 1;

    [Tooltip("Progress bar sonunda gösterilecek sandık/kutu ikonu.")]
    public Sprite chestIcon;

    [Tooltip("Ödül uçtuğunda / popup'ta gösterilecek ödül görseli (coin sprite, hammer sprite vb.).")]
    public Sprite rewardIcon;

    [Tooltip("Ödül adının lokalizasyon anahtarı. Örn: \"reward_coins\" → \"{0} Altın\". " +
             "Boş ise sadece adet + ikon gösterilir.")]
    public string nameLocalizationKey;
}

/// <summary>
/// Workshop ödülünü uygulayan (delivery) servis. Tek static giriş noktası.
/// </summary>
public static class WorkshopRewardService
{
    public static void Grant(WorkshopReward reward)
    {
        if (reward == null) return;
        int amt = Mathf.Max(1, reward.amount);

        switch (reward.type)
        {
            case WorkshopRewardType.Coins:
                PlayerWallet.AddCoins(amt);
                break;

            case WorkshopRewardType.Joker_LineH:
                PreLevelSpecialInventory.Add(TileSpecial.LineH, amt);
                break;
            case WorkshopRewardType.Joker_PulseCore:
                PreLevelSpecialInventory.Add(TileSpecial.PulseCore, amt);
                break;
            case WorkshopRewardType.Joker_SystemOverride:
                PreLevelSpecialInventory.Add(TileSpecial.SystemOverride, amt);
                break;

            case WorkshopRewardType.Booster_Hammer:
                BoosterInventory.Add(BoardController.BoosterMode.Single, amt);
                break;
            case WorkshopRewardType.Booster_Row:
                BoosterInventory.Add(BoardController.BoosterMode.Row, amt);
                break;
            case WorkshopRewardType.Booster_Column:
                BoosterInventory.Add(BoardController.BoosterMode.Column, amt);
                break;
            case WorkshopRewardType.Booster_Shuffle:
                BoosterInventory.Add(BoardController.BoosterMode.Shuffle, amt);
                break;
        }

        Debug.Log($"[WorkshopReward] Granted {amt}x {reward.type}");
    }
}
