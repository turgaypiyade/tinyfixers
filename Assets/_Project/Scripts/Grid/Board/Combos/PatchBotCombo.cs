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

        var usedTargets = new HashSet<TileView>();
        var dataMatches = new HashSet<TileData>();

        ExecuteSingleDash(rt, firstPatchBot, secondPatchBot, usedTargets, dataMatches);
        ExecuteSingleDash(rt, secondPatchBot, firstPatchBot, usedTargets, dataMatches);

        foreach (var data in dataMatches)
        {
            if (data == null)
                continue;

            if (data.X < 0 || data.X >= rt.Board.Width || data.Y < 0 || data.Y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[data.X, data.Y];
            if (tile == null)
                continue;

            rt.Context.Affected.Add(tile);
            SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
        }

        if (rt.FinalizeAtEnd)
            Finalize(rt, result);

        return result;
    }

    private bool CanExecute(PatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
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
        TileView actor,
        TileView otherPatchBot,
        HashSet<TileView> usedTargets,
        HashSet<TileData> dataMatches)
    {
        if (actor == null)
            return;

        var target = rt.PatchbotService.FindTarget(actor, otherPatchBot, usedTargets);
        if (!target.hasCell)
            return;

        if (target.tile != null)
            usedTargets.Add(target.tile);

        rt.PatchbotService.EnqueueDash(actor, target.x, target.y);
        rt.VisualService.PlayTeleportMarkers(actor, target.x, target.y);

        rt.PatchbotService.HitCellOnce(
            dataMatches,
            target.x,
            target.y,
            target.tile,
            (x, y) => SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board),
            tile => SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board));
    }

    private void Finalize(
        PatchBotComboExecutionRuntime rt,
        PatchBotComboExecutionResult result)
    {
        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        if (rt.Context.OverrideRadialClearDelays != null &&
            rt.Context.OverrideRadialClearDelays.Count > 0)
        {
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(
                rt.Context.Affected,
                rt.Context.OverrideRadialClearDelays);
        }

        result.Actions.Add(BuildClearAction(rt.Context));
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
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }
}