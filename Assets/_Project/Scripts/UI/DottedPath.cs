using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Waypoint'lerin arasından yumuşak bir eğri (Catmull-Rom) geçirip üzerine eşit aralıklı
/// nokta (dot) dizer — Candy Crush tarzı noktalı yol. S şeklini waypoint'leri yerleştirerek verirsin.
///
/// Kullanım: Content altında boş bir obje (DottedPath) + altına waypoint olarak boş RectTransform'lar
/// koy, sahnede sürükleyerek S/zikzak yap. dotSprite ata, Rebuild. Editörde anında görünür.
/// Yol haritayla birlikte kayar (Content çocuğu olduğu için).
/// </summary>
[ExecuteAlways]
public sealed class DottedPath : MonoBehaviour
{
    [Header("Yol Noktaları (sırayla — S için zikzak yerleştir)")]
    [Tooltip("Eğrinin geçeceği noktalar. Sahnede sürükleyip şekli sen verirsin. En az 2 tane.")]
    [SerializeField] private List<RectTransform> waypoints = new();

    [Header("Nokta (dot) görünümü")]
    [Tooltip("Noktanın sprite'ı (yuvarlak nokta PNG). Yoksa düz beyaz daire çizilir.")]
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private Color dotColor = Color.white;
    [Tooltip("Noktalar arası mesafe (piksel). Küçük = sık noktalar.")]
    [SerializeField, Min(4f)] private float spacing = 48f;
    [SerializeField, Min(2f)] private float dotSize = 16f;

    [Header("Eğri kalitesi")]
    [Tooltip("Her segmentte örnekleme sayısı. Yüksek = daha pürüzsüz eğri.")]
    [SerializeField, Range(4, 64)] private int samplesPerSegment = 24;

    [Header("Reveal (opsiyonel)")]
    [Tooltip("Yolun ne kadarı görünsün (0-1). İlerleme açıldıkça artırabilirsin.")]
    [SerializeField, Range(0f, 1f)] private float revealFraction = 1f;

    private readonly List<RectTransform> dots = new();

    private void OnEnable() => Rebuild();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Editörde değer değişince yeniden çiz (bir frame sonra — Destroy güvenliği).
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Rebuild();
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        ClearDots();
        if (dotSprite == null && !Application.isPlaying) { /* sprite yoksa yine daire spawn ederiz */ }

        var pts = BuildPolyline();
        if (pts.Count < 2) return;

        // Toplam uzunluk + eşit aralıkla nokta yerleştir.
        float total = 0f;
        for (int i = 1; i < pts.Count; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
        if (total < 0.01f) return;

        int dotCount = Mathf.Max(2, Mathf.FloorToInt(total / spacing));
        float step = total / dotCount;

        float walked = 0f;
        int seg = 1;
        float segStart = 0f;
        for (int d = 0; d <= dotCount; d++)
        {
            float targetDist = d * step;
            while (seg < pts.Count)
            {
                float segLen = Vector3.Distance(pts[seg - 1], pts[seg]);
                if (segStart + segLen >= targetDist || seg == pts.Count - 1)
                {
                    float f = segLen > 0.0001f ? (targetDist - segStart) / segLen : 0f;
                    Vector3 pos = Vector3.Lerp(pts[seg - 1], pts[seg], Mathf.Clamp01(f));
                    SpawnDot(pos);
                    break;
                }
                segStart += segLen;
                seg++;
            }
            walked = targetDist;
        }

        ApplyReveal();
    }

    /// <summary>Yolun görünür kısmını ayarla (0-1). İlerleme açıldıkça çağır.</summary>
    public void SetReveal(float fraction)
    {
        revealFraction = Mathf.Clamp01(fraction);
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        int visible = Mathf.CeilToInt(dots.Count * revealFraction);
        for (int i = 0; i < dots.Count; i++)
            if (dots[i] != null) dots[i].gameObject.SetActive(i < visible);
    }

    // ─── Eğri ──────────────────────────────────────────────────────────────────

    private List<Vector3> BuildPolyline()
    {
        var result = new List<Vector3>();
        var w = new List<Vector3>();
        for (int i = 0; i < waypoints.Count; i++)
            if (waypoints[i] != null) w.Add(waypoints[i].position);   // world space (anchor/pivot derdi yok)

        if (w.Count < 2) return result;
        if (w.Count == 2)   // iki nokta → düz çizgi
        {
            result.Add(w[0]); result.Add(w[1]); return result;
        }

        // Catmull-Rom: her segment için komşu noktalarla pürüzsüz geçiş.
        for (int i = 0; i < w.Count - 1; i++)
        {
            Vector3 p0 = w[Mathf.Max(i - 1, 0)];
            Vector3 p1 = w[i];
            Vector3 p2 = w[i + 1];
            Vector3 p3 = w[Mathf.Min(i + 2, w.Count - 1)];

            int samples = (i == w.Count - 2) ? samplesPerSegment : samplesPerSegment;
            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)samples;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        result.Add(w[w.Count - 1]);
        return result;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // ─── Dot spawn ───────────────────────────────────────────────────────────────

    private void SpawnDot(Vector3 worldPos)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.hideFlags = HideFlags.DontSave;   // yolu kaydetme, runtime/edit'te üretilir
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * dotSize;
        rt.position = worldPos;

        var img = go.GetComponent<Image>();
        img.sprite = dotSprite;
        img.color = dotColor;
        img.raycastTarget = false;
        if (dotSprite == null) img.type = Image.Type.Simple; // beyaz kare/daire fallback

        dots.Add(rt);
    }

    private void ClearDots()
    {
        // Mevcut üretilmiş noktaları temizle (edit + play güvenli).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c == null) continue;
            // Waypoint'lere dokunma; sadece bizim ürettiğimiz "Dot"ları sil.
            if (c.name != "Dot") continue;
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        dots.Clear();
    }
}
