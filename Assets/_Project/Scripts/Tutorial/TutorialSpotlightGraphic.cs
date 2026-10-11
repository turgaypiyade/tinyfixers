using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Öğretici spot ışığının karartması: tüm paneller TEK mesh'te, kenarları ortak (üst üste binme yok) →
/// birleşim yerlerinde koyu çizgi ya da ince boşluk oluşmaz. Dikdörtgenler TutorialOverlayController'dan gelir.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialSpotlightGraphic : MaskableGraphic
{
    private readonly List<Rect> rects = new();

    public void SetRects(List<Rect> source)
    {
        rects.Clear();
        if (source != null) rects.AddRange(source);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var c = (Color32)color;
        for (int i = 0; i < rects.Count; i++)
        {
            var r = rects[i];
            if (r.width <= 0f || r.height <= 0f) continue;
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, Vector2.one);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.right);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }
}
