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

    [Header("Frame Animation (rotasyon yerine sprite değiştir)")]
    [Tooltip("2+ sprite verilirse ROTASYON tamamen kapanır; pervane bu frame'leri sırayla değiştirerek " +
             "'döner' (dairesel olmayan sprite robotun kafasında saçma dönmez). Boş bırakılırsa eski " +
             "rotasyon davranışı korunur. Board'daki idle pervane için 2 kanat-fazı sprite yeterli.")]
    [SerializeField] private Sprite[] spinFrames;
    [SerializeField, Min(1f)] private float spinFrameFps = 10f;
    [Tooltip("KAPALI (varsayılan): eski idle hissi — arada durur, sonra tekrar kısa bir spin yapar. " +
             "Cadence için Idle Delay / Idle Repeat Min-Max / Idle Spin Turns alanları kullanılır " +
             "(her burst = Idle Spin Turns tam tur). AÇIK: kesintisiz sürekli döner.")]
    [SerializeField] private bool frameSpinContinuous = false;

    public float SpinSpeed => spinSpeed;

    private RectTransform rt;
    private Image img;
    private Coroutine routine;
    private Coroutine idleRoutine;
    private Coroutine frameRoutine;

    private bool UseFrames => spinFrames != null && spinFrames.Length >= 2;

    // Idle patchbot tile'ının kullandığı SON bilinen frame seti. Uçuş (PatchbotDashUI) board'ın
    // PatchBotPropellerFrames alanı boşsa buradan idle spin'i alır (elle eşleme gerekmez).
    public static Sprite[] LastKnownSpinFrames { get; private set; }
    public static float LastKnownSpinFrameFps { get; private set; }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
    }

    private void OnEnable()
    {
        // Frame modu: rotasyon YOK, sprite'ları sırayla değiştirerek dön. Varsayılan periyodik
        // (arada durur, tekrar başlar — eski idle hissi); frameSpinContinuous ile kesintisiz.
        if (UseFrames)
        {
            // Idle frame setini global cache'e al → uçuş pervanesi bunu fallback olarak kullanır.
            LastKnownSpinFrames = spinFrames;
            LastKnownSpinFrameFps = spinFrameFps;

            if (rt != null) rt.localEulerAngles = Vector3.zero;
            frameRoutine = StartCoroutine(frameSpinContinuous ? CoFrameSpin() : CoFrameIdleWatch());
            return;
        }
        idleRoutine = StartCoroutine(CoIdleWatch());
    }
    private void OnDisable() => StopAll();

    // ── Public API ────────────────────────────────────────────────────────────

    public void PlayCreationAnimation()
    {
        if (!isActiveAndEnabled) return;
        if (UseFrames) return;   // frame animasyonu zaten dönüyor; rotasyonlu creation yok
        Stop();
        rt.localEulerAngles = Vector3.zero;
        routine = StartCoroutine(CoCreation());
    }

    // Uçan pervane runtime yaratılır; frame'leri buradan besleriz (tile prefab'ındaki 2-sprite ile aynı).
    public void SetSpinFrames(Sprite[] frames, float fps = -1f)
    {
        spinFrames = frames;
        if (fps > 0f) spinFrameFps = fps;
    }

    public void StartActivationSpin(float speedOverride = -1f)
    {
        if (!isActiveAndEnabled) return;

        if (idleRoutine  != null) { StopCoroutine(idleRoutine);  idleRoutine  = null; }
        if (frameRoutine != null) { StopCoroutine(frameRoutine); frameRoutine = null; }
        if (routine      != null) { StopCoroutine(routine);      routine      = null; }
        if (rt != null) rt.localEulerAngles = Vector3.zero;

        // Frame'ler verildiyse: rotasyon yerine SÜREKLI frame-spin (uçan pervane için).
        if (UseFrames)
        {
            frameRoutine = StartCoroutine(CoFrameSpin());
            return;
        }

        if (speedOverride > 0f) spinSpeed = speedOverride;
        routine = StartCoroutine(CoActivationSpin());
    }

    // Rotasyon yerine sprite frame'lerini sabit hızda döngüler (dairesel olmayan pervane için).
    private System.Collections.IEnumerator CoFrameSpin()
    {
        if (img == null || spinFrames == null || spinFrames.Length == 0)
            yield break;

        float step = 1f / Mathf.Max(1f, spinFrameFps);
        float t = 0f;
        int idx = 0;
        img.sprite = spinFrames[0];

        while (true)
        {
            t += Time.deltaTime;
            if (t >= step)
            {
                t -= step;
                idx = (idx + 1) % spinFrames.Length;
                img.sprite = spinFrames[idx];
            }
            yield return null;
        }
    }

    // Periyodik frame idle: dinlenme frame'inde durur, arada bir kısa spin yapar (eski idle hissi).
    // Cadence idleDelay / idleRepeatMin-Max; her burst idleSpinTurns tam tur (frame döngüsü) yapar.
    private System.Collections.IEnumerator CoFrameIdleWatch()
    {
        if (img != null && spinFrames != null && spinFrames.Length > 0)
            img.sprite = spinFrames[0];

        yield return new WaitForSeconds(idleDelay);

        while (true)
        {
            yield return CoFrameBurst();
            yield return new WaitForSeconds(Random.Range(idleRepeatMin, idleRepeatMax));
        }
    }

    private System.Collections.IEnumerator CoFrameBurst()
    {
        if (img == null || spinFrames == null || spinFrames.Length == 0)
            yield break;

        float step = 1f / Mathf.Max(1f, spinFrameFps);
        int totalSwaps = Mathf.Max(1, idleSpinTurns) * spinFrames.Length;   // idleSpinTurns tam tur
        float t = 0f;
        int idx = 0;
        int swaps = 0;

        while (swaps < totalSwaps)
        {
            t += Time.deltaTime;
            if (t >= step)
            {
                t -= step;
                idx = (idx + 1) % spinFrames.Length;
                img.sprite = spinFrames[idx];
                swaps++;
            }
            yield return null;
        }

        // Dinlenme frame'ine dön (durgun hâl).
        img.sprite = spinFrames[0];
    }

    // Ana animasyonu durdurur; idle watch çalışmaya devam eder.
    public void Stop()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (rt != null) rt.localEulerAngles = Vector3.zero;
    }

    private void StopAll()
    {
        if (routine      != null) { StopCoroutine(routine);      routine      = null; }
        if (idleRoutine  != null) { StopCoroutine(idleRoutine);  idleRoutine  = null; }
        if (frameRoutine != null) { StopCoroutine(frameRoutine); frameRoutine = null; }
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
