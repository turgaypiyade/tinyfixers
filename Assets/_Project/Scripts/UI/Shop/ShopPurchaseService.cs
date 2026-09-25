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
        OnPurchased?.Invoke(offer);
        return true;
    }
}
