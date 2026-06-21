using UnityEngine;
using UnityEngine.UI;

/// One UI cell showing bordered mud sprite (Sprite B, bevel baked on all 4 sides) as permanent base.
/// Cover strips (plain mud texture) hide the bevel toward same-stage neighbours.
/// Strip dimensions are trimmed at each call so they never cover a corner bevel
/// that belongs to an inactive (exposed) neighbouring side.
///
/// Damaged stage (stage 1+) renders as a full-cell RawImage (damageFill) whose UV is
/// the cell's slice of the grid, so the dark mud texture flows continuously across
/// neighbouring damaged cells with no per-cell tiling seam — the same seamless trick
/// the cover strips use for stage 0. If no damaged texture is supplied it falls back
/// to a per-cell sprite Image (legacy).
public class MudCellView : MonoBehaviour
{
    private Image         baseImage;
    private Image         damageOverlay;
    private RawImage      damageFill;
    private RectTransform rt;

    // Stage sprites/textures. The damaged-stage bevel sprite is optional: when present the
    // cell mirrors the stage-0 masking (bordered base + covers) for the dark stage; when
    // absent it falls back to a seamless full-cell RawImage fill (no outline bevel).
    private Sprite  undamagedSprite;       // bordered base, stage 0
    private Sprite  damagedSprite;         // bordered dark base, stage 1+ (nullable)
    private Texture coverPlainTex;         // covers for stage 0
    private Texture coverDamagedTex;       // covers for stage 1+ (nullable)
    private bool    useBevelDamage;        // damagedSprite != null

    private int   gridX, gridY, gridWidth, gridHeight;
    private int   maxHits = 1;

    // Stored cover geometry — set in InitCovers, used every SetCovers call.
    private float coverBP, coverTS;
    private float _uvX, _uvY, _uvW, _uvH, _bpUx, _bpUy;

    private RawImage coverTop, coverRight, coverBottom, coverLeft;

    public int  GridX           => gridX;
    public int  GridY           => gridY;
    public int  MaxHits         => maxHits;
    public bool IsDamaged       { get; private set; }
    public bool UsesBevelDamage => useBevelDamage;

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
        undamagedSprite          = bordered;

        gridX = x; gridY = y;
        gridWidth  = Mathf.Max(1, gridW);
        gridHeight = Mathf.Max(1, gridH);

        // This cell's slice of the grid in UV space. Shared by cover strips and the
        // damaged-stage fill so both stay continuous across neighbouring cells.
        _uvW = 1f / gridWidth;
        _uvH = 1f / gridHeight;
        _uvX = gridX * _uvW;
        _uvY = 1f - (gridY + 1) * _uvH;
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

        coverPlainTex = plainTexture;
        coverTS = tileSize;
        coverBP = Mathf.Round(tileSize * Mathf.Clamp(thicknessRatio, 0.05f, 0.45f));

        // Grid-slice UV (_uvX.._uvH) is computed in Init; only the bevel-thickness in
        // UV space depends on the cover system, so compute it here.
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

    private void SetCoverTexture(Texture texture)
    {
        if (texture == null) return;
        if (coverTop)    coverTop.texture    = texture;
        if (coverRight)  coverRight.texture  = texture;
        if (coverBottom) coverBottom.texture = texture;
        if (coverLeft)   coverLeft.texture   = texture;
    }

    // ── Damaged stage ───────────────────────────────────────────────────────────

    /// Supplies the dark-stage assets. When a bevel sprite is given the damaged stage
    /// mirrors stage-0 masking exactly: the base swaps to the dark bevel sprite and the
    /// cover strips switch to the dark texture, so the bevel shows only on the blob
    /// outline while the interior stays seamless. Must be called BEFORE InitDamageOverlay.
    public void SetDamagedVisuals(Sprite damagedBordered, Texture damagedTexture)
    {
        damagedSprite   = damagedBordered;
        coverDamagedTex = damagedTexture;
        useBevelDamage  = damagedBordered != null;
    }

    // ── Damage overlay ────────────────────────────────────────────────────────

    /// Must be called AFTER InitCovers (and SetDamagedVisuals) so the damage layer renders
    /// on top of covers. In bevel mode the dark stage is drawn by the base+cover system, so
    /// no fill layer is created. Otherwise, when a damaged texture is supplied the cell uses
    /// a full-cell RawImage whose UV is this cell's grid slice — the dark mud then flows
    /// seamlessly across damaged neighbours. Without a texture it falls back to a per-cell
    /// sprite Image (legacy).
    public void InitDamageOverlay(Texture damagedTexture = null)
    {
        if (useBevelDamage) return;

        if (damagedTexture != null)
        {
            var go = new GameObject("MudDamageFill", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(transform, false);

            var ert = go.GetComponent<RectTransform>();
            ert.anchorMin  = Vector2.zero;
            ert.anchorMax  = Vector2.one;
            ert.offsetMin  = Vector2.zero;
            ert.offsetMax  = Vector2.zero;
            ert.localScale = Vector3.one;

            damageFill = go.GetComponent<RawImage>();
            damageFill.texture       = damagedTexture;
            damageFill.raycastTarget = false;
            damageFill.color         = Color.white;
            damageFill.uvRect        = new Rect(_uvX, _uvY, _uvW, _uvH);
            damageFill.gameObject.SetActive(false);
            return;
        }

        var lego = new GameObject("MudDamageOverlay", typeof(RectTransform), typeof(Image));
        lego.transform.SetParent(transform, false);

        var lrt = lego.GetComponent<RectTransform>();
        lrt.anchorMin  = Vector2.zero;
        lrt.anchorMax  = Vector2.one;
        lrt.offsetMin  = Vector2.zero;
        lrt.offsetMax  = Vector2.zero;
        lrt.localScale = Vector3.one;

        damageOverlay = lego.GetComponent<Image>();
        damageOverlay.raycastTarget  = false;
        damageOverlay.preserveAspect = false;
        damageOverlay.gameObject.SetActive(false);
    }

    public void SetDamageState(bool damaged, Sprite librarySprite)
    {
        IsDamaged = damaged;

        // Bevel mode: the dark stage is the base+cover system; ApplyStageVisuals handles it.
        if (useBevelDamage) { ApplyStageVisuals(damaged); return; }

        if (damageFill != null)
        {
            damageFill.gameObject.SetActive(damaged);
            return;
        }

        if (damageOverlay == null) return;
        damageOverlay.sprite = librarySprite;
        damageOverlay.gameObject.SetActive(damaged && librarySprite != null);
    }

    /// Swaps the base sprite and cover texture to match the current stage (bevel mode only).
    private void ApplyStageVisuals(bool damaged)
    {
        if (!useBevelDamage) return;
        baseImage.sprite = damaged ? damagedSprite : undamagedSprite;
        SetCoverTexture(damaged ? coverDamagedTex : coverPlainTex);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetDamageLevel(int remaining, int maxHits)
    {
        bool visible = remaining > 0;
        SetBaseAlpha(visible ? 1f : 0f);
        if (!visible)
        {
            if (damageOverlay != null) damageOverlay.gameObject.SetActive(false);
            if (damageFill    != null) damageFill.gameObject.SetActive(false);
        }
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
        if (damageFill    != null) damageFill.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}
