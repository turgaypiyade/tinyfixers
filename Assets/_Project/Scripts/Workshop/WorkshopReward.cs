using System.Collections.Generic;
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

/// <summary>
/// Tek bir ödül öğesi (örn. 100 altın, 1 joker, 1 booster).
/// Birden fazlası bir WorkshopRewardBundle içinde toplanır.
/// </summary>
[System.Serializable]
public class WorkshopReward
{
    [Tooltip("Ödülün tipi — coin, joker veya booster.")]
    public WorkshopRewardType type;

    [Tooltip("Adet — coin için altın miktarı, joker/booster için adet.")]
    [Min(1)] public int amount = 1;

    [Tooltip("Ödül uçtuğunda / popup'ta gösterilecek ödül görseli (coin, joker, booster sprite). " +
             "Boş bırakılırsa joker/booster ikonu TileIconLibrary.Shared'dan otomatik çözülür.")]
    public Sprite rewardIcon;

    /// <summary>
    /// Gösterilecek ikon: elle atanmış <see cref="rewardIcon"/> varsa o, yoksa joker/booster için
    /// TileIconLibrary.Shared'dan çözülür (booster imajları tek kaynaktan).
    /// </summary>
    public Sprite ResolveIcon()
    {
        if (rewardIcon != null) return rewardIcon;
        var lib = TileIconLibrary.Shared;
        return lib != null ? lib.GetRewardIcon(type) : null;
    }

    [Tooltip("Ödül adının lokalizasyon anahtarı. Örn: \"reward_coins\" → \"{0} Altın\". " +
             "Boş ise sadece adet + ikon gösterilir.")]
    public string nameLocalizationKey;
}

/// <summary>
/// Bir sandıktan çıkacak ödül paketi. Birden fazla WorkshopReward içerebilir.
/// Örn: 100 altın + 1 joker + 1 hammer.
/// </summary>
[System.Serializable]
public class WorkshopRewardBundle
{
    [Tooltip("Sandık açılınca verilecek tüm ödüller. Sırayla teslim edilir.")]
    public List<WorkshopReward> items = new List<WorkshopReward>();

    [Tooltip("Progress bar sonunda gösterilecek kapalı sandık görseli.")]
    public Sprite chestIcon;

    [Tooltip("Sandık açılınca progress bar'da gösterilecek açılmış sandık görseli (opsiyonel).")]
    public Sprite chestOpenedSprite;

    [Tooltip("Sandığın adı / paket adı için lokalizasyon anahtarı (opsiyonel).")]
    public string nameLocalizationKey;
}

/// <summary>
/// Workshop ödülünü uygulayan (delivery) servis. Tek static giriş noktası.
/// </summary>
public static class WorkshopRewardService
{
    /// Bundle içindeki tüm ödülleri sırayla verir.
    public static void Grant(WorkshopRewardBundle bundle)
    {
        if (bundle == null || bundle.items == null) return;
        for (int i = 0; i < bundle.items.Count; i++)
            GrantSingle(bundle.items[i]);
    }

    /// Tek bir ödül öğesini uygular.
    public static void GrantSingle(WorkshopReward reward)
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
