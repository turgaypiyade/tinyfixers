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

    [Header("3D Edge Prefabs")]
    public GameObject edgeTopPrefab;
    public GameObject edgeBottomPrefab;
    public GameObject edgeLeftPrefab;
    public GameObject edgeRightPrefab;

    [Header("3D Outer Corner Prefabs")]
    public GameObject outerLTPrefab;
    public GameObject outerRTPrefab;
    public GameObject outerLBPrefab;
    public GameObject outerRBPrefab;

    [Header("3D Inner Corner Prefabs")]
    public GameObject innerLTPrefab;
    public GameObject innerRTPrefab;
    public GameObject innerLBPrefab;
    public GameObject innerRBPrefab;

    [Header("3D Border Join Settings")]
    public bool use3DBorderPrefabs = true;

    [Tooltip("If true, corner size and overlap are calculated from the source sprite metrics and borderThickness. Turn off only for manual tuning.")]
    public bool autoSize3DBorderFromSprites = true;

    [Tooltip("For the v3 node-centered sprites this is 48 for the 48px folder, 96 for the 96px_hd folder.")]
    public float sourceBorderThicknessPx = 48f;

    [Tooltip("For the v3 node-centered sprites this is 96 for the 48px folder, 192 for the 96px_hd folder.")]
    public float sourceCornerCanvasPx = 96f;

    [Tooltip("Source-pixel overlap for outer corners. Runtime value is scaled by borderThickness / sourceBorderThicknessPx.")]
    public float sourceOuterJoinOverlapPx = 14f;

    [Tooltip("Source-pixel overlap for inner corners. Runtime value is scaled by borderThickness / sourceBorderThicknessPx.")]
    public float sourceInnerJoinOverlapPx = 8f;

    [Tooltip("Use this with node-centered corner sprites. Corner center is placed exactly on the grid node.")]
    public bool center3DCornersOnGridNode = true;

    [Tooltip("Manual corner size. Used only when autoSize3DBorderFromSprites is false.")]
    public float cornerVisualSize = 48f;

    [Tooltip("Manual outer overlap. Used only when autoSize3DBorderFromSprites is false.")]
    public float outerJoinOverlap = 8f;

    [Tooltip("Manual inner overlap. Used only when autoSize3DBorderFromSprites is false.")]
    public float innerJoinOverlap = 4f;

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

    private float BorderSpriteScale => borderThickness / Mathf.Max(1f, sourceBorderThicknessPx);
    private float RuntimeCornerSize => use3DBorderPrefabs && autoSize3DBorderFromSprites
        ? sourceCornerCanvasPx * BorderSpriteScale
        : cornerVisualSize;
    private float RuntimeOuterJoinOverlap => use3DBorderPrefabs && autoSize3DBorderFromSprites
        ? sourceOuterJoinOverlapPx * BorderSpriteScale
        : outerJoinOverlap;
    private float RuntimeInnerJoinOverlap => use3DBorderPrefabs && autoSize3DBorderFromSprites
        ? sourceInnerJoinOverlapPx * BorderSpriteScale
        : innerJoinOverlap;

    public float cornerSize       => use3DBorderPrefabs ? RuntimeCornerSize : borderThickness;
    public float straightH_height => borderThickness;
    public float straightV_width  => borderThickness;

    public void SetLevelData(LevelData value) => level = value;

    private void OnValidate()
    {
        borderThickness = Mathf.Max(1f, borderThickness);
        sourceBorderThicknessPx = Mathf.Max(1f, sourceBorderThicknessPx);
        sourceCornerCanvasPx = Mathf.Max(sourceBorderThicknessPx, sourceCornerCanvasPx);
        sourceOuterJoinOverlapPx = Mathf.Max(0f, sourceOuterJoinOverlapPx);
        sourceInnerJoinOverlapPx = Mathf.Max(0f, sourceInnerJoinOverlapPx);
        cornerVisualSize = Mathf.Max(borderThickness, cornerVisualSize);
        outerJoinOverlap = Mathf.Max(0f, outerJoinOverlap);
        innerJoinOverlap = Mathf.Max(0f, innerJoinOverlap);
    }

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

        // Corners render last, so they cover the straight-piece joins.
        for (int ny = 0; ny <= H; ny++)
        for (int nx = 0; nx <= W; nx++)
            PlaceCorner(nx, ny, blocked);
    }

    // ═══════════════════════════════════════════════════════════
    //  TRIM / JOIN LOGIC
    //
    //  Classic 2D mode:
    //    Keeps the previous white-line behaviour.
    //
    //  3D mode:
    //    Outer corners: straight pieces extend under the corner sprite.
    //    Inner corners: straight pieces are shortened enough to avoid a plus sign,
    //    but still keep a small overlap under the inner corner sprite.
    //
    //  The important part is that the values are derived from the source sprite
    //  metrics. Changing only borderThickness scales the whole system.
    // ═══════════════════════════════════════════════════════════

    private float GetTrim(int nodeMask)
    {
        if (!use3DBorderPrefabs)
        {
            switch (nodeMask)
            {
                // Outer: extend to reach the old flat corner.
                case 1: case 2: case 4: case 8:
                    return -borderOutside;

                // Inner + Diagonal: shorten to avoid a plus sign.
                case 7: case 11: case 13: case 14:
                case 5: case 10:
                    return borderThickness * 0.5f;

                default:
                    return 0f;
            }
        }

        switch (nodeMask)
        {
            // Outer corner: straight piece goes slightly under the corner cap.
            case 1: case 2: case 4: case 8:
                return -RuntimeOuterJoinOverlap;

            // Inner corner / diagonal: prevent plus artefacts while preserving overlap.
            case 7: case 11: case 13: case 14:
            case 5: case 10:
                return Mathf.Max(0f, borderThickness * 0.5f - RuntimeInnerJoinOverlap);

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

        SpawnStraight(PickHorizontalPrefab(solidIsBelow), new Vector2(centerX, centerY),
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

        SpawnStraight(PickVerticalPrefab(solidIsRight), new Vector2(centerX, centerY),
                      new Vector2(borderThickness, rawLen));

        if (debugBorderLogs)
            Debug.Log($"[Border][V] node=({nodeX},{cy}) right={solidIsRight} " +
                      $"pos=({centerX:F1},{centerY:F1}) len={rawLen:F1} tT={trimT} tB={trimB}");
    }

    private GameObject PickHorizontalPrefab(bool solidIsBelow)
    {
        if (!use3DBorderPrefabs)
            return straightHPrefab;

        // If the solid area is below the node line, this is the top edge of that area.
        if (solidIsBelow)
            return edgeTopPrefab != null ? edgeTopPrefab : straightHPrefab;

        return edgeBottomPrefab != null ? edgeBottomPrefab : straightHPrefab;
    }

    private GameObject PickVerticalPrefab(bool solidIsRight)
    {
        if (!use3DBorderPrefabs)
            return straightVPrefab;

        // If the solid area is right of the node line, this is the left edge of that area.
        if (solidIsRight)
            return edgeLeftPrefab != null ? edgeLeftPrefab : straightVPrefab;

        return edgeRightPrefab != null ? edgeRightPrefab : straightVPrefab;
    }

    // ═══════════════════════════════════════════════════════════
    //  CORNERS
    //
    //  Old flat mode:
    //    center = node + dir × (borderOutside + thickness/2)
    //    size   = thickness
    //
    //  Node-centered 3D mode:
    //    center = node
    //    size   = sourceCornerCanvasPx × borderThickness / sourceBorderThicknessPx
    //
    //  This removes the trial-and-error corner offset. The sprite is authored around
    //  the grid node, so the same placement works for outer corners, inner corners,
    //  holes and diagonal touches.
    // ═══════════════════════════════════════════════════════════

    private void PlaceCorner(int nx, int ny, bool[] blocked)
    {
        int mask = GetNodeMask(nx, ny, blocked);
        if (mask == 0 || mask == 15) return;

        Vector2 node = NodePos(nx, ny);
        float t    = borderThickness;
        float off  = borderOutside + t * 0.5f;
        float size = cornerSize;
        bool centered3D = use3DBorderPrefabs && center3DCornersOnGridNode;

        if (debugMasks) SpawnMaskLabel(node, mask);

        switch (mask)
        {
            // OUTER corners
            case 4:  Place(PickOuterLT(), -off, +off); break;
            case 8:  Place(PickOuterRT(), +off, +off); break;
            case 2:  Place(PickOuterLB(), -off, -off); break;
            case 1:  Place(PickOuterRB(), +off, -off); break;

            // INNER corners
            case 11: Place(PickInnerLT(), +off, -off); break;
            case 7:  Place(PickInnerRT(), -off, -off); break;
            case 13: Place(PickInnerLB(), +off, +off); break;
            case 14: Place(PickInnerRB(), -off, +off); break;

            // Diagonal touch: draw two separate outside corner caps.
            case 5:
                Place(PickOuterRB(), +off, -off);
                Place(PickOuterLT(), -off, +off);
                break;

            case 10:
                Place(PickOuterLB(), -off, -off);
                Place(PickOuterRT(), +off, +off);
                break;
        }

        void Place(GameObject prefab, float oldDx, float oldDy)
        {
            Vector2 center = centered3D
                ? node
                : node + new Vector2(oldDx, oldDy);

            SpawnCorner(prefab, center, new Vector2(size, size), mask);
        }
    }

    private GameObject PickOuterLT() => use3DBorderPrefabs && outerLTPrefab != null ? outerLTPrefab : cornerLTPrefab;
    private GameObject PickOuterRT() => use3DBorderPrefabs && outerRTPrefab != null ? outerRTPrefab : cornerRTPrefab;
    private GameObject PickOuterLB() => use3DBorderPrefabs && outerLBPrefab != null ? outerLBPrefab : cornerLBPrefab;
    private GameObject PickOuterRB() => use3DBorderPrefabs && outerRBPrefab != null ? outerRBPrefab : cornerRBPrefab;

    private GameObject PickInnerLT() => use3DBorderPrefabs && innerLTPrefab != null ? innerLTPrefab : cornerLTPrefab;
    private GameObject PickInnerRT() => use3DBorderPrefabs && innerRTPrefab != null ? innerRTPrefab : cornerRTPrefab;
    private GameObject PickInnerLB() => use3DBorderPrefabs && innerLBPrefab != null ? innerLBPrefab : cornerLBPrefab;
    private GameObject PickInnerRB() => use3DBorderPrefabs && innerRBPrefab != null ? innerRBPrefab : cornerRBPrefab;

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
            img.type           = Image.Type.Tiled;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
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
