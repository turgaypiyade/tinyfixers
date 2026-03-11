using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static utility methods for board cell queries and marking during special resolution.
/// Extracted from SpecialResolver to remove duplicated board-traversal logic.
/// </summary>
public static class SpecialCellUtils
{
    public static bool CanAffectCell(BoardController board, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!board.Holes[x, y])
            return true;

        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    public static void MarkAffectedCell(ResolutionContext ctx, int x, int y, BoardController board)
    {
        if (ctx.AffectedCells == null) return;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (!CanAffectCell(board, x, y)) return;
        ctx.AffectedCells.Add(new Vector2Int(x, y));
    }

    public static void MarkAffectedCell(ResolutionContext ctx, TileView tile, BoardController board)
    {
        if (tile == null) return;
        MarkAffectedCell(ctx, tile.X, tile.Y, board);
    }

    public static void AddSquare(HashSet<TileView> matches, ResolutionContext ctx, BoardController board,
        int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
                if (!CanAffectCell(board, x, y)) continue;
                MarkAffectedCell(ctx, x, y, board);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    public static void AddAllTiles(HashSet<TileView> matches, ResolutionContext ctx, BoardController board)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanAffectCell(board, x, y)) continue;
                MarkAffectedCell(ctx, x, y, board);
                if (board.Tiles[x, y] != null) matches.Add(board.Tiles[x, y]);
            }
    }

    public static void AddAllOfType(HashSet<TileView> matches, ResolutionContext ctx, BoardController board,
        TileType type, bool excludeSpecials = false)
    {
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanAffectCell(board, x, y)) continue;
                var t = board.Tiles[x, y];
                if (t == null) continue;
                if (!t.GetTileType().Equals(type)) continue;
                if (excludeSpecials && t.GetSpecial() != TileSpecial.None) continue;
                MarkAffectedCell(ctx, x, y, board);
                matches.Add(t);
            }
    }

    public static void CollectAllOfType(List<TileView> buffer, BoardController board,
        TileType type, bool excludeSpecials)
    {
        if (buffer == null) return;
        buffer.Clear();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!CanAffectCell(board, x, y)) continue;
                var t = board.Tiles[x, y];
                if (t == null) continue;
                if (!t.GetTileType().Equals(type)) continue;
                if (excludeSpecials && t.GetSpecial() != TileSpecial.None) continue;
                buffer.Add(t);
            }
    }

    public static void SyncAfterSpecialChange(BoardController board, TileView tile)
    {
        if (tile == null) return;
        board.SyncTileData(tile.X, tile.Y);
    }
}