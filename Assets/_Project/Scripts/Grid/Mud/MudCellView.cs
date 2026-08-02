using UnityEngine;
using UnityEngine.UI;

/// Additive-bevel mud cell.
///
/// A plain interior fill (seamless, sampled with a board-slice UV) is ALWAYS drawn to fill the
/// whole cell. The bevel is drawn ONLY on the blob's EXPOSED edges/convex-corners, sampled from
/// the beveled sprite's border regions. Internal edges are just fill-meets-fill, so there are no
/// seams, no cover-subtraction and no rounded-corner gaps between neighbouring mud cells.
///
/// Two stages (light stage-0 / dark stage-1+) swap the interior + bevel textures; the exposure
/// layout is stage-independent so a hit only recolours the cell.
public class MudCellView : MonoBehaviour
{
    private RectTransform rt;

    private RawImage interior;                                   // full-cell plain fill (seamless)
    private RawImage eTop, eRight, eBottom, eLeft;               // straight bevel edges
    private RawImage cTL, cTR, cBL, cBR;                         // convex-corner bevels

    // Stage assets.
    private Texture interiorTex0, interiorTex1;                  // plain fill (light / dark)
    private Texture bevelTex0, bevelTex1;                        // beveled sprite textures
    private Rect    bevelUV0, bevelUV1;                          // beveled sprite rect in its texture (UV 0..1)

    private int   gridX, gridY, gridWidth, gridHeight;
    private int   maxHits = 1;
    private float ts, bp, t;                                     // tile px, bevel px, bevel fraction
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

    public void SetStageAssets(Sprite bevelSprite1, Texture interiorTexture1)
    {
        interiorTex1 = interiorTexture1 != null ? interiorTexture1 : interiorTex0;
        bevelTex1    = bevelSprite1 != null ? bevelSprite1.texture : bevelTex0;
        bevelUV1     = bevelSprite1 != null ? SpriteUV(bevelSprite1) : bevelUV0;
    }

    public void SetMaxHits(int max) => maxHits = Mathf.Max(1, max);

    /// Creates the child RawImages and caches geometry. Call after Init/SetStageAssets.
    public void Build(int tileSize, float thicknessRatio)
    {
        ts = tileSize;
        t  = Mathf.Clamp(thicknessRatio, 0.05f, 0.45f);
        bp = Mathf.Round(ts * t);

        // The GameObject may carry a stray base Image (legacy creation); this view renders via
        // its own RawImage children, so silence the parent Image to avoid a white quad behind.
        var baseImg = GetComponent<Image>();
        if (baseImg != null) baseImg.enabled = false;

        interior = MakeChild("MudInterior");
        eTop     = MakeChild("MudEdgeTop");
        eRight   = MakeChild("MudEdgeRight");
        eBottom  = MakeChild("MudEdgeBottom");
        eLeft    = MakeChild("MudEdgeLeft");
        cTL      = MakeChild("MudCornerTL");
        cTR      = MakeChild("MudCornerTR");
        cBL      = MakeChild("MudCornerBL");
        cBR      = MakeChild("MudCornerBR");

        // Interior fills the whole cell (anchors stretch); UV = board slice.
        var irt = interior.rectTransform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
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

    /// Marks which of the 4 sides are EXPOSED (no same-stage mud neighbour) and lays out the
    /// bevel edges + convex corners accordingly. Straight edges span the full side; where a
    /// perpendicular side is also exposed the edge is trimmed to leave room for a corner piece.
    public void SetExposed(bool top, bool right, bool bottom, bool left)
    {
        eT = top; eR = right; eB = bottom; eL = left;
        Rect bu = damaged ? bevelUV1 : bevelUV0;

        float trimL = eL ? bp : 0f, trimR = eR ? bp : 0f;   // px trims where perpendicular exposed
        float trimT = eT ? bp : 0f, trimB = eB ? bp : 0f;
        float uL = eL ? t : 0f, uR = eR ? t : 0f;           // matching UV-fraction trims
        float uT = eT ? t : 0f, uB = eB ? t : 0f;

        // Straight edges: trim ends where the perpendicular side is also exposed (corner piece fills there).
        LayoutRegion(eTop,    eT, trimL, 0f,          ts - trimL - trimR, bp, SubUV(bu, uL,       1f - t, 1f - uL - uR, t));
        LayoutRegion(eBottom, eB, trimL, -(ts - bp),  ts - trimL - trimR, bp, SubUV(bu, uL,       0f,     1f - uL - uR, t));
        LayoutRegion(eLeft,   eL, 0f,     -trimT,     bp, ts - trimT - trimB, SubUV(bu, 0f,       uB,     t, 1f - uT - uB));
        LayoutRegion(eRight,  eR, ts - bp, -trimT,    bp, ts - trimT - trimB, SubUV(bu, 1f - t,   uB,     t, 1f - uT - uB));

        // Convex corners (both perpendicular sides exposed).
        LayoutRegion(cTL, eT && eL, 0f,       0f,          bp, bp, SubUV(bu, 0f,     1f - t, t, t));
        LayoutRegion(cTR, eT && eR, ts - bp,  0f,          bp, bp, SubUV(bu, 1f - t, 1f - t, t, t));
        LayoutRegion(cBL, eB && eL, 0f,       -(ts - bp),  bp, bp, SubUV(bu, 0f,     0f,     t, t));
        LayoutRegion(cBR, eB && eR, ts - bp,  -(ts - bp),  bp, bp, SubUV(bu, 1f - t, 0f,     t, t));
    }

    // ── Stage / damage ──────────────────────────────────────────────────────────

    public void SetDamaged(bool d)
    {
        if (damaged == d && interior != null && interior.texture != null) { /* still refresh */ }
        damaged = d;
        ApplyStageTextures();
        SetExposed(eT, eR, eB, eL);   // re-apply (bevel UV differs per stage)
    }

    private void ApplyStageTextures()
    {
        if (interior != null) interior.texture = damaged ? interiorTex1 : interiorTex0;
        Texture bt = damaged ? bevelTex1 : bevelTex0;
        SetTex(eTop, bt); SetTex(eRight, bt); SetTex(eBottom, bt); SetTex(eLeft, bt);
        SetTex(cTL, bt);  SetTex(cTR, bt);   SetTex(cBL, bt);      SetTex(cBR, bt);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    public void SetVisible(bool visible)
    {
        SetAlpha(interior, visible ? 1f : 0f);
        float a = visible ? 1f : 0f;
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
    private static void SetAlpha(RawImage ri, float a) { if (ri != null) { var c = ri.color; c.a = a; ri.color = c; } }
}
