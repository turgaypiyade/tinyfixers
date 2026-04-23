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
}

public sealed class PulseCoreExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCoreSpecial
{
    private readonly int affectedCellCount;

    public PulseCoreSpecial(int affectedCellCount = 9)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
    }

    /// <summary>
    /// Etkilenen alan boyutundan VFX radius'unu türetir.
    /// affectedCellCount=9  → side=3 → radius=1 (3x3 alan)
    /// affectedCellCount=25 → side=5 → radius=2 (5x5 alan)
    /// affectedCellCount=49 → side=7 → radius=3 (7x7 alan)
    /// </summary>
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

        if (!rt.SkipOriginRegistration)
            RegisterOrigin(rt);

        PlayPulseActivationVisual(rt, rt.Origin.X, rt.Origin.Y);
        CollectArea(rt, rt.Origin.X, rt.Origin.Y);
        ExecuteQueuedChain(rt);

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

        if (!rt.SkipOriginRegistration)
            RegisterOrigin(rt);

        PlayPulseActivationVisual(rt, targetX, targetY);
        CollectArea(rt, targetX, targetY);
        ExecuteQueuedChain(rt);

        if (rt.FinalizeAtEnd)
            Finalize(rt, result, targetX, targetY);

        return result;
    }

    private bool CanExecute(PulseCoreExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() != TileSpecial.PulseCore)
            return false;

        var cell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        if (!rt.SkipOriginRegistration && rt.Context.Processed.Contains(cell))
            return false;

        return true;
    }

    private void RegisterOrigin(PulseCoreExecutionRuntime rt)
    {
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
                    continue;

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

        rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
            SpecialBoardSignalType.SpecialPassFinished,
            new Vector2Int(signalX, signalY),
            rt.Origin));
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

        Dictionary<TileView, float> stagger =
            rt.Board.PulseCoreImpactService.BuildStaggerDelays(ctx.Affected, processedViews);

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            staggerDelays: stagger,
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
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
    }
}
