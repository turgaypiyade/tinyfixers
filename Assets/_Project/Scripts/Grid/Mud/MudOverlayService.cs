using System.Collections.Generic;
using UnityEngine;

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

    private BoardController board;
    private int gridWidth, gridHeight, tileSize;

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
        view.SetMaxHits(max);

        // Stage-1+ (dark) assets; stage-0 (light) came via Init in GridSpawner.
        view.SetStageAssets(damagedBorderedMudSprite, damagedMudTexture, false, Color.white, Vector2.zero);
        if (tileSize > 0)
            view.Build(tileSize, borderThicknessRatio, edgeJoinOverlapPixels, interiorBleedPixels, underBevelFillRatio, cornerJoinPixels, edgeJoinExtendPixels, edgeStraightCropPixels);

        ApplyToView(view, remaining);
    }

    private void ApplyToView(MudCellView view, int remaining)
    {
        if (remaining <= 0) { view.SetVisible(false); return; }

        int damageTaken = view.MaxHits - remaining;
        view.SetDamaged(damageTaken > 0);
        view.SetVisible(true);

        // Refresh this cell + all 8 neighbours. Orthogonals share an edge with us; diagonals'
        // straight-run edge extension depends on whether we (their diagonal) exist.
        int gx = view.GridX, gy = view.GridY;
        RefreshBordersAt(gx,     gy    );
        RefreshBordersAt(gx - 1, gy    );
        RefreshBordersAt(gx + 1, gy    );
        RefreshBordersAt(gx,     gy - 1);
        RefreshBordersAt(gx,     gy + 1);
        RefreshBordersAt(gx - 1, gy - 1);
        RefreshBordersAt(gx + 1, gy - 1);
        RefreshBordersAt(gx - 1, gy + 1);
        RefreshBordersAt(gx + 1, gy + 1);
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

            // Topoloji değişti (bir hücre kalktı) → TÜM sınırları yetkili şekilde yeniden hesapla.
            // Artımlı 8-komşu refresh, bir LineH gibi AYNI PASS'te çok sayıda mud temizlendiğinde
            // (init'teki toplu kayıt gibi) sıra-bağımlı yakınsamıyor, sınır hücrelerinde bayat
            // "izole kutu" bevel'i bırakıyordu. Full refresh bu bug sınıfını tamamen kapatır.
            RefreshAllBorders();
            return;
        }

        ApplyToView(view, change.remainingHits);
    }

    private void RefreshBordersAt(int x, int y)
    {
        int idx = CellIndex(x, y);
        if (!viewsByCellIndex.TryGetValue(idx, out var view) || view == null) return;

        // Additive bevel — SADECE DIŞ SINIR: bir kenar yalnızca komşuda HİÇ mud yoksa açıktır
        // (stage'e bakılmaz). Böylece blob tek temiz outline alır; stage farkı yalnız dolgu
        // rengiyle görünür (açık→koyu), ara çerçeve oluşmaz.
        // Grid Y aşağı artar: y-1 = görsel üst, y+1 = görsel alt.
        view.SetExposed(
            top:    !IsMudAt(x,     y - 1),
            right:  !IsMudAt(x + 1, y    ),
            bottom: !IsMudAt(x,     y + 1),
            left:   !IsMudAt(x - 1, y    ),
            mudTL:   IsMudAt(x - 1, y - 1),
            mudTR:   IsMudAt(x + 1, y - 1),
            mudBL:   IsMudAt(x - 1, y + 1),
            mudBR:   IsMudAt(x + 1, y + 1));
    }

    private bool IsMudAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        return viewsByCellIndex.TryGetValue(CellIndex(x, y), out var v) && v != null;
    }

    // Tüm kayıtlı mud hücrelerinin bevel exposure'ını, TÜM komşular kesinleştikten sonra
    // tek yetkili geçişte yeniden hesaplar. Bulk init'teki artımlı komşu-refresh bazı sınır
    // hücrelerinde (kolonun/satırın son ekleneni) yakınsamayıp bayat exposure bırakıyordu
    // (izole "kutu" bevel'i). Bu geçiş sıra/edge-case bağımlılığını tamamen kaldırır.
    public void RefreshAllBorders()
    {
        foreach (var kv in viewsByCellIndex)
            RefreshBordersAt(kv.Key % gridWidth, kv.Key / gridWidth);
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
