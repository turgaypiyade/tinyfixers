using UnityEngine;
using UnityEngine.UI;

/// <summary>One pooled UI mesh for charge crackles; driven by OverrideSpecialView, no Update/coroutine.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class OverrideChargeGraphic : MaskableGraphic
{
    private int tick = -1;
    private float intensity;
    private float capAngle;
    private float capRock;
    private bool capLight;
    private float blurStrength;
    private float rockCos = 1f, rockSin;

    public void ResetCharge()
    {
        tick = -1;
        intensity = 0f;
        capAngle = capRock = 0f;
        capLight = false;
        blurStrength = rockSin = 0f;
        rockCos = 1f;
        SetVerticesDirty();
    }

    public void SetCharge(float elapsed, float progress, bool sustained,
        float capTurns = 0f, float rockDegrees = 0f, bool showCapLight = false, float speedBlur = 0f,
        float opacity = 1f)
    {
        // Reshape/flicker at 24 Hz instead of rebuilding the UI mesh every render frame.
        int next = Mathf.FloorToInt(elapsed * 24f);
        if (next == tick) return;
        tick = next;
        capAngle = capTurns * Mathf.PI * 2f;
        capRock = rockDegrees * Mathf.Deg2Rad;
        rockCos = Mathf.Cos(capRock);
        rockSin = Mathf.Sin(capRock);
        capLight = showCapLight;
        blurStrength = Mathf.Clamp01(speedBlur);
        intensity = Mathf.Clamp01(progress * 3f) * Mathf.Clamp01(opacity);
        if (!sustained) intensity *= Mathf.Clamp01((1f - progress) * 8f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (tick < 0 || intensity <= 0f) return;
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        if (size <= 0f) return;
        if (blurStrength > 0f) DrawSpeedTrails(vh, rect.center, size);
        // Three short, changing arcs on the silhouette, leaving the gems readable.
        const int segments = 7;
        for (int arc = 0; arc < 3; arc++)
        {
            float start = arc * 2.094395f + Noise(tick / 2, arc) * 6.283185f;
            Vector2 previous = Point(rect.center, size, start, arc, 0);
            float brightness = intensity * Mathf.Lerp(0.65f, 1f, Noise(tick, arc + 41));
            for (int i = 1; i <= segments; i++)
            {
                Vector2 point = Point(rect.center, size, start + i * 0.18f, arc, i);
                float thickness = Noise(tick / 3, arc * 11 + i) > 0.82f ? 1.6f : 1f;
                Segment(vh, previous, point, size * 0.095f * thickness, new Color(0.1f, 0.65f, 1f, brightness * 0.22f));
                Segment(vh, previous, point, size * 0.040f * thickness, new Color(0.3f, 0.9f, 1f, brightness * 0.72f));
                Segment(vh, previous, point, size * 0.018f * thickness, new Color(0.94f, 1f, 1f, brightness));
                previous = point;
            }
        }
        if (capLight) DrawCapLight(vh, rect.center, size);
    }

    private Vector2 Point(Vector2 center, float size, float angle, int arc, int vertex)
    {
        float radius = 0.525f + (Noise(tick + arc * 31, vertex) - 0.5f) * 0.15f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size * radius);
    }

    private void DrawCapLight(VertexHelper vh, Vector2 center, float size)
    {
        // The baked cap symbol barely changes between frames. A moving rim highlight
        // makes its motion readable without rotating/distorting the whole projected dome.
        const int segments = 16;
        Vector2 previous = CapPoint(center, size, capAngle);
        for (int i = 1; i <= segments; i++)
        {
            float progress = i / (float)segments;
            Vector2 point = CapPoint(center, size, capAngle + progress * 1.4f);
            float alpha = intensity * Mathf.Sin((i - 0.5f) / segments * Mathf.PI);
            Segment(vh, previous, point, size * 0.035f, new Color(1f, 0.72f, 0.12f, alpha * 0.35f));
            Segment(vh, previous, point, size * 0.012f, new Color(1f, 0.98f, 0.75f, alpha));
            previous = point;
        }
    }

    private Vector2 CapPoint(Vector2 center, float size, float angle)
    {
        // Registered full-sphere coordinates; orbit and cap centre share the halves' rocking pivot.
        Vector2 point = new Vector2(Mathf.Cos(angle) * 0.162f, 0.414f + Mathf.Sin(angle) * 0.078f);
        return RockPoint(center, size, point);
    }

    private void DrawSpeedTrails(VertexHelper vh, Vector2 center, float size)
    {
        // Soft colored streaks suggest a fast turn without blurring the cap or silhouette.
        // Both halves share this mesh/material; no ghost Images, textures or extra updates.
        const int segments = 10;
        for (int half = 0; half < 2; half++)
        {
            float direction = half == 0 ? 1f : -1f;
            float y = half == 0 ? 0.16f : -0.26f;
            float radiusY = half == 0 ? 0.13f : 0.09f;
            for (int trail = 0; trail < 3; trail++)
            {
                Color tint = trail == 0 ? new Color(0.12f, 0.65f, 1f, 1f)
                    : trail == 1 ? (half == 0 ? new Color(1f, 0.15f, 0.1f, 1f) : new Color(0.8f, 0.15f, 1f, 1f))
                    : new Color(0.25f, 1f, 0.12f, 1f);
                float start = direction * capAngle + trail * 2.094395f;
                Vector2 previous = TrailPoint(center, size, start, y, radiusY);
                for (int i = 1; i <= segments; i++)
                {
                    Vector2 point = TrailPoint(center, size, start + direction * i * 0.19f, y, radiusY);
                    float fade = Mathf.Sin((i - 0.5f) / segments * Mathf.PI) * intensity * blurStrength;
                    tint.a = fade * 0.12f;
                    Segment(vh, previous, point, size * 0.065f, tint);
                    tint.a = fade * 0.32f;
                    Segment(vh, previous, point, size * 0.025f, tint);
                    previous = point;
                }
            }
        }
    }

    private Vector2 TrailPoint(Vector2 center, float size, float angle, float y, float radiusY)
        => RockPoint(center, size, new Vector2(Mathf.Cos(angle) * 0.42f, y + Mathf.Sin(angle) * radiusY));

    private Vector2 RockPoint(Vector2 center, float size, Vector2 point)
        => center + new Vector2(point.x * rockCos - point.y * rockSin, point.x * rockSin + point.y * rockCos) * size;

    private static float Noise(int time, int seed)
    {
        // Local hash: no UnityEngine.Random mutation and no per-tick arrays/allocations.
        unchecked
        {
            uint value = (uint)time * 747796405u + (uint)seed * 2891336453u + 277803737u;
            value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
            return ((value >> 22) ^ value) % 1024u / 1023f;
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
