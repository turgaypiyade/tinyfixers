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
    public PatchBotTargetCoordinator TargetCoordinator;
    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;
    public Func<ResolutionContext, TileView, TileView, List<BoardAction>> ExecuteSpecialActions;
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

        TileView carriedPartner =
            (rt.Partner != null && rt.Partner.GetSpecial() != TileSpecial.None)
                ? rt.Partner
                : null;

        rt.PatchbotService.EnqueueDash(rt.Origin, target.x, target.y, carriedPartner, null, () =>
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
                        ExecuteSpecialActions = rt.ExecuteSpecialActions,
                        ProcessFanout = rt.ProcessFanout,
                        CleanupImplantedTiles = rt.CleanupImplantedTiles,
                        FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                        EmitBoardSignal = rt.EmitBoardSignal,
                        FinalizeAtEnd = true
                    };

                    var deferredActions = new List<BoardAction>();

                    if (cachedPartnerSpecial != TileSpecial.None)
                    {
                        if (TriggerPartnerEffectAtDeferred(arrivalRt, cachedPartnerSpecial, target.x, target.y, deferredActions))
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

    private bool TriggerPartnerEffectAtDeferred(
      PatchBotExecutionRuntime arrivalRt,
      TileSpecial special,
      int originX,
      int originY,
      List<BoardAction> deferredActions)
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

            arrivalRt.Context.LightningLineStrikes.Add(
                new LightningLineStrike(new Vector2Int(originX, originY), true));

            ExecuteChainFromAffected(arrivalRt, deferredActions);
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

            arrivalRt.Context.LightningLineStrikes.Add(
                new LightningLineStrike(new Vector2Int(originX, originY), false));

            ExecuteChainFromAffected(arrivalRt, deferredActions);
            return true;
        }

        if (special == TileSpecial.PulseCore)
        {
            arrivalRt.VisualService.PlayTransientSpecialVisualAt(special, originX, originY);
            arrivalRt.Effects.PlayPulseExplosionAt(originX, originY);
            SpecialCellUtils.AddSquare(arrivalRt.Context.Affected, arrivalRt.Context, arrivalRt.Board, originX, originY, 2);

            ExecuteChainFromAffected(arrivalRt, deferredActions);
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

        var autoPatchBots = new List<(TileView tile, Vector2Int sourceCell, TileType sourceType)>();

        for (int x = 0; x < arrivalRt.Board.Width; x++)
        {
            for (int y = 0; y < arrivalRt.Board.Height; y++)
            {
                if (arrivalRt.Board.Holes[x, y])
                    continue;

                // KRITIK FIX:
                if (arrivalRt.Board.ObstacleStateService != null &&
                    arrivalRt.Board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    continue;

                var tile = arrivalRt.Board.Tiles[x, y];
                if (tile == null || tile == tileAtOrigin)
                    continue;

                if (!tile.GetTileType().Equals(baseType))
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                    continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                SpecialCellUtils.SyncAfterSpecialChange(arrivalRt.Board, tile);

                autoPatchBots.Add((tile, new Vector2Int(x, y), tile.GetTileType()));
            }
        }

        if (autoPatchBots.Count == 0)
            return;

        var coordinator = new PatchBotTargetCoordinator(arrivalRt.Board, arrivalRt.PatchbotService);
        arrivalRt.Board.StartCoroutine(CoStaggeredPatchBotLaunch(arrivalRt, autoPatchBots, coordinator));
    }
    /// <summary>
    /// Tüm auto-PatchBot'ları sıralı olarak havalandırır.
    /// Her biri önce hücresinden kalkar, sonra koordinatörden hedef alır,
    /// dash animasyonu başlatılır. Aralarında kısa süre bırakılarak
    /// önceki botların vurduğu hedefler güncellenmiş olur.
    /// </summary>
    private IEnumerator CoStaggeredPatchBotLaunch(
        PatchBotExecutionRuntime arrivalRt,
        List<(TileView tile, Vector2Int sourceCell, TileType sourceType)> autoPatchBots,
        PatchBotTargetCoordinator coordinator)
    {
        const float staggerInterval = 0.04f;   // Botlar arası bekleme
        const float dashTravelTime = 0.25f;     // Dash animasyon süresi tahmini

        for (int i = 0; i < autoPatchBots.Count; i++)
        {
            var (autoPatchBot, sourceCell, sourceType) = autoPatchBots[i];
            if (autoPatchBot == null) continue;

            // ── Koordinatörden hedef al ──
            var target = coordinator.ReserveTarget(autoPatchBot, null, null);

            if (!target.hasCell)
            {
                // Hedef bulamayan PatchBot'u temizle — ekranda kalmasın
                autoPatchBot.SetSpecial(TileSpecial.None);
                SpecialCellUtils.SyncAfterSpecialChange(arrivalRt.Board, autoPatchBot);
                arrivalRt.Board.ClearCell(sourceCell.x, sourceCell.y);
                arrivalRt.Board.ClearCellVisualOnly(sourceCell, sourceType, autoPatchBot);
                continue;
            }

            // Closure için yakala
            var capturedBot = autoPatchBot;
            var capturedSource = sourceCell;
            var capturedSourceType = sourceType;
            var capturedTarget = target;

            // ── Dash animasyonunu başlat ──
            arrivalRt.VisualService.FireImmediateDash(
                capturedBot.X,
                capturedBot.Y,
                capturedTarget.x,
                capturedTarget.y,
                delay: 0f,
                onDashStart: () =>
                {
                    if (capturedBot == null) return;

                    SpecialVisualService.HideTileVisualForCombo(capturedBot);

                    if (capturedSource.x < 0 || capturedSource.x >= arrivalRt.Board.Width ||
                        capturedSource.y < 0 || capturedSource.y >= arrivalRt.Board.Height)
                        return;

                    if (arrivalRt.Board.Tiles[capturedSource.x, capturedSource.y] == capturedBot)
                    {
                        arrivalRt.Board.ClearCell(capturedSource.x, capturedSource.y);
                        arrivalRt.Board.ClearCellVisualOnly(capturedSource, capturedSourceType, capturedBot);
                    }
                });

            // ── Varış sonrası vuruş ve temizlik — koordinatör bilgilendirilir ──
            arrivalRt.Board.StartCoroutine(CoDeferredHitVanish(
                arrivalRt, capturedTarget.x, capturedTarget.y, capturedTarget.tile,
                dashTravelTime, coordinator));

            // ── Sonraki bot için kısa bekle — bu sürede önceki botun vurduğu
            //    hedef kayıtlardan düşer, obstacle kalan vuruşu güncellenir ──
            yield return new WaitForSeconds(staggerInterval);
        }
    }

    private IEnumerator CoDeferredHitVanish(
        PatchBotExecutionRuntime arrivalRt,
        int targetX, int targetY,
        TileView targetTileView,
        float delay,
        PatchBotTargetCoordinator coordinator = null)
    {
        yield return new WaitForSeconds(delay);

        // ── Koordinatör rezervasyonunu serbest bırak ──
        coordinator?.ReleaseReservation(targetX, targetY);

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
                isSpecialPhase: true,
                enqueueCascadeOnComplete: true
            );

            var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
            if (sequencer != null)
            {
                sequencer.Enqueue(new List<BoardAction> { clearAction });
            }
        }
    }
    private void ExecuteChainFromAffected(PatchBotExecutionRuntime rt, List<BoardAction> deferredActions)
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

            if (tile.GetSpecial() == TileSpecial.None)
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

    private void TryQueue(PatchBotExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
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
            presentationPlan: null,
            enqueueCascadeOnComplete: true
        );
    }
}