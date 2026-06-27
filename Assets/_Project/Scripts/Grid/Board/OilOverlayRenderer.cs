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

    private RectTransform root;
    private readonly Dictionary<Vector2Int, Image> views = new();

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

        if (oilCells != null)
        {
            for (int i = 0; i < oilCells.Count; i++)
            {
                var img = GetOrCreateView(oilCells[i]);
                if (img == null) continue;

                PlaceView(img.rectTransform, oilCells[i], size);
                img.gameObject.SetActive(true);
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

    private static void PlaceView(RectTransform rt, Vector2Int cell, int tileSize)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(cell.x * tileSize, -cell.y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.localScale = Vector3.one;
    }
}
