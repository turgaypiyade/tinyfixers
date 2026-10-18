using UnityEngine;
using UnityEngine.UI;

/// <summary>One pooled mesh tethering two moving Override spheres; updated by the combo loop.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class OverrideComboLinkGraphic : MaskableGraphic
{
    private Vector2 from, to;
    private float size;
    private int tick;

    public void ResetLink()
    {
        from = to = Vector2.zero;
        size = 0f;
        tick = 0;
        SetVerticesDirty();
    }

    public void SetConnection(Vector2 a, Vector2 b, float radiusA, float radiusB, float sphereSize, float elapsed)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        Vector2 direction = length > 0.001f ? delta / length : Vector2.zero;
        Vector2 center = (a + b) * 0.5f;
        // Bind to the facing silhouettes. As spheres touch, the tether contracts into the impact.
        from = a + direction * Mathf.Min(radiusA, length * 0.45f) - center;
        to = b - direction * Mathf.Min(radiusB, length * 0.45f) - center;
        size = Mathf.Max(0f, sphereSize);
        tick = Mathf.FloorToInt(elapsed * 30f);
        rectTransform.anchoredPosition = center;
        rectTransform.sizeDelta = new Vector2(Mathf.Abs(delta.x) + size, Mathf.Abs(delta.y) + size);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 1f || size <= 0f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x) / length;
        float jitter = Mathf.Min(size * 0.18f, length * 0.12f);
        const int segments = 16;
        Vector2 previous = from;
        for (int i = 1; i <= segments; i++)
        {
            float progress = i / (float)segments;
            float displacement = i == segments ? 0f
                : (Noise(tick, i) - 0.5f) * 2f * jitter * Mathf.Sin(progress * Mathf.PI);
            Vector2 point = from + delta * progress + normal * displacement;
            float thick = Noise(tick / 2, i + 37) > 0.7f ? 1.7f : 1f;
            float alpha = Mathf.Lerp(0.8f, 1f, Noise(tick, i + 71));
            Segment(vh, previous, point, size * 0.12f * thick, new Color(0.1f, 0.55f, 1f, alpha * 0.22f));
            Segment(vh, previous, point, size * 0.05f * thick, new Color(0.25f, 0.85f, 1f, alpha * 0.7f));
            Segment(vh, previous, point, size * 0.022f * thick, new Color(0.95f, 1f, 1f, alpha));
            previous = point;
        }
    }

    private static float Noise(int time, int seed)
    {
        unchecked
        {
            uint v = (uint)time * 747796405u + (uint)seed * 2891336453u + 277803737u;
            v = ((v >> ((int)(v >> 28) + 4)) ^ v) * 277803737u;
            return ((v >> 22) ^ v) % 1024u / 1023f;
        }
    }

    private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.0001f) return;
        Vector2 side = new Vector2(-delta.y, delta.x) * (width * 0.5f / length);
        int first = vh.currentVertCount;
        vh.AddVert(a - side, tint, Vector2.zero);
        vh.AddVert(a + side, tint, Vector2.zero);
        vh.AddVert(b + side, tint, Vector2.zero);
        vh.AddVert(b - side, tint, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
