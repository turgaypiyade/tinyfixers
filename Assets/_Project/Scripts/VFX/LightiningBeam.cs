using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningBeam : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private int segments = 12;
    [SerializeField] private float jaggedness = 0.25f;
    [SerializeField] private float noiseScale = 1.0f;

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

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;

        startColor = lr.startColor;
        endColor = lr.endColor;
    }

    public void Init(Vector3 start, Vector3 end)
    {
        Debug.Log($"[LightningBeam.Init] start={start} end={end}");

        a = start;
        b = end;

        a.z = 0f;
        b.z = 0f;

        initialized = true;

        Vector3 dir = (end - start).normalized;
        end += dir * extraLength;

        lr.positionCount = Mathf.Max(2, segments);
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        if (impactFlashPrefab != null)
        {
            var fx = Instantiate(impactFlashPrefab, transform);
            fx.transform.position = end;
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

        if (u < 0.75f)
            BuildLightning();

        if (t >= lifeTime)
            Destroy(gameObject);
    }
    private void BuildLightning()
    {
        Vector3 dir = (b - a);
        float len = dir.magnitude;
        if (len < 0.001f) len = 0.001f;

        Vector3 forward = dir / len;

        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(forward, Camera.main ? Camera.main.transform.forward : Vector3.forward);
        side.Normalize();

        for (int i = 0; i < lr.positionCount; i++)
        {
            float p = (lr.positionCount == 1) ? 0 : (float)i / (lr.positionCount - 1);
            Vector3 pos = Vector3.Lerp(a, b, p);

            float edgeFade = Mathf.Sin(p * Mathf.PI);
            float n = Mathf.PerlinNoise(Time.time * noiseScale, p * noiseScale) - 0.5f;

            pos += side * (n * jaggedness * edgeFade);

            lr.SetPosition(i, pos);
        }
    }
}