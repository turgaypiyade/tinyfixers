using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PulseCoreExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
    public bool SuppressVisualSideEffects;
    public bool SkipOriginRegistration;

    // PatchBot+Pulse target execution için:
    public TileSpecial ForcedOriginSpecial;
    public TileView SignalSourceTile;
}

public sealed class PulseCoreExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCoreSpecial
{
    private readonly int affectedCellCount;

    public PulseCoreSpecial(int affectedCellCount = 25)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
    }

    private int ComputeVfxRadius()
    {
        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0) side += 1;
        return Mathf.Max(1, (side - 1) / 2);
    }

    public PulseCoreExecutionResult Execute(PulseCoreExecutionRuntime rt)
    {
        var result = new PulseCoreExecutionResult();

        if (!CanExecute(rt))
            return result;

        Debug.Log($"[PulseCore.Execute] BEGIN origin=({rt.Origin.X},{rt.Origin.Y}) finalize={rt.FinalizeAtEnd} suppressVfx={rt.SuppressVisualSideEffects}");

        if (!rt.SkipOriginRegistration)
            RegisterOrigin(rt);

        PlayPulseActivationVisual(rt, rt.Origin.X, rt.Origin.Y);
        HideOriginVisualAfterPulse(rt);
        CollectArea(rt, rt.Origin.X, rt.Origin.Y);
        ExecuteQueuedChain(rt);

        Debug.Log($"[PulseCore.Execute] PRE-FINALIZE origin=({rt.Origin.X},{rt.Origin.Y}) affected={rt.Context.Affected.Count} finalize={rt.FinalizeAtEnd}");

        if (rt.FinalizeAtEnd)
            Finalize(rt, result, rt.Origin.X, rt.Origin.Y);

        return result;
    }

    public PulseCoreExecutionResult ExecuteAtTarget(PulseCoreExecutionRuntime rt, int targetX, int targetY)
    {
        var result = new PulseCoreExecutionResult();

        if (!CanExecute(rt))
            return result;

        if (targetX < 0 || targetX >= rt.Board.Width || targetY < 0 || targetY >= rt.Board.Height)
            return result;

        string originLabel = (rt.Origin != null && rt.Origin)
            ? $"({rt.Origin.X},{rt.Origin.Y})"
            : "(dead)";

        Debug.Log($"[PulseCore.ExecuteAtTarget] BEGIN origin={originLabel} target=({targetX},{targetY}) finalize={rt.FinalizeAtEnd} suppressVfx={rt.SuppressVisualSideEffects}");

        if (!rt.SkipOriginRegistration)
            RegisterOrigin(rt);

        PlayPulseActivationVisual(rt, targetX, targetY);
        CollectArea(rt, targetX, targetY);
        ExecuteQueuedChain(rt);

        Debug.Log($"[PulseCore.ExecuteAtTarget] PRE-FINALIZE target=({targetX},{targetY}) affected={rt.Context.Affected.Count} finalize={rt.FinalizeAtEnd}");

        if (rt.FinalizeAtEnd)
            Finalize(rt, result, targetX, targetY);

        return result;
    }

    private bool CanExecute(PulseCoreExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        bool forcedPulseOrigin = rt.ForcedOriginSpecial == TileSpecial.PulseCore;

        if (!forcedPulseOrigin)
        {
            if (rt.Origin == null)
                return false;

            if (rt.Origin.GetSpecial() != TileSpecial.PulseCore)
                return false;

            var cell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
            if (!rt.SkipOriginRegistration && rt.Context.Processed.Contains(cell))
                return false;
        }

        return true;
    }

    private void RegisterOrigin(PulseCoreExecutionRuntime rt)
    {
        if (rt.Origin == null)
            return;

        var originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);

        rt.Context.Processed.Add(originCell);
        rt.Context.Affected.Add(rt.Origin);
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
    }

    private void PlayPulseActivationVisual(PulseCoreExecutionRuntime rt, int centerX, int centerY)
    {
        if (rt.SuppressVisualSideEffects)
            return;

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(centerX, centerY));

        if (rt.Board?.PulseCoreImpactService != null)
            rt.Board.PulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell(centerX, centerY, radiusCells: ComputeVfxRadius());
    }

    private void HideOriginVisualAfterPulse(PulseCoreExecutionRuntime rt)
    {
        if (rt == null || rt.SuppressVisualSideEffects || rt.Origin == null)
            return;

        if (rt.Origin.GetSpecial() != TileSpecial.PulseCore)
            return;

        SpecialVisualService.HideTileVisualForCombo(rt.Origin);
        Debug.Log($"[PulseCore.Execute] HIDE origin=({rt.Origin.X},{rt.Origin.Y}) after-pulse");
    }

    private void CollectArea(PulseCoreExecutionRuntime rt, int centerX, int centerY)
    {
        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0)
            side += 1;

        int half = side / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
        {
            for (int y = centerY - half; y <= centerY + half; y++)
            {
                if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
                    continue;

                if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                {
                    // OverTileBlocker obstacles are blocked by CanAffectCell but PulseCore
                    // should still damage them directly (same as LineV's lightning sweep).
                    if (rt.Board.ObstacleStateService != null && rt.Board.ObstacleStateService.HasObstacleAt(x, y))
                        rt.Context.ImpactCells.Add(new Vector2Int(x, y));
                    continue;
                }

                SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board);

                var tile = rt.Board.Tiles[x, y];
                if (tile == null)
                    continue;

                rt.Context.Affected.Add(tile);
            }
        }
    }

    private void ExecuteQueuedChain(PulseCoreExecutionRuntime rt)
    {
        if (rt.EnqueueChainSpecials == null || rt.ProcessQueue == null)
            return;

        rt.Context.IsPulseCoreActive = true;
        rt.EnqueueChainSpecials(rt.Context);
        rt.ProcessQueue(rt.Context);
        rt.Context.IsPulseCoreActive = false;
    }

    private void Finalize(PulseCoreExecutionRuntime rt, PulseCoreExecutionResult result, int signalX, int signalY)
    {
        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

        var clearAction = BuildClearAction(rt);
        if (clearAction != null)
            result.Actions.Add(clearAction);

        TileView signalTile = rt.SignalSourceTile != null ? rt.SignalSourceTile : rt.Origin;

        rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
            SpecialBoardSignalType.SpecialPassFinished,
            new Vector2Int(signalX, signalY),
            signalTile));
    }

    private MatchClearAction BuildClearAction(PulseCoreExecutionRuntime rt)
    {
        var ctx = rt.Context;

        HashSet<TileView> processedViews = new HashSet<TileView>();
        foreach (var pos in ctx.Processed)
        {
            if (rt.Board.Tiles[pos.x, pos.y] != null)
                processedViews.Add(rt.Board.Tiles[pos.x, pos.y]);
        }

        Dictionary<TileView, float> pulseDelays =
            rt.Board.PulseCoreImpactService.BuildStaggerDelays(ctx.Affected, processedViews);

        Dictionary<TileView, float> clearDelays =
            ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0
                ? ctx.OverrideRadialClearDelays
                : pulseDelays;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            staggerDelays: null,
            staggerAnimTime: rt.Board.ApplySpecialChainTempo(rt.Board.PulseImpactAnimTime),
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: clearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
    }
}