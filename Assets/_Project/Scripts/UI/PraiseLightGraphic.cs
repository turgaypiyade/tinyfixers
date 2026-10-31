using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Övgü efektinin ışık parçaları — doku/sprite yok, mesh koddan:
/// Beam: alttan yukarı uzanan ince hüzme (ortası parlak, kenarları ve uçları saydam),
/// Glow: yumuşak eliptik parıltı (merkez parlak → kenar saydam).
/// Renk alfası graphic.color'dan; şekil yalnız boyut değişince yeniden kurulur.
/// </summary>
public sealed class PraiseLightGraphic : MaskableGraphic
{
    public enum Shape { Beam, Glow }

    [SerializeField] private Shape shape = Shape.Beam;

    public Shape Kind
    {
        get => shape;
        set { if (shape == value) return; shape = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        if (shape == Shape.Beam) BuildBeam(vh, r);
        else BuildGlow(vh, r);
    }

    // 4 sütun × 3 satır: orta %30'luk çekirdek dolu, dış kenarlar saydam (geniş, yumuşak kenarlı hüzme);
    // alt %15 ve üst %45 boyunca alfa sıfıra iner (hüzme yukarıda incelir gibi söner).
    private static readonly float[] BeamColumns = { 0f, 0.35f, 0.65f, 1f };
    private static readonly float[] BeamRows = { 0f, 0.15f, 0.55f, 1f };
    private static readonly float[] BeamRowAlpha = { 0f, 1f, 1f, 0f };

    private void BuildBeam(VertexHelper vh, Rect r)
    {
        Color32 solid = color;
        Color32 clear = color; clear.a = 0;
        int cols = BeamColumns.Length;
        for (int row = 0; row < BeamRows.Length; row++)
            for (int col = 0; col < cols; col++)
            {
                Color32 c = col == 0 || col == cols - 1 ? clear : solid;
                c.a = (byte)(c.a * BeamRowAlpha[row]);
                vh.AddVert(new Vector3(r.xMin + r.width * BeamColumns[col], r.yMin + r.height * BeamRows[row]), c, Vector2.zero);
            }
        for (int row = 0; row < BeamRows.Length - 1; row++)
            for (int col = 0; col < cols - 1; col++)
            {
                int i = row * cols + col;
                vh.AddTriangle(i, i + cols, i + cols + 1);
                vh.AddTriangle(i, i + cols + 1, i + 1);
            }
    }

    // Merkez + 24 kenar noktası: merkez renkli, kenar saydam.
    private void BuildGlow(VertexHelper vh, Rect r)
    {
        const int segments = 24;
        Color32 edge = color; edge.a = 0;
        vh.AddVert(r.center, color, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.PI * 2f * i / segments;
            vh.AddVert(r.center + new Vector2(Mathf.Cos(a) * r.width * 0.5f, Mathf.Sin(a) * r.height * 0.5f), edge, Vector2.zero);
        }
        for (int i = 1; i <= segments; i++)
            vh.AddTriangle(0, i, i == segments ? 1 : i + 1);
    }
}
