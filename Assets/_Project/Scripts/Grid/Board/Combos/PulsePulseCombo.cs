using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PulsePulseComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public SpecialEffectOrchestrator Effects;
    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
}

public sealed class PulsePulseComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulsePulseCombo
{
    private readonly int radius;

    public PulsePulseCombo(int radius = 2)
    {
        this.radius = Mathf.Max(1, radius);
    }

    public PulsePulseComboExecutionResult Execute(PulsePulseComboExecutionRuntime rt)
    {
        rt.Context.IsPulsePulseComboActive = true;

        var result = new PulsePulseComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        int centerX = rt.Origin.X;
        int centerY = rt.Origin.Y;

        RegisterOrigins(rt);
        PlayComboVisuals(rt, centerX, centerY);
        CollectArea(rt, centerX, centerY);
        ExecuteQueuedChain(rt);
        RemoveDeferredOverrideOriginsFromPulseClear(rt);
        rt.Context.IsPulsePulseComboActive = false;
        
        if (rt.FinalizeAtEnd)
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
                new Vector2Int(centerX, centerY),
                rt.Origin));
        }

        return result;
    }

    private bool CanExecute(PulsePulseComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.PulseCore
            && rt.Partner.GetSpecial() == TileSpecial.PulseCore;
    }

    private void RegisterOrigins(PulsePulseComboExecutionRuntime rt)
    {
        AddOrigin(rt, rt.Origin);
        AddOrigin(rt, rt.Partner);
    }

    private void AddOrigin(PulsePulseComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private void PlayComboVisuals(PulsePulseComboExecutionRuntime rt, int centerX, int centerY)
    {
        //rt.Effects?.EmitComboTriggered(TileSpecial.PulseCore, TileSpecial.PulseCore, new Vector2Int(centerX, centerY));
        rt.Effects?.PlayPulseExplosionAt(centerX, centerY);
    }

    private void CollectArea(PulsePulseComboExecutionRuntime rt, int centerX, int centerY)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                    continue;

                var cell = new Vector2Int(x, y);
                rt.Context.AffectedCells.Add(cell);

                var tile = rt.Board.Tiles[x, y];
                if (tile == null)
                    continue;

                rt.Context.Affected.Add(tile);
            }
        }
    }

    private void ExecuteQueuedChain(PulsePulseComboExecutionRuntime rt)
    {
        if (rt.EnqueueChainSpecials == null || rt.ProcessQueue == null)
            return;

        rt.EnqueueChainSpecials(rt.Context);
        rt.ProcessQueue(rt.Context);
    }

    private MatchClearAction BuildClearAction(PulsePulseComboExecutionRuntime rt)
    {
        var ctx = rt.Context;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            staggerDelays: null,
            staggerAnimTime: 0f,
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
    }

    private void RemoveDeferredOverrideOriginsFromPulseClear(PulsePulseComboExecutionRuntime rt)
    {
        if (rt?.Context?.DeferredPulseComboOverrideCells == null || rt.Context.DeferredPulseComboOverrideCells.Count == 0)
            return;

        foreach (var cell in rt.Context.DeferredPulseComboOverrideCells)
        {
            if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            if (tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            rt.Context.Affected.Remove(tile);
        }
    }
}