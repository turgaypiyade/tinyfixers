using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TeleportMarkerAnim : MonoBehaviour
{
    public enum Mode { In, Out }

    [Header("Refs")]
    [SerializeField] private RectTransform ring;
    [SerializeField] private RectTransform glow;
    [SerializeField] private Image ringImg;
    [SerializeField] private Image glowImg;

    [Header("Tuning")]
    public Mode mode = Mode.In;

    [Tooltip("Total time excluding holdTime.")]
    public float duration = 0.16f;

    [Tooltip("Small pause on the 'flash' moment. 0.04–0.10 recommended.")]
    public float holdTime = 0.06f;

    [Header("Ring Scale")]
    [Tooltip("Depart: bigger->small, Arrive: small->bigger")]
    public float ringFromScale = 1.05f;
    public float ringToScale = 0.15f;

    [Header("Glow Scale")]
    public float glowFromScale = 1.25f;
    public float glowToScale = 0.25f;

    [Header("Alpha (Flash -> Fade)")]
    [Tooltip("Peak alpha at the first frame.")]
    public float ringPeakAlpha = 0.95f;

    [Tooltip("Final alpha when finished.")]
    public float ringEndAlpha = 0f;

    public float glowPeakAlpha = 0.35f;
    public float glowEndAlpha = 0f;

    [Header("Optional")]
    [Tooltip("If true, ignores Time.timeScale.")]
    public bool unscaledTime = false;

    Coroutine _co;

    void Reset()
    {
        ring = transform.Find("Ring") as RectTransform;
        glow = transform.Find("Glow") as RectTransform;
        if (ring) ringImg = ring.GetComponent<Image>();
        if (glow) glowImg = glow.GetComponent<Image>();
    }

    void OnEnable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        // Suggested defaults if you didn't tune anything
        ApplyModeDefaults();

        // 0) Flash frame (instant pop)
        ApplyInstantFlash();

        // Small hold to make the "teleport" readable
        if (holdTime > 0f)
            yield return Wait(holdTime);

        // 1) Animate
        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);

        while (t < dur)
        {
            t += Delta();
            float u = Mathf.Clamp01(t / dur);

            // Easing:
            // Out: fast in (implode), In: fast out (explode)
            float k = (mode == Mode.Out) ? EaseIn(u) : EaseOut(u);

            // Scale
            if (ring) ring.localScale = Vector3.one * Mathf.Lerp(ringFromScale, ringToScale, k);
            if (glow) glow.localScale = Vector3.one * Mathf.Lerp(glowFromScale, glowToScale, k);

            // Alpha: start bright, quickly fade
            // We fade with a slightly faster curve so it's punchy
            float fade = Mathf.Pow(u, 1.6f);

            if (ringImg)
            {
                var c = ringImg.color;
                c.a = Mathf.Lerp(ringPeakAlpha, ringEndAlpha, fade);
                ringImg.color = c;
            }

            if (glowImg)
            {
                var c = glowImg.color;
                c.a = Mathf.Lerp(glowPeakAlpha, glowEndAlpha, fade);
                glowImg.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    void ApplyModeDefaults()
    {
        // If user didn't change, these values feel good for teleport
        if (mode == Mode.Out)
        {
            // Depart: implode
            ringFromScale = 1.05f;
            ringToScale   = 0.10f;

            glowFromScale = 1.10f;
            glowToScale   = 0.10f;

            ringPeakAlpha = 0.95f;
            glowPeakAlpha = 0.30f;
        }
        else
        {
            // Arrive: explode
            ringFromScale = 0.20f;
            ringToScale   = 1.25f;

            glowFromScale = 0.35f;
            glowToScale   = 1.60f;

            ringPeakAlpha = 0.95f;
            glowPeakAlpha = 0.35f;
        }

        ringEndAlpha = 0f;
        glowEndAlpha = 0f;
    }

    void ApplyInstantFlash()
    {
        // Set start scale instantly + alpha at peak for a single readable pop
        if (ring) ring.localScale = Vector3.one * ringFromScale;
        if (glow) glow.localScale = Vector3.one * glowFromScale;

        if (ringImg)
        {
            var c = ringImg.color;
            c.a = ringPeakAlpha;
            ringImg.color = c;
        }

        if (glowImg)
        {
            var c = glowImg.color;
            c.a = glowPeakAlpha;
            glowImg.color = c;
        }
    }

    float Delta() => unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    IEnumerator Wait(float seconds)
    {
        if (unscaledTime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
    }


    static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 2f);
    static float EaseIn(float t)  => t * t;
}
