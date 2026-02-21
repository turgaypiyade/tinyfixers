using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OverrideComboController : MonoBehaviour
{
    [Header("Refs (Assign in Inspector)")]
    [SerializeField] private RectTransform pivot;
    [SerializeField] private RectTransform iconA;
    [SerializeField] private RectTransform iconB;
    [SerializeField] private Image centerFlash;
    [SerializeField] private ParticleSystem stormParticles;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timings")]
    [SerializeField] private float orbitDuration = 0.60f;
    [SerializeField] private float compressDuration = 0.25f;
    [SerializeField] private float stormDuration = 0.90f;
    [SerializeField] private float fadeOutDuration = 0.20f;

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 120f;
    [SerializeField] private float orbitTurns = 2.0f; // 2 = 720 degrees
    [SerializeField] private float orbitScaleFrom = 1.0f;
    [SerializeField] private float orbitScaleTo = 1.2f;

    [Header("Compress")]
    [SerializeField] private float compressScaleTo = 0.70f;

    [Header("Flash")]
    [SerializeField] private float flashMaxAlpha = 0.9f;


    private Coroutine _routine;

  /*  private void Start()
    {
        // TEMP TEST: play once when scene starts
        Play();
    }*/

    private void Reset()
    {
        // Try auto-wire if placed on the root
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Plays the combo VFX at a UI anchored position (relative to this object's RectTransform parent).
    /// </summary>
    public void PlayAtAnchoredPosition(Vector2 anchoredPos)
    {
        var rt = transform as RectTransform;
        if (rt != null) rt.anchoredPosition = anchoredPos;

        Play();
    }

    /// <summary>
    /// Plays using current position (recommended: keep this object centered on board).
    /// </summary>
    public void Play()
    {
        if (!IsWired())
        {
            Debug.LogError("[OverrideComboController] Missing references. Assign Pivot/IconA/IconB/CenterFlash/StormParticles/CanvasGroup.");
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Play());
    }

    private bool IsWired()
    {
        return pivot != null
               && iconA != null
               && iconB != null
               && centerFlash != null
               && stormParticles != null
               && canvasGroup != null;
    }

    private IEnumerator Co_Play()
    {
        // Ensure visible & reset
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one * orbitScaleFrom;

        SetFlashAlpha(0f);

        // Reset particle
        stormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // --- PHASE 1: ORBIT ---
        float orbitTime = 0f;
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);

        while (orbitTime < orbitDuration)
        {
            orbitTime += Time.deltaTime;
            float t = Mathf.Clamp01(orbitTime / orbitDuration);

            // EaseIn for speed-up feeling
            float easeT = t * t;

            // Scale up while orbiting
            float s = Mathf.Lerp(orbitScaleFrom, orbitScaleTo, t);
            pivot.localScale = Vector3.one * s;

            // Rotation
            float turns = orbitTurns;
            float ang = baseAngle + (easeT * turns * Mathf.PI * 2f);

            // Icon positions (opposite sides)
            Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * orbitRadius;
            iconA.anchoredPosition = offset;
            iconB.anchoredPosition = -offset;

            // Optional: slight pivot rotation for extra energy
            pivot.localRotation = Quaternion.Euler(0f, 0f, easeT * turns * 360f);

            yield return null;
        }

        // --- PHASE 2: COMPRESS + FLASH ---
        float compressTime = 0f;
        Vector3 startScale = pivot.localScale;
        Vector3 endScale = Vector3.one * compressScaleTo;

        Vector2 aStart = iconA.anchoredPosition;
        Vector2 bStart = iconB.anchoredPosition;

        while (compressTime < compressDuration)
        {
            compressTime += Time.deltaTime;
            float t = Mathf.Clamp01(compressTime / compressDuration);

            // Smooth
            float smoothT = t * t * (3f - 2f * t);

            pivot.localScale = Vector3.Lerp(startScale, endScale, smoothT);

            // Icons move to center
            iconA.anchoredPosition = Vector2.Lerp(aStart, Vector2.zero, smoothT);
            iconB.anchoredPosition = Vector2.Lerp(bStart, Vector2.zero, smoothT);

            // Flash peaks near the end
            float flashT = Mathf.Clamp01((t - 0.35f) / 0.65f);
            SetFlashAlpha(flashT * flashMaxAlpha);

            yield return null;
        }

        // Quick flash pop
        SetFlashAlpha(flashMaxAlpha);
        yield return new WaitForSeconds(0.05f);
        SetFlashAlpha(0f);

        // --- PHASE 3: STORM ---
        stormParticles.Play();

        float stormTime = 0f;
        while (stormTime < stormDuration)
        {
            stormTime += Time.deltaTime;
            yield return null;
        }

        // --- FADE OUT ---
        float fadeTime = 0f;
        float startAlpha = canvasGroup.alpha;

        while (fadeTime < fadeOutDuration)
        {
            fadeTime += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTime / fadeOutDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        _routine = null;

        // Keep object inactive (optional)
        gameObject.SetActive(false);
    }

    private void SetFlashAlpha(float a)
    {
        if (centerFlash == null) return;
        var c = centerFlash.color;
        c.a = a;
        centerFlash.color = c;
    }
}
