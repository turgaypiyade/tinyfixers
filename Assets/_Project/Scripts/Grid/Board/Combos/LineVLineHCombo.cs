using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVLineHComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    // Swap combo merkez hücresi.
    // Doluysa cross buradan başlar.
    public TileView Center;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;
}

public sealed class LineVLineHComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineVLineHCombo
{
    public LineVLineHComboExecutionResult Execute(LineVLineHComboExecutionRuntime rt)
    {
        var result = new LineVLineHComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigins(rt);

        var centerCell = GetCenterCell(rt);
        Debug.Log($"[LineVLineHCombo] delegate cross origin=virtual({centerCell.x},{centerCell.y})");

        // Ownership direction:
        // LineV+LineH combo decides the cross center.
        // Row/column collection and line visuals are delegated to LineHSpecial/LineVSpecial.
        ExecuteLineHAtVirtualOrigin(rt, centerCell);
        ExecuteLineVAtVirtualOrigin(rt, centerCell);

        ExecuteChain(rt);
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

            var center = GetCenterTile(rt);
            rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                SpecialBoardSignalType.SpecialPassFinished,
                new Vector2Int(center.X, center.Y),
                center));
        }

        Debug.Log($"[LineVLineHCombo] PRE-FINALIZE center=virtual({centerCell.x},{centerCell.y}) affected={rt.Context.Affected.Count} lightningTargets={rt.Context.LightningVisualTargets.Count} strikes={rt.Context.LightningLineStrikes.Count}");
        return result;
    }

    private void ExecuteLineHAtVirtualOrigin(LineVLineHComboExecutionRuntime rt, Vector2Int centerCell)
    {
        var lineH = new LineHSpecial();
        lineH.Execute(new LineHExecutionRuntime
        {
            Board = rt.Board,
            Context = rt.Context,
            Origin = null,
            Partner = null,
            VirtualOriginCell = centerCell,
            FinalizeAtEnd = false,
            ActivateSpecial = rt.ActivateSpecial,
            SuppressVisualSideEffects = false
        });
    }

    private void ExecuteLineVAtVirtualOrigin(LineVLineHComboExecutionRuntime rt, Vector2Int centerCell)
    {
        var lineV = new LineVSpecial();
        lineV.Execute(new LineVExecutionRuntime
        {
            Board = rt.Board,
            Context = rt.Context,
            Origin = null,
            Partner = null,
            VirtualOriginCell = centerCell,
            FinalizeAtEnd = false,
            ActivateSpecial = rt.ActivateSpecial,
            SuppressVisualSideEffects = false
        });
    }

    private void RemoveDeferredOverrideOriginsFromLineClear(LineVLineHComboExecutionRuntime rt)
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

            // Ertelenen Override'ı cross combo'nun clear action'ından çıkar.
            // DrainDeferredLineOverrides onu kendi action'ıyla temizleyecek.
            rt.Context.Affected.Remove(tile);
        }
    }

    private bool CanExecute(LineVLineHComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return IsLine(rt.Origin.GetSpecial()) && IsLine(rt.Partner.GetSpecial());
    }

    private void RegisterOrigins(LineVLineHComboExecutionRuntime rt)
    {
        var center = GetCenterTile(rt);

        AddTile(rt, rt.Origin);
        AddTile(rt, rt.Partner);

        if (center != null)
            AddTile(rt, center);

        rt.Context.HasLineActivation = true;
    }

    private void ExecuteChain(LineVLineHComboExecutionRuntime rt)
    {
        var pending = new Queue<TileView>();

        SeedCrossSpecials(rt, pending);

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

            Debug.Log($"[LineVLineHCombo] EXECUTE special={special} cell={pos}");

            if (!rt.Context.Processed.Contains(pos))
                rt.ActivateSpecial?.Invoke(rt.Context, tile, null);

            rt.Context.Processed.Add(pos);

            EnqueueNewlyAffectedSpecials(rt, pending);
        }
    }

    private void SeedCrossSpecials(LineVLineHComboExecutionRuntime rt, Queue<TileView> pending)
    {
        var center = GetCenterTile(rt);
        int cx = center.X;
        int cy = center.Y;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            var tile = rt.Board.Tiles[x, cy];
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }

        for (int y = 0; y < rt.Board.Height; y++)
        {
            var tile = rt.Board.Tiles[cx, y];
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void EnqueueNewlyAffectedSpecials(LineVLineHComboExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void TryQueue(LineVLineHComboExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
            return;

        if (tile.GetSpecial() == TileSpecial.None)
            return;

        var center = GetCenterTile(rt);
        if (tile == center)
            return;

        var pos = new Vector2Int(tile.X, tile.Y);

        if (rt.Context.Processed.Contains(pos))
            return;

        if (rt.Context.Queued.Contains(pos))
            return;

        rt.Context.Queued.Add(pos);
        pending.Enqueue(tile);
    }

    private void AddTile(LineVLineHComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private TileView GetCenterTile(LineVLineHComboExecutionRuntime rt)
    {
        return rt.Center != null ? rt.Center : (rt.Partner != null ? rt.Partner : rt.Origin);
    }

    private Vector2Int GetCenterCell(LineVLineHComboExecutionRuntime rt)
    {
        var center = GetCenterTile(rt);
        return center != null ? new Vector2Int(center.X, center.Y) : Vector2Int.zero;
    }

    private MatchClearAction BuildClearAction(LineVLineHComboExecutionRuntime rt)
    {
        var ctx = rt.Context;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
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

    private static bool IsLine(TileSpecial special)
    {
        return special == TileSpecial.LineH || special == TileSpecial.LineV;
    }
}