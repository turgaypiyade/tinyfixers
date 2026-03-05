using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs (UI Image)")]
    public GameObject cornerLbPrefab;
    public GameObject cornerLtPrefab;
    public GameObject cornerRbPrefab;
    public GameObject cornerRtPrefab;
    [FormerlySerializedAs("belowStraightPrefab")] public GameObject straightHPrefab;
    [FormerlySerializedAs("aboveStraightPrefab")] public GameObject straightVPrefab;

    [Header("Legacy Prefabs (auto-migrate)")]
    [SerializeField, FormerlySerializedAs("outerCornerPrefab")] private GameObject legacyOuterCornerPrefab;
    [SerializeField, FormerlySerializedAs("innerCornerPrefab")] private GameObject legacyInnerCornerPrefab;

    [Header("Hizalama Ayarları")]
    public int tileSize = 110;
    public float borderOutside = 15f;
    public Vector2 contentOffset = new Vector2(8f, -8f);

    [Header("Sizing")]
    [Tooltip("true ise prefab/sprite oranından border kalınlığını otomatik türetir.")]
    public bool usePrefabRatioSizing = true;
    [Tooltip("Otomatik oran kapalıysa düz çizgi kalınlığı")]
    public float thickness = 20f;
    [Tooltip("Segment birleşimlerinde ufak bindirme")]
    public float joinOverlap = 0f;

    [Header("Debug")]
    public bool debugMasks = false;
    public Font debugFont;

    [Header("Optional: Treat obstacles as solid")]
    public bool includeObstaclesAsSolid = true;

    public void SetLevelData(LevelData value)
    {
        level = value;
    }

    private void Awake()
    {
        EnsurePrefabFallbacks();
    }

    private void OnValidate()
    {
        EnsurePrefabFallbacks();
    }

    private void EnsurePrefabFallbacks()
    {
        if (legacyOuterCornerPrefab != null)
        {
            if (cornerLbPrefab == null) cornerLbPrefab = legacyOuterCornerPrefab;
            if (cornerLtPrefab == null) cornerLtPrefab = legacyOuterCornerPrefab;
            if (cornerRbPrefab == null) cornerRbPrefab = legacyOuterCornerPrefab;
            if (cornerRtPrefab == null) cornerRtPrefab = legacyOuterCornerPrefab;
        }

        if (cornerLbPrefab != null)
        {
            if (cornerLtPrefab == null) cornerLtPrefab = cornerLbPrefab;
            if (cornerRbPrefab == null) cornerRbPrefab = cornerLbPrefab;
            if (cornerRtPrefab == null) cornerRtPrefab = cornerLbPrefab;
        }

        if (straightVPrefab == null) straightVPrefab = straightHPrefab;

        // legacyInnerCornerPrefab intentionally kept for serialized data migration.
    }

    public void Draw(bool[] blocked = null)
    {
        if (level == null || borderRoot == null) return;
        ClearChildren();

        int w = level.width;
        int h = level.height;

        float edgeThickness = GetEdgeThickness();
        float cornerSize = edgeThickness * 2f;
        float edgeOffset = borderOutside + edgeThickness * 0.5f;

        DrawHorizontalEdge(blocked, w, h, true, edgeOffset, edgeThickness);
        DrawHorizontalEdge(blocked, w, h, false, edgeOffset, edgeThickness);
        DrawVerticalEdge(blocked, w, h, true, edgeOffset, edgeThickness);
        DrawVerticalEdge(blocked, w, h, false, edgeOffset, edgeThickness);
        DrawConvexCorners(blocked, w, h, edgeOffset, cornerSize);
    }

    private float GetEdgeThickness()
    {
        if (!usePrefabRatioSizing)
            return Mathf.Max(1f, thickness);

        Vector2 hSize = GetPrefabSize(straightHPrefab, new Vector2(64f, 32f));
        if (hSize.x <= 0.001f || hSize.y <= 0.001f)
            return Mathf.Max(1f, thickness);

        float ratio = hSize.y / hSize.x;
        return Mathf.Max(1f, tileSize * ratio);
    }

    private Vector2 GetPrefabSize(GameObject prefab, Vector2 fallback)
    {
        if (prefab == null) return fallback;

        var rt = prefab.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 d = rt.sizeDelta;
            if (d.x > 0.001f && d.y > 0.001f) return d;
        }

        Image img;
        if (prefab.TryGetComponent<Image>(out img) && img.sprite != null)
        {
            Rect sr = img.sprite.rect;
            if (sr.width > 0.001f && sr.height > 0.001f)
                return new Vector2(sr.width, sr.height);
        }

        return fallback;
    }

    private void DrawHorizontalEdge(bool[] blocked, int w, int h, bool isTop, float edgeOffset, float edgeThickness)
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsSolid(x, y, blocked)) continue;

            bool hasNeighbor = isTop ? IsSolid(x, y - 1, blocked) : IsSolid(x, y + 1, blocked);
            if (hasNeighbor) continue;

            Vector2 cell = GetCellCenter(x, y);
            float halfTile = tileSize * 0.5f;
            float yPos = isTop ? cell.y + halfTile + edgeOffset : cell.y - halfTile - edgeOffset;
            Spawn(straightHPrefab, new Vector2(cell.x, yPos), new Vector2(tileSize + joinOverlap * 2f, edgeThickness));
        }
    }

    private void DrawVerticalEdge(bool[] blocked, int w, int h, bool isLeft, float edgeOffset, float edgeThickness)
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsSolid(x, y, blocked)) continue;

            bool hasNeighbor = isLeft ? IsSolid(x - 1, y, blocked) : IsSolid(x + 1, y, blocked);
            if (hasNeighbor) continue;

            Vector2 cell = GetCellCenter(x, y);
            float halfTile = tileSize * 0.5f;
            float xPos = isLeft ? cell.x - halfTile - edgeOffset : cell.x + halfTile + edgeOffset;
            Spawn(straightVPrefab, new Vector2(xPos, cell.y), new Vector2(edgeThickness, tileSize + joinOverlap * 2f));
        }
    }

    private void DrawConvexCorners(bool[] blocked, int w, int h, float edgeOffset, float cornerSize)
    {
        for (int y = 0; y <= h; y++)
        for (int x = 0; x <= w; x++)
        {
            int mask = GetBitmask(x, y, blocked);
            if (mask == 0 || mask == 15) continue;

            bool hasBL = (mask & 1) != 0;
            bool hasBR = (mask & 2) != 0;
            bool hasTR = (mask & 4) != 0;
            bool hasTL = (mask & 8) != 0;

            int solidCount = (hasBL ? 1 : 0) + (hasBR ? 1 : 0) + (hasTR ? 1 : 0) + (hasTL ? 1 : 0);
            if (solidCount != 1)
            {
                if (debugMasks) SpawnMaskLabel(GetNodePosition(x, y), mask);
                continue;
            }

            Vector2 node = GetNodePosition(x, y);
            float half = cornerSize * 0.5f;

            if (hasBL) Spawn(cornerLbPrefab, node + new Vector2(-edgeOffset - half, +edgeOffset + half), new Vector2(cornerSize, cornerSize));
            else if (hasBR) Spawn(cornerRbPrefab, node + new Vector2(+edgeOffset + half, +edgeOffset + half), new Vector2(cornerSize, cornerSize));
            else if (hasTR) Spawn(cornerRtPrefab, node + new Vector2(+edgeOffset + half, -edgeOffset - half), new Vector2(cornerSize, cornerSize));
            else Spawn(cornerLtPrefab, node + new Vector2(-edgeOffset - half, -edgeOffset - half), new Vector2(cornerSize, cornerSize));

            if (debugMasks) SpawnMaskLabel(node, mask);
        }
    }

    private int GetBitmask(int x, int y, bool[] blocked)
    {
        int mask = 0;
        if (IsSolid(x - 1, y - 1, blocked)) mask |= 1;
        if (IsSolid(x, y - 1, blocked)) mask |= 2;
        if (IsSolid(x, y, blocked)) mask |= 4;
        if (IsSolid(x - 1, y, blocked)) mask |= 8;
        return mask;
    }

    private bool IsSolid(int x, int y, bool[] blocked)
    {
        if (!level.InBounds(x, y)) return false;
        int idx = level.Index(x, y);
        bool isObs = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];
        if (includeObstaclesAsSolid && isObs) return true;
        if (!includeObstaclesAsSolid && isObs) return false;
        if (level.cells != null && idx >= 0 && idx < level.cells.Length && level.cells[idx] == (int)CellType.Empty) return false;
        return true;
    }

    private Vector2 GetCellCenter(int x, int y) => new Vector2(
        x * tileSize + contentOffset.x + tileSize / 2f,
        -y * tileSize + contentOffset.y - tileSize / 2f);

    private Vector2 GetNodePosition(int x, int y) => new Vector2(
        x * tileSize + contentOffset.x,
        -y * tileSize + contentOffset.y);

    private void Spawn(GameObject prefab, Vector2 pos, Vector2 size)
    {
        if (prefab == null) return;

        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        Image img;
        if (go.TryGetComponent<Image>(out img))
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
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
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
