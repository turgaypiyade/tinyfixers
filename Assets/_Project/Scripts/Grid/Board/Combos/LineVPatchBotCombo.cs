using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVPatchBotComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public PatchbotComboService PatchbotService;
    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;
}

public sealed class LineVPatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineVPatchBotCombo
{
    public LineVPatchBotComboExecutionResult Execute(LineVPatchBotComboExecutionRuntime rt)
    {
        var result = new LineVPatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var patchBotTile = GetPatchBotTile(rt);
        var lineTile = GetLineTile(rt);

        RegisterComboTiles(rt, patchBotTile, lineTile);

        var target = rt.PatchbotService.FindTarget(patchBotTile, lineTile, null);
        if (!target.hasCell)
            return result;

        int tx = target.x;
        int ty = target.y;

        float travelDuration = rt.Board.PatchbotDashUI != null
            ? rt.Board.PatchbotDashUI.EstimateDashDuration(
                rt.Board,
                new Vector2Int(patchBotTile.X, patchBotTile.Y),
                new Vector2Int(tx, ty))
            : 0.22f;

        rt.PatchbotService.EnqueueDash(patchBotTile, tx, ty);
        rt.VisualService.PlayTeleportMarkers(patchBotTile, tx, ty);
        rt.VisualService.PlayTeleportMarkers(lineTile, tx, ty);

        rt.VisualService.PlayTravelingSpecialPairGhost(
            patchBotTile,
            lineTile,
            new Vector2Int(patchBotTile.X, patchBotTile.Y),
            new Vector2Int(tx, ty),
            travelDuration,
            true);

        CollectColumnAtTarget(rt, tx);
        BuildLineVisuals(rt, tx, ty, travelDuration);
        ExecuteChain(rt);

        if (rt.FinalizeAtEnd)
        {
            var clearAction = BuildClearAction(rt);
            if (clearAction != null)
                result.Actions.Add(clearAction);
        }

        return result;
    }

    private bool CanExecute(LineVPatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot;
        bool partnerIsPatchBot = rt.Partner.GetSpecial() == TileSpecial.PatchBot;
        bool originIsLineV = rt.Origin.GetSpecial() == TileSpecial.LineV;
        bool partnerIsLineV = rt.Partner.GetSpecial() == TileSpecial.LineV;

        return (originIsPatchBot && partnerIsLineV) || (partnerIsPatchBot && originIsLineV);
    }

    private void RegisterComboTiles(LineVPatchBotComboExecutionRuntime rt, TileView patchBotTile, TileView lineTile)
    {
        AddOrigin(rt, patchBotTile);
        AddOrigin(rt, lineTile);
        rt.Context.HasLineActivation = true;
    }

    private void CollectColumnAtTarget(LineVPatchBotComboExecutionRuntime rt, int targetX)
    {
        for (int y = 0; y < rt.Board.Height; y++)
        {
            if (!SpecialUtils.CanAffectCell(rt.Board, targetX, y))
                continue;

            var cell = new Vector2Int(targetX, y);
            rt.Context.AffectedCells.Add(cell);

            var tile = rt.Board.Tiles[targetX, y];
            if (tile == null)
                continue;

            rt.Context.Affected.Add(tile);
            rt.Context.LightningVisualTargets.Add(tile);
        }
    }

    private void BuildLineVisuals(LineVPatchBotComboExecutionRuntime rt, int targetX, int targetY, float delaySeconds)
    {
        rt.Context.LightningLineStrikes.Add(
            new LightningLineStrike(
                new Vector2Int(targetX, targetY),
                false,
                delaySeconds));
    }

    private void ExecuteChain(LineVPatchBotComboExecutionRuntime rt)
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

    private void TryQueue(LineVPatchBotComboExecutionRuntime rt, Queue<TileView> pending, TileView tile)
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

    private void AddOrigin(LineVPatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private MatchClearAction BuildClearAction(LineVPatchBotComboExecutionRuntime rt)
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

    private TileView GetPatchBotTile(LineVPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
    }

    private TileView GetLineTile(LineVPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.LineV ? rt.Origin : rt.Partner;
    }
}