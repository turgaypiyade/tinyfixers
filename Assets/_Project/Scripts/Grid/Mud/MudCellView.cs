using UnityEngine;
using UnityEngine.UI;

/// Additive-bevel mud cell.
///
/// A plain interior fill (seamless, sampled with a board-slice UV) is drawn up to internal
/// neighbour boundaries, but inset behind exposed bevel sides. The bevel is drawn ONLY on the
/// blob's EXPOSED edges/convex-corners, sampled from the beveled sprite's border regions.
/// Internal edges are just fill-meets-fill, so there are no seams, no cover-subtraction and no
/// rounded-corner gaps between neighbouring mud cells.
///
/// Two stages (light stage-0 / dark stage-1+) swap the interior + bevel textures; the exposure
/// layout is stage-independent so a hit only recolours the cell.
public class MudCellView : MonoBehaviour
{
    private RectTransform rt;

    private RawImage interior;                                   // plain fill, inset only on exposed sides
    private RawImage fTop, fRight, fBottom, fLeft;               // fill under straight exposed bevels
    private RawImage eTop, eRight, eBottom, eLeft;               // straight bevel edges
    private RawImage cTL, cTR, cBL, cBR;                         // convex-corner bevels

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
    private float ts, bp, t, sourceT, sourceCornerT, edgeOverlap, interiorBleed; // screen bevel, edge/corner UV borders, seam overlap, fill bleed
    private float underFill; // 0..1 of bevel width: how far the opaque fill covers the band under an exposed bevel
    private float cornerJoin; // px each edge/fill extends UNDER its convex-corner piece to close the join seam
    private float edgeExtend;  // px a bevel edge extends into a STRAIGHT-run neighbour to close the join break
    private float edgeAlongInset; // UV inset (0..1) cropping the edge along-axis INSIDE the rounded corner so edge ends are perfectly straight
    private float uvX, uvY, uvW, uvH;                            // interior board-slice UV

    private bool damaged;
    private bool eT, eR, eB, eL;                                 // current exposure
    private bool dTL, dTR, dBL, dBR;                             // diagonal neighbour is mud (for straight-run edge extension)

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
        t  = Mathf.Clamp(thicknessRatio, 0.05f, 0.45f);
        bp = Mathf.Round(ts * t);
        underFill = Mathf.Clamp01(underBevelFillRatio);
        cornerJoin = Mathf.Max(0f, cornerJoinPixels);
        edgeExtend = Mathf.Max(0f, edgeJoinExtendPixels);
        float sourceSize = Mathf.Max(1f, sourceTextureSizePixels);
        // The authored mud sprites are 990x990 with about 80 px of source border. Oil uses the
        // same renderer with its own source metrics, because sampling a 1227px OilV4 sprite with
        // mud's 990px constants makes the exposed corner/edge joins look clipped and boxy.
        sourceT = Mathf.Clamp(sourceBorderPixels / sourceSize, 0.01f, 0.45f);
        sourceCornerT = Mathf.Clamp(sourceCornerPixels / sourceSize, sourceT, 0.45f);
        // Straight edges crop a few px DEEPER than the 101 px corner radius so the sampled strip is
        // fully flat — otherwise a residual "oval" of the rounded corner shows at each cell join.
        edgeAlongInset = Mathf.Clamp(sourceCornerT + Mathf.Max(0f, edgeStraightCropPixels) / sourceSize, sourceCornerT, 0.45f);
        edgeOverlap = Mathf.Max(0f, edgeOverlapPixels);
        interiorBleed = Mathf.Max(0f, interiorBleedPixels);

        // The GameObject may carry a stray base Image (legacy creation); this view renders via
        // its own RawImage children, so silence the parent Image to avoid a white quad behind.
        var baseImg = GetComponent<Image>();
        if (baseImg != null) baseImg.enabled = false;

        interior = MakeChild("MudInterior");
        fTop     = MakeChild("MudFillTop");
        fRight   = MakeChild("MudFillRight");
        fBottom  = MakeChild("MudFillBottom");
        fLeft    = MakeChild("MudFillLeft");
        eTop     = MakeChild("MudEdgeTop");
        eRight   = MakeChild("MudEdgeRight");
        eBottom  = MakeChild("MudEdgeBottom");
        eLeft    = MakeChild("MudEdgeLeft");
        cTL      = MakeChild("MudCornerTL");
        cTR      = MakeChild("MudCornerTR");
        cBL      = MakeChild("MudCornerBL");
        cBR      = MakeChild("MudCornerBR");

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

    /// Marks which of the 4 sides are EXPOSED (no mud neighbour) and lays out the
    /// bevel edges + convex corners accordingly. Straight edges span the full side; where a
    /// perpendicular side is also exposed the edge is trimmed to leave room for a corner piece.
    public void SetExposed(bool top, bool right, bool bottom, bool left,
        bool mudTL = false, bool mudTR = false, bool mudBL = false, bool mudBR = false)
    {
        eT = top; eR = right; eB = bottom; eL = left;
        dTL = mudTL; dTR = mudTR; dBL = mudBL; dBR = mudBR;
        Rect bu = damaged ? bevelUV1 : bevelUV0;

        float trimL = eL ? bp : 0f, trimR = eR ? bp : 0f;   // px trims where perpendicular exposed
        float trimT = eT ? bp : 0f, trimB = eB ? bp : 0f;
        float uL = eL ? sourceT : 0f, uR = eR ? sourceT : 0f; // source UV trims
        float uT = eT ? sourceT : 0f, uB = eB ? sourceT : 0f;

        LayoutInterior(trimL, trimT, ts - trimL - trimR, ts - trimT - trimB, uL, uT, uR, uB, eL, eT, eR, eB);

        // FILL (opaque interior band, drawn BEHIND the bevel) overlaps the mud neighbour by
        // edgeOverlap on every internal end → guarantees no board colour ever peeks at a join.
        float coL = eL ? cornerJoin : edgeOverlap;
        float coR = eR ? cornerJoin : edgeOverlap;
        float coT = eT ? cornerJoin : edgeOverlap;
        float coB = eB ? cornerJoin : edgeOverlap;

        // BEVEL edge end extensions — per END, direction-aware. An edge extends a hair into the
        // neighbour ONLY where the same exposed run continues straight (perpendicular side is mud
        // AND the diagonal on the exposed side is empty). That closes the little break the user
        // saw between stacked cells, while NOT poking a bevel sliver into an L / reverse-L concave
        // corner (there the diagonal is mud → extension 0). A corner-trimmed end (perpendicular
        // exposed, convex corner) tucks cornerJoin under the opaque corner piece.
        float e = edgeExtend;
        float tL = eL ? cornerJoin : (dTL ? 0f : e);   // eTop:   left / right ends
        float tR = eR ? cornerJoin : (dTR ? 0f : e);
        float bL = eL ? cornerJoin : (dBL ? 0f : e);   // eBottom
        float bR = eR ? cornerJoin : (dBR ? 0f : e);
        float lT = eT ? cornerJoin : (dTL ? 0f : e);   // eLeft:  top / bottom ends
        float lB = eB ? cornerJoin : (dBL ? 0f : e);
        float rT = eT ? cornerJoin : (dTR ? 0f : e);   // eRight
        float rB = eB ? cornerJoin : (dBR ? 0f : e);

        float topX  = trimL - tL,  topW  = ts - trimL - trimR + tL + tR;
        float botX  = trimL - bL,  botW  = ts - trimL - trimR + bL + bR;
        float leftY = -trimT + lT, leftH = ts - trimT - trimB + lT + lB;
        float rightY = -trimT + rT, rightH = ts - trimT - trimB + rT + rB;

        // Cover the band behind an exposed bevel with opaque interior so no board colour peeks
        // through the bevel's soft inner edge (the "ince boşluk"). Fill spans up to the full
        // bevel width (underFill), but the strips are trimmed at exposed corners and only drawn
        // on exposed sides, so they never reach convex corners nor spill into empty diagonals.
        // At least a 2px raster seam is always covered.
        float fillH = Mathf.Max(Mathf.Min(interiorBleed, 2f), bp * underFill);
        float hFillX = trimL - coL;
        float hFillW = ts - trimL - trimR + coL + coR;
        float vFillY = -trimT + coT;
        float vFillH = ts - trimT - trimB + coT + coB;

        LayoutFill(fTop,    eT, hFillX, 0f,             hFillW, fillH, uL,          1f - sourceT, 1f - uL - uR, sourceT,      horizontal: true);
        LayoutFill(fBottom, eB, hFillX, -(ts - fillH),  hFillW, fillH, uL,          0f,           1f - uL - uR, sourceT,      horizontal: true);
        LayoutFill(fLeft,   eL, 0f,     vFillY,         fillH, vFillH, 0f,          uB,           sourceT,      1f - uT - uB, horizontal: false);
        LayoutFill(fRight,  eR, ts - fillH, vFillY,     fillH, vFillH, 1f - sourceT, uB,          sourceT,      1f - uT - uB, horizontal: false);

        // Straight edges sample ONLY the sprite's flat middle section, cropped INSIDE the rounded
        // corner radius on the along-edge axis (sourceCornerT, not sourceT). Sampling from sourceT
        // would pull in the 80..101 px curved corner start, so each edge's ends bow inward and two
        // adjacent cells meet in a slight concave scallop instead of a straight line. The bevel
        // thickness axis stays sourceT. Rounded corners are drawn only by cTL/cTR/cBL/cBR.
        float sc = edgeAlongInset, along = 1f - 2f * edgeAlongInset;
        LayoutRegion(eTop,    eT, topX,  0f,          topW,  bp,      SubUV(bu, sc,           1f - sourceT, along,   sourceT));
        LayoutRegion(eBottom, eB, botX,  -(ts - bp),  botW,  bp,      SubUV(bu, sc,           0f,           along,   sourceT));
        LayoutRegion(eLeft,   eL, 0f,    leftY,       bp,    leftH,   SubUV(bu, 0f,           sc,           sourceT, along));
        LayoutRegion(eRight,  eR, ts - bp, rightY,    bp,    rightH,  SubUV(bu, 1f - sourceT, sc,           sourceT, along));

        // Convex corners (both perpendicular sides exposed).
        LayoutRegion(cTL, eT && eL, 0f,       0f,          bp, bp, SubUV(bu, 0f,                1f - sourceCornerT, sourceCornerT, sourceCornerT));
        LayoutRegion(cTR, eT && eR, ts - bp,  0f,          bp, bp, SubUV(bu, 1f - sourceCornerT, 1f - sourceCornerT, sourceCornerT, sourceCornerT));
        LayoutRegion(cBL, eB && eL, 0f,       -(ts - bp),  bp, bp, SubUV(bu, 0f,                0f,                sourceCornerT, sourceCornerT));
        LayoutRegion(cBR, eB && eR, ts - bp,  -(ts - bp),  bp, bp, SubUV(bu, 1f - sourceCornerT, 0f,                sourceCornerT, sourceCornerT));
    }

    // ── Stage / damage ──────────────────────────────────────────────────────────

    public void SetDamaged(bool d)
    {
        if (damaged == d && interior != null && interior.texture != null) { /* still refresh */ }
        damaged = d;
        ApplyStageTextures();
        SetExposed(eT, eR, eB, eL, dTL, dTR, dBL, dBR);   // re-apply (bevel UV differs per stage)
    }

    private void ApplyStageTextures()
    {
        if (interior != null)
        {
            bool flat = damaged ? flatInterior1 : flatInterior0;
            Texture tex = flat ? Texture2D.whiteTexture : (damaged ? interiorTex1 : interiorTex0);
            Color color = flat ? (damaged ? flatInteriorColor1 : flatInteriorColor0) : Color.white;
            SetInteriorTexAndColor(interior, tex, color);
            SetInteriorTexAndColor(fTop, tex, color);
            SetInteriorTexAndColor(fRight, tex, color);
            SetInteriorTexAndColor(fBottom, tex, color);
            SetInteriorTexAndColor(fLeft, tex, color);
        }

        Texture bt = damaged ? bevelTex1 : bevelTex0;
        SetTex(eTop, bt); SetTex(eRight, bt); SetTex(eBottom, bt); SetTex(eLeft, bt);
        SetTex(cTL, bt);  SetTex(cTR, bt);   SetTex(cBL, bt);      SetTex(cBR, bt);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetVisible(bool visible)
    {
        SetAlpha(interior, visible ? 1f : 0f);
        float a = visible ? 1f : 0f;
        SetAlpha(fTop, a); SetAlpha(fRight, a); SetAlpha(fBottom, a); SetAlpha(fLeft, a);
        SetAlpha(eTop, a); SetAlpha(eRight, a); SetAlpha(eBottom, a); SetAlpha(eLeft, a);
        SetAlpha(cTL, a);  SetAlpha(cTR, a);   SetAlpha(cBL, a);      SetAlpha(cBR, a);
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

    private void LayoutFill(RawImage img, bool active, float px, float py, float sw, float sh,
        float fx, float fy, float fw, float fh, bool horizontal)
    {
        if (img == null) return;
        img.gameObject.SetActive(active && sw > 0f && sh > 0f);
        if (!img.gameObject.activeSelf) return;

        Vector2 offset = damaged ? interiorOffset1 : interiorOffset0;
        if (horizontal)
        {
            if (offset.x < 0f) sw += -offset.x;
            else if (offset.x > 0f) { px -= offset.x; sw += offset.x; }
            px += offset.x;
        }
        else
        {
            if (offset.y < 0f) { py += offset.y; sh += -offset.y; }
            else if (offset.y > 0f) sh += offset.y;
        }

        var crt = img.rectTransform;
        crt.anchoredPosition = new Vector2(px, py);
        crt.sizeDelta        = new Vector2(sw, sh);

        img.uvRect = new Rect(
            uvX + fx * uvW,
            uvY + fy * uvH,
            fw * uvW,
            fh * uvH);
    }

    private void LayoutInterior(float px, float py, float sw, float sh, float uL, float uT, float uR, float uB,
        bool exposedLeft, bool exposedTop, bool exposedRight, bool exposedBottom)
    {
        if (interior == null) return;

        // Bleed only into neighbouring mud sides. Bleeding into exposed sides fills the
        // transparent part of rounded convex corners and makes L / reverse-L joins look crushed.
        var crt = interior.rectTransform;
        Vector2 offset = damaged ? interiorOffset1 : interiorOffset0;

        float bleedL = exposedLeft   ? 0f : interiorBleed;
        float bleedT = exposedTop    ? 0f : interiorBleed;
        float bleedR = exposedRight  ? 0f : interiorBleed;
        float bleedB = exposedBottom ? 0f : interiorBleed;

        // If the interior is nudged for art alignment, compensate the opposite side so the
        // patch still reaches the original cell bounds and does not reveal the board color.
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
