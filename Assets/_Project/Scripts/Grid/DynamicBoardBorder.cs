using UnityEngine;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData     level;
    public RectTransform borderRoot;

    [Header("Prefabs")]
    public GameObject straightHPrefab;  // board_straight_h  (64w × 32h)
    public GameObject straightVPrefab;  // board_straight_v  (32w × 64h)
    public GameObject cornerLTPrefab;   // board_corner_lt   ┌
    public GameObject cornerRTPrefab;   // board_corner_rt   ┐
    public GameObject cornerLBPrefab;   // board_corner_lb   └
    public GameObject cornerRBPrefab;   // board_corner_rb   ┘

    [Header("Layout")]
    public int     tileSize      = 110;
    public Vector2 contentOffset = new Vector2(8f, -8f);

    [Header("Border Settings")]
    [Tooltip("Art setinin tasarlandığı tile boyu. Örn: tile 64 iken corner=64, h=32, v=32.")]
    public float borderReferenceTileSize = 64f;
    public bool  autoScaleWithTileSize   = true;
    public float cornerSize       = 64f;
    public float straightH_height = 32f;
    public float straightV_width  = 32f;
    public float borderOutside    = 0f;

    [Header("Obstacle")]
    public bool includeObstaclesAsSolid = true;

    public void SetLevelData(LevelData value) => level = value;

    public void Draw(bool[] blocked = null, bool[] holes = null)
    {
        if (level == null || borderRoot == null) return;
        EnsureScaledMetrics();
        _holes = holes;
        ClearChildren();

        int W = level.width;
        int H = level.height;

        // ── YATAY KENARLAR ────────────────────────────────────────────────
        for (int nodeY = 0; nodeY <= H; nodeY++)
        for (int x = 0; x < W; x++)
        {
            bool above = IsSolid(x, nodeY - 1, blocked);
            bool below = IsSolid(x, nodeY,     blocked);
            if (above == below) continue;
            PlaceStraightH(x, nodeY, solidIsBelow: below, blocked);
        }

        // ── DİKEY KENARLAR ────────────────────────────────────────────────
        for (int y = 0; y < H; y++)
        for (int nodeX = 0; nodeX <= W; nodeX++)
        {
            bool leftCell  = IsSolid(nodeX - 1, y, blocked);
            bool rightCell = IsSolid(nodeX,     y, blocked);
            if (leftCell == rightCell) continue;
            PlaceStraightV(nodeX, y, solidIsRight: rightCell, blocked);
        }

        // ── KÖŞELER ───────────────────────────────────────────────────────
        for (int ny = 0; ny <= H; ny++)
        for (int nx = 0; nx <= W; nx++)
            PlaceCorner(nx, ny, blocked);
    }

    // =========================================================================
    // STRAIGHT H
    // =========================================================================
    private void PlaceStraightH(int cx, int nodeY, bool solidIsBelow, bool[] blocked)
    {
        bool cornerL = NodeNeedsTrim(cx,     nodeY, blocked);
        bool cornerR = NodeNeedsTrim(cx + 1, nodeY, blocked);

        float trimL = cornerL ? cornerSize * 0.5f : 0f;
        float trimR = cornerR ? cornerSize * 0.5f : 0f;

        Vector2 nL = NodePos(cx,     nodeY);
        Vector2 nR = NodePos(cx + 1, nodeY);

        float len     = Mathf.Max(1f, tileSize - trimL - trimR);
        float centerX = nL.x + trimL + len * 0.5f;

        float halfH   = straightH_height * 0.5f;
        float centerY = nL.y + (solidIsBelow ? 1f : -1f) * (borderOutside + halfH);

        SpawnRect(straightHPrefab,
                  new Vector2(centerX, centerY),
                  new Vector2(len, straightH_height));
    }

    // =========================================================================
    // STRAIGHT V
    // =========================================================================
    private void PlaceStraightV(int nodeX, int cy, bool solidIsRight, bool[] blocked)
    {
        bool cornerT = NodeNeedsTrim(nodeX, cy,     blocked);
        bool cornerB = NodeNeedsTrim(nodeX, cy + 1, blocked);

        float trimT = cornerT ? cornerSize * 0.5f : 0f;
        float trimB = cornerB ? cornerSize * 0.5f : 0f;

        Vector2 nT = NodePos(nodeX, cy);
        Vector2 nB = NodePos(nodeX, cy + 1);

        float len     = Mathf.Max(1f, tileSize - trimT - trimB);
        float centerY = nT.y - trimT - len * 0.5f;

        float halfW   = straightV_width * 0.5f;
        float centerX = nT.x + (solidIsRight ? -1f : 1f) * (borderOutside + halfW);

        SpawnRect(straightVPrefab,
                  new Vector2(centerX, centerY),
                  new Vector2(straightV_width, len));
    }

    // =========================================================================
    // CORNER
    // Node (nx,ny) çevresindeki 4 hücre:
    //   TL=(nx-1,ny-1)  TR=(nx,ny-1)
    //   BL=(nx-1,ny  )  BR=(nx,ny  )
    // Bitmask: bit0=TL  bit1=TR  bit2=BR  bit3=BL
    // =========================================================================
    private void PlaceCorner(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx,     ny - 1, blocked);
        bool br = IsSolid(nx,     ny,     blocked);
        bool bl = IsSolid(nx - 1, ny,     blocked);

        int mask = (tl?1:0)|(tr?2:0)|(br?4:0)|(bl?8:0);

        if (mask == 0 || mask == 15) return;

        Vector2 node = NodePos(nx, ny);
        // Köşe görselinin merkezi, straight parçaların merkezi ile aynı hatta olmalı.
        // cornerSize/2 kullanmak köşeleri fazla dışarı itip (mask varsa) tamamen görünmez yapabiliyor.
        float offX = borderOutside + straightV_width * 0.5f;
        float offY = borderOutside + straightH_height * 0.5f;
        Vector2 sz = new Vector2(cornerSize, cornerSize);

        switch (mask)
        {
            // outer
            case  4: SpawnRect(cornerLTPrefab, node + new Vector2(-offX, +offY), sz); break; // ┌
            case  8: SpawnRect(cornerRTPrefab, node + new Vector2(+offX, +offY), sz); break; // ┐
            case  2: SpawnRect(cornerLBPrefab, node + new Vector2(-offX, -offY), sz); break; // └
            case  1: SpawnRect(cornerRBPrefab, node + new Vector2(+offX, -offY), sz); break; // ┘
            // inner
            case 11: SpawnRect(cornerLTPrefab, node + new Vector2(+offX, -offY), sz); break; // BR boş
            case  7: SpawnRect(cornerRTPrefab, node + new Vector2(-offX, -offY), sz); break; // BL boş
            case 13: SpawnRect(cornerLBPrefab, node + new Vector2(+offX, +offY), sz); break; // TR boş
            case 14: SpawnRect(cornerRBPrefab, node + new Vector2(-offX, +offY), sz); break; // TL boş

            // diagonal ambiguous: iki ayrı köşe gerekir
            case  5: // TL + BR dolu
                SpawnRect(cornerRBPrefab, node + new Vector2(-offX, +offY), sz); // TL hücre köşesi
                SpawnRect(cornerLTPrefab, node + new Vector2(+offX, -offY), sz); // BR hücre köşesi
                break;
            case 10: // TR + BL dolu
                SpawnRect(cornerLBPrefab, node + new Vector2(+offX, +offY), sz); // TR hücre köşesi
                SpawnRect(cornerRTPrefab, node + new Vector2(-offX, -offY), sz); // BL hücre köşesi
                break;
        }
    }

    private void EnsureScaledMetrics()
    {
        if (!autoScaleWithTileSize) return;
        if (borderReferenceTileSize <= 0f) return;

        float scale = tileSize / borderReferenceTileSize;
        cornerSize       = 64f * scale;
        straightH_height = 32f * scale;
        straightV_width  = 32f * scale;
    }

    // =========================================================================
    private bool NodeNeedsTrim(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx,     ny - 1, blocked);
        bool br = IsSolid(nx,     ny,     blocked);
        bool bl = IsSolid(nx - 1, ny,     blocked);

        bool hasVEdge = (tl != tr) || (bl != br);
        bool hasHEdge = (tl != bl) || (tr != br);

        return hasVEdge && hasHEdge;
    }

    private Vector2 NodePos(int x, int y) => new Vector2(
        x * tileSize + contentOffset.x,
       -y * tileSize + contentOffset.y);

    private bool[] _holes;

    private bool IsSolid(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;
        int idx = level.Index(x, y);

        if (_holes != null && idx >= 0 && idx < _holes.Length && _holes[idx]) return false;
        if (level.cells != null && idx >= 0 && idx < level.cells.Length &&
            level.cells[idx] == (int)CellType.Empty) return false;

        bool isObs = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];
        if ( includeObstaclesAsSolid && isObs) return true;
        if (!includeObstaclesAsSolid && isObs) return false;
        return true;
    }

    private void SpawnRect(GameObject prefab, Vector2 pos, Vector2 size)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.identity;
        rt.localScale       = Vector3.one;
        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget  = false;
            img.preserveAspect = false;
        }
    }

    private void ClearChildren()
    {
        if (borderRoot == null) return;
        for (int i = borderRoot.childCount - 1; i >= 0; i--)
        {
            var c = borderRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c);
            else                       DestroyImmediate(c);
        }
    }
}
