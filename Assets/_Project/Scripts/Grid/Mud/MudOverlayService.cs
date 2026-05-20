using System.Collections.Generic;
using UnityEngine;

/// Owns the seamless mud overlay. Mud lives in the existing ObstacleId.Mud
/// path, so the damage flow re-uses ApplyObstacleDamageAt; we listen to
/// ObstacleVisualChanged and translate it into per-cell alpha/tint updates.
public class MudOverlayService : MonoBehaviour
{
    [Header("Mud Texture & Tint")]
    [Tooltip("Tek texture: alfa fade ile damage stage'leri arasında geçiş yapılır.")]
    [SerializeField] private Texture sharedMudTexture;

    [Tooltip("Per-stage texture array. Index 0 = full dirt, son element = clear olmadan önceki son stage.\n" +
             "Doluysa stage geçişinde texture swap yapılır (alfa hep 1.0, arkaplan SIZMAZ).\n" +
             "Boş bırakılırsa sharedMudTexture + alfa fade kullanılır (eski davranış).")]
    [SerializeField] private Texture[] stageTextures;

    [SerializeField] private Color darkTint = new Color(0.35f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color lightTint = new Color(0.65f, 0.50f, 0.35f, 1f);

    [Tooltip("Used when a stage transition snapshot does not carry max-hits info.")]
    [SerializeField] private int defaultMaxHits = 2;

    [Tooltip("Son hit kala (cleared değil ama 1 hit önce) Mud'un sahip olacağı minimum alfa. " +
             "1.0 = hiç solmaz (sadece tint değişir), 0.0 = lineer fade. " +
             "Yüksek değer = grid çizgileri/arkaplan daha az görünür.")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlphaAtLastStage = 0.7f;

    public float MinAlphaAtLastStage => minAlphaAtLastStage;

    private readonly Dictionary<int, MudCellView> viewsByCellIndex = new();
    private BoardController board;
    private int gridWidth;
    private int gridHeight;

    public Texture SharedMudTexture => sharedMudTexture;
    public Color DarkTint => darkTint;
    public Color LightTint => lightTint;
    public int DefaultMaxHits => defaultMaxHits;
    public bool HasPerStageTextures => stageTextures != null && stageTextures.Length > 0;

    /// damage taken = max - remaining. Index 0 = full dirt, end = last visible stage.
    public Texture GetTextureForStage(int remaining, int max)
    {
        if (!HasPerStageTextures) return sharedMudTexture;
        int damageTaken = Mathf.Max(0, max - remaining);
        int idx = Mathf.Clamp(damageTaken, 0, stageTextures.Length - 1);
        return stageTextures[idx];
    }

    public void Init(BoardController board, int width, int height)
    {
        this.board = board;
        gridWidth = width;
        gridHeight = height;

        if (board != null)
        {
            board.ObstacleVisualChanged -= HandleVisualChanged;
            board.ObstacleVisualChanged += HandleVisualChanged;
        }
    }

    private void OnDestroy()
    {
        if (board != null)
            board.ObstacleVisualChanged -= HandleVisualChanged;
    }

    public void RegisterCell(int x, int y, MudCellView view, int remaining, int max)
    {
        int idx = CellIndex(x, y);
        viewsByCellIndex[idx] = view;
        ApplyToView(view, remaining, max);
    }

    private void ApplyToView(MudCellView view, int remaining, int max)
    {
        if (HasPerStageTextures)
        {
            view.ApplyStage(GetTextureForStage(remaining, max), remaining > 0);
        }
        else
        {
            view.SetDamageLevel(remaining, max);
        }
    }

    public bool TryGetView(int x, int y, out MudCellView view)
    {
        return viewsByCellIndex.TryGetValue(CellIndex(x, y), out view);
    }

    public bool HasMudAt(int x, int y)
    {
        return viewsByCellIndex.ContainsKey(CellIndex(x, y));
    }

    private void HandleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.Mud) return;

        if (!viewsByCellIndex.TryGetValue(change.originIndex, out var view) || view == null)
            return;

        if (change.cleared)
        {
            view.Clear();
            viewsByCellIndex.Remove(change.originIndex);
            return;
        }

        ApplyToView(view, change.remainingHits, defaultMaxHits);
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
