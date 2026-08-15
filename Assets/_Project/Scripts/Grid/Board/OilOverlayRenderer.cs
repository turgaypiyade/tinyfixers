using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cell-anchored oil overlay rendering.
///
/// Oil DATA is cell-based (level.obstacles[idx] == Oil). This renderer draws a persistent
/// oil sprite at each oil CELL coordinate, in its own layer above the tiles — independent
/// of whether a TileView exists at that cell.
///
/// Replaces the old tile-bound overlay (TileView._cellOverlayImage / SetCoveredByCellOverlay),
/// which broke whenever an oil cell had no tile (spawn-isolated columns, mid-cascade, spread
/// onto transient cells): the visual vanished while the oil data still blocked the cell,
/// producing invisible-oil, flicker, and "mysterious empty gaps that don't fill".
///
/// Mirrors the MudOverlayService pattern (mud is also cell-anchored). No scene wiring: the
/// overlay root is created programmatically as a sibling above TilesRoot, and the sprite is
/// taken from the existing OilSpreadAnimator.
/// </summary>
public sealed class OilOverlayRenderer
{
    private readonly BoardController board;
    private readonly Sprite oilSprite;
    private const float InternalJoinOverlapPixels = 8f;

    private RectTransform root;
    private readonly Dictionary<Vector2Int, Image> views = new();
    private readonly Dictionary<Vector3Int, Image> joins = new();
    private Sprite solidSprite;
    private Color joinColor;
    private bool joinColorResolved;

    public OilOverlayRenderer(BoardController board, Sprite oilSprite)
    {
        this.board = board;
        this.oilSprite = oilSprite;
    }

    /// <summary>Shows oil overlays for exactly the given oil cells, hides all others.</summary>
    public void Refresh(IReadOnlyList<Vector2Int> oilCells)
    {
        int size = board != null ? board.TileSize : 0;
        if (size <= 0)
            return;

        EnsureRoot();
        if (root == null) return;

        // Hide everything first; re-show only current oil cells below.
        foreach (var kv in views)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);
        foreach (var kv in joins)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);

        if (oilCells != null)
        {
            var oilSet = new HashSet<Vector2Int>(oilCells);
            for (int i = 0; i < oilCells.Count; i++)
            {
                var cell = oilCells[i];
                var img = GetOrCreateView(cell);
                if (img == null) continue;

                PlaceView(img.rectTransform, cell, size, oilSet);
                img.gameObject.SetActive(true);

                var right = cell + Vector2Int.right;
                if (oilSet.Contains(right))
                    ShowJoin(cell, horizontal: true, size);

                var below = new Vector2Int(cell.x, cell.y + 1);
                if (oilSet.Contains(below))
                    ShowJoin(cell, horizontal: false, size);
            }
        }

        // Stay above tiles even after RefreshAllSortingOrders reorders the tile layer.
        root.SetAsLastSibling();
    }

    /// <summary>Hides a single oil cell's overlay (e.g. right when that oil is destroyed).</summary>
    public void Hide(Vector2Int cell)
    {
        if (views.TryGetValue(cell, out var img) && img != null)
            img.gameObject.SetActive(false);
    }

    private void EnsureRoot()
    {
        var tilesRoot = board != null ? board.TilesRoot : null;
        if (tilesRoot == null) return;

        var parent = tilesRoot.parent as RectTransform;
        if (parent == null) return;

        if (root == null)
        {
            var go = new GameObject("OilOverlay", typeof(RectTransform));
            root = go.GetComponent<RectTransform>();
            root.SetParent(parent, false);
        }
        else if (root.parent != parent)
        {
            root.SetParent(parent, false);
        }

        // Mirror TilesRoot's transform so the per-cell formula below maps to the same
        // screen positions as the tiles themselves.
        root.anchorMin = tilesRoot.anchorMin;
        root.anchorMax = tilesRoot.anchorMax;
        root.pivot = tilesRoot.pivot;
        root.anchoredPosition = tilesRoot.anchoredPosition;
        root.sizeDelta = tilesRoot.sizeDelta;
        root.localScale = tilesRoot.localScale;

        root.SetAsLastSibling();
    }

    private Image GetOrCreateView(Vector2Int cell)
    {
        if (views.TryGetValue(cell, out var existing) && existing != null)
            return existing;

        var go = new GameObject($"Oil_{cell.x}_{cell.y}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        var img = go.GetComponent<Image>();
        img.sprite = oilSprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
        // Sprite atanmamışsa görünür bir fallback renk (yine de "boş gap" yerine oil belli olsun).
        img.color = oilSprite != null ? Color.white : new Color(0.62f, 0.30f, 0.05f, 0.65f);

        views[cell] = img;
        return img;
    }

    private void ShowJoin(Vector2Int cell, bool horizontal, int tileSize)
    {
        var img = GetOrCreateJoin(cell, horizontal);
        if (img == null) return;

        float overlap = Mathf.Max(1f, InternalJoinOverlapPixels);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.localScale = Vector3.one;

        if (horizontal)
        {
            rt.anchoredPosition = new Vector2((cell.x + 1) * tileSize - overlap, -cell.y * tileSize);
            rt.sizeDelta = new Vector2(overlap * 2f, tileSize);
        }
        else
        {
            rt.anchoredPosition = new Vector2(cell.x * tileSize, -(cell.y + 1) * tileSize + overlap);
            rt.sizeDelta = new Vector2(tileSize, overlap * 2f);
        }

        img.gameObject.SetActive(true);
        img.transform.SetAsFirstSibling();
    }

    private Image GetOrCreateJoin(Vector2Int cell, bool horizontal)
    {
        var key = new Vector3Int(cell.x, cell.y, horizontal ? 0 : 1);
        if (joins.TryGetValue(key, out var existing) && existing != null)
            return existing;

        var go = new GameObject($"OilJoin_{cell.x}_{cell.y}_{(horizontal ? "H" : "V")}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        var img = go.GetComponent<Image>();
        img.sprite = GetSolidSprite();
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.color = ResolveJoinColor();

        joins[key] = img;
        return img;
    }

    private static void PlaceView(RectTransform rt, Vector2Int cell, int tileSize, HashSet<Vector2Int> oilSet)
    {
        if (rt == null) return;

        float bleedLeft = oilSet.Contains(cell + Vector2Int.left) ? InternalJoinOverlapPixels : 0f;
        float bleedRight = oilSet.Contains(cell + Vector2Int.right) ? InternalJoinOverlapPixels : 0f;
        float bleedTop = oilSet.Contains(new Vector2Int(cell.x, cell.y - 1)) ? InternalJoinOverlapPixels : 0f;
        float bleedBottom = oilSet.Contains(new Vector2Int(cell.x, cell.y + 1)) ? InternalJoinOverlapPixels : 0f;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(cell.x * tileSize - bleedLeft, -cell.y * tileSize + bleedTop);
        rt.sizeDelta = new Vector2(tileSize + bleedLeft + bleedRight, tileSize + bleedTop + bleedBottom);
        rt.localScale = Vector3.one;
    }

    private Sprite GetSolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        var tex = Texture2D.whiteTexture;
        solidSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return solidSprite;
    }

    private Color ResolveJoinColor()
    {
        if (joinColorResolved)
            return joinColor;

        joinColor = new Color(0.62f, 0.30f, 0.05f, 0.65f);
        if (oilSprite != null && oilSprite.texture != null)
        {
            try
            {
                var rect = oilSprite.textureRect;
                int px = Mathf.Clamp(Mathf.RoundToInt(rect.x + rect.width * 0.5f), 0, oilSprite.texture.width - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.5f), 0, oilSprite.texture.height - 1);
                var sampled = oilSprite.texture.GetPixel(px, py);
                if (sampled.a > 0.05f)
                    joinColor = sampled;
            }
            catch (UnityException)
            {
                joinColor = new Color(0.62f, 0.30f, 0.05f, 0.65f);
            }
        }

        joinColorResolved = true;
        return joinColor;
    }
}
