using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class OverrideSpecialView : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite topHalfSprite;
    [SerializeField] private Sprite bottomHalfSprite;
    [SerializeField] private Sprite fullSphereSprite;
    [Tooltip("Ayrı parça sprite'ları yoksa tam görselin alttan kesim oranı.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float fullSpriteSplitFromBottom = 0.4f;

    [Header("Y-axis turn frames (during existing spin)")]
    [SerializeField] private Sprite[] topTurnFrames;
    [SerializeField] private Sprite[] bottomTurnFrames;
    [Tooltip("Yalnız yeni dönüş karelerini göster; eski Z dönüşü, ayrılma ve birleşme pop'u kapalı.")]
    [SerializeField] private bool turnFramesOnly;
    [SerializeField] private Image staticIconImage;
    [Tooltip("Dönüşün ulaştığı en yüksek kare hızı; başlangıç bunun %15'i.")]
    [SerializeField, Range(0f, 144f)] private float turnFramesPerSecond = 120f;
    [SerializeField] private bool topTurnReverse = true;
    [SerializeField] private bool bottomTurnReverse;
    [Header("Charge motion")]
    [SerializeField, Range(0f, 15f)] private float rockingDegrees = 9f;
    [SerializeField] private bool chargeLightning = true;
    [SerializeField, Range(0f, 1f)] private float turnBlurStrength = 0.8f;
    [Tooltip("Trimmed frame dimensions relative to the original full-square half sprite.")]
    [SerializeField] private Vector2 topTurnSizeRatio = new Vector2(0.991228f, 0.651515f);
    [SerializeField] private Vector2 topTurnOffsetRatio = new Vector2(0.000399f, 0.169458f);
    [SerializeField] private Vector2 bottomTurnSizeRatio = new Vector2(0.961722f, 0.451356f);
    [SerializeField] private Vector2 bottomTurnOffsetRatio = new Vector2(-0.000797f, -0.264753f);

    [Header("Spin-In")]
    [Tooltip("Kaç tam tur döner (3 = 1080°).")]
    [Range(1f, 10f)]
    [SerializeField] private float spinTurns = 3f;
    [Tooltip("Spin süresi (sn).")]
    [SerializeField] private float spinDuration = 0.70f;
    [Tooltip("Spin başlangıcında yarıların Y ayrımı — tile boyutunun yüzdesi.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float spinSeparationRatio = 0.40f;

    [Header("Settle")]
    [SerializeField] private float settleDuration = 0.20f;
    [SerializeField] private float settleOvershootScale = 1.45f;

    [Header("Idle (boşta creation tekrarı)")]
    [SerializeField] private float idleDelay = 2f;

    private RectTransform rt;
    private Image topImage;
    private Image bottomImage;
    private Image fullImage;
    private Coroutine routine;
    private Coroutine idleRoutine;
    private bool topUsesTurnFrame, bottomUsesTurnFrame;
    private Vector2 canvasSize;
    private float turnFramePhase;
    private bool staticIconHidden;
    private float staticIconAlpha;
    private bool activating;
    private bool externalTurning;
    private OverrideChargeGraphic chargeGraphic;
    private bool disabling;
    private const string ChargePoolKey = "Override.ChargeLightning";

    public bool UsesTurnFramesOnly => turnFramesOnly;
    public bool IsPlaying => externalTurning || routine != null || idleRoutine != null;

    /// <summary>Copies asset references/configuration only; the combo drives both views in its own loop.</summary>
    public bool BeginComboTurn(OverrideSpecialView source, Image icon, bool opposite)
    {
        Stop();
        if (source == null || source.topTurnFrames == null || source.topTurnFrames.Length == 0
            || source.bottomTurnFrames == null || source.bottomTurnFrames.Length == 0) return false;
        topHalfSprite = source.topHalfSprite;
        bottomHalfSprite = source.bottomHalfSprite;
        fullSphereSprite = source.fullSphereSprite;
        fullSpriteSplitFromBottom = source.fullSpriteSplitFromBottom;
        topTurnFrames = source.topTurnFrames;
        bottomTurnFrames = source.bottomTurnFrames;
        topTurnReverse = opposite ? !source.topTurnReverse : source.topTurnReverse;
        bottomTurnReverse = opposite ? !source.bottomTurnReverse : source.bottomTurnReverse;
        topTurnSizeRatio = source.topTurnSizeRatio;
        topTurnOffsetRatio = source.topTurnOffsetRatio;
        bottomTurnSizeRatio = source.bottomTurnSizeRatio;
        bottomTurnOffsetRatio = source.bottomTurnOffsetRatio;
        turnFramesPerSecond = source.turnFramesPerSecond;
        spinDuration = source.spinDuration;
        rockingDegrees = source.rockingDegrees;
        turnBlurStrength = source.turnBlurStrength;
        chargeLightning = source.chargeLightning;
        staticIconImage = icon;
        turnFramesOnly = true;
        BuildChildren();
        RestoreOriginalHalves();
        externalTurning = true;
        HideStaticIcon();
        if (chargeLightning) AcquireChargeGraphic();
        return true;
    }

    public void TickComboTurn(float elapsed, float deltaTime, float alpha)
    {
        if (!externalTurning) return;
        HideStaticIcon();
        TickTurn(elapsed, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, spinDuration)),
            deltaTime, 0f, 0f, alpha, true, true);
    }

    public void EndComboTurn()
    {
        Stop();
        // This view lives in the persistent pool; do not retain the scene owner's Image.
        staticIconImage = null;
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        BuildChildren();
        HideAll();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void PlayCreation() => PlayMotion(false);

    // Fanout owns the lifetime: keep spinning at peak speed until it calls Stop.
    public void BeginActivation() => PlayMotion(true);

    private void PlayMotion(bool activation)
    {
        if (!gameObject.activeInHierarchy) return;
        Stop();
        activating = activation;
        BuildChildren();

        ConfigureHalf(topImage, topHalfSprite, true);
        ConfigureHalf(bottomImage, bottomHalfSprite, false);
        if (fullSphereSprite != null && fullImage   != null) fullImage.sprite   = fullSphereSprite;

        SyncChildSizes();
        routine = StartCoroutine(CoCreation());
    }

    private void ConfigureHalf(Image image, Sprite halfSprite, bool top)
    {
        if (image == null) return;
        image.preserveAspect = true;
        image.sprite = halfSprite != null ? halfSprite : fullSphereSprite;
        if (halfSprite != null)
        {
            image.type = Image.Type.Simple;
            return;
        }
        // Reuse the full texture and existing Images; no generated textures,
        // Sprite.Create, materials or extra objects during creation/idle repeats.
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Vertical;
        image.fillOrigin = top ? 1 : 0; // Top / Bottom
        float split = Mathf.Clamp(fullSpriteSplitFromBottom, 0.1f, 0.9f);
        image.fillAmount = top ? 1f - split : split;
    }

    /// Creation animasyonu oynamadan override olan taşlar (level'e yerleştirilmiş / pre-level inject)
    /// için idle döngüsünü başlatır. Döngü zaten çalışıyorsa dokunmaz.
    public void EnsureIdle()
    {
        if (externalTurning || routine != null || idleRoutine != null) return;
        if (idleDelay <= 0f || !gameObject.activeInHierarchy) return;
        idleRoutine = StartCoroutine(CoIdleWatch());
    }

    public void Stop()
    {
        if (routine     != null) { StopCoroutine(routine);     routine     = null; }
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        HideAll();
    }

    private void OnDisable()
    {
        // Unity is walking the parent's activation hierarchy here. A pooled child must not
        // be reparented from this callback (iOS: "Cannot set the parent ... while ...").
        disabling = true;
        try { Stop(); }
        finally { disabling = false; }
    }

    // ── Layout sync ──────────────────────────────────────────────────────────

    private void OnRectTransformDimensionsChange() => SyncChildSizes();

    private void SyncChildSizes()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (rt == null) return;
        Vector2 size = rt.sizeDelta;
        if (size.x <= 0f) size = rt.rect.size;
        if (size.x <= 0f) return;
        canvasSize = size;
        SetChildSize(topImage, topUsesTurnFrame ? Vector2.Scale(size, topTurnSizeRatio) : size);
        SetChildSize(bottomImage, bottomUsesTurnFrame ? Vector2.Scale(size, bottomTurnSizeRatio) : size);
        SetChildSize(fullImage,   size);
        if (chargeGraphic != null) chargeGraphic.rectTransform.sizeDelta = size;
    }

    private static void SetChildSize(Image img, Vector2 size)
    {
        if (img != null) img.rectTransform.sizeDelta = size;
    }

    // ── Child creation ───────────────────────────────────────────────────────

    private void BuildChildren()
    {
        if (topImage    == null) topImage    = MakeImage("OvTop");
        if (bottomImage == null) bottomImage = MakeImage("OvBottom");
        if (fullImage   == null) fullImage   = MakeImage("OvFull");
    }

    private Image MakeImage(string childName)
    {
        var go = new GameObject(childName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        var irt = go.GetComponent<RectTransform>();
        irt.anchorMin        = new Vector2(0.5f, 0.5f);
        irt.anchorMax        = new Vector2(0.5f, 0.5f);
        irt.pivot            = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.localScale       = Vector3.one;
        irt.localRotation    = Quaternion.identity;

        var img = go.GetComponent<Image>();
        img.raycastTarget  = false;
        img.preserveAspect = true;
        img.color          = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    // ── Hide ─────────────────────────────────────────────────────────────────

    private void HideAll()
    {
        externalTurning = false;
        activating = false;
        ReleaseChargeGraphic();
        RestoreStaticIcon();
        topUsesTurnFrame = bottomUsesTurnFrame = false;
        turnFramePhase = 0f;
        ResetHalf(topImage);
        ResetHalf(bottomImage);
        RestoreOriginalHalves();
        if (fullImage != null) { fullImage.color = new Color(1f,1f,1f,0f); fullImage.rectTransform.localScale = Vector3.one; }
    }

    private static void ResetHalf(Image img)
    {
        if (img == null) return;
        img.color = new Color(1f, 1f, 1f, 0f);
        img.rectTransform.anchoredPosition = Vector2.zero;
        img.rectTransform.localEulerAngles = Vector3.zero;
    }

    // ── Animation ────────────────────────────────────────────────────────────

    private IEnumerator CoCreation()
    {
        yield return null;
        SyncChildSizes();

        // ── Phase 1: Spin-In ─────────────────────────────────────────────────
        // Top CW (sağa): +turns*360 → 0
        // Bottom CCW (sola): -turns*360 → 0
        // Legacy Z spin eases out; the sprite turn accelerates independently.
        bool framesOnly = turnFramesOnly;
        float totalAngle = framesOnly ? 0f : spinTurns * 360f;
        float sep = framesOnly ? 0f : rt.sizeDelta.y * spinSeparationRatio;

        float t = 0f;
        turnFramePhase = 0f;
        if (framesOnly && chargeLightning) AcquireChargeGraphic();
        while (activating || t < spinDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, spinDuration));
            float e = 1f - Mathf.Pow(1f - k, 3f);   // ease-out cubic
            float angle = Mathf.Lerp(totalAngle, 0f, e);
            float yOff  = Mathf.Lerp(sep, 0f, e);
            float alpha = framesOnly ? 1f : Mathf.Clamp01(k * 4f);
            // The ordinary Icon may have its own raised Canvas. Hide its pixels during the
            // isolated frame preview while leaving the Image/raycast target enabled.
            if (framesOnly) HideStaticIcon();

            TickTurn(t, k, Time.deltaTime, yOff, angle, alpha, framesOnly, activating);
            yield return null;
        }
        if (framesOnly)
        {
            HideAll();
            routine = null;
            if (idleDelay > 0f && gameObject.activeInHierarchy)
                idleRoutine = StartCoroutine(CoIdleWatch());
            yield break;
        }
        RestoreOriginalHalves();
        SpinHalf(topImage,    0f, 0f, 1f);
        SpinHalf(bottomImage, 0f, 0f, 1f);

        yield return null;  // 1 frame birleşik dur

        // ── Phase 2: Merge — yarılar kaybolur, tam küre büyük çıkar (flash = scale) ──
        if (topImage    != null) topImage.color    = new Color(1f,1f,1f,0f);
        if (bottomImage != null) bottomImage.color = new Color(1f,1f,1f,0f);

        if (fullImage != null)
        {
            fullImage.color                    = Color.white;
            fullImage.rectTransform.localScale = Vector3.one * settleOvershootScale;
        }

        // ── Phase 3: Settle bounce — büyükten normale döner ───────────────────
        t = 0f;
        while (t < settleDuration)
        {
            t += Time.deltaTime;
            float sk = Mathf.Clamp01(t / settleDuration);
            float e  = 1f - (1f - sk) * (1f - sk);
            float s  = Mathf.Lerp(settleOvershootScale, 1f, e);

            if (fullImage != null)
                fullImage.rectTransform.localScale = Vector3.one * s;

            yield return null;
        }

        if (fullImage != null) { fullImage.color = Color.white; fullImage.rectTransform.localScale = Vector3.one; }

        routine = null;

        if (idleDelay > 0f && gameObject.activeInHierarchy)
            idleRoutine = StartCoroutine(CoIdleWatch());
    }

    private IEnumerator CoIdleWatch()
    {
        yield return new WaitForSeconds(idleDelay);
        idleRoutine = null;
        if (gameObject.activeInHierarchy)
            PlayCreation();
    }

    private void HideStaticIcon()
    {
        if (staticIconImage == null) return;
        if (!staticIconHidden)
        {
            staticIconAlpha = staticIconImage.color.a;
            staticIconHidden = true;
        }
        var color = staticIconImage.color;
        color.a = 0f;
        staticIconImage.color = color;
    }

    private void RestoreStaticIcon()
    {
        if (!staticIconHidden) return;
        if (staticIconImage != null)
        {
            var color = staticIconImage.color;
            color.a = staticIconAlpha;
            staticIconImage.color = color;
        }
        staticIconHidden = false;
    }

    private void ApplyTurnFrames(float phase)
    {
        if (turnFramesPerSecond <= 0f) return;
        ApplyTurnFrame(topImage, topTurnFrames, phase, topTurnReverse, true);
        ApplyTurnFrame(bottomImage, bottomTurnFrames, phase, bottomTurnReverse, false);
    }

    private float TurnFrameRate(float progress)
    {
        // Accelerate from a readable crawl to full speed over the first half, then hold
        // that speed briefly so the fast finish is visible instead of ending on the ramp.
        float ramp = Mathf.Clamp01(progress / 0.5f);
        return turnFramesPerSecond * Mathf.Lerp(0.15f, 1f, ramp * ramp);
    }

    private void TickTurn(float elapsed, float progress, float deltaTime, float yOff,
        float angle, float alpha, bool framesOnly, bool sustained)
    {
        // Shared by solo and combo. Never restart this phase at the separation/smash boundary.
        ApplyTurnFrames(turnFramePhase);
        float frameRate = TurnFrameRate(progress);
        turnFramePhase += Mathf.Min(deltaTime * frameRate, 2f);
        float rock = framesOnly ? ChargeRock(elapsed, progress, sustained) : 0f;
        SpinHalf(topImage, yOff, angle + rock, alpha);
        SpinHalf(bottomImage, -yOff, -angle + rock, alpha);
        if (chargeGraphic == null) return;
        int count = topTurnFrames != null ? topTurnFrames.Length : 0;
        float turns = count > 0 ? turnFramePhase / count : 0f;
        chargeGraphic.SetCharge(elapsed, progress, sustained,
            topTurnReverse ? -turns : turns, rock, topUsesTurnFrame,
            Mathf.Clamp01((frameRate / 120f - 0.25f) / 0.5f) * turnBlurStrength, alpha);
    }

    private float ChargeRock(float elapsed, float progress, bool sustained)
    {
        float envelope = Mathf.Min(1f, progress * 5f);
        if (!sustained) envelope *= Mathf.Sin(progress * Mathf.PI);
        return Mathf.Sin(elapsed * 17f) * rockingDegrees * envelope;
    }

    private void AcquireChargeGraphic()
    {
        var go = UiVfxPool.RentRect(ChargePoolKey, transform, "OvChargeLightning");
        if (go == null) return;
        chargeGraphic = go.GetComponent<OverrideChargeGraphic>();
        if (chargeGraphic == null) chargeGraphic = go.AddComponent<OverrideChargeGraphic>();
        chargeGraphic.raycastTarget = false;
        chargeGraphic.rectTransform.sizeDelta = canvasSize;
        chargeGraphic.ResetCharge();
    }

    private void ReleaseChargeGraphic()
    {
        if (chargeGraphic == null) return;
        var graphic = chargeGraphic;
        graphic.ResetCharge();
        // Keep the invisible child with this pooled view until its next explicit Stop/Play.
        // If the owner is destroyed, Unity destroys the child too. No deferred job/root needed.
        if (disabling) return;
        chargeGraphic = null;
        UiVfxPool.Return(ChargePoolKey, graphic.gameObject, 32);
    }

    private void ApplyTurnFrame(Image image, Sprite[] frames, float phase, bool reverse, bool top)
    {
        if (image == null || frames == null || frames.Length == 0) return;
        int step = Mathf.FloorToInt(phase) % frames.Length;
        int index = reverse ? (frames.Length - step) % frames.Length : step;
        var sprite = frames[index];
        if (sprite == null) return;
        bool alreadyUsingFrames = top ? topUsesTurnFrame : bottomUsesTurnFrame;
        if (alreadyUsingFrames && image.sprite == sprite) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        // Every imported frame is registered to the same original half-canvas bounds.
        image.preserveAspect = false;
        if (top) topUsesTurnFrame = true; else bottomUsesTurnFrame = true;
        SetChildSize(image, Vector2.Scale(canvasSize, top ? topTurnSizeRatio : bottomTurnSizeRatio));
    }

    private void RestoreOriginalHalves()
    {
        topUsesTurnFrame = bottomUsesTurnFrame = false;
        ConfigureHalf(topImage, topHalfSprite, true);
        ConfigureHalf(bottomImage, bottomHalfSprite, false);
        if (topImage != null) topImage.preserveAspect = true;
        if (bottomImage != null) bottomImage.preserveAspect = true;
        SyncChildSizes();
    }

    private void SpinHalf(Image img, float yOffset, float zAngle, float alpha)
    {
        if (img == null) return;
        bool usingFrames = img == topImage ? topUsesTurnFrame : bottomUsesTurnFrame;
        Vector2 offset = usingFrames
            ? Vector2.Scale(canvasSize, img == topImage ? topTurnOffsetRatio : bottomTurnOffsetRatio)
            : Vector2.zero;
        // Orbit the trimmed centre around the original full-sphere pivot, preserving the Z spin.
        var rotation = Quaternion.Euler(0f, 0f, zAngle);
        img.rectTransform.anchoredPosition = new Vector2(0f, yOffset) + (Vector2)(rotation * (Vector3)offset);
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, zAngle);
        img.color = new Color(1f, 1f, 1f, alpha);
    }

}
