using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineHExecutionRuntime
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
}

public sealed class LineHExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineHSpecial
{
    public LineHExecutionResult Execute(LineHExecutionRuntime rt)
    {
        var result = new LineHExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigin(rt);
        CollectRow(rt);
        BuildLineVisuals(rt);
        ExecuteQueuedChain(rt);
        RemoveDeferredOverrideOriginsFromLineClear(rt);

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
                new Vector2Int(rt.Origin.X, rt.Origin.Y),
                rt.Origin));
        }

        return result;
    }

    private void RemoveDeferredOverrideOriginsFromLineClear(LineHExecutionRuntime rt)
    {
        if (rt?.Context?.DeferredLineHitOverrideCells == null || rt.Context.DeferredLineHitOverrideCells.Count == 0)
            return;

        foreach (var cell in rt.Context.DeferredLineHitOverrideCells)
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
    private void ExecuteQueuedChain(LineHExecutionRuntime rt)
    {
        if (rt.EnqueueChainSpecials == null || rt.ProcessQueue == null)
            return;

        rt.EnqueueChainSpecials(rt.Context);
        rt.ProcessQueue(rt.Context);
    }

    private bool CanExecute(LineHExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() != TileSpecial.LineH)
            return false;

        var cell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        if (rt.Context.Processed.Contains(cell))
            return false;

        return true;
    }

    private void RegisterOrigin(LineHExecutionRuntime rt)
    {
        var originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);

        rt.Context.Processed.Add(originCell);
        rt.Context.Affected.Add(rt.Origin);
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
        rt.Context.HasLineActivation = true;
    }

    private void CollectRow(LineHExecutionRuntime rt)
    {
        int y = rt.Origin.Y;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                continue;

            var cell = new Vector2Int(x, y);
            rt.Context.AffectedCells.Add(cell);

            var tile = rt.Board.Tiles[x, y];
            if (tile == null)
                continue;

            rt.Context.Affected.Add(tile);
            rt.Context.LightningVisualTargets.Add(tile);
        }
    }

    private void BuildLineVisuals(LineHExecutionRuntime rt)
    {
        rt.Context.LightningLineStrikes.Add(
            new LightningLineStrike(
                new Vector2Int(rt.Origin.X, rt.Origin.Y),
                true)); // true => horizontal
    }


    private MatchClearAction BuildClearAction(LineHExecutionRuntime rt)
    {
        var ctx = rt.Context;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
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
}
