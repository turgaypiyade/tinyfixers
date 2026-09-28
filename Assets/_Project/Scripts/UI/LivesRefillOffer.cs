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
        int packAmount = DefaultPackAmount, int packCost = DefaultPackCost, string closeLabel = "Kapat")
    {
        bool canAfford = PlayerWallet.Coins >= packCost;

        RuntimeChoicePopup.ShowOffer(
            "Canın Bitti",
            $"Oynamaya devam etmek için can gerekli.\nŞu an {PlayerWallet.Coins} altının var.",
            new[]
            {
                new RuntimeChoicePopup.OfferButton($"{packAmount} Can Yükle", $"{packCost} altın",
                    () => BuyPack(packAmount, packCost, onLivesAdded), interactable: canAfford),
                new RuntimeChoicePopup.OfferButton("Reklam İzle", "1 can kazan",
                    () => WatchAd(host, onLivesAdded), interactable: host != null),
                new RuntimeChoicePopup.OfferButton("Satın Al", "Market'ten altın al", MarketNavigator.OpenMarket),
            },
            closeLabel,
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
        if (host == null) return;
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
