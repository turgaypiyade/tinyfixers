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

    /// <param name="host">Reklam beklemesi için coroutine sahibi (popup kapansa da yaşayan bir obje).</param>
    public static void Show(MonoBehaviour host, Action onLivesAdded,
        int packAmount = DefaultPackAmount, int packCost = DefaultPackCost, string closeLabel = null)
    {
        bool canAfford = PlayerWallet.Coins >= packCost;
        // Can reklamı seviye hakkından AYRI: günde RewardedAdCaps.DailyLimit(Life) (plan 2026-11-08).
        bool adAvailable = RewardedAdCaps.CanOffer(AdPlacement.Life);
        if (adAvailable) RewardedAdCaps.NoteOffer(AdPlacement.Life);

        RuntimeChoicePopup.ShowOffer(
            GameLocalization.Get("lives_offer_title"),
            GameLocalization.GetFormat("lives_offer_body", PlayerWallet.Coins),
            new[]
            {
                new RuntimeChoicePopup.OfferButton(GameLocalization.GetFormat("lives_offer_buy_pack", packAmount), GameLocalization.GetFormat("common_gold_amount", packCost),
                    () => BuyPack(packAmount, packCost, onLivesAdded), interactable: canAfford),
                // Günlük sınırlı; kalan hak alt yazıda.
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("common_watch_ad"),
                    adAvailable
                        ? GameLocalization.GetFormat("lives_offer_ad_left", RewardedAdCaps.RemainingToday(AdPlacement.Life))
                        : GameLocalization.Get("ad_daily_limit_reached"),
                    () => WatchAd(host, onLivesAdded), interactable: host != null && adAvailable),
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("common_buy"), GameLocalization.Get("common_buy_gold_sub"), MarketNavigator.OpenMarket),
            },
            closeLabel ?? GameLocalization.Get("common_close"),
            null,
            defaultFrame: true);
        RuntimeChoicePopup.SetTitleArc(15f);   // kullanıcı ayarı (2026-10-27): "Canın Bitti" başlığı 15°
        RuntimeChoicePopup.NudgeTitle(22f);    // bantta aşağıda kalıyordu → ortaya
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
        if (host == null || AdService.IsShowing) return;
        // Hak yalnız reklam gerçekten izlenince harcanır.
        RewardedAdCaps.Show(AdPlacement.Life, () =>
        {
            LivesManager.AddLives(1);
            onLivesAdded?.Invoke();
        });
    }
}
