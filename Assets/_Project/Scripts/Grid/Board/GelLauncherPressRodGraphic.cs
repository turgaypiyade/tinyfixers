using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared-material shaft. Only a four-vertex quad changes during a hit, never a texture.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GelLauncherPressRodGraphic : MaskableGraphic
{
    private float phase;
    public void SetPhase(float value)
    {
        if (Mathf.Approximately(phase, value)) return;
        phase = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float aspect = r.height / Mathf.Max(1f, r.width);
        var v = UIVertex.simpleVert;
        v.color = color;
        v.uv1 = new Vector4(phase, aspect, 0f, 0f);
        v.position = new Vector3(r.xMin, r.yMin); v.uv0 = new Vector4(0f, 0f); vh.AddVert(v);
        v.position = new Vector3(r.xMin, r.yMax); v.uv0 = new Vector4(0f, 1f); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMax); v.uv0 = new Vector4(1f, 1f); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMin); v.uv0 = new Vector4(1f, 0f); vh.AddVert(v);
        vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
    }
}
