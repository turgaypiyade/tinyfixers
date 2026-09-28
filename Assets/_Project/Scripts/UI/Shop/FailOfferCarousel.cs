using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fail popup'ının altında, karartma üzerinde dönen mağaza vitrini. Kartlar mağazadakiyle birebir aynı
/// prefab (ShopOfferCard); fiyat butonları doğrudan satın alır. Kaydırma/nokta/otomatik geçiş
/// <see cref="HorizontalPager"/>'dan.
///
/// Sayfalar: katalogda "Fail popup'ında göster" işaretli her Bundle bir sayfa + son sayfa "altın üçlüsü"
/// (aynı Bundle çerçevesinde yan yana 3 altın paketi). Üçlü: işaretli altın paketleri varsa onlar; yoksa
/// devam için eksik altını karşılayan en küçük paket + sonraki iki kademe.
/// </summary>
public sealed class FailOfferCarousel : MonoBehaviour
{
    private const float ViewportWidth = 1080f;
    private const float PageSpacing = 1000f;   // kart (930) + boşluk
    private const float DotSize = 20f;
    private const int TrioSize = 3;

    private sealed class Page
    {
        public ShopOffer bundle;          // tek paket sayfası
        public List<ShopOffer> coins;     // ya da altın üçlüsü
        public ShopOfferCard card;
    }

    private InGameShopOverlay.Refs refs;
    private Action<ShopOffer> onPurchase;
    private HorizontalPager pager;
    private readonly List<Page> pages = new();

    public int PageCount => pages.Count;

    /// <summary>parent altında (fail popup kökü) carousel'i kurar; içeriği <see cref="Refresh"/> doldurur.</summary>
    public static FailOfferCarousel Create(RectTransform parent, Vector2 anchoredPosition, float scale,
        float height, InGameShopOverlay.Refs refs, float autoAdvanceSeconds, Sprite dotSprite,
        Action<ShopOffer> onPurchase)
    {
        var pager = HorizontalPager.Create(parent, "FailOfferCarousel");
        var rt = (RectTransform)pager.transform;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(ViewportWidth, height);
        rt.localScale = Vector3.one * scale;
        pager.PageSpacing = PageSpacing;
        pager.AutoAdvanceSeconds = autoAdvanceSeconds;
        pager.DotSprite = dotSprite;
        pager.DotSize = DotSize;
        pager.DotsBottomOffset = DotSize;

        var carousel = pager.gameObject.AddComponent<FailOfferCarousel>();
        carousel.refs = refs;
        carousel.onPurchase = onPurchase;
        carousel.pager = pager;
        return carousel;
    }

    /// <summary>Sayfaları katalogdan yeniden seçer ve kartları kurar.</summary>
    /// <param name="coinShortfall">Devam için eksik altın (üçlü seçimi bunu karşılayan paketten başlar).</param>
    public void Refresh(int coinShortfall)
    {
        pager.Clear();
        pages.Clear();

        if (refs.bundleCardPrefab != null)
            CollectPages(refs.catalog, coinShortfall, pages);

        foreach (var page in pages)
        {
            var pageRt = pager.AddPage();
            var card = Instantiate(refs.bundleCardPrefab, pageRt, false);
            var rt = (RectTransform)card.transform;
            // Mağazada boyu VerticalLayout + LayoutElement verir; burada layout yok → aynısını elle uygula.
            Vector2 size = rt.sizeDelta;
            if (card.TryGetComponent(out LayoutElement le) && le.preferredHeight > 0f)
                size.y = le.preferredHeight;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, DotSize);   // noktalara yer bırak
            page.card = card;
            ConfigureCard(page);
        }

        pager.Finish();
        gameObject.SetActive(pages.Count > 0);
    }

    /// <summary>Satın alma sonrası fiyat/uygunluk durumunu tazeler (sayfa yerinde kalır).</summary>
    public void RefreshPrices()
    {
        foreach (var page in pages)
            ConfigureCard(page);
    }

    private void ConfigureCard(Page page)
    {
        if (page.card == null) return;
        if (page.coins != null)
            page.card.ConfigureCoinTrio(page.coins, refs.theme, onPurchase);
        else
            page.card.Configure(page.bundle, refs.theme, onPurchase);
    }

    private static void CollectPages(ShopCatalog catalog, int coinShortfall, List<Page> result)
    {
        if (catalog == null || catalog.sections == null) return;

        var allCoins = new List<ShopOffer>();
        var markedCoins = new List<ShopOffer>();
        foreach (var section in catalog.sections)
        {
            if (section?.offers == null) continue;
            foreach (var offer in section.offers)
            {
                if (offer == null || !ShopState.IsAvailable(offer)) continue;
                if (offer.cardStyle == ShopOffer.CardStyle.CoinRow)
                {
                    allCoins.Add(offer);
                    if (offer.showOnFailPopup) markedCoins.Add(offer);
                }
                else if (offer.showOnFailPopup)
                {
                    result.Add(new Page { bundle = offer });
                }
            }
        }

        var trio = markedCoins.Count > 0 ? markedCoins : PickCoinsForShortfall(allCoins, coinShortfall);
        trio.Sort((a, b) => CoinAmount(a).CompareTo(CoinAmount(b)));
        if (trio.Count > TrioSize) trio.RemoveRange(TrioSize, trio.Count - TrioSize);
        if (trio.Count > 0)
            result.Add(new Page { coins = trio });
    }

    // Eksik altını karşılayan en küçük paket + sonraki iki kademe (sona yakınsa pencere geri kayar).
    private static List<ShopOffer> PickCoinsForShortfall(List<ShopOffer> coins, int shortfall)
    {
        var sorted = new List<ShopOffer>(coins);
        sorted.Sort((a, b) => CoinAmount(a).CompareTo(CoinAmount(b)));

        int first = sorted.FindIndex(o => CoinAmount(o) >= shortfall);
        if (first < 0) first = sorted.Count - 1;
        first = Mathf.Clamp(first, 0, Mathf.Max(0, sorted.Count - TrioSize));
        return sorted.GetRange(first, Mathf.Min(TrioSize, sorted.Count - first));
    }

    // Paketin verdiği altın (grants); grant yoksa görünen miktar.
    private static int CoinAmount(ShopOffer offer)
    {
        int total = 0;
        if (offer.groups == null) return 0;
        foreach (var group in offer.groups)
        {
            if (group?.grants == null) continue;
            foreach (var grant in group.grants)
                if (grant != null && grant.kind == ShopReward.Kind.Coins) total += grant.amount;
        }
        if (total == 0 && offer.groups.Count > 0 && offer.groups[0] != null)
            total = offer.groups[0].labelValue;
        return total;
    }
}
