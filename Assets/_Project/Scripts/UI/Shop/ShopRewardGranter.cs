using UnityEngine;

/// <summary>
/// Satın alınan teklifin içeriğini gerçek oyun sistemlerine işler.
/// PlayerWallet (coin/yıldız), BoosterInventory (booster), LivesManager (can),
/// TimedRewardService (süreli sınırsız can). Tek giriş noktası: Grant(offer).
/// </summary>
public static class ShopRewardGranter
{
    /// <summary>Teklifin tüm ödüllerini ve varsa hero coin miktarını grant eder.</summary>
    public static void Grant(ShopOffer offer)
    {
        if (offer == null) return;

        // Hero miktarı her zaman coin kabul edilir (coin paketleri için).
        if (offer.heroAmount > 0)
            PlayerWallet.AddCoins(offer.heroAmount);

        if (offer.contents == null) return;
        foreach (var reward in offer.contents)
            GrantReward(reward);
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
