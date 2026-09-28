using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bundle kartı (MegaAwards1 çerçevesi): SOL'da doğrudan altın ikonu + miktar (kutu YOK),
/// sağında ödül kutuları + mor bantta isim + BuyButton.
///
/// Veri: <see cref="ShopOffer.groups"/>[0] = hero (altın), groups[1..] = kutular.
/// Kutu arka planı ikon sayısına göre OTOMATİK seçilir (1→MATGrup1, 2-3→MATGrup3, 4+→MATGrup5);
/// genişlik sprite en-boyuna orantılı, yükseklik hepsinde aynı. Boş slot gizlenir; görünen kutular
/// ayrılan alanı oranlarına göre doldurur (biri gizlenince kalanlar büyür).
/// </summary>
public sealed class ShopOfferCard : ShopOfferCardBase
{
    [Header("Hero (altın, sol — kutu değil)")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TMP_Text heroAmountText;

    [Header("Ödül kutuları (altının sağı)")]
    [Tooltip("Prefab'ta yerleştirilmiş kutu slotları; groups[1..] soldan sağa doldurulur, kalanı gizlenir.")]
    [SerializeField] private ShopRewardGroupBox[] boxes;

    [Header("Kutu arka planları (ikon sayısına göre otomatik)")]
    [Tooltip("1 ikon")] [SerializeField] private Sprite matGrup1;
    [Tooltip("2-3 ikon")] [SerializeField] private Sprite matGrup3;
    [Tooltip("4+ ikon")] [SerializeField] private Sprite matGrup5;
    [Tooltip("Süreli kutularda saat sprite'ı (tüm kutulara verilir).")]
    [SerializeField] private Sprite timerSprite;

    [Header("Kutu alanı (normalize, altının sağı)")]
    [SerializeField] private float boxAreaLeft   = 0.24f;
    [SerializeField] private float boxAreaRight  = 0.965f;
    [SerializeField] private float boxAreaBottom = 0.40f;
    [SerializeField] private float boxAreaTop    = 0.90f;
    [Tooltip("Kutular arası yatay boşluk (normalize).")]
    [SerializeField] private float boxGap = 0.012f;

    [Header("Diğer")]
    [SerializeField] private GameObject bestBadge;   // "En İyi Fırsat" kurdelesi
    [SerializeField] private TMP_Text nameText;

    private readonly List<ShopRewardGroupBox> visibleBoxes = new();
    private readonly List<ShopRewardGroup> visibleGroups = new();
    private readonly List<Sprite> visibleBg = new();
    private readonly List<float> visibleAspects = new();

    protected override void BuildBody()
    {
        if (bestBadge != null) bestBadge.SetActive(offer.showBestBadge);

        var groups = offer.groups;
        int count = groups?.Count ?? 0;

        // --- groups[0] = altın hero (doğrudan sol ikon + miktar) ---
        ShopRewardGroup hero = count > 0 ? groups[0] : null;
        if (heroIcon != null)
        {
            Sprite s = (hero?.icons != null && hero.icons.Count > 0) ? hero.icons[0] : null;
            heroIcon.sprite = s;
            heroIcon.enabled = s != null;
            heroIcon.preserveAspect = true;
        }
        // Yalnız metni yaz — font/material/renk Label'ın kendi TMP ayarlarında kalır (outline korunur).
        if (heroAmountText != null)
            heroAmountText.text = hero != null ? hero.GroupLabel() : "";

        // --- groups[1..] = kutular ---
        visibleBoxes.Clear();
        visibleGroups.Clear();
        visibleBg.Clear();
        visibleAspects.Clear();

        if (boxes != null)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null) continue;

                int g = i + 1;   // hero'yu atla
                ShopRewardGroup grp = (g < count) ? groups[g] : null;

                if (grp == null)
                {
                    box.gameObject.SetActive(false);   // boş slot → gizle
                    continue;
                }

                Sprite bg = SelectBackground(grp.icons?.Count ?? 0);
                box.gameObject.SetActive(true);
                visibleBoxes.Add(box);
                visibleGroups.Add(grp);
                visibleBg.Add(bg);
                visibleAspects.Add(Aspect(bg));
            }
        }

        LayoutVisibleBoxes(boxAreaLeft, boxAreaRight, boxGap);

        for (int i = 0; i < visibleBoxes.Count; i++)
            visibleBoxes[i].Setup(visibleGroups[i], theme, visibleBg[i], timerSprite);

        if (nameText != null)
        {
            nameText.text = offer.displayName;
            if (theme != null) theme.ApplyText(nameText, theme.textLight, heading: true);
        }
    }

    /// <summary>İkon sayısına göre kutu arka planı: 1→MATGrup1, 2-3→MATGrup3, 4+→MATGrup5 (0 da MATGrup1).</summary>
    private Sprite SelectBackground(int iconCount)
    {
        if (iconCount >= 4) return matGrup5;
        if (iconCount >= 2) return matGrup3;
        return matGrup1;
    }

    private static float Aspect(Sprite s)
        => (s != null && s.rect.height > 0f) ? s.rect.width / s.rect.height : 1f;

    /// <summary>Görünen kutuları [left, right] içine en-boy oranlarına göre yayar (aynı yükseklik).</summary>
    private void LayoutVisibleBoxes(float left, float right, float gap)
    {
        int n = visibleBoxes.Count;
        if (n == 0) return;

        float sum = 0f;
        for (int i = 0; i < n; i++) sum += visibleAspects[i];
        if (sum <= 0f) sum = n;

        float totalGap = gap * (n - 1);
        float usable = (right - left) - totalGap;
        if (usable <= 0f) { usable = right - left; totalGap = 0f; }

        float x = left;
        for (int i = 0; i < n; i++)
        {
            float w = usable * (visibleAspects[i] / sum);
            var rt = (RectTransform)visibleBoxes[i].transform;
            rt.anchorMin = new Vector2(x, boxAreaBottom);
            rt.anchorMax = new Vector2(x + w, boxAreaTop);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            x += w + gap;
        }
    }

    // ── Altın üçlüsü (fail carousel'i) ────────────────────────────────────────
    // Aynı çerçeve (MegaAwards1): krem alanda 3 kutu (MATGrup1, altın görseli + miktar), mor bantta her
    // kutunun altında kendi fiyat butonu (prefab'taki PriceButton'ın kopyası). Hero/isim/kurdele gizli.
    private const float TrioAreaLeft = 0.04f;
    private const float TrioAreaRight = 0.96f;
    private const float TrioGap = 0.03f;
    private const float TrioIconFill = 0.5f;          // ikon = kutu yüksekliğinin oranı
    private const float TrioButtonWidthFill = 0.95f;  // buton = sütun genişliğinin en fazla oranı

    private readonly List<GameObject> trioButtons = new();

    /// <summary>Kartı 1-3 altın paketinin yan yana satıldığı üçlü görünüme çevirir (Configure yerine).</summary>
    public void ConfigureCoinTrio(IReadOnlyList<ShopOffer> coinOffers, UITheme uiTheme,
        System.Action<ShopOffer> purchaseHandler)
    {
        theme = uiTheme;
        if (bestBadge != null) bestBadge.SetActive(false);
        if (heroIcon != null) heroIcon.enabled = false;
        if (heroAmountText != null) heroAmountText.gameObject.SetActive(false);
        if (nameText != null) nameText.gameObject.SetActive(false);

        foreach (var go in trioButtons) if (go != null) Destroy(go);
        trioButtons.Clear();

        int n = Mathf.Min(coinOffers?.Count ?? 0, boxes?.Length ?? 0, 3);
        visibleBoxes.Clear();
        visibleGroups.Clear();
        visibleBg.Clear();
        visibleAspects.Clear();
        for (int i = 0; i < (boxes?.Length ?? 0); i++)
        {
            if (boxes[i] == null) continue;
            var coin = i < n ? coinOffers[i] : null;
            var grp = coin?.groups != null && coin.groups.Count > 0 ? coin.groups[0] : null;
            boxes[i].gameObject.SetActive(grp != null);
            if (grp == null) continue;
            visibleBoxes.Add(boxes[i]);
            visibleGroups.Add(grp);
            visibleBg.Add(matGrup1);
        }

        var cardRt = (RectTransform)transform;
        float cardW = cardRt.rect.width;
        float cardH = cardRt.rect.height;

        // Kutular MATGrup1'in KENDİ en-boy oranında (esnetilmez → köşeler bozulmaz). Eşit sütunlara
        // ortalanır; sığmazsa oran korunarak küçülür.
        int count = visibleBoxes.Count;
        float columnWidth = count > 0 ? (TrioAreaRight - TrioAreaLeft) * cardW / count : 0f;
        float boxHeight = (boxAreaTop - boxAreaBottom) * cardH;
        float boxWidth = boxHeight * Aspect(matGrup1);
        float maxWidth = columnWidth - TrioGap * cardW;
        if (boxWidth > maxWidth && boxWidth > 0f)
        {
            boxHeight *= maxWidth / boxWidth;
            boxWidth = maxWidth;
        }
        float centerY = (boxAreaBottom + boxAreaTop) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var rt = (RectTransform)visibleBoxes[i].transform;
            float xNorm = TrioAreaLeft + (i + 0.5f) * columnWidth / Mathf.Max(1f, cardW);
            rt.anchorMin = rt.anchorMax = new Vector2(xNorm, centerY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(boxWidth, boxHeight);
            visibleBoxes[i].Setup(visibleGroups[i], theme, visibleBg[i], timerSprite, boxHeight * TrioIconFill);
        }

        if (priceButton == null) return;

        // Orijinal buton mor bantta; yüksekliğini koru, her kopyayı kendi kutusunun altına ortala.
        var btnRt = (RectTransform)priceButton.transform;
        Vector2 btnCenter = cardRt.InverseTransformPoint(btnRt.TransformPoint(btnRt.rect.center));
        float yNorm = (btnCenter.y - cardRt.rect.yMin) / cardRt.rect.height;
        priceButton.gameObject.SetActive(false);

        for (int i = 0; i < visibleBoxes.Count; i++)
        {
            var coin = coinOffers[i];
            var boxRt = (RectTransform)visibleBoxes[i].transform;
            float xNorm = boxRt.anchorMin.x;   // kutu merkezine sabitlendi (min == max)
            float colWidth = columnWidth;

            var clone = Instantiate(priceButton.gameObject, cardRt, false);
            clone.name = "TrioPriceButton" + i;
            clone.SetActive(true);
            var rt = (RectTransform)clone.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(xNorm, yNorm);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = btnRt.rect.size;
            rt.localScale = Vector3.one * Mathf.Min(1f, colWidth * TrioButtonWidthFill / Mathf.Max(1f, btnRt.rect.width));

            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = PriceLabel(coin);

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => purchaseHandler?.Invoke(coin));
                button.interactable = true;
            }
            trioButtons.Add(clone);
        }
    }
}
