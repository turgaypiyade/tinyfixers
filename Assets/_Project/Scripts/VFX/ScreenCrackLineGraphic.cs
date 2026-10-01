using UnityEngine;
using UnityEngine.UI;

/// <summary>Tapers a standard UI Image, retaining its renderer, material and rebuild lifecycle.</summary>
[RequireComponent(typeof(Image))]
public sealed class ScreenCrackLineGraphic : BaseMeshEffect
{
    private float startWidth, endWidth, fullLength;

    public void SetWidths(float start, float end, float length)
    {
        startWidth = start;
        endWidth = end;
        fullLength = Mathf.Max(1f, length);
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || graphic == null || fullLength <= 0f) return;
        Rect r = graphic.GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        UIVertex vertex = default;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float progress = Mathf.Clamp01((vertex.position.x - r.xMin) / fullLength);
            float width = Mathf.Lerp(startWidth, endWidth, progress);
            vertex.position.y = r.center.y + (vertex.position.y - r.center.y) * width / r.height;
            vh.SetUIVertex(vertex, i);
        }
    }
}
