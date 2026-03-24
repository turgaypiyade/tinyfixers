using System;
using System.Collections;
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

        rt.Context.Affected.Add(rt.Origin);
        if (rt.Partner != null) rt.Context.Affected.Add(rt.Partner);
        SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
        if (rt.Partner != null) SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Partner, rt.Board);

        rt.Context.Processed.Add(new Vector2Int(rt.Origin.X, rt.Origin.Y));

        // Only create the initial clear action when FinalizeAtEnd is true (solo activation).
        // When FinalizeAtEnd is false (chain activation), the parent resolution owns the clear.
        if (rt.FinalizeAtEnd)
        {
            var initialTiles = new HashSet<TileView> { rt.Origin };
            if (rt.Partner != null) initialTiles.Add(rt.Partner);

            var initialClearAction = new MatchClearAction(
                initialTiles,
                doShake: false,
                animationMode: ClearAnimationMode.Default,
                isSpecialPhase: true
            );
            result.Actions.Add(initialClearAction);
        }

        var target = rt.PatchbotService.FindTarget(rt.Origin, rt.Partner, null);
        if (!target.hasCell) return result;

        var cachedPartnerSpecial = rt.Partner != null ? rt.Partner.GetSpecial() : TileSpecial.None;
        rt.VisualService.PlayTeleportMarkers(rt.Origin, target.x, target.y);

        rt.PatchbotService.EnqueueDash(rt.Origin, target.x, target.y, null, () =>
        {
            var arrivalCtx = new ResolutionContext();
            var arrivalRt = new PatchBotExecutionRuntime
            {
                Board = rt.Board,
                Context = arrivalCtx,
                Origin = null,
                Partner = null,
                PatchbotService = rt.PatchbotService,
                VisualService = rt.VisualService,
                Effects = rt.Effects,
                ActivateSpecial = rt.ActivateSpecial,
                ProcessFanout = rt.ProcessFanout,
                CleanupImplantedTiles = rt.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = rt.EmitBoardSignal,
                FinalizeAtEnd = true
            };

            if (cachedPartnerSpecial != TileSpecial.None)
            {
                if (TriggerPartnerEffectAtDeferred(arrivalRt, cachedPartnerSpecial, target.x, target.y))
                    arrivalRt.Context.HasLineActivation = true;
            }
            else if (rt.Partner != null)
            {
                ApplyPatchBotTeleportToCellDeferred(arrivalRt, target.x, target.y);
            }
            else
            {
                ApplyPatchBotSoloHitDeferred(arrivalRt, target.x, target.y);
            }

            var deferredActions = new List<BoardAction>();
            if (arrivalRt.FinalizeAtEnd)
            {
                if (arrivalRt.ProcessFanout != null)
                {
                    var fanoutActions = arrivalRt.ProcessFanout(arrivalRt.Context);
                    if (fanoutActions != null && fanoutActions.Count > 0)
                        deferredActions.AddRange(fanoutActions);
                }

                if (arrivalRt.Context.OverrideDeferredPulseExplosions.Count == 0)
                    arrivalRt.CleanupImplantedTiles?.Invoke(arrivalRt.Context);

                if (arrivalRt.Context.OverrideRadialClearDelays != null && arrivalRt.Context.OverrideRadialClearDelays.Count > 0)
                    arrivalRt.FireOverrideOverrideSpecialVisuals?.Invoke(arrivalRt.Context.Affected, arrivalRt.Context.OverrideRadialClearDelays);

                var clearAction = BuildClearAction(arrivalRt);
                if (clearAction != null)
                    deferredActions.Add(clearAction);

                arrivalRt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                    SpecialBoardSignalType.SpecialPassFinished,
                    new Vector2Int(target.x, target.y),
                    rt.Origin));
            }

            var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
            if (sequencer != null && deferredActions.Count > 0)
            {
                sequencer.Enqueue(deferredActions);
            }
        });

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

    private void ApplyPatchBotSoloHitDeferred(PatchBotExecutionRuntime arrivalRt, int targetX, int targetY)
    {
        bool hasObstacleAtTarget = arrivalRt.PatchbotService.HasObstacleAt(targetX, targetY);
        var dataMatches = new HashSet<TileData>();
        
        arrivalRt.PatchbotService.ResolveTargetImpact(dataMatches, targetX, targetY, hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, y, arrivalRt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, tile, arrivalRt.Board));

        foreach (var data in dataMatches)
        {
            if (data != null && arrivalRt.Board.Tiles[data.X, data.Y] != null) 
                arrivalRt.Context.Affected.Add(arrivalRt.Board.Tiles[data.X, data.Y]);
        }
    }

    private void ApplyPatchBotTeleportToCellDeferred(PatchBotExecutionRuntime arrivalRt, int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= arrivalRt.Board.Width || targetY < 0 || targetY >= arrivalRt.Board.Height) return;

        bool hasObstacleAtTarget = arrivalRt.PatchbotService.HasObstacleAt(targetX, targetY);
        if (arrivalRt.Board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        var matchDatas = new HashSet<TileData>();
        arrivalRt.PatchbotService.ResolveTargetImpact(
            matchDatas,
            targetX,
            targetY,
            hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, y, arrivalRt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, tile, arrivalRt.Board));

        foreach (var data in matchDatas)
        {
            if (data != null && arrivalRt.Board.Tiles[data.X, data.Y] != null)
                arrivalRt.Context.Affected.Add(arrivalRt.Board.Tiles[data.X, data.Y]);
        }
    }

    private bool TriggerPartnerEffectAtDeferred(PatchBotExecutionRuntime arrivalRt, TileSpecial special, int originX, int originY)
    {
        if (special == TileSpecial.LineH)
        {
            arrivalRt.VisualService.PlayTransientSpecialVisualAt(special, originX, originY);

            for (int x = 0; x < arrivalRt.Board.Width; x++)
            {
                if (!SpecialUtils.CanAffectCell(arrivalRt.Board, x, originY))
                    continue;

                SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, originY, arrivalRt.Board);
                if (arrivalRt.Board.Tiles[x, originY] != null)
                {
                    arrivalRt.Context.Affected.Add(arrivalRt.Board.Tiles[x, originY]);
                    arrivalRt.Context.LightningVisualTargets.Add(arrivalRt.Board.Tiles[x, originY]);
                }
            }

            arrivalRt.Context.LightningLineStrikes.Add(new LightningLineStrike(new Vector2Int(originX, originY), true));
            return true;
        }

        if (special == TileSpecial.LineV)
        {
            arrivalRt.VisualService.PlayTransientSpecialVisualAt(special, originX, originY);

            for (int y = 0; y < arrivalRt.Board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(arrivalRt.Board, originX, y))
                    continue;

                SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, originX, y, arrivalRt.Board);
                if (arrivalRt.Board.Tiles[originX, y] != null)
                {
                    arrivalRt.Context.Affected.Add(arrivalRt.Board.Tiles[originX, y]);
                    arrivalRt.Context.LightningVisualTargets.Add(arrivalRt.Board.Tiles[originX, y]);
                }
            }

            arrivalRt.Context.LightningLineStrikes.Add(new LightningLineStrike(new Vector2Int(originX, originY), false));
            return true;
        }

        if (special == TileSpecial.PulseCore)
        {
            arrivalRt.VisualService.PlayTransientSpecialVisualAt(special, originX, originY);
            arrivalRt.Effects.PlayPulseExplosionAt(originX, originY);
            SpecialCellUtils.AddSquare(arrivalRt.Context.Affected, arrivalRt.Context, arrivalRt.Board, originX, originY, 2);
            return false;
        }

        if (special == TileSpecial.SystemOverride)
        {
            TriggerSystemOverridePatchBotConversionDeferred(arrivalRt, originX, originY);
        }

        return false;
    }

    private void TriggerSystemOverridePatchBotConversionDeferred(PatchBotExecutionRuntime arrivalRt, int originX, int originY)
    {
        var tileAtOrigin = arrivalRt.Board.Tiles[originX, originY];
        if (tileAtOrigin == null) return;

        TileType baseType = tileAtOrigin.GetOverrideBaseType(out var storedType)
            ? storedType
            : tileAtOrigin.GetTileType();

        int activationIndex = 0;

        for (int x = 0; x < arrivalRt.Board.Width; x++)
        {
            for (int y = 0; y < arrivalRt.Board.Height; y++)
            {
                if (arrivalRt.Board.Holes[x, y]) continue;

                var tile = arrivalRt.Board.Tiles[x, y];
                if (tile == null || tile == tileAtOrigin) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                SpecialCellUtils.SyncAfterSpecialChange(arrivalRt.Board, tile);

                AutoPatchBotTeleportHitAndVanishDeferred(arrivalRt, tile, activationIndex);
                activationIndex++;
            }
        }
    }

    private void AutoPatchBotTeleportHitAndVanishDeferred(
        PatchBotExecutionRuntime arrivalRt,
        TileView autoPatchBot,
        int activationIndex)
    {
        if (autoPatchBot == null) return;

        var sourceCell = new Vector2Int(autoPatchBot.X, autoPatchBot.Y);
        var sourceType = autoPatchBot.GetTileType();

        var target = arrivalRt.PatchbotService.FindTarget(autoPatchBot, null, null);
        if (!target.hasCell) return;

        const float sequentialActivationStep = 0.01f;
        float dashDelay = Mathf.Max(0, activationIndex) * sequentialActivationStep;

        arrivalRt.VisualService.FireImmediateDash(
            autoPatchBot.X,
            autoPatchBot.Y,
            target.x,
            target.y,
            dashDelay,
            onDashStart: () =>
            {
                if (autoPatchBot == null) return;

                SpecialVisualService.HideTileVisualForCombo(autoPatchBot);

                if (sourceCell.x < 0 || sourceCell.x >= arrivalRt.Board.Width || sourceCell.y < 0 || sourceCell.y >= arrivalRt.Board.Height)
                    return;

                if (arrivalRt.Board.Tiles[sourceCell.x, sourceCell.y] == autoPatchBot)
                {
                    arrivalRt.Board.ClearCell(sourceCell.x, sourceCell.y);
                    arrivalRt.Board.ClearCellVisualOnly(sourceCell, sourceType, autoPatchBot);
                }
            });

        arrivalRt.Board.StartCoroutine(CoDeferredHitVanish(arrivalRt, target.x, target.y, target.tile, dashDelay + 0.25f)); // Wait for dash to complete visually
    }

    private IEnumerator CoDeferredHitVanish(PatchBotExecutionRuntime arrivalRt, int targetX, int targetY, TileView targetTileView, float delay)
    {
        yield return new WaitForSeconds(delay);

        var matchSetData = new HashSet<TileData>();
        arrivalRt.PatchbotService.HitCellOnce(
            matchSetData,
            targetX,
            targetY,
            null,
            (x, y) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, y, arrivalRt.Board),
            (tile) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, tile, arrivalRt.Board));

        var hits = new HashSet<TileView>();
        foreach (var data in matchSetData)
        {
            if (data != null && arrivalRt.Board.Tiles[data.X, data.Y] != null)
                hits.Add(arrivalRt.Board.Tiles[data.X, data.Y]);
        }

        if (hits.Count > 0)
        {
            var clearAction = new MatchClearAction(
                hits,
                doShake: true,
                animationMode: ClearAnimationMode.Default,
                isSpecialPhase: true
            );
            
            var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
            if (sequencer != null)
            {
                sequencer.Enqueue(new List<BoardAction> { clearAction });
            }
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
