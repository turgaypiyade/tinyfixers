using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// Level completion logo animation.
/// Three logo parts fly to the center and fireworks play.
/// Double click during this animation skips only the logo animation.
///
/// Important: this component does not disable its root object at the end.
/// The skipButton can live under the same LogoAnimPanel and still work during BonusMovesService.RunBonusRound().
/// </summary>
public class LevelCompletionLogoAnimation : MonoBehaviour
{
    [Header("Arkaplan / Overlay")]
    [SerializeField] private Image overlayImage;
    [SerializeField] private bool keepOverlayTransparent = true;
    [SerializeField, Range(0f, 1f)] private float overlayTargetAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float dimFadeDuration = 0.22f;
    [SerializeField] private int canvasSortOrder = 200;

    [Header("Logo Parcalari (atanmayanlar atlanir)")]
    [SerializeField] private RectTransform tinysImage;
    [SerializeField] private RectTransform fixersLeftImage;
    [SerializeField] private RectTransform fixersRightImage;

    [Header("Fisek VFX")]
    [SerializeField] private RectTransform vfxRoot;
    [SerializeField] private int fireworkBurstCount = 12;
    [Tooltip("Havai fisek gosterisinin toplam suresi (saniye). Son patlamanin sonme suresi bu sureye dahildir.")]
    [SerializeField, Min(0.5f)] private float fireworkShowDuration = 2.4f;

    [Header("Zamanlama")]
    [SerializeField, Min(0.1f)] private float flyInDuration = 0.55f;
    [SerializeField, Min(0f)] private float holdDuration = 0.65f;
    [SerializeField, Min(0f)] private float fireworkDelay = 0.18f;

    [Header("Giris Mesafeleri (piksel)")]
    [SerializeField] private float tinysTopOffset = 1100f;
    [SerializeField] private float fixersBottomOffset = 1200f;
    [SerializeField] private float fixersSideOffset = 600f;

    [Header("Cift Tiklama Skip")]
    [SerializeField, Min(0.05f)] private float doubleTapWindow = 0.40f;

    // -------------------------------------------------------------------------
    public bool WasSkipped { get; private set; }
    public event Action FireworksStarted;
    public event Action FireworksFinished;
    /// <summary>Her tek görsel havai fişek patlamasında tetiklenir (kesintili ses için).</summary>
    public event Action FireworkBurst;

    private bool _playing;
    private bool _skipRequested;
    private float _lastTapTime = -99f;
    private Canvas _canvas;
    private readonly List<GameObject> spawnedFireworkVfx = new();

    private const float FireworkBurstDuration = 0.9f;
    private static Sprite fireworkGlowSprite;
    private static Sprite fireworkLineSprite;
    private static Sprite fireworkSolidDotSprite;

    private float TargetOverlayAlpha => keepOverlayTransparent ? 0f : Mathf.Clamp01(overlayTargetAlpha);

    private void Update()
    {
        if (!_playing)
            return;

        bool tapped = false;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapped = true;

        var touch = Touchscreen.current;
        if (!tapped && touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            tapped = true;

        if (!tapped)
            return;

        float now = Time.unscaledTime;
        float gap = now - _lastTapTime;

        if (gap <= doubleTapWindow)
            _skipRequested = true;

        _lastTapTime = now;
    }

    // -------------------------------------------------------------------------
    public IEnumerator Play()
    {
        WasSkipped = false;
        _skipRequested = false;
        _playing = false;
        _lastTapTime = -99f;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_canvas == null)
            if (!TryGetComponent(out _canvas)) _canvas = GetComponentInParent<Canvas>(true);

        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = canvasSortOrder;
        }

        ClearSpawnedFireworks();
        SetLogoPiecesVisible(true);
        SetVfxRootVisible(true);
        PrepareOverlayForAnimation();
        SetOverlayAlpha(0f);

        _playing = true;

        float targetAlpha = TargetOverlayAlpha;
        yield return StartCoroutine(FadeOverlay(0f, targetAlpha, dimFadeDuration));

        if (_skipRequested)
        {
            _playing = false;
            WasSkipped = true;
            yield return StartCoroutine(FadeOverlay(targetAlpha, 0f, dimFadeDuration));
            HideVisualsAfterPlay();
            yield break;
        }

        var startOffsets = new Vector2[]
        {
            new Vector2(0f, tinysTopOffset),
            new Vector2(-fixersSideOffset, -fixersBottomOffset),
            new Vector2(fixersSideOffset, -fixersBottomOffset),
        };

        var pieces = new RectTransform[] { tinysImage, fixersLeftImage, fixersRightImage };
        var centers = new Vector2[pieces.Length];

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null)
                continue;

            centers[i] = pieces[i].anchoredPosition;
            pieces[i].anchoredPosition = centers[i] + startOffsets[i];
        }

        Coroutine fireworksRoutine = StartCoroutine(PlayFireworks());

        float elapsed = 0f;
        while (elapsed < flyInDuration && !_skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float e = EaseOutBack(Mathf.Clamp01(elapsed / flyInDuration));

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                    continue;

                pieces[i].anchoredPosition = Vector2.LerpUnclamped(
                    centers[i] + startOffsets[i], centers[i], e);
            }

            yield return null;
        }

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null)
                pieces[i].anchoredPosition = centers[i];
        }

        float holdElapsed = 0f;
        while (holdElapsed < holdDuration && !_skipRequested)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_skipRequested && fireworksRoutine != null)
            yield return fireworksRoutine;

        _playing = false;
        WasSkipped = _skipRequested;

        yield return StartCoroutine(FadeOverlay(targetAlpha, 0f, dimFadeDuration));
        HideVisualsAfterPlay();
    }

    // -------------------------------------------------------------------------
    private void PrepareOverlayForAnimation()
    {
        if (overlayImage == null)
            return;

        overlayImage.enabled = true;
        overlayImage.raycastTarget = false;

        var c = overlayImage.color;
        c.a = 0f;
        overlayImage.color = c;
    }

    private void SetOverlayAlpha(float a)
    {
        if (overlayImage == null)
            return;

        var c = overlayImage.color;
        c.a = keepOverlayTransparent ? 0f : Mathf.Clamp01(a);
        overlayImage.color = c;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlayImage == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetOverlayAlpha(to);
    }

    private void HideVisualsAfterPlay()
    {
        _playing = false;
        _skipRequested = false;

        SetOverlayAlpha(0f);

        if (overlayImage != null)
        {
            overlayImage.enabled = keepOverlayTransparent ? false : overlayImage.enabled;
            overlayImage.raycastTarget = false;
        }

        SetLogoPiecesVisible(false);
        SetVfxRootVisible(false);
        ClearSpawnedFireworks();

        // Reset canvas override so world-space renderers (RocketTrailBeam, sortingOrder=100)
        // are not occluded on iOS Metal during the bonus round.
        if (_canvas != null)
            _canvas.overrideSorting = false;

        // Do not disable gameObject here. The bonus-round skipButton may be a child of this root.
    }

    private void SetLogoPiecesVisible(bool visible)
    {
        SetGraphicVisible(tinysImage, visible);
        SetGraphicVisible(fixersLeftImage, visible);
        SetGraphicVisible(fixersRightImage, visible);
    }

    private static void SetGraphicVisible(RectTransform target, bool visible)
    {
        if (target == null)
            return;

        var image = target.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = visible;
            image.raycastTarget = false;
        }

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void SetVfxRootVisible(bool visible)
    {
        if (vfxRoot != null)
            vfxRoot.gameObject.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    private IEnumerator PlayFireworks()
    {
        if (vfxRoot == null)
            yield break;

        yield return new WaitForSecondsRealtime(fireworkDelay);

        int salvoCount = Mathf.Max(1, fireworkBurstCount);
        // Son salvonun sonmesi de toplam sureye dahil → salvolar kalan pencereye yayilir.
        float salvoWindow = Mathf.Max(0f, fireworkShowDuration - FireworkBurstDuration);
        float gap = salvoCount > 1 ? salvoWindow / (salvoCount - 1) : 0f;

        // Salvo siralamasi karistirilir: arka arkaya gelen patlamalar
        // ekranin farkli bolgelerinden cikar, ayni yere yigilmaz.
        var lanes = new int[salvoCount];
        for (int i = 0; i < salvoCount; i++)
            lanes[i] = i;
        for (int i = salvoCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
        }

        int fired = 0;
        for (int i = 0; i < salvoCount && !_skipRequested; i++)
        {
            float lane = salvoCount > 1 ? lanes[i] / (float)(salvoCount - 1) : 0.5f;
            Vector2 pos = new Vector2(
                Mathf.Lerp(-340f, 340f, lane) + Random.Range(-40f, 41f),
                Random.Range(120f, 540f));
            // Salvo rengi = merkez parlamasinin tonu; isinlarin her biri kendi rastgele rengini alir.
            Color col = Color.HSVToRGB(Random.value, Random.Range(0.45f, 0.75f), 1f);

            if (fired == 0)
                FireworksStarted?.Invoke();

            FireworkBurst?.Invoke();

            // Her salvo 1-2 yakin ama ayri merkezden patlar.
            int centerCount = Random.Range(1, 3);
            for (int center = 0; center < centerCount; center++)
            {
                Vector2 clusterOffset = center == 0
                    ? Vector2.zero
                    : new Vector2(Random.Range(-72f, 73f), Random.Range(-38f, 39f));
                StartCoroutine(BurstAt(pos + clusterOffset, col));
            }
            fired++;

            if (i < salvoCount - 1 && gap > 0f)
                yield return new WaitForSecondsRealtime(gap * Random.Range(0.65f, 1.35f));
        }

        if (fired > 0 && !_skipRequested)
        {
            yield return new WaitForSecondsRealtime(FireworkBurstDuration);
            FireworksFinished?.Invoke();
        }
    }

    private static void SetFireworkLine(RectTransform rt, Vector2 tail, Vector2 head, float width)
    {
        Vector2 delta = head - tail;
        rt.anchoredPosition = (head + tail) * 0.5f;
        rt.sizeDelta = new Vector2(width, Mathf.Max(width, delta.magnitude));
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
    }

    private void RemoveFirework(RectTransform rt)
    {
        if (rt == null) return;
        spawnedFireworkVfx.Remove(rt.gameObject);
        Destroy(rt.gameObject);
    }

    private IEnumerator BurstAt(Vector2 center, Color baseColor)
    {
        // Neredeyse duz ucus: yercekimi cok hafif, surtunme dusuk.
        const float gravity = 20f;
        const float drag = 0.55f;
        int count = Random.Range(14, 21);
        var flash = CreateDot(center, 70f, Color.Lerp(baseColor, Color.white, 0.75f));

        var rts = new RectTransform[count];
        var cgs = new CanvasGroup[count];
        var headRts = new RectTransform[count];
        var headCgs = new CanvasGroup[count];
        var headSizes = new float[count];
        var vels = new Vector2[count];
        var positions = new Vector2[count];
        var fadeStarts = new float[count];
        var maxDistances = new float[count];

        float angleOffset = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + (360f / count) * i + Random.Range(-6f, 6f);
            positions[i] = center;
            float speed = Random.Range(430f, 620f);
            vels[i] = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;

            // Her isin belirli bir mesafeden sonra sonmeye baslar ve menzil sonunda kaybolur.
            maxDistances[i] = Random.Range(300f, 430f);
            fadeStarts[i] = maxDistances[i] * Random.Range(0.45f, 0.6f);

            // Her cizgi KENDI rastgele rengini alir (canli ton, hafif beyaz karisim).
            Color c = Color.Lerp(
                Color.HSVToRGB(Random.value, Random.Range(0.6f, 0.9f), 1f),
                Color.white,
                Random.Range(0.08f, 0.22f));

            rts[i] = CreateDot(center, 2f, c);
            rts[i].GetComponent<Image>().sprite = GetFireworkLineSprite();
            cgs[i] = rts[i].GetComponent<CanvasGroup>();

            // Isinin ucundaki dolu yuvarlak bas — cizgi inceldikce bas onu tasir.
            headSizes[i] = Random.Range(9f, 14f);
            headRts[i] = CreateDot(center, headSizes[i], c);
            headRts[i].GetComponent<Image>().sprite = GetFireworkSolidDotSprite();
            headCgs[i] = headRts[i].GetComponent<CanvasGroup>();
        }

        float elapsed = 0f;
        while (elapsed < FireworkBurstDuration && !_skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / FireworkBurstDuration;
            if (flash != null)
            {
                flash.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.8f, Mathf.Clamp01(t * 4f));
                flash.GetComponent<CanvasGroup>().alpha = Mathf.Max(0f, 1f - t * 5f);
            }

            for (int i = 0; i < count; i++)
            {
                if (rts[i] == null)
                    continue;

                positions[i] += vels[i] * Time.unscaledDeltaTime;
                Vector2 head = positions[i];
                vels[i] *= Mathf.Exp(-drag * Time.unscaledDeltaTime);
                vels[i].y -= gravity * Time.unscaledDeltaTime;

                // Ince ve uzun iz: kalinlik sabit kalir, uzunluk merkezden uzaklastikca acilir.
                float distance = Vector2.Distance(head, center);
                float length = Mathf.Min(distance, Mathf.Lerp(240f, 150f, t));
                Vector2 tail = head - vels[i].normalized * length;
                SetFireworkLine(rts[i], tail, head, Mathf.Lerp(3.2f, 1.6f, t));

                // Sonme mesafeye bagli: menzilin sonuna dogru alpha sifirlanir.
                float alpha = 1f;
                if (cgs[i] != null)
                {
                    float fade = Mathf.InverseLerp(fadeStarts[i], maxDistances[i], distance);
                    alpha = 1f - fade;
                    cgs[i].alpha = alpha;
                }

                if (headRts[i] != null)
                {
                    headRts[i].anchoredPosition = head;
                    headRts[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
                    if (headCgs[i] != null)
                        headCgs[i].alpha = alpha;
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (rts[i] != null)
            {
                spawnedFireworkVfx.Remove(rts[i].gameObject);
                Destroy(rts[i].gameObject);
            }

            if (headRts[i] != null)
            {
                spawnedFireworkVfx.Remove(headRts[i].gameObject);
                Destroy(headRts[i].gameObject);
            }
        }

        RemoveFirework(flash);
        spawnedFireworkVfx.RemoveAll(go => go == null);
    }

    private RectTransform CreateDot(Vector2 pos, float size, Color color)
    {
        var go = new GameObject("FwDot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        spawnedFireworkVfx.Add(go);
        go.layer = vfxRoot.gameObject.layer;

        go.transform.SetParent(vfxRoot, false);
        go.transform.localScale = Vector3.one;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = GetFireworkGlowSprite();
        img.color = color;
        img.raycastTarget = false;

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        return rt;
    }

    private static Sprite GetFireworkLineSprite()
    {
        if (fireworkLineSprite != null) return fireworkLineSprite;
        // A round glow squeezed into a narrow strip loses almost all its opacity.
        // Give trails an opaque white core with soft edges and a fading tail.
        const int width = 16, height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "FireworkBrightTrail";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, across));
                float tail = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y + 0.5f) / height * 4f));
                pixels[y * width + x] = new Color(1f, 1f, 1f, edge * tail);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        fireworkLineSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        return fireworkLineSprite;
    }

    // Isin ucundaki DOLU yuvarlak: merkezi tam opak, yalniz kenarda 1-2px yumusama (AA).
    private static Sprite GetFireworkSolidDotSprite()
    {
        if (fireworkSolidDotSprite != null) return fireworkSolidDotSprite;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "FireworkSolidDot";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                float alpha = Mathf.Clamp01((1f - radius) / (2f / size));   // ~1px kenar yumusamasi
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        fireworkSolidDotSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return fireworkSolidDotSprite;
    }

    private static Sprite GetFireworkGlowSprite()
    {
        if (fireworkGlowSprite != null) return fireworkGlowSprite;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "FireworkSoftGlow";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                float alpha = Mathf.Clamp01(1f - radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        fireworkGlowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return fireworkGlowSprite;
    }

    private void ClearSpawnedFireworks()
    {
        for (int i = 0; i < spawnedFireworkVfx.Count; i++)
        {
            if (spawnedFireworkVfx[i] != null)
                Destroy(spawnedFireworkVfx[i]);
        }

        spawnedFireworkVfx.Clear();
    }

    // -------------------------------------------------------------------------
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float tm1 = t - 1f;
        return 1f + c3 * (tm1 * tm1 * tm1) + c1 * (tm1 * tm1);
    }
}
