using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Native UI geometry based on Vectors/TimingBar_*.svg. No rasterized meter, generated texture,
/// runtime SVG parser, package dependency or per-frame mesh rebuild. Only the indicator transform moves.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class GiftFestivalTimingGraphic : MaskableGraphic
{
    public enum Part { Frame, Zones, Indicator }
    [SerializeField] private Part part;
    private float red = 0.15f, green = 0.2f;
    private Rect area;
    private Vector2 reference;
    private static readonly Color32 WoodTop = new(229, 164, 90, 255), WoodBottom = new(91, 49, 30, 255);
    private static readonly Color32 GoldTop = new(255, 240, 169, 255), GoldBottom = new(183, 122, 45, 255);

    public void Configure(Part value, GiftFestivalConfig cfg)
    {
        part = value;
        red = cfg.SafeRedWidth;
        green = cfg.SafeGreenWidth;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        area = rectTransform.rect;
        if (area.width <= 0f || area.height <= 0f) return;
        reference = part == Part.Frame ? new Vector2(800, 128) : part == Part.Zones ? new Vector2(660, 52) : new Vector2(20, 89);
        if (part == Part.Frame) Frame(vh);
        else if (part == Part.Zones) Zones(vh);
        else Indicator(vh);
    }

    private Vector2 Point(float x, float y) => new(area.xMin + x * area.width / reference.x, area.yMax - y * area.height / reference.y);
    private Color Tint(Color c) => c * color;

    private void Quad(VertexHelper vh, float left, float right, float top, float bottom, Color a, Color b)
    {
        if (right <= left || bottom <= top) return;
        int i = vh.currentVertCount;
        vh.AddVert(Point(left, top), Tint(a), Vector2.zero);
        vh.AddVert(Point(right, top), Tint(a), Vector2.zero);
        vh.AddVert(Point(right, bottom), Tint(b), Vector2.zero);
        vh.AddVert(Point(left, bottom), Tint(b), Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }

    private static float Inset(float y, float height, float radius)
    {
        float d = y < radius ? radius - y : y > height - radius ? y - (height - radius) : 0f;
        return radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - d * d));
    }

    private void Rounded(VertexHelper vh, float x, float y, float width, float height, float radius, Color top, Color bottom)
    {
        const int steps = 32;
        radius = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);
        for (int n = 0; n < steps; n++)
        {
            float t0 = (float)n / steps, t1 = (float)(n + 1) / steps;
            float y0 = height * t0, y1 = height * t1;
            int i = vh.currentVertCount;
            float a = Inset(y0, height, radius), b = Inset(y1, height, radius);
            var c0 = Tint(Color.Lerp(top, bottom, t0)); var c1 = Tint(Color.Lerp(top, bottom, t1));
            vh.AddVert(Point(x + a, y + y0), c0, Vector2.zero);
            vh.AddVert(Point(x + width - a, y + y0), c0, Vector2.zero);
            vh.AddVert(Point(x + width - b, y + y1), c1, Vector2.zero);
            vh.AddVert(Point(x + b, y + y1), c1, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    private void Frame(VertexHelper vh)
    {
        Rounded(vh, 5, 9, 790, 114, 57, new Color(0.15f, 0.09f, 0.15f, 0.24f), new Color(0.15f, 0.09f, 0.15f, 0.24f));
        Rounded(vh, 1, 1, 798, 124, 62, new Color32(94, 57, 38, 255), new Color32(55, 29, 22, 255));
        Rounded(vh, 4.5f, 2.5f, 791, 117, 58.5f, GoldTop, GoldBottom);
        Rounded(vh, 9, 7, 782, 108, 54, WoodTop, WoodBottom);
        Rounded(vh, 36, 20, 728, 84, 42, new Color32(77, 41, 30, 255), new Color32(101, 59, 40, 255));
        Rounded(vh, 58, 27, 684, 70, 35, GoldTop, GoldBottom);
        Rounded(vh, 65, 34, 670, 56, 28, new Color32(16, 27, 46, 255), new Color32(16, 27, 46, 255));
        Rounded(vh, 67, 36, 666, 52, 26, new Color32(9, 27, 49, 255), new Color32(27, 53, 83, 255));
        Rounded(vh, 64, 11, 672, 3, 1.5f, new Color(1f, 0.84f, 0.55f, 0.6f), new Color(1f, 0.84f, 0.55f, 0.2f));
        Bolt(vh, 29); Bolt(vh, 771);
    }

    private void Bolt(VertexHelper vh, float x)
    {
        Rounded(vh, x - 10.5f, 50.5f, 21, 21, 10.5f, new Color32(87, 51, 33, 255), new Color32(87, 51, 33, 255));
        Rounded(vh, x - 7.5f, 51.5f, 15, 15, 7.5f, GoldTop, GoldBottom);
        Rounded(vh, x - 3, 55, 5, 1.4f, 0.7f, Color.white, GoldTop);
    }

    private void Zones(VertexHelper vh)
    {
        float greenStart = (1f - green) * 0.5f;
        Zone(vh, 0, red, new Color32(255, 159, 128, 255), new Color32(168, 42, 49, 255));
        Zone(vh, red, greenStart, new Color32(255, 238, 161, 255), new Color32(202, 130, 27, 255));
        Zone(vh, greenStart, 1f - greenStart, new Color32(184, 245, 161, 255), new Color32(24, 132, 81, 255));
        Zone(vh, 1f - greenStart, 1f - red, new Color32(255, 238, 161, 255), new Color32(202, 130, 27, 255));
        Zone(vh, 1f - red, 1, new Color32(255, 159, 128, 255), new Color32(168, 42, 49, 255));
        Divider(vh, red); Divider(vh, greenStart); Divider(vh, 1f - greenStart); Divider(vh, 1f - red);
        Quad(vh, 26, 634, 2.5f, 3.5f, new Color(1, 1, 1, 0.22f), new Color(1, 1, 1, 0.22f));
    }

    private void Zone(VertexHelper vh, float start, float end, Color top, Color bottom)
    {
        const int steps = 32;
        for (int n = 0; n < steps; n++)
        {
            float t0 = (float)n / steps, t1 = (float)(n + 1) / steps;
            float y0 = t0 * 52, y1 = t1 * 52;
            float a = Inset(y0, 52, 26), b = Inset(y1, 52, 26);
            float l0 = Mathf.Max(660 * start, a), r0 = Mathf.Min(660 * end, 660 - a);
            float l1 = Mathf.Max(660 * start, b), r1 = Mathf.Min(660 * end, 660 - b);
            if (r0 < l0 && r1 < l1) continue;
            int i = vh.currentVertCount;
            var c0 = Tint(Color.Lerp(top, bottom, t0)); var c1 = Tint(Color.Lerp(top, bottom, t1));
            vh.AddVert(Point(l0, y0), c0, Vector2.zero); vh.AddVert(Point(Mathf.Max(l0, r0), y0), c0, Vector2.zero);
            vh.AddVert(Point(Mathf.Max(l1, r1), y1), c1, Vector2.zero); vh.AddVert(Point(l1, y1), c1, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    private void Divider(VertexHelper vh, float t) => Quad(vh, t * 660 - 0.65f, t * 660 + 0.65f, 1, 51,
        new Color(0.2f, 0.17f, 0.2f, 0.55f), new Color(0.2f, 0.17f, 0.2f, 0.55f));

    private void Indicator(VertexHelper vh)
    {
        Rounded(vh, 3.4f, 16.5f, 16, 72, 8, new Color(0.04f, 0.09f, 0.15f, 0.28f), new Color(0.04f, 0.09f, 0.15f, 0.28f));
        Rounded(vh, 0.8f, 12.8f, 18.4f, 74.4f, 9.2f, new Color32(28, 44, 62, 255), new Color32(28, 44, 62, 255));
        Rounded(vh, 2, 14, 16, 72, 8, new Color32(255, 253, 240, 255), new Color32(207, 205, 198, 255));
        Quad(vh, 5, 7, 23, 76, Color.white, new Color(1, 1, 1, 0.5f));
        Triangle(vh, new Vector2(1.8f, 0.5f), new Vector2(18.2f, 0.5f), new Vector2(10, 13), new Color32(28, 44, 62, 255));
        Triangle(vh, new Vector2(3.4f, 2), new Vector2(16.6f, 2), new Vector2(10, 10.8f), new Color32(255, 248, 232, 255));
    }

    private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color shade)
    {
        int i = vh.currentVertCount;
        vh.AddVert(Point(a.x, a.y), Tint(shade), Vector2.zero);
        vh.AddVert(Point(b.x, b.y), Tint(shade), Vector2.zero);
        vh.AddVert(Point(c.x, c.y), Tint(shade), Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}
