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

    public static void ExecuteFromAffectedViaQueue(
        BoardController board,
        ResolutionContext ctx,
        Action<ResolutionContext, TileView, TileView> enqueueActivation,
        Action<ResolutionContext> processQueuedActivations,
        Action<string> debugLog = null,
        params TileView[] excludedTiles)
    {
        if (board == null || ctx == null || enqueueActivation == null || processQueuedActivations == null)
            return;

        var excluded = BuildExcludedCells(excludedTiles);
        int enqueuedCount = 0;

        foreach (var tile in ctx.Affected)
        {
            if (tile == null || tile.GetSpecial() == TileSpecial.None)
                continue;

            var cell = new Vector2Int(tile.X, tile.Y);
            if (excluded.Contains(cell) || ctx.Processed.Contains(cell))
                continue;

            enqueueActivation(ctx, tile, null);
            enqueuedCount++;
            debugLog?.Invoke($"[LineVHPulseCoreCombo] Chain enqueue: {tile.GetSpecial()} at {cell}.");
        }

        debugLog?.Invoke($"[LineVHPulseCoreCombo] Processing queued chain activations. Seed count={enqueuedCount}.");
        processQueuedActivations(ctx);
    }

    public static void ExecuteFromAffectedCellsViaQueue(
        BoardController board,
        ResolutionContext ctx,
        Action<ResolutionContext, TileView, TileView> enqueueActivation,
        Action<ResolutionContext> processQueuedActivations,
        Action<string> debugLog = null,
        params TileView[] excludedTiles)
    {
        if (board == null || ctx == null || enqueueActivation == null || processQueuedActivations == null)
            return;

        var excluded = BuildExcludedCells(excludedTiles);
        int enqueuedCount = 0;

        // Prefer deterministic board-cell scan from affected cells.
        foreach (var cell in ctx.AffectedCells)
        {
            if (excluded.Contains(cell) || ctx.Processed.Contains(cell))
                continue;

            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
            {
                debugLog?.Invoke($"[LineVHPulseCoreCombo] Chain skip: no tile at {cell}.");
                continue;
            }

            var special = tile.GetSpecial();
            if (special == TileSpecial.None)
                continue;

            if (ctx.Queued.Contains(cell))
                continue;

            enqueueActivation(ctx, tile, null);
            enqueuedCount++;
            debugLog?.Invoke($"[LineVHPulseCoreCombo] Chain enqueue(fromCells): {special} at {cell}.");
        }

        // Fallback: there are cases where AffectedCells may miss injected tiles, keep old behavior too.
        foreach (var tile in ctx.Affected)
        {
            if (tile == null)
                continue;

            var cell = new Vector2Int(tile.X, tile.Y);
            if (excluded.Contains(cell) || ctx.Processed.Contains(cell))
                continue;

            var special = tile.GetSpecial();
            if (special == TileSpecial.None)
                continue;

            if (ctx.Queued.Contains(cell))
                continue;

            enqueueActivation(ctx, tile, null);
            enqueuedCount++;
            debugLog?.Invoke($"[LineVHPulseCoreCombo] Chain enqueue(fromAffected): {special} at {cell}.");
        }

        debugLog?.Invoke($"[LineVHPulseCoreCombo] Processing queued chain activations. Seed count={enqueuedCount}.");
        processQueuedActivations(ctx);
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
