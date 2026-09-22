using UnityEngine;
using UnityEngine.UI;

/// Small shield badges and daze stars, without font glyph or texture dependencies.
public sealed class BossDuelStatusGraphic : MaskableGraphic
{
    public bool Star { get; set; }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        Vector2 center = rect.center;
        vh.AddVert(center, color, Vector2.zero);
        int count = Star ? 10 : 6;
        for (int i = 0; i < count; i++)
        {
            Vector2 point;
            if (Star)
            {
                float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? 0.5f : 0.23f;
                point = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            else
            {
                switch (i)
                {
                    case 0: point = new Vector2(0f, 0.5f); break;
                    case 1: point = new Vector2(-0.45f, 0.3f); break;
                    case 2: point = new Vector2(-0.38f, -0.12f); break;
                    case 3: point = new Vector2(0f, -0.5f); break;
                    case 4: point = new Vector2(0.38f, -0.12f); break;
                    default: point = new Vector2(0.45f, 0.3f); break;
                }
            }
            vh.AddVert(center + Vector2.Scale(point, rect.size), color, Vector2.zero);
        }
        for (int i = 0; i < count; i++)
            vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
    }
}
