using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PulseCoreExplosionFX : MonoBehaviour
{
    [SerializeField] private Image sunburstRays;
    [SerializeField] private Image innerGlow;
    [SerializeField] private Image shockwaveRing;
    [SerializeField] private Image coreFlash;

    [SerializeField] private float baseSize = 300f;
    [SerializeField, Range(0.5f, 2.0f)] private float areaOvershoot = 1.15f;

    [SerializeField] private float totalDuration = 0.85f;
    [SerializeField] private bool destroyOnFinish = true;

    // ───────────────────────── FIREWORK RANDOMIZATION ─────────────────────────
    [Header("Firework Randomization")]
    [Tooltip("Her patlamada rastgele renk ve glow/core/ring kalınlığı seçilir. Çap random değişmez.")]
    [SerializeField] private bool randomizeOnPlay = true;

    [Header("Thickness Randomization")]
    [Tooltip("Glow outline kalınlığı min. Eski 1-2 aralığının üzerinde başlar. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float glowThicknessMin = 2.2f;

    [Tooltip("Glow outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float glowThicknessMax = 3.6f;

    [Tooltip("Core/flash outline kalınlığı min. Eski 1-2 aralığının üzerinde başlar. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float coreThicknessMin = 2.4f;

    [Tooltip("Core/flash outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(2f, 6f)] private float coreThicknessMax = 4.2f;

    [Tooltip("Ring outline kalınlığı min. Çapı değiştirmez.")]
    [SerializeField, Range(1f, 3f)] private float ringThicknessMin = 1.2f;

    [Tooltip("Ring outline kalınlığı max. Çapı değiştirmez.")]
    [SerializeField, Range(1f, 3f)] private float ringThicknessMax = 2.4f;

    [Tooltip("Outline renginin alpha çarpanı. Kalınlık random, ana glow/core/ring alpha random değildir.")]
    [SerializeField, Range(0f, 1f)] private float thicknessAlphaMultiplier = 0.65f;
    // ──────────────────────────────────────────────────────────────────────────

    [SerializeField, Range(0.1f, 2f)] private float flashPeakSizeRatio = 0.75f;
    [SerializeField] private float flashInTime = 0.05f;
    [SerializeField] private float flashOutTime = 0.22f;
    [SerializeField] private float flashStartRatio = 0.3f;
    [SerializeField] private float flashEndRatio = 1.15f;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);

    [SerializeField, Range(0.1f, 2f)] private float glowPeakSizeRatio = 0.90f;
    [SerializeField] private float glowInTime = 0.08f;
    [SerializeField] private float glowOutTime = 0.50f;
    [SerializeField] private float glowStartRatio = 0.3f;
    [SerializeField] private float glowEndRatio = 1.1f;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.1f, 0.75f);

    [SerializeField, Range(0.1f, 2f)] private float raysPeakSizeRatio = 1.00f;
    [SerializeField] private float raysInTime = 0.10f;
    [SerializeField] private float raysOutTime = 0.60f;
    [SerializeField] private float raysStartRatio = 0.2f;
    [SerializeField] private float raysEndRatio = 1.1f;
    [SerializeField] private float raysRotateSpeed = 90f;
    [SerializeField] private Color raysColor = new Color(1f, 0.82f, 0.25f, 0.95f);

    [SerializeField, Range(0.1f, 2f)] private float ringPeakSizeRatio = 1.10f;
    [SerializeField] private float ringInTime = 0.03f;
    [SerializeField] private float ringOutTime = 0.50f;
    [SerializeField] private float ringStartRatio = 0.2f;
    [SerializeField] private float ringEndRatio = 1.15f;
    [SerializeField] private Color ringColor = new Color(1f, 0.75f, 0.2f, 1f);

    [SerializeField]
    private AnimationCurve easeOut =
        new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(1, 1, 0, 0));

    [SerializeField]
    private AnimationCurve easeIn =
        new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 3));

    private Coroutine playRoutine;
    private bool isShuttingDown;

    private bool baselineCached;

    private float origFlashPeakSizeRatio;
    private float origGlowPeakSizeRatio;
    private float origRaysPeakSizeRatio;
    private float origRingPeakSizeRatio;

    private Color origFlashColor;
    private Color origGlowColor;
    private Color origRaysColor;
    private Color origRingColor;

    private Outline glowOutline;
    private Outline coreOutline;
    private Outline ringOutline;

    public void SetRadiusCells(int radiusCells, float tileSize)
    {
        int sideCells = radiusCells * 2 + 1;
        SetAreaCells(sideCells, tileSize);
    }

    public void SetAreaCells(int sideCells, float tileSize)
    {
        baseSize = tileSize * sideCells * areaOvershoot;
    }

    public void SetBaseSize(float size)
    {
        baseSize = size;
    }

    public void SetLifetime(float lifetime)
    {
        totalDuration = Mathf.Max(0.01f, lifetime);
    }

    private void OnEnable()
    {
        isShuttingDown = false;

        CacheBaselineOnce();

        if (randomizeOnPlay)
            ApplyRandomization();
        else
            RestoreBaseline();

        HideAll();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(DeferredStart());
    }

    private void OnDisable()
    {
        isShuttingDown = true;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  RANDOMIZATION
    // ═════════════════════════════════════════════════════════════════════════

    private void CacheBaselineOnce()
    {
        if (baselineCached)
            return;

        origFlashPeakSizeRatio = flashPeakSizeRatio;
        origGlowPeakSizeRatio = glowPeakSizeRatio;
        origRaysPeakSizeRatio = raysPeakSizeRatio;
        origRingPeakSizeRatio = ringPeakSizeRatio;

        origFlashColor = flashColor;
        origGlowColor = glowColor;
        origRaysColor = raysColor;
        origRingColor = ringColor;

        glowOutline = innerGlow != null ? innerGlow.GetComponent<Outline>() : null;
        coreOutline = coreFlash != null ? coreFlash.GetComponent<Outline>() : null;
        ringOutline = shockwaveRing != null ? shockwaveRing.GetComponent<Outline>() : null;

        baselineCached = true;
    }

    private void RestoreBaseline()
    {
        flashPeakSizeRatio = origFlashPeakSizeRatio;
        glowPeakSizeRatio = origGlowPeakSizeRatio;
        raysPeakSizeRatio = origRaysPeakSizeRatio;
        ringPeakSizeRatio = origRingPeakSizeRatio;

        flashColor = origFlashColor;
        glowColor = origGlowColor;
        raysColor = origRaysColor;
        ringColor = origRingColor;

        DisableOutline(glowOutline);
        DisableOutline(coreOutline);
        DisableOutline(ringOutline);
    }

    private void ApplyRandomization()
    {
        var variant = PulseFireworkPalette.PickRandomVariant();

        // Renk random kalır ama alpha random değişmez.
        // Alpha random olursa glow gradient dışarı daha görünür ve çap büyümüş gibi durur.
        flashColor = WithAlpha(variant.flash, origFlashColor.a);
        glowColor = WithAlpha(variant.glow, origGlowColor.a);
        raysColor = WithAlpha(variant.rays, origRaysColor.a);
        ringColor = WithAlpha(variant.ring, origRingColor.a);

        // Radius / çap sistemi korunur.
        // Normal Pulse radius 2, Pulse+Pulse radius 4 gibi değerler
        // dışarıdan SetRadiusCells ile gelmeye devam eder.
        //
        // Burada random olarak baseSize, radius veya peak ratio değiştirmiyoruz.
        flashPeakSizeRatio = origFlashPeakSizeRatio;
        glowPeakSizeRatio = origGlowPeakSizeRatio;
        raysPeakSizeRatio = origRaysPeakSizeRatio;
        ringPeakSizeRatio = origRingPeakSizeRatio;

        // Glow ve core kalınlığı eski 1-2 aralığının üzerinde random değişir.
        // Radius / çap değişmez; sadece Outline.effectDistance değişir.
        float glowThickness = Random.Range(glowThicknessMin, glowThicknessMax);
        float coreThickness = Random.Range(coreThicknessMin, coreThicknessMax);
        float ringThickness = Random.Range(ringThicknessMin, ringThicknessMax);

        ApplyOutlineThickness(innerGlow, ref glowOutline, glowColor, glowThickness);
        ApplyOutlineThickness(coreFlash, ref coreOutline, flashColor, coreThickness);
        ApplyOutlineThickness(shockwaveRing, ref ringOutline, ringColor, ringThickness);
    }

    private void ApplyOutlineThickness(Image img, ref Outline outline, Color color, float thickness)
    {
        if (img == null)
            return;

        if (outline == null)
            outline = img.GetComponent<Outline>();

        if (outline == null)
            outline = img.gameObject.AddComponent<Outline>();

        outline.enabled = true;
        outline.useGraphicAlpha = true;

        // Sadece outline mesafesi değişiyor.
        // RectTransform sizeDelta değişmiyor, yani radius/çap değişmiyor.
        outline.effectDistance = new Vector2(thickness, thickness);
        outline.effectColor = WithAlpha(color, color.a * thicknessAlphaMultiplier);
    }

    private void DisableOutline(Outline outline)
    {
        if (outline != null)
            outline.enabled = false;
    }

    // ═════════════════════════════════════════════════════════════════════════

    private IEnumerator DeferredStart()
    {
        yield return null;

        if (isShuttingDown || this == null || !isActiveAndEnabled)
            yield break;

        yield return PlayExplosion();
        playRoutine = null;
    }

    private void HideAll()
    {
        if (sunburstRays != null) sunburstRays.color = WithAlpha(raysColor, 0f);
        if (innerGlow != null) innerGlow.color = WithAlpha(glowColor, 0f);
        if (shockwaveRing != null) shockwaveRing.color = WithAlpha(ringColor, 0f);
        if (coreFlash != null) coreFlash.color = WithAlpha(flashColor, 0f);
    }

    private IEnumerator PlayExplosion()
    {
        yield return StartCoroutine(CoPlayExplosion());

        if (!isShuttingDown && destroyOnFinish && gameObject != null)
            Destroy(gameObject);
    }

    private IEnumerator CoPlayExplosion()
    {
        Coroutine flash = StartCoroutine(AnimFlash());
        Coroutine glow = StartCoroutine(AnimGlow());
        Coroutine rays = StartCoroutine(AnimRays());
        Coroutine ring = StartCoroutine(AnimRing());

        yield return new WaitForSeconds(totalDuration);

        if (isShuttingDown)
            yield break;

        if (flash != null) StopCoroutine(flash);
        if (glow != null) StopCoroutine(glow);
        if (rays != null) StopCoroutine(rays);
        if (ring != null) StopCoroutine(ring);
    }

    private IEnumerator AnimFlash()
    {
        if (coreFlash == null)
            yield break;

        float peak = baseSize * flashPeakSizeRatio;

        yield return AnimateLayer(coreFlash, flashColor, peak * flashStartRatio, peak, 0f, flashColor.a, flashInTime, easeOut);
        yield return AnimateLayer(coreFlash, flashColor, peak, peak * flashEndRatio, flashColor.a, 0f, flashOutTime, easeIn);
    }

    private IEnumerator AnimGlow()
    {
        if (innerGlow == null)
            yield break;

        float peak = baseSize * glowPeakSizeRatio;

        yield return AnimateLayer(innerGlow, glowColor, peak * glowStartRatio, peak, 0f, glowColor.a, glowInTime, easeOut);
        yield return AnimateLayer(innerGlow, glowColor, peak, peak * glowEndRatio, glowColor.a, 0f, glowOutTime, easeIn);
    }

    private IEnumerator AnimRays()
    {
        if (sunburstRays == null)
            yield break;

        float peak = baseSize * raysPeakSizeRatio;

        if (sunburstRays.rectTransform == null)
            yield break;

        sunburstRays.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        yield return AnimateLayerWithRotation(sunburstRays, raysColor, peak * raysStartRatio, peak, 0f, raysColor.a, raysInTime, easeOut);
        yield return AnimateLayerWithRotation(sunburstRays, raysColor, peak, peak * raysEndRatio, raysColor.a, 0f, raysOutTime, easeOut);
    }

    private IEnumerator AnimRing()
    {
        if (shockwaveRing == null)
            yield break;

        float peak = baseSize * ringPeakSizeRatio;

        yield return AnimateLayer(shockwaveRing, ringColor, peak * ringStartRatio, peak, 0f, ringColor.a, ringInTime, easeOut);
        yield return AnimateLayer(shockwaveRing, ringColor, peak, peak * ringEndRatio, ringColor.a, 0f, ringOutTime, easeOut);
    }

    private IEnumerator AnimateLayer(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;

        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private IEnumerator AnimateLayerWithRotation(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;

        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));
            rt.localRotation *= Quaternion.Euler(0, 0, raysRotateSpeed * Time.deltaTime);

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }
}
// ═════════════════════════════════════════════════════════════════════════════
//  FIREWORK COLOR PALETTE
//  4 aile (mavi / kırmızı / sarı / yeşil) × 3 ton = 12 varyant
//  Her varyant 4 katman için RGB üretir. Alpha değerleri prefab'dan korunur.
// ═════════════════════════════════════════════════════════════════════════════
public static class PulseFireworkPalette
{
    public struct Variant
    {
        public Color flash;  // merkez — en parlak
        public Color glow;   // yumuşak halka
        public Color rays;   // ışın accent
        public Color ring;   // shockwave
    }

    private static readonly Variant[] Variants =
    {
        // ─── 🔵 MAVİ AİLESİ ──────────────────────────────────────────────
        // Klasik mavi — kraliyet mavisi → açık sky
        new Variant
        {
            flash = new Color(0.85f, 0.95f, 1.00f),  // buzlu beyaz
            glow  = new Color(0.30f, 0.55f, 1.00f),  // kraliyet mavisi
            rays  = new Color(0.55f, 0.80f, 1.00f),  // açık sky
            ring  = new Color(0.20f, 0.45f, 1.00f),  // doygun mavi
        },
        // Açık mavi / cyan — buzul havai fişek
        new Variant
        {
            flash = new Color(0.92f, 1.00f, 1.00f),
            glow  = new Color(0.50f, 0.85f, 1.00f),
            rays  = new Color(0.70f, 0.95f, 1.00f),
            ring  = new Color(0.35f, 0.75f, 1.00f),
        },
        // Turkuaz / aqua
        new Variant
        {
            flash = new Color(0.85f, 1.00f, 0.98f),
            glow  = new Color(0.20f, 0.80f, 0.75f),
            rays  = new Color(0.45f, 0.95f, 0.85f),
            ring  = new Color(0.10f, 0.70f, 0.70f),
        },

        // ─── 🔴 KIRMIZI AİLESİ ───────────────────────────────────────────
        // Saf kırmızı
        new Variant
        {
            flash = new Color(1.00f, 0.90f, 0.90f),
            glow  = new Color(1.00f, 0.25f, 0.20f),
            rays  = new Color(1.00f, 0.50f, 0.40f),
            ring  = new Color(0.95f, 0.15f, 0.15f),
        },
        // Kızıl-turuncu
        new Variant
        {
            flash = new Color(1.00f, 0.95f, 0.85f),
            glow  = new Color(1.00f, 0.45f, 0.15f),
            rays  = new Color(1.00f, 0.65f, 0.30f),
            ring  = new Color(1.00f, 0.35f, 0.10f),
        },
        // Fuşya / hot pink
        new Variant
        {
            flash = new Color(1.00f, 0.92f, 0.96f),
            glow  = new Color(1.00f, 0.30f, 0.65f),
            rays  = new Color(1.00f, 0.55f, 0.80f),
            ring  = new Color(0.95f, 0.20f, 0.60f),
        },

        // ─── 🟡 SARI AİLESİ ──────────────────────────────────────────────
        // Altın sarısı
        new Variant
        {
            flash = new Color(1.00f, 0.98f, 0.85f),
            glow  = new Color(1.00f, 0.75f, 0.15f),
            rays  = new Color(1.00f, 0.88f, 0.40f),
            ring  = new Color(1.00f, 0.70f, 0.10f),
        },
        // Limon sarısı — parlak
        new Variant
        {
            flash = new Color(1.00f, 1.00f, 0.90f),
            glow  = new Color(0.98f, 0.95f, 0.30f),
            rays  = new Color(1.00f, 1.00f, 0.55f),
            ring  = new Color(0.95f, 0.90f, 0.20f),
        },
        // Amber / sıcak altın
        new Variant
        {
            flash = new Color(1.00f, 0.94f, 0.80f),
            glow  = new Color(1.00f, 0.65f, 0.10f),
            rays  = new Color(1.00f, 0.82f, 0.35f),
            ring  = new Color(0.95f, 0.60f, 0.08f),
        },

        // ─── 🟢 YEŞİL AİLESİ ─────────────────────────────────────────────
        // Saf yeşil
        new Variant
        {
            flash = new Color(0.90f, 1.00f, 0.90f),
            glow  = new Color(0.20f, 0.85f, 0.30f),
            rays  = new Color(0.50f, 1.00f, 0.55f),
            ring  = new Color(0.10f, 0.75f, 0.25f),
        },
        // Lime / neon yeşil
        new Variant
        {
            flash = new Color(0.95f, 1.00f, 0.88f),
            glow  = new Color(0.60f, 0.95f, 0.20f),
            rays  = new Color(0.80f, 1.00f, 0.45f),
            ring  = new Color(0.50f, 0.90f, 0.15f),
        },
        // Zümrüt / koyu yeşil
        new Variant
        {
            flash = new Color(0.88f, 1.00f, 0.92f),
            glow  = new Color(0.10f, 0.65f, 0.40f),
            rays  = new Color(0.35f, 0.85f, 0.55f),
            ring  = new Color(0.05f, 0.55f, 0.35f),
        },
    };

    public static Variant PickRandomVariant()
    {
        int i = Random.Range(0, Variants.Length);
        return Variants[i];
    }
}