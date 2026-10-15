using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Draws four independent petal sectors from the existing flower sprite without new textures.</summary>
public sealed class WaterLilyPetalImage : Image
{
    private int petals = 15;
    private bool center = true;
    public void SetPetals(int mask, bool keepCenter = true)
    {
        if (petals == mask && center == keepCenter) return;
        petals = mask; center = keepCenter; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (sprite == null) return;
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        rect = new Rect(rect.center - Vector2.one * size * 0.5f, Vector2.one * size);
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        // Full and damaged flowers use the exact same canvas/UV mapping. Removing a
        // petal changes only the mask, never the size/padding of the remaining artwork.
        if (petals == 15 && center)
        {
            Add(vh, rect, uv, new Vector2(0, 0));
            Add(vh, rect, uv, new Vector2(0, 1));
            Add(vh, rect, uv, new Vector2(1, 1));
            Add(vh, rect, uv, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
            return;
        }
        Vector2 pivot = new Vector2(0.5f, 0.475f);
        const int segments = 8;
        const float radius = 0.12f;
        for (int petal = 0; petal < 4; petal++)
        {
            if ((petals & (1 << petal)) == 0) continue;
            for (int i = 0; i < segments; i++)
            {
                float a = (45f + petal * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                float b = a + 90f / segments * Mathf.Deg2Rad;
                Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int v = vh.currentVertCount;
                Add(vh, rect, uv, pivot + da * radius);
                Add(vh, rect, uv, Edge(pivot, da));
                Add(vh, rect, uv, Edge(pivot, db));
                Add(vh, rect, uv, pivot + db * radius);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 3, v);
            }
        }
        if (!center) return;
        int first = vh.currentVertCount;
        Add(vh, rect, uv, pivot);
        for (int i = 0; i <= 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            Add(vh, rect, uv, pivot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
        }
    }
    private static Vector2 Edge(Vector2 pivot, Vector2 direction)
    {
        float x = Mathf.Abs(direction.x) < 0.0001f ? float.MaxValue
            : (direction.x > 0 ? 1 - pivot.x : -pivot.x) / direction.x;
        float y = Mathf.Abs(direction.y) < 0.0001f ? float.MaxValue
            : (direction.y > 0 ? 1 - pivot.y : -pivot.y) / direction.y;
        return pivot + direction * Mathf.Min(x, y);
    }
    private void Add(VertexHelper vh, Rect rect, Vector4 uv, Vector2 point)
    {
        vh.AddVert(new Vector3(rect.x + point.x * rect.width, rect.y + point.y * rect.height), color,
            new Vector2(Mathf.Lerp(uv.x, uv.z, point.x), Mathf.Lerp(uv.y, uv.w, point.y)));
    }
}
