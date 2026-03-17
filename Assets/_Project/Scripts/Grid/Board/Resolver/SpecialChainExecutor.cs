using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpecialChainExecutor
{
    public static void ExecuteFromAffected(
        BoardController board,
        ResolutionContext ctx,
        Action<ResolutionContext, TileView, TileView> activateSpecial,
        params TileView[] excludedTiles)
    {
        if (board == null || ctx == null || activateSpecial == null)
            return;

        var excluded = BuildExcludedCells(excludedTiles);
        var pending = new Queue<Vector2Int>();

        EnqueueAffectedSpecials(ctx, pending, excluded);

        while (pending.Count > 0)
        {
            var cell = pending.Dequeue();
            ctx.Queued.Remove(cell);

            if (excluded.Contains(cell) || ctx.Processed.Contains(cell))
                continue;

            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            if (tile.GetSpecial() == TileSpecial.None)
                continue;

            activateSpecial(ctx, tile, null);
            ctx.Processed.Add(cell);

            if (!ctx.ChainExecutionOrder.Contains(cell))
                ctx.ChainExecutionOrder.Add(cell);

            EnqueueAffectedSpecials(ctx, pending, excluded);
        }
    }

    private static void EnqueueAffectedSpecials(ResolutionContext ctx, Queue<Vector2Int> pending, HashSet<Vector2Int> excluded)
    {
        foreach (var tile in ctx.Affected)
            TryQueue(ctx, pending, tile, excluded);
    }

    private static void TryQueue(ResolutionContext ctx, Queue<Vector2Int> pending, TileView tile, HashSet<Vector2Int> excluded)
    {
        if (tile == null || tile.GetSpecial() == TileSpecial.None)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);

        if (excluded.Contains(cell) || ctx.Processed.Contains(cell) || ctx.Queued.Contains(cell))
            return;

        ctx.Queued.Add(cell);
        pending.Enqueue(cell);
    }

    private static HashSet<Vector2Int> BuildExcludedCells(TileView[] excludedTiles)
    {
        var excluded = new HashSet<Vector2Int>();

        if (excludedTiles == null)
            return excluded;

        foreach (var tile in excludedTiles)
        {
            if (tile == null)
                continue;

            excluded.Add(new Vector2Int(tile.X, tile.Y));
        }

        return excluded;
    }
}
