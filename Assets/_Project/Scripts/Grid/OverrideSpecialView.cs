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

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        BuildChildren();
        HideAll();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void PlayCreation()
    {
        if (!gameObject.activeInHierarchy) return;
        Stop();
        BuildChildren();

        if (topHalfSprite    != null && topImage    != null) topImage.sprite    = topHalfSprite;
        if (bottomHalfSprite != null && bottomImage != null) bottomImage.sprite = bottomHalfSprite;
        if (fullSphereSprite != null && fullImage   != null) fullImage.sprite   = fullSphereSprite;

        SyncChildSizes();
        routine = StartCoroutine(CoCreation());
    }

    public void Stop()
    {
        if (routine     != null) { StopCoroutine(routine);     routine     = null; }
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        HideAll();
    }

    // ── Layout sync ──────────────────────────────────────────────────────────

    private void OnRectTransformDimensionsChange() => SyncChildSizes();

    private void SyncChildSizes()
    {
        Vector2 size = rt.sizeDelta;
        if (size.x <= 0f) size = rt.rect.size;
        if (size.x <= 0f) return;
        SetChildSize(topImage,    size);
        SetChildSize(bottomImage, size);
        SetChildSize(fullImage,   size);
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
        ResetHalf(topImage);
        ResetHalf(bottomImage);
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
        // Ease-out: başta hızlı döner, sona doğru yavaşlar ve durur
        float totalAngle = spinTurns * 360f;
        float sep = rt.sizeDelta.y * spinSeparationRatio;

        float t = 0f;
        while (t < spinDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / spinDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f);   // ease-out cubic
            float angle = Mathf.Lerp(totalAngle, 0f, e);
            float yOff  = Mathf.Lerp(sep, 0f, e);
            float alpha = Mathf.Clamp01(k * 4f);     // hızlı görünür olur

            SpinHalf(topImage,     yOff,  angle, alpha);
            SpinHalf(bottomImage, -yOff, -angle, alpha);
            yield return null;
        }
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

    private static void SpinHalf(Image img, float yOffset, float zAngle, float alpha)
    {
        if (img == null) return;
        img.rectTransform.anchoredPosition = new Vector2(0f, yOffset);
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, zAngle);
        img.color = new Color(1f, 1f, 1f, alpha);
    }

}
