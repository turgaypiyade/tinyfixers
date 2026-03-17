using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    // true => top-level çağrı, final clear action üretir
    // false => nested chain çağrısı, sadece context büyütür
    public bool FinalizeAtEnd;

    // LineV dışındaki special'ları mevcut yoldan çağırmak için
    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    // top-level finalize hook'ları
    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;
}

public sealed class LineVExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public enum SpecialBoardSignalType
{
    None = 0,
    TileClearedBySpecial = 1,
    SpecialPassFinished = 2,
}

public readonly struct SpecialBoardSignal
{
    public readonly SpecialBoardSignalType Type;
    public readonly Vector2Int Cell;
    public readonly TileView Tile;

    public SpecialBoardSignal(SpecialBoardSignalType type, Vector2Int cell, TileView tile)
    {
        Type = type;
        Cell = cell;
        Tile = tile;
    }
}

public sealed class LineVSpecial
{
    public LineVExecutionResult Execute(LineVExecutionRuntime rt)
    {
        var result = new LineVExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigin(rt);
        CollectColumn(rt);
        BuildLineVisuals(rt);
        ExecuteChain(rt);

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

    private bool CanExecute(LineVExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() != TileSpecial.LineV)
            return false;

        var cell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        if (rt.Context.Processed.Contains(cell))
            return false;

        return true;
    }

    private void RegisterOrigin(LineVExecutionRuntime rt)
    {
        var originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);

        rt.Context.Processed.Add(originCell);
        rt.Context.Affected.Add(rt.Origin);
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
        rt.Context.HasLineActivation = true;
    }

    private void CollectColumn(LineVExecutionRuntime rt)
    {
        int x = rt.Origin.X;

        for (int y = 0; y < rt.Board.Height; y++)
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

    private void BuildLineVisuals(LineVExecutionRuntime rt)
    {
        rt.Context.LightningLineStrikes.Add(
            new LightningLineStrike(
                new Vector2Int(rt.Origin.X, rt.Origin.Y),
                false)); // false => vertical
    }

    private void ExecuteChain(LineVExecutionRuntime rt)
    {
        var pending = new Queue<TileView>();

        SeedColumnSpecials(rt, pending);

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

            if (special == TileSpecial.LineV)
            {
                Execute(new LineVExecutionRuntime
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

    private void SeedColumnSpecials(LineVExecutionRuntime rt, Queue<TileView> pending)
    {
        int x = rt.Origin.X;

        for (int y = 0; y < rt.Board.Height; y++)
        {
            var tile = rt.Board.Tiles[x, y];
            if (tile == null || tile == rt.Origin)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void EnqueueNewlyAffectedSpecials(LineVExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void TryQueue(LineVExecutionRuntime rt, Queue<TileView> pending, TileView tile)
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

    private MatchClearAction BuildClearAction(LineVExecutionRuntime rt)
    {
        var ctx = rt.Context;

        // Burada presentation plan kullanmıyoruz.
        // Çünkü LineV'nin animasyon bitiş / hit / clear sırasını,
        // eski çalışan lightning-strike akışına en yakın şekilde korumak istiyoruz.
        // Ama ownership yine LineVSpecial'da kalıyor.
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