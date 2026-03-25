using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Override + Override combo VFX controller.
/// 
/// PHASES:
///   1) ORBIT        – Two swapped override icons orbit each other (~2s), stormParticles emitting sparkles.
///   2) MERGE        – Icons compress to center, bright flash, burst emission, merged icon appears.
///   3) RADIAL CLEAR – A shockwave ring expands from center, clearing the board radius by radius.
///   4) FADE OUT     – Everything fades.
///
/// IconA, IconB, MergedIcon Image objects are AUTO-CREATED at runtime if not assigned in Inspector.
/// Sprites come from Play(spriteA, spriteB) — BoardController provides them automatically.
/// </summary>
public class OverrideComboController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  REFERENCES
    // ──────────────────────────────────────────────
    [Header("Core Refs (Assign in Inspector)")]
    [SerializeField] private RectTransform pivot;
    [SerializeField] private Image centerFlash;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Icons (auto-created if left empty)")]
    [SerializeField] private Image iconImageA;
    [SerializeField] private Image iconImageB;
    [SerializeField] private Image mergedIconImage;

    [Header("Particles")]
    [SerializeField] private ParticleSystem stormParticles;
    [SerializeField] private int mergeBurstCount = 60;

    [Header("Radial Clear (optional – shockwave ring)")]
    [SerializeField] private Image shockwaveImage;
    [SerializeField] private float shockwaveMaxScale = 25f;
    [SerializeField] private float shockwaveStartScale = 0.1f;

    // ──────────────────────────────────────────────
    //  TIMINGS
    // ──────────────────────────────────────────────
    [Header("Phase Timings")]
    [SerializeField] private float orbitDuration = 2.0f;
    [SerializeField] private float mergeDuration = 0.30f;
    [SerializeField] private float flashHoldDuration = 0.08f;
    [SerializeField] private float mergedIconShowDuration = 0.35f;
    [SerializeField] private float radialClearDuration = 0.70f;
    [SerializeField] private float fadeOutDuration = 0.30f;

    // ──────────────────────────────────────────────
    //  ORBIT SETTINGS
    // ──────────────────────────────────────────────
    [Header("Orbit")]
    [SerializeField] private float orbitRadiusStart = 130f;
    [SerializeField] private float orbitRadiusEnd = 50f;
    [SerializeField] private float orbitTurns = 4.0f;
    [SerializeField] private float orbitScaleFrom = 0.8f;
    [SerializeField] private float orbitScaleTo = 1.3f;
    [SerializeField] private float iconGlowPulseSpeed = 8f;
    [SerializeField] private float iconSize = 80f; // Width & height of auto-created icon Images

    [Header("Merge")]
    [SerializeField] private float mergeScalePunch = 1.6f;
    [SerializeField] private float flashMaxAlpha = 0.95f;

    [Header("Shockwave")]
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.85f, 0.3f, 0.7f);

    // ──────────────────────────────────────────────
    //  CALLBACKS
    // ──────────────────────────────────────────────

    /// <summary>
    /// Fired as the radial clear wave expands. Parameter: normalized radius (0 = center, 1 = edge).
    /// </summary>
    public event Action<float> OnRadialClearProgress;

    /// <summary>
    /// Fired once when the entire VFX sequence is complete.
    /// </summary>
    public event Action OnComboFinished;

    // ──────────────────────────────────────────────
    //  PRIVATE
    // ──────────────────────────────────────────────
    private Coroutine _routine;
    private float _savedEmissionRate;
    private float _savedStartSpeed;
    private float _savedStartSize;
    private float _savedStartLifetime;
    private bool _savedLoop;

    private RectTransform IconRectA => iconImageA.rectTransform;
    private RectTransform IconRectB => iconImageB.rectTransform;

    // ──────────────────────────────────────────────
    //  LIFECYCLE
    // ──────────────────────────────────────────────

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // ──────────────────────────────────────────────
    //  AUTO-CREATE MISSING ICON IMAGES
    // ──────────────────────────────────────────────

    /// <summary>
    /// Creates a UI Image as a child of pivot if the reference is null.
    /// </summary>
    private Image EnsureIconImage(ref Image field, string objectName)
    {
        if (field != null) return field;

        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pivot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;

        field = img;
        return img;
    }

    private void EnsureAllIcons()
    {
        EnsureIconImage(ref iconImageA, "IconA_Auto");
        EnsureIconImage(ref iconImageB, "IconB_Auto");
        EnsureIconImage(ref mergedIconImage, "MergedIcon_Auto");
    }

    // ──────────────────────────────────────────────
    //  PUBLIC API
    // ──────────────────────────────────────────────

    public void PlayAtAnchoredPosition(Vector2 anchoredPos, Sprite overrideSpriteA, Sprite overrideSpriteB, Sprite mergedSprite = null)
    {
        var rt = transform as RectTransform;
        if (rt != null) rt.anchoredPosition = anchoredPos;
        Play(overrideSpriteA, overrideSpriteB, mergedSprite);
    }

    public void Play(Sprite overrideSpriteA, Sprite overrideSpriteB, Sprite mergedSprite = null)
    {
        if (!IsWired())
        {
            Debug.LogError("[OverrideComboController] Missing CORE refs (pivot/flash/canvasGroup/stormParticles)!");
            return;
        }

        // Auto-create icon Images if not assigned in Inspector
        EnsureAllIcons();

        // Set sprites from the swapped override tiles
        iconImageA.sprite = overrideSpriteA;
        iconImageB.sprite = overrideSpriteB;
        mergedIconImage.sprite = mergedSprite != null ? mergedSprite : overrideSpriteA;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Play());
    }

    public float GetTotalDuration()
    {
        // Orbit + Merge compress + simultaneous (flash+shockwave+clear) + Fade
        return orbitDuration + mergeDuration + radialClearDuration + fadeOutDuration;
    }

    public void Stop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        Cleanup();
    }

    // ──────────────────────────────────────────────
    //  WIRING CHECK (only truly required refs)
    // ──────────────────────────────────────────────

    private bool IsWired()
    {
        return pivot != null
               && centerFlash != null
               && canvasGroup != null
               && stormParticles != null;
        // iconImageA, iconImageB, mergedIconImage → auto-created if null
        // shockwaveImage → optional, null-checked at use
    }

    // ──────────────────────────────────────────────
    //  CLEANUP (always runs, even on error)
    // ──────────────────────────────────────────────

    private void Cleanup()
    {
        RestoreStormDefaults();
        StormStop(true);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        _routine = null;
        gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────
    //  MAIN COROUTINE
    // ──────────────────────────────────────────────

    private IEnumerator Co_Play()
    {
        bool finished = false;
        try
        {
            // ── INIT ──
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;

            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            // Merged icon: hidden initially
            SetAlpha(mergedIconImage, 0f);
            mergedIconImage.rectTransform.localScale = Vector3.zero;

            // Shockwave: hidden initially (optional)
            if (shockwaveImage != null)
            {
                SetAlpha(shockwaveImage, 0f);
                shockwaveImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
            }

            // Show orbit icons
            SetAlpha(iconImageA, 1f);
            SetAlpha(iconImageB, 1f);
            IconRectA.localScale = Vector3.one * orbitScaleFrom;
            IconRectB.localScale = Vector3.one * orbitScaleFrom;

            SetFlashAlpha(0f);

            SaveStormDefaults();
            StormStop(true);

            // ════════════════════════════════════════════
            //  PHASE 1: ORBIT (~2s)
            // ════════════════════════════════════════════

            var main = stormParticles.main;
            main.loop = true;
            main.startSpeed = _savedStartSpeed;
            main.startSize = _savedStartSize;
            main.startLifetime = _savedStartLifetime;

            stormParticles.Play();

            float orbitTime = 0f;
            float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            while (orbitTime < orbitDuration)
            {
                orbitTime += Time.deltaTime;
                float t = Mathf.Clamp01(orbitTime / orbitDuration);

                float easeT = EaseInOutCubic(t);
                float currentRadius = Mathf.Lerp(orbitRadiusStart, orbitRadiusEnd, t * t);

                float iconScale = Mathf.Lerp(orbitScaleFrom, orbitScaleTo, t);
                IconRectA.localScale = Vector3.one * iconScale;
                IconRectB.localScale = Vector3.one * iconScale;

                float ang = baseAngle + (easeT * orbitTurns * Mathf.PI * 2f);

                Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * currentRadius;
                IconRectA.anchoredPosition = offset;
                IconRectB.anchoredPosition = -offset;

                float glowPulse = 0.85f + 0.15f * Mathf.Sin(orbitTime * iconGlowPulseSpeed);
                SetAlpha(iconImageA, glowPulse);
                SetAlpha(iconImageB, glowPulse);

                var shape = stormParticles.shape;
                shape.radius = currentRadius * 0.8f;

                var emission = stormParticles.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(30f, 150f, t);

                if (t > 0.8f)
                {
                    float preFlashT = (t - 0.8f) / 0.2f;
                    SetFlashAlpha(preFlashT * 0.15f);
                }

                yield return null;
            }

            // ════════════════════════════════════════════
            //  PHASE 2: MERGE
            // ════════════════════════════════════════════

            stormParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            float mergeTime = 0f;
            Vector2 aStart = IconRectA.anchoredPosition;
            Vector2 bStart = IconRectB.anchoredPosition;
            Vector3 aScaleStart = IconRectA.localScale;
            Vector3 bScaleStart = IconRectB.localScale;

            while (mergeTime < mergeDuration)
            {
                mergeTime += Time.deltaTime;
                float t = Mathf.Clamp01(mergeTime / mergeDuration);
                float smoothT = EaseInBack(t);

                IconRectA.anchoredPosition = Vector2.Lerp(aStart, Vector2.zero, smoothT);
                IconRectB.anchoredPosition = Vector2.Lerp(bStart, Vector2.zero, smoothT);

                float compressScale = Mathf.Lerp(1f, 0.5f, smoothT);
                IconRectA.localScale = aScaleStart * compressScale;
                IconRectB.localScale = bScaleStart * compressScale;

                float flashT = Mathf.Clamp01((t - 0.3f) / 0.7f);
                SetFlashAlpha(flashT * flashMaxAlpha);

                yield return null;
            }

            // ════════════════════════════════════════════
            //  PHASE 3: FLASH + BURST + SHOCKWAVE + NUCLEAR BLAST (simultaneous)
            // ════════════════════════════════════════════

            // Everything fires at once: flash, burst, merged icon, shockwave
            SetFlashAlpha(flashMaxAlpha);
            SetAlpha(iconImageA, 0f);
            SetAlpha(iconImageB, 0f);

            // Initial merge burst
            var burstMain = stormParticles.main;
            burstMain.startSpeed = _savedStartSpeed * 3f;
            burstMain.startSize = _savedStartSize * 1.4f;
            burstMain.startLifetime = 0.5f;
            stormParticles.Emit(mergeBurstCount);

            // Now switch stormParticles to "nuclear blast" mode:
            // continuous emission, expanding radius, particles flying outward
            burstMain.loop = true;
            burstMain.startSpeed = _savedStartSpeed * 5f;   // fast outward push
            burstMain.startSize = _savedStartSize * 1.8f;   // big bright particles
            burstMain.startLifetime = 0.6f;

            var blastShape = stormParticles.shape;
            blastShape.shapeType = ParticleSystemShapeType.Circle;
            blastShape.radius = 0.1f; // starts tiny, expands in the loop

            var blastEmission = stormParticles.emission;
            blastEmission.rateOverTimeMultiplier = 200f; // dense particle cloud

            stormParticles.Play();

            // Show merged icon with scale punch
            SetAlpha(mergedIconImage, 1f);
            mergedIconImage.rectTransform.localScale = Vector3.one * mergeScalePunch;

            // Start shockwave immediately
            if (shockwaveImage != null)
            {
                SetAlpha(shockwaveImage, shockwaveColor.a);
                shockwaveImage.color = shockwaveColor;
                shockwaveImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
            }

            // All animate together: flash + merged icon + shockwave + nuclear particle wave
            float clearTime = 0f;
            float lastReportedRadius = -1f;

            while (clearTime < radialClearDuration)
            {
                clearTime += Time.deltaTime;
                float t = Mathf.Clamp01(clearTime / radialClearDuration);

                float expandT = EaseOutQuad(t);

                // Flash fades out in first 30%
                float flashFade = Mathf.Lerp(flashMaxAlpha, 0f, Mathf.Clamp01(t / 0.3f));
                SetFlashAlpha(flashFade);

                // Shockwave ring expands
                if (shockwaveImage != null)
                {
                    float ringScale = Mathf.Lerp(shockwaveStartScale, shockwaveMaxScale, expandT);
                    shockwaveImage.rectTransform.localScale = Vector3.one * ringScale;

                    float ringAlpha = Mathf.Lerp(shockwaveColor.a, 0f, t * t);
                    SetAlpha(shockwaveImage, ringAlpha);
                }

                // Nuclear blast: particle shape radius expands with the shockwave
                float blastRadius = Mathf.Lerp(0.1f, orbitRadiusStart * 2f, expandT);
                var shape = stormParticles.shape;
                shape.radius = blastRadius;

                // Emission fades as blast expands (dense center, sparse edges)
                var emission = stormParticles.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(250f, 30f, t);

                // Particle speed slows as wave reaches edges
                var blastMain = stormParticles.main;
                blastMain.startSpeed = Mathf.Lerp(_savedStartSpeed * 5f, _savedStartSpeed * 1.5f, t);

                // Merged icon: elastic settle then fade
                float scaleT = EaseOutElastic(Mathf.Clamp01(t / 0.4f));
                float s = Mathf.Lerp(mergeScalePunch, 1.0f, scaleT);
                float iconFade = Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.3f) / 0.4f));
                mergedIconImage.rectTransform.localScale = Vector3.one * s;
                SetAlpha(mergedIconImage, iconFade);

                // Board clear progress callback
                float radiusNorm = expandT;
                if (radiusNorm - lastReportedRadius >= 0.05f)
                {
                    lastReportedRadius = radiusNorm;
                    OnRadialClearProgress?.Invoke(radiusNorm);
                }

                yield return null;
            }

            // Stop nuclear blast emission, let remaining particles fade
            stormParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            SetFlashAlpha(0f);
            SetAlpha(mergedIconImage, 0f);
            OnRadialClearProgress?.Invoke(1f);

            // ════════════════════════════════════════════
            //  PHASE 4: FADE OUT
            // ════════════════════════════════════════════

            float fadeTime = 0f;
            float startAlpha = canvasGroup.alpha;

            while (fadeTime < fadeOutDuration)
            {
                fadeTime += Time.deltaTime;
                float t = Mathf.Clamp01(fadeTime / fadeOutDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            finished = true;
        }
        finally
        {
            Cleanup();

            if (finished)
                OnComboFinished?.Invoke();
            else
                Debug.LogError("[OverrideComboController] Coroutine died unexpectedly — cleaned up. Check Console for errors above.");
        }
    }

    // ──────────────────────────────────────────────
    //  STORM PARTICLE HELPERS
    // ──────────────────────────────────────────────

    private void SaveStormDefaults()
    {
        if (stormParticles == null) return;
        var m = stormParticles.main;
        _savedEmissionRate = stormParticles.emission.rateOverTimeMultiplier;
        _savedStartSpeed = m.startSpeed.constant;
        _savedStartSize = m.startSize.constant;
        _savedStartLifetime = m.startLifetime.constant;
        _savedLoop = m.loop;
    }

    private void RestoreStormDefaults()
    {
        if (stormParticles == null) return;
        var m = stormParticles.main;
        m.startSpeed = _savedStartSpeed;
        m.startSize = _savedStartSize;
        m.startLifetime = _savedStartLifetime;
        m.loop = _savedLoop;
        var emission = stormParticles.emission;
        emission.rateOverTimeMultiplier = _savedEmissionRate;
    }

    private void StormStop(bool clear)
    {
        if (stormParticles == null) return;
        stormParticles.Stop(true, clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting);
    }

    // ──────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────

    private void SetFlashAlpha(float a)
    {
        if (centerFlash == null) return;
        var c = centerFlash.color;
        c.a = a;
        centerFlash.color = c;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    // ──────────────────────────────────────────────
    //  EASING FUNCTIONS
    // ──────────────────────────────────────────────

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    private static float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
}