using System.Collections.Generic;
using UnityEngine;

public class MudOverlayService : MonoBehaviour
{
    [Header("Mud Sprites & Texture")]
    [Tooltip("Sprite B — tüm 4 kenarında bevel/shadow baked-in olan mud sprite'ı. Her zaman base olarak kullanılır.")]
    [SerializeField] private Sprite borderedMudSprite;
    [Tooltip("Sprite A'nın texture'ı — kenarsız düz mud. Stage-0 komşular arasında bevel'ı kapatmak için UV-mapped cover olarak kullanılır.")]
    [SerializeField] private Texture plainMudTexture;

    [Header("Border Cover Thickness")]
    [Tooltip("Sprite B'deki bevel kalınlığı — tile boyutunun yüzdesi. Sprite art ile eşleşmeli.")]
    [Range(0.05f, 0.40f)]
    [SerializeField] private float borderThicknessRatio = 0.18f;

    [Header("Hits")]
    [SerializeField] private int defaultMaxHits = 2;

    public Sprite BorderedMudSprite => borderedMudSprite;
    public int    DefaultMaxHits    => defaultMaxHits;

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

        if (plainMudTexture != null && tileSize > 0)
            view.InitCovers(plainMudTexture, borderThicknessRatio, tileSize);

        // Overlay must be created after covers so it renders on top.
        view.InitDamageOverlay();

        ApplyToView(view, remaining);
    }

    private void ApplyToView(MudCellView view, int remaining)
    {
        if (remaining <= 0) { view.SetDamageLevel(0, view.MaxHits); return; }

        int damageTaken = view.MaxHits - remaining;
        bool isDamaged  = damageTaken > 0;

        Sprite overlay = null;
        if (isDamaged)
        {
            var def = board?.LevelData?.obstacleLibrary?.Get(ObstacleId.Mud);
            overlay = def?.GetSpriteForRemainingHits(remaining);
        }

        view.SetDamageState(isDamaged, overlay);
        view.SetDamageLevel(remaining, view.MaxHits);

        // Refresh this cell's covers + all 4 neighbours (their cover state may depend on our stage).
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

        if (view.IsDamaged)
        {
            // Stage 1+: covers kapalı, bevel her yerde görünür; damage overlay görünümü yönetir.
            view.SetCovers(false, false, false, false);
            return;
        }

        // Stage 0: cover yalnızca aynı stage'deki (hasarsız) mud komşularına karşı açılır.
        // Grid Y aşağı artar: y-1 = görsel üst, y+1 = görsel alt.
        view.SetCovers(
            top:    IsUndamagedMudAt(x,     y - 1),
            right:  IsUndamagedMudAt(x + 1, y    ),
            bottom: IsUndamagedMudAt(x,     y + 1),
            left:   IsUndamagedMudAt(x - 1, y    ));
    }

    private bool IsUndamagedMudAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        if (!viewsByCellIndex.TryGetValue(CellIndex(x, y), out var v) || v == null) return false;
        return !v.IsDamaged;
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
