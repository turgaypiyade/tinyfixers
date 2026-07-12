using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility methods for board cell queries and marking during special resolution.
/// Extracted from SpecialResolver to remove duplicated board-traversal logic.
/// </summary>
public static class SpecialCellUtils
{
    public static void MarkAffectedCell(ResolutionContext ctx, int x, int y, BoardController board)
    {
        if (ctx == null || board == null) return;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (!SpecialUtils.CanAffectCell(board, x, y)) return;

        var cell = new Vector2Int(x, y);

        if (ctx.AffectedCells != null)
            ctx.AffectedCells.Add(cell);

        if (ctx.ImpactCells != null)
            ctx.ImpactCells.Add(cell);
    }
    

    public static void MarkAffectedCell(ResolutionContext ctx, TileView tile, BoardController board)
    {
        if (tile == null) return;
        MarkAffectedCell(ctx, tile.X, tile.Y, board);
    }

    /// <summary>
    /// Movable obstacle (HelmetPorcelain/plastik/coin/kalkan vb.) bir hücrede mi?
    /// Öyleyse hücreyi SADECE ImpactCells'e ekler (obstacle hasarı) ve true döner — çağıran
    /// continue etmeli, tile'ı Affected'a EKLEMEMELİ. Aksi halde tile-clear olur, cell
    /// clearedObstacleCellsThisPass'e girer ve obstacle hasarı BoardAnimator dedup'ında bastırılır
    /// → movable sağ kalır. Bu tek nokta, pulse/override toplama kodlarındaki "movable'a hasar yok"
    /// bug sınıfını kapatır. (Line'lar line-sweep ile ayrıca vurur; onlar bunu çağırmaz.)
    /// </summary>
    public static bool TryRouteMovableToImpact(ResolutionContext ctx, BoardController board, int x, int y)
    {
        return TryRouteMovableToImpact(board, x, y, ctx?.ImpactCells);
    }

    public static bool TryRouteMovableToImpact(BoardController board, int x, int y, ICollection<Vector2Int> impactCells)
    {
        var obstacles = board != null ? board.ObstacleStateService : null;
        if (obstacles == null || !obstacles.IsMovableObstacleAt(x, y))
            return false;

        impactCells?.Add(new Vector2Int(x, y));
        return true;
    }

    public static bool TryAddObstacleImpact(BoardController board, int x, int y, ICollection<Vector2Int> impactCells)
    {
        if (board == null || impactCells == null)
            return false;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        var obstacles = board.ObstacleStateService;
        if (obstacles == null || !obstacles.HasObstacleAt(x, y))
            return false;

        impactCells.Add(new Vector2Int(x, y));
        return true;
    }

    /// <summary>
    /// HatLauncher / EnergyContainer cells are permanent OverTileBlockers, so CanAffectCell
    /// rejects them and they never enter a special's affected/impact set. But a special whose
    /// footprint covers an emitter must still make it emit. We fire the hit DIRECTLY here (context
    /// SpecialActivation) instead of routing it through the merged clear: each special activation
    /// visits a covered cell exactly once, so chained specials (pulse+pulse+override) each pay out
    /// and the energy STACKS. Cascade/normal-match adjacency hits stay capped per move in
    /// ObstacleStateService, so a single move still can't drain the whole goal.
    /// Returns true when the cell was an emitter (caller should treat it as handled).
    /// </summary>
    public static bool TryMarkEmitterImpact(ResolutionContext ctx, BoardController board, int x, int y)
    {
        if (board == null)
            return false;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        var obstacles = board.ObstacleStateService;
        if (obstacles == null)
            return false;

        var id = obstacles.GetObstacleIdAt(x, y);
        if (id != ObstacleId.HatLauncher && id != ObstacleId.EnergyContainer)
            return false;

        board.ApplyObstacleDamageAt(x, y, ObstacleHitContext.SpecialActivation);
        return true;
    }

    public static void AddSquare(HashSet<TileView> matches, ResolutionContext ctx, BoardController board,
        int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!SpecialUtils.CanAffectCell(board, x, y))
                {
                    TryMarkEmitterImpact(ctx, board, x, y);
                    continue;
                }
                // Movable obstacle → obstacle hasarı (ImpactCells), tile-clear değil. Bug sınıfı tek noktada.
                if (TryRouteMovableToImpact(ctx, board, x, y))
                    continue;
                MarkAffectedCell(ctx, x, y, board);
                if (SpecialUtils.CanTargetTileContent(board, x, y) && board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    public static void AddAllTiles(HashSet<TileView> matches, ResolutionContext ctx, BoardController board)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(board, x, y))
                {
                    TryMarkEmitterImpact(ctx, board, x, y);
                    continue;
                }
                // Movable obstacle → obstacle hasarı (ImpactCells), tile-clear değil. Bug sınıfı tek noktada.
                if (TryRouteMovableToImpact(ctx, board, x, y))
                    continue;
                MarkAffectedCell(ctx, x, y, board);
                if (SpecialUtils.CanTargetTileContent(board, x, y) && board.Tiles[x, y] != null)
                    matches.Add(board.Tiles[x, y]);
            }
    }

    public static void AddAllOfType(
      HashSet<TileView> matches,
      ResolutionContext ctx,
      BoardController board,
      TileType type,
      bool excludeSpecials = false)
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (!SpecialUtils.CanTargetTileContent(board, x, y))
                    continue;

                var t = board.Tiles[x, y];
                if (t == null)
                    continue;

                if (board.ObstacleStateService != null)
                {
                    // Movable obstacle üstündeki tile'ları override hedef listesine alma.
                    if (board.ObstacleStateService.IsMovableObstacleAt(x, y))
                        continue;

                }

                if (!t.GetTileType().Equals(type))
                    continue;

                if (excludeSpecials && t.GetSpecial() != TileSpecial.None)
                    continue;

                MarkAffectedCell(ctx, x, y, board);
                matches.Add(t);
            }
        }
    }

    public static void CollectAllOfType(
        List<TileView> buffer,
        BoardController board,
        TileType type,
        bool excludeSpecials)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (!SpecialUtils.CanTargetTileContent(board, x, y))
                    continue;

                var t = board.Tiles[x, y];
                if (t == null)
                    continue;

                if (board.ObstacleStateService != null)
                {
                    // Movable obstacle üstündeki tile'ları override hedef listesine alma.
                    if (board.ObstacleStateService.IsMovableObstacleAt(x, y))
                        continue;

                }

                if (!t.GetTileType().Equals(type))
                    continue;

                if (excludeSpecials && t.GetSpecial() != TileSpecial.None)
                    continue;

                buffer.Add(t);
            }
        }
    }
    public static void SyncAfterSpecialChange(BoardController board, TileView tile)
    {
        if (tile == null) return;
        board.SyncTileData(tile.X, tile.Y);
    }
}
