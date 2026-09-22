using UnityEngine;
using UnityEngine.UI;

/// Tapered red/orange/yellow strike streaks centered on the weapon contact point.
public sealed class BossDuelImpactGraphic : MaskableGraphic
{
    private float strength, elapsed, lifetime;

    public void Play(float size, float intensity, float duration)
    {
        strength = Mathf.Clamp01(intensity);
        elapsed = 0f;
        lifetime = Mathf.Max(0.02f, duration);
        rectTransform.sizeDelta = Vector2.one * size;
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) gameObject.SetActive(false);
        else SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.02f, lifetime));
        float fade = (1f - t) * (1f - t);
        float size = rectTransform.rect.width;
        int count = Mathf.RoundToInt(Mathf.Lerp(4f, 10f, strength));
        // Fan-shaped curved streaks rise from the contact point like a downward strike's wake.
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(15f, 165f, (float)i / Mathf.Max(1, count - 1)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-dir.y, dir.x);
            float length = size * (i % 2 == 0 ? 0.55f : 0.42f) * (1f + t * 0.2f);
            float width = size * Mathf.Lerp(0.018f, 0.04f, strength);
            Color outer = new Color(1f, Mathf.Lerp(0.55f, 0.14f, strength), 0.025f, fade * 0.8f);
            AddStreak(vh, dir, side, length, width * 1.8f, t, outer);
            AddStreak(vh, dir, side, length * 0.92f, width * 0.65f, t,
                new Color(1f, 0.92f, 0.22f, fade));
        }
    }

    private static void AddStreak(VertexHelper vh, Vector2 dir, Vector2 side,
        float length, float width, float time, Color tint)
    {
        const int segments = 10;
        int start = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            Vector2 center = dir * (length * (u + time * 0.1f)) + side * (length * 0.2f * u * u);
            Vector2 tangent = (dir + side * (0.4f * u)).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float halfWidth = width * Mathf.Sin(u * Mathf.PI) * 0.5f;
            Color c = tint;
            c.a *= 1f - u * 0.75f;
            vh.AddVert(center - normal * halfWidth, c, Vector2.zero);
            vh.AddVert(center + normal * halfWidth, c, Vector2.zero);
            if (i == 0) continue;
            int a = start + (i - 1) * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }
}
