using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpecialChainExecutor
{
    public static void ExecuteFromAffected(
        ResolutionContext ctx,
        Action<ResolutionContext, TileView, TileView> activateSpecial,
        params TileView[] excludedTiles)
    {
        if (ctx == null || activateSpecial == null)
            return;

        var excluded = BuildExcludedCells(excludedTiles);
        var pending = new Queue<TileView>();

        EnqueueAffectedSpecials(ctx, pending, excluded);

        while (pending.Count > 0)
        {
            var tile = pending.Dequeue();
            if (tile == null)
                continue;

            var cell = new Vector2Int(tile.X, tile.Y);
            ctx.Queued.Remove(cell);

            if (excluded.Contains(cell) || ctx.Processed.Contains(cell))
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

    private static void EnqueueAffectedSpecials(ResolutionContext ctx, Queue<TileView> pending, HashSet<Vector2Int> excluded)
    {
        foreach (var tile in ctx.Affected)
            TryQueue(ctx, pending, tile, excluded);
    }

    private static void TryQueue(ResolutionContext ctx, Queue<TileView> pending, TileView tile, HashSet<Vector2Int> excluded)
    {
        if (tile == null || tile.GetSpecial() == TileSpecial.None)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);

        if (excluded.Contains(cell) || ctx.Processed.Contains(cell) || ctx.Queued.Contains(cell))
            return;

        ctx.Queued.Add(cell);
        pending.Enqueue(tile);
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
