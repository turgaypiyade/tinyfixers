using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bundle kartındaki tek bir ödül kutusu: içine dizilen ikonlar + tek etiket (miktar / adet / süre)
/// + opsiyonel saat ikonu. İkonları koddan doldurur ve ORTALANMIŞ satırlara dizer
/// (4→2+2, 5→3+2 ortalı). Boyut/aralık Inspector'dan ayarlanır. Kutu arka planı group.background'tan.
/// </summary>
public sealed class ShopRewardGroupBox : MonoBehaviour
{
    [Header("Kutu")]
    [SerializeField] private Image panelBackground;

    [Header("İkonlar")]
    [Tooltip("İkonların basılacağı container. Varsa GridLayoutGroup runtime'da kapatılır (manuel ortalı yerleşim).")]
    [SerializeField] private Transform iconContainer;
    [Tooltip("Tek ikon prefab'ı (Image). Kod her sprite için bir kopya basar.")]
    [SerializeField] private Image iconPrefab;

    [Header("İkon düzeni")]
    [Tooltip("Her ikonun kenar boyutu (px). Büyütmek için artır.")]
    [SerializeField] private float iconSize = 60f;
    [Tooltip("İkonlar arası boşluk (px).")]
    [SerializeField] private float iconSpacing = 6f;
    [Tooltip("Bir satırdaki maksimum ikon (üst sınır). 5 ikon → 3 üst + 2 alt.")]
    [SerializeField] private int maxColumns = 3;

    [Header("Etiket")]
    [Tooltip("Duration modunda gösterilen saat ikonu objesi (opsiyonel).")]
    [SerializeField] private GameObject timerIcon;
    [Tooltip("Saat ikonu sprite'ı — buraya atarsan saat kutusuna basılır (aksi hâlde boş kutu).")]
    [SerializeField] private Sprite timerSprite;
    [Tooltip("Saat ikonu kenar boyutu (px). Büyütmek için artır.")]
    [SerializeField] private float timerSize = 56f;
    [SerializeField] private TMP_Text labelText;

    private readonly List<GameObject> spawnedIcons = new();

    public void Setup(ShopRewardGroup group, UITheme theme)
    {
        ClearIcons();
        if (group == null) return;

        // Kutu arka planı: önce grup'a atanan (MATGrup1/3/5), yoksa tema.
        if (panelBackground != null)
        {
            if (group.background != null)
            {
                panelBackground.sprite = group.background;
                panelBackground.color = Color.white;
                panelBackground.enabled = true;
            }
            else if (theme != null)
            {
                UITheme.ApplySurface(panelBackground, theme.panelBackground, theme.creamSurface);
            }
        }

        // İkonları bas — manuel yerleşim yapacağımız için varsa GridLayoutGroup'u kapat.
        if (iconContainer != null)
        {
            var glg = iconContainer.GetComponent<GridLayoutGroup>();
            if (glg != null) glg.enabled = false;

            if (iconPrefab != null && group.icons != null)
            {
                foreach (var sprite in group.icons)
                {
                    if (sprite == null) continue;
                    var icon = Instantiate(iconPrefab, iconContainer);
                    icon.sprite = sprite;
                    icon.enabled = true;
                    icon.preserveAspect = true;
                    icon.gameObject.SetActive(true);
                    spawnedIcons.Add(icon.gameObject);
                }
            }
            ArrangeIcons();
        }

        if (timerIcon != null)
        {
            bool showTimer = group.labelMode == ShopRewardGroup.LabelMode.Duration && group.showTimerIcon;
            timerIcon.SetActive(showTimer);
            if (showTimer)
            {
                // Boyut: LayoutElement (satır bunu okur) + RectTransform, ikisini de büyüt.
                var le = timerIcon.GetComponent<LayoutElement>();
                if (le != null) { le.preferredWidth = timerSize; le.preferredHeight = timerSize; }
                ((RectTransform)timerIcon.transform).sizeDelta = new Vector2(timerSize, timerSize);

                var img = timerIcon.GetComponent<Image>();
                if (img != null)
                {
                    if (timerSprite != null) img.sprite = timerSprite;
                    img.color = Color.white; img.preserveAspect = true;
                }
            }
        }

        if (labelText != null)
        {
            labelText.text = group.GroupLabel();
            if (theme != null) theme.ApplyText(labelText, theme.textOnCream, heading: true);
        }
    }

    /// <summary>İkonları ortalanmış 2 satıra diz: 1-2 tek satır, 3→2+1, 4→2+2, 5→3+2 (kalan alt satır ortalı).</summary>
    private void ArrangeIcons()
    {
        int n = spawnedIcons.Count;
        if (n == 0) return;

        // Dengeli 2 satır: üst = ceil(n/2), maxColumns ile sınırlı. 3→2+1, 4→2+2, 5→3+2 (hep ortalı).
        int cols = n <= 2 ? n : Mathf.Min(maxColumns, Mathf.CeilToInt(n / 2f));

        int rows = Mathf.CeilToInt(n / (float)cols);
        float step = iconSize + iconSpacing;
        float totalH = rows * iconSize + (rows - 1) * iconSpacing;

        for (int i = 0; i < n; i++)
        {
            int r = i / cols;
            int c = i % cols;
            int itemsInRow = Mathf.Min(cols, n - r * cols);          // son satır daha az olabilir
            float rowW = itemsInRow * iconSize + (itemsInRow - 1) * iconSpacing;

            float x = -rowW / 2f + iconSize / 2f + c * step;         // satırı yatayda ortala
            float y =  totalH / 2f - iconSize / 2f - r * step;       // satırları dikeyde ortala

            var rt = (RectTransform)spawnedIcons[i].transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }

    private void ClearIcons()
    {
        foreach (var go in spawnedIcons) if (go != null) Destroy(go);
        spawnedIcons.Clear();
    }
}
