using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Engel kırılınca kendi son görselini kare parçalara bölüp savurur (Brandalı heykel finalindeki parçalanmanın genel
/// hâli). Parça boyu engelin boyuna oranlı: 1x1 → 3x3 parça, 2x2 → 3x3, 3x3 → 4x4, 4x4 → 5x5 (büyük engel büyük parça);
/// 2x2 ve üstünde her parça ızgara hücresinin %150'si kadar bölge keser (kullanıcı: "büyüklerde %50 büyük"). Parçalar
/// kendi gerçek konumlarında başladığından ilk karede bindirmeler aynı görüntüdür — dikiş/çiftlenme görünmez.
/// Parçalar dışa + yukarı fırlar, döner, yerçekimiyle düşer, küçülerek söner; ortada kısa bir parlama.
/// Performans: parça Image'ları havuzdan (kırılma dalgasında yüzlerce nesne yaratılıp silinmez), kesilen alt-sprite'lar
/// (sprite, ızgara) başına önbellekte, her kırılma tek coroutine. Sprite'ın şeffaf köşelerinden kesilen parça görünmez.
/// </summary>
public static class ObstacleShatterFx
{
    private const float Life = 0.8f;
    private const float FlashTime = 0.22f;
    private const float PieceOverlap = 1.12f;   // 1x1: komşu parçalar hafif bindirir, ilk karede dikiş görünmez
    private const float LargePieceScale = 1.5f; // 2x2+: parça ızgara hücresinin 1.5 katı

    private static readonly Stack<Image> pool = new();
    private static readonly Dictionary<(Sprite, int), Sprite[]> cutCache = new();   // anahtar: (sprite, grid·1000 + frac·1000)
    private static readonly Vector3[] corners = new Vector3[4];

    /// source: engelin tahtadaki Image'ı (çağıran hemen ardından yok edebilir — sprite + konum burada yakalanır).
    public static void Play(MonoBehaviour host, RectTransform root, Image source, int footprintCells)
    {
        if (host == null || root == null || source == null || source.sprite == null) return;
        int grid = Mathf.Clamp(footprintCells <= 1 ? 3 : footprintCells + 1, 3, 5);
        float pieceFrac = (footprintCells >= 2 ? LargePieceScale : PieceOverlap) / grid;   // parça kenarı / engel kenarı
        var pieces = Cut(source.sprite, grid, pieceFrac);
        if (pieces == null) return;

        source.rectTransform.GetWorldCorners(corners);
        Vector2 bl = root.InverseTransformPoint(corners[0]);
        Vector2 tr = root.InverseTransformPoint(corners[2]);
        host.StartCoroutine(Run(root, pieces, grid, pieceFrac, bl, tr - bl, source.color));
    }

    private static IEnumerator Run(RectTransform root, Sprite[] pieces, int grid, float pieceFrac, Vector2 bl, Vector2 size,
        Color tint)
    {
        int n = pieces.Length;
        var imgs = new Image[n];
        var vel = new Vector2[n];
        var spin = new float[n];
        float span = Mathf.Max(size.x, size.y);
        Vector2 center = bl + size * 0.5f;
        Vector2 pieceSize = size * pieceFrac;

        for (int i = 0; i < n; i++)
        {
            int gx = i % grid, gy = i / grid;
            var img = Acquire(root);
            img.sprite = pieces[i];
            img.color = tint;
            var rt = img.rectTransform;
            rt.sizeDelta = pieceSize;
            // InverseTransformPoint kökün pivotuna göre konum verir → localPosition (anchoredPosition değil).
            rt.localPosition = bl + new Vector2((gx + 0.5f) * size.x / grid, (gy + 0.5f) * size.y / grid);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
            rt.localScale = Vector3.one;
            Vector2 off = (Vector2)rt.localPosition - center;
            Vector2 dir = (off.sqrMagnitude > 1f ? off.normalized : Random.insideUnitCircle.normalized)
                          + Random.insideUnitCircle * 0.3f;
            vel[i] = (dir.normalized + Vector2.up * 0.6f) * span * Random.Range(1.1f, 1.9f);
            spin[i] = Random.Range(-480f, 480f);
            imgs[i] = img;
        }

        // Kısa parlama (parçaların altında), engel boyunda.
        var flash = Acquire(root);
        flash.sprite = FlightTrailFx.GlowSprite();
        flash.rectTransform.sizeDelta = Vector2.one * span * 1.3f;
        flash.rectTransform.localPosition = center;
        flash.rectTransform.localRotation = Quaternion.identity;
        flash.rectTransform.SetSiblingIndex(Mathf.Max(0, imgs[0].rectTransform.GetSiblingIndex()));

        float gravity = span * 4.5f;
        for (float t = 0f; t < Life; t += Time.deltaTime)
        {
            float dt = Time.deltaTime, k = t / Life;
            float fade = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            float f = Mathf.Clamp01(t / FlashTime);
            if (flash != null)
            {
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, 1f - (1f - f) * (1f - f));
                flash.color = new Color(1f, 1f, 1f, (1f - f) * 0.8f);
            }
            for (int i = 0; i < n; i++)
            {
                var img = imgs[i];
                if (img == null) continue;
                vel[i].y -= gravity * dt;
                vel[i] *= 1f - 0.5f * dt;
                var rt = img.rectTransform;
                rt.localPosition += (Vector3)(vel[i] * dt);
                rt.localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
                var c = tint;
                c.a *= fade;
                img.color = c;
            }
            yield return null;
        }

        for (int i = 0; i < n; i++) Release(imgs[i]);
        Release(flash);
    }

    // Sprite'ı grid×grid parçaya böler (UI y yukarı: indeks gy=0 alt sıra). Her parça kendi ızgara hücresinin merkezinde,
    // kenarı frac × doku kenarı (ızgara hücresinden büyükse komşularla bindirir; kenarda doku içine kaydırılır).
    // Sonuç (sprite, grid, frac) başına önbellekte.
    private static Sprite[] Cut(Sprite source, int grid, float frac)
    {
        var key = (source, grid * 1000 + Mathf.RoundToInt(frac * 1000f));
        if (cutCache.TryGetValue(key, out var cached)) return cached;
        if (source.texture == null) return null;
        var r = source.textureRect;
        float w = Mathf.Min(r.width, r.width * frac), h = Mathf.Min(r.height, r.height * frac);
        var result = new Sprite[grid * grid];
        for (int gy = 0; gy < grid; gy++)
            for (int gx = 0; gx < grid; gx++)
            {
                float x = Mathf.Clamp(r.x + (gx + 0.5f) * r.width / grid - w * 0.5f, r.x, r.xMax - w);
                float y = Mathf.Clamp(r.y + (gy + 0.5f) * r.height / grid - h * 0.5f, r.y, r.yMax - h);
                result[gy * grid + gx] = Sprite.Create(source.texture, new Rect(x, y, w, h),
                    new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            }
        cutCache[key] = result;
        return result;
    }

    private static Image Acquire(RectTransform root)
    {
        while (pool.Count > 0)
        {
            var img = pool.Pop();
            if (img == null) continue;   // sahne değişiminde yok olmuş
            var rt = img.rectTransform;
            if (rt.parent != root) rt.SetParent(root, false);
            img.gameObject.layer = root.gameObject.layer;
            rt.SetAsLastSibling();
            img.gameObject.SetActive(true);
            return img;
        }
        var go = new GameObject("ObstacleShard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = root.gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera çizmez
        var nrt = (RectTransform)go.transform;
        nrt.SetParent(root, false);
        nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static void Release(Image img)
    {
        if (img == null) return;
        img.gameObject.SetActive(false);
        pool.Push(img);
    }
}
