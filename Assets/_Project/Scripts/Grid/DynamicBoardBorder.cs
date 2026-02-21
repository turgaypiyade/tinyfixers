using UnityEngine;
using UnityEngine.UI;

public class DynamicBoardBorder : MonoBehaviour
{
    [Header("Dependencies")]
    public LevelData level;
    public RectTransform borderRoot;

    [Header("Prefabs (UI Image)")]
    public GameObject straightPrefab;
    public GameObject outerCornerPrefab;
    public GameObject innerCornerPrefab;

    [Header("Hizalama Ayarları")]
    public int tileSize = 110;

    [Header("Debug")]
    public bool debugMasks = false;
    public Font debugFont;

    [Header("Optional: Treat obstacles as solid")]
    public bool includeObstaclesAsSolid = true;

    [Tooltip("Çizgi kalınlığı (px)")]
    public float thickness = 20f;

    [Tooltip("Çizgiyi hücre dışına kaç px taşıyalım")]
    public float borderOutside = 15f;

    [Tooltip("BoardContent'e göre global offset (anchoredPosition)")]
    public Vector2 contentOffset = new Vector2(8f, -8f);

    [Header("Birleşim (Gap / Padding Fix)")]
    [Tooltip("Köşe tarafında çizgi bindirmesi (px). Sadece köşe varsa uygulanır.")]
    public float joinOverlap = 10f;

    [Tooltip("Köşe sprite'larında transparan padding varsa büyütür.")]
    public float cornerScale = 1.20f;


    public void SetLevelData(LevelData value)
    {
        level = value;
    }

    public void Draw(bool[] blocked = null)
    {
        if (level == null || borderRoot == null) return;
        ClearChildren();

        int w = level.width;
        int h = level.height;

        float edgeDistFromCellCenter = (tileSize / 2f) + borderOutside + (thickness * 0.5f);
        float straightLen = tileSize;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!IsSolid(x, y, blocked)) continue;

                Vector2 cellPos = GetCellCenter(x, y);

                if (!IsSolid(x, y - 1, blocked))
                {
                    bool extendLeft =
                        (!IsSolid(x - 1, y - 1, blocked)) && IsSolid(x - 1, y, blocked);

                    bool extendRight =
                        (!IsSolid(x + 1, y - 1, blocked)) && IsSolid(x + 1, y, blocked);

                    SpawnStraightOneSided(
                        centerPos: cellPos + new Vector2(0, edgeDistFromCellCenter),
                        horizontal: true,
                        length: straightLen,
                        thick: thickness,
                        extendNeg: extendLeft,
                        extendPos: extendRight,
                        overlap: joinOverlap
                    );
                }

                if (!IsSolid(x, y + 1, blocked))
                {
                    bool extendLeft =
                        (!IsSolid(x - 1, y + 1, blocked)) && IsSolid(x - 1, y, blocked);

                    bool extendRight =
                        (!IsSolid(x + 1, y + 1, blocked)) && IsSolid(x + 1, y, blocked);

                    SpawnStraightOneSided(
                        centerPos: cellPos + new Vector2(0, -edgeDistFromCellCenter),
                        horizontal: true,
                        length: straightLen,
                        thick: thickness,
                        extendNeg: extendLeft,
                        extendPos: extendRight,
                        overlap: joinOverlap
                    );
                }

                if (!IsSolid(x - 1, y, blocked))
                {
                    bool extendUp =
                        (!IsSolid(x - 1, y - 1, blocked)) && IsSolid(x, y - 1, blocked);

                    bool extendDown =
                        (!IsSolid(x - 1, y + 1, blocked)) && IsSolid(x, y + 1, blocked);

                    SpawnStraightOneSided(
                        centerPos: cellPos + new Vector2(-edgeDistFromCellCenter, 0),
                        horizontal: false,
                        length: straightLen,
                        thick: thickness,
                        extendNeg: extendUp,
                        extendPos: extendDown,
                        overlap: joinOverlap
                    );
                }

                if (!IsSolid(x + 1, y, blocked))
                {
                    bool extendUp =
                        (!IsSolid(x + 1, y - 1, blocked)) && IsSolid(x, y - 1, blocked);

                    bool extendDown =
                        (!IsSolid(x + 1, y + 1, blocked)) && IsSolid(x, y + 1, blocked);

                    SpawnStraightOneSided(
                        centerPos: cellPos + new Vector2(edgeDistFromCellCenter, 0),
                        horizontal: false,
                        length: straightLen,
                        thick: thickness,
                        extendNeg: extendUp,
                        extendPos: extendDown,
                        overlap: joinOverlap
                    );
                }
            }
        }

        for (int y = 0; y <= h; y++)
        {
            for (int x = 0; x <= w; x++)
            {
                int mask = GetBitmask(x, y, blocked);
                if (mask > 0 && mask < 15)
                    HandleCorner(GetNodePosition(x, y), mask);
            }
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
        t.font = debugFont != null 
            ? debugFont 
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18;
        t.color = Color.magenta;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
    }
    private void SpawnStraightOneSided(
        Vector2 centerPos,
        bool horizontal,
        float length,
        float thick,
        bool extendNeg,
        bool extendPos,
        float overlap
    )
    {
        float extraNeg = extendNeg ? overlap : 0f;
        float extraPos = extendPos ? overlap : 0f;

        float totalLen = length + extraNeg + extraPos;

        // ✅ KÖŞE VARSA STRAIGHT BİRAZ KISALSIN (3-12-6-9 hepsi)
        // Bu değerleri küçük başlat:
        const float trimOneSide = 5f;  // tek uçta köşe birleşimi varsa
        const float trimTwoSide = 10f;  // iki uçta da köşe birleşimi varsa

        if (extendNeg && extendPos)
            totalLen -= trimTwoSide;
        else if (extendNeg || extendPos)
            totalLen -= trimOneSide;

        // Negatif/çok küçük olmasın
        totalLen = Mathf.Max(10f, totalLen);

        // Shift, uçlara eklenen overlap'e göre çizgiyi ortalar
        float shift = (extraPos - extraNeg) * 0.5f;

        Vector2 size = new Vector2(totalLen, thick);

        if (horizontal)
        {
            Spawn(straightPrefab, centerPos + new Vector2(shift, 0), 0, size);
        }
        else
        {
            Spawn(straightPrefab, centerPos + new Vector2(0, -shift), 90, size);
        }
    }


    private void HandleCorner(Vector2 nodePos, int mask)
    {

        if (debugMasks)
        {
            SpawnMaskLabel(nodePos, mask);
        }

        float baseOff = borderOutside + (thickness * 0.5f);

        // ✅ aynı sprite kullanırken concave/convex farkını offset ile çözüyoruz
        float k = 0.70f; // 0.6-0.8 arası genelde en iyi
        float offOuter = Mathf.Max(0f, baseOff - joinOverlap * k); // outer dışarı gelsin
        float offInner = baseOff + joinOverlap * k;               // inner içeri gelsin

        float cornerSize = (thickness + joinOverlap * 2f) * cornerScale;
        Vector2 sz = new Vector2(cornerSize, cornerSize);

        switch (mask)
        {
            // OUTER
            case 1:
                Spawn(outerCornerPrefab, nodePos + new Vector2(-offOuter, +offOuter), 0, sz, flipX: true, flipY: true);
                break;
            case 2:
                Spawn(outerCornerPrefab, nodePos + new Vector2(+offOuter, +offOuter), 0, sz, flipX: false, flipY: true);
                break;
            case 4:
                Spawn(outerCornerPrefab, nodePos + new Vector2(+offOuter, -offOuter), 0, sz, flipX: false, flipY: false);
                break;
            case 8:
                Spawn(outerCornerPrefab, nodePos + new Vector2(-offOuter, -offOuter), 0, sz, flipX: true, flipY: false);
                break;

            // INNER
            case 14:
                Spawn(innerCornerPrefab,
                    nodePos + new Vector2(-offInner -5f, +offInner + 5f),
                    0, sz, flipX: true, flipY: true);
                break;
            case 13: // ┐
                Spawn(innerCornerPrefab,
                    nodePos + new Vector2(+offInner + 5f, +offInner + 5f),
                    0, sz, flipX: false, flipY: true);
                break;

            case 11: // ┘
                UnityEngine.Debug.Log("Mask 11 offInner: " + offInner);
                Spawn(innerCornerPrefab,
                    nodePos + new Vector2(+offInner + 5f, -offInner - 5f),
                    0, sz, flipX: false, flipY: false);
                break;
            case 7:
                Spawn(innerCornerPrefab,
                    nodePos + new Vector2(-offInner-5f , -offInner -5f),
                    0, sz, flipX: true, flipY: false);
                break;
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

        bool isBlockedByObstacle = blocked != null && idx >= 0 && idx < blocked.Length && blocked[idx];

        // BlocksCells obstacle'ları seviye datasında Empty hücreye çeviriyor.
        // includeObstaclesAsSolid = true iken bu hücreleri solid sayalım ki
        // obstacle etrafına ayrıca çerçeve oluşmasın.
        if (includeObstaclesAsSolid && isBlockedByObstacle)
            return true;

        if (!includeObstaclesAsSolid && isBlockedByObstacle)
            return false;

        if (level.cells != null && idx >= 0 && idx < level.cells.Length &&
            level.cells[idx] == (int)CellType.Empty)
            return false;

        return true;
    }

    private Vector2 GetCellCenter(int x, int y)
    {
        return new Vector2(
            x * tileSize + contentOffset.x + tileSize / 2f,
            -y * tileSize + contentOffset.y - tileSize / 2f
        );
    }

    private Vector2 GetNodePosition(int x, int y)
    {
        return new Vector2(
            x * tileSize + contentOffset.x,
            -y * tileSize + contentOffset.y
        );
    }

    private void Spawn(GameObject prefab, Vector2 pos, float rot, Vector2 size, bool flipX = false, bool flipY = false)
    {
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

    private void ClearChildren()
    {
        if (borderRoot == null) return;

        for (int i = borderRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(borderRoot.GetChild(i).gameObject);
            else DestroyImmediate(borderRoot.GetChild(i).gameObject);
        }
    }
}
