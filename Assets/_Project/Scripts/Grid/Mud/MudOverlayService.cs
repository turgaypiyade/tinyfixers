using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MudOverlayService : MonoBehaviour
{
    [Header("Mud Sprites & Texture")]
    [Tooltip("Sprite B — tüm 4 kenarında bevel/shadow baked-in olan mud sprite'ı. Her zaman base olarak kullanılır.")]
    [SerializeField] private Sprite borderedMudSprite;
    [Tooltip("Sprite A'nın texture'ı — kenarsız düz mud. Stage-0 komşular arasında bevel'ı kapatmak için UV-mapped cover olarak kullanılır.")]
    [SerializeField] private Texture plainMudTexture;
    [Tooltip("Hasarlı (stage 1+) koyu mud texture'ı — kenarsız. Damaged hücreler bunu grid-slice UV ile çizer, böylece komşu koyu mud hücreleri seam olmadan birleşir. Boş ise eski per-cell sprite overlay'e düşülür.")]
    [SerializeField] private Texture damagedMudTexture;
    [Tooltip("Sprite B'nin koyu eşdeğeri — 4 kenarında bevel baked-in olan KOYU mud sprite'ı. Atanırsa damaged stage de stage-0 ile birebir maskelenir (bevel sadece dış kenarda, iç seamless). Boş ise yukarıdaki damagedMudTexture ile seamless dolgu kullanılır.")]
    [SerializeField] private Sprite damagedBorderedMudSprite;

    [Header("Mud Corner Patches")]
    [Tooltip("MudCorners/MCLT1 — yalnız mud overlay birleşim köşeleri için kullanılır.")]
    [SerializeField] private Sprite cornerTL;
    [Tooltip("MudCorners/MCRT1 — yalnız mud overlay birleşim köşeleri için kullanılır.")]
    [SerializeField] private Sprite cornerTR;
    [Tooltip("MudCorners/MCLB1 — yalnız mud overlay birleşim köşeleri için kullanılır.")]
    [SerializeField] private Sprite cornerBL;
    [Tooltip("MudCorners/MCRB1 — yalnız mud overlay birleşim köşeleri için kullanılır.")]
    [SerializeField] private Sprite cornerBR;
    [Tooltip("Corner patch ince ayarı: x sağa, y aşağı.")]
    [SerializeField] private Vector2 cornerOffsetTL;
    [SerializeField] private Vector2 cornerOffsetTR;
    [SerializeField] private Vector2 cornerOffsetBL;
    [SerializeField] private Vector2 cornerOffsetBR;
    [Tooltip("Köşe patch'ini birleşim vertex'inden mud tarafına içeri taşıyan mesafe (px).")]
    [SerializeField, Min(0f)] private float cornerInsetPixels = 2f;

    [Header("Stage 0 Interior")]
    [Tooltip("Açıkken üst mud katmanında MudOverlayStage1_Sp 1 dokusu yerine tek, pürüzsüz renk kullanılır. Bevel yine MudWithBevel'dan sadece dış sınıra çizilir.")]
    [SerializeField] private bool useFlatStage0Interior = true;
    [SerializeField] private Color flatStage0InteriorColor = new Color(0.72f, 0.28f, 0.07f, 1f);
    [Tooltip("Üst mud iç patch hizası. X negatifse sola, Y pozitifse yukarı kayar.")]
    [SerializeField] private Vector2 stage0InteriorOffsetPixels = new Vector2(-1.5f, 0f);

    [Header("Bevel Width")]
    [Tooltip("Additive bevel: AÇIK kenarlara çizilen bevel şeridinin kalınlığı = sprite'taki " +
             "bevel'in kaç yüzdesi (tile boyutunun oranı). Sprite'taki bevel genişliğiyle EŞLEŞMELİ. " +
             "Bevel ince/kalın görünüyorsa BUNU ayarla.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float borderThicknessRatio = 0.18f;
    [Tooltip("Komşu hücrelerde ayrı RawImage edge parçalarının arada saç teli boşluk bırakmaması için küçük bindirme.")]
    [SerializeField] private float edgeJoinOverlapPixels = 1.5f;
    [Tooltip("Interior patch bevel altına kaç piksel taşsın. İnce board rengi çizgilerini kapatır.")]
    [SerializeField] private float interiorBleedPixels = 2f;
    [Tooltip("Açık kenarda bevel'in ARKASINI opak iç dolguyla kaç kadar kaplasın (bevel genişliğinin oranı). " +
             "1 = tam bant (kenardaki ince board sızıntısını kapatır). Concave köşede taşma görürsen düşür.")]
    [Range(0f, 1f)]
    [SerializeField] private float underBevelFillRatio = 1f;
    [Tooltip("Kenar şeritlerinin köşe parçasının ALTINA kaç px uzayacağı. Köşe/birleşim noktasındaki " +
             "saç-teli boşluğu kapatır (border'ı kalınlaştırmadan). 1 px genelde yeter.")]
    [SerializeField] private float cornerJoinPixels = 1f;
    [Tooltip("DÜZ kenar birleşimlerinde (üst üste/yan yana hücreler) bevel'in komşuya kaç px taşacağı. " +
             "Hücre sınırındaki ufak kesintiyi kapatır. Yalnız düz devam eden birleşime gider — L/concave " +
             "köşeye VE fill'e dokunmaz, köşeyi kalınlaştırmaz. Boşluk kalırsa BUNU büyüt.")]
    [SerializeField] private float edgeJoinExtendPixels = 2f;
    [Tooltip("Kenar şeridi, sprite'ın yuvarlak köşesinden kaç KAYNAK px (990'lık sprite ölçeğinde) DAHA " +
             "içeriden örneklensin. 101 px köşe yarıçapının biraz ötesi. Birleşimlerde kalan minik 'köşe " +
             "ovali' hâlâ görünüyorsa BUNU büyüt (kenarları tam düzleştirir).")]
    [SerializeField] private float edgeStraightCropPixels = 8f;

    [Header("Hits")]
    [SerializeField] private int defaultMaxHits = 2;

    public Sprite  BorderedMudSprite => borderedMudSprite;   // stage-0 bevel (Sprite B)
    public Texture PlainMudTexture   => plainMudTexture;     // stage-0 interior fill
    public bool    UseFlatStage0Interior => useFlatStage0Interior;
    public Color   FlatStage0InteriorColor => flatStage0InteriorColor;
    public Vector2 Stage0InteriorOffsetPixels => stage0InteriorOffsetPixels;
    public int     DefaultMaxHits    => defaultMaxHits;

    private readonly Dictionary<int, MudCellView> viewsByCellIndex = new();
    private readonly Dictionary<Vector3Int, RawImage> cornerPatches = new();

    private BoardController board;
    private int gridWidth, gridHeight, tileSize;
    private Coroutine pendingBorderRefresh;
    private RectTransform overlayRoot;

    public void Init(BoardController board, int width, int height, int tileSize = 0)
    {
        this.board  = board;
        gridWidth   = width;
        gridHeight  = height;
        if (tileSize > 0) this.tileSize = tileSize;

        if (board != null)
        {
            board.ObstacleVisualChanged -= HandleVisualChanged;
            board.ObstacleVisualChanged += HandleVisualChanged;
        }
    }

    private void OnDestroy()
    {
        if (board != null) board.ObstacleVisualChanged -= HandleVisualChanged;
    }

    public void RegisterCell(int x, int y, MudCellView view, int remaining, int max)
    {
        int idx = CellIndex(x, y);
        viewsByCellIndex[idx] = view;
        if (view != null && overlayRoot == null)
            overlayRoot = view.transform.parent as RectTransform;
        view.SetMaxHits(max);

        // Stage-1+ (dark) assets; stage-0 (light) came via Init in GridSpawner.
        view.SetStageAssets(damagedBorderedMudSprite, damagedMudTexture, false, Color.white, Vector2.zero);
        if (tileSize > 0)
            // Kenar = mud sprite'ın 1/10'u (990px sprite → 99px): "10px ise her kenardan 1px kes"
            // kuralı. Default 80px (≈0.081) yerine 99px ile örnekle ki ekrandaki bevel şeridiyle
            // (borderThicknessRatio) tam örtüşsün, dikiş/oval kalmasın.
            view.Build(tileSize, borderThicknessRatio, edgeJoinOverlapPixels, interiorBleedPixels, underBevelFillRatio, cornerJoinPixels, edgeJoinExtendPixels, edgeStraightCropPixels,
                sourceBorderPixels: 99f);

        ApplyToView(view, remaining);
        QueueRefreshAllBorders();
    }

    private void ApplyToView(MudCellView view, int remaining)
    {
        if (remaining <= 0) { view.SetVisible(false); return; }

        int damageTaken = view.MaxHits - remaining;
        view.SetDamaged(damageTaken > 0);
        view.SetVisible(true);
    }

    public bool TryGetView(int x, int y, out MudCellView view)
        => viewsByCellIndex.TryGetValue(CellIndex(x, y), out view);

    public bool HasMudAt(int x, int y)
        => viewsByCellIndex.ContainsKey(CellIndex(x, y));

    private void HandleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.Mud) return;
        if (!viewsByCellIndex.TryGetValue(change.originIndex, out var view) || view == null) return;

        if (change.cleared)
        {
            view.Clear();
            viewsByCellIndex.Remove(change.originIndex);

            // Topoloji değişti (bir hücre kalktı) → sınırları frame sonunda tek full pass olarak
            // hesapla. Aynı cascade içinde birden çok mud event'i gelince ara komşuluk durumları
            // ekrana kısa süreli iç çizgi/bevel olarak yansıyordu.
            QueueRefreshAllBorders();
            return;
        }

        ApplyToView(view, change.remainingHits);

        // stage1→stage2 geçişi overlay topolojisini değiştirir (bu hücre artık overlay değil):
        // komşu overlay'lerin kenar/köşesini yeniden hesapla. Deferred + coalesced olduğu için
        // aynı cascade'deki çok sayıda hit tek geçişte toplanır.
        QueueRefreshAllBorders();
    }

    private void RefreshBordersAt(int x, int y)
    {
        int idx = CellIndex(x, y);
        if (!viewsByCellIndex.TryGetValue(idx, out var view) || view == null) return;

        // Additive bevel — SADECE DIŞ SINIR. Komşuluk STAGE-AWARE:
        //   • Overlay (stage1) hücrenin komşusu = SADECE başka bir overlay. stage2 (damaged)
        //     komşu, overlay için "mud değil" gibi davranır → aralarına kenar/köşe çizilir.
        //   • stage2 hücrenin KENDİ exposure'ı eskisi gibi (her mud komşu birleşir) — stage2
        //     görünümüne dokunmuyoruz.
        // Grid Y aşağı artar: y-1 = görsel üst, y+1 = görsel alt.
        bool overlay = !view.IsDamaged;
        view.SetExposed(
            top:    !SameMaterial(overlay, x,     y - 1),
            right:  !SameMaterial(overlay, x + 1, y    ),
            bottom: !SameMaterial(overlay, x,     y + 1),
            left:   !SameMaterial(overlay, x - 1, y    ),
            mudTL:   SameMaterial(overlay, x - 1, y - 1),
            mudTR:   SameMaterial(overlay, x + 1, y - 1),
            mudBL:   SameMaterial(overlay, x - 1, y + 1),
            mudBR:   SameMaterial(overlay, x + 1, y + 1));
    }

    // Overlay hücre için komşu yalnız overlay sayılır; damaged (stage2) hücre için eski davranış
    // (her mud komşu). Hit/damage mantığı DEĞİŞMEZ — bu yalnız görsel kenar/köşe hesabıdır.
    // NOT: Örtülü mud (EnergyContainer vb. altında) artık düz MudUnder değil, GERÇEK bir MudOverlay
    // hücresi olarak kaydediliyor (GridSpawner) → IsMudAt/IsOverlayAt onu zaten görür; ayrı veri
    // kontrolüne gerek yok. Gizleyen cover'lar (Safe/OBB) altındaki mud hiç kaydedilmez → dikiş doğru.
    private bool SameMaterial(bool overlayCell, int x, int y)
        => overlayCell ? IsOverlayAt(x, y) : IsMudAt(x, y);

    private bool IsMudAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        int idx = CellIndex(x, y);
        return viewsByCellIndex.TryGetValue(idx, out var v) && v != null;
    }

    // stage1 (overlay, hasarsız) mud hücresi mi? Köşe/kenar birleşimi yalnız bunları sayar.
    private bool IsOverlayAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        int idx = CellIndex(x, y);
        return viewsByCellIndex.TryGetValue(idx, out var v) && v != null && !v.IsDamaged;
    }

    // Tüm kayıtlı mud hücrelerinin bevel exposure'ını, TÜM komşular kesinleştikten sonra
    // tek yetkili geçişte yeniden hesaplar. Bulk init'teki artımlı komşu-refresh bazı sınır
    // hücrelerinde (kolonun/satırın son ekleneni) yakınsamayıp bayat exposure bırakıyordu
    // (izole "kutu" bevel'i). Bu geçiş sıra/edge-case bağımlılığını tamamen kaldırır.
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

        if (pendingBorderRefresh != null)
            return;

        pendingBorderRefresh = StartCoroutine(CoRefreshAllBordersNextFrame());
    }

    private IEnumerator CoRefreshAllBordersNextFrame()
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
            var view = kv.Value;
            if (view == null) continue;

            view.Clear();
            Destroy(view.gameObject);
        }

        viewsByCellIndex.Clear();
        foreach (var kv in cornerPatches)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }

        cornerPatches.Clear();
        overlayRoot = null;
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;

    private void RefreshCornerPatches()
    {
        if (overlayRoot == null || tileSize <= 0 || gridWidth <= 0 || gridHeight <= 0)
            return;

        for (int cy = 1; cy < gridHeight; cy++)
        for (int cx = 1; cx < gridWidth; cx++)
        {
            // Köşe yalnız OVERLAY birleşiminde oluşur. stage2 hücre burada "boş" gibi sayılır,
            // böylece iki overlay arasındaki damaged hücrenin üstüne yanlış köşe çizilmez.
            bool nw = IsOverlayAt(cx - 1, cy - 1);
            bool ne = IsOverlayAt(cx,     cy - 1);
            bool sw = IsOverlayAt(cx - 1, cy);
            bool se = IsOverlayAt(cx,     cy);

            int count = (nw ? 1 : 0) + (ne ? 1 : 0) + (sw ? 1 : 0) + (se ? 1 : 0);
            if (count != 3)
                continue;

            int missingQuadrant = !nw ? 0 : !ne ? 1 : !sw ? 2 : 3;
            ShowCornerPatch(cx, cy, missingQuadrant);
        }
    }

    // missingQuadrant: 0=NW boş, 1=NE boş, 2=SW boş, 3=SE boş.
    // Sprite canvas'ı 1 hücre kabul edilir; pivot, sprite'ın mud köşesini grid vertex'ine kilitler.
    private void ShowCornerPatch(int gridCornerX, int gridCornerY, int missingQuadrant)
    {
        Sprite sprite;
        Vector2 pivot;
        Vector2 offset;
        switch (missingQuadrant)
        {
            case 0:
                sprite = cornerBR;
                pivot = new Vector2(1f, 0f);
                // MCRB: vertexten sağa ve aşağı açılır.
                offset = cornerOffsetBR + new Vector2(cornerInsetPixels, cornerInsetPixels);
                break;
            case 1:
                sprite = cornerBL;
                pivot = new Vector2(0f, 0f);
                // MCLB: vertexten sola ve aşağı açılır.
                offset = cornerOffsetBL + new Vector2(-cornerInsetPixels, cornerInsetPixels);
                break;
            case 2:
                sprite = cornerTR;
                pivot = new Vector2(1f, 1f);
                // MCRT: vertexten sağa ve yukarı açılır.
                offset = cornerOffsetTR + new Vector2(cornerInsetPixels, -cornerInsetPixels);
                break;
            default:
                sprite = cornerTL;
                pivot = new Vector2(0f, 1f);
                // MCLT: vertexten sola ve yukarı açılır.
                offset = cornerOffsetTL + new Vector2(-cornerInsetPixels, -cornerInsetPixels);
                break;
        }

        if (sprite == null || sprite.texture == null)
            return;

        var key = new Vector3Int(gridCornerX, gridCornerY, missingQuadrant);
        var img = GetOrCreateCornerPatch(key);
        if (img == null)
            return;

        // Köşe sprite'ı mudoverlay ile AYNI boyda (tam 1 hücre); nub tuvalin içinde zaten 0.2
        // gömülü. İmajı ASLA büyütmüyoruz — konum yalnız inset/offset ile ayarlanır.
        float size = tileSize;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;
        rt.localEulerAngles = Vector3.zero;
        rt.anchoredPosition = new Vector2(
            gridCornerX * tileSize + offset.x,
            -gridCornerY * tileSize - offset.y);
        rt.sizeDelta = new Vector2(size, size);

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

        if (overlayRoot == null)
            return null;

        var go = new GameObject($"MudCornerPatch_{key.x}_{key.y}_{key.z}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(overlayRoot, false);
        var img = go.GetComponent<RawImage>();
        img.raycastTarget = false;
        cornerPatches[key] = img;
        return img;
    }

    private static Rect SpriteUV(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return new Rect(0f, 0f, 1f, 1f);

        Rect tr = sprite.textureRect;
        float tw = sprite.texture.width;
        float th = sprite.texture.height;
        return new Rect(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th);
    }
}
