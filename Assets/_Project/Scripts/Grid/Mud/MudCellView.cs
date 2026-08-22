using UnityEngine;
using UnityEngine.UI;

/// Sprite-underlay mud cell.
///
/// The ORIGINAL beveled mud sprite is drawn once, full-cell, underneath everything. That gives the
/// exposed edges and the rounded convex corners straight from the authored art — nothing is
/// reconstructed, so an isolated cell is literally the original sprite. A seamless interior fill
/// (board-slice UV) is layered on top: it stays inset by the bevel width on EXPOSED sides (so the
/// sprite's bevel shows) but extends to the cell edge on NEIGHBOUR sides (so the join is flat and
/// seamless). Finally, where a run continues straight (one side exposed, the perpendicular side has
/// a neighbour) a small flat strip squares off the sprite's rounded corner so two joined cells meet
/// in a straight line instead of a scallop.
///
/// Because the opaque sprite is always underneath, the board colour can never leak at a join — the
/// whole "ince boşluk / under-bevel" class of seams is gone. Two stages (light stage-0 / dark
/// stage-1+) swap the sprite + interior textures; the exposure layout is stage-independent so a hit
/// only recolours the cell.
public class MudCellView : MonoBehaviour
{
    private RectTransform rt;

    private RawImage baseSprite;                                 // full-cell original beveled sprite (underlay)
    private RawImage interior;                                   // seamless fill, inset only on exposed sides
    private RawImage cTL, cTR, cBL, cBR;                         // straight-run corner squaring strips

    // Stage assets.
    private Texture interiorTex0, interiorTex1;                  // plain fill (light / dark)
    private Texture bevelTex0, bevelTex1;                        // beveled sprite textures
    private Rect    bevelUV0, bevelUV1;                          // beveled sprite rect in its texture (UV 0..1)
    private bool    flatInterior0, flatInterior1;
    private Color   flatInteriorColor0 = Color.white;
    private Color   flatInteriorColor1 = Color.white;
    private Vector2 interiorOffset0, interiorOffset1;

    private int   gridX, gridY, gridWidth, gridHeight;
    private int   maxHits = 1;
    private float ts, bp;                                        // tile size, bevel width in screen px
    private float sourceT, sourceCornerT, edgeAlongInset;       // sprite border UV, corner UV, flat-edge crop
    private float interiorBleed;                                 // px the fill bleeds into neighbour mud
    private float cornerJoinExtend;                              // px a squaring strip overlaps into its neighbour to close the join break
    private float uvX, uvY, uvW, uvH;                            // interior board-slice UV

    private bool damaged;
    private bool eT, eR, eB, eL;                                 // current exposure

    public int  GridX     => gridX;
    public int  GridY     => gridY;
    public int  MaxHits   => maxHits;
    public bool IsDamaged => damaged;

    // ── Init ─────────────────────────────────────────────────────────────────

    public void Init(Sprite bevelSprite0, Texture interiorTexture0, int x, int y, int gridW, int gridH)
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        gridX = x; gridY = y;
        gridWidth  = Mathf.Max(1, gridW);
        gridHeight = Mathf.Max(1, gridH);

        interiorTex0 = interiorTexture0;
        bevelTex0    = bevelSprite0 != null ? bevelSprite0.texture : null;
        bevelUV0     = SpriteUV(bevelSprite0);

        // This cell's slice of the grid in UV space → interior fill stays continuous across cells.
        uvW = 1f / gridWidth;
        uvH = 1f / gridHeight;
        uvX = gridX * uvW;
        uvY = 1f - (gridY + 1) * uvH;
    }

    public void SetStage0InteriorStyle(bool useFlatInterior, Color flatColor, Vector2 offsetPixels)
    {
        flatInterior0 = useFlatInterior;
        flatInteriorColor0 = flatColor;
        interiorOffset0 = offsetPixels;
    }

    public void SetStageAssets(Sprite bevelSprite1, Texture interiorTexture1, bool useFlatInterior1, Color flatColor1, Vector2 offsetPixels1)
    {
        interiorTex1 = interiorTexture1 != null ? interiorTexture1 : interiorTex0;
        bevelTex1    = bevelSprite1 != null ? bevelSprite1.texture : bevelTex0;
        bevelUV1     = bevelSprite1 != null ? SpriteUV(bevelSprite1) : bevelUV0;
        flatInterior1 = useFlatInterior1;
        flatInteriorColor1 = flatColor1;
        interiorOffset1 = offsetPixels1;
    }

    public void SetMaxHits(int max) => maxHits = Mathf.Max(1, max);

    /// Creates the child RawImages and caches geometry. Call after Init/SetStageAssets.
    /// Most of the legacy tuning params are kept only for call-site compatibility — the sprite
    /// underlay makes them unnecessary. The bevel width now tracks the sprite's own border
    /// (sourceBorderPixels) so the interior meets the real bevel exactly, no stretching.
    public void Build(
        int tileSize,
        float thicknessRatio,
        float edgeOverlapPixels = 1.5f,
        float interiorBleedPixels = 2f,
        float underBevelFillRatio = 1f,
        float cornerJoinPixels = 1f,
        float edgeJoinExtendPixels = 2f,
        float edgeStraightCropPixels = 8f,
        float sourceTextureSizePixels = 990f,
        float sourceBorderPixels = 80f,
        float sourceCornerPixels = 101f)
    {
        ts = tileSize;
        float sourceSize = Mathf.Max(1f, sourceTextureSizePixels);
        // The authored mud sprites are 990x990 with about 99 px of source border. Sample the bevel
        // at its NATIVE width so the underlay bevel, the interior inset and the corner strips all
        // line up 1:1 with the original art (no stretch → no distorted round).
        sourceT = Mathf.Clamp(sourceBorderPixels / sourceSize, 0.01f, 0.45f);
        sourceCornerT = Mathf.Clamp(sourceCornerPixels / sourceSize, sourceT, 0.45f);
        // Straight edges/strips crop a few px DEEPER than the corner radius so the sampled slice is
        // fully flat — otherwise a residual "oval" of the rounded corner shows at a cell join.
        edgeAlongInset = Mathf.Clamp(sourceCornerT + Mathf.Max(0f, edgeStraightCropPixels) / sourceSize, sourceCornerT, 0.45f);
        bp = Mathf.Round(ts * sourceT);
        interiorBleed = Mathf.Max(0f, interiorBleedPixels);
        // Both-direction join overlap. Floor it to half a bevel width so the boundary gets a full
        // bevel-width of overlap even if the inspector value is tiny — that reliably kills the join
        // break. Raise Edge Join Extend Pixels for more.
        cornerJoinExtend = Mathf.Max(edgeJoinExtendPixels, bp * 0.5f);

        // The GameObject may carry a stray base Image (legacy creation); this view renders via
        // its own RawImage children, so silence the parent Image to avoid a white quad behind.
        var baseImg = GetComponent<Image>();
        if (baseImg != null) baseImg.enabled = false;

        baseSprite = MakeChild("MudBaseSprite");
        interior   = MakeChild("MudInterior");
        cTL        = MakeChild("MudCornerTL");
        cTR        = MakeChild("MudCornerTR");
        cBL        = MakeChild("MudCornerBL");
        cBR        = MakeChild("MudCornerBR");

        // Underlay covers the whole cell; UV set per-stage in SetExposed.
        var brt = baseSprite.rectTransform;
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta        = new Vector2(ts, ts);

        // Interior layout is exposure-dependent; SetExposed positions it below.
        var irt = interior.rectTransform;
        irt.anchorMin = new Vector2(0, 1);
        irt.anchorMax = new Vector2(0, 1);
        irt.pivot     = new Vector2(0, 1);
        interior.uvRect = new Rect(uvX, uvY, uvW, uvH);

        ApplyStageTextures();
        SetExposed(false, false, false, false);
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

    // ── Exposure layout ────────────────────────────────────────────────────────

    /// Marks which of the 4 sides are EXPOSED (no mud neighbour) and lays out the interior + the
    /// straight-run corner squares. The exposed bevels and convex corners come straight from the
    /// full-cell sprite underlay, so there is nothing to build for them. Diagonal flags are no
    /// longer needed (kept for call-site compatibility).
    public void SetExposed(bool top, bool right, bool bottom, bool left,
        bool mudTL = false, bool mudTR = false, bool mudBL = false, bool mudBR = false)
    {
        eT = top; eR = right; eB = bottom; eL = left;
        Rect bu = damaged ? bevelUV1 : bevelUV0;

        // Underlay: the full original sprite. This alone gives correct exposed edges + convex
        // rounded corners for an isolated cell — no reconstruction.
        baseSprite.uvRect = bu;

        // Interior: inset by the bevel width on EXPOSED sides (so the sprite bevel shows), and
        // extend to the edge (+bleed) on NEIGHBOUR sides (so the join is flat and seamless).
        float insL = eL ? bp : 0f, insR = eR ? bp : 0f;
        float insT = eT ? bp : 0f, insB = eB ? bp : 0f;
        float uL = eL ? sourceT : 0f, uR = eR ? sourceT : 0f;
        float uT = eT ? sourceT : 0f, uB = eB ? sourceT : 0f;
        LayoutInterior(insL, insT, ts - insL - insR, ts - insT - insB, uL, uT, uR, uB, eL, eT, eR, eB);

        // Straight-run corner squares: exactly ONE adjacent side exposed → the sprite would show a
        // rounded corner where the run should continue straight into the neighbour. Cover it with a
        // flat bevel strip sampled from the exposed side. Both sides exposed = convex (keep the
        // sprite's round); both sides neighbour = concave (interior covers it flat).
        float sc = edgeAlongInset, along = 1f - 2f * edgeAlongInset;
        Rect topFlat    = SubUV(bu, sc,           1f - sourceT, along,   sourceT);
        Rect bottomFlat = SubUV(bu, sc,           0f,           along,   sourceT);
        Rect leftFlat   = SubUV(bu, 0f,           sc,           sourceT, along);
        Rect rightFlat  = SubUV(bu, 1f - sourceT, sc,           sourceT, along);

        SquareCorner(cTL, 0f,      0f,          eT, eL, topFlat,    leftFlat);
        SquareCorner(cTR, ts - bp, 0f,          eT, eR, topFlat,    rightFlat);
        SquareCorner(cBL, 0f,      -(ts - bp),  eB, eL, bottomFlat, leftFlat);
        SquareCorner(cBR, ts - bp, -(ts - bp),  eB, eR, bottomFlat, rightFlat);
    }

    // A corner is squared only for a straight run: one side exposed, the perpendicular not. `hExposed`
    // is the horizontal-edge side (top/bottom), `vExposed` the vertical-edge side (left/right). The
    // strip is widened along the edge in BOTH directions by cornerJoinExtend — inward it covers the
    // sprite's rounded corner start, outward it overlaps into the joined neighbour — so the bevel line
    // runs continuous across the cell boundary with no break. Flat bevel is uniform along its length,
    // so growing the strip just extends it (no visible stretch).
    private void SquareCorner(RawImage img, float px, float py, bool hExposed, bool vExposed, Rect hFlat, Rect vFlat)
    {
        float e = cornerJoinExtend;
        if (hExposed && !vExposed)
            LayoutRegion(img, true, px - e, py, bp + 2f * e, bp, hFlat);      // horizontal bevel → widen left+right
        else if (vExposed && !hExposed)
            LayoutRegion(img, true, px, py + e, bp, bp + 2f * e, vFlat);      // vertical bevel → widen up+down
        else
            LayoutRegion(img, false, px, py, bp, bp, hFlat);
    }

    // ── Stage / damage ──────────────────────────────────────────────────────────

    public void SetDamaged(bool d)
    {
        damaged = d;
        ApplyStageTextures();
        SetExposed(eT, eR, eB, eL);   // re-apply (bevel UV differs per stage)
    }

    private void ApplyStageTextures()
    {
        if (interior != null)
        {
            bool flat = damaged ? flatInterior1 : flatInterior0;
            Texture tex = flat ? Texture2D.whiteTexture : (damaged ? interiorTex1 : interiorTex0);
            Color color = flat ? (damaged ? flatInteriorColor1 : flatInteriorColor0) : Color.white;
            SetInteriorTexAndColor(interior, tex, color);
        }

        Texture bt = damaged ? bevelTex1 : bevelTex0;
        SetTex(baseSprite, bt);
        SetTex(cTL, bt); SetTex(cTR, bt); SetTex(cBL, bt); SetTex(cBR, bt);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetVisible(bool visible)
    {
        float a = visible ? 1f : 0f;
        SetAlpha(baseSprite, a);
        SetAlpha(interior, a);
        SetAlpha(cTL, a); SetAlpha(cTR, a); SetAlpha(cBL, a); SetAlpha(cBR, a);
    }

    public void Clear()
    {
        SetVisible(false);
        eT = eR = eB = eL = false;
        damaged = false;
        gameObject.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private RawImage MakeChild(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = gameObject.layer;
        var crt = go.GetComponent<RectTransform>();
        crt.SetParent(transform, false);
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(0, 1);
        crt.pivot     = new Vector2(0, 1);
        var ri = go.GetComponent<RawImage>();
        ri.raycastTarget = false;
        ri.color = Color.white;
        return ri;
    }

    private void LayoutRegion(RawImage img, bool active, float px, float py, float sw, float sh, Rect uv)
    {
        if (img == null) return;
        img.gameObject.SetActive(active);
        if (!active) return;
        var crt = img.rectTransform;
        crt.anchoredPosition = new Vector2(px, py);
        crt.sizeDelta        = new Vector2(sw, sh);
        img.uvRect           = uv;
    }

    private void LayoutInterior(float px, float py, float sw, float sh, float uL, float uT, float uR, float uB,
        bool exposedLeft, bool exposedTop, bool exposedRight, bool exposedBottom)
    {
        if (interior == null) return;

        // Bleed only into neighbouring mud sides. Bleeding into exposed sides would eat the sprite's
        // rounded convex corners and crush L / reverse-L joins.
        var crt = interior.rectTransform;
        Vector2 offset = damaged ? interiorOffset1 : interiorOffset0;

        float bleedL = exposedLeft   ? 0f : interiorBleed;
        float bleedT = exposedTop    ? 0f : interiorBleed;
        float bleedR = exposedRight  ? 0f : interiorBleed;
        float bleedB = exposedBottom ? 0f : interiorBleed;

        // If the interior is nudged for art alignment, compensate the opposite side so the
        // patch still reaches the original cell bounds and does not reveal the sprite middle.
        if (offset.x < 0f) bleedR = Mathf.Max(bleedR, -offset.x);
        else if (offset.x > 0f) bleedL = Mathf.Max(bleedL, offset.x);

        if (offset.y < 0f) bleedT = Mathf.Max(bleedT, -offset.y);
        else if (offset.y > 0f) bleedB = Mathf.Max(bleedB, offset.y);

        crt.anchoredPosition = new Vector2(px + offset.x - bleedL, -py + offset.y + bleedT);
        crt.sizeDelta        = new Vector2(sw + bleedL + bleedR, sh + bleedT + bleedB);

        // RawImage UV origin is bottom-left. Visual top/bottom trims must therefore adjust
        // the sampled board slice from opposite vertical sides.
        interior.uvRect = new Rect(
            uvX + uL * uvW,
            uvY + uB * uvH,
            (1f - uL - uR) * uvW,
            (1f - uT - uB) * uvH);
    }

    // Sub-region of the beveled sprite's UV rect. fx/fy/fw/fh are fractions (0..1) of the sprite.
    private static Rect SubUV(Rect spriteUV, float fx, float fy, float fw, float fh)
        => new Rect(spriteUV.x + fx * spriteUV.width,
                    spriteUV.y + fy * spriteUV.height,
                    fw * spriteUV.width,
                    fh * spriteUV.height);

    private static Rect SpriteUV(Sprite s)
    {
        if (s == null || s.texture == null) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect;
        float tw = s.texture.width, th = s.texture.height;
        return new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
    }

    private static void SetTex(RawImage ri, Texture tex) { if (ri != null) ri.texture = tex; }
    private static void SetInteriorTexAndColor(RawImage ri, Texture tex, Color color)
    {
        if (ri == null) return;
        ri.texture = tex;
        ri.color = color;
    }
    private static void SetAlpha(RawImage ri, float a) { if (ri != null) { var c = ri.color; c.a = a; ri.color = c; } }
}
