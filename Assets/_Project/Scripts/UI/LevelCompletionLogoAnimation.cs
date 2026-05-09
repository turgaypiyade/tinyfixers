using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Level tamamlanma logosu animasyonu.
/// Üç logo parçası farklı yönlerden uçarak ortada buluşur, fişek patlar.
/// Herhangi bir yere çift tıklama animasyonu atlar (WasSkipped=true).
/// </summary>
public class LevelCompletionLogoAnimation : MonoBehaviour
{
    [Header("Arkaplan Karartma")]
    [SerializeField] private Image overlayImage;
    [SerializeField, Range(0f, 1f)] private float overlayTargetAlpha = 0.75f;
    [SerializeField, Min(0.05f)] private float dimFadeDuration = 0.22f;
    [SerializeField] private int canvasSortOrder = 200;

    [Header("Logo Parçaları (atanmayanlar atlanır)")]
    [SerializeField] private RectTransform tinysImage;
    [SerializeField] private RectTransform fixersLeftImage;
    [SerializeField] private RectTransform fixersRightImage;

    [Header("Fişek VFX")]
    [SerializeField] private RectTransform vfxRoot;
    [SerializeField] private int fireworkBurstCount = 4;

    [Header("Zamanlama")]
    [SerializeField, Min(0.1f)] private float flyInDuration    = 0.55f;
    [SerializeField, Min(0f)]   private float holdDuration     = 0.65f;
    [SerializeField, Min(0f)]   private float fireworkDelay    = 0.18f;
    [SerializeField, Min(0.05f)] private float fireworkInterval = 0.25f;

    [Header("Giriş Mesafeleri (piksel)")]
    [SerializeField] private float tinysTopOffset     = 1100f;
    [SerializeField] private float fixersBottomOffset = 1200f;
    [SerializeField] private float fixersSideOffset   = 600f;

    [Header("Çift Tıklama Skip")]
    [SerializeField, Min(0.05f)] private float doubleTapWindow = 0.40f;

    // ─────────────────────────────────────────────────────────────────────────

    public bool WasSkipped { get; private set; }

    private bool  _playing;
    private bool  _skipRequested;
    private float _lastTapTime = -99f;
    private Canvas _canvas;

    private void Update()
    {
        if (!_playing) return;

        bool tapped = false;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapped = true;

        var touch = Touchscreen.current;
        if (!tapped && touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            tapped = true;

        if (!tapped) return;

        float now = Time.unscaledTime;
        float gap = now - _lastTapTime;
        Debug.Log($"[LogoAnim] Tapped. gap={gap:F2}s window={doubleTapWindow}s → skip={gap <= doubleTapWindow}");

        if (gap <= doubleTapWindow)
            _skipRequested = true;

        _lastTapTime = now;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public IEnumerator Play()
    {
        WasSkipped     = false;
        _skipRequested = false;
        _playing       = false;
        _lastTapTime   = -99f;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Canvas sort order'ı yüksek tut — board canvas'ın üzerinde render edilsin.
        if (_canvas == null)
            _canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>(true);
        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = canvasSortOrder;
        }

        Debug.Log($"[LogoAnim] Play — overlay={overlayImage != null}  tinys={tinysImage != null}  fixersL={fixersLeftImage != null}  fixersR={fixersRightImage != null}  vfx={vfxRoot != null}  canvas={(_canvas != null ? _canvas.sortingOrder.ToString() : "null")}");

        SetOverlayAlpha(0f);

        // Çift tıklama algılamayı hemen aç — fade sırasında da tıklama kabul edilsin.
        _playing = true;

        // Fade-in overlay
        yield return StartCoroutine(FadeOverlay(0f, overlayTargetAlpha, dimFadeDuration));

        if (_skipRequested)
        {
            _playing   = false;
            WasSkipped = true;
            yield return StartCoroutine(FadeOverlay(overlayTargetAlpha, 0f, dimFadeDuration));
            gameObject.SetActive(false);
            yield break;
        }

        // Logo parçalarını başlangıç pozisyonlarına al
        var startOffsets = new Vector2[]
        {
            new Vector2(0f,               tinysTopOffset),      // tinys — yukarıdan
            new Vector2(-fixersSideOffset, -fixersBottomOffset), // sol  — sol alttan
            new Vector2( fixersSideOffset, -fixersBottomOffset), // sağ  — sağ alttan
        };

        var pieces  = new RectTransform[] { tinysImage, fixersLeftImage, fixersRightImage };
        var centers = new Vector2[pieces.Length];

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;
            centers[i] = pieces[i].anchoredPosition;
            pieces[i].anchoredPosition = centers[i] + startOffsets[i];
        }

        // Fişekleri arka planda başlat
        StartCoroutine(PlayFireworks());

        // Uçuş animasyonu
        float elapsed = 0f;
        while (elapsed < flyInDuration && !_skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float e = EaseOutBack(Mathf.Clamp01(elapsed / flyInDuration));

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].anchoredPosition = Vector2.LerpUnclamped(
                    centers[i] + startOffsets[i], centers[i], e);
            }
            yield return null;
        }

        // Merkeze kilitle
        for (int i = 0; i < pieces.Length; i++)
            if (pieces[i] != null)
                pieces[i].anchoredPosition = centers[i];

        // Bekleme
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration && !_skipRequested)
        {
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _playing   = false;
        WasSkipped = _skipRequested;

        // Fade-out overlay
        yield return StartCoroutine(FadeOverlay(overlayTargetAlpha, 0f, dimFadeDuration));

        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OVERLAY
    // ─────────────────────────────────────────────────────────────────────────

    private void SetOverlayAlpha(float a)
    {
        if (overlayImage == null) return;
        var c = overlayImage.color;
        c.a = a;
        overlayImage.color = c;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlayImage == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FİŞEK
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayFireworks()
    {
        if (vfxRoot == null)
        {
            Debug.LogWarning("[LogoAnim] vfxRoot atanmamış — fişek çalışmaz.");
            yield break;
        }

        yield return new WaitForSecondsRealtime(fireworkDelay);

        for (int i = 0; i < fireworkBurstCount && !_skipRequested; i++)
        {
            Vector2 pos = new Vector2(Random.Range(-200f, 200f), Random.Range(-60f, 280f));
            Color   col = (Random.value > 0.5f)
                ? new Color(1f, 0.18f, 0.08f)
                : new Color(1f, 0.85f, 0.10f);
            StartCoroutine(BurstAt(pos, col));
            yield return new WaitForSecondsRealtime(fireworkInterval);
        }
    }

    private IEnumerator BurstAt(Vector2 center, Color baseColor)
    {
        const float duration = 0.70f;
        const float gravity  = 300f;
        int count = Random.Range(12, 20);

        var rts  = new RectTransform[count];
        var cgs  = new CanvasGroup[count];
        var vels = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-10f, 10f);
            float speed = Random.Range(180f, 380f);
            vels[i] = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;

            Color c = Color.Lerp(baseColor, Color.white, Random.Range(0f, 0.3f));
            rts[i] = CreateDot(center, Random.Range(16f, 28f), c);
            cgs[i] = rts[i].GetComponent<CanvasGroup>();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (rts[i] == null) continue;
                vels[i].y -= gravity * Time.unscaledDeltaTime;
                rts[i].anchoredPosition += vels[i] * Time.unscaledDeltaTime;
                if (cgs[i] != null) cgs[i].alpha = 1f - t * t;
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (rts[i] != null) Destroy(rts[i].gameObject);
    }

    private RectTransform CreateDot(Vector2 pos, float size, Color color)
    {
        var go = new GameObject("FwDot",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(vfxRoot, false);
        go.transform.localScale = Vector3.one;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(size, size);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        return rt;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float tm1 = t - 1f;
        return 1f + c3 * (tm1 * tm1 * tm1) + c1 * (tm1 * tm1);
    }
}
