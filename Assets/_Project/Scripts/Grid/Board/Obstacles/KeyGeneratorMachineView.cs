using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural layered visual + animation engine for a single KeyGenerator machine.
/// Built at runtime as children of the generator's body Image (like the door overlay).
///
/// Layers (back → front, all children of the body rect):
///   body (existing Image, not owned here) · scan band (clipped to glass) · draft diamond · crank lever
///
/// The service drives the produce sequence:
///   PlayIdle() while waiting → CoPreLaunch() (crank pull + green scan up + draft dim) →
///   [service flies the real Key from the podium] → PlayPostLaunch() (crank return + draft restore).
/// SetClosed()/SetOpen() toggle the "lights off" state when the production quota is reached.
///
/// All tunables come from <see cref="KeyGeneratorMachineConfig"/> so the single inspector
/// surface stays on KeyGeneratorService.
/// </summary>
public sealed class KeyGeneratorMachineView : MonoBehaviour
{
    private RectTransform bodyRect;
    private KeyGeneratorMachineConfig cfg;

    private RectTransform scanMask;   // RectMask2D clip = glass interior
    private RectTransform scanBand;   // moving gradient bar
    private Image scanImage;
    private CanvasGroup scanGroup;

    private RectTransform draftRect;
    private Image draftImage;
    private CanvasGroup draftGroup;

    private RectTransform crankRect;
    private Image crankImage;

    private Coroutine idleRoutine;
    private Coroutine crankRoutine;
    private Coroutine postRoutine;
    private bool closed;
    private bool built;

    private static Sprite _scanGradient; // shared generated soft vertical gradient

    // ── Build ───────────────────────────────────────────────────────────────
    public void Build(RectTransform body, KeyGeneratorMachineConfig config)
    {
        bodyRect = body;
        cfg = config;
        if (bodyRect == null || cfg == null)
            return;

        Vector2 size = bodyRect.rect.size;

        // Glass interior rect (normalized insets: x=left, y=bottom, z=right, w=top).
        Vector4 g = cfg.glassInsets;
        var glassMin = new Vector2(g.x, g.y);
        var glassMax = new Vector2(1f - g.z, 1f - g.w);

        // ── Scan mask (clips the band to the glass window) ──
        var maskGo = new GameObject("KG_ScanMask", typeof(RectTransform), typeof(RectMask2D));
        scanMask = (RectTransform)maskGo.transform;
        scanMask.SetParent(bodyRect, false);
        scanMask.anchorMin = glassMin;
        scanMask.anchorMax = glassMax;
        scanMask.offsetMin = Vector2.zero;
        scanMask.offsetMax = Vector2.zero;
        maskGo.layer = bodyRect.gameObject.layer;

        // ── Scan band (soft gradient bar that rises through the glass) ──
        var bandGo = new GameObject("KG_ScanBand", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        scanBand = (RectTransform)bandGo.transform;
        scanBand.SetParent(scanMask, false);
        scanBand.anchorMin = new Vector2(0f, 0f);
        scanBand.anchorMax = new Vector2(1f, 0f);
        scanBand.pivot = new Vector2(0.5f, 0.5f);
        scanBand.sizeDelta = new Vector2(0f, size.y * Mathf.Max(0.02f, cfg.scanBandHeight));
        bandGo.layer = bodyRect.gameObject.layer;
        scanImage = bandGo.GetComponent<Image>();
        scanImage.sprite = GetScanGradient();
        scanImage.type = Image.Type.Simple;
        scanImage.raycastTarget = false;
        scanImage.color = cfg.scanColor;
        scanGroup = bandGo.GetComponent<CanvasGroup>();
        scanGroup.alpha = 0f;

        // ── Draft diamond (holographic idle hologram) ──
        var draftGo = new GameObject("KG_Draft", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        draftRect = (RectTransform)draftGo.transform;
        draftRect.SetParent(bodyRect, false);
        draftRect.anchorMin = draftRect.anchorMax = new Vector2(0.5f, 0.5f);
        draftRect.pivot = new Vector2(0.5f, 0.5f);
        float draftW = size.x * Mathf.Max(0.05f, cfg.draftSize);
        draftRect.sizeDelta = new Vector2(draftW, draftW);
        draftRect.anchoredPosition = new Vector2(cfg.draftOffset.x * size.x, cfg.draftOffset.y * size.y);
        draftGo.layer = bodyRect.gameObject.layer;
        draftImage = draftGo.GetComponent<Image>();
        draftImage.sprite = cfg.draftSprite;
        draftImage.preserveAspect = true;
        draftImage.raycastTarget = false;
        draftGroup = draftGo.GetComponent<CanvasGroup>();
        draftGroup.alpha = cfg.draftIdleAlpha;

        // ── Crank lever (rotates around the bottom hinge disc) ──
        var crankGo = new GameObject("KG_Crank", typeof(RectTransform), typeof(Image));
        crankRect = (RectTransform)crankGo.transform;
        crankRect.SetParent(bodyRect, false);
        crankRect.anchorMin = crankRect.anchorMax = new Vector2(0.5f, 0.5f);
        crankRect.pivot = cfg.crankPivot; // e.g. (0.5, 0.11) = hinge disc center
        float crankH = size.y * Mathf.Max(0.1f, cfg.crankHeight);
        float aspect = cfg.crankSprite != null && cfg.crankSprite.rect.height > 0f
            ? cfg.crankSprite.rect.width / cfg.crankSprite.rect.height
            : 0.28f;
        crankRect.sizeDelta = new Vector2(crankH * aspect, crankH);
        crankRect.anchoredPosition = new Vector2(cfg.crankOffset.x * size.x, cfg.crankOffset.y * size.y);
        crankRect.localRotation = Quaternion.Euler(0f, 0f, cfg.crankRestAngle);
        crankGo.layer = bodyRect.gameObject.layer;
        crankImage = crankGo.GetComponent<Image>();
        crankImage.sprite = cfg.crankSprite;
        crankImage.preserveAspect = true;
        crankImage.raycastTarget = false;

        built = true;
        PlayIdle();
    }

    // ── Idle (draft breathing glow) ─────────────────────────────────────────
    public void PlayIdle()
    {
        if (!built || closed || !isActiveAndEnabled)
            return;
        if (idleRoutine != null)
            StopCoroutine(idleRoutine);
        idleRoutine = StartCoroutine(CoIdleBreathe());
    }

    private IEnumerator CoIdleBreathe()
    {
        float lo = cfg.draftIdleAlpha * 0.72f;
        float hi = Mathf.Min(1f, cfg.draftIdleAlpha * 1.12f);
        float period = Mathf.Max(0.4f, cfg.draftBreathePeriod);
        float t = 0f;
        while (true)
        {
            if (draftGroup == null)
                yield break;
            t += Time.deltaTime;
            float k = 0.5f - 0.5f * Mathf.Cos((t / period) * Mathf.PI * 2f);
            draftGroup.alpha = Mathf.Lerp(lo, hi, k);
            yield return null;
        }
    }

    // ── Produce sequence ────────────────────────────────────────────────────
    /// Crank pull + green scan rising through the glass + draft dim. Completes at the
    /// moment the key should launch (scan has crossed the diamond zone). Non-null only
    /// when built & open.
    public IEnumerator CoPreLaunch()
    {
        if (!built || closed)
            yield break;

        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }

        // Kick the crank pull (runs alongside the scan; returns on its own after PostLaunch).
        if (crankRoutine != null) StopCoroutine(crankRoutine);
        crankRoutine = StartCoroutine(CoCrankPull());

        // Scan rises from just below the glass to the top, additive-bright green.
        float dur = Mathf.Max(0.08f, cfg.scanDuration);
        float glassH = scanMask != null ? scanMask.rect.height : (bodyRect != null ? bodyRect.rect.height : 100f);
        float from = -glassH * 0.15f;
        float to = glassH * 1.05f;

        if (scanGroup != null) scanGroup.alpha = 0f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (scanBand != null)
                scanBand.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, k));
            if (scanGroup != null)
                scanGroup.alpha = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); // fade in/out along travel
            // Dim the draft as the beam sweeps over it, so the real diamond can take over.
            if (draftGroup != null)
                draftGroup.alpha = Mathf.Lerp(cfg.draftIdleAlpha, cfg.draftDimAlpha, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.3f)));
            yield return null;
        }
        if (scanGroup != null) scanGroup.alpha = 0f;
        // Launch happens now (service flies the Key from the podium).
    }

    /// Crank returns to rest + draft restores + idle resumes. Fire-and-forget after launch.
    public void PlayPostLaunch()
    {
        if (!built || !isActiveAndEnabled)
            return;
        if (postRoutine != null) StopCoroutine(postRoutine);
        postRoutine = StartCoroutine(CoPostLaunch());
    }

    private IEnumerator CoPostLaunch()
    {
        // Restore draft.
        if (draftGroup != null)
        {
            float t = 0f, dur = 0.22f;
            float start = draftGroup.alpha;
            while (t < dur)
            {
                t += Time.deltaTime;
                // Kota dolup SetClosed geldiyse restore'u BIRAK — yoksa alpha=0'ı ezip
                // draft'ı geri getirir (son key'i alan generator'da hayalet draft bug'ı).
                if (draftGroup == null || closed) yield break;
                draftGroup.alpha = Mathf.Lerp(start, cfg.draftIdleAlpha, Mathf.Clamp01(t / dur));
                yield return null;
            }
        }
        postRoutine = null;
        if (!closed)
            PlayIdle();
    }

    private IEnumerator CoCrankPull()
    {
        if (crankRect == null)
            yield break;

        float rest = cfg.crankRestAngle;
        float pulled = rest + cfg.crankPullAngle; // + = one rotational direction; sign in config
        float down = Mathf.Max(0.05f, cfg.crankPullDuration);
        float up = Mathf.Max(0.05f, cfg.crankReturnDuration);

        // Pull down (ease-out, slight overshoot).
        float t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            if (crankRect == null) yield break;
            float k = Mathf.Clamp01(t / down);
            float e = 1f - (1f - k) * (1f - k);
            float over = pulled + Mathf.Sin(k * Mathf.PI) * cfg.crankOvershoot;
            crankRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(rest, over, e));
            yield return null;
        }

        if (cfg.crankHold > 0f)
            yield return new WaitForSeconds(cfg.crankHold);

        // Return (ease-in).
        t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            if (crankRect == null) yield break;
            float k = Mathf.Clamp01(t / up);
            crankRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(pulled, rest, k * k));
            yield return null;
        }
        if (crankRect != null)
            crankRect.localRotation = Quaternion.Euler(0f, 0f, rest);
        crankRoutine = null;
    }

    // ── Closed / open (quota reached → lights off) ──────────────────────────
    public void SetClosed()
    {
        closed = true;
        // TÜM canlı animasyonları durdur — yoksa devam eden CoPostLaunch/idle draft alpha'sını
        // geri yazıp lights-off durumu bozuyor (son key'i alan generator'da hayalet draft).
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        if (postRoutine != null) { StopCoroutine(postRoutine); postRoutine = null; }
        if (crankRoutine != null) { StopCoroutine(crankRoutine); crankRoutine = null; }
        // Hide the hologram + scan so only the lights-off body sprite reads.
        if (draftGroup != null) draftGroup.alpha = 0f;
        if (scanGroup != null) scanGroup.alpha = 0f;
        // Kol rest açısına dönsün (mid-animasyonda donmasın).
        if (crankRect != null && cfg != null)
            crankRect.localRotation = Quaternion.Euler(0f, 0f, cfg.crankRestAngle);
        if (cfg != null && cfg.hideCrankWhenClosed && crankImage != null)
            crankImage.enabled = false;
    }

    public void SetOpen()
    {
        closed = false;
        if (crankImage != null) crankImage.enabled = true;
        if (draftGroup != null) draftGroup.alpha = cfg != null ? cfg.draftIdleAlpha : 1f;
        PlayIdle();
    }

    // ── Generated soft vertical gradient sprite (transparent → white → transparent) ──
    private static Sprite GetScanGradient()
    {
        if (_scanGradient != null)
            return _scanGradient;

        const int h = 64;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float n = y / (float)(h - 1);          // 0..1 bottom→top
            float a = Mathf.Sin(n * Mathf.PI);      // soft peak in the middle
            a = Mathf.Pow(a, 1.4f);
            var c = new Color(1f, 1f, 1f, a);
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply(false, false);
        _scanGradient = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        return _scanGradient;
    }
}

/// Tunables + sprite refs for the KeyGenerator machine visual. Lives on KeyGeneratorService
/// so the whole thing is tuned from one inspector; passed by reference into each view.
[System.Serializable]
public sealed class KeyGeneratorMachineConfig
{
    [Header("Sprites")]
    public Sprite crankSprite;
    public Sprite draftSprite;

    [Header("Glass window (normalized insets: left, bottom, right, top)")]
    public Vector4 glassInsets = new Vector4(0.19f, 0.20f, 0.19f, 0.15f);

    [Header("Draft diamond")]
    [Range(0.05f, 1f)] public float draftSize = 0.42f;
    public Vector2 draftOffset = new Vector2(0f, 0.02f); // fraction of body size from center
    [Range(0f, 1f)] public float draftIdleAlpha = 0.85f;
    [Range(0f, 1f)] public float draftDimAlpha = 0.12f;
    [Min(0.4f)] public float draftBreathePeriod = 1.8f;

    [Header("Green scan band")]
    public Color scanColor = new Color(0.35f, 1f, 0.55f, 1f);
    [Range(0.02f, 0.5f)] public float scanBandHeight = 0.10f;
    [Min(0.08f)] public float scanDuration = 0.36f;

    [Header("Crank lever (hinge = pivot at bottom disc)")]
    public Vector2 crankPivot = new Vector2(0.5f, 0.11f);
    [Range(0.1f, 1.4f)] public float crankHeight = 0.55f;   // fraction of body height
    public Vector2 crankOffset = new Vector2(0.40f, -0.15f); // fraction of body size from center (+x=sağ, -y=aşağı)
    public float crankRestAngle = 0f;
    public float crankPullAngle = -34f;   // pull direction (deg); sign flips side
    public float crankOvershoot = 6f;
    [Min(0.05f)] public float crankPullDuration = 0.16f;
    [Min(0f)] public float crankHold = 0.03f;
    [Min(0.05f)] public float crankReturnDuration = 0.22f;

    [Header("Closed (lights off)")]
    public bool hideCrankWhenClosed = false;
}
