using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs end-of-level bonus LineH/LineV tiles through the same queued activation
/// model used by OverrideSpecialized line batches instead of constructing a
/// booster-like row/column clear by hand.
///
/// This keeps the native LineH/LineV path as the source of truth for affected
/// cells, chain specials, lightning strikes and LineTravel timing.
/// </summary>
public static class BonusLineOverrideStyleRunner
{
    public static IEnumerator Run(
        BoardController board,
        IReadOnlyList<BoardController.BonusLinePlacement> placements,
        Func<bool> hardSkipRequested = null)
    {
        if (board == null || placements == null || placements.Count == 0)
            yield break;

        if (hardSkipRequested != null && hardSkipRequested())
            yield break;

        var activations = CollectActivations(board, placements);
        if (activations.Count == 0)
            yield break;

        ActionSequencer sequencer = board.GetComponent<ActionSequencer>();
        if (sequencer == null)
            sequencer = board.gameObject.AddComponent<ActionSequencer>();
        sequencer.Initialize(board);

        var ctx = new ResolutionContext();
        var services = new RuntimeServices(board);

        bool previousSpecialPhase = board.IsSpecialActivationPhase;

        board.BeginBusy();
        board.IsSpecialActivationPhase = true;
        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;

        try
        {
            List<Vector2Int> pendingCells = ExtractCells(activations);

            yield return new PendingTriggeredSpecialScopeAction(pendingCells, true)
                .ExecuteVisuals(sequencer);

            yield return ExecuteActivationBatch(
                board,
                sequencer,
                ctx,
                services,
                activations);

            yield return new PendingTriggeredSpecialScopeAction(pendingCells, false)
                .ExecuteVisuals(sequencer);

            if (ctx.OverrideDeferredPulseExplosions.Count == 0)
                services.Implants.CleanupImplantedTiles(ctx);

            if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0)
                services.Visuals.FireOverrideOverrideSpecialVisuals(ctx.Affected, ctx.OverrideRadialClearDelays);

            if (hardSkipRequested == null || !hardSkipRequested())
                yield return board.ResolveBoardPublic();
        }
        finally
        {
            board.IsSpecialActivationPhase = previousSpecialPhase;
            board.EndBusy();
        }
    }

    private static List<ResolutionContext.SpecialActivation> CollectActivations(
        BoardController board,
        IReadOnlyList<BoardController.BonusLinePlacement> placements)
    {
        var result = new List<ResolutionContext.SpecialActivation>();
        var seen = new HashSet<Vector2Int>();

        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            var cell = new Vector2Int(p.x, p.y);

            if (!seen.Add(cell))
                continue;

            if (cell.x < 0 || cell.x >= board.Width ||
                cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            TileSpecial special = tile.GetSpecial();
            if (special != TileSpecial.LineH && special != TileSpecial.LineV)
                continue;

            result.Add(new ResolutionContext.SpecialActivation(cell, null));
        }

        result.Sort((a, b) =>
        {
            int y = a.cell.y.CompareTo(b.cell.y);
            if (y != 0) return y;
            return a.cell.x.CompareTo(b.cell.x);
        });

        return result;
    }

    private static List<Vector2Int> ExtractCells(
        List<ResolutionContext.SpecialActivation> activations)
    {
        var cells = new List<Vector2Int>(activations.Count);
        for (int i = 0; i < activations.Count; i++)
            cells.Add(activations[i].cell);
        return cells;
    }

    private static IEnumerator ExecuteActivationBatch(
        BoardController board,
        ActionSequencer sequencer,
        ResolutionContext ctx,
        RuntimeServices services,
        List<ResolutionContext.SpecialActivation> activations)
    {
        if (activations == null || activations.Count == 0)
            yield break;

        var currentBatchCells = ExtractCells(activations);
        board.ClearPendingTriggeredSpecialCells(currentBatchCells);

        var beforeBatch = CaptureSnapshot(ctx);

        for (int i = 0; i < activations.Count; i++)
        {
            var activation = activations[i];
            var cell = activation.cell;

            if (cell.x < 0 || cell.x >= board.Width ||
                cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            TileSpecial special = tile.GetSpecial();
            if (special != TileSpecial.LineH && special != TileSpecial.LineV)
                continue;

            ctx.Processed.Remove(cell);
            ctx.Queued.Remove(cell);

            // Match the visible behavior of normal solo LineV/LineH activation:
            // the source special is consumed and LineTravel owns the visual pass.
            SpecialVisualService.HideTileVisualForCombo(tile);

            TileView partner = null;
            if (activation.partnerCell.HasValue)
            {
                var partnerCell = activation.partnerCell.Value;
                if (partnerCell.x >= 0 && partnerCell.x < board.Width &&
                    partnerCell.y >= 0 && partnerCell.y < board.Height)
                {
                    partner = board.Tiles[partnerCell.x, partnerCell.y];
                }
            }

            services.Queue.EnqueueActivation(ctx, tile, partner);
        }

        services.Queue.ProcessQueue(ctx);

        // Same finalize order as LineVSpecial/LineHSpecial top-level activation:
        // first resolve override fan-out/implants, then build the clear payload.
        var fanoutActions = services.Fanout.ProcessFanout(ctx);
        if (fanoutActions != null)
        {
            for (int i = 0; i < fanoutActions.Count; i++)
            {
                if (fanoutActions[i] != null)
                    yield return fanoutActions[i].ExecuteVisuals(sequencer);
            }
        }

        var payload = BuildDeltaClearPayload(ctx, beforeBatch);
        if (payload.Action != null)
        {
            yield return payload.Action.ExecuteVisuals(sequencer);
        }
    }

    private static ClearSnapshot CaptureSnapshot(ResolutionContext ctx)
    {
        return new ClearSnapshot(
            new HashSet<TileView>(ctx.Affected),
            new HashSet<Vector2Int>(ctx.AffectedCells),
            new HashSet<TileView>(ctx.LightningVisualTargets),
            ctx.LightningLineStrikes.Count,
            ctx.ImpactCells.Count);
    }

    private static ClearPayload BuildDeltaClearPayload(
        ResolutionContext ctx,
        ClearSnapshot before)
    {
        var deltaTiles = new HashSet<TileView>();
        foreach (var tile in ctx.Affected)
        {
            if (tile != null && !before.Affected.Contains(tile))
                deltaTiles.Add(tile);
        }

        var deltaCells = new HashSet<Vector2Int>();
        foreach (var cell in ctx.AffectedCells)
        {
            if (!before.AffectedCells.Contains(cell))
                deltaCells.Add(cell);
        }

        var deltaLightningTargets = new HashSet<TileView>();
        foreach (var tile in ctx.LightningVisualTargets)
        {
            if (tile != null && !before.LightningTargets.Contains(tile))
                deltaLightningTargets.Add(tile);
        }

        List<LightningLineStrike> deltaStrikes = null;
        int strikeDelta = ctx.LightningLineStrikes.Count - before.StrikeCount;
        if (strikeDelta > 0)
            deltaStrikes = ctx.LightningLineStrikes.GetRange(before.StrikeCount, strikeDelta);

        List<Vector2Int> deltaImpacts = null;
        int impactDelta = ctx.ImpactCells.Count - before.ImpactCount;
        if (impactDelta > 0)
            deltaImpacts = ctx.ImpactCells.GetRange(before.ImpactCount, impactDelta);

        if (deltaTiles.Count == 0)
            return new ClearPayload(null, null, null);

        Dictionary<TileView, float> batchRadialDelays = null;
        if (ctx.OverrideRadialClearDelays != null)
        {
            foreach (var kv in ctx.OverrideRadialClearDelays)
            {
                if (!deltaTiles.Contains(kv.Key))
                    continue;

                if (batchRadialDelays == null)
                    batchRadialDelays = new Dictionary<TileView, float>();

                batchRadialDelays[kv.Key] = kv.Value;
            }
        }

        bool hasChainLightning = deltaStrikes != null && deltaStrikes.Count > 0;

        var action = new MatchClearAction(
            deltaTiles,
            doShake: true,
            animationMode: hasChainLightning && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: deltaCells.Count > 0 ? deltaCells : null,
            impactCells: deltaImpacts,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: hasChainLightning && deltaLightningTargets.Count > 0
                ? deltaLightningTargets
                : null,
            lightningLineStrikes: hasChainLightning ? deltaStrikes : null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: batchRadialDelays,
            isSpecialPhase: true,
            presentationPlan: null,
            enqueueCascadeOnComplete: false);

        return new ClearPayload(action, deltaTiles, deltaCells);
    }

    private sealed class RuntimeServices
    {
        public readonly SpecialVisualService Visuals;
        public readonly ActivationQueueProcessor Queue;
        public readonly SpecialImplantService Implants;
        public readonly SpecialFanoutService Fanout;

        public RuntimeServices(BoardController board)
        {
            var patchbotComboService = new PatchbotComboService(board);
            Visuals = new SpecialVisualService(board, board.boardAnimatorRef, patchbotComboService);
            var effects = new SpecialEffectOrchestrator(board);
            var dispatcher = new SpecialBehaviorDispatcher(board, patchbotComboService, Visuals, effects);
            Queue = new ActivationQueueProcessor(board, dispatcher);
            Implants = new SpecialImplantService(board, patchbotComboService, Visuals, Queue);
            Fanout = new SpecialFanoutService(board, Implants, Queue, Visuals);
            dispatcher.QueueProcessor = Queue;
        }
    }

    private readonly struct ClearSnapshot
    {
        public readonly HashSet<TileView> Affected;
        public readonly HashSet<Vector2Int> AffectedCells;
        public readonly HashSet<TileView> LightningTargets;
        public readonly int StrikeCount;
        public readonly int ImpactCount;

        public ClearSnapshot(
            HashSet<TileView> affected,
            HashSet<Vector2Int> affectedCells,
            HashSet<TileView> lightningTargets,
            int strikeCount,
            int impactCount)
        {
            Affected = affected;
            AffectedCells = affectedCells;
            LightningTargets = lightningTargets;
            StrikeCount = strikeCount;
            ImpactCount = impactCount;
        }
    }

    private readonly struct ClearPayload
    {
        public readonly MatchClearAction Action;
        public readonly HashSet<TileView> Tiles;
        public readonly HashSet<Vector2Int> Cells;

        public ClearPayload(
            MatchClearAction action,
            HashSet<TileView> tiles,
            HashSet<Vector2Int> cells)
        {
            Action = action;
            Tiles = tiles;
            Cells = cells;
        }
    }
}
