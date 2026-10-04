using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// "Canın Bitti" teklifi — can 0 iken oynamak isteyen her yerin TEK yolu (ana menü can alanı,
/// level öncesi popup'ın Oyna / Tekrar Dene butonu; ana menüde de oyun sahnesinde de çalışır).
/// Seçenekler: altınla can paketi · reklam izle (+1 can) · market. Can gelince onLivesAdded çağrılır.
/// </summary>
public static class LivesRefillOffer
{
    public const int DefaultPackAmount = 5;
    public const int DefaultPackCost = 900;
    private const float SimulatedAdSeconds = 1f;

    /// <param name="host">Reklam beklemesi için coroutine sahibi (popup kapansa da yaşayan bir obje).</param>
    public static void Show(MonoBehaviour host, Action onLivesAdded,
        int packAmount = DefaultPackAmount, int packCost = DefaultPackCost, string closeLabel = null)
    {
        bool canAfford = PlayerWallet.Coins >= packCost;
        bool adUsed = LevelAdRight.IsUsed;

        RuntimeChoicePopup.ShowOffer(
            GameLocalization.Get("lives_offer_title"),
            GameLocalization.GetFormat("lives_offer_body", PlayerWallet.Coins),
            new[]
            {
                new RuntimeChoicePopup.OfferButton(GameLocalization.GetFormat("lives_offer_buy_pack", packAmount), GameLocalization.GetFormat("common_gold_amount", packCost),
                    () => BuyPack(packAmount, packCost, onLivesAdded), interactable: canAfford),
                // Reklam hakkı level başına TEK (fail devam reklamıyla ortak): kaybedilen level'ı
                // reklamla sonsuz tekrar oynamak olmasın.
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("common_watch_ad"),
                    GameLocalization.Get(adUsed ? "level_end_ad_used" : "lives_offer_ad_sub"),
                    () => WatchAd(host, onLivesAdded), interactable: host != null && !adUsed),
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("common_buy"), GameLocalization.Get("common_buy_gold_sub"), MarketNavigator.OpenMarket),
            },
            closeLabel ?? GameLocalization.Get("common_close"),
            null,
            defaultFrame: true);
    }

    private static void BuyPack(int packAmount, int packCost, Action onLivesAdded)
    {
        if (PlayerWallet.SpendCoins(packCost))
        {
            LivesManager.AddLives(packAmount);
            onLivesAdded?.Invoke();
            return;
        }
        // Popup açıkken altın değiştiyse → markete yönlendir.
        MarketNavigator.OpenMarket();
    }

    private static void WatchAd(MonoBehaviour host, Action onLivesAdded)
    {
        if (host == null || !LevelAdRight.TryConsume()) return;
        host.StartCoroutine(CoWatchAd(onLivesAdded));
    }

    private static IEnumerator CoWatchAd(Action onLivesAdded)
    {
        // TODO: Gerçek rewarded-ad SDK bağlanınca burada gösterilecek (LevelEnd reklamıyla aynı kanca).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[LivesRefillOffer] Reklam simülasyonu → +1 can.");
        yield return new WaitForSecondsRealtime(SimulatedAdSeconds);
        LivesManager.AddLives(1);
        onLivesAdded?.Invoke();
#else
        Debug.LogWarning("[LivesRefillOffer] Rewarded ad SDK bağlı değil; can verilemedi.");
        yield break;
#endif
    }
}
