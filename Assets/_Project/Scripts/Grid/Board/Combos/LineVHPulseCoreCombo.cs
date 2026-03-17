using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVHPulseCoreComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;

    // Mevcut swap semantiğinde:
    // Origin = line tile
    // Partner = pulse tile
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;
    public Action<TileSpecial, TileSpecial, Vector2Int> EmitComboTriggered;
    public Action<Vector2Int> EmitPulseEmitterComboTriggered;
}

public sealed class LineVHPulseCoreComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineVHPulseCoreCombo
{
    public LineVHPulseCoreComboExecutionResult Execute(LineVHPulseCoreComboExecutionRuntime rt)
    {
        var result = new LineVHPulseCoreComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var pulseTile = GetPulseTile(rt);
        var lineTile = GetLineTile(rt);
        var pulseCell = new Vector2Int(pulseTile.X, pulseTile.Y);

        rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), pulseCell);
        rt.EmitPulseEmitterComboTriggered?.Invoke(pulseCell);

        var pulseAction = PulseLineCombo.CreatePulseEmitterComboAction(rt.Board, pulseTile.X, pulseTile.Y);
        if (pulseAction != null)
            result.Actions.Add(pulseAction);

        RegisterComboTiles(rt, lineTile, pulseTile);

        BuildAffectedArea(rt, lineTile, pulseTile);
        ExpandChain(rt);

        if (rt.FinalizeAtEnd && rt.Context.Affected.Count > 0)
        {
            var chainMode = rt.Context.HasLineActivation
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            result.Actions.Add(new MatchClearAction(
                rt.Context.Affected,
                doShake: true,
                animationMode: chainMode,
                affectedCells: rt.Context.AffectedCells,
                obstacleHitContext: null,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: null,
                lightningOriginCell: null,
                lightningVisualTargets: rt.Context.LightningVisualTargets,
                lightningLineStrikes: rt.Context.LightningLineStrikes,
                isSpecialPhase: true,
                presentationPlan: null
            ));
        }

        return result;
    }

    private bool CanExecute(LineVHPulseCoreComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsLine = IsLine(rt.Origin.GetSpecial());
        bool partnerIsLine = IsLine(rt.Partner.GetSpecial());
        bool originIsPulse = IsPulse(rt.Origin.GetSpecial());
        bool partnerIsPulse = IsPulse(rt.Partner.GetSpecial());

        return (originIsLine && partnerIsPulse) || (originIsPulse && partnerIsLine);
    }

    private void RegisterComboTiles(LineVHPulseCoreComboExecutionRuntime rt, TileView lineTile, TileView pulseTile)
    {
        AddOrigin(rt, lineTile);
        AddOrigin(rt, pulseTile);
        rt.Context.HasLineActivation = true;
    }

    private void BuildAffectedArea(LineVHPulseCoreComboExecutionRuntime rt, TileView lineTile, TileView pulseTile)
    {
        int cx = pulseTile.X;
        int cy = pulseTile.Y;

        if (lineTile.GetSpecial() == TileSpecial.LineH)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = 0; x < rt.Board.Width; x++)
                    AddCell(rt, x, y, horizontalStrike: true, centerX: cx, centerY: y);
        }
        else
        {
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = 0; y < rt.Board.Height; y++)
                    AddCell(rt, x, y, horizontalStrike: false, centerX: x, centerY: cy);
        }
    }

    private void ExpandChain(LineVHPulseCoreComboExecutionRuntime rt)
    {
        var pending = new Queue<TileView>();

        foreach (var tile in rt.Context.Affected)
            TryQueue(rt, pending, tile);

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

            rt.ActivateSpecial?.Invoke(rt.Context, tile, null);
            rt.Context.Processed.Add(pos);

            foreach (var affectedTile in rt.Context.Affected)
                TryQueue(rt, pending, affectedTile);
        }
    }

    private void AddCell(LineVHPulseCoreComboExecutionRuntime rt, int x, int y, bool horizontalStrike, int centerX, int centerY)
    {
        if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
            return;

        if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
            return;

        var cell = new Vector2Int(x, y);
        rt.Context.AffectedCells.Add(cell);

        var tile = rt.Board.Tiles[x, y];
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        rt.Context.LightningVisualTargets.Add(tile);
        rt.Context.HasLineActivation = true;
    }

    private void AddOrigin(LineVHPulseCoreComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private void TryQueue(LineVHPulseCoreComboExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
            return;

        if (tile.GetSpecial() == TileSpecial.None)
            return;

        if (tile == rt.Origin || tile == rt.Partner)
            return;

        var pos = new Vector2Int(tile.X, tile.Y);

        if (rt.Context.Processed.Contains(pos))
            return;

        if (rt.Context.Queued.Contains(pos))
            return;

        rt.Context.Queued.Add(pos);
        pending.Enqueue(tile);
    }

    private TileView GetPulseTile(LineVHPulseCoreComboExecutionRuntime rt)
    {
        return IsPulse(rt.Origin.GetSpecial()) ? rt.Origin : rt.Partner;
    }

    private TileView GetLineTile(LineVHPulseCoreComboExecutionRuntime rt)
    {
        return IsLine(rt.Origin.GetSpecial()) ? rt.Origin : rt.Partner;
    }

    private static bool IsLine(TileSpecial s)
    {
        return s == TileSpecial.LineH || s == TileSpecial.LineV;
    }

    private static bool IsPulse(TileSpecial s)
    {
        return s == TileSpecial.PulseCore;
    }
}
