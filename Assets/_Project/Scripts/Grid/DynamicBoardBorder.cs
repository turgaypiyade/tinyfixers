using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs (UI Image)")]
    [FormerlySerializedAs("outerCornerPrefab")] public GameObject cornerLbPrefab;
    public GameObject cornerLtPrefab;
    public GameObject cornerRbPrefab;
    public GameObject cornerRtPrefab;
    [FormerlySerializedAs("belowStraightPrefab")] public GameObject straightHPrefab;
    [FormerlySerializedAs("aboveStraightPrefab")] public GameObject straightVPrefab;

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

    [Tooltip("Segmentlerin birleşiminde küçük bindirme için kullanılır")]
    public float joinOverlap = 0f;

    public void SetLevelData(LevelData value) => level = value;

    public void Draw(bool[] blocked = null)
    {
        if (level == null || borderRoot == null) return;
        ClearChildren();

        int w = level.width;
        int h = level.height;
        float edgeOffset = borderOutside + thickness * 0.5f;

        DrawHorizontalEdge(blocked, w, h, isTop: true, edgeOffset);
        DrawHorizontalEdge(blocked, w, h, isTop: false, edgeOffset);
        DrawVerticalEdge(blocked, w, h, isLeft: true, edgeOffset);
        DrawVerticalEdge(blocked, w, h, isLeft: false, edgeOffset);
        DrawCorners(blocked, w, h, edgeOffset);
    }

    private void DrawHorizontalEdge(bool[] blocked, int w, int h, bool isTop, float edgeOffset)
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

            Spawn(straightHPrefab, new Vector2(cell.x, yPos), 0f, new Vector2(tileSize + joinOverlap * 2f, thickness));
        }
    }

    private void DrawVerticalEdge(bool[] blocked, int w, int h, bool isLeft, float edgeOffset)
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

            Spawn(straightVPrefab, new Vector2(xPos, cell.y), 0f, new Vector2(thickness, tileSize + joinOverlap * 2f));
        }
    }

    private void DrawCorners(bool[] blocked, int w, int h, float edgeOffset)
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
            if (solidCount != 1 && solidCount != 3) continue;

            bool useConcaveOffset = solidCount == 3;
            Vector2 pos = GetCornerPosition(GetNodePosition(x, y), hasBL, hasBR, hasTR, hasTL, edgeOffset, useConcaveOffset);
            GameObject cornerPrefab = SelectCornerPrefab(hasBL, hasBR, hasTR, hasTL, solidCount == 1);

            Spawn(cornerPrefab, pos, 0f, new Vector2(thickness * 2f, thickness * 2f));

            if (debugMasks)
                SpawnMaskLabel(GetNodePosition(x, y), mask);
        }
    }

    private Vector2 GetCornerPosition(Vector2 node, bool hasBL, bool hasBR, bool hasTR, bool hasTL, float edgeOffset, bool concave)
    {
        float signX;
        float signY;

        if (!concave)
        {
            if (hasBL) { signX = -1f; signY = +1f; }
            else if (hasBR) { signX = +1f; signY = +1f; }
            else if (hasTR) { signX = +1f; signY = -1f; }
            else { signX = -1f; signY = -1f; }

            float half = thickness * 0.5f;
            return node + new Vector2(signX * (edgeOffset + half), signY * (edgeOffset + half));
        }

        // İç köşe (3 dolu): boş kalan çeyrek hangi taraftaysa köşeyi içeri çek.
        if (!hasBL) { signX = -1f; signY = +1f; }
        else if (!hasBR) { signX = +1f; signY = +1f; }
        else if (!hasTR) { signX = +1f; signY = -1f; }
        else { signX = -1f; signY = -1f; }

        float inner = Mathf.Max(0f, edgeOffset - thickness * 0.5f);
        return node + new Vector2(signX * inner, signY * inner);
    }

    private GameObject SelectCornerPrefab(bool hasBL, bool hasBR, bool hasTR, bool hasTL, bool convex)
    {
        if (convex)
        {
            if (hasBL) return cornerLbPrefab;
            if (hasBR) return cornerRbPrefab;
            if (hasTR) return cornerRtPrefab;
            return cornerLtPrefab;
        }

        if (!hasBL) return cornerLbPrefab;
        if (!hasBR) return cornerRbPrefab;
        if (!hasTR) return cornerRtPrefab;
        return cornerLtPrefab;
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

    private void Spawn(GameObject prefab, Vector2 pos, float rot, Vector2 size,
        bool flipX = false, bool flipY = false)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, borderRoot);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0, 0, rot);
        rt.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);
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
