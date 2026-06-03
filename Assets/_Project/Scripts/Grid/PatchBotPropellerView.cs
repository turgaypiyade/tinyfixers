using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class PatchBotPropellerView : MonoBehaviour
{
    [Header("Creation — CCW sonra CW (4'er tur, ease-out)")]
    [SerializeField] private float creationCcwDuration = 0.70f;
    [SerializeField] private float creationCwDuration  = 0.58f;

    [Header("Creation — Azalan wobble")]
    [SerializeField] private float wobbleFirstAngle = 44f;
    [SerializeField] private float wobbleDuration   = 0.15f;
    [SerializeField] private float wobbleDecay      = 0.38f;
    [SerializeField] private int   wobbleHalfSteps  = 4;

    [Header("Activation Spin (CW, sürekli)")]
    [SerializeField] private float spinSpeed  = 960f;
    [SerializeField] private float spinUpTime = 0.10f;

    [Header("Idle Spin (dokunulmayınca)")]
    [SerializeField] private float idleDelay       = 2f;
    [SerializeField] private float idleRepeatMin   = 3f;
    [SerializeField] private float idleRepeatMax   = 6f;
    [SerializeField] private int   idleSpinTurns   = 2;      // tam tur → 0'da biter
    [SerializeField] private float idleSpinDuration = 0.75f;

    public float SpinSpeed => spinSpeed;

    private RectTransform rt;
    private Coroutine routine;
    private Coroutine idleRoutine;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        idleRoutine = StartCoroutine(CoIdleWatch());
        if (routine == null)
            StartActivationSpin();
    }
    private void OnDisable() => StopAll();

    // ── Public API ────────────────────────────────────────────────────────────

    public void PlayCreationAnimation()
    {
        if (!isActiveAndEnabled) return;
        Stop();
        rt.localEulerAngles = Vector3.zero;
        routine = StartCoroutine(CoCreation());
    }

    public void StartActivationSpin(float speedOverride = -1f)
    {
        if (!isActiveAndEnabled) return;
        if (speedOverride > 0f) spinSpeed = speedOverride;
        Stop();
        routine = StartCoroutine(CoActivationSpin());
    }

    // Ana animasyonu durdurur; idle watch çalışmaya devam eder.
    public void Stop()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (rt != null) rt.localEulerAngles = Vector3.zero;
    }

    private void StopAll()
    {
        if (routine     != null) { StopCoroutine(routine);     routine     = null; }
        if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
        if (rt != null) rt.localEulerAngles = Vector3.zero;
    }

    // ── Idle watch ────────────────────────────────────────────────────────────

    private IEnumerator CoIdleWatch()
    {
        yield return new WaitForSeconds(idleDelay);

        while (true)
        {
            if (routine == null)
                yield return CoIdleSpin();

            yield return new WaitForSeconds(Random.Range(idleRepeatMin, idleRepeatMax));
        }
    }

    private IEnumerator CoIdleSpin()
    {
        // Sadece dinlenme konumundayken spin yap (yaratma/aktivasyon animasyonu bitmemişse atla)
        float curZ = rt.localEulerAngles.z;
        if (curZ > 0.5f && curZ < 359.5f) yield break;

        yield return CoPhase(idleSpinTurns * 360f, idleSpinDuration);
        // Tam tur kullandığımız için z=0'da biter, snap gerekmez
    }

    // ── Creation coroutine ────────────────────────────────────────────────────

    private IEnumerator CoCreation()
    {
        yield return CoPhase(-4f * 360f, creationCcwDuration);
        yield return CoPhase( 4f * 360f, creationCwDuration);

        float angle = wobbleFirstAngle;
        for (int i = 0; i < wobbleHalfSteps; i++)
        {
            float dir = (i % 2 == 0) ? 1f : -1f;
            yield return CoRelative(dir * angle, wobbleDuration);
            angle *= wobbleDecay;
        }

        float curZ = rt.localEulerAngles.z;
        if (curZ > 180f) curZ -= 360f;
        yield return CoAbsolute(curZ, 0f, 0.07f);
        routine = null;
    }

    // totalDegrees > 0 → CW, < 0 → CCW, cubic ease-out (hızdan yavaşlamaya)
    private IEnumerator CoPhase(float totalDegrees, float duration)
    {
        float startZ  = rt.localEulerAngles.z;
        float elapsed = 0f;
        float safeDur = Mathf.Max(0.01f, duration);

        while (elapsed < safeDur)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / safeDur);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            rt.localEulerAngles = new Vector3(0f, 0f, startZ + eased * totalDegrees);
            yield return null;
        }
        rt.localEulerAngles = new Vector3(0f, 0f, startZ + totalDegrees);
    }

    private IEnumerator CoRelative(float degrees, float duration)
    {
        float startZ = rt.localEulerAngles.z;
        if (startZ > 180f) startZ -= 360f;
        yield return CoAbsolute(startZ, startZ + degrees, duration);
    }

    private IEnumerator CoAbsolute(float fromZ, float toZ, float duration)
    {
        float elapsed = 0f;
        float safeDur = Mathf.Max(0.001f, duration);

        while (elapsed < safeDur)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / safeDur);
            float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(fromZ, toZ, eased));
            yield return null;
        }
        rt.localEulerAngles = new Vector3(0f, 0f, toZ);
    }

    // ── Activation spin ───────────────────────────────────────────────────────

    private IEnumerator CoActivationSpin()
    {
        float elapsed = 0f;
        float safeSpin = Mathf.Max(0.001f, spinUpTime);

        while (elapsed < safeSpin)
        {
            elapsed += Time.deltaTime;
            float speed = Mathf.Lerp(0f, spinSpeed, elapsed / safeSpin);
            rt.Rotate(0f, 0f, speed * Time.deltaTime);
            yield return null;
        }

        while (true)
        {
            rt.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
