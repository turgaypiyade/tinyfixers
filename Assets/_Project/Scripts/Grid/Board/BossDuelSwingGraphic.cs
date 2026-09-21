using UnityEngine;
using UnityEngine.UI;

/// Pooled, downward weapon sweep. Attack progress drives the tip up to contact;
/// only the remaining glow animates independently after the hit.
public sealed class BossDuelSwingGraphic : MaskableGraphic
{
    private float strength, progress, fadeTime, fadeDuration;
    private bool released;

    public void Begin(float radius, float intensity, float direction)
    {
        strength = Mathf.Clamp01(intensity);
        progress = fadeTime = 0f;
        released = false;
        rectTransform.sizeDelta = Vector2.one * radius;
        rectTransform.localScale = new Vector3(direction < 0f ? -1f : 1f, 1f, 1f);
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    public void Release()
    {
        progress = 1f;
        released = true;
        fadeTime = 0f;
        fadeDuration = Mathf.Lerp(0.14f, 0.28f, strength);
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!released) return;
        fadeTime += Time.deltaTime;
        if (fadeTime >= fadeDuration) gameObject.SetActive(false);
        else SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (progress <= 0f) return;
        float fade = released ? Mathf.Clamp01(fadeTime / fadeDuration) : 0f;
        float alpha = (1f - fade) * (1f - fade);
        float head = 1f - (1f - progress) * (1f - progress);
        float tail = Mathf.Max(0f, head - Mathf.Lerp(0.6f, 1f, strength));
        tail = Mathf.Lerp(tail, head, fade * 0.7f);
        float radius = rectTransform.rect.width;
        float width = radius * Mathf.Lerp(0.055f, 0.14f, strength);

        AddRibbon(vh, tail, head, radius, width * 2.3f, new Color(1f, 0.08f, 0.01f, alpha * 0.16f));
        AddRibbon(vh, tail, head, radius, width * 1.35f, new Color(0.95f, 0.08f, 0.025f, alpha * 0.85f));
        AddRibbon(vh, tail, head, radius, width, new Color(1f, 0.38f, 0.025f, alpha));
        AddRibbon(vh, tail, head, radius, width * 0.5f, new Color(1f, 0.88f, 0.12f, alpha));
        AddRibbon(vh, tail, head, radius, width * 0.17f, new Color(1f, 1f, 0.75f, alpha));

        // Heavy hits leave a second, slimmer wake inside the main crescent.
        if (strength > 0.55f)
            AddRibbon(vh, tail, head * 0.94f, radius * 0.82f, width * 0.2f,
                new Color(1f, 0.65f, 0.07f, alpha * (strength - 0.55f) * 1.6f));
    }

    private static void AddRibbon(VertexHelper vh, float tail, float head, float radius, float width, Color tint)
    {
        if (head <= tail) return;
        const int segments = 40;
        const float endAngle = -24f * Mathf.Deg2Rad;
        Vector2 end = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
        int start = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments;
            float angle = Mathf.Lerp(112f, -24f, Mathf.Lerp(tail, head, u)) * Mathf.Deg2Rad;
            Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 center = (normal - end) * radius;
            // Taper both ends; the thicker leading section reads as a fast weapon edge.
            float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.65f) * Mathf.Lerp(0.25f, 1f, u);
            float halfWidth = width * taper * 0.5f;
            Color c = tint;
            c.a *= Mathf.Lerp(0.15f, 1f, u);
            vh.AddVert(center - normal * halfWidth, c, Vector2.zero);
            vh.AddVert(center + normal * halfWidth, c, Vector2.zero);
            if (i == 0) continue;
            int a = start + (i - 1) * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }
}
