using System.Collections;
using UnityEngine;

/// <summary>
/// Ana ekrandaki event ikonlarına periyodik idle animasyonu ekler.
/// Her <idleInterval> saniyede bir bounce + sallama oynar.
/// Herhangi bir event ikon GameObject'ine ekle.
/// </summary>
public class EventIconAnimator : MonoBehaviour
{
    [Tooltip("Animasyonlar arasındaki bekleme süresi (saniye).")]
    [SerializeField, Min(0.5f)] private float idleInterval = 3f;

    [Tooltip("Bounce sırasında ulaşılacak maksimum scale çarpanı.")]
    [SerializeField, Range(1f, 1.5f)] private float bounceScale = 1.2f;

    [Tooltip("Sallama sırasında maksimum dönüş açısı (derece).")]
    [SerializeField, Range(0f, 30f)] private float shakeAngle = 12f;

    [Tooltip("Toplam animasyon süresi (saniye).")]
    [SerializeField, Min(0.1f)] private float animDuration = 0.55f;

    private RectTransform rt;
    private Vector3 baseScale;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        baseScale = rt != null ? rt.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        if (rt == null) return;
        StopAllCoroutines();
        StartCoroutine(IdleLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetTransform();
    }

    private void ResetTransform()
    {
        if (rt == null) return;
        rt.localScale = baseScale;
        rt.localEulerAngles = Vector3.zero;
    }

    private IEnumerator IdleLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(idleInterval);
            yield return PlayIdleAnim();
        }
    }

    private IEnumerator PlayIdleAnim()
    {
        float bounceDur = animDuration * 0.45f;
        float shakeDur  = animDuration * 0.55f;
        float q         = shakeDur * 0.25f;

        // ── Bounce ──────────────────────────────────────────────
        Vector3 peak = baseScale * bounceScale;

        for (float t = 0f; t < bounceDur * 0.5f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.Lerp(baseScale, peak,
                Mathf.SmoothStep(0f, 1f, t / (bounceDur * 0.5f)));
            yield return null;
        }
        for (float t = 0f; t < bounceDur * 0.5f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.Lerp(peak, baseScale,
                Mathf.SmoothStep(0f, 1f, t / (bounceDur * 0.5f)));
            yield return null;
        }
        rt.localScale = baseScale;

        // ── Shake (sağ → sol → orta) ─────────────────────────
        for (float t = 0f; t < q; t += Time.unscaledDeltaTime)
        {
            rt.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Lerp(0f, shakeAngle, Mathf.SmoothStep(0f, 1f, t / q)));
            yield return null;
        }
        for (float t = 0f; t < q * 2f; t += Time.unscaledDeltaTime)
        {
            rt.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Lerp(shakeAngle, -shakeAngle, Mathf.SmoothStep(0f, 1f, t / (q * 2f))));
            yield return null;
        }
        for (float t = 0f; t < q; t += Time.unscaledDeltaTime)
        {
            rt.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Lerp(-shakeAngle, 0f, Mathf.SmoothStep(0f, 1f, t / q)));
            yield return null;
        }

        ResetTransform();
    }
}
