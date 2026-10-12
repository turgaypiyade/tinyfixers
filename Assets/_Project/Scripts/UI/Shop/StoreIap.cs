using System;
using System.Collections.Generic;
using UnityEngine;
#if TF_IAP
using UnityEngine.Purchasing;
#endif

/// <summary>
/// Gerçek para satın alma (App Store / Google Play) — Unity IAP 5 (com.unity.purchasing).
///
/// Etkinleştirme: paket manifest'te; Player Settings ▸ Scripting Define Symbols (iOS + Android) içine TF_IAP
/// eklenince derlenir. TF_IAP yokken bu sınıf boş taslaktır (IsAvailable=false) ve ShopPurchaseService eski
/// davranışına (yalnız editör/dev build'de simülasyon) döner.
///
/// Akış: açılışta mağazaya bağlan → ShopCatalog'daki RealMoney tekliflerini ürün olarak çek (fiyatlar oyuncunun
/// ülkesi/para birimiyle gelir) → satın al → OnPurchasePending: içerik verilir (ShopPurchaseService.FulfillStorePurchase)
/// SONRA ConfirmPurchase. Aynı işlem (transactionId) ikinci kez gelirse içerik tekrar verilmez, yalnız onaylanır
/// (uygulama onaydan önce kapanırsa mağaza siparişi yeniden gönderir).
/// NOT: Sunucu tarafı makbuz doğrulaması henüz yok (canlıdan önce eklenecek — ProductionPlan).
/// </summary>
public static class StoreIap
{
    /// Fiyatlar mağazadan geldi (kartlar yenilenebilir).
    public static event Action OnPricesUpdated;
    /// Satın alma başarısız / iptal (userCancelled = oyuncu vazgeçti → sessiz geç).
    public static event Action<ShopOffer, bool> OnPurchaseFailed;

    private const string ProcessedKey = "iap_processed_transactions";   // CloudSaveManifest'te
    private const int MaxProcessedKept = 200;

#if TF_IAP
    private static StoreController store;
    private static bool productsReady;
    private static ShopOffer purchasingOffer;
    private static readonly Dictionary<string, ShopOffer> offersByProductId = new();

    public static bool IsAvailable => store != null && productsReady;
    public static bool IsPurchasing => purchasingOffer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || store != null) return;
        Connect();
    }

    private static async void Connect()
    {
        try
        {
            store = UnityIAPServices.StoreController();
            store.OnProductsFetched += HandleProductsFetched;
            store.OnProductsFetchFailed += f => Debug.LogWarning($"[IAP] Ürünler alınamadı: {f.FailureReason}");
            store.OnPurchasePending += HandlePurchasePending;
            store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            store.OnPurchaseFailed += HandlePurchaseFailed;
            store.OnPurchaseDeferred += _ => { purchasingOffer = null; Debug.Log("[IAP] Satın alma ertelendi (onay bekliyor)."); };
            store.OnStoreDisconnected += d => Debug.LogWarning($"[IAP] Mağaza bağlantısı koptu: {d.Message}");

            await store.Connect();

            var catalog = ShopCatalog.Shared;
            var defs = new List<ProductDefinition>();
            offersByProductId.Clear();
            if (catalog != null)
                foreach (var offer in catalog.RealMoneyOffers())
                {
                    string pid = offer.StoreProductId;
                    if (offersByProductId.ContainsKey(pid)) continue;
                    offersByProductId[pid] = offer;
                    // Tekrar alınabilir paketler mağazada "consumable"; tek seferlik/günlük sınırı oyun tutar (ShopState).
                    defs.Add(new ProductDefinition(pid, ProductType.Consumable));
                }
            if (defs.Count > 0) store.FetchProducts(defs);
            else Debug.LogWarning("[IAP] Katalogda gerçek para teklifi yok.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP] Mağazaya bağlanılamadı: {e.Message}");
        }
    }

    private static void HandleProductsFetched(List<Product> products)
    {
        productsReady = true;
        Debug.Log($"[IAP] {products?.Count ?? 0} ürün hazır.");
        OnPricesUpdated?.Invoke();
        // Önceki oturumda onaylanmamış alımlar (ör. uygulama kapandı) OnPurchasePending ile yeniden gelir.
        store.FetchPurchases();
    }

    public static bool TryGetPrice(ShopOffer offer, out string label)
    {
        label = null;
        if (store == null || offer == null) return false;
        var product = store.GetProductById(offer.StoreProductId);
        label = product?.metadata?.localizedPriceString;
        return !string.IsNullOrEmpty(label);
    }

    /// Satın almayı başlatır. true → mağaza penceresi açılıyor; sonuç olaylarla gelir (OnPurchased / OnPurchaseFailed).
    public static bool BeginPurchase(ShopOffer offer)
    {
        if (!IsAvailable || offer == null || IsPurchasing) return false;
        var product = store.GetProductById(offer.StoreProductId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning($"[IAP] Ürün mağazada yok: {offer.StoreProductId}");
            return false;
        }
        purchasingOffer = offer;
        store.PurchaseProduct(product);
        return true;
    }

    private static void HandlePurchasePending(PendingOrder order)
    {
        string txId = order.Info?.TransactionID;
        if (string.IsNullOrEmpty(txId)) txId = "notx:" + Guid.NewGuid().ToString("N");
        bool alreadyProcessed = IsProcessed(txId);
        foreach (var item in order.CartOrdered.Items())
        {
            string pid = item.Product?.definition?.id;
            if (pid == null || !offersByProductId.TryGetValue(pid, out var offer))
            {
                Debug.LogWarning($"[IAP] Bilinmeyen ürün onaylanıyor: {pid}");
                continue;
            }
            if (alreadyProcessed) continue;
            // İçerik ÖNCE verilir, sonra onay: onaydan önce çökerse sipariş tekrar gelir, işlem kimliği tekrarı önler.
            ShopPurchaseService.FulfillStorePurchase(offer, txId);
        }
        if (!alreadyProcessed) MarkProcessed(txId);
        purchasingOffer = null;
        store.ConfirmPurchase(order);
    }

    private static void HandlePurchaseConfirmed(Order order)
    {
        if (order is FailedOrder failed)
            Debug.LogWarning($"[IAP] Onay başarısız: {failed.FailureReason} {failed.Details}");
    }

    private static void HandlePurchaseFailed(FailedOrder order)
    {
        var offer = purchasingOffer;
        purchasingOffer = null;
        bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
        if (!cancelled) Debug.LogWarning($"[IAP] Satın alma başarısız: {order.FailureReason} {order.Details}");
        OnPurchaseFailed?.Invoke(offer, cancelled);
    }
#else
    public static bool IsAvailable => false;
    public static bool IsPurchasing => false;
    public static bool TryGetPrice(ShopOffer offer, out string label) { label = null; return false; }
    public static bool BeginPurchase(ShopOffer offer) => false;
    // Taslakta olaylar hiç tetiklenmez; derleyici uyarısı olmasın diye boş erişim.
    private static void Unused() { OnPricesUpdated?.Invoke(); OnPurchaseFailed?.Invoke(null, false); }
#endif

    // ── İşlenmiş işlem kimlikleri (tekrar teslimi önler; bulut kaydında) ──

    private static bool IsProcessed(string txId)
    {
        string all = PlayerPrefs.GetString(ProcessedKey, "");
        return all.Length > 0 && Array.IndexOf(all.Split('|'), txId) >= 0;
    }

    private static void MarkProcessed(string txId)
    {
        var list = new List<string>(PlayerPrefs.GetString(ProcessedKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        list.Add(txId);
        if (list.Count > MaxProcessedKept) list.RemoveRange(0, list.Count - MaxProcessedKept);
        PlayerPrefs.SetString(ProcessedKey, string.Join("|", list));
        PlayerPrefs.Save();
    }
}
