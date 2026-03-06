using UnityEngine;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs")]
    public GameObject straightHPrefab;  // board_straight_h
    public GameObject straightVPrefab;  // board_straight_v
    public GameObject cornerLTPrefab;   // board_corner_lt
    public GameObject cornerRTPrefab;   // board_corner_rt
    public GameObject cornerLBPrefab;   // board_corner_lb
    public GameObject cornerRBPrefab;   // board_corner_rb

    [Header("Debug")]
    public bool debugMasks = false;
    public bool debugBorderLogs = false;
    public Font debugFont;

    [Header("Layout")]
    public int tileSize = 110;
    public Vector2 contentOffset = Vector2.zero;

    [Header("Border Settings")]
    [Tooltip("Art setinin tasarlandigi tile boyu. Ornek: tile 64 iken corner=64, h=32, v=32.")]
    public float borderReferenceTileSize = 64f;
    public bool autoScaleWithTileSize = true;
    public float cornerSize = 64f;
    public float straightH_height = 32f;
    public float straightV_width = 32f;
    public float borderOutside = 0f;
    [Header("Corner Tuning")]
    public float cornerInset = 0f;
    public float innerCornerInset = 0.5f;
    
    [Header("Render Mode")]
    [Tooltip("Ince ve temiz cerceve icin corner trim sistemini kapatir.")]
    public bool useThinLineMode = true;
    [Tooltip("Kose sprite'larini ciz. Ince cizgi modunda kapali tutmak daha guvenli.")]
    public bool drawCorners = false;

    [Header("Obstacle")]
    public bool includeObstaclesAsSolid = true;

    private bool[] _holes;

    public void SetLevelData(LevelData value) => level = value;

    public void Draw(bool[] blocked = null, bool[] holes = null)
    {
        if (level == null || borderRoot == null) return;

        EnsureScaledMetrics();
        _holes = holes;
        ClearChildren();

        int W = level.width;
        int H = level.height;

        for (int nodeY = 0; nodeY <= H; nodeY++)
        for (int x = 0; x < W; x++)
        {
            bool above = IsSolid(x, nodeY - 1, blocked);
            bool below = IsSolid(x, nodeY, blocked);
            if (above == below) continue;
            PlaceStraightH(x, nodeY, solidIsBelow: below, blocked);
        }

        for (int y = 0; y < H; y++)
        for (int nodeX = 0; nodeX <= W; nodeX++)
        {
            bool leftCell = IsSolid(nodeX - 1, y, blocked);
            bool rightCell = IsSolid(nodeX, y, blocked);
            if (leftCell == rightCell) continue;
            PlaceStraightV(nodeX, y, solidIsRight: rightCell, blocked);
        }

        if (drawCorners )
        {
            for (int ny = 0; ny <= H; ny++)
            for (int nx = 0; nx <= W; nx++)
                PlaceCorner(nx, ny, blocked);
        }
    }

    private void PlaceStraightH(int cx, int nodeY, bool solidIsBelow, bool[] blocked)
    {
        float trimL = 0f;
        float trimR = 0f;

        int maskL = GetNodeMask(cx, nodeY, blocked);
        int maskR = GetNodeMask(cx + 1, nodeY, blocked);

        bool innerL = IsInnerCornerMask(maskL);
        bool innerR = IsInnerCornerMask(maskR);

        // Sadece inner/hole corner'da trim uygula
        if (innerL) trimL = cornerSize * 0.5f;
        if (innerR) trimR = cornerSize * 0.5f;

        Vector2 nL = NodePos(cx, nodeY);
        float rawLen = tileSize - trimL - trimR;
        if (rawLen <= 0.01f) return;

        float len = rawLen;
        float centerX = nL.x + trimL + len * 0.5f;
        float halfH = straightH_height * 0.5f;
        float centerY = nL.y + (solidIsBelow ? 1f : -1f) * (borderOutside + halfH);

        Vector2 pos = new Vector2(centerX, centerY);
        Vector2 size = new Vector2(len, straightH_height);

        SpawnRect(straightHPrefab, pos, size);
        LogStraightH(cx, nodeY, solidIsBelow, pos, size, trimL, trimR);
    }

    private void PlaceStraightV(int nodeX, int cy, bool solidIsRight, bool[] blocked)
    {
        float trimT = 0f;
        float trimB = 0f;

        int maskT = GetNodeMask(nodeX, cy, blocked);
        int maskB = GetNodeMask(nodeX, cy + 1, blocked);

        bool innerT = IsInnerCornerMask(maskT);
        bool innerB = IsInnerCornerMask(maskB);

        // Sadece inner/hole corner'da trim uygula
        if (innerT) trimT = cornerSize * 0.5f;
        if (innerB) trimB = cornerSize * 0.5f;

        Vector2 nT = NodePos(nodeX, cy);
        float rawLen = tileSize - trimT - trimB;
        if (rawLen <= 0.01f) return;

        float len = rawLen;
        float centerY = nT.y - trimT - len * 0.5f;
        float halfW = straightV_width * 0.5f;
        float centerX = nT.x + (solidIsRight ? -1f : 1f) * (borderOutside + halfW);

        Vector2 pos = new Vector2(centerX, centerY);
        Vector2 size = new Vector2(straightV_width, len);

        SpawnRect(straightVPrefab, pos, size);
        LogStraightV(nodeX, cy, solidIsRight, pos, size, trimT, trimB);
    }

    private void PlaceCorner(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx,     ny - 1, blocked);

        bool br = IsSolid(nx,     ny,     blocked);
        bool bl = IsSolid(nx - 1, ny,     blocked);

        int mask = (tl ? 1 : 0) | (tr ? 2 : 0) | (br ? 4 : 0) | (bl ? 8 : 0);
        if (mask == 0 || mask == 15) return;

        Vector2 node = NodePos(nx, ny);
        Vector2 sz = new Vector2(cornerSize, cornerSize);

        if (debugMasks)
            SpawnMaskLabel(node, mask);

        switch (mask)
        {
            // outer corners
            case 4:  SpawnCorner("LT", cornerLTPrefab, node, sz, nx, ny, mask, new Vector2(1f, 0f)); break;
            case 8:  SpawnCorner("RT", cornerRTPrefab, node, sz, nx, ny, mask, new Vector2(0f, 0f)); break;
            case 2:  SpawnCorner("LB", cornerLBPrefab, node, sz, nx, ny, mask, new Vector2(1f, 1f)); break;
            case 1:  SpawnCorner("RB", cornerRBPrefab, node, sz, nx, ny, mask, new Vector2(0f, 1f)); break;

            // inner corners
            case 11: SpawnCorner("LT", cornerLTPrefab, node, sz, nx, ny, mask, new Vector2(0f, 1f)); break;
            case 7:  SpawnCorner("RT", cornerRTPrefab, node, sz, nx, ny, mask, new Vector2(1f, 1f)); break;
            case 13: SpawnCorner("LB", cornerLBPrefab, node, sz, nx, ny, mask, new Vector2(0f, 0f)); break;
            case 14: SpawnCorner("RB", cornerRBPrefab, node, sz, nx, ny, mask, new Vector2(1f, 0f)); break;

            // diagonal ambiguous
            case 5:
                SpawnCorner("RB", cornerRBPrefab, node, sz, nx, ny, mask, new Vector2(1f, 0f));
                SpawnCorner("LT", cornerLTPrefab, node, sz, nx, ny, mask, new Vector2(0f, 1f));
                break;

            case 10:
                SpawnCorner("LB", cornerLBPrefab, node, sz, nx, ny, mask, new Vector2(0f, 0f));
                SpawnCorner("RT", cornerRTPrefab, node, sz, nx, ny, mask, new Vector2(1f, 1f));
                break;
        }
    }

    private void SpawnCorner(
        string cornerName,
        GameObject prefab,
        Vector2 pos,
        Vector2 size,
        int nx,
        int ny,
        int mask,
        Vector2 pivot)
    {
        if (prefab == null || borderRoot == null) return;

        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();


        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        Vector2 insetOffset = Vector2.zero;

        bool isInnerCorner = (mask == 11 || mask == 7 || mask == 13 || mask == 14);

        Vector2 appliedOffset = insetOffset;
        if (isInnerCorner)
        {
            if (pivot == new Vector2(1f, 0f))      appliedOffset = new Vector2(+innerCornerInset, -innerCornerInset);
            else if (pivot == new Vector2(0f, 0f)) appliedOffset = new Vector2(-innerCornerInset, -innerCornerInset);
            else if (pivot == new Vector2(1f, 1f)) appliedOffset = new Vector2(+innerCornerInset, +innerCornerInset);
            else if (pivot == new Vector2(0f, 1f)) appliedOffset = new Vector2(-innerCornerInset, +innerCornerInset);
        }

        rt.anchoredPosition = pos + appliedOffset;

        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget = false;
            img.preserveAspect = false;
        }

        if (!debugBorderLogs) return;

        Debug.Log(
            $"[DynamicBoardBorder][Corner] node=({nx},{ny}) mask={mask} corner={cornerName} " +
            $"pivot={pivot} anchoredPosition={pos} size={size}");
    }

    private void LogStraightH(int cx, int nodeY, bool solidIsBelow, Vector2 pos, Vector2 size, float trimL, float trimR)
    {
        if (!debugBorderLogs) return;

        Debug.Log(
            $"[DynamicBoardBorder][StraightH] cellX={cx} nodeY={nodeY} solidIsBelow={solidIsBelow} " +
            $"anchorMin=(0.00,1.00) anchorMax=(0.00,1.00) pivot=(0.50,0.50) " +
            $"anchoredPosition={pos} size={size} thickness={straightH_height:F2} trimL={trimL:F2} trimR={trimR:F2} outside={borderOutside:F2}");
    }

    private void LogStraightV(int nodeX, int cy, bool solidIsRight, Vector2 pos, Vector2 size, float trimT, float trimB)
    {
        if (!debugBorderLogs) return;

        Debug.Log(
            $"[DynamicBoardBorder][StraightV] nodeX={nodeX} cellY={cy} solidIsRight={solidIsRight} " +
            $"anchorMin=(0.00,1.00) anchorMax=(0.00,1.00) pivot=(0.50,0.50) " +
            $"anchoredPosition={pos} size={size} thickness={straightV_width:F2} trimT={trimT:F2} trimB={trimB:F2} outside={borderOutside:F2}");
    }

    private void EnsureScaledMetrics()
    {
        if (!autoScaleWithTileSize) return;
        if (borderReferenceTileSize <= 0f) return;

        float scale = tileSize / borderReferenceTileSize;
        cornerSize = 64f * scale;
        straightH_height = 32f * scale;
        straightV_width = 32f * scale;
    }

    private bool NodeNeedsTrim(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx, ny - 1, blocked);
        bool br = IsSolid(nx, ny, blocked);
        bool bl = IsSolid(nx - 1, ny, blocked);

        bool hasVEdge = (tl != tr) || (bl != br);
        bool hasHEdge = (tl != bl) || (tr != br);

        return hasVEdge && hasHEdge;
    }

    private Vector2 NodePos(int x, int y)
    {
        float ox = -(level.width * tileSize) * 0.5f;
        float oy =  (level.height * tileSize) * 0.5f;

        return new Vector2(
            ox + x * tileSize + contentOffset.x,
            oy - y * tileSize + contentOffset.y
        );
    }

    private bool IsSolid(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;

        int idx = level.Index(x, y);

        bool isBlockedByObstacle = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];

        // Obstacle hücrelerini istersek solid kabul et
        if (includeObstaclesAsSolid && isBlockedByObstacle)
            return true;

        if (!includeObstaclesAsSolid && isBlockedByObstacle)
            return false;

        if (_holes != null && idx >= 0 && idx < _holes.Length && _holes[idx])
            return false;

        if (level.cells != null && idx >= 0 && idx < level.cells.Length &&
            level.cells[idx] == (int)CellType.Empty)
            return false;

        return true;
    }

    private void SpawnRect(GameObject prefab, Vector2 pos, Vector2 size)
    {
        if (prefab == null) return;

        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget = false;
            img.preserveAspect = false;
        }
    }
    private void SpawnMaskLabel(Vector2 pos, int mask)
    {
        var go = new GameObject("Mask_" + mask, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(borderRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(60, 30);

        var t = go.GetComponent<Text>();
        t.text = mask.ToString();
        t.font = debugFont != null ? debugFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18;
        t.color = Color.magenta;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }

    private int GetNodeMask(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx,     ny - 1, blocked);
        bool br = IsSolid(nx,     ny,     blocked);
        bool bl = IsSolid(nx - 1, ny,     blocked);

        return (tl ? 1 : 0) | (tr ? 2 : 0) | (br ? 4 : 0) | (bl ? 8 : 0);
    }

    private bool IsInnerCornerMask(int mask)
    {
        return mask == 7 || mask == 11 || mask == 13 || mask == 14;
    }

    private void ClearChildren()
    {
        if (borderRoot == null) return;
        for (int i = borderRoot.childCount - 1; i >= 0; i--)
        {
            var c = borderRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }
}
