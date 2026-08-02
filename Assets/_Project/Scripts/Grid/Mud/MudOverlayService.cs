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

    [Header("Bevel Width")]
    [Tooltip("Additive bevel: AÇIK kenarlara çizilen bevel şeridinin kalınlığı = sprite'taki " +
             "bevel'in kaç yüzdesi (tile boyutunun oranı). Sprite'taki bevel genişliğiyle EŞLEŞMELİ. " +
             "Bevel ince/kalın görünüyorsa BUNU ayarla.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float borderThicknessRatio = 0.18f;

    [Header("Hits")]
    [SerializeField] private int defaultMaxHits = 2;

    public Sprite  BorderedMudSprite => borderedMudSprite;   // stage-0 bevel (Sprite B)
    public Texture PlainMudTexture   => plainMudTexture;     // stage-0 interior fill
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
        view.SetStageAssets(damagedBorderedMudSprite, damagedMudTexture);
        if (tileSize > 0)
            view.Build(tileSize, borderThicknessRatio);

        ApplyToView(view, remaining);
    }

    private void ApplyToView(MudCellView view, int remaining)
    {
        if (remaining <= 0) { view.SetVisible(false); return; }

        int damageTaken = view.MaxHits - remaining;
        view.SetDamaged(damageTaken > 0);
        view.SetVisible(true);

        // Refresh this cell's exposure + all 4 neighbours (their exposure depends on our stage).
        RefreshBordersAt(view.GridX,     view.GridY    );
        RefreshBordersAt(view.GridX - 1, view.GridY    );
        RefreshBordersAt(view.GridX + 1, view.GridY    );
        RefreshBordersAt(view.GridX,     view.GridY - 1);
        RefreshBordersAt(view.GridX,     view.GridY + 1);
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

            int cx = change.originIndex % gridWidth;
            int cy = change.originIndex / gridWidth;
            RefreshBordersAt(cx - 1, cy);
            RefreshBordersAt(cx + 1, cy);
            RefreshBordersAt(cx, cy - 1);
            RefreshBordersAt(cx, cy + 1);
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
            left:   !IsMudAt(x - 1, y    ));
    }

    private bool IsMudAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        return viewsByCellIndex.TryGetValue(CellIndex(x, y), out var v) && v != null;
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
