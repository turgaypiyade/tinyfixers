using UnityEngine;

/// <summary>
/// Roketin arkasında sürekli uzayan ışın izi.
/// Origin'den (kuyruk) head'e (roket) doğru büyür.
/// Alpha: kuyruk → şeffaf, baş → parlak.
/// Roket bitince FadeOutAndDestroy() ile söner ve silinir.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class RocketTrailBeam : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private float jaggedness = 0.20f;
    [SerializeField] private float noiseScale = 1.2f;
    [SerializeField] private int minSegments = 4;
    [SerializeField] private int segmentsPerUnit = 10;

    [Header("Alpha")]
    [SerializeField] private float tailAlpha = 0.0f;
    [SerializeField] private float headAlpha = 1.0f;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutDuration = 0.18f;

    [Header("Sorting (UI üstünde görünsün)")]
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 100;

    private LineRenderer lr;
    private Vector3 origin;
    private Vector3 head;
    private bool alive;
    private bool fadingOut;
    private float fadeTimer;
    private float masterAlpha = 1f;

    private Color baseStartColor;
    private Color baseEndColor;

    public void Init(Vector3 originWorld)
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;

        if (!string.IsNullOrEmpty(sortingLayerName))
            lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        baseStartColor = lr.startColor;
        baseEndColor = lr.endColor;

        originWorld.z = 0f;
        origin = originWorld;
        head = originWorld;
        alive = true;
        fadingOut = false;
        masterAlpha = 1f;

        lr.positionCount = 0;

        Debug.Log($"[RocketTrailBeam.Init] origin={origin} scale={transform.localScale}");

        ApplyGradient();
    }

    public void UpdateHead(Vector3 headWorld)
    {
        if (!alive || this == null) return;
        headWorld.z = 0f;
        head = headWorld;
    }

    public void FadeOutAndDestroy()
    {
        if (!alive || this == null) return;
        Debug.Log($"[RocketTrailBeam.FadeOut] origin={origin} head={head}");
        fadingOut = true;
        fadeTimer = 0f;
    }

    public void Kill()
    {
        alive = false;
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    private void Update()
    {
        if (!alive) return;
        if (this == null || lr == null) { alive = false; return; }

        BuildTrail();

        if (fadingOut)
        {
            fadeTimer += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(fadeTimer / fadeOutDuration);
            masterAlpha = 1f - u;
            ApplyGradient();

            if (u >= 1f)
            {
                alive = false;
                Destroy(gameObject);
            }
        }
    }

    private void ApplyGradient()
    {
        if (lr == null) return;

        float tA = tailAlpha * masterAlpha;
        float hA = headAlpha * masterAlpha;

        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(baseStartColor, 0f),
                new GradientColorKey(baseEndColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(tA, 0f),
                new GradientAlphaKey(hA, 1f)
            }
        );

        lr.colorGradient = grad;
    }

    private void BuildTrail()
    {
        if (lr == null) return;

        Vector3 dir = head - origin;
        float len = dir.magnitude;

        if (len < 0.001f)
        {
            lr.positionCount = 0;
            return;
        }

        int segCount = Mathf.Max(minSegments, Mathf.RoundToInt(len * segmentsPerUnit));
        lr.positionCount = segCount;

        Vector3 forward = dir / len;

        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(forward, Vector3.up);
        side.Normalize();

        float noiseSeed = (origin.x + origin.y) * 17f;

        for (int i = 0; i < segCount; i++)
        {
            float p = (segCount <= 1) ? 0f : (float)i / (segCount - 1);
            Vector3 pos = Vector3.Lerp(origin, head, p);

            float edgeFade = Mathf.Sin(p * Mathf.PI);
            float noise = Mathf.PerlinNoise(
                Time.unscaledTime * noiseScale + noiseSeed,
                p * noiseScale * 5f + noiseSeed
            ) - 0.5f;

            pos += side * (noise * jaggedness * edgeFade * len);

            lr.SetPosition(i, pos);
        }
    }
}