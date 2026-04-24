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

    [Header("Timing")]
    [SerializeField] private float lifeTime = 0.20f;
    [SerializeField]
    private AnimationCurve alphaOverLife =
        AnimationCurve.EaseInOut(0, 1, 1, 0);

    public float extraLength = 0.15f;
    [SerializeField] private GameObject impactFlashPrefab;

    private LineRenderer lr;
    private float t;
    private Color startColor;
    private Color endColor;

    private Vector3 a;
    private Vector3 b;
    private bool initialized;
    private float seed;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;

        startColor = lr.startColor;
        endColor = lr.endColor;

        seed = Random.Range(0f, 1000f);
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

    private void Update()
    {
        if (!initialized) return;

        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / lifeTime);

        float alpha = alphaOverLife.Evaluate(u);
        var sc = startColor; sc.a = alpha;
        var ec = endColor; ec.a = alpha;
        lr.startColor = sc;
        lr.endColor = ec;

        // Yaşamının ilk %75'inde jagged şekli yeniden örer → flicker/animasyon
        if (u < 0.75f && liveFlicker)
            BuildLightning();

        if (t >= lifeTime)
            Destroy(gameObject);
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