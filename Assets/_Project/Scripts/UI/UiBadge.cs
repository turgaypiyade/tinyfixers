using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ortak kırmızı bildirim rozeti: beyaz kontur halkası + kırmızı dolgu (+ opsiyonel sayı).
/// Daire sprite'ı koddan üretilir (atama gerekmez). Ana menü ikonlarındaki "yeni" noktası ve
/// can kutusunun köşesindeki fazla-can sayacı aynı görünümü kullanır.
/// </summary>
public static class UiBadge
{
    public static readonly Color RingColor = Color.white;
    public static readonly Color FillColor = new Color(0.88f, 0.12f, 0.14f, 1f);
    private const float RingThickness01 = 0.13f;   // halka kalınlığı (çapın oranı)

    private static Sprite _circle;

    /// <summary>Kenarı yumuşatılmış beyaz daire (renk Image.color ile verilir).</summary>
    public static Sprite Circle
    {
        get
        {
            if (_circle == null) _circle = BuildCircle(128);
            return _circle;
        }
    }

    /// <summary>Var olan bir Image'ı (ör. sahnedeki kare kırmızı nokta) rozete çevirir.
    /// Image beyaz halka olur, içine kırmızı dolgu eklenir.</summary>
    public static void StyleDot(Image dot, float size, Vector2 cornerOffset)
    {
        if (dot == null) return;
        dot.sprite = Circle;
        dot.color = RingColor;
        dot.raycastTarget = false;

        var rt = dot.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = cornerOffset;

        EnsureFill(rt);
    }

    /// <summary>Sayılı rozet oluşturur; sayı yazısı döner (rozetin kökü = text.transform.parent).</summary>
    public static TMP_Text CreateCount(RectTransform parent, string name, float size, TMP_FontAsset font)
    {
        var root = NewRect(parent, name);
        root.sizeDelta = new Vector2(size, size);
        var ring = root.gameObject.AddComponent<Image>();
        ring.sprite = Circle;
        ring.color = RingColor;
        ring.raycastTarget = false;

        var fill = EnsureFill(root);

        var label = NewRect(fill, "Count").gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        if (font != null) label.font = font;
        label.fontSize = size * 0.58f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform EnsureFill(RectTransform ring)
    {
        var existing = ring.Find("Fill") as RectTransform;
        if (existing != null) return existing;

        var fill = NewRect(ring, "Fill");
        float inset = ring.sizeDelta.x * RingThickness01;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(inset, inset);
        fill.offsetMax = new Vector2(-inset, -inset);
        var img = fill.gameObject.AddComponent<Image>();
        img.sprite = Circle;
        img.color = FillColor;
        img.raycastTarget = false;
        return fill;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling guard
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private static Sprite BuildCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "UiBadgeCircle"
        };
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - r, dy = y + 0.5f - r;
            float a = Mathf.Clamp01(r - 1f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);   // ~1px kenar yumuşatma
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
