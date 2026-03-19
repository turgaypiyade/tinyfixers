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

    public PulseCoreExecutionResult Execute(PulseCoreExecutionRuntime rt)
    {
        var result = new PulseCoreExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigin(rt);
        CollectArea(rt, rt.Origin.X, rt.Origin.Y);
        ExecuteChain(rt);

        if (rt.FinalizeAtEnd)
        {
            Finalize(rt, result, rt.Origin.X, rt.Origin.Y);
        }

        return result;
    }

    public PulseCoreExecutionResult ExecuteAtTarget(PulseCoreExecutionRuntime rt, int targetX, int targetY)
    {
        var result = new PulseCoreExecutionResult();

        if (!CanExecute(rt))
            return result;

        if (targetX < 0 || targetX >= rt.Board.Width || targetY < 0 || targetY >= rt.Board.Height)
            return result;

        RegisterOrigin(rt);
        CollectArea(rt, targetX, targetY);
        ExecuteChain(rt);

        if (rt.FinalizeAtEnd)
        {
            Finalize(rt, result, targetX, targetY);
        }

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
        if (rt.Context.Processed.Contains(cell))
            return false;

        return true;
    }

    private void RegisterOrigin(PulseCoreExecutionRuntime rt)
    {
        var originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);

        rt.Context.Processed.Add(originCell);
        rt.Context.Affected.Add(rt.Origin);
        rt.Context.HasPulseActivation = true;
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
    }

    private void CollectArea(PulseCoreExecutionRuntime rt, int centerX, int centerY)
    {
        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(centerX, centerY));

        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0) side += 1;
        int half = side / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
            for (int y = centerY - half; y <= centerY + half; y++)
            {
                if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
                    continue;

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

    private void ExecuteChain(PulseCoreExecutionRuntime rt)
    {
        var pending = new Queue<TileView>();

        SeedAreaSpecials(rt, pending);

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

            if (special == TileSpecial.PulseCore)
            {
                Execute(new PulseCoreExecutionRuntime
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

    private void SeedAreaSpecials(PulseCoreExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var cell in rt.Context.AffectedCells)
        {
            if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[cell.x, cell.y];
            if (tile == null || tile == rt.Origin)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void EnqueueNewlyAffectedSpecials(PulseCoreExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void TryQueue(PulseCoreExecutionRuntime rt, Queue<TileView> pending, TileView tile)
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
            animationMode: ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: null,
            lightningLineStrikes: null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
    }
}