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

    // Used by generated PatchBots, e.g. PatchBot+Override fanout.
    // The source cell is hidden/cleared when the dash starts, while PatchBotSpecial
    // still owns target selection, dash enqueue, and arrival impact.
    public bool ClearOriginOnDashStart;
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

        var coordinator = rt.TargetCoordinator ?? new PatchBotTargetCoordinator(rt.Board, rt.PatchbotService);
        var picked = coordinator.PickIntent(rt.Origin, rt.Partner, null);

        if (!picked.hasIntent || picked.intent == null)
        {
            if (rt.ClearOriginOnDashStart)
                ClearPatchBotOriginVisualAndData(rt.Board, rt.Origin, new Vector2Int(rt.Origin.X, rt.Origin.Y), rt.Origin.GetTileType());
            return result;
        }

        var initialTarget = picked.intent.CurrentCell(rt.Board);
        if (!IsInside(rt.Board, initialTarget.x, initialTarget.y))
            initialTarget = picked.intent.InitialCell;

        if (!IsInside(rt.Board, initialTarget.x, initialTarget.y))
        {
            coordinator.ReleaseIntent(picked.intent);
            if (rt.ClearOriginOnDashStart)
                ClearPatchBotOriginVisualAndData(rt.Board, rt.Origin, new Vector2Int(rt.Origin.X, rt.Origin.Y), rt.Origin.GetTileType());
            return result;
        }

        var cachedPartnerSpecial = rt.Partner != null ? rt.Partner.GetSpecial() : TileSpecial.None;
        var originTile = rt.Origin;
        var originCell = new Vector2Int(originTile.X, originTile.Y);
        var originSourceType = originTile.GetTileType();

        rt.VisualService.PlayTeleportMarkers(rt.Origin, initialTarget.x, initialTarget.y);

        TileView carriedPartner =
            (rt.Partner != null && rt.Partner.GetSpecial() != TileSpecial.None)
                ? rt.Partner
                : null;

        Action dashStart = null;
        if (rt.ClearOriginOnDashStart)
        {
            dashStart = () =>
            {
                ClearPatchBotOriginVisualAndData(rt.Board, originTile, originCell, originSourceType);
            };
        }

        rt.PatchbotService.EnqueueDashFromIntent(
            rt.Origin,
            picked.intent,
            coordinator,
            rt.Partner,
            null,
            carriedPartner,
            dashStart,
            (hitX, hitY, liveIntent) =>
            {
                try
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
                        if (TriggerPartnerEffectAtDeferred(arrivalRt, cachedPartnerSpecial, hitX, hitY, deferredActions))
                            arrivalRt.Context.HasLineActivation = true;
                    }
                    else if (rt.Partner != null)
                    {
                        ApplyPatchBotTeleportToCellDeferred(arrivalRt, hitX, hitY);
                    }
                    else
                    {
                        ApplyPatchBotSoloHitDeferred(arrivalRt, hitX, hitY);
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
                            new Vector2Int(hitX, hitY),
                            rt.Origin));
                    }

                    var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
                    if (sequencer != null && deferredActions.Count > 0)
                    {
                        sequencer.Enqueue(deferredActions);
                    }
                }
                finally
                {
                    coordinator.ReleaseIntent(liveIntent ?? picked.intent);
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

    private static bool IsInside(BoardController board, int x, int y)
    {
        return board != null && x >= 0 && x < board.Width && y >= 0 && y < board.Height;
    }

    private void ClearPatchBotOriginVisualAndData(BoardController board, TileView tile, Vector2Int cell, TileType sourceType)
    {
        if (board == null || tile == null)
            return;

        SpecialVisualService.HideTileVisualForCombo(tile);

        if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
            return;

        if (board.Tiles[cell.x, cell.y] == tile)
        {
            board.ClearCell(cell.x, cell.y);
            board.ClearCellVisualOnly(cell, sourceType, tile);
        }
    }

    private void ApplyPatchBotSoloHitDeferred(PatchBotExecutionRuntime arrivalRt, int targetX, int targetY)
    {
        bool hasObstacleAtTarget = arrivalRt.PatchbotService.HasObstacleAt(targetX, targetY);
        var dataMatches = new HashSet<TileData>();

        arrivalRt.PatchbotService.ResolveTargetImpact(dataMatches, targetX, targetY, hasObstacleAtTarget,
            (x, y) =>
            {
                SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, y, arrivalRt.Board);
                // OverTileBlocker obstacles are blocked by CanAffectCell in MarkAffectedCell.
                // Add them directly to ImpactCells so the forced hit is consumed in ClearMatchesAnimated.
                if (!SpecialUtils.CanAffectCell(arrivalRt.Board, x, y) &&
                    arrivalRt.Board.ObstacleStateService?.HasObstacleAt(x, y) == true)
                    arrivalRt.Context.ImpactCells.Add(new Vector2Int(x, y));
            },
            (tile) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, tile, arrivalRt.Board));

        foreach (var data in dataMatches)
        {
            if (data != null
                && SpecialUtils.CanTargetTileContent(arrivalRt.Board, data.X, data.Y)
                && arrivalRt.Board.Tiles[data.X, data.Y] != null)
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
            (x, y) =>
            {
                SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, x, y, arrivalRt.Board);
                if (!SpecialUtils.CanAffectCell(arrivalRt.Board, x, y) &&
                    arrivalRt.Board.ObstacleStateService?.HasObstacleAt(x, y) == true)
                    arrivalRt.Context.ImpactCells.Add(new Vector2Int(x, y));
            },
            (tile) => SpecialCellUtils.MarkAffectedCell(arrivalRt.Context, tile, arrivalRt.Board));

        foreach (var data in matchDatas)
        {
            if (data != null
                && SpecialUtils.CanTargetTileContent(arrivalRt.Board, data.X, data.Y)
                && arrivalRt.Board.Tiles[data.X, data.Y] != null)
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

                if (SpecialUtils.CanTargetTileContent(arrivalRt.Board, x, originY)
                    && arrivalRt.Board.Tiles[x, originY] != null)
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

                if (SpecialUtils.CanTargetTileContent(arrivalRt.Board, originX, y)
                    && arrivalRt.Board.Tiles[originX, y] != null)
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
    /// Her bot PatchBotSpecial üzerinden çalışır; bu sınıf hedef bulma, dash enqueue
    /// ve hedefte vuruş davranışının tek sahibi olarak kalır.
    /// </summary>
    private IEnumerator CoStaggeredPatchBotLaunch(
        PatchBotExecutionRuntime arrivalRt,
        List<(TileView tile, Vector2Int sourceCell, TileType sourceType)> autoPatchBots,
        PatchBotTargetCoordinator coordinator)
    {
        const float staggerInterval = 0.04f;

        Debug.Log($"[PatchBotSpecial] Override auto PatchBot sequence count={autoPatchBots.Count}");

        for (int i = 0; i < autoPatchBots.Count; i++)
        {
            var (autoPatchBot, sourceCell, sourceType) = autoPatchBots[i];
            if (autoPatchBot == null) continue;

            if (autoPatchBot.GetSpecial() != TileSpecial.PatchBot)
                continue;

            Debug.Log($"[PatchBotSpecial] Override auto PatchBot step={i + 1}/{autoPatchBots.Count} cell={sourceCell}");

            var nestedCtx = new ResolutionContext();
            var nestedRt = new PatchBotExecutionRuntime
            {
                Board = arrivalRt.Board,
                Context = nestedCtx,
                Origin = autoPatchBot,
                Partner = null,
                PatchbotService = arrivalRt.PatchbotService,
                TargetCoordinator = coordinator,
                VisualService = arrivalRt.VisualService,
                Effects = arrivalRt.Effects,
                ActivateSpecial = arrivalRt.ActivateSpecial,
                ExecuteSpecialActions = arrivalRt.ExecuteSpecialActions,
                ProcessFanout = arrivalRt.ProcessFanout,
                CleanupImplantedTiles = arrivalRt.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = arrivalRt.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = arrivalRt.EmitBoardSignal,
                FinalizeAtEnd = false,
                ClearOriginOnDashStart = true
            };

            var nestedResult = Execute(nestedRt);
            if (nestedResult != null && nestedResult.Actions != null && nestedResult.Actions.Count > 0)
            {
                var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
                if (sequencer != null)
                    sequencer.Enqueue(nestedResult.Actions);
            }

            yield return new WaitForSeconds(arrivalRt.Board.ApplySpecialChainTempo(staggerInterval));
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

        if (!SpecialUtils.CanTargetTileContent(rt.Board, pos.x, pos.y))
            return;

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
