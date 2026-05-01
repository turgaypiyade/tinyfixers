using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotComboExecutionRuntime
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

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
}

public sealed class PatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PatchBotCombo
{
    public PatchBotComboExecutionResult Execute(PatchBotComboExecutionRuntime rt)
    {
        var result = new PatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var firstPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
        var secondPatchBot = firstPatchBot == rt.Origin ? rt.Partner : rt.Origin;

        RegisterComboTiles(rt, firstPatchBot, secondPatchBot);

        ComboBehaviorEvents.EmitComboTriggered(
            TileSpecial.PatchBot,
            TileSpecial.PatchBot,
            new Vector2Int(firstPatchBot.X, firstPatchBot.Y));

        rt.Context.Affected.Add(firstPatchBot);
        rt.Context.Affected.Add(secondPatchBot);
        SpecialCellUtils.MarkAffectedCell(rt.Context, firstPatchBot, rt.Board);
        SpecialCellUtils.MarkAffectedCell(rt.Context, secondPatchBot, rt.Board);

        var initialClearAction = new MatchClearAction(
            new HashSet<TileView> { firstPatchBot, secondPatchBot },
            doShake: false,
            animationMode: ClearAnimationMode.Default,
            isSpecialPhase: true
        );
        result.Actions.Add(initialClearAction);

        var usedTargets = new HashSet<TileView>();
        var coordinator = new PatchBotTargetCoordinator(rt.Board, rt.PatchbotService);

        ExecuteSingleDash(rt, coordinator, firstPatchBot, secondPatchBot, usedTargets);
        ExecuteSingleDash(rt, coordinator, secondPatchBot, firstPatchBot, usedTargets);

        if (rt.FinalizeAtEnd)
        {
            // PatchBot + PatchBot combo kendi içinde iki dash üretir.
            // Context içinde iki source PatchBot hâlâ Affected olarak durduğu için
            // ProcessFanout burada çalışırsa aynı PatchBot'lar tekrar chain activation'a girip
            // ikinci kez dash üretebilir.
            //
            // Bu yüzden PatchBot + PatchBot combo'da fanout'u burada çalıştırmıyoruz.
            // Arrival tarafı zaten kendi hedef clear action'larını enqueue ediyor.
            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);
        }

        return result;
    }

    private bool CanExecute(PatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.PatchbotService == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.PatchBot &&
               rt.Partner.GetSpecial() == TileSpecial.PatchBot;
    }

    private void RegisterComboTiles(
        PatchBotComboExecutionRuntime rt,
        TileView a,
        TileView b)
    {
        if (a != null)
        {
            rt.Context.Affected.Add(a);
            SpecialCellUtils.MarkAffectedCell(rt.Context, a, rt.Board);
        }

        if (b != null)
        {
            rt.Context.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(rt.Context, b, rt.Board);
        }
    }

    private void ExecuteSingleDash(
        PatchBotComboExecutionRuntime rt,
        PatchBotTargetCoordinator coordinator,
        TileView actor,
        TileView otherPatchBot,
        HashSet<TileView> usedTargets)
    {
        if (rt == null || rt.Board == null || rt.PatchbotService == null || coordinator == null)
            return;

        if (actor == null)
            return;

        var picked = coordinator.PickIntent(actor, otherPatchBot, usedTargets);
        if (!picked.hasIntent || picked.intent == null)
            return;

        var initialCell = picked.intent.CurrentCell(rt.Board);
        if (!IsInside(rt.Board, initialCell.x, initialCell.y))
            initialCell = picked.intent.InitialCell;

        if (!IsInside(rt.Board, initialCell.x, initialCell.y))
        {
            coordinator.ReleaseIntent(picked.intent);
            return;
        }

        if (picked.intent.TargetTile != null)
            usedTargets.Add(picked.intent.TargetTile);

        if (rt.VisualService != null)
            rt.VisualService.PlayTeleportMarkers(actor, initialCell.x, initialCell.y);

        rt.PatchbotService.EnqueueDashFromIntent(
            actor,
            picked.intent,
            coordinator,
            null,
            null,
            (hitX, hitY, liveIntent) =>
            {
                try
                {
                    if (rt == null || rt.Board == null || rt.PatchbotService == null)
                        return;

                    if (!IsInside(rt.Board, hitX, hitY))
                        return;

                    var arrivalCtx = new ResolutionContext();
                    var dataMatches = new HashSet<TileData>();

                    TileView liveTargetTile = rt.Board.Tiles[hitX, hitY];

                    rt.PatchbotService.HitCellOnce(
                        dataMatches,
                        hitX,
                        hitY,
                        liveTargetTile,
                        (x, y) => SpecialCellUtils.MarkAffectedCell(arrivalCtx, x, y, rt.Board),
                        tile =>
                        {
                            if (tile != null)
                                SpecialCellUtils.MarkAffectedCell(arrivalCtx, tile, rt.Board);
                        });

                    foreach (var data in dataMatches)
                    {
                        if (data == null) continue;
                        if (!IsInside(rt.Board, data.X, data.Y)) continue;

                        var tile = rt.Board.Tiles[data.X, data.Y];
                        if (tile == null) continue;

                        arrivalCtx.Affected.Add(tile);
                    }

                    if (arrivalCtx.Affected.Count == 0 &&
                        (arrivalCtx.AffectedCells == null || arrivalCtx.AffectedCells.Count == 0) &&
                        (arrivalCtx.ImpactCells == null || arrivalCtx.ImpactCells.Count == 0))
                    {
                        return;
                    }

                    var clearAction = BuildClearAction(arrivalCtx);

                    var deferredActions = new List<BoardAction>();
                    if (clearAction != null)
                        deferredActions.Add(clearAction);

                    var sequencer = rt.Board.GetComponent<ActionSequencer>();
                    if (sequencer != null && deferredActions.Count > 0)
                        sequencer.Enqueue(deferredActions);
                }
                finally
                {
                    coordinator.ReleaseIntent(liveIntent ?? picked.intent);
                }
            });
    }

    private static bool IsInside(BoardController board, int x, int y)
    {
        return board != null &&
               x >= 0 && x < board.Width &&
               y >= 0 && y < board.Height;
    }


    private MatchClearAction BuildClearAction(ResolutionContext ctx)
    {
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
            enqueueCascadeOnComplete: true
        );
    }
}
