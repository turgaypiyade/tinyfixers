using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Override + Override combo VFX controller.
///
/// PHASES:
///   1) LIFT   - Icons grow to 1.25x, lift out of their cells, separate slightly, and self-spin fast.
///   2) SMASH  - Icons rush together and collide violently.
///   3) WAVE   - A radial ray burst expands from center. Board tiles clear IN SYNC with the wave.
///               Icons fade at the impact point while the wave rolls out.
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

    [Header("Radial Clear")]
    [SerializeField] private Image shockwaveImage;
    [SerializeField] private float shockwaveMaxScale = 25f;
    [SerializeField] private float shockwaveStartScale = 0.1f;

    // ─────────────────────────────────────────
    //  TIMINGS
    // ─────────────────────────────────────────
    [Header("Phase Timings")]
    [SerializeField] private float orbitDuration     = 0.55f;  // lift / separation phase
    [SerializeField] private float mergeDuration     = 0.15f;  // smash / collision phase
    [SerializeField] private float radialClearDuration = 1.50f; // wave phase
    [SerializeField] private float fadeOutDuration   = 0.15f;

    // ─────────────────────────────────────────
    //  LIFT / SMASH SETTINGS
    // ─────────────────────────────────────────
    [Header("Lift / Smash")]
    [SerializeField] private float orbitRadiusStart  = 130f;
    [SerializeField] private float orbitRadiusEnd    = 50f;
    [SerializeField] private float liftDistance      = 32f;
    [SerializeField] private float separationDistance = 72f;
    [SerializeField] private float liftSpinTurns     = 3.5f;
    [SerializeField] private float smashSpinTurns    = 3.0f;
    [SerializeField] private float impactPulseScale  = 1.42f;
    [SerializeField] private float orbitTurns        = 3.5f;
    [SerializeField] private float orbitScaleTarget  = 1.25f;  // grow to 1.25x while lifting
    [SerializeField] private float iconGlowPulseSpeed = 8f;
    [SerializeField] private float iconSize          = 80f;

    [Header("Glow")]
    [SerializeField] private Color glowColor         = new Color(1f, 0.72f, 0.18f, 0.65f);
    [SerializeField] private float glowSizeMultiplier = 1.85f;

    [Header("Smash / Merge")]
    [SerializeField] private float smashImpactFraction = 0.22f; // 0..1 - flash buildup start during smash
    [SerializeField] private float flashMaxAlpha     = 0.95f;

    [Header("Shockwave")]
    [SerializeField] private Color shockwaveColor    = new Color(1f, 0.85f, 0.3f, 0.75f);

    [Header("Wave Glow")]
    [SerializeField] private Sprite waveGlowSprite;
    [SerializeField] private Color  waveGlowColor    = new Color(1f, 0.85f, 0.3f, 0.40f);

    [Header("Blast Rays")]
    [SerializeField] private int blastRayCount = 20;
    [SerializeField] private float blastRayThickness = 10f;
    [SerializeField] private float blastRayEndpointSize = 38f;
    [SerializeField] private Color blastRayColor = new Color(1f, 0.92f, 0.45f, 1f);
    // Halka çizgilerle büyür AMA bu yarıçapı GEÇMEZ. Çizgiler ekran dışına taşabilir (iç kısımları görünür)
    // ama halka SADECE dış çemberde yaşadığı için board dışına çıkarsa TAMAMEN görünmez. Board içinde tut.
    [SerializeField] private float blastHaloMaxRadius = 420f;
    // Halka kalınlık/bulutumsuluk:
    [SerializeField] private float blastHaloThickness = 0.7f;
    [SerializeField] private int blastHaloSegmentCount = 128;
    [SerializeField] private float blastHaloSegmentThickness = 12f;
    [SerializeField] private Color blastHaloColor = new Color(1f, 0.92f, 0.5f, 1f);

    // ─────────────────────────────────────────
    //  CALLBACKS
    // ─────────────────────────────────────────
    public event Action<float> OnRadialClearProgress;
    public event Action        OnComboFinished;
    public event Action        OnImpact;
    public event Action<float> OnWaveRadiusChanged;

    // ─────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────
    private Coroutine _routine;

    // Dalga cephesinin varacağı yarıçap (px). BoardVfxService her oynatmada origin'den en uzak
    // board köşesine göre set eder; set edilmezse blastHaloMaxRadius kullanılır.
    private float waveMaxRadiusOverride = -1f;

    private float _savedEmissionRate;
    private float _savedStartSpeed;
    private float _savedStartSize;
    private float _savedStartLifetime;
    private bool  _savedLoop;

    private Image glowImageA;
    private Image glowImageB;
    private Image waveGlowImage;
    private Image blastHaloImage;
    private Image[] blastHaloSegments;
    private Image[] blastRayImages;
    private Image[] blastRayCaps;
    private Vector2 _startOffsetA;
    private Vector2 _startOffsetB;
    private bool _hasPreparedStartOffsets;
    private static Sprite solidSprite;
    private static Sprite circleSprite;
    private static Sprite softRingSprite;

    private RectTransform IconRectA  => iconImageA.rectTransform;
    private RectTransform IconRectB  => iconImageB.rectTransform;

    // ─────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Unity Prefab serialize edilmiş değerleri ezmek için kodda zorluyoruz:
        orbitDuration = 0.55f;
        mergeDuration = 0.15f;
        radialClearDuration = 1.50f;
        fadeOutDuration = 0.15f;
    }
    
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

    /// <summary>Dalga cephesinin varış yarıçapı (px, board uzayında). Play'den ÖNCE çağır.</summary>
    public void SetWaveMaxRadius(float radius) => waveMaxRadiusOverride = radius;

    private float WaveMaxRadius => waveMaxRadiusOverride > 0f
        ? waveMaxRadiusOverride
        : Mathf.Max(8f, blastHaloMaxRadius);

    // ─────────────────────────────────────────
    //  AUTO-CREATE ICONS
    // ─────────────────────────────────────────
    private Image EnsureIconImage(ref Image field, string objectName)
    {
        if (field != null) return field;
        var go  = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(pivot, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        var img = go.AddComponent<Image>();
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
            var go  = new GameObject("WaveGlow_Auto", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(pivot, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.one * iconSize;
            rt.localScale       = Vector3.one;
            waveGlowImage = go.AddComponent<Image>();
            waveGlowImage.raycastTarget = false;
            waveGlowImage.color = waveGlowColor;
            // Inspector'dan sprite atandıysa onu kullan, yoksa shockwave sprite'ı dene
            if (waveGlowSprite != null)
                waveGlowImage.sprite = waveGlowSprite;
            else if (shockwaveImage != null && shockwaveImage.sprite != null)
                waveGlowImage.sprite = shockwaveImage.sprite;
            go.transform.SetSiblingIndex(0);
        }

        if (blastHaloImage == null)
            blastHaloImage = CreateBlastImage("BlastHalo_Auto", GetSoftRingSprite());

        EnsureBlastRays();
        EnsureBlastHaloSegments();
        BringBlastHaloToFront();
    }

    private Image CreateGlowBehind(Image anchor, string name)
    {
        var go  = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(pivot, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = glowColor;
        // Place BEHIND the anchor icon in sibling order
        if (anchor != null)
            go.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex());
        return img;
    }

    private void EnsureBlastRays()
    {
        int count = Mathf.Max(0, blastRayCount);
        if (count == 0)
            return;

        if (blastRayImages != null && blastRayCaps != null && blastRayImages.Length == count && blastRayCaps.Length == count)
            return;

        blastRayImages = new Image[count];
        blastRayCaps = new Image[count];
        for (int i = 0; i < count; i++)
        {
            blastRayImages[i] = CreateBlastImage($"BlastRay_{i:00}", GetSolidSprite());
            blastRayCaps[i] = CreateBlastImage($"BlastCap_{i:00}", GetCircleSprite());
        }
    }

    private void EnsureBlastHaloSegments()
    {
        int count = Mathf.Max(0, blastHaloSegmentCount);
        if (count == 0)
            return;

        if (blastHaloSegments != null && blastHaloSegments.Length == count)
            return;

        blastHaloSegments = new Image[count];
        for (int i = 0; i < count; i++)
            blastHaloSegments[i] = CreateBlastImage($"BlastHaloSegment_{i:00}", GetSolidSprite());
    }

    private void BringBlastHaloToFront()
    {
        // Soft halo + crisp segment ring en üstte dursun; ray'ler parlaksa halkayı yutmasın.
        if (blastHaloImage != null)
            blastHaloImage.transform.SetAsLastSibling();

        if (blastHaloSegments == null)
            return;

        for (int i = 0; i < blastHaloSegments.Length; i++)
        {
            if (blastHaloSegments[i] != null)
                blastHaloSegments[i].transform.SetAsLastSibling();
        }
    }

    private Image CreateBlastImage(string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(pivot, false);
        go.transform.SetSiblingIndex(Mathf.Min(1, pivot.childCount - 1));

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = WithAlpha(blastRayColor, 0f);
        return img;
    }

    // ─────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────
    public void PlayAtAnchoredPosition(Vector2 anchoredPos, Sprite sprA, Sprite sprB, Sprite merged = null)
    {
        var rt = transform as RectTransform;
        if (rt != null) rt.anchoredPosition = anchoredPos;
        _startOffsetA = Vector2.zero;
        _startOffsetB = Vector2.zero;
        _hasPreparedStartOffsets = true;
        Play(sprA, sprB, merged);
    }

    public void PlayAtAnchoredPositions(Vector2 anchoredA, Vector2 anchoredB, Vector2 epicenter, Sprite sprA, Sprite sprB, Sprite merged = null)
    {
        var rt = transform as RectTransform;
        if (rt != null) rt.anchoredPosition = epicenter;
        _startOffsetA = anchoredA - epicenter;
        _startOffsetB = anchoredB - epicenter;
        _hasPreparedStartOffsets = true;
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
        if (!_hasPreparedStartOffsets)
        {
            _startOffsetA = Vector2.zero;
            _startOffsetB = Vector2.zero;
        }
        _hasPreparedStartOffsets = false;

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

        // Halka sprite'ını TAZE üret ve yeniden ata → blastHaloThickness değişiklikleri her oynatmada
        // uygulansın (static cache + domain-reload-off durumunda eski ince sprite'a takılıp kalmasın).
        softRingSprite = null;
        if (blastHaloImage != null) blastHaloImage.sprite = GetSoftRingSprite();

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
            if (blastHaloImage != null)
            {
                blastHaloImage.color = WithAlpha(blastHaloColor, 0f);
                blastHaloImage.rectTransform.sizeDelta = Vector2.zero;
                blastHaloImage.rectTransform.localScale = Vector3.one;
            }
            HideBlastRays();

            SetAlpha(iconImageA, 1f);
            SetAlpha(iconImageB, 1f);
            IconRectA.localScale    = Vector3.one;
            IconRectB.localScale    = Vector3.one;
            IconRectA.localRotation = Quaternion.identity;
            IconRectB.localRotation = Quaternion.identity;
            IconRectA.anchoredPosition = _startOffsetA;
            IconRectB.anchoredPosition = _startOffsetB;

            SetAlpha(glowImageA, 0f);
            SetAlpha(glowImageB, 0f);
            SetGlowPos(glowImageA, _startOffsetA, Quaternion.identity, Vector3.one);
            SetGlowPos(glowImageB, _startOffsetB, Quaternion.identity, Vector3.one);

            SetFlashAlpha(0f);
            SaveStormDefaults();
            StormStop(true);

            // ════════════════════════════════════════
            //  PHASE 1 — LIFT + SEPARATE + FAST SELF-SPIN  (orbitDuration)
            // ════════════════════════════════════════
            var main = stormParticles.main;
            main.loop          = true;
            main.startSpeed    = _savedStartSpeed;
            main.startSize     = _savedStartSize;
            main.startLifetime = _savedStartLifetime;
            stormParticles.Play();

            float orbitTime = 0f;
            float selfRotA   = 0f;
            float selfRotB   = 0f;
            Vector2 awayA = ResolveAwayDirection(_startOffsetA, Vector2.left);
            Vector2 awayB = ResolveAwayDirection(_startOffsetB, Vector2.right);
            Vector2 aLiftEnd = _startOffsetA + Vector2.up * liftDistance + awayA * separationDistance;
            Vector2 bLiftEnd = _startOffsetB + Vector2.up * liftDistance + awayB * separationDistance;

            while (orbitTime < orbitDuration)
            {
                orbitTime += Time.deltaTime;
                float t     = Mathf.Clamp01(orbitTime / orbitDuration);
                float easeT = EaseInOutCubic(t);

                // Rise out of the cells and drift apart before the collision.
                Vector2 aPos = Vector2.Lerp(_startOffsetA, aLiftEnd, easeT);
                Vector2 bPos = Vector2.Lerp(_startOffsetB, bLiftEnd, easeT);
                IconRectA.anchoredPosition = aPos;
                IconRectB.anchoredPosition = bPos;

                float iconScale = Mathf.Lerp(1.0f, orbitScaleTarget, easeT);
                IconRectA.localScale = Vector3.one * iconScale;
                IconRectB.localScale = Vector3.one * iconScale;

                // Strong self-spin sells the spherical override icons lifting out of the grid.
                float spinT = EaseOutQuad(t);
                selfRotA = spinT * liftSpinTurns * 360f;
                selfRotB = -spinT * liftSpinTurns * 360f;
                IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);

                // Glow: fade in, pulse
                float glowAlpha = Mathf.Lerp(0f, 0.65f, t) + 0.2f * Mathf.Sin(orbitTime * 6f);
                float glowScale = iconScale * glowSizeMultiplier;
                SetGlowPos(glowImageA, aPos, IconRectA.localRotation, Vector3.one * glowScale);
                SetGlowPos(glowImageB, bPos, IconRectB.localRotation, Vector3.one * glowScale);
                SetAlpha(glowImageA, Mathf.Clamp01(glowAlpha));
                SetAlpha(glowImageB, Mathf.Clamp01(glowAlpha));

                // Icon alpha pulse
                float alphaPulse = 0.85f + 0.15f * Mathf.Sin(orbitTime * iconGlowPulseSpeed);
                SetAlpha(iconImageA, alphaPulse);
                SetAlpha(iconImageB, alphaPulse);

                // Particles
                var shape    = stormParticles.shape;
                shape.radius = Mathf.Lerp(8f, separationDistance, easeT);
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

            Vector2 aSmashStart = IconRectA.anchoredPosition;
            Vector2 bSmashStart = IconRectB.anchoredPosition;
            float   smashTime     = 0f;

            while (smashTime < mergeDuration)
            {
                smashTime += Time.deltaTime;
                float t = Mathf.Clamp01(smashTime / mergeDuration);
                float rushT = EaseInQuint(t);

                IconRectA.anchoredPosition = Vector2.Lerp(aSmashStart, Vector2.zero, rushT);
                IconRectB.anchoredPosition = Vector2.Lerp(bSmashStart, Vector2.zero, rushT);

                float smashSpinT = EaseOutQuad(t);
                IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA + smashSpinT * smashSpinTurns * 360f);
                IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB - smashSpinT * smashSpinTurns * 360f);

                // Scale stays at 1.25x throughout the rush.
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
                SetFlashAlpha(Mathf.Lerp(0.18f, flashMaxAlpha * 0.75f, Mathf.InverseLerp(smashImpactFraction, 1f, t)));

                yield return null;
            }

            IconRectA.anchoredPosition = Vector2.zero;
            IconRectB.anchoredPosition = Vector2.zero;
            IconRectA.localScale = Vector3.one * impactPulseScale;
            IconRectB.localScale = Vector3.one * impactPulseScale;
            SetGlowPos(glowImageA, Vector2.zero, IconRectA.localRotation, Vector3.one * impactPulseScale * glowSizeMultiplier);
            SetGlowPos(glowImageB, Vector2.zero, IconRectB.localRotation, Vector3.one * impactPulseScale * glowSizeMultiplier);
            SetAlpha(glowImageA, 0.85f);
            SetAlpha(glowImageB, 0.85f);
            OnImpact?.Invoke();
            SetFlashAlpha(flashMaxAlpha);

            var bm = stormParticles.main;
            bm.startSpeed    = _savedStartSpeed * 5f;
            bm.startSize     = _savedStartSize  * 1.8f;
            bm.startLifetime = 0.5f;
            var burstShape = stormParticles.shape;
            burstShape.shapeType = ParticleSystemShapeType.Circle;
            burstShape.radius = 0.1f;
            stormParticles.Emit(mergeBurstCount * 2);

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

            UpdateBlastRays(0f, 0f);

            float clearTime = 0f;
            float lastReportedRadius = -1f;

            while (clearTime < radialClearDuration)
            {
                clearTime += Time.deltaTime;
                float t       = Mathf.Clamp01(clearTime / radialClearDuration);
                float expandT = EaseOutQuad(t);

                // TEK doğruluk kaynağı: dalga cephesi görsel halkayla BİREBİR AYNI (EaseOutQuad) ilerler.
                float waveRadius = expandT * WaveMaxRadius;
                OnWaveRadiusChanged?.Invoke(waveRadius);

                // Icons collapse and fade at the impact point while the blast wave does the clear.
                IconRectA.anchoredPosition = Vector2.zero;
                IconRectB.anchoredPosition = Vector2.zero;

                float spinDecay = Mathf.Lerp(620f, 45f, t);
                selfRotA += spinDecay * Time.deltaTime;
                selfRotB -= spinDecay * Time.deltaTime;
                IconRectA.localRotation = Quaternion.Euler(0f, 0f, selfRotA);
                IconRectB.localRotation = Quaternion.Euler(0f, 0f, selfRotB);
                float postScale = Mathf.Lerp(impactPulseScale, orbitScaleTarget, EaseOutQuad(t));
                IconRectA.localScale = Vector3.one * postScale;
                IconRectB.localScale = Vector3.one * postScale;

                // Icons + glow fade out as wave passes (ilk %35'te tamamen kaybolur)
                float iconAlpha = 1f - Smooth01(t / 0.35f);
                SetAlpha(iconImageA, iconAlpha);
                SetAlpha(iconImageB, iconAlpha);
                float gAlpha = iconAlpha * 0.65f;
                float gScalePost = postScale * glowSizeMultiplier;
                SetGlowPos(glowImageA, Vector2.zero, IconRectA.localRotation, Vector3.one * gScalePost);
                SetGlowPos(glowImageB, Vector2.zero, IconRectB.localRotation, Vector3.one * gScalePost);
                SetAlpha(glowImageA, gAlpha);
                SetAlpha(glowImageB, gAlpha);

                // Impact flash'i dalganın ilk %25'inde TAMAMEN söndür — parlak kalırsa
                // merkezden çıkan ray/halkayı yutuyor.
                // (NOT: prefab'da shockwaveImage ve centerFlash AYNI Image — shockwave'e ayrı
                // animasyon YOK; flash alpha'sını tek yerden, buradan yönetiyoruz.)
                SetFlashAlpha(flashMaxAlpha * (1f - Smooth01(t / 0.25f)));

                // Soft fill supports the ray burst without reading as repeated rings.
                // Cepheyle senkron büyür (rect iconSize tabanlı → scale = çap / iconSize).
                if (waveGlowImage != null)
                {
                    float glowScale = (waveRadius * 2.2f) / Mathf.Max(1f, iconSize);
                    waveGlowImage.rectTransform.localScale = Vector3.one * glowScale;
                    float glowAlpha = waveGlowColor.a * 0.55f * Mathf.Clamp01(t * 3f) * (1f - t);
                    SetAlpha(waveGlowImage, Mathf.Clamp01(glowAlpha));
                }
                UpdateBlastRays(waveRadius, t);

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
            if (blastHaloImage != null) SetAlpha(blastHaloImage, 0f);
            HideBlastRays();
            OnRadialClearProgress?.Invoke(1f);
            OnWaveRadiusChanged?.Invoke(WaveMaxRadius);

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

    private void UpdateBlastRays(float waveRadius, float phaseT)
    {
        bool hasRays = blastRayImages != null && blastRayCaps != null;
        if (!hasRays)
            return;

        // Çizgi uçları TAM dalga cephesinde: cepheden ne ileri fırlar ne geride kalır.
        // Cephe zaten Co_Play'de taş temizleme delay eğrisinin tersiyle hesaplanıyor.
        float radius = Mathf.Max(8f, waveRadius);
        // Çizgiler HIZLI belirir (ilk %6), %72'ye kadar TAM parlak kalır, sonra söner.
        float alphaIn = Smooth01(phaseT / 0.06f);
        float alphaOut = 1f - Smooth01((phaseT - 0.72f) / 0.28f);
        float alpha = blastRayColor.a * alphaIn * alphaOut;

        // Halka ray uçlarıyla AYNI cephede: glow ring + çizgiler tek dalga olarak birlikte büyür.
        float haloRadius = radius;
        float haloSize = haloRadius * 2f / 0.68f;
        float haloIn = Smooth01(phaseT / 0.06f);
        float haloOut = 1f - Smooth01((phaseT - 0.85f) / 0.15f);
        float haloAlpha = haloIn * haloOut;
        UpdateBlastHalo(haloRadius, haloSize, haloAlpha, phaseT);

        float angleStep = 360f / Mathf.Max(1, blastRayImages.Length);
        for (int i = 0; i < blastRayImages.Length; i++)
        {
            float variance = 0.88f + 0.12f * Mathf.Sin(i * 12.9898f);
            float angle = (angleStep * i + phaseT * 24f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float rayLength = Mathf.Max(1f, radius * variance);
            float lineLength = Mathf.Max(1f, rayLength - blastRayEndpointSize * 0.5f);

            Image ray = blastRayImages[i];
            if (ray != null)
            {
                var rt = ray.rectTransform;
                rt.anchoredPosition = dir * (lineLength * 0.5f);
                rt.sizeDelta = new Vector2(lineLength, blastRayThickness);
                rt.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                ray.color = WithAlpha(blastRayColor, alpha);
            }

            Image cap = i < blastRayCaps.Length ? blastRayCaps[i] : null;
            if (cap != null)
            {
                var rt = cap.rectTransform;
                rt.anchoredPosition = dir * rayLength;
                rt.sizeDelta = Vector2.one * blastRayEndpointSize;
                rt.localRotation = Quaternion.identity;
                cap.color = WithAlpha(blastRayColor, alpha * 0.95f);
            }
        }
    }

    private void UpdateBlastHalo(float haloRadius, float haloSize, float haloAlpha, float phaseT)
    {
        if (blastHaloImage != null)
        {
            var haloRt = blastHaloImage.rectTransform;
            haloRt.anchoredPosition = Vector2.zero;
            haloRt.sizeDelta = Vector2.one * haloSize;
            haloRt.localRotation = Quaternion.identity;
            haloRt.localScale = Vector3.one;
            blastHaloImage.color = WithAlpha(blastHaloColor, haloAlpha * 0.75f);
        }

        if (blastHaloSegments == null || blastHaloSegments.Length == 0)
            return;

        int count = blastHaloSegments.Length;
        float angleStep = 360f / Mathf.Max(1, count);
        float circumference = 2f * Mathf.PI * Mathf.Max(1f, haloRadius);
        float segmentLength = Mathf.Max(4f, circumference / Mathf.Max(1, count) * 0.72f);
        float segmentThickness = Mathf.Max(1f, blastHaloSegmentThickness);
        Color segmentColor = WithAlpha(blastHaloColor, haloAlpha);

        for (int i = 0; i < count; i++)
        {
            Image segment = blastHaloSegments[i];
            if (segment == null)
                continue;

            float degrees = angleStep * i + phaseT * 18f;
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            var rt = segment.rectTransform;
            rt.anchoredPosition = dir * haloRadius;
            rt.sizeDelta = new Vector2(segmentLength, segmentThickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees + 90f);
            rt.localScale = Vector3.one;
            segment.color = segmentColor;
        }
    }

    private void HideBlastRays()
    {
        if (blastRayImages != null)
        {
            for (int i = 0; i < blastRayImages.Length; i++)
                SetAlpha(blastRayImages[i], 0f);
        }

        SetAlpha(blastHaloImage, 0f);

        if (blastHaloSegments != null)
        {
            for (int i = 0; i < blastHaloSegments.Length; i++)
                SetAlpha(blastHaloSegments[i], 0f);
        }

        if (blastRayCaps != null)
        {
            for (int i = 0; i < blastRayCaps.Length; i++)
                SetAlpha(blastRayCaps[i], 0f);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Vector2 ResolveAwayDirection(Vector2 offsetFromMidpoint, Vector2 fallback)
    {
        return offsetFromMidpoint.sqrMagnitude > 1f
            ? offsetFromMidpoint.normalized
            : fallback;
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite == null)
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return solidSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "OverrideComboBlastCap";
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float radius = center;
        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, alpha > 0f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply(false, true);
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    private Sprite GetSoftRingSprite()
    {
        if (softRingSprite != null)
            return softRingSprite;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "OverrideComboGlowRing";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        // Basit, hafif BULUTUMSU halka: parlak band yarıçapın ~%68'inde, iki yana GENİŞ gaussian ile
        // dağılır (keskin değil, yumuşak/nebulöz). haloSize UpdateBlastRays'te /0.68 ile bandı ray
        // uçlarına oturtuyor. Band'ın texture kenarına taşmaması için %68 (kenarda sert kesinti olmasın).
        float ringRadius = center * 0.68f;
        // Gaussian genişliği (sigma). blastHaloThickness büyüdükçe daha kalın/bulutumsu.
        // Tavan: çok kalın değerlerde bulut texture kenarına taşıp sert daire kesintisi yapmasın.
        float sigma = Mathf.Clamp(Mathf.Clamp01(blastHaloThickness) * center * 0.9f, 6f, 32f);
        var clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float delta = (distance - ringRadius) / sigma;
                // Yumuşak halka: band merkezinde parlak, dışa doğru bulut gibi söner.
                float ring = Mathf.Exp(-delta * delta);
                // İçeride çok hafif bir pus (bulutumsu his), dışa taşmaz.
                float innerHaze = Mathf.Clamp01(1f - distance / ringRadius) * 0.10f;
                float alpha = Mathf.Clamp01(ring + innerHaze);
                texture.SetPixel(x, y, alpha > 0.003f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply(false, true);
        softRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return softRingSprite;
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
    // GERÇEK smoothstep (0→1 yumuşak eşik). DİKKAT: Mathf.SmoothStep(a,b,t) bu DEĞİL —
    // o yumuşatılmış Lerp'tir; "1 - Mathf.SmoothStep(0.72,1,t)" t=0'da bile 0.28 verir ve
    // ray/halo alpha'larını baştan kısar (89 saatlik görünmezlik bug'ının kök sebebi).
    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static float EaseInOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseInQuart(float t) => t * t * t * t;

    private static float EaseInQuint(float t) => t * t * t * t * t;

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseInOutCubicUnclamped(float t) => EaseInOutCubic(Mathf.Clamp01(t));
}
