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
        ExecuteChain(rt);

        if (rt.FinalizeAtEnd)
        {
            List<BoardAction> fanoutActions = null;
            if (rt.ProcessFanout != null)
                fanoutActions = rt.ProcessFanout(rt.Context);

            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);

            if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
                rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

            var clearAction = BuildClearAction(rt);
            if (clearAction != null)
                result.Actions.Add(clearAction);

            if (!ShouldSuppressOverrideFanoutPresentation(rt.Context) && fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);

            rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                SpecialBoardSignalType.SpecialPassFinished,
                new Vector2Int(rt.Origin.X, rt.Origin.Y),
                rt.Origin));
        }

        return result;
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

    private void ExecuteChain(LineHExecutionRuntime rt)
    {
        var pending = new Queue<TileView>();

        SeedRowSpecials(rt, pending);

        while (pending.Count > 0)
        {
            var tile = pending.Dequeue();
            if (tile == null)
                continue;

            var pos = new Vector2Int(tile.X, tile.Y);

            if (rt.Context.Processed.Contains(pos))
                continue;

            var special = tile.GetSpecial();
            if (special == TileSpecial.None)
                continue;

            rt.Context.Queued.Remove(pos);

            if (special == TileSpecial.LineH)
            {
                Execute(new LineHExecutionRuntime
                {
                    Board = rt.Board,
                    Context = rt.Context,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    ActivateSpecial = rt.ActivateSpecial,
                    ProcessFanout = rt.ProcessFanout,
                    CleanupImplantedTiles = rt.CleanupImplantedTiles,
                    FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                    EmitBoardSignal = rt.EmitBoardSignal
                });
            }
            else
            {
                if (!rt.Context.Processed.Contains(pos))
                    rt.ActivateSpecial?.Invoke(rt.Context, tile, null);

                rt.Context.Processed.Add(pos);
            }

            EnqueueNewlyAffectedSpecials(rt, pending);
        }
    }

    private void SeedRowSpecials(LineHExecutionRuntime rt, Queue<TileView> pending)
    {
        int y = rt.Origin.Y;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            var tile = rt.Board.Tiles[x, y];
            if (tile == null || tile == rt.Origin)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void EnqueueNewlyAffectedSpecials(LineHExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void TryQueue(LineHExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
            return;

        if (tile == rt.Origin)
            return;

        if (tile.GetSpecial() == TileSpecial.None)
            return;

        var pos = new Vector2Int(tile.X, tile.Y);

        if (rt.Context.Processed.Contains(pos))
            return;

        if (rt.Context.Queued.Contains(pos))
            return;

        rt.Context.Queued.Add(pos);
        pending.Enqueue(tile);
    }

    private bool ShouldSuppressOverrideFanoutPresentation(ResolutionContext ctx)
    {
        if (ctx == null)
            return false;

        return ctx.HasLineActivation
            && ctx.OverrideFanoutOrigin != null
            && ctx.OverrideFanoutTargets.Count > 0
            && ctx.PendingOverrideImplants.Count == 0
            && ctx.OverrideDeferredPulseExplosions.Count == 0
            && ctx.OverrideDeferredPatchBotDashes.Count == 0;
    }

    private MatchClearAction BuildClearAction(LineHExecutionRuntime rt)
    {
        var ctx = rt.Context;

        HashSet<TileView> processedViews = null;
        Dictionary<TileView, float> stagger = null;

        if (ctx.HasPulseActivation)
        {
            processedViews = new HashSet<TileView>();
            foreach (var pos in ctx.Processed)
            {
                if (rt.Board.Tiles[pos.x, pos.y] != null)
                    processedViews.Add(rt.Board.Tiles[pos.x, pos.y]);
            }

            stagger = rt.Board.PulseCoreImpactService.BuildStaggerDelays(ctx.Affected, processedViews);
        }

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
            staggerDelays: stagger,
            staggerAnimTime: rt.Board.ApplySpecialChainTempo(rt.Board.PulseImpactAnimTime),
            isSpecialPhase: true,
            presentationPlan: null
        );
    }
}
