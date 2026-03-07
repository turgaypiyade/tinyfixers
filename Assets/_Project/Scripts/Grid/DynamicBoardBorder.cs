using UnityEngine;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs")]
    public GameObject straightHPrefab;
    public GameObject straightVPrefab;
    public GameObject cornerLTPrefab;
    public GameObject cornerRTPrefab;
    public GameObject cornerLBPrefab;
    public GameObject cornerRBPrefab;

    [Header("Debug")]
    public bool debugMasks = false;
    public bool debugBorderLogs = false;
    public Font debugFont;

    [Header("Layout")]
    public int tileSize = 110;
    public Vector2 contentOffset = Vector2.zero;

    [Header("Border Settings")]
    public float borderThickness = 10f;
    public float borderOutside = 0f;

    [Header("Obstacle")]
    public bool includeObstaclesAsSolid = true;

    private bool[] _holes;

    public float cornerSize       => borderThickness;
    public float straightH_height => borderThickness;
    public float straightV_width  => borderThickness;

    public void SetLevelData(LevelData value) => level = value;

    public void Draw(bool[] blocked = null, bool[] holes = null)
    {
        if (level == null || borderRoot == null) return;

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
            bool leftCell  = IsSolid(nodeX - 1, y, blocked);
            bool rightCell = IsSolid(nodeX, y, blocked);
            if (leftCell == rightCell) continue;
            PlaceStraightV(nodeX, y, solidIsRight: rightCell, blocked);
        }

        // Köşeler en son → render sırasında en üstte
        for (int ny = 0; ny <= H; ny++)
        for (int nx = 0; nx <= W; nx++)
            PlaceCorner(nx, ny, blocked);
    }

    // ═══════════════════════════════════════════════════════════
    //  TRIM MANTIĞI
    //
    //  Outer corner (mask 1,2,4,8):
    //    Köşe grid DIŞINDA, düz parça uzayıp köşenin altına girmeli.
    //    trim = -borderOutside → düz parça borderOutside kadar uzar.
    //    Köşe üstte çizildiği için birleşim görünmez.
    //
    //  Inner corner (mask 7,11,13,14) + Diagonal (mask 5,10):
    //    Köşe grid İÇİNDE, düz parça kısalmalı yoksa "+" olur.
    //    trim = +thickness/2 → düz parça köşe merkezine kadar gelir.
    //
    //  Düz kenar (mask 3,6,9,12): köşe yok → trim = 0
    // ═══════════════════════════════════════════════════════════

    private float GetTrim(int nodeMask)
    {
        switch (nodeMask)
        {
            // Outer: uzat (köşe dışarıda, düz parça yetişmeli)
            case 1: case 2: case 4: case 8:
                return -borderOutside;

            // Inner + Diagonal: kısalt ("+" olmasın)
            case 7: case 11: case 13: case 14:
            case 5: case 10:
                return borderThickness * 0.5f;

            // Düz kenar: trim yok
            default:
                return 0f;
        }
    }

    private void PlaceStraightH(int cx, int nodeY, bool solidIsBelow, bool[] blocked)
    {
        int maskL = GetNodeMask(cx, nodeY, blocked);
        int maskR = GetNodeMask(cx + 1, nodeY, blocked);

        float trimL = GetTrim(maskL);
        float trimR = GetTrim(maskR);

        Vector2 nL   = NodePos(cx, nodeY);
        float rawLen  = tileSize - trimL - trimR;
        if (rawLen <= 0.01f) return;

        float halfT   = borderThickness * 0.5f;
        float centerX = nL.x + trimL + rawLen * 0.5f;
        float centerY = nL.y + (solidIsBelow ? 1f : -1f) * (borderOutside + halfT);

        SpawnStraight(straightHPrefab, new Vector2(centerX, centerY),
                      new Vector2(rawLen, borderThickness));

        if (debugBorderLogs)
            Debug.Log($"[Border][H] cell=({cx},{nodeY}) below={solidIsBelow} " +
                      $"pos=({centerX:F1},{centerY:F1}) len={rawLen:F1} tL={trimL} tR={trimR}");
    }

    private void PlaceStraightV(int nodeX, int cy, bool solidIsRight, bool[] blocked)
    {
        int maskT = GetNodeMask(nodeX, cy, blocked);
        int maskB = GetNodeMask(nodeX, cy + 1, blocked);

        float trimT = GetTrim(maskT);
        float trimB = GetTrim(maskB);

        Vector2 nT   = NodePos(nodeX, cy);
        float rawLen  = tileSize - trimT - trimB;
        if (rawLen <= 0.01f) return;

        float halfT   = borderThickness * 0.5f;
        float centerY = nT.y - trimT - rawLen * 0.5f;
        float centerX = nT.x + (solidIsRight ? -1f : 1f) * (borderOutside + halfT);

        SpawnStraight(straightVPrefab, new Vector2(centerX, centerY),
                      new Vector2(borderThickness, rawLen));

        if (debugBorderLogs)
            Debug.Log($"[Border][V] node=({nodeX},{cy}) right={solidIsRight} " +
                      $"pos=({centerX:F1},{centerY:F1}) len={rawLen:F1} tT={trimT} tB={trimB}");
    }

    // ═══════════════════════════════════════════════════════════
    //  KÖŞE
    //
    //  center = node + dir × (borderOutside + thickness/2)
    //  pivot  = (0.5, 0.5) her zaman
    //  size   = thickness × thickness
    // ═══════════════════════════════════════════════════════════

    private void PlaceCorner(int nx, int ny, bool[] blocked)
    {
        int mask = GetNodeMask(nx, ny, blocked);
        if (mask == 0 || mask == 15) return;

        Vector2 node = NodePos(nx, ny);
        float t   = borderThickness;
        float off = borderOutside + t * 0.5f;

        if (debugMasks) SpawnMaskLabel(node, mask);

        switch (mask)
        {
            case 4:  Place(cornerLTPrefab, -off, +off); break;
            case 8:  Place(cornerRTPrefab, +off, +off); break;
            case 2:  Place(cornerLBPrefab, -off, -off); break;
            case 1:  Place(cornerRBPrefab, +off, -off); break;

            case 11: Place(cornerLTPrefab, +off, -off); break;
            case 7:  Place(cornerRTPrefab, -off, -off); break;
            case 13: Place(cornerLBPrefab, +off, +off); break;
            case 14: Place(cornerRBPrefab, -off, +off); break;

            case 5:
                Place(cornerRBPrefab, +off, -off);
                Place(cornerLTPrefab, -off, +off);
                break;
            case 10:
                Place(cornerLBPrefab, -off, -off);
                Place(cornerRTPrefab, +off, +off);
                break;
        }

        void Place(GameObject prefab, float dx, float dy)
        {
            SpawnCorner(prefab, node + new Vector2(dx, dy),
                        new Vector2(t, t), mask);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN
    // ═══════════════════════════════════════════════════════════

    private void SpawnCorner(GameObject prefab, Vector2 center, Vector2 size, int mask)
    {
        if (prefab == null || borderRoot == null) return;

        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.identity;
        rt.localScale       = Vector3.one;

        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget  = false;
            img.preserveAspect = false;
            img.type           = Image.Type.Simple;
        }

        if (debugBorderLogs)
            Debug.Log($"[Border][Corner] mask={mask} center={center} size={size}");
    }

    private void SpawnStraight(GameObject prefab, Vector2 center, Vector2 size)
    {
        if (prefab == null) return;

        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.identity;
        rt.localScale       = Vector3.one;

        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget  = false;
            img.preserveAspect = false;
            img.type           = Image.Type.Tiled;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  YARDIMCI
    // ═══════════════════════════════════════════════════════════

    private Vector2 NodePos(int x, int y)
    {
        float ox = -(level.width  * tileSize) * 0.5f;
        float oy =  (level.height * tileSize) * 0.5f;
        return new Vector2(
            ox + x * tileSize + contentOffset.x,
            oy - y * tileSize + contentOffset.y);
    }

    private int GetNodeMask(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx,     ny - 1, blocked);
        bool br = IsSolid(nx,     ny,     blocked);
        bool bl = IsSolid(nx - 1, ny,     blocked);
        return (tl ? 1 : 0) | (tr ? 2 : 0) | (br ? 4 : 0) | (bl ? 8 : 0);
    }

    private bool IsSolid(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;
        int idx = level.Index(x, y);

        bool isBlocked = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];

        if (includeObstaclesAsSolid && isBlocked) return true;
        if (!includeObstaclesAsSolid && isBlocked) return false;

        if (_holes != null && idx >= 0 && idx < _holes.Length && _holes[idx])
            return false;

        if (level.cells != null && idx >= 0 && idx < level.cells.Length &&
            level.cells[idx] == (int)CellType.Empty)
            return false;

        return true;
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
        t.text      = mask.ToString();
        t.font      = debugFont != null ? debugFont
                      : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 18;
        t.color     = Color.magenta;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
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