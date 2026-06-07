using UnityEngine;
using UnityEngine.UI;

/// One UI cell showing bordered mud sprite (Sprite B, bevel baked on all 4 sides) as permanent base.
/// Cover strips (plain mud texture) hide the bevel toward same-stage neighbours.
/// Strip dimensions are trimmed at each call so they never cover a corner bevel
/// that belongs to an inactive (exposed) neighbouring side.
/// A full-cell damage overlay Image shows the library sprite for stage 1+.
public class MudCellView : MonoBehaviour
{
    private Image         baseImage;
    private Image         damageOverlay;
    private RectTransform rt;

    private int   gridX, gridY, gridWidth, gridHeight;
    private int   maxHits = 1;

    // Stored cover geometry — set in InitCovers, used every SetCovers call.
    private float coverBP, coverTS;
    private float _uvX, _uvY, _uvW, _uvH, _bpUx, _bpUy;

    private RawImage coverTop, coverRight, coverBottom, coverLeft;

    public int  GridX     => gridX;
    public int  GridY     => gridY;
    public int  MaxHits   => maxHits;
    public bool IsDamaged { get; private set; }

    // ── Init ─────────────────────────────────────────────────────────────────

    public void Init(Sprite bordered, int x, int y, int gridW, int gridH)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        baseImage = GetComponent<Image>();
        if (baseImage == null) baseImage = gameObject.AddComponent<Image>();

        baseImage.raycastTarget  = false;
        baseImage.preserveAspect = false;
        baseImage.sprite         = bordered;

        gridX = x; gridY = y;
        gridWidth  = Mathf.Max(1, gridW);
        gridHeight = Mathf.Max(1, gridH);
    }

    public void SetMaxHits(int max) => maxHits = Mathf.Max(1, max);

    public void PlaceInCell(int tileSize)
    {
        if (rt == null) return;
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(gridX * tileSize, -gridY * tileSize);
        rt.sizeDelta        = new Vector2(tileSize, tileSize);
    }

    // ── Cover strips ──────────────────────────────────────────────────────────

    public void InitCovers(Texture plainTexture, float thicknessRatio, int tileSize)
    {
        if (plainTexture == null) return;

        coverTS = tileSize;
        coverBP = Mathf.Round(tileSize * Mathf.Clamp(thicknessRatio, 0.05f, 0.45f));

        _uvW  = 1f / gridWidth;
        _uvH  = 1f / gridHeight;
        _uvX  = gridX * _uvW;
        _uvY  = 1f - (gridY + 1) * _uvH;
        _bpUx = coverBP / coverTS * _uvW;
        _bpUy = coverBP / coverTS * _uvH;

        Vector2 anc = new Vector2(0f, 1f);
        coverTop    = MakeCover("MudCoverTop",    plainTexture, anc);
        coverBottom = MakeCover("MudCoverBottom", plainTexture, anc);
        coverLeft   = MakeCover("MudCoverLeft",   plainTexture, anc);
        coverRight  = MakeCover("MudCoverRight",  plainTexture, anc);

        SetCovers(false, false, false, false);
    }

    /// Shows/hides each cover strip and trims its size so it never covers the
    /// corner bevel area of an inactive (exposed) neighbouring side.
    ///
    /// TOP/BOTTOM strips: trimmed left by bp if left=false, trimmed right if right=false.
    /// LEFT/RIGHT strips: trimmed top  by bp if top=false,  trimmed bottom if bottom=false.
    public void SetCovers(bool top, bool right, bool bottom, bool left)
    {
        float bp = coverBP;
        float ts = coverTS;

        // Horizontal offsets for top/bottom strips
        float hX = left  ? 0f : bp;
        float hW = ts - (left ? 0f : bp) - (right ? 0f : bp);
        float hUx = left  ? _uvX       : _uvX + _bpUx;
        float hUw = _uvW - (left ? 0f : _bpUx) - (right ? 0f : _bpUx);

        // Vertical offsets for left/right strips
        float vY = top    ? 0f  : -bp;
        float vH = ts - (top ? 0f : bp) - (bottom ? 0f : bp);
        float vUy = bottom ? _uvY      : _uvY + _bpUy;
        float vUh = _uvH - (top ? 0f : _bpUy) - (bottom ? 0f : _bpUy);

        ApplyCover(coverTop,    top,    hX,         0f,        hW, bp, hUx,            _uvY + _uvH - _bpUy, hUw, _bpUy);
        ApplyCover(coverBottom, bottom, hX,         -(ts-bp),  hW, bp, hUx,            _uvY,                hUw, _bpUy);
        ApplyCover(coverLeft,   left,   0f,         vY,        bp, vH, _uvX,           vUy,                 _bpUx, vUh);
        ApplyCover(coverRight,  right,  ts - bp,    vY,        bp, vH, _uvX+_uvW-_bpUx, vUy,                _bpUx, vUh);
    }

    private void ApplyCover(RawImage cover, bool active,
        float px, float py, float sw, float sh,
        float uRx, float uRy, float uRw, float uRh)
    {
        if (cover == null) return;
        cover.gameObject.SetActive(active);
        if (!active) return;
        var crt = cover.rectTransform;
        crt.anchoredPosition = new Vector2(px, py);
        crt.sizeDelta        = new Vector2(sw, sh);
        cover.uvRect         = new Rect(uRx, uRy, uRw, uRh);
    }

    private RawImage MakeCover(string name, Texture texture, Vector2 anchor)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);

        var ert = go.GetComponent<RectTransform>();
        ert.anchorMin = anchor;
        ert.anchorMax = anchor;
        ert.pivot     = new Vector2(0f, 1f);

        var ri = go.GetComponent<RawImage>();
        ri.texture       = texture;
        ri.raycastTarget = false;
        ri.color         = Color.white;
        return ri;
    }

    // ── Damage overlay ────────────────────────────────────────────────────────

    /// Must be called AFTER InitCovers so the overlay renders on top of covers.
    public void InitDamageOverlay()
    {
        var go = new GameObject("MudDamageOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var ert = go.GetComponent<RectTransform>();
        ert.anchorMin  = Vector2.zero;
        ert.anchorMax  = Vector2.one;
        ert.offsetMin  = Vector2.zero;
        ert.offsetMax  = Vector2.zero;
        ert.localScale = Vector3.one;

        damageOverlay = go.GetComponent<Image>();
        damageOverlay.raycastTarget  = false;
        damageOverlay.preserveAspect = false;
        damageOverlay.gameObject.SetActive(false);
    }

    public void SetDamageState(bool damaged, Sprite librarySprite)
    {
        IsDamaged = damaged;
        if (damageOverlay == null) return;
        damageOverlay.sprite = librarySprite;
        damageOverlay.gameObject.SetActive(damaged && librarySprite != null);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetDamageLevel(int remaining, int maxHits)
    {
        bool visible = remaining > 0;
        SetBaseAlpha(visible ? 1f : 0f);
        if (!visible && damageOverlay != null)
            damageOverlay.gameObject.SetActive(false);
    }

    private void SetBaseAlpha(float alpha)
    {
        if (baseImage != null) { var c = baseImage.color; c.a = alpha; baseImage.color = c; }
        void A(RawImage ri) { if (ri) { var c = ri.color; c.a = alpha; ri.color = c; } }
        A(coverTop); A(coverRight); A(coverBottom); A(coverLeft);
    }

    public void Clear()
    {
        SetBaseAlpha(0f);
        SetCovers(false, false, false, false);
        IsDamaged = false;
        if (damageOverlay != null) damageOverlay.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}
