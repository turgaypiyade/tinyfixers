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
    private const int PulseChainAreaHalf = 2;

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
        Debug.Log($"[BonusDebug] gate2 Run placements={placements.Count} activations={activations.Count}");
        if (activations.Count == 0)
            yield break;

        var lineCells = ExtractCells(activations);

        ActionSequencer sequencer = board.GetComponent<ActionSequencer>();
        if (sequencer == null)
            sequencer = board.gameObject.AddComponent<ActionSequencer>();
        sequencer.Initialize(board);

        var services = new RuntimeServices(board);

        bool previousSpecialPhase = board.IsSpecialActivationPhase;

        board.BeginBusy();
        board.IsSpecialActivationPhase = true;
        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;

        try
        {
            var chain = new SpecialChainRunner(
                board,
                new List<TileView>(),
                PulseChainAreaHalf,
                board.PulseChainCatchOverlap,
                services.ResolveOtherSpecialInline,
                simultaneousLineCells: lineCells);

            yield return chain.ExecuteVisuals(sequencer);

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

        Debug.Log($"[BonusDebug] gate3 delta tiles={deltaTiles.Count} strikes={(deltaStrikes != null ? deltaStrikes.Count : 0)} ctxAffected={ctx.Affected.Count} ctxStrikes={ctx.LightningLineStrikes.Count}");
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
        private readonly BoardController board;
        private readonly SpecialBehaviorDispatcher dispatcher;
        private readonly LineVSpecial lineVSpecial = new();
        private readonly LineHSpecial lineHSpecial = new();
        private readonly PulseCoreSpecial pulseCoreSpecial = new();
        private readonly PatchBotSpecial patchBotSpecial = new();
        private readonly OverrideSpecial overrideSpecial = new();

        public readonly SpecialVisualService Visuals;
        public readonly ActivationQueueProcessor Queue;
        public readonly SpecialImplantService Implants;
        public readonly SpecialFanoutService Fanout;
        public readonly SpecialEffectOrchestrator Effects;
        public readonly PatchbotComboService PatchbotComboService;

        public RuntimeServices(BoardController board)
        {
            this.board = board;

            PatchbotComboService = new PatchbotComboService(board);
            Visuals = new SpecialVisualService(board, board.boardAnimatorRef, PatchbotComboService);
            Effects = new SpecialEffectOrchestrator(board);
            dispatcher = new SpecialBehaviorDispatcher(board, PatchbotComboService, Visuals, Effects);
            Queue = new ActivationQueueProcessor(board, dispatcher);
            Implants = new SpecialImplantService(board, PatchbotComboService, Visuals, Queue);
            Fanout = new SpecialFanoutService(board, Implants, Queue, Visuals);
            dispatcher.QueueProcessor = Queue;
        }

        public List<BoardAction> ResolveOtherSpecialInline(TileView tile)
        {
            if (tile == null || !tile)
                return null;

            var sp = tile.GetSpecial();
            var scoped = new ResolutionContext();
            scoped.Affected.Add(tile);
            SpecialCellUtils.MarkAffectedCell(scoped, tile, board);

            switch (sp)
            {
                case TileSpecial.LineV:
                    scoped.HasLineActivation = true;
                    return lineVSpecial.Execute(new LineVExecutionRuntime
                    {
                        Board = board,
                        Context = scoped,
                        Origin = tile,
                        Partner = null,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = c => Fanout.ProcessFanout(c),
                        CleanupImplantedTiles = c => Implants.CleanupImplantedTiles(c),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) => Visuals.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = c => Queue.EnqueueChainSpecials(c),
                        ProcessQueue = c => Queue.ProcessQueue(c)
                    }).Actions;

                case TileSpecial.LineH:
                    scoped.HasLineActivation = true;
                    return lineHSpecial.Execute(new LineHExecutionRuntime
                    {
                        Board = board,
                        Context = scoped,
                        Origin = tile,
                        Partner = null,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = c => Fanout.ProcessFanout(c),
                        CleanupImplantedTiles = c => Implants.CleanupImplantedTiles(c),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) => Visuals.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = c => Queue.EnqueueChainSpecials(c),
                        ProcessQueue = c => Queue.ProcessQueue(c)
                    }).Actions;

                case TileSpecial.PulseCore:
                    return pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                    {
                        Board = board,
                        Context = scoped,
                        Origin = tile,
                        Partner = null,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = c => Fanout.ProcessFanout(c),
                        CleanupImplantedTiles = c => Implants.CleanupImplantedTiles(c),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) => Visuals.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = c => Queue.EnqueueChainSpecials(c),
                        ProcessQueue = c => Queue.ProcessQueue(c)
                    }).Actions;

                case TileSpecial.PatchBot:
                    return patchBotSpecial.Execute(new PatchBotExecutionRuntime
                    {
                        Board = board,
                        Context = scoped,
                        Origin = tile,
                        Partner = null,
                        FinalizeAtEnd = true,
                        PatchbotService = PatchbotComboService,
                        VisualService = Visuals,
                        Effects = Effects,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = c => Fanout.ProcessFanout(c),
                        CleanupImplantedTiles = c => Implants.CleanupImplantedTiles(c),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) => Visuals.FireOverrideOverrideSpecialVisuals(affected, delays)
                    }).Actions;

                case TileSpecial.SystemOverride:
                    return overrideSpecial.Execute(new OverrideExecutionRuntime
                    {
                        Board = board,
                        Context = scoped,
                        Origin = tile,
                        Partner = null,
                        FinalizeAtEnd = true,
                        ProcessFanout = c => Fanout.ProcessFanout(c),
                        CleanupImplantedTiles = c => Implants.CleanupImplantedTiles(c),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) => Visuals.FireOverrideOverrideSpecialVisuals(affected, delays)
                    }).Actions;

                default:
                    return null;
            }
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
