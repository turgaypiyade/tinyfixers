using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ödül/bilgi göz atma balonu (kullanıcı 2026-11-28): popup açmak yerine dokunulan öğenin üstünde krem zeminli,
/// lacivert çerçeveli küçük kutu. İçerik: isteğe bağlı metin + isteğe bağlı ödül satırı (ikon + etiket).
/// Kök başına tek balon (bir kez kurulur, öğeler yeniden kullanılır). Kökü kaplayan görünmez yakalayıcı: herhangi
/// bir yere dokunmak kapatır; aynı öğeye yeniden dokunmak da kapatır (toggle).
/// Çerçeve: Resources/UIEffects/RewardPeekFrame (YellowFrameForButton'dan lacivert, içi şeffaf; 9-slice). Yerel,
/// ~4 KB — ana menü/liderlik chrome'u (parçalı içerik: yerel).
/// Kullananlar: WeeklyPodiumView (ilk 3 ödülü), ProgressBarView (hedef bilgisi + ödülü).
/// </summary>
public sealed class RewardPeekBubble : MonoBehaviour
{
    public readonly struct Item
    {
        public readonly Sprite icon;
        public readonly string label;
        public Item(Sprite icon, string label) { this.icon = icon; this.label = label; }
    }

    private const float ItemW = 92f, ItemH = 122f, ItemGap = 6f, RowGap = 8f, Pad = 30f, TextGap = 12f;
    private const int MaxPerRow = 5;
    private const float TextWidth = 520f, TextSize = 34f;
    private const float FrameThinning = 1.25f;   // 9-slice kenar kalınlığı: 44 px kenarı biraz incelt
    private static readonly Color FillColor = new Color(1f, 0.96f, 0.86f);
    private static readonly Color AmountSurface = new Color(0.45f, 0.28f, 0.12f);

    private static Sprite s_frame, s_fill;

    private RectTransform root, self, bubble, itemsRow;
    private TMP_Text body;
    private readonly List<(Image icon, TMP_Text amount)> items = new();
    private RectTransform anchor;
    private object key;

    /// Kökteki balonu getirir (yoksa kurar). Kök, balonun sınırı ve dokunma yakalayıcısının alanıdır.
    public static RewardPeekBubble On(RectTransform root)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
            if (root.GetChild(i).TryGetComponent(out RewardPeekBubble existing)) return existing;
        var go = new GameObject("RewardPeekBubble", typeof(RectTransform));
        go.layer = root.gameObject.layer;   // runtime UI: kamera görsün
        var bubble = go.AddComponent<RewardPeekBubble>();
        bubble.Build(root);
        return bubble;
    }

    public bool IsShowing(object k) => gameObject.activeSelf && Equals(key, k);

    /// Aynı anahtarla açıksa kapatır. text ya da items boş olabilir (ikisi birden değil).
    public void Toggle(object k, string text, IReadOnlyList<Item> rewards, RectTransform at)
    {
        if (IsShowing(k)) { Hide(); return; }
        Show(k, text, rewards, at);
    }

    public void Show(object k, string text, IReadOnlyList<Item> rewards, RectTransform at)
    {
        if (at == null) return;
        key = k;
        anchor = at;
        int count = rewards != null ? rewards.Count : 0;
        bool hasText = !string.IsNullOrEmpty(text);

        // Ödüller
        while (items.Count < count) items.Add(NewItem());
        int perRow = Mathf.Clamp(count, 1, MaxPerRow);
        int rows = count > 0 ? Mathf.CeilToInt(count / (float)perRow) : 0;
        float itemsW = count > 0 ? perRow * (ItemW + ItemGap) - ItemGap : 0f;
        float itemsH = rows > 0 ? rows * (ItemH + RowGap) - RowGap : 0f;
        for (int i = 0; i < items.Count; i++)
        {
            var (icon, amount) = items[i];
            var cell = (RectTransform)icon.transform.parent;
            bool on = i < count;
            cell.gameObject.SetActive(on);
            if (!on) continue;
            icon.sprite = rewards[i].icon;
            icon.enabled = icon.sprite != null;
            amount.text = rewards[i].label ?? string.Empty;
            int row = i / perRow, col = i % perRow, inRow = Mathf.Min(perRow, count - row * perRow);
            cell.anchoredPosition = new Vector2((col - (inRow - 1) * 0.5f) * (ItemW + ItemGap),
                                                ((rows - 1) * 0.5f - row) * (ItemH + RowGap));
        }

        // Metin
        float textW = 0f, textH = 0f;
        body.gameObject.SetActive(hasText);
        if (hasText)
        {
            float maxW = Mathf.Min(TextWidth, root.rect.width - 2f * (Pad + 24f));
            body.text = text;
            Vector2 pref = body.GetPreferredValues(text, maxW, 0f);
            textW = Mathf.Min(maxW, pref.x);
            textH = pref.y;
        }

        float innerW = Mathf.Max(itemsW, textW);
        float innerH = textH + itemsH + (hasText && count > 0 ? TextGap : 0f);
        bubble.sizeDelta = new Vector2(innerW + Pad * 2f, innerH + Pad * 2f);
        float top = innerH * 0.5f;
        if (hasText)
        {
            var brt = body.rectTransform;
            brt.sizeDelta = new Vector2(innerW, textH);
            brt.anchoredPosition = new Vector2(0f, top - textH * 0.5f);
        }
        itemsRow.anchoredPosition = new Vector2(0f, top - innerH + itemsH * 0.5f);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Place();
    }

    public void Hide()
    {
        key = null;
        anchor = null;
        if (this != null) gameObject.SetActive(false);
    }

    // Yalnız açıkken çalışır (kapalıyken GameObject pasif): kaynak öğe kaybolursa (ekran/panel kapandı) balon da kapanır.
    private void LateUpdate()
    {
        if (anchor == null || !anchor.gameObject.activeInHierarchy) Hide();
    }

    /// Öğenin üstüne; üstte yer yoksa altına. Kök içinde yatayda sıkıştırılır.
    private void Place()
    {
        Vector2 size = bubble.sizeDelta;
        Rect r = root.rect;
        Vector2 aTop = root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0f)));
        Vector2 aBottom = root.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin, 0f)));
        const float gap = 10f, edge = 12f;
        float y = aTop.y + gap + size.y * 0.5f;
        if (y + size.y * 0.5f > r.yMax - edge) y = aBottom.y - gap - size.y * 0.5f;
        float halfW = size.x * 0.5f;
        float x = Mathf.Clamp(aTop.x, r.xMin + edge + halfW, r.xMax - edge - halfW);
        bubble.localPosition = new Vector3(x, y, 0f);
    }

    private void Build(RectTransform parent)
    {
        root = parent;
        self = (RectTransform)transform;
        self.SetParent(parent, false);
        Stretch(self);

        // Görünmez yakalayıcı: her dokunuş kapatır (balonun üstü dahil).
        var catcher = NewImage("Catcher", self, null);
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;
        Stretch(catcher.rectTransform);
        var close = catcher.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(Hide);

        bubble = NewRect("Bubble", self);
        bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0.5f, 0.5f);

        if (s_frame == null) s_frame = Resources.Load<Sprite>("UIEffects/RewardPeekFrame");
        if (s_fill == null) s_fill = Resources.Load<Sprite>("UIEffects/LegalPanel");

        // Krem zemin çerçevenin halkası altına girecek kadar içeride (köşeler dışarı taşmasın).
        var fill = NewImage("Fill", bubble, s_fill);
        fill.color = FillColor;
        Sliced(fill);
        Stretch(fill.rectTransform, 6f);

        var frame = NewImage("Frame", bubble, s_frame);
        Sliced(frame);
        Stretch(frame.rectTransform);
        if (s_frame == null) frame.enabled = false;

        body = NewRect("Text", bubble).gameObject.AddComponent<TextMeshProUGUI>();
        CommonPopupView.StyleText(body, TextSize, CommonPopupSkin.Shared.bodyTextColor, readable: true);
        body.enableAutoSizing = false;   // ölçü GetPreferredValues'tan; otomatik boyut kutuyu küçültmesin
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        itemsRow = NewRect("Items", bubble);
        itemsRow.anchorMin = itemsRow.anchorMax = itemsRow.pivot = new Vector2(0.5f, 0.5f);
        itemsRow.sizeDelta = Vector2.zero;

        gameObject.SetActive(false);
    }

    private (Image, TMP_Text) NewItem()
    {
        var cell = NewRect("Item", itemsRow);
        cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0.5f, 0.5f);
        cell.sizeDelta = new Vector2(ItemW, ItemH);
        var icon = NewImage("Icon", cell, null);
        icon.preserveAspect = true;
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(84f, 84f);
        irt.anchoredPosition = new Vector2(0f, 14f);
        var amount = NewRect("Amount", cell).gameObject.AddComponent<TextMeshProUGUI>();
        CommonPopupView.StyleText(amount, 30f, Color.white);
        var art = amount.rectTransform;
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
        art.sizeDelta = new Vector2(ItemW + 8f, 36f);
        art.anchoredPosition = new Vector2(0f, -44f);
        TextSurfaceStyle.Apply(amount, AmountSurface);
        return (icon, amount);
    }

    private static void Sliced(Image img)
    {
        if (img.sprite == null || img.sprite.border.sqrMagnitude <= 0f) return;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = FrameThinning;
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = root.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var img = NewRect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
