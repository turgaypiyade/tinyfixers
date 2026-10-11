using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lazer topunun ışın bandı: namlu ucunda dar başlayıp hedefe doğru çok küçük bir açıyla genişleyen yamuk (4 köşe).
/// Pivot namlu ucunda (0, 0.5); +x hedefe bakar. Doku enine yumuşak kenarlı bant, boyuna düz.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LaserBeamGraphic : MaskableGraphic
{
    private Texture texture;
    private float length, startWidth, endWidth;

    public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

    public void SetTexture(Texture tex)
    {
        texture = tex;
        SetMaterialDirty();
    }

    public void SetShape(float beamLength, float widthAtMuzzle, float widthAtTarget)
    {
        if (Mathf.Approximately(length, beamLength) && Mathf.Approximately(startWidth, widthAtMuzzle)
            && Mathf.Approximately(endWidth, widthAtTarget))
            return;
        length = beamLength;
        startWidth = widthAtMuzzle;
        endWidth = widthAtTarget;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (length <= 0f) return;
        var c = (Color32)color;
        float s = startWidth * 0.5f, e = endWidth * 0.5f;
        vh.AddVert(new Vector3(0f, -s), c, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(0f, s), c, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(length, e), c, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(length, -e), c, new Vector2(1f, 0f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
