using UnityEngine;

/// <summary>
/// Satın alınan teklifin içeriğini gerçek oyun sistemlerine işler.
/// PlayerWallet (coin/yıldız), BoosterInventory (booster), LivesManager (can),
/// TimedRewardService (süreli sınırsız can). Ödüller kutuların grant listelerinden okunur —
/// görselden (icon/label) bağımsız. Tek giriş noktası: Grant(offer).
/// </summary>
public static class ShopRewardGranter
{
    /// <summary>Teklifin tüm kutularındaki grant'ları oyuncuya verir.</summary>
    public static void Grant(ShopOffer offer)
    {
        if (offer?.groups == null) return;

        foreach (var group in offer.groups)
        {
            if (group?.grants == null) continue;
            foreach (var reward in group.grants)
                GrantReward(reward);
        }
    }

    private static void GrantReward(ShopReward reward)
    {
        if (reward == null) return;
        int amount = Mathf.Max(1, reward.amount);

        switch (reward.kind)
        {
            case ShopReward.Kind.Coins:
                PlayerWallet.AddCoins(amount);
                break;

            case ShopReward.Kind.Stars:
                PlayerWallet.AddStars(amount);
                break;

            case ShopReward.Kind.Life:
                LivesManager.AddLives(amount);
                break;

            case ShopReward.Kind.Booster:
                BoosterInventory.Add(reward.booster, amount);
                break;

            case ShopReward.Kind.InfiniteLifeTimed:
                TimedRewardService.Grant(DailySlotRewardType.Lives,
                                         Mathf.Max(1, reward.durationHours) * 60);
                break;
        }
    }
}
