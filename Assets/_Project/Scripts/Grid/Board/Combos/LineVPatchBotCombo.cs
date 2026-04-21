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
    public Func<ResolutionContext, TileView, TileView, List<BoardAction>> ExecuteSpecialActions;
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

        rt.VisualService.PlayTeleportMarkers(patchBotTile, tx, ty);
        rt.VisualService.PlayTeleportMarkers(lineTile, tx, ty);

        /* rt.VisualService.PlayTravelingSpecialPairGhost(
             patchBotTile,
             lineTile,
             new Vector2Int(patchBotTile.X, patchBotTile.Y),
             new Vector2Int(tx, ty),
             travelDuration,
             true);*/

        rt.Context.Affected.Add(patchBotTile);
        rt.Context.Affected.Add(lineTile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, patchBotTile, rt.Board);
        SpecialCellUtils.MarkAffectedCell(rt.Context, lineTile, rt.Board);

        if (rt.FinalizeAtEnd)
        {
            var initialClearAction = new MatchClearAction(
                new HashSet<TileView> { patchBotTile, lineTile },
                doShake: false,
                animationMode: ClearAnimationMode.Default,
                isSpecialPhase: true
            );
            result.Actions.Add(initialClearAction);
        }

        rt.Board.ActiveBackgroundJobs++;

        rt.PatchbotService.EnqueueDash(patchBotTile, tx, ty, lineTile, null, () =>
                {
                    var arrivalCtx = new ResolutionContext();
                    arrivalCtx.HasLineActivation = true;
                    var arrivalRt = new LineVPatchBotComboExecutionRuntime
                    {
                        Board = rt.Board,
                        Context = arrivalCtx,
                        Origin = patchBotTile,
                        Partner = lineTile,
                        PatchbotService = rt.PatchbotService,
                        VisualService = rt.VisualService,
                        Effects = rt.Effects,
                        ActivateSpecial = rt.ActivateSpecial,
                        ExecuteSpecialActions = rt.ExecuteSpecialActions,
                        FinalizeAtEnd = true
                    };

                    CollectColumnAtTarget(arrivalRt, tx);
                    BuildLineVisuals(arrivalRt, tx, ty, 0f);

                    var deferredActions = new List<BoardAction>();
                    if (arrivalRt.FinalizeAtEnd)
                    {
                        ExecuteChain(arrivalRt, deferredActions);

                        var clearAction = BuildClearAction(arrivalRt);
                        if (clearAction != null)
                            deferredActions.Add(clearAction);
                    }

                    var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
                    if (sequencer != null && deferredActions.Count > 0)
                    {
                        sequencer.Enqueue(deferredActions);
                    }

                    rt.Board.ActiveBackgroundJobs--;
                });

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

            SpecialCellUtils.MarkAffectedCell(rt.Context, targetX, y, rt.Board);

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

    private void ExecuteChain(LineVPatchBotComboExecutionRuntime rt, List<BoardAction> deferredActions)
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

            if (rt.ExecuteSpecialActions != null)
            {
                rt.Context.Processed.Remove(pos);

                var nestedActions = rt.ExecuteSpecialActions(rt.Context, tile, null);

                rt.Context.Processed.Add(pos);

                if (nestedActions != null && nestedActions.Count > 0)
                    deferredActions.AddRange(nestedActions);
            }
            else
            {
                rt.ActivateSpecial?.Invoke(rt.Context, tile, null);
                rt.Context.Processed.Add(pos);
            }

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

    private TileView GetPatchBotTile(LineVPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
    }

    private TileView GetLineTile(LineVPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.LineV ? rt.Origin : rt.Partner;
    }
}