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

        LayoutVisibleBoxes();

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

    /// <summary>Görünen kutuları [boxAreaLeft, boxAreaRight] içine en-boy oranlarına göre yayar (aynı yükseklik).</summary>
    private void LayoutVisibleBoxes()
    {
        int n = visibleBoxes.Count;
        if (n == 0) return;

        float sum = 0f;
        for (int i = 0; i < n; i++) sum += visibleAspects[i];
        if (sum <= 0f) sum = n;

        float totalGap = boxGap * (n - 1);
        float usable = (boxAreaRight - boxAreaLeft) - totalGap;
        if (usable <= 0f) { usable = boxAreaRight - boxAreaLeft; totalGap = 0f; }

        float x = boxAreaLeft;
        for (int i = 0; i < n; i++)
        {
            float w = usable * (visibleAspects[i] / sum);
            var rt = (RectTransform)visibleBoxes[i].transform;
            rt.anchorMin = new Vector2(x, boxAreaBottom);
            rt.anchorMax = new Vector2(x + w, boxAreaTop);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            x += w + boxGap;
        }
    }
}
