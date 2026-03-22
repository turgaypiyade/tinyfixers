using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PulseEmitterComboController : MonoBehaviour
{
    [Header("Refs (Assign in Inspector)")]
    [SerializeField] private RectTransform pivot;
    [SerializeField] private Image chargeRing;
    [SerializeField] private Image beamH;
    [SerializeField] private Image beamV;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timings")]
    [SerializeField] private float chargeDuration = 0.15f;
    [SerializeField] private float burstDuration = 0.35f;
    [SerializeField] private float dissipateDuration = 0.20f;

    [Header("Sizes")]
    [Tooltip("Beam final thickness (px). Burst starts at 3 tiles and shrinks to this.")]
    [SerializeField] private float beamThickness = 90f;

    [Tooltip("Beam extra length padding (px) added to board size.")]
    [SerializeField] private float beamPadding = 120f;

    [Tooltip("Ring starts at 3 tiles (sizeDelta) and shrinks to this many tiles during burst.")]
    [SerializeField] private float ringEndSizeInTiles = 1.2f;

    [Header("Alphas")]
    [SerializeField] private float ringMaxAlpha = 0.9f;
    [SerializeField] private float beamMaxAlpha = 0.85f;

    [Header("Runtime Settings")]
    [SerializeField] private float tileSize = 110f; // BoardController'dan set edeceğiz

    [Header("Stability")]
    [Tooltip("If true, beams won't visually scale when pivot scales (recommended).")]
    [SerializeField] private bool beamsIgnorePivotScale = true;

    private Coroutine _routine;

    // Cached layout
    private float _beamLenH;
    private float _beamLenV;
    private float _centerGapPx;

    private RectTransform _rtRing;
    private RectTransform _rtH;
    private RectTransform _rtV;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetTileSize(float size)
    {
        tileSize = size;
    }

    /// <summary>
    /// Play at UI anchored position. Provide the board rect size (in pixels) so beams fit perfectly.
    /// </summary>
    public void PlayAt(Vector2 anchoredPos, Vector2 boardSize)
    {
        if (pivot != null)
            pivot.anchoredPosition = anchoredPos;

        ConfigureBeams(boardSize);
        Play();
    }

    private void ConfigureBeams(Vector2 boardSize)
    {
        if (!IsWired()) return;

        _rtRing = chargeRing.rectTransform;
        _rtH = beamH.rectTransform;
        _rtV = beamV.rectTransform;

        // Anchor/pivot fix: sizeDelta'nin her zaman doğru çalışması için
        ForceCenterAnchors(_rtRing);
        ForceCenterAnchors(_rtH);
        ForceCenterAnchors(_rtV);

        // Board boyunca uzasın
        _beamLenH = boardSize.x + beamPadding;
        _beamLenV = boardSize.y + beamPadding;

        // Merkezde çok kütle olmasın diye küçük boşluk
        _centerGapPx = tileSize * 0.65f;

        float lenH = Mathf.Max(tileSize, _beamLenH - _centerGapPx);
        float lenV = Mathf.Max(tileSize, _beamLenV - _centerGapPx);

        // Başlangıç kalınlığı: 3 tile
        float startThickness = tileSize * 3f;

        // İlk değerleri setle
        _rtH.sizeDelta = new Vector2(lenH, startThickness);
        _rtV.sizeDelta = new Vector2(startThickness, lenV);

        _rtH.anchoredPosition = Vector2.zero;
        _rtV.anchoredPosition = Vector2.zero;
        _rtRing.anchoredPosition = Vector2.zero;

        // Ring başlangıç boyutu: 3 tile
        _rtRing.sizeDelta = Vector2.one * (tileSize * 3f);

        // PreserveAspect kapalı olmalı (yoksa kalınlık/uzunluk sapıtır)
        beamH.preserveAspect = false;
        beamV.preserveAspect = false;
        chargeRing.preserveAspect = false;

        // Scale reset
        _rtRing.localScale = Vector3.one;
        _rtH.localScale = Vector3.one;
        _rtV.localScale = Vector3.one;
    }

    private void ForceCenterAnchors(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void Play()
    {
        if (!IsWired())
        {
            Debug.LogError("[PulseEmitterComboController] Missing refs. Assign Pivot/ChargeRing/BeamH/BeamV/CanvasGroup.");
            return;
        }

        beamH.preserveAspect = false;
        beamV.preserveAspect = false;
        chargeRing.preserveAspect = false;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Play());
    }

    private bool IsWired()
    {
        return pivot != null && chargeRing != null && beamH != null && beamV != null && canvasGroup != null;
    }

    private IEnumerator Co_Play()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        // Reset transforms
        pivot.localScale = Vector3.one;
        pivot.localRotation = Quaternion.identity;

        // Hide visuals
        SetAlpha(chargeRing, 0f);
        SetAlpha(beamH, 0f);
        SetAlpha(beamV, 0f);

        // Cache start/end sizes
        float startBeamT = tileSize * 3f;                 // 3 tile
        float endBeamT = Mathf.Max(2f, beamThickness);    // final thickness

        float ringStart = tileSize * 3f;                                  // 3 tile
        float ringEnd = Mathf.Max(tileSize * 0.6f, tileSize * ringEndSizeInTiles);

        // Beam lengths (board based) - configure edilmiş olmalı
        // (ConfigureBeams çağrılmadıysa da güvenli olsun)
        float lenH = (_rtH != null) ? _rtH.sizeDelta.x : (tileSize * 3f);
        float lenV = (_rtV != null) ? _rtV.sizeDelta.y : (tileSize * 3f);

        // --- PHASE 1: CHARGE ---
        float t = 0f;
        float startScale = 0.75f;
        float endScale = 1.15f;

        // Ring CHARGE sırasında 3 tile kalsın
        if (_rtRing != null) _rtRing.sizeDelta = Vector2.one * ringStart;

        while (t < chargeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / chargeDuration);
            float smooth = k * k * (3f - 2f * k);

            pivot.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, smooth);
            SetAlpha(chargeRing, smooth * ringMaxAlpha);

            // slight spin for energy feel
            pivot.localRotation = Quaternion.Euler(0f, 0f, smooth * 50f);

            ApplyBeamScaleCompensationIfNeeded();

            yield return null;
        }

        // --- PHASE 2: CROSS BURST ---
        t = 0f;
        float burstScaleFrom = 1.15f;
        float burstScaleTo = 1.00f;

        while (t < burstDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / burstDuration);

            // ease out
            float ease = 1f - Mathf.Pow(1f - k, 2f);

            pivot.localScale = Vector3.one * Mathf.Lerp(burstScaleFrom, burstScaleTo, ease);

            // Beams fade in quickly, then hold
            float beamIn = Mathf.Clamp01(k / 0.25f);
            SetAlpha(beamH, beamIn * beamMaxAlpha);
            SetAlpha(beamV, beamIn * beamMaxAlpha);

            // Ring fades out during burst
            float ringOut = 1f - Mathf.Clamp01((k - 0.2f) / 0.8f);
            SetAlpha(chargeRing, ringOut * ringMaxAlpha);

            // === SIZE ANIMATION (the important part) ===
            // Beam thickness: 3 tile -> final thickness
            float curT = Mathf.Lerp(startBeamT, endBeamT, ease);

            if (_rtH != null) _rtH.sizeDelta = new Vector2(lenH, curT);
            if (_rtV != null) _rtV.sizeDelta = new Vector2(curT, lenV);

            // Ring: 3 tile -> smaller
            if (_rtRing != null)
            {
                float curRing = Mathf.Lerp(ringStart, ringEnd, ease);
                _rtRing.sizeDelta = Vector2.one * curRing;
            }

            // tiny jitter
            float jitter = (1f - k) * 2f;
            pivot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-jitter, jitter));

            ApplyBeamScaleCompensationIfNeeded();

            yield return null;
        }

        // --- PHASE 3: DISSIPATE ---
        t = 0f;
        float aH0 = beamH.color.a;
        float aV0 = beamV.color.a;

        while (t < dissipateDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dissipateDuration);

            float outA = Mathf.Lerp(1f, 0f, k);
            SetAlpha(beamH, aH0 * outA);
            SetAlpha(beamV, aV0 * outA);
            SetAlpha(chargeRing, 0f);

            // subtle shrink
            pivot.localScale = Vector3.one * Mathf.Lerp(1.0f, 0.92f, k);

            // optional: dissipate sırasında bir tık daha incelsin
            float curT = Mathf.Lerp(endBeamT, Mathf.Max(2f, endBeamT * 0.6f), k);
            if (_rtH != null) _rtH.sizeDelta = new Vector2(lenH, curT);
            if (_rtV != null) _rtV.sizeDelta = new Vector2(curT, lenV);

            ApplyBeamScaleCompensationIfNeeded();

            yield return null;
        }

        // finish
        SetAlpha(beamH, 0f);
        SetAlpha(beamV, 0f);
        SetAlpha(chargeRing, 0f);

        canvasGroup.alpha = 0f;
        _routine = null;
        gameObject.SetActive(false);
    }

    private void ApplyBeamScaleCompensationIfNeeded()
    {
        if (!beamsIgnorePivotScale) return;
        if (_rtH == null || _rtV == null) return;

        // pivot uniform scale varsayımı
        float s = Mathf.Max(0.0001f, pivot.localScale.x);
        float inv = 1f / s;

        _rtH.localScale = Vector3.one * inv;
        _rtV.localScale = Vector3.one * inv;
    }

    private void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = a;
        img.color = c;
    }
}
