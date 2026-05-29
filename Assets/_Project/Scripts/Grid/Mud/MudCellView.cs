using UnityEngine;
using UnityEngine.UI;

/// One UI cell that samples its own region out of a single shared mud texture.
/// No per-cell texture clamping → adjacent cells look like one continuous mud surface.
public class MudCellView : MonoBehaviour
{
    private RawImage rawImage;
    private RectTransform rt;

    private int gridX;
    private int gridY;
    private int gridWidth;
    private int gridHeight;

    private Color darkTint = new Color(0.35f, 0.25f, 0.18f, 1f);
    private Color lightTint = new Color(0.65f, 0.50f, 0.35f, 1f);
    private float minAlphaAtLastStage = 0.7f;

    // Border edges — only outer edges of the mud region are shown
    private Image edgeTop, edgeRight, edgeBottom, edgeLeft;

    public int GridX => gridX;
    public int GridY => gridY;

    public void Init(
        Texture sharedMudTexture,
        int x, int y,
        int gridW, int gridH,
        Color dark, Color light,
        float minAlpha)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rawImage = GetComponent<RawImage>();
        if (rawImage == null) rawImage = gameObject.AddComponent<RawImage>();

        rawImage.raycastTarget = false;
        rawImage.texture = sharedMudTexture;

        gridX = x; gridY = y;
        gridWidth  = Mathf.Max(1, gridW);
        gridHeight = Mathf.Max(1, gridH);
        darkTint   = dark;
        lightTint  = light;
        minAlphaAtLastStage = Mathf.Clamp01(minAlpha);

        float uvW = 1f / gridWidth;
        float uvH = 1f / gridHeight;
        rawImage.uvRect = new Rect(x * uvW, 1f - (y + 1) * uvH, uvW, uvH);
    }

    public void PlaceInCell(int tileSize)
    {
        if (rt == null) return;
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(gridX * tileSize, -gridY * tileSize);
        rt.sizeDelta        = new Vector2(tileSize, tileSize);
    }

    // ── Border API ────────────────────────────────────────────────────────────

    /// spriteH : horizontal strip (bottom=dark, top=transparent) — top/bottom edges.
    /// spriteV : vertical strip   (left=dark, right=transparent) — left/right edges.
    ///           null → spriteH is used (gradient direction slightly off but position correct).
    /// outwardRatio : 0 = border entirely inside cell,
    ///                0.5 = centered on cell edge (half in / half out),
    ///                1.0 = border entirely outside cell.
    public void InitBorders(Sprite spriteH, Sprite spriteV,
                            float thicknessRatio, int tileSize, float outwardRatio)
    {
        if (spriteH == null) return;

        Sprite sv  = spriteV != null ? spriteV : spriteH;
        float  ts  = tileSize;
        float  bp  = Mathf.Round(ts * Mathf.Clamp(thicknessRatio, 0.05f, 0.45f));
        float  off = bp * Mathf.Clamp01(outwardRatio); // outward shift in pixels

        // Anchor + pivot: same top-left origin as parent
        Vector2 a = new Vector2(0f, 1f);

        // Bottom edge: dark faces down (outward). Shift down by `off`.
        edgeBottom = MakeEdge("MudEdgeBottom", spriteH, a,
            new Vector2(0f,      -(ts - bp + off)),
            new Vector2(ts, bp),
            Vector3.one);

        // Top edge: flip Y so dark faces up (outward). Shift up by `off`.
        edgeTop = MakeEdge("MudEdgeTop", spriteH, a,
            new Vector2(0f,      off - bp),
            new Vector2(ts, bp),
            new Vector3(1f, -1f, 1f));

        // Left edge: dark faces left (outward). Shift left by `off`.
        edgeLeft = MakeEdge("MudEdgeLeft", sv, a,
            new Vector2(-off,    0f),
            new Vector2(bp, ts),
            Vector3.one);

        // Right edge: scale=(-1,1,1) → strip renders LEFTWARD from anchoredPosition.x.
        // To center on boundary (x=ts): anchoredPosition.x = ts + off
        // → strip covers (ts+off-bp) to (ts+off), centered at ts when off=bp/2.
        edgeRight = MakeEdge("MudEdgeRight", sv, a,
            new Vector2(ts + off, 0f),
            new Vector2(bp, ts),
            new Vector3(-1f, 1f, 1f));

        SetBorders(false, false, false, false);
    }

    public void SetBorders(bool top, bool right, bool bottom, bool left)
    {
        if (edgeTop)    edgeTop.gameObject.SetActive(top);
        if (edgeRight)  edgeRight.gameObject.SetActive(right);
        if (edgeBottom) edgeBottom.gameObject.SetActive(bottom);
        if (edgeLeft)   edgeLeft.gameObject.SetActive(left);
    }

    private Image MakeEdge(string name, Sprite sprite,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, Vector3 localScale)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var ert = go.GetComponent<RectTransform>();
        ert.anchorMin        = anchor;
        ert.anchorMax        = anchor;
        ert.pivot            = new Vector2(0f, 1f);
        ert.anchoredPosition = anchoredPos;
        ert.sizeDelta        = size;
        ert.localScale       = localScale;

        var img = go.GetComponent<Image>();
        img.sprite         = sprite;
        img.raycastTarget  = false;
        img.color          = Color.white;
        img.preserveAspect = false;
        return img;
    }

    // ── Damage / Clear ────────────────────────────────────────────────────────

    public void SetDamageLevel(int remaining, int maxHits)
    {
        if (rawImage == null) return;
        int max = Mathf.Max(1, maxHits);
        int rem = Mathf.Clamp(remaining, 0, max);

        if (rem <= 0) { rawImage.color = new Color(lightTint.r, lightTint.g, lightTint.b, 0f); return; }

        float t    = (float)rem / max;
        Color tint = Color.Lerp(lightTint, darkTint, t);
        tint.a     = Mathf.Lerp(minAlphaAtLastStage, 1f, t);
        rawImage.color = tint;
    }

    public void Clear()
    {
        if (rawImage != null) { var c = rawImage.color; c.a = 0f; rawImage.color = c; }
        SetBorders(false, false, false, false);
        gameObject.SetActive(false);
    }

    public void ApplyStage(Texture stageTexture, bool visible)
    {
        if (rawImage == null) return;
        if (!visible) { rawImage.color = new Color(1f, 1f, 1f, 0f); return; }
        if (stageTexture != null) rawImage.texture = stageTexture;
        rawImage.color = Color.white;
    }
}
