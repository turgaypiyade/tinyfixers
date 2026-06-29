using System;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningBeam : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Yıldırım kaç parçaya bölünsün — daha fazla = daha detaylı kırılma")]
    [SerializeField] private int segments = 18;

    [Tooltip("Kırılma miktarı — uzunluğun yüzdesi olarak (0.15 = %15)")]
    [SerializeField, Range(0f, 0.5f)] private float jaggednessRatio = 0.15f;

    [Tooltip("Perlin noise hızı — düşük = yavaş dalgalanma, yüksek = titrek")]
    [SerializeField] private float noiseScale = 4.0f;

    [Tooltip("Her frame seed'ini değiştir (daha canlı flicker)")]
    [SerializeField] private bool liveFlicker = true;

    [Header("Thickness")]
    [Tooltip("Kaynak ucunun prefab kalınlığına göre çarpanı. Düşük değer = daha ince başlangıç.")]
    [SerializeField, Range(0.01f, 1f)] private float startWidthMultiplier = 0.25f;
    [Tooltip("Uç kalınlığının prefab kalınlığına göre çarpanı. Düşük değer = daha ip gibi görünüm.")]
    [SerializeField, Range(0.01f, 1f)] private float endWidthMultiplier = 0.08f;

    [Header("Timing")]
    [SerializeField] private float lifeTime = 0.20f;
    [SerializeField]
    private AnimationCurve alphaOverLife =
        AnimationCurve.EaseInOut(0, 1, 1, 0);

    public float extraLength = 0.15f;
    [SerializeField] private GameObject impactFlashPrefab;
    [SerializeField, Range(0f, 0.6f)] private float persistentPulseAmplitude = 0.24f;
    [SerializeField] private float persistentPulseFrequency = 18f;

    private LineRenderer lr;
    private float t;
    private Color startColor;
    private Color endColor;

    private Vector3 a;
    private Vector3 b;
    private bool initialized;
    private float seed;
    private bool persistent;
    private bool fadingOut;
    private float fadeOutDuration = 0.10f;
    private float fadeOutElapsed;
    private Func<Vector3> startProvider;
    private Func<Vector3> endProvider;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;

        startColor = lr.startColor;
        endColor = lr.endColor;

        seed = UnityEngine.Random.Range(0f, 1000f);
        ApplyWidthProfile();
    }

    public void Init(Vector3 start, Vector3 end)
    {
        Debug.Log($"[LightningBeam.Init] start={start} end={end}");

        a = start;
        b = end;

        a.z = 0f;
        b.z = 0f;

        initialized = true;

        // Segment count'u garantile — positionCount yazılmadan önce set et
        lr.positionCount = Mathf.Max(3, segments);
        ApplyWidthProfile();

        if (impactFlashPrefab != null)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 flashPos = b + dir * extraLength;

            var fx = Instantiate(impactFlashPrefab, transform);
            fx.transform.position = flashPos;
            fx.transform.localScale = Vector3.one * 0.35f;
        }

        BuildLightning();
    }

    public void InitPersistent(Func<Vector3> startProvider, Func<Vector3> endProvider, Color color)
    {
        this.startProvider = startProvider;
        this.endProvider = endProvider;

        persistent = true;
        fadingOut = false;
        fadeOutElapsed = 0f;
        t = 0f;

        startColor = color;
        endColor = Color.Lerp(Color.white, color, 0.72f);
        endColor.a = color.a;
        ApplyPersistentGradient(color);

        initialized = true;
        lr.positionCount = Mathf.Max(3, segments);
        ApplyWidthProfile();

        UpdatePersistentEndpoints();
        BuildLightning();
    }

    public void FadeOutAndDestroy(float duration)
    {
        if (!persistent)
        {
            Destroy(gameObject);
            return;
        }

        fadingOut = true;
        fadeOutDuration = Mathf.Max(0.02f, duration);
        fadeOutElapsed = 0f;
    }

    private void Update()
    {
        if (!initialized) return;

        t += Time.deltaTime;

        if (persistent)
        {
            UpdatePersistentEndpoints();

            float fade = 1f;
            if (fadingOut)
            {
                fadeOutElapsed += Time.deltaTime;
                fade = 1f - Mathf.Clamp01(fadeOutElapsed / fadeOutDuration);
            }

            float pulse = 1f - (persistentPulseAmplitude * (0.5f + 0.5f * Mathf.Sin((Time.time + seed) * persistentPulseFrequency)));
            ApplyAlphaScaled(Mathf.Clamp01(fade * pulse));

            if (liveFlicker)
                BuildLightning();

            if (fadingOut && fadeOutElapsed >= fadeOutDuration)
                Destroy(gameObject);

            return;
        }

        float u = Mathf.Clamp01(t / lifeTime);

        float alpha = alphaOverLife.Evaluate(u);
        ApplyAlphaAbsolute(alpha);

        // Yaşamının ilk %75'inde jagged şekli yeniden örer → flicker/animasyon
        if (u < 0.75f && liveFlicker)
            BuildLightning();

        if (t >= lifeTime)
            Destroy(gameObject);
    }

    private void ApplyAlphaAbsolute(float alpha)
    {
        var sc = startColor; sc.a = alpha;
        var ec = endColor; ec.a = alpha;
        lr.startColor = sc;
        lr.endColor = ec;
    }

    private void ApplyAlphaScaled(float alphaMultiplier)
    {
        var sc = startColor; sc.a = startColor.a * alphaMultiplier;
        var ec = endColor; ec.a = endColor.a * alphaMultiplier;
        lr.startColor = sc;
        lr.endColor = ec;
    }

    private void ApplyPersistentGradient(Color color)
    {
        if (lr == null)
            return;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(Color.Lerp(Color.white, color, 0.5f), 0.65f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a * 0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });

        lr.colorGradient = gradient;
    }

    private void UpdatePersistentEndpoints()
    {
        if (startProvider != null)
            a = startProvider();

        if (endProvider != null)
            b = endProvider();

        a.z = 0f;
        b.z = 0f;
    }

    private void ApplyWidthProfile()
    {
        if (lr == null)
            return;

        float baseStart = lr.startWidth;
        float baseEnd = lr.endWidth;

        lr.startWidth = Mathf.Max(0.001f, baseStart * startWidthMultiplier);
        lr.endWidth = Mathf.Max(0.001f, baseEnd * endWidthMultiplier);
    }

    private void BuildLightning()
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.001f) length = 0.001f;

        Vector3 forward = delta / length;

        // 2D perpendicular (Z düzlemi)
        Vector3 side = new Vector3(-forward.y, forward.x, 0f);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.up;

        // Jaggedness'i uzunluğun yüzdesi yap → ölçekten bağımsız görünür olur
        float jaggednessAmount = length * jaggednessRatio;

        // Zaman seed'i — her frame farklı olabilir (liveFlicker için)
        float timeSeed = liveFlicker ? (Time.time * noiseScale + seed) : seed;

        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float p = (count == 1) ? 0f : (float)i / (count - 1);
            Vector3 pos = Vector3.LerpUnclamped(a, b, p);

            // Uçlarda 0, ortada 1 — edge fade
            float edgeFade = Mathf.Sin(p * Mathf.PI);

            // Perlin noise — [-0.5, 0.5] aralığına çek
            float n1 = Mathf.PerlinNoise(timeSeed, p * 6f) - 0.5f;
            float n2 = Mathf.PerlinNoise(timeSeed + 100f, p * 12f) - 0.5f; // yüksek frekanslı ikinci katman
            float offset = (n1 * 0.7f + n2 * 0.3f) * 2f; // [-1, 1] civarı

            pos += side * (offset * jaggednessAmount * edgeFade);

            lr.SetPosition(i, pos);
        }
    }
}
