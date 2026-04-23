using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Royal Match tarzı PulseCore patlama efekti.
/// 
/// BOYUTLAMA MANTIGI (önemli):
///   baseSize = patlamanın hedef alan çapı (piksel). SetRadiusCells/SetAreaCells ile override edilir.
///   Her katmanın peak-anındaki görsel çapı: baseSize × peakSizeRatio
///   
/// Yani baseSize=500 ise:
///   - Shockwave peak'te 500 × 1.10 = 550 piksel çap
///   - Rays peak'te 500 × 1.00 = 500 piksel çap
///   - Glow peak'te 500 × 0.90 = 450 piksel çap
///   - Flash peak'te 500 × 0.75 = 375 piksel çap
///   
/// Bu sayede "3x3 alan" dediğinde her katman alana oturur.
/// </summary>
public class PulseCoreExplosionFX : MonoBehaviour
{
    [Header("Layers (prefab'dan child Image'ları sürükle)")]
    [SerializeField] private Image sunburstRays;
    [SerializeField] private Image innerGlow;
    [SerializeField] private Image shockwaveRing;
    [SerializeField] private Image coreFlash;

    [Header("Boyut")]
    [Tooltip("Hedef alan çapı (piksel). SetRadiusCells/SetAreaCells ile override edilir.")]
    [SerializeField] private float baseSize = 300f;

    [Tooltip("Alan boyutu için görsel taşma oranı. 1.0 = tam alan, 1.15 = %15 taşma (şık)")]
    [SerializeField, Range(0.5f, 2.0f)] private float areaOvershoot = 1.15f;

    [Header("Toplam süre")]
    [SerializeField] private float totalDuration = 0.85f;
    [SerializeField] private bool destroyOnFinish = true;

    // ═══════════════════════════════════════════════════════════════
    //  KATMAN PEAK ORANLARI (baseSize çarpanı)
    //  Her katmanın patlamanın doruğunda baseSize'ın kaç katı olacağı.
    //  Düşükten yükseğe sıralı: Flash (merkez) → Glow → Rays → Ring (en dış)
    // ═══════════════════════════════════════════════════════════════

    [Header("Core Flash (merkez beyaz parlama)")]
    [SerializeField, Range(0.1f, 2f)] private float flashPeakSizeRatio = 0.75f;
    [SerializeField] private float flashInTime = 0.05f;
    [SerializeField] private float flashOutTime = 0.22f;
    [SerializeField] private float flashStartRatio = 0.3f;   // start = peak × 0.3
    [SerializeField] private float flashEndRatio = 1.15f;    // end = peak × 1.15
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);

    [Header("Inner Glow (sıcak hale)")]
    [SerializeField, Range(0.1f, 2f)] private float glowPeakSizeRatio = 0.90f;
    [SerializeField] private float glowInTime = 0.08f;
    [SerializeField] private float glowOutTime = 0.50f;
    [SerializeField] private float glowStartRatio = 0.3f;
    [SerializeField] private float glowEndRatio = 1.1f;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.1f, 0.75f);

    [Header("Sunburst Rays (dönen ışın demeti)")]
    [SerializeField, Range(0.1f, 2f)] private float raysPeakSizeRatio = 1.00f;
    [SerializeField] private float raysInTime = 0.10f;
    [SerializeField] private float raysOutTime = 0.60f;
    [SerializeField] private float raysStartRatio = 0.2f;
    [SerializeField] private float raysEndRatio = 1.1f;
    [SerializeField] private float raysRotateSpeed = 90f;
    [SerializeField] private Color raysColor = new Color(1f, 0.82f, 0.25f, 0.95f);

    [Header("Shockwave Ring (en dış halka)")]
    [SerializeField, Range(0.1f, 2f)] private float ringPeakSizeRatio = 1.10f;
    [SerializeField] private float ringInTime = 0.03f;
    [SerializeField] private float ringOutTime = 0.50f;
    [SerializeField] private float ringStartRatio = 0.2f;
    [SerializeField] private float ringEndRatio = 1.15f;
    [SerializeField] private Color ringColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Header("Easing")]
    [SerializeField]
    private AnimationCurve easeOut =
        new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(1, 1, 0, 0));
    [SerializeField]
    private AnimationCurve easeIn =
        new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 3));

    // ═══════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// radius=1 → 3x3 alan, radius=2 → 5x5, radius=3 → 7x7, vb.
    /// </summary>
    public void SetRadiusCells(int radiusCells, float tileSize)
    {
        int sideCells = radiusCells * 2 + 1;
        SetAreaCells(sideCells, tileSize);
    }

    /// <summary>
    /// Doğrudan alan kenar uzunluğunu hücre cinsinden verir.
    /// 3x3 için sideCells=3, 5x5 için 5, 8x8 için 8.
    /// </summary>
    public void SetAreaCells(int sideCells, float tileSize)
    {
        baseSize = tileSize * sideCells * areaOvershoot;
    }

    public void SetBaseSize(float size)
    {
        baseSize = size;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        // Başlangıçta her şey görünmez olsun, böylece 1 frame beyaz patlama olmaz
        HideAll();
        StartCoroutine(DeferredStart());
    }

    private IEnumerator DeferredStart()
    {
        // 1 frame bekle: çağıran kodun SetRadiusCells yapmasına şans ver
        yield return null;

        yield return PlayExplosion();
    }

    private void HideAll()
    {
        if (sunburstRays != null) sunburstRays.color = WithAlpha(raysColor, 0f);
        if (innerGlow != null) innerGlow.color = WithAlpha(glowColor, 0f);
        if (shockwaveRing != null) shockwaveRing.color = WithAlpha(ringColor, 0f);
        if (coreFlash != null) coreFlash.color = WithAlpha(flashColor, 0f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Animation
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator PlayExplosion()
    {
        StartCoroutine(AnimFlash());
        StartCoroutine(AnimGlow());
        StartCoroutine(AnimRays());
        StartCoroutine(AnimRing());

        yield return new WaitForSeconds(totalDuration);
        if (destroyOnFinish)
            Destroy(gameObject);
    }

    private IEnumerator AnimFlash()
    {
        if (coreFlash == null) yield break;
        float peak = baseSize * flashPeakSizeRatio;
        yield return AnimateLayer(coreFlash, flashColor,
            peak * flashStartRatio, peak, 0f, 1f, flashInTime, easeOut);
        yield return AnimateLayer(coreFlash, flashColor,
            peak, peak * flashEndRatio, 1f, 0f, flashOutTime, easeIn);
    }

    private IEnumerator AnimGlow()
    {
        if (innerGlow == null) yield break;
        float peak = baseSize * glowPeakSizeRatio;
        yield return AnimateLayer(innerGlow, glowColor,
            peak * glowStartRatio, peak, 0f, glowColor.a, glowInTime, easeOut);
        yield return AnimateLayer(innerGlow, glowColor,
            peak, peak * glowEndRatio, glowColor.a, 0f, glowOutTime, easeIn);
    }

    private IEnumerator AnimRays()
    {
        if (sunburstRays == null) yield break;
        float peak = baseSize * raysPeakSizeRatio;
        var rt = sunburstRays.rectTransform;

        // Rastgele başlangıç rotation'ı — her patlama farklı görünsün
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        yield return AnimateLayerWithRotation(sunburstRays, raysColor,
            peak * raysStartRatio, peak, 0f, raysColor.a, raysInTime, easeOut);

        yield return AnimateLayerWithRotation(sunburstRays, raysColor,
            peak, peak * raysEndRatio, raysColor.a, 0f, raysOutTime, easeOut);
    }

    private IEnumerator AnimRing()
    {
        if (shockwaveRing == null) yield break;
        float peak = baseSize * ringPeakSizeRatio;
        yield return AnimateLayer(shockwaveRing, ringColor,
            peak * ringStartRatio, peak, 0f, ringColor.a, ringInTime, easeOut);
        yield return AnimateLayer(shockwaveRing, ringColor,
            peak, peak * ringEndRatio, ringColor.a, 0f, ringOutTime, easeOut);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Animation helpers
    //  Size (piksel) + alpha animasyonu. Scale yerine sizeDelta kullanıyoruz,
    //  böylece hedef boyutu birebir kontrol edebiliyoruz.
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator AnimateLayer(Image img, Color baseCol,
        float fromSize, float toSize, float fromA, float toA,
        float duration, AnimationCurve curve)
    {
        if (img == null || duration <= 0f) yield break;
        var rt = img.rectTransform;
        rt.localScale = Vector3.one; // scale 1 sabit, boyutu sizeDelta ile kontrol ediyoruz

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);
            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));
            yield return null;
        }

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private IEnumerator AnimateLayerWithRotation(Image img, Color baseCol,
        float fromSize, float toSize, float fromA, float toA,
        float duration, AnimationCurve curve)
    {
        if (img == null || duration <= 0f) yield break;
        var rt = img.rectTransform;
        rt.localScale = Vector3.one;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);
            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));
            rt.localRotation *= Quaternion.Euler(0, 0, raysRotateSpeed * Time.deltaTime);
            yield return null;
        }

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}