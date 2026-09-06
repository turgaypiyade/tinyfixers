using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// SpreadingGel (yayılan jel) görsel yöneticisi. MudOverlayService'in TEK-STAGE klonu:
/// jel under-tile çizilir (mud gibi), bevel dış kenar + bevelsız iç dolgu + köşe patch'leriyle
/// seamless birleşir; ama jelin damaged/koyu ikinci stage'i YOKTUR ve HİÇ temizlenmez — yalnız
/// eklenir (yayılır). Tüm hücreler daima "overlay" (IsGelAt) kabul edilir.
///
/// Mud'dan farkı: hücreleri RUNTIME'da <see cref="AddGel"/> ile kendisi doğurur (yayılma için).
/// Görsel per-cell <see cref="MudCellView"/> ile çizilir (stage-0 modunda, damaged=false).
public class SpreadingGelOverlayService : MonoBehaviour
{
    [Header("Gel Sprites")]
    [Tooltip("SGOverlay — 4 kenarında bevel baked-in jel sprite'ı (base underlay).")]
    [SerializeField] private Sprite overlaySprite;
    [Tooltip("SGPlained — kenarsız düz jel dokusu (komşular arası iç dolgu; bevel'ı kapatır).")]
    [SerializeField] private Texture plainTexture;

    [Header("Corner Patches (SGCLT2/SGCRT2/SGCLB2/SGCRB2)")]
    [SerializeField] private Sprite cornerTL;
    [SerializeField] private Sprite cornerTR;
    [SerializeField] private Sprite cornerBL;
    [SerializeField] private Sprite cornerBR;
    // Default'lar MudOverlayService'in Inspector'da tunelenmiş değerleriyle aynı (kullanıcı isteği).
    [SerializeField] private Vector2 cornerOffsetTL = new Vector2(-6f, -6f);
    [SerializeField] private Vector2 cornerOffsetTR = new Vector2(6f, -6f);
    [SerializeField] private Vector2 cornerOffsetBL = new Vector2(-6f, 6f);
    [SerializeField] private Vector2 cornerOffsetBR = new Vector2(6f, 6f);
    [SerializeField, Min(0f)] private float cornerInsetPixels = 2f;

    [Header("Interior")]
    [Tooltip("Açıkken iç dolguda SGPlained yerine tek düz renk kullanılır (bevel yine dış sınırda).")]
    [SerializeField] private bool useFlatInterior = false;
    [SerializeField] private Color flatInteriorColor = new Color(0.35f, 0.75f, 0.45f, 1f);
    [SerializeField] private Vector2 interiorOffsetPixels = Vector2.zero;

    [Header("Bevel Width (mud ile aynı; jel sprite'ına göre tunele)")]
    [Range(0.05f, 0.45f)][SerializeField] private float borderThicknessRatio = 0.085f;
    [SerializeField] private float edgeJoinOverlapPixels = 10f;
    [SerializeField] private float interiorBleedPixels = 2f;
    [Range(0f, 1f)][SerializeField] private float underBevelFillRatio = 1f;
    [SerializeField] private float cornerJoinPixels = 4f;
    [SerializeField] private float edgeJoinExtendPixels = 2f;
    [SerializeField] private float edgeStraightCropPixels = 8f;
    [Tooltip("Jel sprite'ının kaynak kenar kalınlığı (px, ~sprite boyutunun 1/10'u). 990px sprite → 99.")]
    [SerializeField] private float sourceBorderPixels = 99f;

    [Header("Spawn Animasyonu (yayılma hissi)")]
    [Tooltip("Yeni jel hücresi fade-in süresi (sn). Pop-in yerine materialize; 0 = anında.")]
    [SerializeField, Min(0f)] private float spawnFadeDuration = 0.1f;

    /// Jel hücre sayısı değişince (eklenince) tetiklenir — coverage goal bunu dinler.
    public event Action OnGelChanged;

    private readonly Dictionary<int, MudCellView> viewsByCellIndex = new();
    private readonly Dictionary<Vector3Int, RawImage> cornerPatches = new();

    private int gridWidth, gridHeight, tileSize;
    private RectTransform overlayRoot;
    private Coroutine pendingBorderRefresh;

    public int Count => viewsByCellIndex.Count;

    // Jel hiç temizlenmediği (yalnız eklendiği) için ObstacleVisualChanged'a abone olmaz → board gerekmez.
    public void Init(int width, int height, int tileSize, RectTransform overlayRoot)
    {
        gridWidth = Mathf.Max(1, width);
        gridHeight = Mathf.Max(1, height);
        if (tileSize > 0) this.tileSize = tileSize;
        if (overlayRoot != null) this.overlayRoot = overlayRoot;
    }

    public bool IsGelAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        return viewsByCellIndex.ContainsKey(CellIndex(x, y));
    }

    public bool TryGetView(int x, int y, out MudCellView view)
        => viewsByCellIndex.TryGetValue(CellIndex(x, y), out view) && view != null;

    /// Runtime yayılma: hücreyi ANINDA (o olay anında — kırılma/rocket-varış) fade-in ile çizer. ERTELENMEZ:
    /// gel yayılma anı = taş kırılma/düşme anı (kullanıcı kuralı). Sıra da olayın kendi zamanlamasından gelir
    /// (line rocket travel gecikmesi → soldan sağa). Contiguity (izole gel yok) BoardController'da komşuluk kuralı.
    public bool AddGel(int x, int y) => AddGelInternal(x, y, fade: true);

    /// Author seed (level başı): fade yok, hemen.
    public bool AddGelImmediate(int x, int y) => AddGelInternal(x, y, fade: false);

    private bool AddGelInternal(int x, int y, bool fade)
    {
        if (overlayRoot == null || tileSize <= 0) return false;
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        int idx = CellIndex(x, y);
        if (viewsByCellIndex.ContainsKey(idx)) return false;

        var view = SpawnCell(x, y);
        if (view == null) return false;

        viewsByCellIndex[idx] = view;
        if (fade) AnimateSpawn(view);
        QueueRefreshAllBorders();
        OnGelChanged?.Invoke();
        return true;
    }

    // Yeni jel hücresi anında pop-in yerine fade-in ile materialize olur → yoğun patlamada daha akıcı,
    // "yayılıyormuş" hissi. Border refresh alfaya dokunmadığı için fade bozulmaz.
    private void AnimateSpawn(MudCellView view)
    {
        if (view == null || spawnFadeDuration <= 0f || !isActiveAndEnabled) return;
        StartCoroutine(CoFadeIn(view));
    }

    private IEnumerator CoFadeIn(MudCellView view)
    {
        float d = spawnFadeDuration;
        float t = 0f;
        if (view != null) view.SetGroupAlpha(0f);
        while (t < d && view != null)
        {
            t += Time.unscaledDeltaTime;
            view.SetGroupAlpha(Mathf.Clamp01(t / d));
            yield return null;
        }
        if (view != null) view.SetGroupAlpha(1f);
    }

    private MudCellView SpawnCell(int x, int y)
    {
        var go = new GameObject($"Gel_{x}_{y}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MudCellView));
        go.layer = overlayRoot.gameObject.layer;
        go.transform.SetParent(overlayRoot, false);

        var view = go.GetComponent<MudCellView>();
        view.Init(overlaySprite, plainTexture, x, y, gridWidth, gridHeight);
        view.SetStage0InteriorStyle(useFlatInterior, flatInteriorColor, interiorOffsetPixels);
        view.PlaceInCell(tileSize);
        view.SetMaxHits(1);
        // Tek-stage: stage-1 (damaged) = stage-0 → null geçince MudCellView stage-0'a düşer.
        view.SetStageAssets(null, null, useFlatInterior, flatInteriorColor, interiorOffsetPixels);
        view.Build(tileSize, borderThicknessRatio, edgeJoinOverlapPixels, interiorBleedPixels,
            underBevelFillRatio, cornerJoinPixels, edgeJoinExtendPixels, edgeStraightCropPixels,
            sourceBorderPixels: sourceBorderPixels);
        view.SetDamaged(false);
        view.SetVisible(true);
        return view;
    }

    // ── Bevel exposure (tek-stage: komşu = herhangi jel hücresi) ──────────────────

    private void RefreshBordersAt(int x, int y)
    {
        if (!viewsByCellIndex.TryGetValue(CellIndex(x, y), out var view) || view == null) return;

        // Grid Y aşağı artar: y-1 görsel üst, y+1 görsel alt.
        view.SetExposed(
            top:    !IsGelAt(x,     y - 1),
            right:  !IsGelAt(x + 1, y    ),
            bottom: !IsGelAt(x,     y + 1),
            left:   !IsGelAt(x - 1, y    ),
            mudTL:   IsGelAt(x - 1, y - 1),
            mudTR:   IsGelAt(x + 1, y - 1),
            mudBL:   IsGelAt(x - 1, y + 1),
            mudBR:   IsGelAt(x + 1, y + 1));
    }

    public void RefreshAllBorders()
    {
        pendingBorderRefresh = null;
        foreach (var kv in cornerPatches)
            if (kv.Value != null) kv.Value.gameObject.SetActive(false);

        foreach (var kv in viewsByCellIndex)
            RefreshBordersAt(kv.Key % gridWidth, kv.Key / gridWidth);

        RefreshCornerPatches();
    }

    private void QueueRefreshAllBorders()
    {
        if (!isActiveAndEnabled)
        {
            RefreshAllBorders();
            return;
        }
        if (pendingBorderRefresh != null) return;
        pendingBorderRefresh = StartCoroutine(CoRefreshNextFrame());
    }

    private IEnumerator CoRefreshNextFrame()
    {
        yield return null;
        RefreshAllBorders();
    }

    public void ClearAll()
    {
        if (pendingBorderRefresh != null)
        {
            StopCoroutine(pendingBorderRefresh);
            pendingBorderRefresh = null;
        }

        foreach (var kv in viewsByCellIndex)
        {
            if (kv.Value == null) continue;
            kv.Value.Clear();
            Destroy(kv.Value.gameObject);
        }
        viewsByCellIndex.Clear();

        foreach (var kv in cornerPatches)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        cornerPatches.Clear();
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;

    // ── Corner patches (mud ile birebir; jel köşe sprite'larıyla) ─────────────────

    private void RefreshCornerPatches()
    {
        if (overlayRoot == null || tileSize <= 0 || gridWidth <= 0 || gridHeight <= 0) return;

        for (int cy = 1; cy < gridHeight; cy++)
        for (int cx = 1; cx < gridWidth; cx++)
        {
            bool nw = IsGelAt(cx - 1, cy - 1);
            bool ne = IsGelAt(cx,     cy - 1);
            bool sw = IsGelAt(cx - 1, cy);
            bool se = IsGelAt(cx,     cy);

            int count = (nw ? 1 : 0) + (ne ? 1 : 0) + (sw ? 1 : 0) + (se ? 1 : 0);
            if (count != 3) continue;

            int missingQuadrant = !nw ? 0 : !ne ? 1 : !sw ? 2 : 3;
            ShowCornerPatch(cx, cy, missingQuadrant);
        }
    }

    private void ShowCornerPatch(int gridCornerX, int gridCornerY, int missingQuadrant)
    {
        Sprite sprite;
        Vector2 pivot;
        Vector2 offset;
        switch (missingQuadrant)
        {
            case 0:
                sprite = cornerBR; pivot = new Vector2(1f, 0f);
                offset = cornerOffsetBR + new Vector2(cornerInsetPixels, cornerInsetPixels);
                break;
            case 1:
                sprite = cornerBL; pivot = new Vector2(0f, 0f);
                offset = cornerOffsetBL + new Vector2(-cornerInsetPixels, cornerInsetPixels);
                break;
            case 2:
                sprite = cornerTR; pivot = new Vector2(1f, 1f);
                offset = cornerOffsetTR + new Vector2(cornerInsetPixels, -cornerInsetPixels);
                break;
            default:
                sprite = cornerTL; pivot = new Vector2(0f, 1f);
                offset = cornerOffsetTL + new Vector2(-cornerInsetPixels, -cornerInsetPixels);
                break;
        }

        if (sprite == null || sprite.texture == null) return;

        var key = new Vector3Int(gridCornerX, gridCornerY, missingQuadrant);
        var img = GetOrCreateCornerPatch(key);
        if (img == null) return;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;
        rt.localEulerAngles = Vector3.zero;
        rt.anchoredPosition = new Vector2(
            gridCornerX * tileSize + offset.x,
            -gridCornerY * tileSize - offset.y);
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        img.texture = sprite.texture;
        img.uvRect = SpriteUV(sprite);
        img.color = Color.white;
        img.gameObject.SetActive(true);
        img.transform.SetAsLastSibling();
    }

    private RawImage GetOrCreateCornerPatch(Vector3Int key)
    {
        if (cornerPatches.TryGetValue(key, out var existing) && existing != null)
            return existing;
        if (overlayRoot == null) return null;

        var go = new GameObject($"GelCornerPatch_{key.x}_{key.y}_{key.z}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = overlayRoot.gameObject.layer;
        go.transform.SetParent(overlayRoot, false);
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        cornerPatches[key] = img;
        return img;
    }

    private static Rect SpriteUV(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return new Rect(0f, 0f, 1f, 1f);
        Rect tr = sprite.textureRect;
        return new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height,
                        tr.width / sprite.texture.width, tr.height / sprite.texture.height);
    }
}
