using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PulseCoreExplosionFX : MonoBehaviour
{
    [SerializeField] private Image sunburstRays;
    [SerializeField] private Image innerGlow;
    [SerializeField] private Image shockwaveRing;
    [SerializeField] private Image coreFlash;

    [SerializeField] private float baseSize = 300f;
    [SerializeField, Range(0.5f, 2.0f)] private float areaOvershoot = 1.15f;

    [SerializeField] private float totalDuration = 0.85f;
    [SerializeField] private bool destroyOnFinish = true;

    [SerializeField, Range(0.1f, 2f)] private float flashPeakSizeRatio = 0.75f;
    [SerializeField] private float flashInTime = 0.05f;
    [SerializeField] private float flashOutTime = 0.22f;
    [SerializeField] private float flashStartRatio = 0.3f;
    [SerializeField] private float flashEndRatio = 1.15f;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 1f);

    [SerializeField, Range(0.1f, 2f)] private float glowPeakSizeRatio = 0.90f;
    [SerializeField] private float glowInTime = 0.08f;
    [SerializeField] private float glowOutTime = 0.50f;
    [SerializeField] private float glowStartRatio = 0.3f;
    [SerializeField] private float glowEndRatio = 1.1f;
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.1f, 0.75f);

    [SerializeField, Range(0.1f, 2f)] private float raysPeakSizeRatio = 1.00f;
    [SerializeField] private float raysInTime = 0.10f;
    [SerializeField] private float raysOutTime = 0.60f;
    [SerializeField] private float raysStartRatio = 0.2f;
    [SerializeField] private float raysEndRatio = 1.1f;
    [SerializeField] private float raysRotateSpeed = 90f;
    [SerializeField] private Color raysColor = new Color(1f, 0.82f, 0.25f, 0.95f);

    [SerializeField, Range(0.1f, 2f)] private float ringPeakSizeRatio = 1.10f;
    [SerializeField] private float ringInTime = 0.03f;
    [SerializeField] private float ringOutTime = 0.50f;
    [SerializeField] private float ringStartRatio = 0.2f;
    [SerializeField] private float ringEndRatio = 1.15f;
    [SerializeField] private Color ringColor = new Color(1f, 0.75f, 0.2f, 1f);

    [SerializeField]
    private AnimationCurve easeOut =
        new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(1, 1, 0, 0));

    [SerializeField]
    private AnimationCurve easeIn =
        new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 3));

    private Coroutine playRoutine;
    private bool isShuttingDown;

    public void SetRadiusCells(int radiusCells, float tileSize)
    {
        int sideCells = radiusCells * 2 + 1;
        SetAreaCells(sideCells, tileSize);
    }

    public void SetAreaCells(int sideCells, float tileSize)
    {
        baseSize = tileSize * sideCells * areaOvershoot;
    }

    public void SetBaseSize(float size)
    {
        baseSize = size;
    }

    public void SetLifetime(float lifetime)
    {
        totalDuration = Mathf.Max(0.01f, lifetime);
    }

    private void OnEnable()
    {
        isShuttingDown = false;
        HideAll();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(DeferredStart());
    }

    private void OnDisable()
    {
        isShuttingDown = true;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    private IEnumerator DeferredStart()
    {
        yield return null;

        if (isShuttingDown || this == null || !isActiveAndEnabled)
            yield break;

        yield return PlayExplosion();
        playRoutine = null;
    }

    private void HideAll()
    {
        if (sunburstRays != null) sunburstRays.color = WithAlpha(raysColor, 0f);
        if (innerGlow != null) innerGlow.color = WithAlpha(glowColor, 0f);
        if (shockwaveRing != null) shockwaveRing.color = WithAlpha(ringColor, 0f);
        if (coreFlash != null) coreFlash.color = WithAlpha(flashColor, 0f);
    }

    private IEnumerator PlayExplosion()
    {
        yield return StartCoroutine(CoPlayExplosion());

        if (!isShuttingDown && destroyOnFinish && gameObject != null)
            Destroy(gameObject);
    }

    private IEnumerator CoPlayExplosion()
    {
        Coroutine flash = StartCoroutine(AnimFlash());
        Coroutine glow = StartCoroutine(AnimGlow());
        Coroutine rays = StartCoroutine(AnimRays());
        Coroutine ring = StartCoroutine(AnimRing());

        yield return new WaitForSeconds(totalDuration);

        if (isShuttingDown)
            yield break;

        if (flash != null) StopCoroutine(flash);
        if (glow != null) StopCoroutine(glow);
        if (rays != null) StopCoroutine(rays);
        if (ring != null) StopCoroutine(ring);
    }

    private IEnumerator AnimFlash()
    {
        if (coreFlash == null) yield break;
        float peak = baseSize * flashPeakSizeRatio;
        yield return AnimateLayer(coreFlash, flashColor, peak * flashStartRatio, peak, 0f, 1f, flashInTime, easeOut);
        yield return AnimateLayer(coreFlash, flashColor, peak, peak * flashEndRatio, 1f, 0f, flashOutTime, easeIn);
    }

    private IEnumerator AnimGlow()
    {
        if (innerGlow == null) yield break;
        float peak = baseSize * glowPeakSizeRatio;
        yield return AnimateLayer(innerGlow, glowColor, peak * glowStartRatio, peak, 0f, glowColor.a, glowInTime, easeOut);
        yield return AnimateLayer(innerGlow, glowColor, peak, peak * glowEndRatio, glowColor.a, 0f, glowOutTime, easeIn);
    }

    private IEnumerator AnimRays()
    {
        if (sunburstRays == null) yield break;
        float peak = baseSize * raysPeakSizeRatio;

        if (sunburstRays.rectTransform == null)
            yield break;

        sunburstRays.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        yield return AnimateLayerWithRotation(sunburstRays, raysColor, peak * raysStartRatio, peak, 0f, raysColor.a, raysInTime, easeOut);
        yield return AnimateLayerWithRotation(sunburstRays, raysColor, peak, peak * raysEndRatio, raysColor.a, 0f, raysOutTime, easeOut);
    }

    private IEnumerator AnimRing()
    {
        if (shockwaveRing == null) yield break;
        float peak = baseSize * ringPeakSizeRatio;
        yield return AnimateLayer(shockwaveRing, ringColor, peak * ringStartRatio, peak, 0f, ringColor.a, ringInTime, easeOut);
        yield return AnimateLayer(shockwaveRing, ringColor, peak, peak * ringEndRatio, ringColor.a, 0f, ringOutTime, easeOut);
    }

    private IEnumerator AnimateLayer(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;
        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private IEnumerator AnimateLayerWithRotation(
        Image img,
        Color baseCol,
        float fromSize,
        float toSize,
        float fromA,
        float toA,
        float duration,
        AnimationCurve curve)
    {
        if (img == null || duration <= 0f)
            yield break;

        RectTransform rt = img.rectTransform;
        if (rt == null)
            yield break;

        rt.localScale = Vector3.one;

        float t = 0f;
        while (t < duration)
        {
            if (isShuttingDown || img == null || rt == null)
                yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = curve.Evaluate(u);
            float size = Mathf.Lerp(fromSize, toSize, eased);

            rt.sizeDelta = new Vector2(size, size);
            img.color = WithAlpha(baseCol, Mathf.Lerp(fromA, toA, eased));
            rt.localRotation *= Quaternion.Euler(0, 0, raysRotateSpeed * Time.deltaTime);

            yield return null;
        }

        if (isShuttingDown || img == null || rt == null)
            yield break;

        rt.sizeDelta = new Vector2(toSize, toSize);
        img.color = WithAlpha(baseCol, toA);
    }

    private static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }
}
