using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair'in koddan kurulan UI parçaları için küçük yardımcılar. Runtime'da doğan objeler
/// parent'ın layer'ını alır (layer 0'da doğan UI, Screen Space Camera culling'ine takılıp görünmez olur).
/// </summary>
public static class BridgeRepairUI
{
    public static readonly Color Cream = new Color(1f, 0.97f, 0.86f, 1f);
    public static readonly Color Gold  = new Color(1f, 0.83f, 0.25f, 1f);
    public static readonly Color Ink   = new Color(0.16f, 0.09f, 0.03f, 1f);

    public static string L(string key, string fallback)
    {
        string value = GameLocalization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    public static string LFormat(string key, string fallback, params object[] args)
    {
        string format = L(key, fallback);
        try { return string.Format(format, args); }
        catch (System.FormatException) { return format; }
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position,
        bool preserveAspect = true)
    {
        var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    public static Image Solid(string name, Transform parent, Color color)
    {
        var image = Stretch(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Material _crispGold;
    private static Material CrispGold => _crispGold != null ? _crispGold
        : (_crispGold = Resources.Load<Material>("TextMaterials/GoldOutlineCrisp"));

    /// Ödül yazısı stili (RewardTextStyle) — yoksa kalın krem + koyu outline.
    public static TMP_Text Label(string name, Transform parent, string value, float size, Vector2 box,
        Vector2 position, Color? color = null, bool rewardStyle = true)
    {
        var text = Rect(name, parent, box, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.raycastTarget = false;
        if (rewardStyle && RewardTextStyle.Shared != null && RewardTextStyle.Shared.font != null)
        {
            RewardTextStyle.Apply(text, size);
            // Sarı + altın kontur kalır; ortak materyalin geniş/yumuşak kahve gölgesi (çamurlu hale) yerine
            // dar ve net alt gölgeli varyant (Resources/TextMaterials/GoldOutlineCrisp).
            if (CrispGold != null) text.fontSharedMaterial = CrispGold;
            text.textWrappingMode = TextWrappingModes.Normal;
        }
        else
        {
            text.fontSize = text.fontSizeMax = size;
            text.fontSizeMin = size * 0.6f;
            text.enableAutoSizing = true;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.outlineColor = Ink;
            text.outlineWidth = 0.18f;
            text.color = Cream;
        }
        if (color.HasValue) text.color = color.Value;
        return text;
    }

    public static Button MakeButton(Image image, UnityEngine.Events.UnityAction onClick)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    /// Yuvarlak avatar (maskeli) + renkli çerçeve.
    public static RectTransform Avatar(string name, Transform parent, Sprite avatar, float size, Vector2 position,
        Color ring)
    {
        var root = Rect(name, parent, Vector2.one * size, position);
        var frame = root.gameObject.AddComponent<Image>();
        frame.sprite = Circle();
        frame.color = ring;
        frame.raycastTarget = false;

        var maskRt = Rect("Mask", root, Vector2.one * (size * 0.84f), Vector2.zero);
        var maskImg = maskRt.gameObject.AddComponent<Image>();
        maskImg.sprite = Circle();
        maskImg.raycastTarget = false;
        maskRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var face = Picture("Face", maskRt, avatar, Vector2.one * (size * 0.84f), Vector2.zero, preserveAspect: false);
        face.enabled = true;
        if (avatar == null) face.color = new Color(0.55f, 0.6f, 0.7f, 1f);
        return root;
    }

    private static Sprite s_circle;
    public static Sprite Circle()
    {
        if (s_circle != null) return s_circle;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        s_circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_circle;
    }

    private static Sprite s_glow;
    public static Sprite Glow()
    {
        if (s_glow != null) return s_glow;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        s_glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_glow;
    }

    private static Sprite s_roundedPill;
    /// 9-slice yuvarlak köşeli dolgu (isim etiketi / rozet zemini).
    public static Sprite Pill()
    {
        if (s_roundedPill != null) return s_roundedPill;
        const int size = 64, radius = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d) * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        s_roundedPill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return s_roundedPill;
    }

    /// Nasıl oynanır okları: kalın, kavisli sarı ok (koyu kenarlı). flip = sağdan sola kıvrılan.
    public static Sprite CurvedArrow(bool flip)
    {
        const int w = 200, h = 220;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var fill = new float[w * h];
        var edge = new float[w * h];

        // Quadratic bezier: sol-üstten sağa kıvrılıp aşağı iner (y aşağı doğru azalan tex koordinatı).
        Vector2 p0 = new Vector2(20, 190), p1 = new Vector2(165, 185), p2 = new Vector2(150, 60);
        const float thick = 15f, outline = 5f;
        for (int i = 0; i <= 200; i++)
        {
            float t = i / 200f;
            Vector2 p = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
            float r = thick * Mathf.Lerp(0.55f, 1f, t);
            Stamp(fill, edge, w, h, p, r, outline);
        }
        // Ok başı: uç noktada aşağı bakan üçgen.
        Vector2 tip = new Vector2(150, 12);
        Vector2 left = new Vector2(112, 72), right = new Vector2(190, 72);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var q = new Vector2(x + 0.5f, y + 0.5f);
            float d = TriangleDistance(q, tip, left, right);
            if (d <= 0f) fill[y * w + x] = 1f;
            if (d <= outline) edge[y * w + x] = Mathf.Max(edge[y * w + x], Mathf.Clamp01(outline - d + 0.5f));
        }

        var pixels = new Color32[w * h];
        var yellow = new Color(1f, 0.8f, 0.16f);
        var dark = new Color(0.45f, 0.22f, 0.02f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int sx = flip ? w - 1 - x : x;
            float f = fill[y * w + sx], e = edge[y * w + sx];
            float a = Mathf.Max(f, e);
            var c = Color.Lerp(dark, yellow, f);
            pixels[y * w + x] = new Color(c.r, c.g, c.b, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void Stamp(float[] fill, float[] edge, int w, int h, Vector2 c, float r, float outline)
    {
        int x0 = Mathf.Max(0, (int)(c.x - r - outline - 1)), x1 = Mathf.Min(w - 1, (int)(c.x + r + outline + 1));
        int y0 = Mathf.Max(0, (int)(c.y - r - outline - 1)), y1 = Mathf.Min(h - 1, (int)(c.y + r + outline + 1));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
            int i = y * w + x;
            fill[i] = Mathf.Max(fill[i], Mathf.Clamp01(r - d + 0.5f));
            edge[i] = Mathf.Max(edge[i], Mathf.Clamp01(r + outline - d + 0.5f));
        }
    }

    // İşaretli uzaklık (içeride ≤ 0).
    private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(SegmentDistance(p, a, b), Mathf.Min(SegmentDistance(p, b, c), SegmentDistance(p, c, a)));
        bool inside = SameSide(p, a, b, c) && SameSide(p, b, c, a) && SameSide(p, c, a, b);
        return inside ? -d : d;
    }

    private static bool SameSide(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float cp1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        float cp2 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return cp1 * cp2 >= 0f;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
