using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideSpecializedComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> EnqueueActivation;
    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;

    public bool UseBatchClearSpike;
    public int DeferredSpecialBatchSize;
    public bool EnqueueCascadeBetweenBatches;
}

public sealed class OverrideSpecializedComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideSpecializedCombo
{
    private const float OverridePulseCoreStaggerSeconds = 0.1f;

    public OverrideSpecializedComboExecutionResult Execute(OverrideSpecializedComboExecutionRuntime rt)
    {
        var result = new OverrideSpecializedComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var overrideTile = rt.Origin.GetSpecial() == TileSpecial.SystemOverride ? rt.Origin : rt.Partner;
        var otherTile = overrideTile == rt.Origin ? rt.Partner : rt.Origin;
        var targetSpecial = otherTile.GetSpecial();

        AddOrigin(rt, overrideTile);
        AddOrigin(rt, otherTile);

        PrepareFanout(rt, overrideTile, targetSpecial);
        var deferredSpecials = CollectTargets(rt, overrideTile, otherTile, targetSpecial);

        if (!rt.FinalizeAtEnd)
            return result;

        bool useLineBatch =
            rt.UseBatchClearSpike &&
            (targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV);

        if (useLineBatch)
            return ExecuteOverrideLineInBatches(rt, result, deferredSpecials, overrideTile, otherTile);

        ExecuteNonLineOverride(rt, result, deferredSpecials, targetSpecial, overrideTile, otherTile);
        return result;
    }

    private OverrideSpecializedComboExecutionResult ExecuteOverrideLineInBatches(
       OverrideSpecializedComboExecutionRuntime rt,
       OverrideSpecializedComboExecutionResult result,
       List<Vector2Int> deferredSpecials,
       TileView overrideTile,
       TileView otherTile)
    {
        var emittedTiles = new HashSet<TileView>();
        var emittedCells = new HashSet<Vector2Int>();

        rt.Context.SuppressImmediateOverrideQueueProcessing = true;

        var beforeFanout = CaptureSnapshot(rt.Context);

        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        rt.Context.SuppressImmediateOverrideQueueProcessing = false;

        // KRITIK:
        // Source override + source line hemen tuketilsin.
        // Ekranda kalmasin, fall'a katilmasin, sona kalmasin.
        var sourcePayload = BuildSourcePairClearPayload(overrideTile, otherTile);
        ConsumeSourcePairForBatchMode(rt, overrideTile, otherTile);
        AppendDeltaClearAction(
            result.Actions,
            sourcePayload,
            emittedTiles,
            emittedCells);

        var batchActivations = BuildBatchActivations(rt, deferredSpecials);

        // Pending'e AKTIF batch'i degil, henuz patlatilmamis GELECEK batch seed'lerini koy.
        // Boylece batch clear sonrasi fall sirasinda sirasi gelmemis special'lar yerinde kilitli kalir.
        if (batchActivations.Count > 0)
        {
            var allPendingCells = new List<Vector2Int>(batchActivations.Count);
            foreach (var activation in batchActivations)
                allPendingCells.Add(activation.cell);

            result.Actions.Add(new PendingTriggeredSpecialScopeAction(allPendingCells, true));
        }

        var fanoutPayload = BuildDeltaClearPayload(
            rt.Context,
            beforeFanout,
            enqueueCascadeOnComplete: false);

        if (fanoutPayload.Action != null)
        {
            result.Actions.Add(new InlineClearPayloadAction(
                this,
                fanoutPayload,
                emittedTiles,
                emittedCells,
                rt.EnqueueCascadeBetweenBatches));
        }

        if (batchActivations.Count > 0)
        {
            QueueDeferredLineActivationBatches(
                rt,
                batchActivations,
                result.Actions,
                emittedTiles,
                emittedCells);

            var allPendingCells = new List<Vector2Int>(batchActivations.Count);
            foreach (var activation in batchActivations)
                allPendingCells.Add(activation.cell);

            result.Actions.Add(new PendingTriggeredSpecialScopeAction(allPendingCells, false));
        }

        rt.Context.OverrideDeferredLineVActivations.Clear();

        result.Actions.Add(new FinalizeRemainingSeedAction(
            this,
            rt.Context,
            emittedTiles,
            emittedCells,
            runCascadeInline: false));

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

        return result;
    }

    private void ExecuteNonLineOverride(
        OverrideSpecializedComboExecutionRuntime rt,
        OverrideSpecializedComboExecutionResult result,
        List<Vector2Int> deferredSpecials,
        TileSpecial targetSpecial,
        TileView overrideTile,
        TileView otherTile)
    {
        // For PulseCore target: suppress immediate activation of implanted PulseCores
        // so they can fire sequentially after the placement animation.
        if (targetSpecial == TileSpecial.PulseCore)
            rt.Context.SuppressImmediateOverrideQueueProcessing = true;

        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        // KRİTİK (PulseCore): kaynak override + pulse ikonları, fanout yerleşimi biter bitmez
        // temizlensin. Aksi hâlde ctx.Affected'te kalıp tüm sıralı pulse patlamaları boyunca
        // ekranda asılı durup ancak en sondaki BuildClearAction'da yok oluyorlardı
        // ("bombaları koydu ama kendi ikonu ortada kaldı, en son kayboldu").
        if (targetSpecial == TileSpecial.PulseCore)
        {
            var sourcePayload = BuildSourcePairClearPayload(overrideTile, otherTile);
            ConsumeSourcePairForBatchMode(rt, overrideTile, otherTile);
            if (sourcePayload.Action != null)
                result.Actions.Add(sourcePayload.Action);
        }

        if (targetSpecial == TileSpecial.PulseCore)
        {
            rt.Context.SuppressImmediateOverrideQueueProcessing = false;

            // Defer all implanted PulseCores (fanout targets) into the sequential explosion list.
            // Add to Processed so EnqueueChainSpecials won't re-queue them.
            foreach (var t in rt.Context.OverrideFanoutTargets)
            {
                if (t == null) continue;
                var cell = new Vector2Int(t.X, t.Y);
                rt.Context.Processed.Add(cell);
                if (!rt.Context.OverrideDeferredPulseExplosions.Contains(cell))
                    rt.Context.OverrideDeferredPulseExplosions.Add(cell);
            }
        }

        if (deferredSpecials != null && deferredSpecials.Count > 0)
        {
            foreach (var cell in deferredSpecials)
            {
                // Existing PulseCore tiles on the board: defer into the stagger sequence.
                if (targetSpecial == TileSpecial.PulseCore)
                {
                    var dTile = (cell.x >= 0 && cell.x < rt.Board.Width && cell.y >= 0 && cell.y < rt.Board.Height)
                        ? rt.Board.Tiles[cell.x, cell.y]
                        : null;
                    if (dTile != null && dTile.GetSpecial() == TileSpecial.PulseCore)
                    {
                        if (!rt.Context.OverrideDeferredPulseExplosions.Contains(cell))
                            rt.Context.OverrideDeferredPulseExplosions.Add(cell);
                        continue;
                    }
                }

                rt.Context.Processed.Remove(cell);
            }
        }

        // Process any non-PulseCore chain specials that may have been queued.
        rt.EnqueueChainSpecials?.Invoke(rt.Context);
        rt.ProcessQueue?.Invoke(rt.Context);

        if (targetSpecial == TileSpecial.PulseCore && rt.ActivateSpecial != null)
        {
            result.Actions.Add(new ResolveDeferredPulseCoreActivationSequenceAction(
                this,
                rt,
                new List<Vector2Int>(rt.Context.OverrideDeferredPulseExplosions),
                targetSpecial,
                OverridePulseCoreStaggerSeconds));
            return;
        }

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

        result.Actions.Add(BuildClearAction(rt.Context, targetSpecial));
    }

    private List<ResolutionContext.SpecialActivation> BuildBatchActivations(
       OverrideSpecializedComboExecutionRuntime rt,
       List<Vector2Int> deferredSpecials)
    {
        var activations = new List<ResolutionContext.SpecialActivation>();
        var seen = new HashSet<Vector2Int>();

        if (rt.Context.OverrideDeferredLineVActivations != null)
        {
            foreach (var activation in rt.Context.OverrideDeferredLineVActivations)
            {
                if (seen.Add(activation.cell))
                    activations.Add(activation);
            }
        }

        if (deferredSpecials != null)
        {
            foreach (var cell in deferredSpecials)
            {
                if (seen.Add(cell))
                    activations.Add(new ResolutionContext.SpecialActivation(cell, null));
            }
        }

        activations.Sort((a, b) =>
        {
            int y = a.cell.y.CompareTo(b.cell.y);
            if (y != 0) return y;
            return a.cell.x.CompareTo(b.cell.x);
        });

        return activations;
    }

    private void QueueDeferredLineActivationBatches(
      OverrideSpecializedComboExecutionRuntime rt,
      List<ResolutionContext.SpecialActivation> activations,
      List<BoardAction> actions,
      HashSet<TileView> emittedTiles,
      HashSet<Vector2Int> emittedCells)
    {
        if (activations == null || activations.Count == 0)
            return;

        int batchSize = Mathf.Max(1, rt.DeferredSpecialBatchSize);

        for (int start = 0; start < activations.Count; start += batchSize)
        {
            int end = Mathf.Min(start + batchSize, activations.Count);
            var batch = new List<ResolutionContext.SpecialActivation>(end - start);

            for (int i = start; i < end; i++)
                batch.Add(activations[i]);

            actions.Add(new ResolveDeferredLineActivationBatchAction(
                this,
                rt,
                batch,
                emittedTiles,
                emittedCells,
                rt.EnqueueCascadeBetweenBatches));
        }
    }

    private bool CanExecute(OverrideSpecializedComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool hasOverride = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            || rt.Partner.GetSpecial() == TileSpecial.SystemOverride;

        if (!hasOverride)
            return false;

        var targetSpecial = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            ? rt.Partner.GetSpecial()
            : rt.Origin.GetSpecial();

        return targetSpecial == TileSpecial.LineH
            || targetSpecial == TileSpecial.LineV
            || targetSpecial == TileSpecial.PulseCore
            || targetSpecial == TileSpecial.PatchBot;
    }

    private void PrepareFanout(OverrideSpecializedComboExecutionRuntime rt, TileView overrideTile, TileSpecial targetSpecial)
    {
        rt.Context.OverrideFanoutOrigin = overrideTile;
        rt.Context.OverrideForceDefaultClearAnim = !(targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV);
        rt.Context.OverrideSuppressPerTileClearVfx = false;
        rt.Context.OverrideFanoutNormalSelectionPulse = false;

        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(
            new Vector2Int(overrideTile.X, overrideTile.Y),
            targetSpecial);
    }

    private List<Vector2Int> CollectTargets(
        OverrideSpecializedComboExecutionRuntime rt,
        TileView overrideTile,
        TileView otherTile,
        TileSpecial targetSpecial)
    {
        TileType baseType = otherTile.GetTileType();
        List<Vector2Int> deferredSpecialCells = null;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            for (int y = 0; y < rt.Board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                    continue;

                if (rt.Board.ObstacleStateService != null &&
                    rt.Board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    continue;

                var tile = rt.Board.Tiles[x, y];
                if (tile == null || !tile.GetTileType().Equals(baseType))
                    continue;

                if (tile == overrideTile || tile == otherTile)
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                {
                    var cell = new Vector2Int(tile.X, tile.Y);
                    rt.Context.Affected.Add(tile);
                    SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
                    rt.Context.Processed.Add(cell);

                    deferredSpecialCells ??= new List<Vector2Int>();
                    deferredSpecialCells.Add(cell);
                    continue;
                }

                rt.Context.OverrideFanoutTargets.Add(tile);
                var implantSpecial = (targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV)
                    ? (UnityEngine.Random.value < 0.5f ? TileSpecial.LineH : TileSpecial.LineV)
                    : targetSpecial;

                rt.Context.PendingOverrideImplants.Add(new ResolutionContext.PendingOverrideImplant(
                    new Vector2Int(tile.X, tile.Y),
                    implantSpecial,
                    new Vector2Int(otherTile.X, otherTile.Y),
                    new Vector2Int(overrideTile.X, overrideTile.Y)));
            }
        }

        return deferredSpecialCells;
    }

    private void AddOrigin(OverrideSpecializedComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private MatchClearAction BuildClearAction(ResolutionContext ctx, TileSpecial targetSpecial)
    {
        bool hasChainLightning = ctx.HasLineActivation
            && ctx.LightningLineStrikes != null
            && ctx.LightningLineStrikes.Count > 0;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            animationMode: hasChainLightning
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: hasChainLightning ? ctx.LightningVisualTargets : null,
            lightningLineStrikes: hasChainLightning ? ctx.LightningLineStrikes : null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }

    private ClearPayload BuildSourcePairClearPayload(TileView overrideTile, TileView otherTile)
    {
        var tiles = new HashSet<TileView>();
        var cells = new HashSet<Vector2Int>();

        if (overrideTile != null)
        {
            tiles.Add(overrideTile);
            cells.Add(new Vector2Int(overrideTile.X, overrideTile.Y));
        }

        if (otherTile != null)
        {
            tiles.Add(otherTile);
            cells.Add(new Vector2Int(otherTile.X, otherTile.Y));
        }

        if (tiles.Count == 0)
            return new ClearPayload(null, null, null);

        var action = new MatchClearAction(
            tiles,
            doShake: false,
            animationMode: ClearAnimationMode.Default,
            affectedCells: cells,
            impactCells: null,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: null,
            lightningLineStrikes: null,
            suppressPerTileClearVfx: false,
            perTileClearDelays: null,
            isSpecialPhase: true,
            presentationPlan: null,
            enqueueCascadeOnComplete: false);

        return new ClearPayload(action, tiles, cells);
    }

    private void ConsumeSourcePairForBatchMode(
        OverrideSpecializedComboExecutionRuntime rt,
        TileView overrideTile,
        TileView otherTile)
    {
        ConsumeSourceTileForBatchMode(rt, overrideTile);
        ConsumeSourceTileForBatchMode(rt, otherTile);
    }

    private void ConsumeSourceTileForBatchMode(
        OverrideSpecializedComboExecutionRuntime rt,
        TileView tile)
    {
        if (rt == null || rt.Context == null || rt.Board == null || tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);

        rt.Context.Affected.Remove(tile);
        rt.Context.AffectedCells?.Remove(cell);
        rt.Context.LightningVisualTargets?.Remove(tile);
        rt.Context.Processed.Remove(cell);
        rt.Context.Queued.Remove(cell);

        if (rt.Context.OverrideRadialClearDelays != null)
            rt.Context.OverrideRadialClearDelays.Remove(tile);

        if (tile.X >= 0 && tile.X < rt.Board.Width && tile.Y >= 0 && tile.Y < rt.Board.Height)
        {
            if (rt.Board.Tiles[tile.X, tile.Y] == tile)
                rt.Board.ClearCell(tile.X, tile.Y);
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

        public ClearPayload(MatchClearAction action, HashSet<TileView> tiles, HashSet<Vector2Int> cells)
        {
            Action = action;
            Tiles = tiles;
            Cells = cells;
        }
    }

    private ClearSnapshot CaptureSnapshot(ResolutionContext ctx)
    {
        return new ClearSnapshot(
            new HashSet<TileView>(ctx.Affected),
            new HashSet<Vector2Int>(ctx.AffectedCells),
            new HashSet<TileView>(ctx.LightningVisualTargets),
            ctx.LightningLineStrikes.Count,
            ctx.ImpactCells.Count);
    }

    private void AppendDeltaClearAction(
        List<BoardAction> actions,
        ClearPayload payload,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells)
    {
        if (payload.Action == null)
            return;

        actions.Add(new InlineClearPayloadAction(
            this,
            payload,
            emittedTiles,
            emittedCells,
            false));
    }

    private ClearPayload BuildDeltaClearPayload(
        ResolutionContext ctx,
        ClearSnapshot before,
        bool enqueueCascadeOnComplete)
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

        // KRITIK FIX:
        // Gercek temizlenecek tile yoksa matches=0 clear zinciri uretme.
        if (deltaTiles.Count == 0)
            return new ClearPayload(null, null, null);

        Dictionary<TileView, float> batchRadialDelays = null;
        if (ctx.OverrideRadialClearDelays != null)
        {
            foreach (var kv in ctx.OverrideRadialClearDelays)
            {
                if (!deltaTiles.Contains(kv.Key))
                    continue;

                batchRadialDelays ??= new Dictionary<TileView, float>();
                batchRadialDelays[kv.Key] = kv.Value;
            }
        }

        bool hasChainLightning = deltaStrikes != null && deltaStrikes.Count > 0;

        var action = new MatchClearAction(
            deltaTiles,
            true,
            hasChainLightning ? ClearAnimationMode.LightningStrike : ClearAnimationMode.Default,
            affectedCells: deltaCells.Count > 0 ? deltaCells : null,
            impactCells: deltaImpacts,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: hasChainLightning && deltaLightningTargets.Count > 0 ? deltaLightningTargets : null,
            lightningLineStrikes: hasChainLightning ? deltaStrikes : null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: batchRadialDelays,
            isSpecialPhase: true,
            presentationPlan: null,
            enqueueCascadeOnComplete: enqueueCascadeOnComplete);

        return new ClearPayload(action, deltaTiles, deltaCells);
    }

    private ClearPayload BuildRemainingSeedClearPayload(
        ResolutionContext ctx,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells,
        bool enqueueCascadeOnComplete)
    {
        var remainingTiles = new HashSet<TileView>();
        foreach (var tile in ctx.Affected)
        {
            if (tile != null && (emittedTiles == null || !emittedTiles.Contains(tile)))
                remainingTiles.Add(tile);
        }

        var remainingCells = new HashSet<Vector2Int>();
        foreach (var cell in ctx.AffectedCells)
        {
            if (emittedCells == null || !emittedCells.Contains(cell))
                remainingCells.Add(cell);
        }

        // KRITIK FIX:
        // Gercek tile yoksa tail clear uretme.
        if (remainingTiles.Count == 0)
            return new ClearPayload(null, null, null);

        Dictionary<TileView, float> tailDelays = null;
        if (ctx.OverrideRadialClearDelays != null)
        {
            foreach (var kv in ctx.OverrideRadialClearDelays)
            {
                if (!remainingTiles.Contains(kv.Key))
                    continue;

                tailDelays ??= new Dictionary<TileView, float>();
                tailDelays[kv.Key] = kv.Value;
            }
        }

        var action = new MatchClearAction(
            remainingTiles,
            true,
            ClearAnimationMode.Default,
            affectedCells: remainingCells.Count > 0 ? remainingCells : null,
            impactCells: null,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: null,
            lightningLineStrikes: null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: tailDelays,
            isSpecialPhase: true,
            presentationPlan: null,
            enqueueCascadeOnComplete: enqueueCascadeOnComplete);

        return new ClearPayload(action, remainingTiles, remainingCells);
    }

    private void MergeEmitted(
        ClearPayload payload,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells)
    {
        if (payload.Tiles != null && emittedTiles != null)
        {
            foreach (var t in payload.Tiles)
                emittedTiles.Add(t);
        }

        if (payload.Cells != null && emittedCells != null)
        {
            foreach (var c in payload.Cells)
                emittedCells.Add(c);
        }
    }

    private IEnumerator ExecutePayloadInline(
        ActionSequencer sequencer,
        ClearPayload payload,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells,
        bool runCascadeInline)
    {
        if (payload.Action == null)
            yield break;

        yield return payload.Action.ExecuteVisuals(sequencer);
        MergeEmitted(payload, emittedTiles, emittedCells);

        if (!runCascadeInline)
            yield break;

        var cascades = sequencer.Board.CascadeLogic.CalculateCascades();
        if (cascades == null || cascades.Count == 0)
            yield break;

        for (int i = 0; i < cascades.Count; i++)
            yield return cascades[i].ExecuteVisuals(sequencer);

        sequencer.Board.RefreshAllSortingOrders();
    }

    private sealed class InlineClearPayloadAction : BoardAction
    {
        private readonly OverrideSpecializedCombo owner;
        private readonly ClearPayload payload;
        private readonly HashSet<TileView> emittedTiles;
        private readonly HashSet<Vector2Int> emittedCells;
        private readonly bool runCascadeInline;

        public override bool Blocking => true;

        public InlineClearPayloadAction(
            OverrideSpecializedCombo owner,
            ClearPayload payload,
            HashSet<TileView> emittedTiles,
            HashSet<Vector2Int> emittedCells,
            bool runCascadeInline)
        {
            this.owner = owner;
            this.payload = payload;
            this.emittedTiles = emittedTiles;
            this.emittedCells = emittedCells;
            this.runCascadeInline = runCascadeInline;
        }

        public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
        {
            yield return owner.ExecutePayloadInline(
                sequencer,
                payload,
                emittedTiles,
                emittedCells,
                runCascadeInline);
        }
    }

    private sealed class FinalizeRemainingSeedAction : BoardAction
    {
        private readonly OverrideSpecializedCombo owner;
        private readonly ResolutionContext ctx;
        private readonly HashSet<TileView> emittedTiles;
        private readonly HashSet<Vector2Int> emittedCells;
        private readonly bool runCascadeInline;

        public override bool Blocking => true;

        public FinalizeRemainingSeedAction(
            OverrideSpecializedCombo owner,
            ResolutionContext ctx,
            HashSet<TileView> emittedTiles,
            HashSet<Vector2Int> emittedCells,
            bool runCascadeInline)
        {
            this.owner = owner;
            this.ctx = ctx;
            this.emittedTiles = emittedTiles;
            this.emittedCells = emittedCells;
            this.runCascadeInline = runCascadeInline;
        }

        public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
        {
            var payload = owner.BuildRemainingSeedClearPayload(
                ctx,
                emittedTiles,
                enqueueCascadeOnComplete: false,
                emittedCells: emittedCells);

            bool hasRealTiles = payload.Tiles != null && payload.Tiles.Count > 0;
            if (!hasRealTiles)
                yield break;

            yield return owner.ExecutePayloadInline(
                sequencer,
                payload,
                emittedTiles,
                emittedCells,
                runCascadeInline);
        }
    }

    private sealed class ResolveDeferredPulseCoreActivationSequenceAction : BoardAction
    {
        private readonly OverrideSpecializedCombo owner;
        private readonly OverrideSpecializedComboExecutionRuntime rt;
        private readonly List<Vector2Int> pulseCells;
        private readonly TileSpecial targetSpecial;
        private readonly float staggerSeconds;

        public override bool Blocking => true;

        public ResolveDeferredPulseCoreActivationSequenceAction(
            OverrideSpecializedCombo owner,
            OverrideSpecializedComboExecutionRuntime rt,
            List<Vector2Int> pulseCells,
            TileSpecial targetSpecial,
            float staggerSeconds)
        {
            this.owner = owner;
            this.rt = rt;
            this.pulseCells = pulseCells ?? new List<Vector2Int>();
            this.targetSpecial = targetSpecial;
            this.staggerSeconds = Mathf.Max(0f, staggerSeconds);
        }

        public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
        {
            Debug.Log($"[OverrideSpecializedCombo] PulseCore sequence count={pulseCells.Count} stagger={staggerSeconds:0.000}");

            bool previousSuppressPulseCoreChain = rt.Context.SuppressPulseCoreToPulseCoreChain;
            rt.Context.SuppressPulseCoreToPulseCoreChain = true;

            var clearedInSteps = new HashSet<TileView>();

            // Patlama sırası gelene kadar implant pulse'lar hücrelerinde çakılı
            // (pending-triggered = gravity-blocked). Adımlar arası taş düşüşü açık
            // olduğu için bu şart: pulse oyuncunun gördüğü yerde patlamalı, ve
            // cell-bazlı Processed koruması ancak hücre sabitse geçerli kalır.
            var anchoredCells = new HashSet<Vector2Int>();
            foreach (var cell in pulseCells)
            {
                if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                    continue;

                var anchorTile = rt.Board.Tiles[cell.x, cell.y];
                if (anchorTile != null && anchorTile.GetSpecial() == TileSpecial.PulseCore)
                    anchoredCells.Add(cell);
            }

            if (anchoredCells.Count > 0)
                rt.Board.SetPendingTriggeredSpecialCells(anchoredCells);

            var releaseBuffer = new Vector2Int[1];

            try
            {
                for (int i = 0; i < pulseCells.Count; i++)
                {
                    var cell = pulseCells[i];

                    if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                        continue;

                    // Sırası geldi: anchor'ı bırak (patlama + ardından gelen gravity
                    // bu hücreyi artık normal işleyebilir).
                    if (anchoredCells.Remove(cell))
                    {
                        releaseBuffer[0] = cell;
                        rt.Board.ClearPendingTriggeredSpecialCells(releaseBuffer);
                    }

                    var tile = rt.Board.Tiles[cell.x, cell.y];
                    if (tile == null || tile.GetSpecial() != TileSpecial.PulseCore)
                        continue;

                    rt.Context.Processed.Remove(cell);
                    rt.Context.Queued.Remove(cell);

                    Debug.Log($"[OverrideSpecializedCombo] PulseCore sequence step={i + 1}/{pulseCells.Count} cell={cell}");

                    var affectedBefore = new HashSet<TileView>(rt.Context.Affected);
                    rt.ActivateSpecial?.Invoke(rt.Context, tile, null);

                    // Tiles newly added by this PulseCore explosion
                    var stepTiles = new HashSet<TileView>();
                    foreach (var t in rt.Context.Affected)
                    {
                        if (t != null && !affectedBefore.Contains(t))
                            stepTiles.Add(t);
                    }

                    if (stepTiles.Count > 0)
                    {
                        var delays = new Dictionary<TileView, float>();
                        foreach (var t in stepTiles)
                        {
                            int dist = Mathf.Abs(t.X - cell.x) + Mathf.Abs(t.Y - cell.y);
                            delays[t] = dist * rt.Board.PulseImpactDelayStep;
                        }

                        var stepCells = new HashSet<Vector2Int>();
                        foreach (var t in stepTiles)
                            stepCells.Add(new Vector2Int(t.X, t.Y));

                        var stepClear = new MatchClearAction(
                            stepTiles,
                            doShake: i == 0,
                            staggerDelays: null,
                            staggerAnimTime: rt.Board.ApplySpecialChainTempo(rt.Board.PulseImpactAnimTime),
                            animationMode: ClearAnimationMode.Default,
                            affectedCells: stepCells,
                            impactCells: null,
                            includeAdjacentOverTileBlockerDamage: false,
                            lightningVisualTargets: null,
                            lightningLineStrikes: null,
                            suppressPerTileClearVfx: false,
                            perTileClearDelays: delays,
                            isSpecialPhase: true,
                            presentationPlan: null);

                        yield return stepClear.ExecuteVisuals(sequencer);

                        foreach (var t in stepTiles)
                            clearedInSteps.Add(t);
                    }

                    // Patlamanın boşalttığı alana taş düşüşü hemen başlar (background);
                    // bekleyen pulse'lar anchor'lı olduğundan yerlerinde kalır. Sıradaki
                    // patlama, düşmekte olan taşları havada yakalar.
                    float fallDuration = 0f;
                    var cascades = rt.Board.CascadeLogic.CalculateCascades();
                    if (cascades != null && cascades.Count > 0)
                    {
                        for (int c = 0; c < cascades.Count; c++)
                        {
                            if (cascades[c] is FallAction fa)
                                fallDuration = Mathf.Max(fallDuration, fa.GetEstimatedVisualDuration(rt.Board));
                            rt.Board.StartImmediateAction(cascades[c]);
                        }
                    }
                    rt.Board.RefreshAllSortingOrders();

                    if (i < pulseCells.Count - 1)
                    {
                        float wait = Mathf.Max(
                            rt.Board.ApplySpecialChainTempo(staggerSeconds),
                            fallDuration * rt.Board.PulseChainCatchOverlap);
                        if (wait > 0f)
                            yield return new WaitForSeconds(wait);
                    }
                }
            }
            finally
            {
                rt.Context.SuppressPulseCoreToPulseCoreChain = previousSuppressPulseCoreChain;

                // Patlamadan atlanan (skip) pulse hücrelerinde anchor kalmasın —
                // kalıcı pending hücre o sütunun gravity'sini sonsuza dek bloklar.
                if (anchoredCells.Count > 0)
                    rt.Board.ClearPendingTriggeredSpecialCells(anchoredCells);
            }

            // Remove per-step cleared tiles; only origin tiles remain for final clear
            foreach (var t in clearedInSteps)
                rt.Context.Affected.Remove(t);

            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);

            if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
                rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

            if (rt.Context.Affected.Count > 0)
            {
                var clearAction = owner.BuildClearAction(rt.Context, targetSpecial);
                if (clearAction != null)
                    yield return clearAction.ExecuteVisuals(sequencer);
            }
        }
    }

    private sealed class ResolveDeferredLineActivationBatchAction : BoardAction
    {
        private readonly OverrideSpecializedCombo owner;
        private readonly OverrideSpecializedComboExecutionRuntime rt;
        private readonly List<ResolutionContext.SpecialActivation> batchActivations;
        private readonly HashSet<TileView> emittedTiles;
        private readonly HashSet<Vector2Int> emittedCells;
        private readonly bool runCascadeInline;

        public override bool Blocking => true;

        public ResolveDeferredLineActivationBatchAction(
            OverrideSpecializedCombo owner,
            OverrideSpecializedComboExecutionRuntime rt,
            List<ResolutionContext.SpecialActivation> batchActivations,
            HashSet<TileView> emittedTiles,
            HashSet<Vector2Int> emittedCells,
            bool runCascadeInline)
        {
            this.owner = owner;
            this.rt = rt;
            this.batchActivations = batchActivations ?? new List<ResolutionContext.SpecialActivation>();
            this.emittedTiles = emittedTiles;
            this.emittedCells = emittedCells;
            this.runCascadeInline = runCascadeInline;
        }

        public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
        {
            if (batchActivations == null || batchActivations.Count == 0)
                yield break;

            var currentBatchCells = new List<Vector2Int>(batchActivations.Count);
            foreach (var activation in batchActivations)
                currentBatchCells.Add(activation.cell);

            // Bu batch artık patlayacağı için pending'den çıkar.
            // Pending'de sadece henüz sıra gelmemiş diğer batchler kalsın.
            rt.Board.ClearPendingTriggeredSpecialCells(currentBatchCells);

            var beforeBatch = owner.CaptureSnapshot(rt.Context);

            foreach (var activation in batchActivations)
            {
                var cell = activation.cell;

                if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                    continue;

                var tile = rt.Board.Tiles[cell.x, cell.y];
                if (tile == null)
                    continue;

                var special = tile.GetSpecial();
                if (special == TileSpecial.None)
                    continue;

                rt.Context.Processed.Remove(cell);
                rt.Context.Queued.Remove(cell);

                TileView partner = activation.partnerCell.HasValue
                    ? rt.Board.Tiles[activation.partnerCell.Value.x, activation.partnerCell.Value.y]
                    : null;

                rt.EnqueueActivation?.Invoke(rt.Context, tile, partner);
            }

            rt.ProcessQueue?.Invoke(rt.Context);

            var payload = owner.BuildDeltaClearPayload(
                rt.Context,
                beforeBatch,
                enqueueCascadeOnComplete: false);

            yield return owner.ExecutePayloadInline(
                sequencer,
                payload,
                emittedTiles,
                emittedCells,
                runCascadeInline);
        }
    }
}