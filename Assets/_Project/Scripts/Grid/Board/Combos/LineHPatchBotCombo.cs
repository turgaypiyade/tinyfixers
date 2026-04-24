using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineHPatchBotComboExecutionRuntime
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

public sealed class LineHPatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineHPatchBotCombo
{
    public LineHPatchBotComboExecutionResult Execute(LineHPatchBotComboExecutionRuntime rt)
    {
        var result = new LineHPatchBotComboExecutionResult();

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
            try
            {
                var arrivalCtx = new ResolutionContext();
                var targetCell = new Vector2Int(tx, ty);
                var deferredActions = ExecuteLineHAtTarget(rt, arrivalCtx, targetCell);

                var sequencer = rt.Board.GetComponent<ActionSequencer>();
                if (sequencer != null && deferredActions.Count > 0)
                {
                    sequencer.Enqueue(deferredActions);
                }
            }
            finally
            {
                rt.Board.ActiveBackgroundJobs--;
            }
        });

        return result;
    }

    private List<BoardAction> ExecuteLineHAtTarget(
        LineHPatchBotComboExecutionRuntime rt,
        ResolutionContext arrivalCtx,
        Vector2Int targetCell)
    {
        var actions = new List<BoardAction>();

        if (rt == null || rt.Board == null || arrivalCtx == null)
            return actions;

        var dispatcher = new SpecialBehaviorDispatcher(
            rt.Board,
            rt.PatchbotService,
            rt.VisualService,
            rt.Effects);

        var queueProcessor = new ActivationQueueProcessor(rt.Board, dispatcher);
        dispatcher.QueueProcessor = queueProcessor;

        var implantService = new SpecialImplantService(
            rt.Board,
            rt.PatchbotService,
            rt.VisualService,
            queueProcessor);

        var fanoutService = new SpecialFanoutService(
            rt.Board,
            implantService,
            queueProcessor,
            rt.VisualService);

        var lineH = new LineHSpecial();
        var result = lineH.Execute(new LineHExecutionRuntime
        {
            Board = rt.Board,
            Context = arrivalCtx,
            Origin = null,
            Partner = null,
            VirtualOriginCell = targetCell,
            FinalizeAtEnd = true,
            ActivateSpecial = dispatcher.ApplySpecialActivation,
            ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
            CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
            FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                rt.VisualService.FireOverrideOverrideSpecialVisuals(affected, delays),
            EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
            ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
        });

        if (result != null && result.Actions != null && result.Actions.Count > 0)
            actions.AddRange(result.Actions);

        DrainDeferredLineOverrides(
            rt,
            arrivalCtx,
            actions,
            fanoutService,
            implantService);

        return actions;
    }

    private void DrainDeferredLineOverrides(
        LineHPatchBotComboExecutionRuntime rt,
        ResolutionContext context,
        List<BoardAction> actions,
        SpecialFanoutService fanoutService,
        SpecialImplantService implantService)
    {
        if (rt == null || context == null || actions == null)
            return;

        if (context.DeferredLineHitOverrideCells == null || context.DeferredLineHitOverrideCells.Count == 0)
            return;

        var deferred = new List<Vector2Int>(context.DeferredLineHitOverrideCells);
        context.DeferredLineHitOverrideCells.Clear();

        var overrideSpecial = new OverrideSpecial();

        foreach (var cell in deferred)
        {
            if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            if (tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            context.Processed.Remove(cell);
            context.Queued.Remove(cell);

            var overrideResult = overrideSpecial.Execute(new OverrideExecutionRuntime
            {
                Board = rt.Board,
                Context = context,
                Origin = tile,
                Partner = null,
                FinalizeAtEnd = true,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                    rt.VisualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            if (overrideResult != null && overrideResult.Actions != null && overrideResult.Actions.Count > 0)
                actions.AddRange(overrideResult.Actions);
        }
    }

    private bool CanExecute(LineHPatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot;
        bool partnerIsPatchBot = rt.Partner.GetSpecial() == TileSpecial.PatchBot;
        bool originIsLineH = rt.Origin.GetSpecial() == TileSpecial.LineH;
        bool partnerIsLineH = rt.Partner.GetSpecial() == TileSpecial.LineH;

        return (originIsPatchBot && partnerIsLineH) || (partnerIsPatchBot && originIsLineH);
    }

    private void RegisterComboTiles(LineHPatchBotComboExecutionRuntime rt, TileView patchBotTile, TileView lineTile)
    {
        AddOrigin(rt, patchBotTile);
        AddOrigin(rt, lineTile);
        rt.Context.HasLineActivation = true;
    }

    private void AddOrigin(LineHPatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private TileView GetPatchBotTile(LineHPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
    }

    private TileView GetLineTile(LineHPatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.LineH ? rt.Origin : rt.Partner;
    }
}