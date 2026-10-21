using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
/// Bağlantı/ürün listesi başarısız olur ya da bağlantı koparsa artan aralıkla (5 sn → 60 sn) yeniden denenir.
/// Mağaza hazır değilken satın almaya basılırsa oyuncuya mesaj gösterilir (sessiz kalmaz). [IAP] uyarıları
/// her sürümde loglanır (cihaz logunda sebep görünsün; seyrek olaylar).
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
    private static bool connected;
    private static bool connecting;
    private static bool retryScheduled;
    private static int retryAttempt;

    public static bool IsAvailable => store != null && productsReady;
    public static bool IsPurchasing => purchasingOffer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || store != null) return;
        CreateStore();
        Connect();
    }

    // Olay abonelikleri tek sefer; Connect yeniden denemelerde tekrar çağrılır.
    private static void CreateStore()
    {
        store = UnityIAPServices.StoreController();
        // IAP requires this subscription before Connect; it also owns catalog loading.
        store.OnStoreConnected += HandleStoreConnected;
        store.OnProductsFetched += HandleProductsFetched;
        store.OnProductsFetchFailed += f =>
        {
            Debug.LogWarning($"[IAP] Ürünler alınamadı: {f.FailureReason}");
            ScheduleRetry();
        };
        store.OnPurchasePending += HandlePurchasePending;
        store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
        store.OnPurchaseFailed += HandlePurchaseFailed;
        store.OnPurchaseDeferred += _ => { purchasingOffer = null; };
        store.OnStoreDisconnected += d =>
        {
            connected = false;
            productsReady = false;
            Debug.LogWarning($"[IAP] Mağaza bağlantısı koptu: {d.Message}");
            ScheduleRetry();
        };
        // Geçmiş alımlar: onaylanmamış olanlar OnPurchasePending ile gelir; burada yalnız kayıt.
        store.OnPurchasesFetched += _ => { };
        store.OnPurchasesFetchFailed += f => Debug.LogWarning($"[IAP] Geçmiş alımlar alınamadı: {f.message}");
    }

    private static async void Connect()
    {
        if (connecting) return;
        connecting = true;
        try
        {
            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP] Mağazaya bağlanılamadı: {e.Message}");
            ScheduleRetry();
        }
        finally
        {
            connecting = false;
        }
    }

    // Bağlı değilse yeniden bağlan, bağlıysa ürünleri yeniden iste. Aynı anda tek bekleyen deneme olur.
    private static async void ScheduleRetry()
    {
        if (retryScheduled || store == null) return;
        retryScheduled = true;
        int delaySeconds = Mathf.Min(60, 5 << Mathf.Min(retryAttempt, 4));
        retryAttempt++;
        await Task.Delay(delaySeconds * 1000);
        retryScheduled = false;
        if (!Application.isPlaying || IsAvailable) return;
        if (connected) HandleStoreConnected();
        else Connect();
    }

    /// Mağaza hazır değilken satın alma istendi: oyuncuya söyle, beklemede değilse hemen yeniden dene.
    public static void ShowUnavailable()
    {
        RuntimeChoicePopup.Show(GameLocalization.Get("store_unavailable_title"), GameLocalization.Get("store_unavailable_body"));
        if (store != null && !retryScheduled)
        {
            retryAttempt = 0;
            if (connected) HandleStoreConnected();
            else Connect();
        }
    }

    private static void HandleStoreConnected()
    {
        connected = true;
        productsReady = false;
        var catalog = ShopCatalog.Shared;
        var defs = new List<ProductDefinition>();
        offersByProductId.Clear();
        if (catalog != null)
            foreach (var offer in catalog.RealMoneyOffers())
            {
                string pid = offer.StoreProductId;
                if (offersByProductId.ContainsKey(pid)) continue;
                offersByProductId[pid] = offer;
                // Tekrar alınabilir paketler mağazada consumable olarak tutulur.
                defs.Add(new ProductDefinition(pid, ProductType.Consumable));
            }
        if (defs.Count > 0) store.FetchProducts(defs);
        else Debug.LogWarning("[IAP] Katalogda gerçek para teklifi yok.");
    }

    private static void HandleProductsFetched(List<Product> products)
    {
        productsReady = true;
        retryAttempt = 0;
        int available = 0;
        foreach (var p in products) if (p != null && p.availableToPurchase) available++;
        Debug.Log($"[IAP] Mağaza hazır: {available}/{offersByProductId.Count} ürün satın alınabilir.");
        OnPricesUpdated?.Invoke();
        // Önceki oturumda onaylanmamış alımlar (ör. uygulama kapandı) OnPurchasePending ile yeniden gelir.
        store.FetchPurchases();
    }

    public static bool TryGetPrice(ShopOffer offer, out string label)
    {
        label = null;
#if UNITY_EDITOR
        // Editörde Unity IAP sahte mağazası her ürüne "$0.01" döndürür → katalogdaki yedek etiket gösterilsin.
        // Cihazda fiyat gerçek mağazadan (oyuncunun ülkesi/para birimi) gelir.
        return false;
#else
        if (store == null || offer == null) return false;
        var product = store.GetProductById(offer.StoreProductId);
        label = product?.metadata?.localizedPriceString;
        return !string.IsNullOrEmpty(label);
#endif
    }

    /// Satın almayı başlatır. true → mağaza penceresi açılıyor; sonuç olaylarla gelir (OnPurchased / OnPurchaseFailed).
    public static bool BeginPurchase(ShopOffer offer)
    {
        if (!IsAvailable || offer == null || IsPurchasing) return false;
        var product = store.GetProductById(offer.StoreProductId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning($"[IAP] Ürün mağazada yok: {offer.StoreProductId}");
            ShowUnavailable();
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
        if (!cancelled)
        {
            Debug.LogWarning($"[IAP] Satın alma başarısız: {order.FailureReason} {order.Details}");
            RuntimeChoicePopup.Show(GameLocalization.Get("store_unavailable_title"), GameLocalization.Get("store_purchase_failed_body"));
        }
        OnPurchaseFailed?.Invoke(offer, cancelled);
    }
#else
    public static bool IsAvailable => false;
    public static bool IsPurchasing => false;
    public static bool TryGetPrice(ShopOffer offer, out string label) { label = null; return false; }
    public static bool BeginPurchase(ShopOffer offer) => false;
    public static void ShowUnavailable() =>
        RuntimeChoicePopup.Show(GameLocalization.Get("store_unavailable_title"), GameLocalization.Get("store_unavailable_body"));
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
