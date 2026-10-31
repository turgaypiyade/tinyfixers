using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if TF_IAP
using UnityEngine.Purchasing;
#endif

/// <summary>
/// Unity IAP 5: native store prices, purchase callbacks, durable duplicate receipts and reconnects.
/// Device purchases never fall back to simulation. Receipt verification on a trusted server remains
/// a separate integration; a PendingOrder callback alone is not server-side verification.
/// </summary>
public static class StoreIap
{
    public static event Action OnPricesUpdated;
    public static event Action<ShopOffer, bool> OnPurchaseFailed;
    private const string ProcessedKey = "iap_processed_transactions";
    // Not cloud-restored: a stale cloud snapshot cannot erase this device's completed deliveries.
    private const string DeviceProcessedKey = "iap_processed_transactions_device_v1";

#if TF_IAP
    private static StoreController store;
    private static Runner runner;
    private static bool productsReady, connected, connecting, fetchingProducts, stopping;
    private static int retryAttempt;
    private static Coroutine retry, delivery;
    private static ShopOffer purchasingOffer;
    private static readonly Dictionary<string, ShopOffer> offersByProductId = new();
    private static readonly Dictionary<string, PendingOrder> pending = new();
    private static readonly WaitForSecondsRealtime DeliveryWait = new(1f);

    private sealed class Runner : MonoBehaviour
    {
        private void OnDestroy() { if (runner == this) Shutdown(); }
        private void OnApplicationQuit() => Shutdown();
        private void OnApplicationPause(bool paused)
        {
            if (paused || stopping) return;
            if (!connected) Connect();
            else if (!productsReady) FetchProducts();
            else FetchPurchases();
        }
    }

    public static bool IsAvailable => !stopping && connected && productsReady;
    public static bool IsPurchasing => purchasingOffer != null || pending.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Shutdown();
        stopping = false;
        retryAttempt = 0;
        OnPricesUpdated = null;
        OnPurchaseFailed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive || store != null) return;
        var go = new GameObject("StoreIap");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        store = UnityIAPServices.StoreController();
        // Handle fetched pending orders explicitly, once, through the same delivery path.
        store.ProcessPendingOrdersOnPurchasesFetched(false);
        store.OnStoreConnected += HandleStoreConnected;
        store.OnStoreDisconnected += HandleStoreDisconnected;
        store.OnProductsFetched += HandleProductsFetched;
        store.OnProductsFetchFailed += HandleProductsFetchFailed;
        store.OnPurchasePending += HandlePurchasePending;
        store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
        store.OnPurchaseFailed += HandlePurchaseFailed;
        store.OnPurchaseDeferred += HandlePurchaseDeferred;
        store.OnPurchasesFetched += HandlePurchasesFetched;
        store.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
        Connect();
    }

    private static void Shutdown()
    {
        stopping = true;
        if (runner != null) runner.StopAllCoroutines();
        retry = delivery = null;
        if (store != null)
        {
            store.OnStoreConnected -= HandleStoreConnected;
            store.OnStoreDisconnected -= HandleStoreDisconnected;
            store.OnProductsFetched -= HandleProductsFetched;
            store.OnProductsFetchFailed -= HandleProductsFetchFailed;
            store.OnPurchasePending -= HandlePurchasePending;
            store.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
            store.OnPurchaseFailed -= HandlePurchaseFailed;
            store.OnPurchaseDeferred -= HandlePurchaseDeferred;
            store.OnPurchasesFetched -= HandlePurchasesFetched;
            store.OnPurchasesFetchFailed -= HandlePurchasesFetchFailed;
        }
        store = null;
        runner = null;
        productsReady = connected = connecting = fetchingProducts = false;
        purchasingOffer = null;
        pending.Clear();
        offersByProductId.Clear();
    }

    private static async void Connect()
    {
        if (stopping || connecting || store == null) return;
        var current = store;
        connecting = true;
        try { await current.Connect(); }
        catch (Exception error)
        {
            if (stopping || current != store) return;
            Debug.LogWarning($"[IAP] Store connection failed: {error.Message}");
            ScheduleRetry();
        }
        finally { if (current == store) connecting = false; }
    }

    private static void HandleStoreConnected()
    {
        if (stopping) return;
        connected = true;
        FetchProducts();
    }

    private static void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        connected = productsReady = fetchingProducts = false;
        Debug.LogWarning($"[IAP] Store disconnected: {failure.Message}");
        ScheduleRetry();
        OnPricesUpdated?.Invoke();
    }

    private static void FetchProducts()
    {
        if (stopping || !connected || fetchingProducts) return;
        var definitions = new List<ProductDefinition>();
        offersByProductId.Clear();
        var catalog = ShopCatalog.Shared;
        if (catalog != null)
            foreach (var offer in catalog.RealMoneyOffers())
            {
                string id = offer.StoreProductId;
                if (offersByProductId.ContainsKey(id))
                {
                    Debug.LogError($"[IAP] Duplicate store product ID: {id}");
                    continue;
                }
                offersByProductId.Add(id, offer);
                definitions.Add(new ProductDefinition(id, ProductType.Consumable));
            }
        if (definitions.Count == 0) { Debug.LogError("[IAP] No real-money products in ShopCatalog."); return; }
        fetchingProducts = true;
        try { store.FetchProducts(definitions); }
        catch (Exception error)
        {
            fetchingProducts = false;
            Debug.LogWarning($"[IAP] Product request failed: {error.Message}");
            ScheduleRetry();
        }
    }

    private static void HandleProductsFetched(List<Product> products)
    {
        fetchingProducts = false;
        int available = 0;
        foreach (var product in products)
            if (product != null && product.availableToPurchase
                && offersByProductId.ContainsKey(product.definition.id)) available++;
        productsReady = available > 0;
        Debug.Log($"[IAP] Available products: {available}/{offersByProductId.Count}.");
        if (available < offersByProductId.Count) ScheduleRetry();
        else retryAttempt = 0;
        // Recover paid, unconfirmed consumables even if this product fetch was only partial.
        FetchPurchases();
        OnPricesUpdated?.Invoke();
    }

    private static void HandleProductsFetchFailed(ProductFetchFailed failure)
    {
        fetchingProducts = false;
        Debug.LogWarning($"[IAP] Product fetch failed: {failure.FailureReason}");
        ScheduleRetry();
    }

    private static void FetchPurchases()
    {
        if (stopping || !connected) return;
        try { store.FetchPurchases(); }
        catch (Exception error)
        {
            Debug.LogWarning($"[IAP] Purchase recovery failed: {error.Message}");
            ScheduleRetry();
        }
    }

    private static void HandlePurchasesFetched(Orders orders)
    {
        foreach (var order in orders.PendingOrders) HandlePurchasePending(order);
    }

    private static void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogWarning($"[IAP] Purchase recovery failed: {failure.message}");
        ScheduleRetry();
    }

    private static void ScheduleRetry()
    {
        if (stopping || runner == null || retry != null) return;
        retry = runner.StartCoroutine(Retry());
    }

    private static IEnumerator Retry()
    {
        int delay = Mathf.Min(60, 5 << Mathf.Min(retryAttempt++, 4));
        yield return new WaitForSecondsRealtime(delay);
        retry = null;
        if (stopping) yield break;
        if (connected) FetchProducts(); else Connect();
    }

    public static void ShowUnavailable()
    {
        RuntimeChoicePopup.Show(GameLocalization.Get("store_unavailable_title"), GameLocalization.Get("store_unavailable_body"));
        if (stopping || store == null) return;
        if (!connected) Connect();
        else if (!productsReady) FetchProducts();
    }

    public static bool TryGetPrice(ShopOffer offer, out string label)
    {
        label = null;
#if UNITY_EDITOR
        return false; // Keep the designed catalogue prices instead of FakeStore's $0.01.
#else
        if (!IsAvailable || offer == null) return false;
        var product = store.GetProductById(offer.StoreProductId);
        if (product == null || !product.availableToPurchase) return false;
        label = product.metadata?.localizedPriceString;
        return !string.IsNullOrEmpty(label);
#endif
    }

    public static bool BeginPurchase(ShopOffer offer)
    {
        if (!IsAvailable || offer == null || offer.priceType != ShopOffer.PriceType.RealMoney
            || IsPurchasing || !ShopState.IsAvailable(offer)) return false;
        if (!CurrencyLedger.SyncGateOpen || AuthLinkService.IsBusy || LocalAccountData.IsSwitchPending
            || AccountDeletionService.InProgress) { ShowUnavailable(); return false; }
        var product = store.GetProductById(offer.StoreProductId);
        if (product == null || !product.availableToPurchase) { ShowUnavailable(); return false; }
        purchasingOffer = offer;
        try { store.PurchaseProduct(product); return true; }
        catch (Exception error)
        {
            purchasingOffer = null;
            Debug.LogWarning($"[IAP] Purchase request failed: {error.Message}");
            ShowUnavailable();
            OnPurchaseFailed?.Invoke(offer, false);
            return false;
        }
    }

    private static void HandlePurchaseDeferred(DeferredOrder order) => purchasingOffer = null;

    private static void HandlePurchasePending(PendingOrder order)
    {
        if (stopping || order == null) return;
        string txId = order.Info?.TransactionID;
        if (string.IsNullOrWhiteSpace(txId) || txId.Contains("|"))
        {
            // Never invent an ID: a replay must resolve to the same durable receipt.
            Debug.LogError("[IAP] Missing or invalid transaction ID; order left unconfirmed.");
            purchasingOffer = null;
            return;
        }
#if !UNITY_EDITOR
        if (string.IsNullOrEmpty(order.Info.Receipt))
        {
            Debug.LogError("[IAP] Missing store receipt; order left unconfirmed.");
            purchasingOffer = null;
            return;
        }
#endif
        var items = order.CartOrdered?.Items();
        if (items == null || items.Count == 0) { purchasingOffer = null; return; }
        foreach (var item in items)
            if (item?.Product?.definition?.id == null || item.Quantity < 1
                || !offersByProductId.ContainsKey(item.Product.definition.id))
            {
                Debug.LogError("[IAP] Unknown product or invalid quantity; order left unconfirmed without delivery.");
                purchasingOffer = null;
                return;
            }
        pending[txId] = order;
        if (delivery == null && runner != null) delivery = runner.StartCoroutine(DeliverPending());
    }

    private static IEnumerator DeliverPending()
    {
        // Do not credit purchases to the temporary wallet before boot restore/account replacement.
        yield return null;
        while (!stopping && pending.Count > 0)
        {
            if (connected && CurrencyLedger.SyncGateOpen && !AuthLinkService.IsBusy
                && !LocalAccountData.IsSwitchPending && !AccountDeletionService.InProgress)
            {
                var snapshot = new List<KeyValuePair<string, PendingOrder>>(pending);
                foreach (var pair in snapshot)
                {
                    try
                    {
                        Deliver(pair.Value, pair.Key);
                        // Remove before ConfirmPurchase, which can synchronously call back.
                        pending.Remove(pair.Key);
                        purchasingOffer = null;
                        store.ConfirmPurchase(pair.Value);
                    }
                    catch (Exception error)
                    {
                        Debug.LogError($"[IAP] Delivery/confirmation interrupted; paid order retained for retry: {error.Message}");
                        if (!pending.ContainsKey(pair.Key)) pending.Add(pair.Key, pair.Value);
                    }
                }
            }
            yield return DeliveryWait;
        }
        delivery = null;
    }

    private static void Deliver(PendingOrder order, string txId)
    {
#if UNITY_EDITOR
        const string storeName = "FakeStore:";
#elif UNITY_ANDROID
        const string storeName = "GooglePlay:";
#else
        const string storeName = "AppleAppStore:";
#endif
        string receipt = storeName + txId;
        // Backward compatibility with the original, unqualified transaction IDs.
        if (IsProcessed(txId) || IsProcessed(receipt)) return;
        int line = 0;
        foreach (var item in order.CartOrdered.Items())
        {
            var offer = offersByProductId[item.Product.definition.id];
            for (int unit = 0; unit < item.Quantity; unit++)
            {
                string unitReceipt = receipt + ":" + line + ":" + unit;
                if (IsProcessed(unitReceipt)) continue;
                bool thanks = ShopPurchaseService.GrantStorePurchase(offer, unitReceipt);
                MarkProcessed(unitReceipt); // Save contents + receipt BEFORE any UI event.
                ShopPurchaseService.PublishStoreReceipt(offer, thanks);
            }
            line++;
        }
        MarkProcessed(receipt);
        FirebaseCloudSaveService.Push();
    }

    private static void HandlePurchaseConfirmed(Order order)
    {
        if (order is not FailedOrder failed) return;
        if (failed.FailureReason == PurchaseFailureReason.DuplicateTransaction) return;
        Debug.LogWarning($"[IAP] Confirmation failed: {failed.FailureReason} {failed.Details}");
        // FetchPurchases redelivers the original PendingOrder. Its receipt suppresses re-granting.
        ScheduleRetry();
    }

    private static void HandlePurchaseFailed(FailedOrder order)
    {
        var offer = purchasingOffer;
        purchasingOffer = null;
        bool cancelled = order.FailureReason == PurchaseFailureReason.UserCancelled;
        if (!cancelled)
        {
            Debug.LogWarning($"[IAP] Purchase failed: {order.FailureReason} {order.Details}");
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
    private static void Unused() { OnPricesUpdated?.Invoke(); OnPurchaseFailed?.Invoke(null, false); }
#endif

    private static bool IsProcessed(string txId)
    {
        string all = PlayerPrefs.GetString(ProcessedKey, "");
        if (all.Length > 0 && Array.IndexOf(all.Split('|'), txId) >= 0) return true;
        string device = PlayerPrefs.GetString(DeviceProcessedKey, "");
        return device.Length > 0 && Array.IndexOf(device.Split('|'), txId) >= 0;
    }

    private static void MarkProcessed(string txId)
    {
        // Do not evict older receipts: a late store redelivery must never become a fresh purchase.
        string all = PlayerPrefs.GetString(ProcessedKey, "");
        PlayerPrefs.SetString(ProcessedKey, all.Length == 0 ? txId : all + "|" + txId);
        string device = PlayerPrefs.GetString(DeviceProcessedKey, "");
        PlayerPrefs.SetString(DeviceProcessedKey, device.Length == 0 ? txId : device + "|" + txId);
        PlayerPrefs.Save();
    }
}
