using UnityEngine;
using UnityEngine.UI;

/// <summary>Small UI mesh effect for the hatch, three-way split, target rings and impacts.</summary>
public sealed class EggBirdBurstGraphic : MaskableGraphic
{
    private float lifetime;
    private float elapsed;
    private bool target;

    public static EggBirdBurstGraphic Spawn(RectTransform parent, Vector2 position,
        float size, Color tint, float lifetime, bool target)
    {
        var go = new GameObject(target ? "EggBird_TargetRing" : "EggBird_Burst",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(EggBirdBurstGraphic));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = parent.pivot;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * size;
        var graphic = go.GetComponent<EggBirdBurstGraphic>();
        graphic.color = tint;
        graphic.raycastTarget = false;
        graphic.lifetime = Mathf.Max(0.05f, lifetime);
        graphic.target = target;
        return graphic;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) Destroy(gameObject);
        else SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, lifetime));
        float size = rectTransform.rect.width;
        float fade = target ? 0.55f + 0.2f * Mathf.Sin(t * Mathf.PI * 6f) : (1f - t) * (1f - t);
        float radius = size * (target ? 0.38f + 0.025f * Mathf.Sin(t * 12f) : Mathf.Lerp(0.08f, 0.48f, Mathf.Sqrt(t)));
        Color tint = color;
        tint.a *= fade;
        AddRing(vh, radius, size * (target ? 0.024f : 0.028f * (1f - t)), tint);
        if (target) return;

        // White-hot center, soft colored edge, and twelve outward sparks.
        float core = size * Mathf.Lerp(0.08f, 0.31f, Mathf.Sqrt(t));
        Color center = Color.Lerp(Color.white, color, t);
        center.a = Mathf.Clamp01((1f - t * 2f) * 0.9f);
        AddDisc(vh, core, center, new Color(color.r, color.g, color.b, 0f));
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6f + 0.13f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float reach = radius * (i % 2 == 0 ? 1.05f : 0.87f);
            Vector2 mid = direction * reach;
            float halfWidth = size * 0.018f * (1f - t);
            AddQuad(vh, mid - direction * size * 0.07f,
                mid + perpendicular * halfWidth, mid + direction * size * 0.04f,
                mid - perpendicular * halfWidth, tint);
        }
    }

    private static void AddRing(VertexHelper vh, float radius, float thickness, Color color)
    {
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            AddQuad(vh, da * radius, db * radius,
                db * Mathf.Max(0f, radius - thickness), da * Mathf.Max(0f, radius - thickness), color);
        }
    }

    private static void AddDisc(VertexHelper vh, float radius, Color center, Color edge)
    {
        int start = vh.currentVertCount;
        vh.AddVert(Vector3.zero, center, Vector2.zero);
        const int segments = 48;
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            vh.AddVert(new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius, edge, Vector2.zero);
            if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
