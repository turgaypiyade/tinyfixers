using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Override + Override combo VFX controller.
///
/// PHASES:
///   1) ORBIT  – Icons grow to 2.5x, self-spin + orbit each other, energy glow trails behind them.
///   2) SMASH  – Icons rush together and collide violently (configurable duration, default 1.5 s).
///               Post-impact they continue orbiting intensely. Glow persists.
///   3) WAVE   – Shockwave ring expands from center. Board tiles clear IN SYNC with the wave.
///               Icons keep spinning while the wave rolls out, then fade as wave reaches the edge.
///   4) FADE   – Quick canvas fade and cleanup.
///
/// Tile-clearing is deliberately delayed until the WAVE phase starts so players see
/// the shockwave "destroy" tiles as it sweeps through (preClearDelay = orbit + smash duration).
///
/// GlowA / GlowB Images are AUTO-CREATED at runtime (same sprite as icon, warm tint, 2x scale).
/// Placed as siblings BEHIND their icon counterparts in the pivot's hierarchy.
/// </summary>
public class OverrideComboController : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  REFERENCES
    // ─────────────────────────────────────────
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
    [SerializeField] private int mergeBurstCount = 80;

    [Header("Radial Clear (optional – shockwave ring)")]
    [SerializeField] private Image shockwaveImage;
    [SerializeField] private float shockwaveMaxScale = 25f;
    [SerializeField] private float shockwaveStartScale = 0.1f;

    // ─────────────────────────────────────────
    //  TIMINGS
    // ─────────────────────────────────────────
    [Header("Phase Timings")]
    [SerializeField] private float orbitDuration     = 2.0f;
    [SerializeField] private float mergeDuration     = 1.5f;   // smash / collision phase
    [SerializeField] private float radialClearDuration = 0.50f; // wave phase (faster than before)
    [SerializeField] private float fadeOutDuration   = 0.20f;

    // ─────────────────────────────────────────
    //  ORBIT SETTINGS
    // ─────────────────────────────────────────
    [Header("Orbit")]
    [SerializeField] private float orbitRadiusStart  = 130f;
    [SerializeField] private float orbitRadiusEnd    = 50f;
    [SerializeField] private float orbitTurns        = 3.5f;
    [SerializeField] private float orbitScaleTarget  = 2.5f;   // grow to 2.5x during orbit
    [SerializeField] private float iconGlowPulseSpeed = 8f;
    [SerializeField] private float iconSize          = 80f;

    [Header("Glow")]
    [SerializeField] private Color glowColor         = new Color(1f, 0.72f, 0.18f, 0.65f);
    [SerializeField] private float glowSizeMultiplier = 1.85f;

    [Header("Smash / Merge")]
    [SerializeField] private float smashImpactFraction = 0.22f; // 0..1 — when within smash the hit fires
    [SerializeField] private float flashMaxAlpha     = 0.95f;

    [Header("Shockwave")]
    [SerializeField] private Color shockwaveColor    = new Color(1f, 0.85f, 0.3f, 0.75f);

    [Header("Wave Glow")]
    [SerializeField] private Sprite waveGlowSprite;
    [SerializeField] private Color  waveGlowColor    = new Color(1f, 0.85f, 0.3f, 0.40f);

    // ─────────────────────────────────────────
    //  CALLBACKS
    // ─────────────────────────────────────────
    public event Action<float> OnRadialClearProgress;
    public event Action        OnComboFinished;
    public event Action        OnImpact;

    // ─────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────
    private Coroutine _routine;
    private float _savedEmissionRate;
    private float _savedStartSpeed;
    private float _savedStartSize;
    private float _savedStartLifetime;
    private bool  _savedLoop;

    private Image glowImageA;
    private Image glowImageB;
    private Image waveGlowImage;

    private RectTransform IconRectA  => iconImageA.rectTransform;
    private RectTransform IconRectB  => iconImageB.rectTransform;

    // ─────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────
    private void Reset() { canvasGroup = GetComponent<CanvasGroup>(); }

    // ─────────────────────────────────────────
    //  TIMING ACCESSORS  (used by board to sync tile-clear delays)
    // ─────────────────────────────────────────

    /// <summary>Seconds before the radial wave starts (orbit + smash phases).</summary>
    public float GetPreClearDuration()     => orbitDuration + mergeDuration;
    /// <summary>Duration of the radial clear wave phase.</summary>
    public float GetRadialWaveDuration()   => radialClearDuration;
    /// <summary>Total VFX duration.</summary>
    public float GetTotalDuration()        => orbitDuration + mergeDuration + radialClearDuration + fadeOutDuration;

    // ─────────────────────────────────────────
    //  AUTO-CREATE ICONS
    // ─────────────────────────────────────────
    private Image EnsureIconImage(ref Image field, string objectName)
    {
        if (field != null) return field;
        var go  = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pivot, false);
        var rt  = go.GetComponent<RectTransform>();
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
        EnsureIconImage(ref iconImageA,    "IconA_Auto");
        EnsureIconImage(ref iconImageB,    "IconB_Auto");
        EnsureIconImage(ref mergedIconImage, "MergedIcon_Auto");
    }

    private void EnsureGlowImages()
    {
        if (glowImageA == null) glowImageA = CreateGlowBehind(iconImageA, "GlowA_Auto");
        if (glowImageB == null) glowImageB = CreateGlowBehind(iconImageB, "GlowB_Auto");

        if (waveGlowImage == null)
        {
            var go  = new GameObject("WaveGlow_Auto", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pivot, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.one * iconSize;
            rt.localScale       = Vector3.one;
            waveGlowImage = go.GetComponent<Image>();
            waveGlowImage.raycastTarget = false;
            waveGlowImage.color = waveGlowColor;
            // Inspector'dan sprite atandıysa onu kullan, yoksa shockwave sprite'ı dene
            if (waveGlowSprite != null)
                waveGlowImage.sprite = waveGlowSprite;
            else if (shockwaveImage != null && shockwaveImage.sprite != null)
                waveGlowImage.sprite = shockwaveImage.sprite;
            go.transform.SetSiblingIndex(0);
        }
    }

    private Image CreateGlowBehind(Image anchor, string name)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pivot, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = glowColor;
        // Place BEHIND the anchor icon in sibling order
        if (anchor != null)
            go.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex());
        return img;
    }

    // ─────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────
    public void PlayAtAnchoredPosition(Vector2 anchoredPos, Sprite sprA, Sprite sprB, Sprite merged = null)
    {
        var rt = transform as RectTransform;
        if (rt != null) rt.anchoredPosition = anchoredPos;
        Play(sprA, sprB, merged);
    }

    public void Play(Sprite sprA, Sprite sprB, Sprite merged = null)
    {
        if (!IsWired())
        {
            Debug.LogError("[OverrideComboController] Missing core refs (pivot/flash/canvasGroup/stormParticles)!");
            return;
        }
        // KRİTİK ÖNEMLİ: gameObject SetActive(true) BoardVfxService'de çağrılıyor → bu noktada child'lar
        // önceki combo'dan kalma state'le bir frame görünebilir. canvasGroup.alpha=0 hepsini gizler,
        // ek olarak tüm görsel state'leri elle sıfırlıyoruz ki Co_Play öncesi flash olmasın.
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        EnsureAllIcons();
        EnsureGlowImages();

        // Hierarchy'deki TÜM Image component'lerini bul ve alpha 0'a indir (custom/unutulmuş sprite'lar dahil).
        var allImages = GetComponentsInChildren<Image>(includeInactive: true);
        for (int i = 0; i < allImages.Length; i++)
        {
            if (allImages[i] == null) continue;
            var c = allImages[i].color;
            c.a = 0f;
            allImages[i].color = c;
        }

        if (mergedIconImage != null)
        {
            mergedIconImage.rectTransform.localScale = Vector3.zero;
        }

        // Shockwave / WaveGlow scale reset (büyük kalmasın geçen combo'dan)
        if (shockwaveImage != null)
            shockwaveImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
        if (waveGlowImage != null)
            waveGlowImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;

        iconImageA.sprite    = sprA;
        iconImageB.sprite    = sprB;
        mergedIconImage.sprite = merged != null ? merged : sprA;

        // Glow uses same sprite as its icon
        if (glowImageA != null) glowImageA.sprite = sprA;
        if (glowImageB != null) glowImageB.sprite = sprB;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Play());
    }

    public void Stop()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        Cleanup();
    }

    // ─────────────────────────────────────────
    //  WIRING CHECK
    // ─────────────────────────────────────────
    private bool IsWired()
        => pivot != null && centerFlash != null && canvasGroup != null && stormParticles != null;

    // ─────────────────────────────────────────
    //  CLEANUP
    // ─────────────────────────────────────────
    private void Cleanup()
    {
        RestoreStormDefaults();
        StormStop(true);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        _routine = null;
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  MAIN COROUTINE
    // ─────────────────────────────────────────
    private IEnumerator Co_Play()
    {
        bool finished = false;
        try
        {
            // ── INIT ──
            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale    = Vector3.one;

            SetAlpha(mergedIconImage, 0f);
            mergedIconImage.rectTransform.localScale = Vector3.zero;

            if (shockwaveImage != null)
            {
                SetAlpha(shockwaveImage, 0f);
                shockwaveImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
            }

            if (waveGlowImage != null)
            {
                waveGlowImage.color = waveGlowColor;
                SetAlpha(waveGlowImage, 0f);
                waveGlowImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
            }

            SetAlpha(iconImageA, 1f);
            SetAlpha(iconImageB, 1f);
            IconRectA.localScale    = Vector3.one;
            IconRectB.localScale    = Vector3.one;
            IconRectA.localRotation = Quaternion.identity;
            IconRectB.localRotation = Quaternion.identity;

            SetAlpha(glowImageA, 0f);
            SetAlpha(glowImageB, 0f);
            SetGlowPos(glowImageA, Vector2.zero, Quaternion.identity, Vector3.one);
            SetGlowPos(glowImageB, Vector2.zero, Quaternion.identity, Vector3.one);

            SetFlashAlpha(0f);
            SaveStormDefaults();
            StormStop(true);

            // ════════════════════════════════════════
            //  PHASE 1 — ORBIT + GROW  (orbitDuration)
            // ════════════════════════════════════════
            var main = stormParticles.main;
            main.loop          = true;
            main.startSpeed    = _savedStartSpeed;
            main.startSize     = _savedStartSize;
            main.startLifetime = _savedStartLifetime;
            stormParticles.Play();

            float orbitTime = 0f;
            float baseAngle  = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float selfRotA   = 0f;
            float selfRotB   = 0f;

            while (orbitTime < orbitDuration)
            {
                orbitTime += Time.deltaTime;
                float t     = Mathf.Clamp01(orbitTime / orbitDuration);
                float easeT = EaseInOutCubic(t);

                // Spiral inward
                float radius = Mathf.Lerp(orbitRadiusStart, orbitRadiusEnd * 1.4f, t * t);

                // Grow to orbitScaleTarget
                float iconScale = Mathf.Lerp(1.0f, orbitScaleTarget, easeT);
                IconRectA.localScale = Vector3.one * iconScale;
                IconRectB.localScale = Vector3.one * iconScale;

                // Self-spin (accelerates slowly)
                float spinSpeed = Mathf.Lerp(40f, 160f, t);
                selfRotA += spinSpeed * Time.deltaTime;
                selfRotB -= spinSpeed * Time.deltaTime;
                IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);

                // Orbital offset
                float ang = baseAngle + easeT * orbitTurns * Mathf.PI * 2f;
                Vector2 off = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                IconRectA.anchoredPosition =  off;
                IconRectB.anchoredPosition = -off;

                // Glow: fade in, pulse
                float glowAlpha = Mathf.Lerp(0f, 0.65f, t) + 0.2f * Mathf.Sin(orbitTime * 6f);
                float glowScale = iconScale * glowSizeMultiplier;
                SetGlowPos(glowImageA,  off, IconRectA.localRotation, Vector3.one * glowScale);
                SetGlowPos(glowImageB, -off, IconRectB.localRotation, Vector3.one * glowScale);
                SetAlpha(glowImageA, Mathf.Clamp01(glowAlpha));
                SetAlpha(glowImageB, Mathf.Clamp01(glowAlpha));

                // Icon alpha pulse
                float alphaPulse = 0.85f + 0.15f * Mathf.Sin(orbitTime * iconGlowPulseSpeed);
                SetAlpha(iconImageA, alphaPulse);
                SetAlpha(iconImageB, alphaPulse);

                // Particles
                var shape    = stormParticles.shape;
                shape.radius = radius * 0.8f;
                var emission = stormParticles.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(30f, 160f, t);

                // Pre-flash buildup in last 15%
                if (t > 0.85f)
                    SetFlashAlpha(((t - 0.85f) / 0.15f) * 0.18f);

                yield return null;
            }

            // ════════════════════════════════════════
            //  PHASE 2 — SMASH / COLLISION  (mergeDuration)
            // ════════════════════════════════════════
            stormParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            Vector2 aOrbitalStart = IconRectA.anchoredPosition;
            Vector2 bOrbitalStart = IconRectB.anchoredPosition;
            float   smashTime     = 0f;
            bool    impactFired   = false;

            while (smashTime < mergeDuration)
            {
                smashTime += Time.deltaTime;
                float t = Mathf.Clamp01(smashTime / mergeDuration);

                // ── APPROACH (0 → impactFraction) ──
                if (t <= smashImpactFraction)
                {
                    float rt = t / smashImpactFraction;
                    float rushT = rt * rt * rt; // ease-in: slow start, violent acceleration
                    IconRectA.anchoredPosition = Vector2.Lerp(aOrbitalStart, Vector2.zero, rushT);
                    IconRectB.anchoredPosition = Vector2.Lerp(bOrbitalStart, Vector2.zero, rushT);

                    float spinSpeed = Mathf.Lerp(160f, 360f, rt);
                    selfRotA += spinSpeed * Time.deltaTime;
                    selfRotB -= spinSpeed * Time.deltaTime;
                    IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                    IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);
                }
                // ── IMPACT + REBOUND (impactFraction → 1) ──
                else
                {
                    if (!impactFired)
                    {
                        impactFired = true;
                        OnImpact?.Invoke();
                        SetFlashAlpha(flashMaxAlpha);

                        // Burst
                        var bm = stormParticles.main;
                        bm.startSpeed    = _savedStartSpeed * 5f;
                        bm.startSize     = _savedStartSize  * 1.8f;
                        bm.startLifetime = 0.5f;
                        stormParticles.Emit(mergeBurstCount * 2);

                        // Continuous blast after impact
                        bm.loop          = true;
                        bm.startSpeed    = _savedStartSpeed * 4f;
                        bm.startSize     = _savedStartSize  * 1.6f;
                        bm.startLifetime = 0.55f;
                        var be = stormParticles.emission;
                        be.rateOverTimeMultiplier = 220f;
                        var bs = stormParticles.shape;
                        bs.radius = orbitRadiusEnd * 0.5f;
                        stormParticles.Play();
                    }

                    // Post-impact orbit (tight orbit, high speed)
                    float prt    = (t - smashImpactFraction) / (1f - smashImpactFraction);
                    float rRadius = Mathf.Lerp(orbitRadiusEnd * 0.5f, orbitRadiusEnd * 0.9f, EaseOutQuad(prt));
                    float rAngle  = baseAngle + t * orbitTurns * 2f * Mathf.PI;
                    Vector2 rOff  = new Vector2(Mathf.Cos(rAngle), Mathf.Sin(rAngle)) * rRadius;
                    IconRectA.anchoredPosition =  rOff;
                    IconRectB.anchoredPosition = -rOff;

                    // Continue spinning post-impact
                    selfRotA += 360f * Time.deltaTime;
                    selfRotB -= 360f * Time.deltaTime;
                    IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                    IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);

                    // Flash fades quickly
                    SetFlashAlpha(Mathf.Lerp(flashMaxAlpha, 0f, Mathf.Clamp01(prt * 4f)));
                }

                // Scale stays at 2.5x throughout smash
                IconRectA.localScale = Vector3.one * orbitScaleTarget;
                IconRectB.localScale = Vector3.one * orbitScaleTarget;

                // Glow: bright and pulsing, stays throughout
                float gPulse = 0.7f + 0.2f * Mathf.Sin(smashTime * 12f);
                float gScale = orbitScaleTarget * glowSizeMultiplier;
                SetGlowPos(glowImageA, IconRectA.anchoredPosition, IconRectA.localRotation, Vector3.one * gScale);
                SetGlowPos(glowImageB, IconRectB.anchoredPosition, IconRectB.localRotation, Vector3.one * gScale);
                SetAlpha(glowImageA, Mathf.Clamp01(gPulse));
                SetAlpha(glowImageB, Mathf.Clamp01(gPulse));
                SetAlpha(iconImageA, 1f);
                SetAlpha(iconImageB, 1f);

                yield return null;
            }

            // ════════════════════════════════════════
            //  PHASE 3 — RADIAL WAVE + CONTINUED SPIN  (radialClearDuration)
            // ════════════════════════════════════════

            // Switch particles to nuclear-blast mode
            var blastMain = stormParticles.main;
            blastMain.loop          = true;
            blastMain.startSpeed    = _savedStartSpeed * 5f;
            blastMain.startSize     = _savedStartSize  * 1.8f;
            blastMain.startLifetime = 0.6f;
            var blastShape = stormParticles.shape;
            blastShape.shapeType = ParticleSystemShapeType.Circle;
            blastShape.radius = 0.1f;
            var blastEmit = stormParticles.emission;
            blastEmit.rateOverTimeMultiplier = 250f;
            stormParticles.Play();

            if (shockwaveImage != null)
            {
                shockwaveImage.color = shockwaveColor;
                SetAlpha(shockwaveImage, shockwaveColor.a);
                shockwaveImage.rectTransform.localScale = Vector3.one * shockwaveStartScale;
            }

            float clearTime = 0f;
            float lastReportedRadius = -1f;

            while (clearTime < radialClearDuration)
            {
                clearTime += Time.deltaTime;
                float t       = Mathf.Clamp01(clearTime / radialClearDuration);
                float expandT = EaseOutQuad(t);

                // Icons: spiral toward center while continuing to spin
                float postRadius = Mathf.Lerp(orbitRadiusEnd * 0.9f, 0f, EaseInOutCubic(t));
                float postAngle  = (selfRotA * Mathf.Deg2Rad) * 0.5f;
                Vector2 postOff  = new Vector2(Mathf.Cos(postAngle), Mathf.Sin(postAngle)) * postRadius;
                IconRectA.anchoredPosition =  postOff;
                IconRectB.anchoredPosition = -postOff;

                float spinDecay = Mathf.Lerp(360f, 45f, t);
                selfRotA += spinDecay * Time.deltaTime;
                selfRotB -= spinDecay * Time.deltaTime;
                IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);

                // Icons + glow fade out as wave passes
                float iconAlpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, t));
                SetAlpha(iconImageA, iconAlpha);
                SetAlpha(iconImageB, iconAlpha);
                float gAlpha = iconAlpha * 0.65f;
                float gScalePost = orbitScaleTarget * glowSizeMultiplier;
                SetGlowPos(glowImageA,  postOff, IconRectA.localRotation, Vector3.one * gScalePost);
                SetGlowPos(glowImageB, -postOff, IconRectB.localRotation, Vector3.one * gScalePost);
                SetAlpha(glowImageA, gAlpha);
                SetAlpha(glowImageB, gAlpha);

                // Shockwave ring expands (faster, more prominent)
                if (shockwaveImage != null)
                {
                    float ringScale = Mathf.Lerp(shockwaveStartScale, shockwaveMaxScale, expandT);
                    shockwaveImage.rectTransform.localScale = Vector3.one * ringScale;
                    float ringAlpha = Mathf.Lerp(shockwaveColor.a, 0f, t * t * 0.8f);
                    SetAlpha(shockwaveImage, Mathf.Clamp01(ringAlpha));
                }

                // Wavy glow fills the board from center outward (synchronized with tile clearing)
                if (waveGlowImage != null)
                {
                    // Slight scale wobble for organic/wavy edge feel
                    float wobble = 1f + 0.04f * Mathf.Sin(clearTime * 28f);
                    float glowScale = Mathf.Lerp(shockwaveStartScale, shockwaveMaxScale * 0.88f, expandT) * wobble;
                    waveGlowImage.rectTransform.localScale = Vector3.one * glowScale;
                    // Fade in fast, then linger and fade out
                    float glowAlpha = waveGlowColor.a * Mathf.Clamp01(expandT * 3f) * (1f - t * 0.7f);
                    SetAlpha(waveGlowImage, Mathf.Clamp01(glowAlpha));
                }

                // Particle blast expands with wave
                float blastRadius = Mathf.Lerp(0.1f, orbitRadiusStart * 2.5f, expandT);
                var bShape = stormParticles.shape;
                bShape.radius = blastRadius;
                var bEmit = stormParticles.emission;
                bEmit.rateOverTimeMultiplier = Mathf.Lerp(250f, 20f, t);
                var bMain2 = stormParticles.main;
                bMain2.startSpeed = Mathf.Lerp(_savedStartSpeed * 5f, _savedStartSpeed * 1.2f, t);

                // Board clear progress callback
                if (expandT - lastReportedRadius >= 0.04f)
                {
                    lastReportedRadius = expandT;
                    OnRadialClearProgress?.Invoke(expandT);
                }

                yield return null;
            }

            stormParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            SetFlashAlpha(0f);
            SetAlpha(iconImageA, 0f);
            SetAlpha(iconImageB, 0f);
            SetAlpha(glowImageA, 0f);
            SetAlpha(glowImageB, 0f);
            if (shockwaveImage != null) SetAlpha(shockwaveImage, 0f);
            if (waveGlowImage  != null) SetAlpha(waveGlowImage, 0f);
            OnRadialClearProgress?.Invoke(1f);

            // ════════════════════════════════════════
            //  PHASE 4 — FADE OUT
            // ════════════════════════════════════════
            float fadeTime = 0f;
            while (fadeTime < fadeOutDuration)
            {
                fadeTime += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(fadeTime / fadeOutDuration));
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
                Debug.LogError("[OverrideComboController] Coroutine died unexpectedly — cleaned up.");
        }
    }

    // ─────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────
    private void SetGlowPos(Image glow, Vector2 pos, Quaternion rot, Vector3 scale)
    {
        if (glow == null) return;
        var rt = glow.rectTransform;
        rt.anchoredPosition = pos;
        rt.localRotation    = rot;
        rt.localScale       = scale;
    }

    private void SetFlashAlpha(float a)
    {
        if (centerFlash == null) return;
        var c = centerFlash.color; c.a = a; centerFlash.color = c;
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }

    // ─────────────────────────────────────────
    //  STORM HELPERS
    // ─────────────────────────────────────────
    private void SaveStormDefaults()
    {
        if (stormParticles == null) return;
        var m = stormParticles.main;
        _savedEmissionRate = stormParticles.emission.rateOverTimeMultiplier;
        _savedStartSpeed   = m.startSpeed.constant;
        _savedStartSize    = m.startSize.constant;
        _savedStartLifetime = m.startLifetime.constant;
        _savedLoop = m.loop;
    }

    private void RestoreStormDefaults()
    {
        if (stormParticles == null) return;
        var m = stormParticles.main;
        m.startSpeed    = _savedStartSpeed;
        m.startSize     = _savedStartSize;
        m.startLifetime = _savedStartLifetime;
        m.loop = _savedLoop;
        var em = stormParticles.emission;
        em.rateOverTimeMultiplier = _savedEmissionRate;
    }

    private void StormStop(bool clear)
    {
        if (stormParticles == null) return;
        stormParticles.Stop(true, clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting);
    }

    // ─────────────────────────────────────────
    //  EASING
    // ─────────────────────────────────────────
    private static float EaseInOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseInOutCubicUnclamped(float t) => EaseInOutCubic(Mathf.Clamp01(t));
}
