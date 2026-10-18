using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wonder Boya Kovası: dokunulan taştan her hedef taşa giden boya kuşağı. Kuyruk dokunulan taşın renginde, baş hedefin
/// YENİ renginde (renk geçişi şeridin köşe renkleriyle — RibbonGradient). Kuşağın başı hafif kavisli bir yolda
/// hedefe akar, kuyruğu arkadan gelir (uzayıp kısalan şerit); baş hedefe varınca onArrive çağrılır ve küçük bir parlama
/// oynar. Performans: Image'lar havuzdan, tüm kuşaklar TEK coroutine'de güncellenir, gökkuşağı dokusu bir kez üretilir.
/// </summary>
public static class PaintRibbonFx
{
    private const float Travel = 0.34f;        // başın kaynaktan hedefe süresi
    private const float TailLag = 0.13f;       // kuyruk gecikmesi (şerit boyu)
    private const float FlashTime = 0.22f;
    private const float Thickness = 0.30f;     // hücre kenarına oranla şerit kalınlığı

    private sealed class Ribbon
    {
        public Image strip, flash;
        public Vector2 from, to, ctrl;
        public float start;
        public Action onArrive;
        public bool arrived;
        public Color flashTint = Color.white;
    }

    private static readonly Stack<Image> pool = new();
    private static readonly List<Ribbon> active = new();
    private static Sprite stripSprite;
    private static Coroutine loop;
    private static MonoBehaviour loopHost;

    /// Bir kuşak başlatır. from/to: root yerel koordinatı. delay: kaynağa uzaklığa göre dalga gecikmesi.
    public static void Launch(MonoBehaviour host, RectTransform root, Vector2 from, Vector2 to, float cellSize,
        float delay, Color fromColor, Color toColor, Action onArrive)
    {
        if (host == null || root == null) { onArrive?.Invoke(); return; }

        var r = new Ribbon
        {
            from = from, to = to, start = Time.time + Mathf.Max(0f, delay), onArrive = onArrive,
            strip = Acquire(root), flash = Acquire(root),
        };
        // Kavis: kirişe dik, uzunluğun %18'i; yön sırayla değişir (kuşaklar birbirine yapışmasın).
        Vector2 d = to - from;
        Vector2 n = new Vector2(-d.y, d.x).normalized * (active.Count % 2 == 0 ? 1f : -1f);
        r.ctrl = (from + to) * 0.5f + n * d.magnitude * 0.18f;

        r.strip.sprite = Strip();
        if (!r.strip.TryGetComponent(out RibbonGradient grad)) grad = r.strip.gameObject.AddComponent<RibbonGradient>();
        grad.Set(fromColor, toColor);
        r.strip.type = Image.Type.Simple;
        r.strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        r.strip.rectTransform.sizeDelta = new Vector2(0f, cellSize * Thickness);
        r.strip.color = new Color(1f, 1f, 1f, 0f);

        r.flash.sprite = FlightTrailFx.GlowSprite();
        r.flash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        r.flash.rectTransform.sizeDelta = Vector2.one * cellSize * 1.25f;
        r.flash.rectTransform.localPosition = to;
        r.flash.color = new Color(toColor.r, toColor.g, toColor.b, 0f);
        r.flashTint = toColor;

        active.Add(r);
        if (loop == null || loopHost == null)
        {
            loopHost = host;
            loop = host.StartCoroutine(Run());
        }
    }

    public static bool IsPlaying => active.Count > 0;

    /// Sahne/level sıfırlanırken: bekleyen varışlar anında uygulanır (taş türü yarım kalmasın), görseller havuza döner.
    public static void FinishAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var r = active[i];
            if (!r.arrived) { r.arrived = true; r.onArrive?.Invoke(); }
            Release(r.strip); Release(r.flash);
        }
        active.Clear();
        if (loop != null && loopHost != null) loopHost.StopCoroutine(loop);
        loop = null; loopHost = null;
    }

    private static IEnumerator Run()
    {
        while (active.Count > 0)
        {
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var r = active[i];
                if (r.strip == null || r.flash == null)
                {
                    // Kök yok oldu (sahne değişimi): varışı uygula, kaydı at.
                    if (!r.arrived) { r.arrived = true; r.onArrive?.Invoke(); }
                    active.RemoveAt(i);
                    continue;
                }

                float t = (now - r.start) / Travel;
                if (t < 0f) continue;

                float head = Mathf.Clamp01(t);
                float tail = Mathf.Clamp01((now - r.start - TailLag) / Travel);
                Vector2 hp = Bezier(r, EaseOut(head)), tp = Bezier(r, EaseOut(tail));
                Vector2 seg = hp - tp;
                var rt = r.strip.rectTransform;
                rt.localPosition = tp;
                rt.sizeDelta = new Vector2(seg.magnitude, rt.sizeDelta.y);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg);
                r.strip.color = new Color(1f, 1f, 1f, tail >= 1f ? 0f : 0.95f);

                if (!r.arrived && head >= 1f)
                {
                    r.arrived = true;
                    r.onArrive?.Invoke();
                }

                if (r.arrived)
                {
                    float f = Mathf.Clamp01((now - r.start - Travel) / FlashTime);
                    r.flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.1f, 1f - (1f - f) * (1f - f));
                    var fc = Color.Lerp(r.flashTint, Color.white, 0.35f);
                    r.flash.color = new Color(fc.r, fc.g, fc.b, (1f - f) * 0.9f);
                    if (f >= 1f && tail >= 1f)
                    {
                        Release(r.strip); Release(r.flash);
                        active.RemoveAt(i);
                    }
                }
            }
            yield return null;
        }
        loop = null; loopHost = null;
    }

    private static Vector2 Bezier(Ribbon r, float t)
    {
        float u = 1f - t;
        return u * u * r.from + 2f * u * t * r.ctrl + t * t * r.to;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    // Beyaz şerit: kalınlıkta yumuşak kenar + üstte parlak çizgi, iki ucu sivri. Renk köşe renklerinden gelir.
    private static Sprite Strip()
    {
        if (stripSprite != null) return stripSprite;
        const int w = 128, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float u = x / (w - 1f);
            float taper = Mathf.Clamp01(Mathf.Min(u / 0.18f, (1f - u) / 0.12f));
            for (int y = 0; y < h; y++)
            {
                float yn = y / (h - 1f);
                float v = Mathf.Abs(yn * 2f - 1f);                                   // 0 orta, 1 kenar
                float band = Mathf.Clamp01((taper - v) / 0.25f);
                float shade = Mathf.Lerp(0.78f, 1f, yn);                              // alt hafif koyu → hacim
                float gloss = Mathf.Clamp01(1f - Mathf.Abs(yn - 0.68f) * 6f) * 0.6f;  // üstte parlak çizgi
                float c = Mathf.Min(1f, shade + gloss);
                px[y * w + x] = new Color(c, c, c, band);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        stripSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 100f);
        stripSprite.name = "PaintRibbonStrip";
        return stripSprite;
    }

    private static Image Acquire(RectTransform root)
    {
        while (pool.Count > 0)
        {
            var img = pool.Pop();
            if (img == null) continue;
            var t = img.rectTransform;
            if (t.parent != root) t.SetParent(root, false);
            img.gameObject.layer = root.gameObject.layer;
            t.SetAsLastSibling();
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
            img.gameObject.SetActive(true);
            return img;
        }
        var go = new GameObject("PaintRibbon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera çizmez
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Release(Image img)
    {
        if (img == null) return;
        img.gameObject.SetActive(false);
        pool.Push(img);
    }
}

/// Şeridin sol (kuyruk) ve sağ (baş) köşelerine renk verir → boya kaynak renginden hedef rengine akar.
[DisallowMultipleComponent]
public sealed class RibbonGradient : BaseMeshEffect
{
    private Color tail = Color.white, head = Color.white;

    public void Set(Color tailColor, Color headColor)
    {
        tail = tailColor; head = headColor;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var rect = graphic.rectTransform.rect;
        float w = Mathf.Max(0.0001f, rect.width);
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            float u = Mathf.Clamp01((v.position.x - rect.xMin) / w);
            Color c = Color.Lerp(tail, head, u);
            c.a *= v.color.a / 255f;
            v.color = c;
            vh.SetUIVertex(v, i);
        }
    }
}
