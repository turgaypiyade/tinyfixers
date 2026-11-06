using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Engel kırılınca kendi son görselinden kesilen 2-3 BÜYÜK parçaya ayrılıp savrulur (kullanıcı kuralı 2026-10-31: bütün
/// kırılmalarda parçalar 2-3 tane ve orijinal resimden büyük parçalar). 1x1 → 2 parça, 2x2 ve üstü → 3 parça. Bölme
/// yönü her kırılmada rastgele (dikey/yatay; 3 parçada bir yarı ikiye ayrılır) → kırılmalar tekdüze görünmez.
/// Parçalar kendi gerçek konumlarında başlar ve hafif bindirir; ilk karede görüntü engelin aynısıdır (dikiş görünmez).
/// Parçalar bölme çizgisinden dışa + yukarı fırlar, yavaş döner, yerçekimiyle düşer, küçülerek söner; ortada kısa parlama.
/// Performans: parça Image'ları havuzdan (kırılma dalgasında nesne yaratılıp silinmez), kesilen alt-sprite'lar
/// (sprite, yerleşim) başına önbellekte, her kırılma tek coroutine.
/// </summary>
public static class ObstacleShatterFx
{
    private const float Life = 0.8f;
    private const float FlashTime = 0.22f;
    private const float Overlap = 1.04f;   // komşu parçalar hafif bindirir, ilk karede dikiş görünmez

    // Yerleşimler: normalize dikdörtgenler (x, y, w, h; UI y yukarı). 0-1: iki parça, 2-5: üç parça.
    private static readonly Rect[][] Layouts =
    {
        new[] { new Rect(0f, 0f, 0.5f, 1f), new Rect(0.5f, 0f, 0.5f, 1f) },                                   // dikey bölme
        new[] { new Rect(0f, 0f, 1f, 0.5f), new Rect(0f, 0.5f, 1f, 0.5f) },                                   // yatay bölme
        new[] { new Rect(0f, 0f, 0.5f, 1f), new Rect(0.5f, 0f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f) }, // sol yarı + sağ ikiye
        new[] { new Rect(0.5f, 0f, 0.5f, 1f), new Rect(0f, 0f, 0.5f, 0.5f), new Rect(0f, 0.5f, 0.5f, 0.5f) },   // sağ yarı + sol ikiye
        new[] { new Rect(0f, 0.5f, 1f, 0.5f), new Rect(0f, 0f, 0.5f, 0.5f), new Rect(0.5f, 0f, 0.5f, 0.5f) },   // üst yarı + alt ikiye
        new[] { new Rect(0f, 0f, 1f, 0.5f), new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f) }, // alt yarı + üst ikiye
    };

    private static readonly Stack<Image> pool = new();
    private static readonly Dictionary<(Sprite, int), Sprite[]> cutCache = new();   // anahtar: (sprite, yerleşim)
    private static readonly Vector3[] corners = new Vector3[4];

    /// source: engelin tahtadaki Image'ı (çağıran hemen ardından yok edebilir — sprite + konum burada yakalanır).
    public static void Play(BoardController board, MonoBehaviour host, RectTransform root, Image source, int footprintCells)
    {
        if (host == null || root == null || source == null || source.sprite == null) return;
        int layout = footprintCells <= 1 ? Random.Range(0, 2) : Random.Range(2, Layouts.Length);
        var pieces = Cut(source.sprite, layout);
        if (pieces == null) return;

        source.rectTransform.GetWorldCorners(corners);
        Vector2 bl = root.InverseTransformPoint(corners[0]);
        Vector2 tr = root.InverseTransformPoint(corners[2]);
        host.StartCoroutine(Run(root, pieces, Layouts[layout], bl, tr - bl, source.color, ObstacleDebrisBounds.For(board, root)));
    }

    private static IEnumerator Run(RectTransform root, Sprite[] pieces, Rect[] layout, Vector2 bl, Vector2 size,
        Color tint, ObstacleDebrisBounds bounds)
    {
        int n = pieces.Length;
        var imgs = new Image[n];
        var vel = new Vector2[n];
        var spin = new float[n];
        float span = Mathf.Max(size.x, size.y);
        Vector2 center = bl + size * 0.5f;

        Image flash = null;
        try
        {
            for (int i = 0; i < n; i++)
            {
                var r = layout[i];
                var img = Acquire(root);
                imgs[i] = img;
                img.sprite = pieces[i];
                img.color = tint;
                var rt = img.rectTransform;
                Vector2 pieceSize = new Vector2(r.width * size.x, r.height * size.y) * Overlap;
                // InverseTransformPoint kökün pivotuna göre konum verir → localPosition (anchoredPosition değil).
                rt.localPosition = bl + new Vector2(r.center.x * size.x, r.center.y * size.y);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                Vector2 off = (Vector2)rt.localPosition - center;
                Vector2 dir = (off.sqrMagnitude > 1f ? off.normalized : Random.insideUnitCircle.normalized)
                              + Random.insideUnitCircle * 0.2f;
                vel[i] = (dir.normalized + Vector2.up * 0.7f) * span * Random.Range(1.0f, 1.5f);
                Vector2 start = rt.localPosition;
                Vector2 fittedSize = pieceSize;
                bounds.Fit(ref start, ref vel[i], ref fittedSize, Life);
                rt.localPosition = start;
                rt.sizeDelta = fittedSize;
                // Büyük parçalar ağır döner: dışa doğru, bölme çizgisinden uzaklaşan yönde.
                spin[i] = (off.x >= 0f ? -1f : 1f) * Random.Range(90f, 220f);
            }

            // Kısa parlama (parçaların altında), engel boyunda.
            flash = Acquire(root);
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
                    rt.localPosition = bounds.ClampCenter(rt.localPosition, rt.sizeDelta);
                    rt.localRotation *= Quaternion.Euler(0f, 0f, spin[i] * dt);
                    rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, k);
                    var c = tint;
                    c.a *= fade;
                    img.color = c;
                }
                yield return null;
            }

        }
        finally
        {
            // Return already rented pieces on interruption as well as normal completion.
            for (int i = 0; i < n; i++) Release(imgs[i]);
            Release(flash);
        }
    }

    // Sprite'ı yerleşimdeki dikdörtgenlere böler (doku koordinatı; UI ile aynı yön: y yukarı). Parçalar Overlap kadar
    // büyütülür, kenarda doku içine kaydırılır. Sonuç (sprite, yerleşim) başına önbellekte.
    private static Sprite[] Cut(Sprite source, int layoutIndex)
    {
        var key = (source, layoutIndex);
        if (cutCache.TryGetValue(key, out var cached)) return cached;
        if (source.texture == null) return null;
        var tex = source.textureRect;
        var layout = Layouts[layoutIndex];
        var result = new Sprite[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            var r = layout[i];
            float w = Mathf.Min(tex.width, r.width * tex.width * Overlap);
            float h = Mathf.Min(tex.height, r.height * tex.height * Overlap);
            float cx = tex.x + r.center.x * tex.width, cy = tex.y + r.center.y * tex.height;
            float x = Mathf.Clamp(cx - w * 0.5f, tex.x, tex.xMax - w);
            float y = Mathf.Clamp(cy - h * 0.5f, tex.y, tex.yMax - h);
            result[i] = Sprite.Create(source.texture, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
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
