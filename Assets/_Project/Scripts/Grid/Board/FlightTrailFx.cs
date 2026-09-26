using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uçan board görselleri için ortak "kayan yıldız" izi: hız yönünde uzayan kuyruk + yoldan hafif
/// sapıp sönen kıvılcımlar. Magnet special fırlatması ve KeyGenerator key uçuşu aynı görseli kullanır.
/// Kullanım: uçuş başında <see cref="Create"/>, her kare <see cref="Step"/>, bitince <see cref="Finish"/>.
/// Görseller ikonun ALTINDA kalsın diye kap, ikonun hemen önündeki sibling'e yerleşir.
/// </summary>
public sealed class FlightTrailFx
{
    private const float DotSpacingCells = 0.05f;
    private const float DotLifetime = 0.38f;

    private static Sprite glowSprite;

    private readonly MonoBehaviour host;
    private readonly RectTransform root;
    private readonly RectTransform tail;
    private readonly Image tailImage;
    private readonly Color tint;
    private readonly float tileSize;
    private Vector2 prevPos;
    private Vector2 lastDot;
    private bool started;

    private FlightTrailFx(MonoBehaviour host, RectTransform icon, Color tint, float tileSize)
    {
        this.host = host;
        this.tint = tint;
        this.tileSize = Mathf.Max(1f, tileSize);

        var parent = icon.parent as RectTransform;
        root = CreateImage(parent, "FlightTrailFx", null, Color.clear, 0f);
        if (root == null) return;
        root.GetComponent<Image>().enabled = false;
        root.SetSiblingIndex(icon.GetSiblingIndex());

        tail = CreateImage(root, "FlightTail", GlowSprite(), WithAlpha(TailColor, 0f), 0f);
        tail.pivot = new Vector2(1f, 0.5f);
        tail.sizeDelta = new Vector2(this.tileSize * 0.2f, this.tileSize * 0.42f);
        tailImage = tail.GetComponent<Image>();
    }

    public static FlightTrailFx Create(MonoBehaviour host, RectTransform icon, Color tint, float tileSize)
        => host != null && icon != null && icon.parent is RectTransform ? new FlightTrailFx(host, icon, tint, tileSize) : null;

    private Color TailColor => Color.Lerp(tint, Color.white, 0.35f);

    /// progress: 0 kalkış … 1 iniş (kuyruk inişe doğru kısalır ve söner).
    public void Step(Vector2 pos, float progress)
    {
        if (root == null) return;
        if (!started)
        {
            prevPos = lastDot = pos;
            started = true;
            return;
        }

        Vector2 velocity = pos - prevPos;
        if (tail != null && velocity.sqrMagnitude > 0.01f)
        {
            float speedCells = velocity.magnitude / tileSize / Mathf.Max(0.0001f, Time.deltaTime);
            float length = tileSize * Mathf.Clamp(speedCells * 0.22f, 0.5f, 2.6f) * Mathf.Lerp(1f, 0.4f, progress * progress);
            tail.anchoredPosition = pos;
            tail.sizeDelta = new Vector2(length, tileSize * 0.42f * Mathf.Lerp(1.15f, 0.75f, progress));
            tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            tailImage.color = WithAlpha(TailColor, Mathf.Lerp(0.9f, 0.35f, progress));
        }
        prevPos = pos;

        if ((pos - lastDot).magnitude >= tileSize * DotSpacingCells)
        {
            SpawnDot(pos + Random.insideUnitCircle * (tileSize * 0.08f));
            lastDot = pos;
        }
    }

    /// Kuyruk hemen kalkar; kıvılcımlar sönerek biter, kap sonra yok edilir.
    public void Finish()
    {
        if (tail != null) Object.Destroy(tail.gameObject);
        if (root != null) Object.Destroy(root.gameObject, DotLifetime + 0.05f);
    }

    private void SpawnDot(Vector2 pos)
    {
        var dot = CreateImage(root, "FlightTrailDot", GlowSprite(), WithAlpha(tint, 0.8f), tileSize * 0.42f);
        if (dot == null) return;
        dot.anchoredPosition = pos;
        if (host != null && host.isActiveAndEnabled)
            host.StartCoroutine(CoFadeDot(dot, tint));
        else
            Object.Destroy(dot.gameObject, DotLifetime);
    }

    private static IEnumerator CoFadeDot(RectTransform dot, Color tint)
    {
        var img = dot.GetComponent<Image>();
        float elapsed = 0f;
        while (elapsed < DotLifetime && dot != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / DotLifetime);
            dot.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, k);
            img.color = WithAlpha(Color.Lerp(tint, Color.white, k * 0.6f), Mathf.Lerp(0.8f, 0f, k));
            yield return null;
        }
        if (dot != null)
            Object.Destroy(dot.gameObject);
    }

    // Çalışma anında oluşan görsel: parent'ın layer'ını alır (UI kamerası layer 0'ı çizmez).
    public static RectTransform CreateImage(RectTransform parent, string name, Sprite sprite, Color color, float size)
    {
        if (parent == null) return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * size;
        return rt;
    }

    public static Sprite GlowSprite()
    {
        if (glowSprite != null)
            return glowSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / radius);
            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2.2f));
        }
        tex.SetPixels(pixels);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
