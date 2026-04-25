using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// V4 — Sprite içine "outside padding" entegre.
///
/// Sprite kanonik boyutu T+2P (örn 160), içinde profil T çapında (128), etrafında P padding (16).
/// Unity'de borderThickness ile sprite'ı T+2P boyutunda kullanırız:
///   - Görünür profil kalınlığı = T * (CORE/TOTAL) = T * 0.8
///   - Padding = T * (PAD/TOTAL) = T * 0.1 (her yöne)
///   - Sprite tile sınırından otomatik %10 dışa taşar
///
/// Bu yaklaşımın faydası:
///   - borderOutside parametresine gerek yok (sprite kendi dışa taşmasını taşıyor)
///   - Tüm konum mantığı tek ve tutarlı: HER SPRITE NODE-MERKEZLİ
///   - Outer corner ve straight aynı koordinat sisteminde
///   - Çift profil/çakışma sorunu yok
/// </summary>
public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Legacy Flat Prefabs (fallback only)")]
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

    [Header("3D Border Settings")]
    public bool use3DBorderPrefabs = true;

    [Header("Debug")]
    public bool debugMasks = false;
    public bool debugBorderLogs = false;
    [Tooltip("Her border sprite'ın etrafına outline çizer (gerçek konumu görmek için).")]
    public bool debugDrawOutlines = false;
    public Font debugFont;

    [Header("Layout")]
    public int tileSize = 110;
    public Vector2 contentOffset = Vector2.zero;

    [Header("Border Settings")]
    [Tooltip("Sprite'ın RectTransform total boyutu. Yüklediğin 160px sprite'larda görünür core yaklaşık %80 olduğu için gerçek görünen kalınlık = borderThickness * spriteCoreRatio.")]
    public float borderThickness = 30f;

    [Tooltip("Sprite içindeki görünür border/core oranı. Yüklediğin 160px sprite'larda alpha alanı 128px olduğu için 128/160 = 0.8.")]
    [Range(0.1f, 1f)]
    public float spriteCoreRatio = 0.8f;

    [Tooltip("Görünür border'ın iç kenarı grid çizgisinden kaç px dışarıda dursun? İstediğin değer: 2.")]
    public float outsideGap = 2f;

    [Tooltip("Açıkken border gridin üstüne binmez; görünür border grid çizgisinin dışına taşınır.")]
    public bool keepBorderOutsideGrid = true;

    private float VisibleBorderThickness => borderThickness * spriteCoreRatio;

    // Görünür stroke'un iç kenarını grid çizgisinden outsideGap kadar dışarı almak için
    // center offset = visibleThickness/2 + gap.
    private float OutsideCenterOffset =>
        keepBorderOutsideGrid ? (outsideGap + VisibleBorderThickness * 0.5f) : 0f;

    [Header("Obstacle")]
    public bool includeObstaclesAsSolid = true;

    private bool[] _holes;

    public float CornerSize => borderThickness;
    public float StraightThickness => borderThickness;

    // ─── Backward-compatible aliases ───
    public float cornerSize => borderThickness;
    public float straightH_height => borderThickness;
    public float straightV_width => borderThickness;

    // GridSpawner uyumluluğu için (eski borderOutside kullananlar)
    [HideInInspector] public float borderOutside = 0f;

    public void SetLevelData(LevelData value) => level = value;

    private void OnValidate()
    {
        borderThickness = Mathf.Max(1f, borderThickness);
        spriteCoreRatio = Mathf.Clamp(spriteCoreRatio, 0.1f, 1f);
        outsideGap = Mathf.Max(0f, outsideGap);
    }

    // ═══════════════════════════════════════════════════════════
    //  ENTRY POINT
    // ═══════════════════════════════════════════════════════════

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
                bool leftCell = IsSolid(nodeX - 1, y, blocked);
                bool rightCell = IsSolid(nodeX, y, blocked);
                if (leftCell == rightCell) continue;
                PlaceStraightV(nodeX, y, solidIsRight: rightCell, blocked);
            }

        for (int ny = 0; ny <= H; ny++)
            for (int nx = 0; nx <= W; nx++)
                PlaceCorner(nx, ny, blocked);
    }

    // ═══════════════════════════════════════════════════════════
    //  STRAIGHTS
    //  
    //  Tüm sprite'lar NODE-MERKEZLİ T×T (T = borderThickness, sprite total).
    //  Straight kalınlık merkezi tam node hattında.
    //  Straight uçları her köşede T/2 kısalır (varsa köşe).
    //  Pass-through (mask 3, 12, 6, 9): trim 0, kesintisiz çizgi.
    // ═══════════════════════════════════════════════════════════

    private float GetTrim(int nodeMask)
    {
        if (!HasCornerAtNode(nodeMask)) return 0f;

        // Outer corner sprites have padding. With borderThickness=24,
        // that padding scales to 2.4 px. The visible corner side
        // starts outside the node by outsideGap, so the straight
        // must extend past the node by outsideGap on outer corners.
        if (IsOuterCornerMask(nodeMask))
            return keepBorderOutsideGrid ? -outsideGap : 0f;

        // Inner corners already sit well, so keep the previous trim.
        float trim = borderThickness * 0.5f - OutsideCenterOffset;
        return Mathf.Max(0f, trim);
    }

    private bool IsOuterCornerMask(int nodeMask)
    {
        return nodeMask == 1 || nodeMask == 2 || nodeMask == 4 || nodeMask == 8 ||
               nodeMask == 5 || nodeMask == 10;
    }

    private bool HasCornerAtNode(int nodeMask)
    {
        if (nodeMask == 0 || nodeMask == 15) return false;
        if (nodeMask == 3 || nodeMask == 12) return false;
        if (nodeMask == 9 || nodeMask == 6) return false;
        return true;
    }

    private void PlaceStraightH(int cx, int nodeY, bool solidIsBelow, bool[] blocked)
    {
        int maskL = GetNodeMask(cx, nodeY, blocked);
        int maskR = GetNodeMask(cx + 1, nodeY, blocked);

        float trimL = GetTrim(maskL);
        float trimR = GetTrim(maskR);

        Vector2 nL = NodePos(cx, nodeY);
        float rawLen = tileSize - trimL - trimR;
        if (rawLen <= 0.01f) return;

        float centerX = nL.x + trimL + rawLen * 0.5f;
        float centerY = nL.y;

        Vector2 outsideNormal = solidIsBelow ? Vector2.up : Vector2.down;
        Vector2 center = new Vector2(centerX, centerY) + outsideNormal * OutsideCenterOffset;

        SpawnStraight(PickHorizontalPrefab(solidIsBelow),
                      center,
                      new Vector2(rawLen, borderThickness));

        if (debugBorderLogs)
            Debug.Log($"[Border][H] cell=({cx},{nodeY}) below={solidIsBelow} " +
                      $"pos=({center.x:F1},{center.y:F1}) base=({centerX:F1},{centerY:F1}) len={rawLen:F1} tL={trimL} tR={trimR}");
    }

    private void PlaceStraightV(int nodeX, int cy, bool solidIsRight, bool[] blocked)
    {
        int maskT = GetNodeMask(nodeX, cy, blocked);
        int maskB = GetNodeMask(nodeX, cy + 1, blocked);

        float trimT = GetTrim(maskT);
        float trimB = GetTrim(maskB);

        Vector2 nT = NodePos(nodeX, cy);
        float rawLen = tileSize - trimT - trimB;
        if (rawLen <= 0.01f) return;

        float centerY = nT.y - trimT - rawLen * 0.5f;
        float centerX = nT.x;

        Vector2 outsideNormal = solidIsRight ? Vector2.left : Vector2.right;
        Vector2 center = new Vector2(centerX, centerY) + outsideNormal * OutsideCenterOffset;

        SpawnStraight(PickVerticalPrefab(solidIsRight),
                      center,
                      new Vector2(borderThickness, rawLen));

        if (debugBorderLogs)
            Debug.Log($"[Border][V] node=({nodeX},{cy}) right={solidIsRight} " +
                      $"pos=({center.x:F1},{center.y:F1}) base=({centerX:F1},{centerY:F1}) len={rawLen:F1} tT={trimT} tB={trimB}");
    }

    private GameObject PickHorizontalPrefab(bool solidIsBelow)
    {
        if (!use3DBorderPrefabs) return straightHPrefab;
        if (solidIsBelow)
            return edgeTopPrefab != null ? edgeTopPrefab : straightHPrefab;
        return edgeBottomPrefab != null ? edgeBottomPrefab : straightHPrefab;
    }

    private GameObject PickVerticalPrefab(bool solidIsRight)
    {
        if (!use3DBorderPrefabs) return straightVPrefab;
        if (solidIsRight)
            return edgeLeftPrefab != null ? edgeLeftPrefab : straightVPrefab;
        return edgeRightPrefab != null ? edgeRightPrefab : straightVPrefab;
    }

    // ═══════════════════════════════════════════════════════════
    //  CORNERS — TÜMÜ NODE-MERKEZLİ
    // ═══════════════════════════════════════════════════════════

    private void PlaceCorner(int nx, int ny, bool[] blocked)
    {
        int mask = GetNodeMask(nx, ny, blocked);
        if (mask == 0 || mask == 15) return;
        if (mask == 3 || mask == 12 || mask == 9 || mask == 6) return;

        Vector2 node = NodePos(nx, ny);
        float size = CornerSize;

        if (debugMasks) SpawnMaskLabel(node, mask);

        switch (mask)
        {
            // Tek solid quadrant: corner'ı solid hücreden uzağa, yani dışarıya al.
            case 1: PlaceOffset(PickOuterLT(), Vector2.right + Vector2.down); break; // TL solid
            case 2: PlaceOffset(PickOuterRT(), Vector2.left + Vector2.down); break;  // TR solid
            case 4: PlaceOffset(PickOuterRB(), Vector2.left + Vector2.up); break;    // BR solid
            case 8: PlaceOffset(PickOuterLB(), Vector2.right + Vector2.up); break;   // BL solid

            // Üç solid quadrant: eksik/boş quadrant tarafı dışarıdır.
            case 14: PlaceOffset(PickInnerLT(), Vector2.left + Vector2.up); break;    // TL boş
            case 13: PlaceOffset(PickInnerRT(), Vector2.right + Vector2.up); break;   // TR boş
            case 11: PlaceOffset(PickInnerRB(), Vector2.right + Vector2.down); break; // BR boş
            case 7:  PlaceOffset(PickInnerLB(), Vector2.left + Vector2.down); break;  // BL boş

            // Diagonal temas: iki ayrı dış corner var; aynı node'da değil, kendi dış yönlerine offsetlenmeli.
            case 5:
                PlaceOffset(PickOuterLT(), Vector2.right + Vector2.down); // TL solid
                PlaceOffset(PickOuterRB(), Vector2.left + Vector2.up);     // BR solid
                break;
            case 10:
                PlaceOffset(PickOuterRT(), Vector2.left + Vector2.down);   // TR solid
                PlaceOffset(PickOuterLB(), Vector2.right + Vector2.up);    // BL solid
                break;
        }

        void PlaceOffset(GameObject prefab, Vector2 dir)
        {
            // Diagonal yönlerde normalize etmiyoruz; x ve y eksenlerinde ayrı ayrı aynı offset gerekli.
            Vector2 sign = new Vector2(Mathf.Sign(dir.x), Mathf.Sign(dir.y));
            Vector2 center = node + sign * OutsideCenterOffset;
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
    //  SPAWN HELPERS
    // ═══════════════════════════════════════════════════════════

    private void SpawnCorner(GameObject prefab, Vector2 center, Vector2 size, int mask)
    {
        if (prefab == null || borderRoot == null) return;
        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget = false;
            img.preserveAspect = false;
            img.type = Image.Type.Simple;
        }
        if (debugDrawOutlines) SpawnDebugOutline(center, size, new Color(1f, 0f, 0f, 0.9f));
        if (debugBorderLogs)
            Debug.Log($"[Border][Corner] mask={mask} center=({center.x:F2}, {center.y:F2}) size=({size.x:F2}, {size.y:F2})");
    }

    private void SpawnStraight(GameObject prefab, Vector2 center, Vector2 size)
    {
        if (prefab == null || borderRoot == null) return;
        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget = false;
            img.preserveAspect = false;
            img.type = Image.Type.Simple;
        }
        if (debugDrawOutlines) SpawnDebugOutline(center, size, new Color(0f, 1f, 0f, 0.9f));
    }

    private void SpawnDebugOutline(Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject("DbgOutline", typeof(RectTransform));
        go.transform.SetParent(borderRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = size;
        AddDebugEdge(rt, new Vector2(0, size.y * 0.5f), new Vector2(size.x, 1f), color);
        AddDebugEdge(rt, new Vector2(0, -size.y * 0.5f), new Vector2(size.x, 1f), color);
        AddDebugEdge(rt, new Vector2(-size.x * 0.5f, 0), new Vector2(1f, size.y), color);
        AddDebugEdge(rt, new Vector2(size.x * 0.5f, 0), new Vector2(1f, size.y), color);
    }

    private void AddDebugEdge(RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        var e = new GameObject("e", typeof(RectTransform), typeof(Image));
        e.transform.SetParent(parent, false);
        var rt = e.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = e.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  GRID HELPERS
    // ═══════════════════════════════════════════════════════════

    private Vector2 NodePos(int x, int y)
    {
        float ox = -(level.width * tileSize) * 0.5f;
        float oy = (level.height * tileSize) * 0.5f;
        return new Vector2(
            ox + x * tileSize + contentOffset.x,
            oy - y * tileSize + contentOffset.y);
    }

    private int GetNodeMask(int nx, int ny, bool[] blocked)
    {
        bool tl = IsSolid(nx - 1, ny - 1, blocked);
        bool tr = IsSolid(nx, ny - 1, blocked);
        bool br = IsSolid(nx, ny, blocked);
        bool bl = IsSolid(nx - 1, ny, blocked);
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
        t.text = mask.ToString();
        t.font = debugFont != null ? debugFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18;
        t.color = Color.magenta;
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
