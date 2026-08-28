using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RocketBasket roketlerinin uçuş görseli — havada bir "yarım ay" (yarım daire) çizerek süzülüş:
///   • Kaynaktan bir AÇIYLA yükselir, tepede yarım daire çizip hedefe iner (S DEĞİL, tek kavis).
///   • Yay src↔tgt'yi çap kabul eden bir daire üzerinde; bulgeScale ile daha yüksek/oval yapılır.
///   • Ölçek tepe anında büyüyüp (1 → peakScale) inişte 1'e döner.
///   • Roket burnu daima gittiği yöne (tanjant) döner — tek sprite yeter, alev arkada kalır.
/// BoardController GameObject'ine component olarak eklenir. Hedefleme/impact PatchBot ile aynı.
/// </summary>
public sealed class RocketProjectileFlight : MonoBehaviour
{
    [SerializeField] private BoardController board;

    [Header("Boyut")]
    [Tooltip("Roket temel (scale=1) boyutu — tile oranı.")]
    [Range(0.3f, 1.8f)] [SerializeField] private float rocketSizeRatio = 0.95f;
    [Tooltip("Tepe anında ulaşılan ölçek (1 → peakScale → 1).")]
    [Range(1f, 3f)] [SerializeField] private float peakScale = 1.6f;

    [Header("Duman izi")]
    [SerializeField] private bool smokeTrailEnabled = true;
    [SerializeField] private Sprite smokeSprite;
    [SerializeField, Range(4f, 45f)] private float smokePuffsPerSecond = 20f;
    [SerializeField, Range(0.15f, 1.2f)] private float smokeSizeRatio = 0.52f;
    [SerializeField, Min(0.05f)] private float smokeLife = 0.42f;
    [SerializeField] private Color smokeColor = new Color(0.92f, 0.94f, 0.98f, 0.58f);

    [Header("Çarpma patlaması (impact explosion)")]
    [Tooltip("Roket hedefe varınca oynatılan patlama sprite'ı (sarı yıldız + glow). Boşsa patlama " +
             "çizilmez (yalnız tile-clear burst'ü kalır). Proje: Art/Icons/FX/vfx_pulsecore_coreburst.")]
    [SerializeField] private Sprite impactExplosionSprite;
    [Tooltip("Patlamanın tile'a göre TEPE boyutu (1 = tam hücre). Roket hafif fazla taşsın diye >1.")]
    [Range(0.6f, 3f)] [SerializeField] private float impactExplosionSizeRatio = 1.7f;
    [Tooltip("Patlama süresi (sn).")]
    [Min(0.05f)] [SerializeField] private float impactExplosionDuration = 0.34f;
    [Tooltip("Patlama başlangıç ölçeği (tepe boyutun oranı). Küçükten hızla açılır.")]
    [Range(0.05f, 1f)] [SerializeField] private float impactExplosionStartScale = 0.45f;

    [Header("Yarım daire yayı")]
    [Tooltip("Yayın yüksekliği. 1 = tam yarım daire; >1 daha yüksek/oval yarım ay; <1 daha basık.")]
    [Range(0.4f, 2.5f)] [SerializeField] private float bulgeScale = 1.4f;

    [Header("Süre")]
    [Tooltip("Temel uçuş süresi (sn).")]
    [SerializeField] private float baseDuration = 0.6f;
    [Tooltip("Mesafeye göre eklenen süre (sn / tile).")]
    [SerializeField] private float durationPerTile = 0.03f;

    [Header("Yön")]
    [Tooltip("Sprite'ın burnu +Y (yukarı) bakıyorsa 0. Sağa bakıyorsa -90.")]
    [SerializeField] private float noseOffsetDeg = 0f;

    private BoardController Board => board != null ? board : (board = GetComponent<BoardController>());
    private static Sprite fallbackSmokeSprite;

    public IEnumerator Fly(Vector2Int from, Vector2Int to, Sprite rocketSprite, Action onArrived)
    {
        var b = Board;
        // Obstacle'ların ÜSTÜNDE uçsun: PatchBot dash'iyle aynı VFX katmanı (yoksa board parent'ı).
        var flightRoot = (b != null && b.BoardVfxPlayer != null && b.BoardVfxPlayer.VfxRoot != null)
            ? b.BoardVfxPlayer.VfxRoot
            : (b != null ? b.Parent : null);
        if (b == null || flightRoot == null || rocketSprite == null)
        {
            onArrived?.Invoke();
            yield break;
        }

        Vector2 src = CellAnchored(b, from, flightRoot);
        Vector2 tgt = CellAnchored(b, to, flightRoot);
        // Flight root local uzayındaki gerçek tile boyutu (VfxRoot ölçeği farklı olabilir).
        float ts = Vector2.Distance(src, CellAnchored(b, from + Vector2Int.right, flightRoot));
        if (ts < 1f) ts = Mathf.Max(1f, b.TileSize);

        // src↔tgt'yi çap kabul eden dairesel yay. along = merkez→src; perp = yukarı kabaran dik.
        Vector2 mid = (src + tgt) * 0.5f;
        Vector2 along = src - mid;
        Vector2 perp = new Vector2(-along.y, along.x);
        if (perp.y < 0f) perp = -perp;                                  // yay yukarı kabarsın
        Vector2 perpUnit = perp.sqrMagnitude > 0.0001f ? perp.normalized : Vector2.up;
        float radius = along.magnitude;

        Vector2 PathPoint(float t)
        {
            float ang = Mathf.PI * t;                                   // 0 → π : src → tgt
            return mid
                 + Mathf.Cos(ang) * along
                 + Mathf.Sin(ang) * perpUnit * (radius * bulgeScale);
        }

        var go = new GameObject("Rocket", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flightRoot, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float size = ts * rocketSizeRatio;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = src;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = rocketSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        float dur = Mathf.Max(0.15f, baseDuration + durationPerTile * (radius * 2f / Mathf.Max(1f, ts)));
        float smokeInterval = smokePuffsPerSecond > 0f ? 1f / smokePuffsPerSecond : float.MaxValue;
        float smokeAcc = smokeInterval;

        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector2 pos = PathPoint(k);
            rt.anchoredPosition = pos;

            float s = 1f + (peakScale - 1f) * Mathf.Sin(Mathf.PI * k);
            rt.localScale = new Vector3(s, s, 1f);

            Vector2 next = PathPoint(Mathf.Min(1f, k + 0.02f));
            Vector2 dir = next - pos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + noseOffsetDeg;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            }

            if (smokeTrailEnabled)
            {
                smokeAcc += Time.deltaTime;
                while (smokeAcc >= smokeInterval)
                {
                    smokeAcc -= smokeInterval;
                    Vector2 backDir = dir.sqrMagnitude > 0.0001f ? -dir.normalized : Vector2.down;
                    SpawnSmokePuff(flightRoot, go.transform.GetSiblingIndex(), pos + backDir * (size * 0.32f), ts, s);
                }
            }

            yield return null;
        }

        rt.anchoredPosition = tgt;
        rt.localScale = Vector3.one;
        Destroy(go);

        // Çarpma anında hedefte tek, tatmin edici bir patlama (sprite tabanlı — procedural
        // yıldız yerine). Uçuş root'unun en üstünde, kendi ömrünce oynayıp yok olur.
        SpawnImpactExplosion(flightRoot, tgt, ts);

        onArrived?.Invoke();
    }

    // Hedefte sprite tabanlı patlama: küçükten hızla açılır, hafif döner ve solarak kaybolur.
    private void SpawnImpactExplosion(RectTransform parent, Vector2 pos, float tileSize)
    {
        if (parent == null || impactExplosionSprite == null || impactExplosionDuration <= 0f)
            return;

        var go = new GameObject("RocketImpactExplosion", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        float peak = tileSize * Mathf.Max(0.1f, impactExplosionSizeRatio);
        rt.sizeDelta = new Vector2(peak, peak);
        rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

        var img = go.GetComponent<Image>();
        img.sprite = impactExplosionSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        StartCoroutine(CoImpactExplosion(go, rt, img));
    }

    private IEnumerator CoImpactExplosion(GameObject go, RectTransform rt, Image img)
    {
        if (go == null || rt == null || img == null)
            yield break;

        float startScale = Mathf.Clamp(impactExplosionStartScale, 0.05f, 1f);
        float spin = UnityEngine.Random.Range(-40f, 40f);
        float baseRot = rt.localEulerAngles.z;

        float elapsed = 0f;
        while (elapsed < impactExplosionDuration)
        {
            if (go == null) yield break;
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / impactExplosionDuration);

            // Ölçek: küçükten hızla açıl (easeOut), sonuna doğru hafif taşmayı sürdür.
            float grow = 1f - (1f - k) * (1f - k);            // easeOutQuad
            float scale = Mathf.Lerp(startScale, 1.08f, grow);
            rt.localScale = new Vector3(scale, scale, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, baseRot + spin * k);

            // Alpha: ilk %20'de parla, sonra solarak kaybol.
            float a = k < 0.2f ? (k / 0.2f) : (1f - (k - 0.2f) / 0.8f);
            var c = img.color;
            c.a = Mathf.Clamp01(a);
            img.color = c;

            yield return null;
        }

        Destroy(go);
    }

    // Hücre merkezini, verilen flight root'un local anchored uzayına çevirir (PatchBot dash ile
    // aynı yöntem) — böylece roket obstacle katmanının ÜSTÜNDEKİ VFX root'ta doğru konumda uçar.
    private static Vector2 CellAnchored(BoardController b, Vector2Int cell, RectTransform space)
    {
        Vector3 worldPos = b.GetCellWorldCenterPosition(cell.x, cell.y);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }

    private void SpawnSmokePuff(RectTransform parent, int rocketSiblingIndex, Vector2 pos, float tileSize, float rocketScale)
    {
        if (parent == null || smokeLife <= 0f) return;

        var go = new GameObject("RocketSmokePuff", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(Mathf.Max(0, rocketSiblingIndex));

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        float size = tileSize * smokeSizeRatio * Mathf.Lerp(0.9f, 1.25f, Mathf.Clamp01((rocketScale - 1f) / Mathf.Max(0.0001f, peakScale - 1f)));
        rt.sizeDelta = new Vector2(size, size);
        rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        rt.localScale = Vector3.one * UnityEngine.Random.Range(0.78f, 1.08f);

        var img = go.GetComponent<Image>();
        img.sprite = smokeSprite != null ? smokeSprite : GetFallbackSmokeSprite();
        img.color = smokeColor;
        img.raycastTarget = false;

        StartCoroutine(CoFadeSmokePuff(go, rt, img));
    }

    private IEnumerator CoFadeSmokePuff(GameObject go, RectTransform rt, Image img)
    {
        if (go == null || rt == null || img == null)
            yield break;

        float elapsed = 0f;
        Vector3 startScale = rt.localScale;
        Vector3 endScale = startScale * 1.85f;
        Color startColor = img.color;
        while (elapsed < smokeLife)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / smokeLife);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            rt.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            var c = startColor;
            c.a = startColor.a * (1f - eased);
            img.color = c;
            yield return null;
        }

        Destroy(go);
    }

    private static Sprite GetFallbackSmokeSprite()
    {
        if (fallbackSmokeSprite != null)
            return fallbackSmokeSprite;

        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float radius = res * 0.48f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / radius;
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply(false, true);
        fallbackSmokeSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        fallbackSmokeSprite.name = "GeneratedRocketSmoke";
        return fallbackSmokeSprite;
    }
}
