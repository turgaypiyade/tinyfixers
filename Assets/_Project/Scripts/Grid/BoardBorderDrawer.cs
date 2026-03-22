using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardBorderDrawer : MonoBehaviour
{
    [Header("Input")]
    public LevelData level;

    [Header("Layout")]
    public int tileSize = 110;

    [Header("Border Visual")]
    public RectTransform borderRoot;          // Mask DIŞINDA olmalı (BoardRoot altında önerilir)
    public GameObject borderSegmentPrefab;    // UI Image prefab
    public int thickness = 5;

    [Header("Border Placement")]
    [Tooltip("BoardContent anchoredPosition ile aynı ver (örn: (8,-8))")]
    public Vector2 contentOffset = new Vector2(8f, -8f);

    [Tooltip("Çizgiyi biraz dışarı alır; kalın çizgide taşın üstüne binmez")]
    public int borderOutside = 18;

    [Header("Optional: Treat obstacles as solid")]
    public bool includeObstaclesAsSolid = true;

    private int width;
    private int height;

    private bool[,] topEdge;
    private bool[,] bottomEdge;
    private bool[,] leftEdge;
    private bool[,] rightEdge;


    public void SetLevelData(LevelData value)
    {
        level = value;
    }

    public void Draw(bool[] blocked = null)
    {
        if (level == null)
        {
            Debug.LogError("BoardBorderDrawer: LevelData NULL");
            return;
        }
        if (borderRoot == null)
        {
            Debug.LogError("BoardBorderDrawer: borderRoot NULL (Mask dışına bir RectTransform ver)");
            return;
        }
        if (borderSegmentPrefab == null)
        {
            Debug.LogError("BoardBorderDrawer: borderSegmentPrefab NULL");
            return;
        }

        width = level.width;
        height = level.height;

        ClearChildren(borderRoot);

        topEdge    = new bool[width, height];
        bottomEdge = new bool[width, height];
        leftEdge   = new bool[width, height];
        rightEdge  = new bool[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!IsSolidCell(x, y, blocked)) continue;

            if (!IsSolidCell(x, y - 1, blocked)) topEdge[x, y] = true;
            if (!IsSolidCell(x, y + 1, blocked)) bottomEdge[x, y] = true;
            if (!IsSolidCell(x - 1, y, blocked)) leftEdge[x, y] = true;
            if (!IsSolidCell(x + 1, y, blocked)) rightEdge[x, y] = true;
        }

        DrawHorizontalMerged(topEdge, BorderDir.Top);
        DrawHorizontalMerged(bottomEdge, BorderDir.Bottom);

        DrawVerticalMerged(leftEdge, BorderDir.Left);
        DrawVerticalMerged(rightEdge, BorderDir.Right);
    }

    private bool IsSolidCell(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;

        int idx = level.Index(x, y);

        bool isBlockedByObstacle = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];

        // BlocksCells obstacle'ları Empty işaretlense bile, includeObstaclesAsSolid=true
        // olduğunda bunları solid kabul edip obstacle çevresinde border çizimini engelle.
        if (includeObstaclesAsSolid && isBlockedByObstacle)
            return true;

        if (level.cells != null && idx >= 0 && idx < level.cells.Length)
        {
            if (level.cells[idx] == (int)CellType.Empty)
                return false;
        }

        if (!includeObstaclesAsSolid && blocked != null && idx >= 0 && idx < blocked.Length)
        {
            if (blocked[idx]) return false;
        }

        return true;
    }

    private void DrawHorizontalMerged(bool[,] edge, BorderDir dir)
    {
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                if (!edge[x, y]) { x++; continue; }

                int start = x;
                int end = x;
                while (end + 1 < width && edge[end + 1, y]) end++;

                SpawnMergedHorizontal(start, end, y, dir);

                x = end + 1;
            }
        }
    }

    private void DrawVerticalMerged(bool[,] edge, BorderDir dir)
    {
        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                if (!edge[x, y]) { y++; continue; }

                int start = y;
                int end = y;
                while (end + 1 < height && edge[x, end + 1]) end++;

                SpawnMergedVertical(x, start, end, dir);

                y = end + 1;
            }
        }
    }

    private void SpawnMergedHorizontal(int startX, int endX, int y, BorderDir dir)
    {
        var go = Instantiate(borderSegmentPrefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        SetupRT(rt);

        float px = startX * tileSize + contentOffset.x;
        float py = -y * tileSize + contentOffset.y;

        int cells = (endX - startX + 1);
        float w = cells * tileSize;

        // Kalınlık artınca otomatik biraz daha dışarı çıksın
        float outside = borderOutside + (thickness * 0.5f);

        if (dir == BorderDir.Top)
        {
            // Top dışarı = yukarı (+Y)
            rt.anchoredPosition = new Vector2(px, py + outside);
            rt.sizeDelta = new Vector2(w, thickness);
        }
        else // Bottom
        {
            // Bottom dışarı = aşağı (-Y)
            rt.anchoredPosition = new Vector2(px, py - tileSize - outside);
            rt.sizeDelta = new Vector2(w, thickness);
        }

        MakeNonRaycast(go);
    }

    private void SpawnMergedVertical(int x, int startY, int endY, BorderDir dir)
    {
        var go = Instantiate(borderSegmentPrefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        SetupRT(rt);

        float px = x * tileSize + contentOffset.x;
        float py = -startY * tileSize + contentOffset.y;

        int cells = (endY - startY + 1);
        float h = cells * tileSize;

        float outside = borderOutside + (thickness * 0.5f);

        if (dir == BorderDir.Left)
        {
            rt.anchoredPosition = new Vector2(px - outside, py);
            rt.sizeDelta = new Vector2(thickness, h);
        }
        else // Right
        {
            rt.anchoredPosition = new Vector2(px + tileSize + outside, py);
            rt.sizeDelta = new Vector2(thickness, h);
        }

        MakeNonRaycast(go);
    }

    private void SetupRT(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void MakeNonRaycast(GameObject go)
    {
        var img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    private void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}

public enum BorderDir { Top, Bottom, Left, Right }
