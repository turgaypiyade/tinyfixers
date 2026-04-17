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
    // OverrideSpecializedCombo.cs
    // Replace ONLY Execute(...) with this stabilized version.
    // Keep ResolveDeferredSpecialBatchAction as-is for now.

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

        // Line batch yolu
        if (useLineBatch)
        {
            bool useBatchClearSpike =
                rt.UseBatchClearSpike &&
                (targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV);

            HashSet<TileView> emittedTiles = null;
            HashSet<Vector2Int> emittedCells = null;

            // 1) Fanout
            var beforeFanout = CaptureSnapshot(rt.Context);

            if (rt.ProcessFanout != null)
            {
                if (rt.ProcessFanout != null)
                {
                    var fanoutActions = rt.ProcessFanout(rt.Context);
                    if (fanoutActions != null && fanoutActions.Count > 0)
                        result.Actions.AddRange(fanoutActions);
                }
            }

            if (useBatchClearSpike)
            {
                emittedTiles = new HashSet<TileView>();
                emittedCells = new HashSet<Vector2Int>();

                // Fanout'un ürettiği chain etkilerini ilk batch gibi clear et
                AppendDeltaClearAction(
                    result.Actions,
                    BuildDeltaClearPayload(rt.Context, beforeFanout, rt.EnqueueCascadeBetweenBatches),
                    emittedTiles,
                    emittedCells);
            }

            // 2) Deferred same-color mevcut special'lar
            if (deferredSpecials != null && deferredSpecials.Count > 0)
            {
                if (useBatchClearSpike)
                {
                    ReleaseDeferredSpecialsInBatches(
                        rt,
                        deferredSpecials,
                        result.Actions,
                        emittedTiles,
                        emittedCells);
                }
                else
                {
                    foreach (var cell in deferredSpecials)
                        rt.Context.Processed.Remove(cell);

                    rt.EnqueueChainSpecials?.Invoke(rt.Context);
                    rt.ProcessQueue?.Invoke(rt.Context);
                }
            }

            // 3) Pulse yolu ilk spike'ta aynen kalsın
            if (targetSpecial == TileSpecial.PulseCore && rt.ActivateSpecial != null)
            {
                var cell = rt.Context.OverrideDeferredLineVActivations[i].cell;
                if (!batchSeeds.Contains(cell))
                    batchSeeds.Add(cell);
            }

            if (deferredSpecials != null)
            {
                for (int i = 0; i < deferredSpecials.Count; i++)
                {
                    if (!batchSeeds.Contains(deferredSpecials[i]))
                        batchSeeds.Add(deferredSpecials[i]);
                }
            }

            // Gercek batch aksiyonlari buraya queue'lanir.
            QueueDeferredSpecialBatchActions(
                rt,
                batchSeeds,
                result.Actions,
                emittedTiles,
                emittedCells);

            rt.Context.OverrideDeferredLineVActivations.Clear();

            // En sonda sadece batch'lerde emit edilmemis seed kalanlarini temizle.
            result.Actions.Add(new FinalizeRemainingSeedAction(
                this,
                rt.Context,
                emittedTiles,
                emittedCells,
                runCascadeInline: false));

            return result;
        }

        // Line disi mevcut yol
        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        if (deferredSpecials != null && deferredSpecials.Count > 0)
        {
            foreach (var cell in deferredSpecials)
                rt.Context.Processed.Remove(cell);

            rt.EnqueueChainSpecials?.Invoke(rt.Context);
            rt.ProcessQueue?.Invoke(rt.Context);
        }

        if (targetSpecial == TileSpecial.PulseCore && rt.ActivateSpecial != null)
        {
            rt.Context.SuppressOverridePulseSelectionVfx = true;

            if (useBatchClearSpike)
            {
                // En sonda origin / partner / deferred source hücreleri gibi
                // delta clear'lara girmemiş kalanları temizle.
                var tail = BuildRemainingSeedClearPayload(
                    rt.Context,
                    emittedTiles,
                    emittedCells,
                    rt.EnqueueCascadeBetweenBatches);

                if (tail.Action != null)
                    result.Actions.Add(tail.Action);
            }
            else
            {
                result.Actions.Add(BuildClearAction(rt.Context, targetSpecial));
            }
        }

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

        result.Actions.Add(BuildClearAction(rt.Context, targetSpecial));
        return result;
    }

    private void ReleaseDeferredSpecialsInBatches(
        OverrideSpecializedComboExecutionRuntime rt,
        List<Vector2Int> deferredSpecials,
        List<BoardAction> actions,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells)
    {
        if (rt == null || deferredSpecials == null || deferredSpecials.Count == 0)
            return;

        QueueDeferredSpecialBatchActions(
            rt,
            deferredSpecials,
            actions,
            emittedTiles,
            emittedCells);
    }
    private void ConsumeComboSourceSpecialVisuals(
        BoardController board,
        TileView overrideTile,
        TileView otherTile)
    {
        if (board == null)
            return;

        if (overrideTile != null && overrideTile.GetSpecial() != TileSpecial.None)
        {
            overrideTile.SetSpecial(TileSpecial.None);
            SpecialCellUtils.SyncAfterSpecialChange(board, overrideTile);
        }

        if (otherTile != null && otherTile.GetSpecial() != TileSpecial.None)
        {
            otherTile.SetSpecial(TileSpecial.None);
            SpecialCellUtils.SyncAfterSpecialChange(board, otherTile);
        }
    }
    private void ReleaseDeferredLineVImplantsAllAtOnce(OverrideSpecializedComboExecutionRuntime rt)
    {
        if (rt == null ||
            rt.Context == null ||
            rt.Board == null ||
            rt.EnqueueActivation == null ||
            rt.Context.OverrideDeferredLineVActivations.Count == 0)
        {
            return;
        }

        var activations = new List<ResolutionContext.SpecialActivation>(
            rt.Context.OverrideDeferredLineVActivations);

        activations.Sort((a, b) =>
        {
            int y = a.cell.y.CompareTo(b.cell.y);
            if (y != 0) return y;
            return a.cell.x.CompareTo(b.cell.x);
        });

        for (int i = 0; i < activations.Count; i++)
        {
            var activation = activations[i];

            if (activation.cell.x < 0 || activation.cell.x >= rt.Board.Width ||
                activation.cell.y < 0 || activation.cell.y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[activation.cell.x, activation.cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.LineV)
                continue;

            var partner = activation.partnerCell.HasValue
                ? rt.Board.Tiles[activation.partnerCell.Value.x, activation.partnerCell.Value.y]
                : null;

            rt.Context.Processed.Remove(activation.cell);
            rt.EnqueueActivation(rt.Context, tile, partner);
        }

        rt.ProcessQueue?.Invoke(rt.Context);
        rt.Context.OverrideDeferredLineVActivations.Clear();
    }
  
    private void ReleaseDeferredLineVImplantsInBatches(
    OverrideSpecializedComboExecutionRuntime rt,
    List<BoardAction> actions,
    HashSet<TileView> emittedTiles,
    HashSet<Vector2Int> emittedCells)
    {
        if (rt == null ||
            rt.Context == null ||
            rt.Board == null ||
            rt.EnqueueActivation == null ||
            rt.Context.OverrideDeferredLineVActivations.Count == 0)
        {
            return;
        }

        var activations = new List<ResolutionContext.SpecialActivation>(
            rt.Context.OverrideDeferredLineVActivations);

        activations.Sort((a, b) =>
        {
            int y = a.cell.y.CompareTo(b.cell.y);
            if (y != 0) return y;
            return a.cell.x.CompareTo(b.cell.x);
        });

        int batchSize = Mathf.Max(1, rt.DeferredSpecialBatchSize);

        for (int start = 0; start < activations.Count; start += batchSize)
        {
            var beforeBatch = CaptureSnapshot(rt.Context);

            int end = Mathf.Min(start + batchSize, activations.Count);
            for (int i = start; i < end; i++)
            {
                var activation = activations[i];

                if (activation.cell.x < 0 || activation.cell.x >= rt.Board.Width ||
                    activation.cell.y < 0 || activation.cell.y >= rt.Board.Height)
                    continue;

                var tile = rt.Board.Tiles[activation.cell.x, activation.cell.y];
                if (tile == null || tile.GetSpecial() != TileSpecial.LineV)
                    continue;

                var partner = activation.partnerCell.HasValue
                    ? rt.Board.Tiles[activation.partnerCell.Value.x, activation.partnerCell.Value.y]
                    : null;

                rt.Context.Processed.Remove(activation.cell);
                rt.EnqueueActivation(rt.Context, tile, partner);
            }

            rt.ProcessQueue?.Invoke(rt.Context);

            AppendDeltaClearAction(
                actions,
                BuildDeltaClearPayload(rt.Context, beforeBatch, rt.EnqueueCascadeBetweenBatches),
                emittedTiles,
                emittedCells);
        }

        rt.Context.OverrideDeferredLineVActivations.Clear();
    }

    private void AppendDeltaClearAction(List<BoardAction> actions, ClearPayload payload, HashSet<TileView> emittedTiles, HashSet<Vector2Int> emittedCells)
    {
        if (actions == null || payload.Action == null)
            return;

        actions.Add(new InlineClearPayloadAction(
            this,
            payload,
            emittedTiles,
            emittedCells,
            runCascadeInline: false));
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
                rt.Context.PendingOverrideImplants.Add(new ResolutionContext.PendingOverrideImplant(
                    new Vector2Int(tile.X, tile.Y),
                    targetSpecial,
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
            doShake: true,
            animationMode: hasChainLightning
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
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
            doShake: true,
            animationMode: ClearAnimationMode.Default,
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
    private void ReleaseDeferredSpecialsAllAtOnce(
    OverrideSpecializedComboExecutionRuntime rt,
    List<Vector2Int> deferredSpecials)
    {
        for (int i = 0; i < deferredSpecials.Count; i++)
            rt.Context.Processed.Remove(deferredSpecials[i]);

        rt.EnqueueChainSpecials?.Invoke(rt.Context);
        rt.ProcessQueue?.Invoke(rt.Context);
    }

    private void ReleaseDeferredSpecialsInBatches(
        OverrideSpecializedComboExecutionRuntime rt,
        List<Vector2Int> deferredSpecials)
    {
        if (deferredSpecials == null || deferredSpecials.Count == 0)
            return;

        int batchSize = Mathf.Max(1, rt.DeferredSpecialBatchSize);

        // İstersen bu sırayı explicit hale getirebilirsin.
        // Mevcut CollectTargets x/y scan ile zaten deterministik bir sıra üretiyor.
        // Güvenli olmak için ayrıca sırala:
        deferredSpecials.Sort((a, b) =>
        {
            int y = a.y.CompareTo(b.y);
            if (y != 0) return y;
            return a.x.CompareTo(b.x);
        });

        for (int start = 0; start < deferredSpecials.Count; start += batchSize)
        {
            int end = Mathf.Min(start + batchSize, deferredSpecials.Count);

            // Sadece bu batch açılıyor
            for (int i = start; i < end; i++)
                rt.Context.Processed.Remove(deferredSpecials[i]);

            rt.EnqueueChainSpecials?.Invoke(rt.Context);
            rt.ProcessQueue?.Invoke(rt.Context);
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

    private void ReleaseDeferredSpecialsInBatches(
        OverrideSpecializedComboExecutionRuntime rt,
        List<Vector2Int> deferredSpecials,
        List<BoardAction> actions,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells)
    {
        if (deferredSpecials == null || deferredSpecials.Count == 0)
            return;

        deferredSpecials.Sort((a, b) =>
        {
            int y = a.y.CompareTo(b.y);
            if (y != 0) return y;
            return a.x.CompareTo(b.x);
        });

        int batchSize = Mathf.Max(1, rt.DeferredSpecialBatchSize);

        for (int start = 0; start < deferredSpecials.Count; start += batchSize)
        {
            var beforeBatch = CaptureSnapshot(rt.Context);

            int end = Mathf.Min(start + batchSize, deferredSpecials.Count);
            for (int i = start; i < end; i++)
                rt.Context.Processed.Remove(deferredSpecials[i]);

            rt.EnqueueChainSpecials?.Invoke(rt.Context);
            rt.ProcessQueue?.Invoke(rt.Context);

            AppendDeltaClearAction(
                actions,
                BuildDeltaClearPayload(rt.Context, beforeBatch, rt.EnqueueCascadeBetweenBatches),
                emittedTiles,
                emittedCells);
        }
    }

    private void AppendDeltaClearAction(
        List<BoardAction> actions,
        ClearPayload payload,
        HashSet<TileView> emittedTiles,
        HashSet<Vector2Int> emittedCells)
    {
        if (payload.Action == null)
            return;

        actions.Add(payload.Action);

        if (payload.Tiles != null)
        {
            foreach (var t in payload.Tiles)
                emittedTiles.Add(t);
        }

        if (payload.Cells != null)
        {
            foreach (var c in payload.Cells)
                emittedCells.Add(c);
        }
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

        if (deltaTiles.Count == 0 &&
            deltaCells.Count == 0 &&
            (deltaStrikes == null || deltaStrikes.Count == 0))
        {
            return new ClearPayload(null, null, null);
        }

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
            doShake: true,
            animationMode: hasChainLightning
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
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

        if (remainingTiles.Count == 0 &&
            remainingCells.Count == 0)
        {
            return new ClearPayload(null, null, null);
        }

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
            doShake: true,
            animationMode: ClearAnimationMode.Default,
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
}