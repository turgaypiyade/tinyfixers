using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TEMİZ tile-set (autotile) oil overlay. MudCellView'in board-geneli UV kesme yaklaşımının aksine,
/// her oil hücresine komşu desenine göre küçük PNG katmanları overlay eder; sadece JOverlaySon'dan
/// dış kenar strip'i alınır. Parçalar:
///   • fill        : seamless interior (her hücrede tekrar eden OilInteriorTexture)
///   • fullOverlay : tek hücre / edge strip kaynağı (JOverlaySon)
///   • corner      : JLT/JRT/JLB/JRB, 1 hücrelik canvas içinde yarım-quadrant jel köşesi.
///                   Sadece 3-oil + 1 boş iç köşede döndürülmeden çizilir.
/// Eksik parça (null) = o katman çizilmez → kısmi set'le bile bozulmadan görünür.
/// </summary>
public sealed class OilTileSetRenderer
{
    private readonly BoardController board;
    private readonly Texture interiorTexture;

    private RectTransform root;
    private readonly Dictionary<Vector3Int, RawImage> pool = new();

    private Sprite edgeSprite;         // Legacy: ÜST kenar olarak çizilir
    private Sprite fullOverlaySprite;  // JOverlaySon: full isolated tile + edge-strip source
    private Sprite cornerBR, cornerBL, cornerTR, cornerTL;
    private float cornerCanvasScale = 1f;
    private Vector2 offBR, offBL, offTR, offTL;

    // Piece id'leri (per-cell). Inner corner'lar vertex-keyed (100+quadrant).
    private const int IdFill = 0, IdFullOverlay = 1;
    private const int IdEdgeTop = 10, IdEdgeRight = 11, IdEdgeBottom = 12, IdEdgeLeft = 13;
    private const float OverlaySourceUnits = 17f;
    private const float OverlayEdgeCropUnits = 2f;
    private const float OverlayEdgeOverlapUnits = 1f;

    public OilTileSetRenderer(BoardController board, Texture interiorTexture)
    {
        this.board = board;
        this.interiorTexture = interiorTexture;
    }

    public void SetPieces(Sprite edge, Sprite outerCorner,
        Sprite iBR, Sprite iBL, Sprite iTR, Sprite iTL,
        float cornerScale, Vector2 oBR, Vector2 oBL, Vector2 oTR, Vector2 oTL)
    {
        edgeSprite = edge; fullOverlaySprite = outerCorner;
        cornerBR = iBR; cornerBL = iBL; cornerTR = iTR; cornerTL = iTL;
        cornerCanvasScale = cornerScale > 0f ? cornerScale : 1f;
        offBR = oBR; offBL = oBL; offTR = oTR; offTL = oTL;
    }

    public void Refresh(IReadOnlyList<Vector2Int> oilCells)
    {
        int ts = board != null ? board.TileSize : 0;
        int W = board != null ? board.Width : 0;
        int H = board != null ? board.Height : 0;
        if (ts <= 0 || W <= 0 || H <= 0)
            return;

        EnsureRoot();
        if (root == null)
            return;

        foreach (var kv in pool)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);

        if (oilCells == null || oilCells.Count == 0)
            return;

        var set = new HashSet<Vector2Int>(oilCells);

        // 1) Per-cell: fill + exposed kenarlar. Dış convex köşe ayrıca basılmaz;
        // JOverlaySon'dan alınan kenarların kendi köşe görseli yeterli.
        for (int i = 0; i < oilCells.Count; i++)
        {
            var c = oilCells[i];
            bool up    = set.Contains(new Vector2Int(c.x,     c.y - 1));
            bool down  = set.Contains(new Vector2Int(c.x,     c.y + 1));
            bool left  = set.Contains(new Vector2Int(c.x - 1, c.y));
            bool right = set.Contains(new Vector2Int(c.x + 1, c.y));
            bool upLeft    = set.Contains(new Vector2Int(c.x - 1, c.y - 1));
            bool upRight   = set.Contains(new Vector2Int(c.x + 1, c.y - 1));
            bool downLeft  = set.Contains(new Vector2Int(c.x - 1, c.y + 1));
            bool downRight = set.Contains(new Vector2Int(c.x + 1, c.y + 1));

            PlaceFill(c, ts, up, down, left, right);

            bool isolated = !up && !down && !left && !right;
            if (isolated && fullOverlaySprite != null)
            {
                PlaceFullCell(c, IdFullOverlay, ts, fullOverlaySprite);
                continue;
            }

            // Kenarlar: yeni JOverlaySon varsa strip crop; yoksa legacy edge sprite döndürülür.
            bool trimTopStart = left && upLeft;
            bool trimTopEnd = right && upRight;
            bool trimRightStart = up && upRight;
            bool trimRightEnd = down && downRight;
            bool trimBottomStart = left && downLeft;
            bool trimBottomEnd = right && downRight;
            bool trimLeftStart = up && upLeft;
            bool trimLeftEnd = down && downLeft;

            if (!up)
                PlaceEdge(c, IdEdgeTop, ts, EdgeSide.Top,
                    trimTopStart, trimTopEnd, left && !trimTopStart, right && !trimTopEnd);
            if (!right)
                PlaceEdge(c, IdEdgeRight, ts, EdgeSide.Right,
                    trimRightStart, trimRightEnd, up && !trimRightStart, down && !trimRightEnd);
            if (!down)
                PlaceEdge(c, IdEdgeBottom, ts, EdgeSide.Bottom,
                    trimBottomStart, trimBottomEnd, left && !trimBottomStart, right && !trimBottomEnd);
            if (!left)
                PlaceEdge(c, IdEdgeLeft, ts, EdgeSide.Left,
                    trimLeftStart, trimLeftEnd, up && !trimLeftStart, down && !trimLeftEnd);
        }

        // 2) İç köşeler (junction: 3 oil + 1 boş vertex).
        PlaceInnerCorners(set, ts);

        root.SetAsLastSibling();
    }

    // ── Placement ────────────────────────────────────────────────────────────
    private void PlaceFill(Vector2Int cell, int ts, bool up, bool down, bool left, bool right)
    {
        if (interiorTexture == null)
            return;
        float edgeInset = ts * (OverlayEdgeCropUnits / OverlaySourceUnits);
        float insetLeft = left ? 0f : edgeInset;
        float insetRight = right ? 0f : edgeInset;
        float insetTop = up ? 0f : edgeInset;
        float insetBottom = down ? 0f : edgeInset;

        var img = GetOrCreate(new Vector3Int(cell.x, cell.y, IdFill), "OilFill");
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0f, 1f);
        rt.localEulerAngles = Vector3.zero;
        rt.anchoredPosition = new Vector2(cell.x * ts + insetLeft, -(cell.y * ts + insetTop));
        rt.sizeDelta = new Vector2(
            Mathf.Max(0f, ts - insetLeft - insetRight),
            Mathf.Max(0f, ts - insetTop - insetBottom));
        img.texture = interiorTexture;
        img.uvRect = new Rect(0f, 0f, 1f, 1f);
        img.color = Color.white;
        img.gameObject.SetActive(true);
    }

    private enum EdgeSide { Top, Right, Bottom, Left }

    private void PlaceEdge(
        Vector2Int cell,
        int id,
        int ts,
        EdgeSide side,
        bool trimStart,
        bool trimEnd,
        bool extendStart,
        bool extendEnd)
    {
        if (fullOverlaySprite != null && fullOverlaySprite.texture != null)
        {
            PlaceOverlayEdgeStrip(cell, id, ts, side, fullOverlaySprite, trimStart, trimEnd, extendStart, extendEnd);
            return;
        }

        float rotZ = side switch
        {
            EdgeSide.Top => 0f,
            EdgeSide.Right => -90f,
            EdgeSide.Bottom => 180f,
            _ => 90f,
        };
        PlaceRotated(cell, id, ts, rotZ, edgeSprite);
    }

    private void PlaceOverlayEdgeStrip(
        Vector2Int cell,
        int id,
        int ts,
        EdgeSide side,
        Sprite sprite,
        bool trimStart,
        bool trimEnd,
        bool extendStart,
        bool extendEnd)
    {
        if (sprite == null || sprite.texture == null)
            return;

        var img = GetOrCreate(new Vector3Int(cell.x, cell.y, id), "OilEdge");
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localEulerAngles = Vector3.zero;

        float strip = OverlayEdgeCropUnits / OverlaySourceUnits;
        float inset = OverlayEdgeCropUnits / OverlaySourceUnits;
        float overlap = OverlayEdgeOverlapUnits / OverlaySourceUnits;
        float startInset = trimStart ? inset : 0f;
        float endInset = trimEnd ? inset : 0f;
        float startExtend = extendStart ? overlap : 0f;
        float endExtend = extendEnd ? overlap : 0f;
        float displayStart = startInset - startExtend;
        float centerLength = Mathf.Max(0f, 1f - startInset - endInset + startExtend + endExtend);
        switch (side)
        {
            case EdgeSide.Top:
                rt.anchoredPosition = new Vector2(
                    cell.x * ts + ts * (displayStart + centerLength * 0.5f),
                    -(cell.y * ts + ts * strip * 0.5f));
                rt.sizeDelta = new Vector2(ts * centerLength, ts * strip);
                break;
            case EdgeSide.Right:
                rt.anchoredPosition = new Vector2(
                    cell.x * ts + ts * (1f - strip * 0.5f),
                    -(cell.y * ts + ts * (displayStart + centerLength * 0.5f)));
                rt.sizeDelta = new Vector2(ts * strip, ts * centerLength);
                break;
            case EdgeSide.Bottom:
                rt.anchoredPosition = new Vector2(
                    cell.x * ts + ts * (displayStart + centerLength * 0.5f),
                    -(cell.y * ts + ts * (1f - strip * 0.5f)));
                rt.sizeDelta = new Vector2(ts * centerLength, ts * strip);
                break;
            default:
                rt.anchoredPosition = new Vector2(
                    cell.x * ts + ts * strip * 0.5f,
                    -(cell.y * ts + ts * (displayStart + centerLength * 0.5f)));
                rt.sizeDelta = new Vector2(ts * strip, ts * centerLength);
                break;
        }

        img.texture = sprite.texture;
        img.uvRect = SpriteEdgeUV(sprite, side, strip, startInset, endInset);
        img.color = Color.white;
        img.gameObject.SetActive(true);
    }

    private void PlaceFullCell(Vector2Int cell, int id, int ts, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        var img = GetOrCreate(new Vector3Int(cell.x, cell.y, id), "OilCorner");
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cell.x * ts + ts * 0.5f, -(cell.y * ts + ts * 0.5f));
        rt.sizeDelta = new Vector2(ts, ts);
        rt.localEulerAngles = Vector3.zero;
        img.texture = sprite.texture;
        img.uvRect = SpriteUV(sprite);
        img.color = Color.white;
        img.gameObject.SetActive(true);
    }

    // Legacy tam-boy parça, hücre merkezine, rotZ ile döndürülmüş.
    private void PlaceRotated(Vector2Int cell, int id, int ts, float rotZ, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;
        var img = GetOrCreate(new Vector3Int(cell.x, cell.y, id), "OilPiece");
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cell.x * ts + ts * 0.5f, -(cell.y * ts + ts * 0.5f));
        rt.sizeDelta = new Vector2(ts, ts);
        rt.localEulerAngles = new Vector3(0f, 0f, rotZ);
        img.texture = sprite.texture;
        img.uvRect = SpriteUV(sprite);
        img.color = Color.white;
        img.gameObject.SetActive(true);
    }

    private void PlaceInnerCorners(HashSet<Vector2Int> set, int ts)
    {
        for (int cy = 1; cy < board.Height; cy++)
        for (int cx = 1; cx < board.Width; cx++)
        {
            bool nw = set.Contains(new Vector2Int(cx - 1, cy - 1));
            bool ne = set.Contains(new Vector2Int(cx,     cy - 1));
            bool sw = set.Contains(new Vector2Int(cx - 1, cy));
            bool se = set.Contains(new Vector2Int(cx,     cy));
            int count = (nw ? 1 : 0) + (ne ? 1 : 0) + (sw ? 1 : 0) + (se ? 1 : 0);
            if (count != 3)
                continue;

            int q = !nw ? 0 : !ne ? 1 : !sw ? 2 : 3;
            Vector2Int emptyCell;
            Sprite sprite; Vector2 off;
            switch (q)
            {
                case 0:
                    emptyCell = new Vector2Int(cx - 1, cy - 1);
                    sprite = cornerBR;
                    off = offBR + CornerNudge(ts, 1f, 1f); // NW boş → RB, sağ+aşağı taşır
                    break;
                case 1:
                    emptyCell = new Vector2Int(cx, cy - 1);
                    sprite = cornerBL;
                    off = offBL + CornerNudge(ts, -1f, 1f); // NE boş → LB, sol+aşağı taşır
                    break;
                case 2:
                    emptyCell = new Vector2Int(cx - 1, cy);
                    sprite = cornerTR;
                    off = offTR + CornerNudge(ts, 1f, -1f); // SW boş → TR, sağ+yukarı taşır
                    break;
                default:
                    emptyCell = new Vector2Int(cx, cy);
                    sprite = cornerTL;
                    off = offTL + CornerNudge(ts, -1f, -1f); // SE boş → TL, sol+yukarı taşır
                    break;
            }
            if (sprite == null || sprite.texture == null)
                continue;

            var img = GetOrCreate(new Vector3Int(cx, cy, 100 + q), "OilInner");
            float size = ts * (cornerCanvasScale > 0f ? cornerCanvasScale : 1f);
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localEulerAngles = Vector3.zero;
            rt.anchoredPosition = new Vector2(
                emptyCell.x * ts + ts * 0.5f + off.x,
                -(emptyCell.y * ts + ts * 0.5f) - off.y);
            rt.sizeDelta = new Vector2(size, size);
            img.texture = sprite.texture;
            img.uvRect = SpriteUV(sprite);
            img.color = Color.white;
            img.gameObject.SetActive(true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private void EnsureRoot()
    {
        var tilesRoot = board != null ? board.TilesRoot : null;
        if (tilesRoot == null) return;
        var parent = tilesRoot.parent as RectTransform;
        if (parent == null) return;

        if (root == null)
        {
            var go = new GameObject("OilTileSet", typeof(RectTransform));
            root = go.GetComponent<RectTransform>();
            root.SetParent(parent, false);
        }
        else if (root.parent != parent)
        {
            root.SetParent(parent, false);
        }

        root.anchorMin = tilesRoot.anchorMin;
        root.anchorMax = tilesRoot.anchorMax;
        root.pivot = tilesRoot.pivot;
        root.anchoredPosition = tilesRoot.anchoredPosition;
        root.sizeDelta = tilesRoot.sizeDelta;
        root.localScale = tilesRoot.localScale;
        root.gameObject.layer = board.gameObject.layer;
    }

    private RawImage GetOrCreate(Vector3Int key, string name)
    {
        if (pool.TryGetValue(key, out var existing) && existing != null)
            return existing;
        var go = new GameObject($"{name}_{key.x}_{key.y}_{key.z}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = root.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        img.color = Color.white;
        pool[key] = img;
        return img;
    }

    private static Rect SpriteUV(Sprite s)
    {
        if (s == null || s.texture == null) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect;
        return new Rect(tr.x / s.texture.width, tr.y / s.texture.height,
                        tr.width / s.texture.width, tr.height / s.texture.height);
    }

    private static Vector2 CornerNudge(int ts, float xDir, float yDir)
    {
        float px = ts * (OverlayEdgeCropUnits / OverlaySourceUnits);
        return new Vector2(px * xDir, px * yDir);
    }

    private static Rect SpriteEdgeUV(Sprite s, EdgeSide side, float strip, float startInset, float endInset)
    {
        var uv = SpriteUV(s);
        strip = Mathf.Clamp01(strip);
        startInset = Mathf.Clamp(startInset, 0f, 0.49f);
        endInset = Mathf.Clamp(endInset, 0f, 0.49f);
        float innerW = Mathf.Max(0f, uv.width * (1f - startInset - endInset));
        float innerH = Mathf.Max(0f, uv.height * (1f - startInset - endInset));
        return side switch
        {
            EdgeSide.Top => new Rect(
                uv.x + uv.width * startInset,
                uv.y + uv.height * (1f - strip),
                innerW,
                uv.height * strip),
            EdgeSide.Right => new Rect(
                uv.x + uv.width * (1f - strip),
                uv.y + uv.height * endInset,
                uv.width * strip,
                innerH),
            EdgeSide.Bottom => new Rect(uv.x + uv.width * startInset, uv.y, innerW, uv.height * strip),
            _ => new Rect(uv.x, uv.y + uv.height * endInset, uv.width * strip, innerH),
        };
    }

    public void SetActive(bool active)
    {
        if (root != null) root.gameObject.SetActive(active);
    }

    public void Destroy()
    {
        if (root != null) { Object.Destroy(root.gameObject); root = null; pool.Clear(); }
    }
}
