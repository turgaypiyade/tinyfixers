using UnityEngine;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs (UI Image)")]
    public GameObject aboveStraightPrefab;   // üst kenar
    public GameObject belowStraightPrefab;   // alt kenar
    public GameObject outerCornerPrefab;
    public GameObject innerCornerPrefab;

    [Header("Hizalama Ayarları")]
    public int tileSize = 110;

    [Header("Debug")]
    public bool debugMasks = false;
    public Font debugFont;

    [Header("Optional: Treat obstacles as solid")]
    public bool includeObstaclesAsSolid = true;

    public float thickness = 20f;
    public float borderOutside = 15f;
    public Vector2 contentOffset = new Vector2(8f, -8f);

    public float joinOverlap = 10f;
    public float cornerScale = 1.20f;

    [Header("Top Edge Optical Tuning")]
    [Tooltip("Üst çizgi kalınlığını optik olarak köşeyle eşitlemek için çarpan (1 = matematiksel eşit)")]
    public float topEdgeOpticalScale = 1f;

    public void SetLevelData(LevelData value) => level = value;

    public void Draw(bool[] blocked = null)
    {
        if (level == null || borderRoot == null) return;
        ClearChildren();

        int w = level.width;
        int h = level.height;

        // Köşe ile AYNI offset hesabı
        float baseOff  = borderOutside + (thickness * 0.5f);
        float k        = 0.70f;
        float offOuter = Mathf.Max(0f, baseOff - joinOverlap * k);
        float cornerSize = (thickness + joinOverlap * 2f) * cornerScale;
        float topEdgeThickness = cornerSize * Mathf.Max(0.1f, topEdgeOpticalScale);
        // Kenar-köşe dış hatlarını aynı hizada tutmak için köşe merkez offset düzeltmesi
        float cornerOuterCenterOffset = offOuter + ((topEdgeThickness - cornerSize) * 0.5f);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsSolid(x, y, blocked)) continue;

            Vector2 cell = GetCellCenter(x, y);
            float   half = tileSize / 2f;

            // ÜST kenar — sadece TEK KENAR debug çizimi.
            // Kalınlık/offset köşeler ile aynı metrikten türetilir:
            // - offset: offOuter
            // - kalınlık: cornerSize (köşe sprite'ı ile aynı görsel yükseklik)
            // - köşe birleşimi için yatayda joinOverlap kadar taşırılır
            if (!IsSolid(x, y - 1, blocked))
                SpawnTopEdge(
                    pos:  new Vector2(cell.x, cell.y + half + offOuter),
                    rot:  0f,
                    size: new Vector2(tileSize + (joinOverlap * 2f), topEdgeThickness));
        }

        // Köşeler — AYNEN ESKİ KOD
        for (int y = 0; y <= h; y++)
        for (int x = 0; x <= w; x++)
        {
            int mask = GetBitmask(x, y, blocked);
            if (mask > 0 && mask < 15)
                HandleCorner(GetNodePosition(x, y), mask, cornerOuterCenterOffset, cornerSize);
        }
    }

    // ── Köşeler — hiç değişmedi ──────────────────────────

    private void HandleCorner(Vector2 nodePos, int mask, float offOuter, float cornerSize)
    {
        if (debugMasks) SpawnMaskLabel(nodePos, mask);

        float baseOff  = borderOutside + (thickness * 0.5f);
        float k        = 0.70f;
        float offInner = baseOff + joinOverlap * k;

        Vector2 sz = new Vector2(cornerSize, cornerSize);

        switch (mask)
        {
            case 1: Spawn(outerCornerPrefab, nodePos + new Vector2(-offOuter, +offOuter), 0, sz, flipX: true,  flipY: true);  break;
            case 2: Spawn(outerCornerPrefab, nodePos + new Vector2(+offOuter, +offOuter), 0, sz, flipX: false, flipY: true);  break;
            case 4: Spawn(outerCornerPrefab, nodePos + new Vector2(+offOuter, -offOuter), 0, sz, flipX: false, flipY: false); break;
            case 8: Spawn(outerCornerPrefab, nodePos + new Vector2(-offOuter, -offOuter), 0, sz, flipX: true,  flipY: false); break;

            case 14: Spawn(innerCornerPrefab, nodePos + new Vector2(-offInner-5f, +offInner+5f), 0, sz, flipX: true,  flipY: true);  break;
            case 13: Spawn(innerCornerPrefab, nodePos + new Vector2(+offInner+5f, +offInner+5f), 0, sz, flipX: false, flipY: true);  break;
            case 11: Spawn(innerCornerPrefab, nodePos + new Vector2(+offInner+5f, -offInner-5f), 0, sz, flipX: false, flipY: false); break;
            case 7:  Spawn(innerCornerPrefab, nodePos + new Vector2(-offInner-5f, -offInner-5f), 0, sz, flipX: true,  flipY: false); break;
        }
    }

    // ── Yardımcılar ──────────────────────────────────────

    private int GetBitmask(int x, int y, bool[] blocked)
    {
        int mask = 0;
        if (IsSolid(x-1, y-1, blocked)) mask |= 1;
        if (IsSolid(x,   y-1, blocked)) mask |= 2;
        if (IsSolid(x,   y,   blocked)) mask |= 4;
        if (IsSolid(x-1, y,   blocked)) mask |= 8;
        return mask;
    }

    private bool IsSolid(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;
        int idx = level.Index(x, y);
        bool isObs = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];
        if ( includeObstaclesAsSolid && isObs) return true;
        if (!includeObstaclesAsSolid && isObs) return false;
        if (level.cells != null && idx >= 0 && idx < level.cells.Length &&
            level.cells[idx] == (int)CellType.Empty) return false;
        return true;
    }

    private Vector2 GetCellCenter(int x, int y) => new Vector2(
        x * tileSize + contentOffset.x + tileSize / 2f,
        -y * tileSize + contentOffset.y - tileSize / 2f);

    private Vector2 GetNodePosition(int x, int y) => new Vector2(
        x * tileSize + contentOffset.x,
        -y * tileSize + contentOffset.y);

    private void SpawnTopEdge(Vector2 pos, float rot, Vector2 size)
    {
        if (belowStraightPrefab == null) return;

        var go = Instantiate(belowStraightPrefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.Euler(0, 0, rot);
        rt.localScale       = Vector3.one;

        if (go.TryGetComponent(out Image img))
        {
            // board_tiles_v1_16 sprite'ında border bilgisi var; Sliced ile çizince
            // çizgi kalınlığı rect yüksekliğine daha doğru tepki verir.
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.raycastTarget  = false;
            img.preserveAspect = false;
        }
    }

    private void Spawn(GameObject prefab, Vector2 pos, float rot, Vector2 size,
        bool flipX = false, bool flipY = false)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        rt.localRotation    = Quaternion.Euler(0, 0, rot);
        rt.localScale       = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);
        if (go.TryGetComponent(out Image img))
        {
            img.raycastTarget  = false;
            img.preserveAspect = false;
        }
    }

    private void SpawnMaskLabel(Vector2 pos, int mask)
    {
        var go = new GameObject("Mask_" + mask, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(borderRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(60, 30);
        var t = go.GetComponent<Text>();
        t.text      = mask.ToString();
        t.font      = debugFont != null ? debugFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            else                       DestroyImmediate(c);
        }
    }
}
