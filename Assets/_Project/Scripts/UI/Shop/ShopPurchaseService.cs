using System;
using UnityEngine;

/// <summary>
/// Mağaza satın almasının TEK giriş noktası (ana menü market'i + oyun içi market aynı yolu kullanır).
/// Coin/Yıldız bedeli düşülür, teklif kaydedilir (günlük/tek seferlik), içerik ShopRewardGranter ile
/// verilir. Cihazdaki gerçek para teklifleri yalnız mağaza onayıyla teslim edilir.
/// </summary>
public static class ShopPurchaseService
{
    public static bool SimulateRealMoneyPurchases =>
#if UNITY_EDITOR && !TF_IAP
        true;
#else
        false;
#endif

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
        LevelAssist.RecordPurchaseCompleted();
        PublishReceipt(offer, true);
        return true;
    }

    /// Mağaza bir gerçek para alımını bildirdi (StoreIap, onaydan ÖNCE): içerik + kayıt + teşekkür + makbuz.
    /// Tekrar teslimi StoreIap işlem kimliğiyle engeller; burası her işlem için bir kez çağrılır.
    internal static bool GrantStorePurchase(ShopOffer offer, string transactionId)
    {
        if (offer == null || offer.priceType != ShopOffer.PriceType.RealMoney)
            throw new ArgumentException("A store purchase must match a real-money offer.", nameof(offer));
        ShopState.RecordPurchase(offer);
        ShopRewardGranter.Grant(offer);
        bool thanks = LivesManager.GrantPurchaseThanks(transactionId);
        LevelAssist.RecordPurchaseCompleted();
        return thanks;
    }

    // StoreIap calls this AFTER saving the fulfilment receipt. UI failures cannot replay delivery.
    internal static void PublishStoreReceipt(ShopOffer offer, bool thanks) => PublishReceipt(offer, thanks);

    /// Arayan için: false dönen TryPurchase bir mağaza penceresi başlattıysa true ("yakında" ipucu gösterilmesin).
    public static bool IsStorePurchasePending => StoreIap.IsPurchasing;

    private static void PublishReceipt(ShopOffer offer, bool thanks)
    {
        if (OnReceipt != null)
            foreach (Action<ShopOffer, bool> listener in OnReceipt.GetInvocationList())
                try { listener(offer, thanks); } catch (Exception error) { Debug.LogException(error); }
        if (OnPurchased != null)
            foreach (Action<ShopOffer> listener in OnPurchased.GetInvocationList())
                try { listener(offer); } catch (Exception error) { Debug.LogException(error); }
    }

    /// <summary>Teklifi satın almayı dener. İçerik verildiyse true.</summary>
    public static bool TryPurchase(ShopOffer offer)
    {
        if (offer == null || !ShopState.IsAvailable(offer)) return false;

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
                // Mağaza bağlıysa (TF_IAP): pencere açılır, içerik mağaza onayında FulfillStorePurchase ile verilir.
                // Bu çağrı false döner; arayanlar sonucu OnPurchased / StoreIap.OnPurchaseFailed ile alır.
                if (StoreIap.IsAvailable)
                {
                    StoreIap.BeginPurchase(offer);
                    return false;
                }
                if (!SimulateRealMoneyPurchases)
                {
                    // Yayın sürümünde mağaza hazır değil: sessiz kalma, oyuncuya söyle (yeniden bağlanmayı da tetikler).
                    StoreIap.ShowUnavailable();
                    return false;
                }
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
