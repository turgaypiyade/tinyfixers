using System.Collections.Generic;
using UnityEngine;

public class MudOverlayService : MonoBehaviour
{
    [Header("Mud Texture & Tint")]
    [SerializeField] private Texture sharedMudTexture;
    [SerializeField] private Texture[] stageTextures;
    [SerializeField] private Color darkTint  = new Color(0.35f, 0.25f, 0.18f, 1f);
    [SerializeField] private Color lightTint = new Color(0.65f, 0.50f, 0.35f, 1f);
    [SerializeField] private int defaultMaxHits = 2;
    [Range(0f, 1f)]
    [SerializeField] private float minAlphaAtLastStage = 0.7f;

    [Header("Mud Edge Borders")]
    [Tooltip("Yatay kenar sprite'ı — üst/alt için. Altı koyu, üstü şeffaf.")]
    [SerializeField] private Sprite mudEdgeSpriteH;
    [Tooltip("Dikey kenar sprite'ı — sol/sağ için. Solu koyu, sağı şeffaf. " +
             "Boş bırakılırsa H kullanılır.")]
    [SerializeField] private Sprite mudEdgeSpriteV;
    [Tooltip("Kenar kalınlığı — tile boyutunun yüzdesi.")]
    [Range(0.05f, 0.40f)]
    [SerializeField] private float borderThicknessRatio = 0.18f;
    [Tooltip("0 = tamamen hücre içinde  |  0.5 = kenar ortasında  |  1 = tamamen dışarıda.")]
    [Range(0f, 1f)]
    [SerializeField] private float borderOutwardRatio = 0.5f;

    public float MinAlphaAtLastStage => minAlphaAtLastStage;

    private readonly Dictionary<int, MudCellView> viewsByCellIndex = new();

    private BoardController board;
    private int gridWidth, gridHeight, tileSize;

    public Texture SharedMudTexture  => sharedMudTexture;
    public Color   DarkTint          => darkTint;
    public Color   LightTint         => lightTint;
    public int     DefaultMaxHits    => defaultMaxHits;
    public bool    HasPerStageTextures => stageTextures != null && stageTextures.Length > 0;

    public Texture GetTextureForStage(int remaining, int max)
    {
        if (!HasPerStageTextures) return sharedMudTexture;
        int idx = Mathf.Clamp(Mathf.Max(0, max - remaining), 0, stageTextures.Length - 1);
        return stageTextures[idx];
    }

    public void Init(BoardController board, int width, int height, int tileSize = 0)
    {
        this.board     = board;
        gridWidth      = width;
        gridHeight     = height;
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
        ApplyToView(view, remaining, max);

        if (mudEdgeSpriteH != null && tileSize > 0)
            view.InitBorders(mudEdgeSpriteH, mudEdgeSpriteV,
                             borderThicknessRatio, tileSize, borderOutwardRatio);

        RefreshBordersAt(x,     y    );
        RefreshBordersAt(x - 1, y    );
        RefreshBordersAt(x + 1, y    );
        RefreshBordersAt(x,     y - 1);
        RefreshBordersAt(x,     y + 1);
    }

    private void ApplyToView(MudCellView view, int remaining, int max)
    {
        if (HasPerStageTextures)
            view.ApplyStage(GetTextureForStage(remaining, max), remaining > 0);
        else
            view.SetDamageLevel(remaining, max);
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

        ApplyToView(view, change.remainingHits, defaultMaxHits);
    }

    private void RefreshBordersAt(int x, int y)
    {
        int idx = CellIndex(x, y);
        if (!viewsByCellIndex.TryGetValue(idx, out var view) || view == null) return;

        // Grid Y increases downward: y-1 = visual top, y+1 = visual bottom
        view.SetBorders(
            top:    !HasMudNeighbor(x,     y - 1),
            right:  !HasMudNeighbor(x + 1, y    ),
            bottom: !HasMudNeighbor(x,     y + 1),
            left:   !HasMudNeighbor(x - 1, y    ));
    }

    private bool HasMudNeighbor(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
        return viewsByCellIndex.ContainsKey(CellIndex(x, y));
    }

    private int CellIndex(int x, int y) => y * gridWidth + x;
}
