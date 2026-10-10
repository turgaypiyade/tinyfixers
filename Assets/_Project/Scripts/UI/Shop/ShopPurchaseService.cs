using System;
using UnityEngine;

/// <summary>
/// Mağaza satın almasının TEK giriş noktası (ana menü market'i + oyun içi market aynı yolu kullanır).
/// Coin/Yıldız bedeli düşülür, teklif kaydedilir (günlük/tek seferlik), içerik ShopRewardGranter ile
/// verilir. Gerçek-para (TL) teklifleri IAP bağlanana dek "satın alınmış gibi" işlenir
/// (SimulateRealMoneyPurchases: yalnız editör/dev build) — IAP gelince gerçek akış bağlanacak.
/// </summary>
public static class ShopPurchaseService
{
    // TODO(IAP): Unity Purchasing bağlanınca RealMoney yalnız mağaza onayıyla Fulfil edilsin.
    // Şimdilik yalnız editör/dev build'de simüle edilir — yayın build'inde TL paketleri bedava verilmez.
    public static bool SimulateRealMoneyPurchases => Debug.isDebugBuild;

    /// <summary>Başarılı her alımda (içerik verildikten sonra) tetiklenir.</summary>
    public static event Action<ShopOffer> OnPurchased;

    /// Receipt presentation: whether a separate +1 life thank-you was granted.
    public static event Action<ShopOffer, bool> OnReceipt;

    /// Integration hook for the future IAP adapter, AFTER store/server verification and
    /// successful, idempotent fulfilment of the purchased contents. This method does NOT
    /// verify receipts or deliver the main bundle. Pass a store-qualified transaction ID.
    /// Replayed callbacks do not repeat the gift or its celebration on this saved profile.
    public static bool NotifyVerifiedPurchaseFulfilled(ShopOffer offer, string transactionId)
    {
        if (offer == null || offer.priceType != ShopOffer.PriceType.RealMoney
            || !LivesManager.GrantPurchaseThanks(transactionId)) return false;
        PublishReceipt(offer, true);
        return true;
    }

    private static void PublishReceipt(ShopOffer offer, bool thanks)
    {
        OnReceipt?.Invoke(offer, thanks);
        OnPurchased?.Invoke(offer);
    }

    /// <summary>Teklifi satın almayı dener. İçerik verildiyse true.</summary>
    public static bool TryPurchase(ShopOffer offer)
    {
        if (offer == null) return false;

        switch (offer.priceType)
        {
            case ShopOffer.PriceType.Coins:
                if (!PlayerWallet.SpendCoins(offer.priceAmount)) return false;
                break;

            case ShopOffer.PriceType.Stars:
                if (!PlayerWallet.SpendStars(offer.priceAmount)) return false;
                break;

            case ShopOffer.PriceType.Free:
                break;

            case ShopOffer.PriceType.RealMoney:
                if (!SimulateRealMoneyPurchases) return false;
                Debug.Log($"[Shop] IAP simülasyonu: '{offer.displayName}' satın alındı sayıldı.");
                break;

            default:
                return false;
        }

        ShopState.RecordPurchase(offer);
        ShopRewardGranter.Grant(offer);
        // Development preview only; production RealMoney clicks still return false above.
        bool thanks = offer.priceType == ShopOffer.PriceType.RealMoney
            && LivesManager.GrantPurchaseThanks("dev-simulation:" + Guid.NewGuid().ToString("N"));
        PublishReceipt(offer, thanks);
        return true;
    }
}
