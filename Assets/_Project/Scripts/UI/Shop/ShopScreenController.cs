using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Mağaza ekranı. Katalogdaki bölümleri + teklifleri içerik akışına basar
/// (her bölüm: başlık bandı + kartlar). Coin/Yıldız ile satın alma anında işlenir
/// (PlayerWallet + ShopRewardGranter); gerçek-para (TL) teklifleri v1'de "yakında".
///
/// Panel konvansiyonu: BottomTabController bu panelin GameObject'ini SetActive ile açar.
/// OnEnable'da içerik (yeniden) kurulur; satın alma sonrası affordability tazelenir.
/// </summary>
public sealed class ShopScreenController : MonoBehaviour
{
    /// <summary>UnityEvent&lt;string&gt; Inspector'da serialize olabilsin diye concrete alt-sınıf.</summary>
    [System.Serializable] public sealed class StringEvent : UnityEvent<string> { }

    [Header("Veri & Tema")]
    [SerializeField] private ShopCatalog catalog;
    [SerializeField] private UITheme theme;

    [Header("İçerik akışı")]
    [Tooltip("Bölüm başlıkları + kartların basılacağı dikey container (VerticalLayoutGroup + ScrollRect content).")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private ShopSectionHeader sectionHeaderPrefab;
    [Tooltip("Kutulu büyük paket kartı (cardStyle=Bundle).")]
    [SerializeField] private ShopOfferCard bundleCardPrefab;
    [Tooltip("Basit altın satırı kartı (cardStyle=CoinRow).")]
    [SerializeField] private ShopCoinRowCard coinRowPrefab;

    [Header("Üst bakiye")]
    [SerializeField] private TMP_Text coinBalanceText;
    [SerializeField] private TMP_Text starBalanceText;

    [Header("Gerçek-para (IAP entegre değil)")]
    [Tooltip("TL teklifine basılınca gösterilecek 'yakında' bilgisi (opsiyonel).")]
    [SerializeField] private GameObject comingSoonHint;
    [Tooltip("TL teklifine basılınca tetiklenir — IAP entegrasyonunu buraya bağlarsın.")]
    [SerializeField] private StringEvent onRealMoneyPurchaseRequested;

    [Header("Satın alma geri bildirimi")]
    [Tooltip("Başarılı alımda kısa süre gösterilen toast (opsiyonel).")]
    [SerializeField] private GameObject purchaseToast;
    [SerializeField] private TMP_Text purchaseToastText;
    [SerializeField, Min(0.2f)] private float toastSeconds = 1.4f;

    private readonly List<GameObject> spawned = new();
    private Coroutine toastRoutine;

    private void OnEnable()
    {
        PlayerWallet.OnCoinsChanged      += RefreshCoins;
        PlayerWallet.OnTotalStarsChanged += RefreshStars;
        if (purchaseToast != null) purchaseToast.SetActive(false);
        Build();
        RefreshBalances();
    }

    private void OnDisable()
    {
        PlayerWallet.OnCoinsChanged      -= RefreshCoins;
        PlayerWallet.OnTotalStarsChanged -= RefreshStars;
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        ClearSpawned();
        if (catalog == null || contentContainer == null) return;

        foreach (var section in catalog.sections)
        {
            if (section == null) continue;

            if (sectionHeaderPrefab != null)
            {
                var header = Instantiate(sectionHeaderPrefab, contentContainer);
                header.Setup(section, theme);
                spawned.Add(header.gameObject);
            }

            if (section.offers == null) continue;
            foreach (var offer in section.offers)
            {
                if (offer == null) continue;

                ShopOfferCardBase prefab = offer.cardStyle == ShopOffer.CardStyle.CoinRow
                    ? coinRowPrefab
                    : bundleCardPrefab;
                if (prefab == null) continue;

                var card = Instantiate(prefab, contentContainer);
                card.Configure(offer, theme, HandlePurchase);
                spawned.Add(card.gameObject);
            }
        }
    }

    private void ClearSpawned()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }

    // ------------------------------------------------------------------

    private void HandlePurchase(ShopOffer offer)
    {
        if (offer == null) return;

        switch (offer.priceType)
        {
            case ShopOffer.PriceType.Coins:
                if (PlayerWallet.SpendCoins(offer.priceAmount))
                    Fulfil(offer);
                break;

            case ShopOffer.PriceType.Stars:
                if (PlayerWallet.SpendStars(offer.priceAmount))
                    Fulfil(offer);
                break;

            case ShopOffer.PriceType.Free:
                Fulfil(offer);
                break;

            case ShopOffer.PriceType.RealMoney:
                if (comingSoonHint != null) comingSoonHint.SetActive(true);
                onRealMoneyPurchaseRequested?.Invoke(offer.id);
                break;
        }
    }

    private void Fulfil(ShopOffer offer)
    {
        ShopState.RecordPurchase(offer);   // günlük/tek-seferlik tekliflerin durumunu kaydet
        ShopRewardGranter.Grant(offer);
        ShowToast(offer.displayName + " alındı!");
        // Bakiye + uygunluk değişti → kartları tazele.
        Build();
        RefreshBalances();
    }

    private void ShowToast(string message)
    {
        if (purchaseToast == null) return;
        if (purchaseToastText != null) purchaseToastText.text = message;
        purchaseToast.SetActive(true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastAfter());
    }

    private IEnumerator HideToastAfter()
    {
        yield return new WaitForSecondsRealtime(toastSeconds);
        if (purchaseToast != null) purchaseToast.SetActive(false);
        toastRoutine = null;
    }

    // ------------------------------------------------------------------

    private void RefreshBalances()
    {
        RefreshCoins(PlayerWallet.Coins);
        RefreshStars(PlayerWallet.TotalStars);
    }

    private void RefreshCoins(int amount)
    {
        if (coinBalanceText != null) coinBalanceText.text = amount.ToString("N0");
    }

    private void RefreshStars(int amount)
    {
        if (starBalanceText != null) starBalanceText.text = amount.ToString("N0");
    }
}
