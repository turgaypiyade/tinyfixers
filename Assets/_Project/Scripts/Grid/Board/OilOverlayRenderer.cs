using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cell-anchored oil blob rendering.
///
/// Oil DATA is cell-based (level.obstacles[idx] == Oil). This renderer mirrors the proven
/// MudCellView additive-bevel layout: interior is sampled as a continuous board-slice texture,
/// while bevel edges/corners are drawn only on the blob's exposed outside boundary.
/// Internal oil neighbours therefore read as one connected slime/oil surface instead of
/// separate rounded squares.
/// </summary>
public sealed class OilOverlayRenderer
{
    // OilV6 ölçülü: border ~82px / 1233px ≈ 0.067 → ekran kalınlığı OilV6 oranıyla 1:1 (crisp köşe).
    private const float BorderThicknessRatio = 0.067f;
    private const float EdgeJoinOverlapPixels = 1.5f;
    private const float InteriorBleedPixels = 2f;
    private const float UnderBevelFillRatio = 1f;
    private const float CornerJoinPixels = 2f;
    private const float EdgeJoinExtendPixels = 6f;
    private const float EdgeStraightCropPixels = 12f;
    // OilV6 gerçek ölçüleri (ölçüldü): 1233px sprite, ~82px bevel, yuvarlak köşe ~46px.
    // Eski değerler (1227/100/150) OilV4 içindi ve OilV6'nın köşesini bozuyordu.
    private const float OilSourceTexturePixels = 1233f;
    private const float OilSourceBorderPixels = 82f;
    private const float OilSourceCornerPixels = 82f;
    private const float ConcaveCornerSizeRatio = 0.42f;

    private readonly BoardController board;
    private readonly Sprite borderedOilSprite;
    private readonly Texture oilInteriorTexture;

    private RectTransform root;
    private int builtTileSize;
    private readonly Dictionary<Vector2Int, MudCellView> views = new();
    private readonly Dictionary<Vector3Int, RawImage> concaveCorners = new();

    // Birleşme (concave/junction) köşe sprite'ları. SADECE 3 oil + 1 boş junction'da kullanılır;
    // orijinal dış kenar/köşeye DOKUNULMAZ. Boş hücrenin yönüne göre seçilir, tam-boy,
    // DÖNDÜRÜLMEDEN/KESİLMEDEN, köşesi vertex'e denk gelen hücrenin üstüne konur.
    private Sprite cornerBR, cornerBL, cornerTR, cornerTL;
    private float junctionSizeRatio = 1.6f; // >1 → RB imajı büyür, kollar komşularla overlap eder
    // Her köşe için BAĞIMSIZ px offset (x=sağa, y=aşağı). Aynalama yok; her köşe kendi değeriyle oturur.
    private Vector2 offBR, offBL, offTR, offTL;

    // DEBUG: her junction'a hangi köşe kodunu (RB/LB/TR/TL) koyduğumu kırmızı yazar. Kapatmak için false.
    private static bool ShowCornerDebugLabels = false;
    // DEBUG: yerleştirilen junction parçasını parlak magenta boyar (parça gerçekten konuyor mu görmek için).
    private static bool TintJunctionDebug = true;
    private readonly Dictionary<Vector3Int, UnityEngine.UI.Text> debugLabels = new();
    private static Font debugFont;

    public OilOverlayRenderer(BoardController board, Sprite borderedOilSprite, Texture oilInteriorTexture)
    {
        this.board = board;
        this.borderedOilSprite = borderedOilSprite;
        this.oilInteriorTexture = oilInteriorTexture;
    }

    // br=OilRB (sağ-alt), bl=OilLB1 (sol-alt), tr=sağ-üst, tl=sol-üst. sizeRatio>1 → overlap.
    public void SetCornerSprites(Sprite br, Sprite bl, Sprite tr, Sprite tl, float sizeRatio,
        Vector2 oBR, Vector2 oBL, Vector2 oTR, Vector2 oTL)
    {
        cornerBR = br; cornerBL = bl; cornerTR = tr; cornerTL = tl;
        junctionSizeRatio = sizeRatio > 0f ? sizeRatio : 1f;
        offBR = oBR; offBL = oBL; offTR = oTR; offTL = oTL;
    }

    public void SetActive(bool active)
    {
        if (root != null) root.gameObject.SetActive(active);
    }

    /// <summary>Shows oil overlays for exactly the given oil cells, hides all others.</summary>
    public void Refresh(IReadOnlyList<Vector2Int> oilCells)
    {
        int size = board != null ? board.TileSize : 0;
        if (size <= 0)
            return;

        EnsureRoot();
        if (root == null)
            return;

        if (builtTileSize != size)
        {
            ClearAllViews();
            builtTileSize = size;
        }

        foreach (var kv in views)
            if (kv.Value != null) kv.Value.Clear();
        foreach (var kv in concaveCorners)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);
        foreach (var kv in debugLabels)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);

        if (oilCells != null)
        {
            var oilSet = new HashSet<Vector2Int>(oilCells);
            for (int i = 0; i < oilCells.Count; i++)
            {
                var cell = oilCells[i];
                var view = GetOrCreateView(cell, size);
                if (view == null)
                    continue;

                view.gameObject.SetActive(true);
                view.PlaceInCell(size);
                view.SetVisible(true);
                view.SetExposed(
                    top:    !oilSet.Contains(new Vector2Int(cell.x, cell.y - 1)),
                    right:  !oilSet.Contains(cell + Vector2Int.right),
                    bottom: !oilSet.Contains(new Vector2Int(cell.x, cell.y + 1)),
                    left:   !oilSet.Contains(cell + Vector2Int.left),
                    // Oil'de L şeklindeki iç köşede diagonal oil hücresi edge extension'ı
                    // kapatmamalı. Mud için bu guard doğruydu; OilV4'ün parlak border'ında
                    // guard açık kalırsa iç köşede beyaz arka plan görünür.
                    mudTL:  false,
                    mudTR:  false,
                    mudBL:  false,
                    mudBR:  false);
            }

            ShowConcaveCornerPatches(oilSet, size);
        }

        // Stay above tiles even after RefreshAllSortingOrders reorders the tile layer.
        root.SetAsLastSibling();
    }

    /// <summary>Hides a single oil cell's overlay (e.g. right when that oil is destroyed).</summary>
    public void Hide(Vector2Int cell)
    {
        if (board?.ObstacleStateService != null)
        {
            Refresh(board.ObstacleStateService.GetAllOilCells());
            return;
        }

        if (views.TryGetValue(cell, out var view) && view != null)
            view.Clear();
    }

    private void EnsureRoot()
    {
        var tilesRoot = board != null ? board.TilesRoot : null;
        if (tilesRoot == null) return;

        var parent = tilesRoot.parent as RectTransform;
        if (parent == null) return;

        if (root == null)
        {
            var go = new GameObject("OilOverlay", typeof(RectTransform));
            root = go.GetComponent<RectTransform>();
            root.SetParent(parent, false);
        }
        else if (root.parent != parent)
        {
            root.SetParent(parent, false);
        }

        // Mirror TilesRoot's transform so MudCellView.PlaceInCell maps to the same screen positions.
        root.anchorMin = tilesRoot.anchorMin;
        root.anchorMax = tilesRoot.anchorMax;
        root.pivot = tilesRoot.pivot;
        root.anchoredPosition = tilesRoot.anchoredPosition;
        root.sizeDelta = tilesRoot.sizeDelta;
        root.localScale = tilesRoot.localScale;

        root.SetAsLastSibling();
    }

    private MudCellView GetOrCreateView(Vector2Int cell, int tileSize)
    {
        if (views.TryGetValue(cell, out var existing) && existing != null)
            return existing;

        var go = new GameObject($"Oil_{cell.x}_{cell.y}", typeof(RectTransform));
        go.transform.SetParent(root, false);

        var view = go.AddComponent<MudCellView>();
        view.Init(borderedOilSprite, oilInteriorTexture, cell.x, cell.y, board.Width, board.Height);
        view.SetMaxHits(1);
        view.SetStageAssets(null, null, false, Color.white, Vector2.zero);
        view.Build(
            tileSize,
            BorderThicknessRatio,
            EdgeJoinOverlapPixels,
            InteriorBleedPixels,
            UnderBevelFillRatio,
            CornerJoinPixels,
            EdgeJoinExtendPixels,
            EdgeStraightCropPixels,
            OilSourceTexturePixels,
            OilSourceBorderPixels,
            OilSourceCornerPixels);
        view.PlaceInCell(tileSize);
        view.SetDamaged(false);
        view.SetVisible(true);

        views[cell] = view;
        return view;
    }

    private void ClearAllViews()
    {
        foreach (var kv in views)
        {
            if (kv.Value != null)
                Object.Destroy(kv.Value.gameObject);
        }

        views.Clear();
        foreach (var kv in concaveCorners)
        {
            if (kv.Value != null)
                Object.Destroy(kv.Value.gameObject);
        }

        concaveCorners.Clear();
        foreach (var kv in debugLabels)
        {
            if (kv.Value != null)
                Object.Destroy(kv.Value.gameObject);
        }

        debugLabels.Clear();
    }

    private void ShowConcaveCornerPatches(HashSet<Vector2Int> oilSet, int tileSize)
    {
        if (oilSet == null || root == null || board == null)
            return;

        // A concave oil notch exists at a grid vertex when exactly one of the 4 surrounding cells
        // is empty and the other 3 are oil. Straight edge strips alone leave a small white corner
        // there, especially after one oil cell is hit/removed.
        for (int cy = 1; cy < board.Height; cy++)
        for (int cx = 1; cx < board.Width; cx++)
        {
            bool nw = oilSet.Contains(new Vector2Int(cx - 1, cy - 1));
            bool ne = oilSet.Contains(new Vector2Int(cx,     cy - 1));
            bool sw = oilSet.Contains(new Vector2Int(cx - 1, cy));
            bool se = oilSet.Contains(new Vector2Int(cx,     cy));

            int count = (nw ? 1 : 0) + (ne ? 1 : 0) + (sw ? 1 : 0) + (se ? 1 : 0);
            if (count != 3)
                continue;

            int missingQuadrant = !nw ? 0 : !ne ? 1 : !sw ? 2 : 3;
            ShowConcaveCornerPatch(cx, cy, missingQuadrant, tileSize);
        }
    }

    // Junction (3 oil + 1 boş). Boş hücrenin yönüne göre sprite seçilir ve köşesi vertex'e denk
    // gelen oil hücresinin üstüne TAM-BOY, döndürülmeden/kesilmeden konur. Sprite transparan
    // olduğu için yalnız o birleşme köşesini ekler; orijinal dış şekle dokunmaz.
    // missingQuadrant: 0=NW boş, 1=NE boş, 2=SW boş, 3=SE boş (vertex = grid köşesi cx,cy).
    private void ShowConcaveCornerPatch(int gridCornerX, int gridCornerY, int missingQuadrant, int tileSize)
    {
        // Sprite'ın stroke köşesi vertex'e denk gelmeli; bu yüzden BOŞ hücrenin üstüne, o hücrenin
        // vertex'e değen köşesi = sprite'ın stroke köşesi olacak şekilde konur.
        // Sprite'ın stroke köşesi = pivot; vertex'e sabitlenir. Boyut junctionSizeRatio ile büyür →
        // stroke vertex'te kalırken kollar uzar ve komşu hücrelerin border'ıyla OVERLAP eder.
        Sprite sprite;
        Vector2 pivot;
        string code;
        switch (missingQuadrant)
        {
            case 0:  sprite = cornerBR; pivot = new Vector2(1f, 0f); code = "RB"; break; // NW boş → OilRB, stroke sağ-alt (üst-sola açılır)
            case 1:  sprite = cornerBL; pivot = new Vector2(0f, 0f); code = "LB"; break; // NE boş → OilLB1, stroke sol-alt
            case 2:  sprite = cornerTR; pivot = new Vector2(1f, 1f); code = "TR"; break; // SW boş → stroke sağ-üst
            default: sprite = cornerTL; pivot = new Vector2(0f, 1f); code = "TL"; break; // SE boş → stroke sol-üst
        }

        ShowDebugLabel(gridCornerX, gridCornerY, missingQuadrant, code, tileSize);

        if (sprite == null || sprite.texture == null)
            return; // o yön için sprite henüz çizilmedi → hiçbir şey basma (yanlış yama yok)

        var key = new Vector3Int(gridCornerX, gridCornerY, missingQuadrant);
        var patch = GetOrCreateConcaveCorner(key);
        if (patch == null)
            return;

        float size = tileSize * (junctionSizeRatio > 0f ? junctionSizeRatio : 1f);
        var rt = patch.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;                                    // stroke köşesi
        rt.localEulerAngles = Vector3.zero;
        // Her köşe kendi offset'iyle konur (x=sağa, y=aşağı). anchoredPosition.y negatif olduğu için -y.
        Vector2 off = missingQuadrant == 0 ? offBR
                    : missingQuadrant == 1 ? offBL
                    : missingQuadrant == 2 ? offTR
                    : offTL;
        rt.anchoredPosition = new Vector2(
            gridCornerX * tileSize + off.x,
            -gridCornerY * tileSize - off.y);
        rt.sizeDelta = new Vector2(size, size);

        patch.texture = sprite.texture;
        patch.uvRect = SpriteUV(sprite);
        patch.color = TintJunctionDebug ? new Color(1f, 0f, 1f, 0.7f) : Color.white;
        patch.gameObject.SetActive(true);
        patch.transform.SetAsLastSibling();
    }

    private RawImage GetOrCreateConcaveCorner(Vector3Int key)
    {
        if (concaveCorners.TryGetValue(key, out var existing) && existing != null)
            return existing;

        var go = new GameObject($"OilConcaveCorner_{key.x}_{key.y}_{key.z}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(root, false);
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        img.color = Color.white;
        concaveCorners[key] = img;
        return img;
    }

    // DEBUG: junction vertex'ine kırmızı köşe-kodu yazar (RB/LB/TR/TL).
    private void ShowDebugLabel(int vx, int vy, int quadrant, string code, int tileSize)
    {
        if (!ShowCornerDebugLabels)
            return;

        if (debugFont == null)
        {
            debugFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (debugFont == null) debugFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (debugFont == null)
            return;

        var key = new Vector3Int(vx, vy, 100 + quadrant); // patch key-space'inden ayrı
        if (!debugLabels.TryGetValue(key, out var lbl) || lbl == null)
        {
            var go = new GameObject($"OilDbg_{vx}_{vy}_{quadrant}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(root, false);
            lbl = go.GetComponent<UnityEngine.UI.Text>();
            lbl.font = debugFont;
            lbl.raycastTarget = false;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.fontStyle = FontStyle.Bold;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            debugLabels[key] = lbl;
        }

        lbl.text = code;
        lbl.color = Color.red;
        lbl.fontSize = Mathf.Max(14, tileSize / 3);

        var rt = lbl.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.anchoredPosition = new Vector2(vx * tileSize, -vy * tileSize);
        lbl.gameObject.SetActive(true);
        lbl.transform.SetAsLastSibling();
    }

    private static Rect SpriteUV(Sprite s)
    {
        if (s == null || s.texture == null) return new Rect(0, 0, 1, 1);
        var tr = s.textureRect;
        float tw = s.texture.width, th = s.texture.height;
        return new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
    }
}
