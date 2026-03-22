using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotExecutionRuntime
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
    public Action<SpecialBoardSignal> EmitBoardSignal;
}

public sealed class PatchBotExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PatchBotSpecial
{
    public PatchBotExecutionResult Execute(PatchBotExecutionRuntime rt)
    {
        var result = new PatchBotExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigin(rt);

        if (rt.Partner != null)
        {
            if (ApplyPatchBotTeleportHit(rt, rt.Origin, rt.Partner))
                rt.Context.HasLineActivation = true;
        }
        else
        {
            ApplyPatchBotSoloHit(rt, rt.Origin);
        }

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

    private bool CanExecute(PatchBotExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() != TileSpecial.PatchBot)
            return false;

        return true;
    }

    private void RegisterOrigin(PatchBotExecutionRuntime rt)
    {
        rt.Context.Affected.Add(rt.Origin);
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
        rt.Context.Processed.Add(new Vector2Int(rt.Origin.X, rt.Origin.Y));
    }

    private void ApplyPatchBotSoloHit(PatchBotExecutionRuntime rt, TileView patchBotTile)
    {
        if (patchBotTile == null) return;

        var target = rt.PatchbotService.FindTarget(patchBotTile, null, null);
        if (!target.hasCell) return;

        rt.PatchbotService.EnqueueDash(patchBotTile, target.x, target.y);
        rt.VisualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool hasObstacleAtTarget = rt.PatchbotService.HasObstacleAt(target.x, target.y);
        var dataMatches = new HashSet<TileData>();
        rt.PatchbotService.ResolveTargetImpact(dataMatches, target.x, target.y, hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board));

        foreach (var data in dataMatches)
            if (rt.Board.Tiles[data.X, data.Y] != null) rt.Context.Affected.Add(rt.Board.Tiles[data.X, data.Y]);
    }

    private bool ApplyPatchBotTeleportHit(PatchBotExecutionRuntime rt, TileView patchBotTile, TileView partnerTile)
    {
        if (patchBotTile == null || partnerTile == null) return false;

        var target = rt.PatchbotService.FindTarget(patchBotTile, partnerTile, null);
        if (!target.hasCell) return false;

        rt.PatchbotService.EnqueueDash(patchBotTile, target.x, target.y);
        rt.VisualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool partnerIsSpecial = partnerTile.GetSpecial() != TileSpecial.None;
        if (partnerIsSpecial)
            return TriggerPartnerEffectAt(rt, patchBotTile, partnerTile, target.x, target.y);

        ApplyPatchBotTeleportToCell(rt, patchBotTile, partnerTile, target.x, target.y);
        return false;
    }

    private void ApplyPatchBotTeleportToCell(PatchBotExecutionRuntime rt, TileView patchBotTile, TileView partnerTile, int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= rt.Board.Width || targetY < 0 || targetY >= rt.Board.Height) return;

        bool hasObstacleAtTarget = rt.PatchbotService.HasObstacleAt(targetX, targetY);
        if (rt.Board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        rt.PatchbotService.ConsumePatchBotOnly(
            rt.Context.Affected,
            patchBotTile,
            (tile) => SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board));

        var matchDatas = new HashSet<TileData>();
        rt.PatchbotService.ResolveTargetImpact(
            matchDatas,
            targetX,
            targetY,
            hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board));

        foreach (var data in matchDatas)
            if (rt.Board.Tiles[data.X, data.Y] != null)
                rt.Context.Affected.Add(rt.Board.Tiles[data.X, data.Y]);
    }

    private bool TriggerPartnerEffectAt(PatchBotExecutionRuntime rt, TileView patchBotTile, TileView partnerTile, int originX, int originY)
    {
        var special = partnerTile.GetSpecial();
        if (special == TileSpecial.None) return false;

        if (special == TileSpecial.LineH)
        {
            rt.VisualService.PlayTeleportMarkers(partnerTile, originX, originY);
            rt.VisualService.PlayTransientSpecialVisualAt(partnerTile, originX, originY);

            for (int x = 0; x < rt.Board.Width; x++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, x, originY))
                    continue;

                SpecialCellUtils.MarkAffectedCell(rt.Context, x, originY, rt.Board);
                if (rt.Board.Tiles[x, originY] != null)
                {
                    rt.Context.Affected.Add(rt.Board.Tiles[x, originY]);
                    rt.Context.LightningVisualTargets.Add(rt.Board.Tiles[x, originY]);
                }
            }

            rt.Context.LightningLineStrikes.Add(new LightningLineStrike(new Vector2Int(originX, originY), true));
            return true;
        }

        if (special == TileSpecial.LineV)
        {
            rt.VisualService.PlayTeleportMarkers(partnerTile, originX, originY);
            rt.VisualService.PlayTransientSpecialVisualAt(partnerTile, originX, originY);

            for (int y = 0; y < rt.Board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, originX, y))
                    continue;

                SpecialCellUtils.MarkAffectedCell(rt.Context, originX, y, rt.Board);
                if (rt.Board.Tiles[originX, y] != null)
                {
                    rt.Context.Affected.Add(rt.Board.Tiles[originX, y]);
                    rt.Context.LightningVisualTargets.Add(rt.Board.Tiles[originX, y]);
                }
            }

            rt.Context.LightningLineStrikes.Add(new LightningLineStrike(new Vector2Int(originX, originY), false));
            return true;
        }

        if (special == TileSpecial.PulseCore)
        {
            rt.VisualService.PlayTeleportMarkers(partnerTile, originX, originY);
            rt.VisualService.PlayTransientSpecialVisualAt(partnerTile, originX, originY);
            rt.Effects.PlayPulseExplosionAt(originX, originY);
            SpecialCellUtils.AddSquare(rt.Context.Affected, rt.Context, rt.Board, originX, originY, 2);
            return false;
        }

        if (special == TileSpecial.SystemOverride)
        {
            rt.VisualService.PlayTeleportMarkers(partnerTile, originX, originY);
            TriggerSystemOverridePatchBotConversion(rt, patchBotTile, partnerTile);
        }

        return false;
    }

    private void TriggerSystemOverridePatchBotConversion(PatchBotExecutionRuntime rt, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (systemOverrideTile == null) return;

        TileType baseType = systemOverrideTile.GetOverrideBaseType(out var storedType)
            ? storedType
            : systemOverrideTile.GetTileType();

        int activationIndex = 0;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            for (int y = 0; y < rt.Board.Height; y++)
            {
                if (rt.Board.Holes[x, y]) continue;

                var tile = rt.Board.Tiles[x, y];
                if (tile == null || tile == patchBotTile || tile == systemOverrideTile) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                SpecialCellUtils.SyncAfterSpecialChange(rt.Board, tile);

                AutoPatchBotTeleportHitAndVanish(rt, tile, patchBotTile, systemOverrideTile, activationIndex);
                activationIndex++;
            }
        }
    }

    private void AutoPatchBotTeleportHitAndVanish(
        PatchBotExecutionRuntime rt,
        TileView autoPatchBot,
        TileView patchBotTile,
        TileView systemOverrideTile,
        int activationIndex)
    {
        if (autoPatchBot == null) return;

        rt.Context.Affected.Add(autoPatchBot);
        SpecialCellUtils.MarkAffectedCell(rt.Context, autoPatchBot, rt.Board);

        var sourceCell = new Vector2Int(autoPatchBot.X, autoPatchBot.Y);
        var sourceType = autoPatchBot.GetTileType();

        var target = rt.PatchbotService.FindTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        const float sequentialActivationStep = 0.01f;
        float dashDelay = Mathf.Max(0, activationIndex) * sequentialActivationStep;

        rt.VisualService.FireImmediateDash(
            autoPatchBot.X,
            autoPatchBot.Y,
            target.x,
            target.y,
            dashDelay,
            onDashStart: () =>
            {
                if (autoPatchBot == null) return;

                SpecialVisualService.HideTileVisualForCombo(autoPatchBot);

                if (sourceCell.x < 0 || sourceCell.x >= rt.Board.Width || sourceCell.y < 0 || sourceCell.y >= rt.Board.Height)
                    return;

                if (rt.Board.Tiles[sourceCell.x, sourceCell.y] == autoPatchBot)
                {
                    rt.Board.ClearCell(sourceCell.x, sourceCell.y);
                    rt.Board.ClearCellVisualOnly(sourceCell, sourceType, autoPatchBot);
                }
            });

        var matchSetData = new HashSet<TileData>();
        rt.PatchbotService.HitCellOnce(
            matchSetData,
            target.x,
            target.y,
            target.tile,
            (x, y) => SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board));

        foreach (var data in matchSetData)
        {
            if (rt.Board.Tiles[data.X, data.Y] != null)
                rt.Context.Affected.Add(rt.Board.Tiles[data.X, data.Y]);
        }
    }

    private MatchClearAction BuildClearAction(PatchBotExecutionRuntime rt)
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
}
