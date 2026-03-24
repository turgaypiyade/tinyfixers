using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SystemOverrideFanoutPlacementAction : BoardAction
{
    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly List<Vector2Int> targets;
    private readonly bool doSelectionPulse;
    private readonly List<Vector2Int> deferredPulseExplosionCells;
    private readonly List<Vector2Int> deferredPatchBotCells;

    public SystemOverrideFanoutPlacementAction(
        BoardController board,
        Vector2Int origin,
        List<Vector2Int> targets,
        bool doPulse,
        List<Vector2Int> deferredPulseExplosionCells = null,
        List<Vector2Int> deferredPatchBotCells = null)
    {
        this.board = board;
        this.origin = origin;
        this.targets = targets;
        this.doSelectionPulse = doPulse;
        this.deferredPulseExplosionCells = deferredPulseExplosionCells ?? new List<Vector2Int>();
        this.deferredPatchBotCells = deferredPatchBotCells ?? new List<Vector2Int>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (targets == null || targets.Count == 0)
            yield break;

        TileView originTile = null;
        if (origin.x >= 0 && origin.x < board.Width && origin.y >= 0 && origin.y < board.Height)
            originTile = board.Tiles[origin.x, origin.y];

        var patchbotService = (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0)
            ? new PatchbotComboService(board) : null;
        var launchedPatchBots = new List<(TileView tile, int targetX, int targetY)>();

        foreach (var pos in targets)
        {
            if (pos.x < 0 || pos.x >= board.Width || pos.y < 0 || pos.y >= board.Height)
                continue;

            TileView target = board.Tiles[pos.x, pos.y];
            if (target == null)
                continue;

            bool beamReached = false;

            float duration = board.PlayLightningStrikeForTiles(
                new List<TileView> { target },
                originTile: originTile,
                fallbackOriginCell: origin,
                visualTargets: new List<TileView> { target },
                allowCondense: false,
                onTargetBeamSpawned: _ =>
                {
                    beamReached = true;
                });

            float timeout =   Mathf.Max(duration, board.ApplySpecialChainTempo(0.03f)) +board.ApplySpecialChainTempo(0.02f);

            float elapsed = 0f;
            while (!beamReached && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        
            board.SyncTileData(target.X, target.Y);
            target.RefreshIcon();

            TileSpecial targetSpecial = target.GetSpecial();

            bool shouldPulse =
                doSelectionPulse ||
                targetSpecial == TileSpecial.PatchBot ||
                targetSpecial == TileSpecial.PulseCore;

            if (shouldPulse)
            {
                sequencer.Animator.PlaySelectionPulse(
                    target,
                    delay: 0f,
                    peakScale: 1.30f,
                    upTime: 0.10f,
                    downTime: 0.10f);
            }

            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.03f));
        }

        yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.002f));

        if (deferredPulseExplosionCells != null && deferredPulseExplosionCells.Count > 0)
        {
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.02f));

            for (int i = 0; i < deferredPulseExplosionCells.Count; i++)
            {
                var cell = deferredPulseExplosionCells[i];

                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                    continue;

                var tile = board.Tiles[cell.x, cell.y];
                if (tile == null)
                    continue;

                if (tile.GetSpecial() != TileSpecial.PulseCore)
                    continue;

                var futurePulseCells = new HashSet<Vector2Int>();
                for (int j = i + 1; j < deferredPulseExplosionCells.Count; j++)
                    futurePulseCells.Add(deferredPulseExplosionCells[j]);

                var pulseMatches = BuildPulseClearSet(cell, futurePulseCells);
                if (pulseMatches.Count == 0)
                    continue;

                PlayPulseCoreExplosionVfx(tile);

                // MatchClearAction sadece görsel oynatır; data temizliğini burada yapmalıyız
                foreach (var clearTile in pulseMatches)
                {
                    if (clearTile == null)
                        continue;

                    var clearCell = new Vector2Int(clearTile.X, clearTile.Y);
                    var clearType = clearTile.GetTileType();

                    board.ClearCellDataOnly(clearCell);
                    board.ClearCellVisualOnly(clearCell, clearType, clearTile);
                }

                var pulseClear = new MatchClearAction(
                    pulseMatches,
                    doShake: true,
                    animationMode: ClearAnimationMode.Default,
                    affectedCells: null,
                    obstacleHitContext: null,
                    includeAdjacentOverTileBlockerDamage: true,
                    lightningOriginTile: null,
                    lightningOriginCell: null,
                    lightningVisualTargets: null,
                    lightningLineStrikes: null,
                    suppressPerTileClearVfx: false,
                    perTileClearDelays: null,
                    staggerDelays: null,
                    staggerAnimTime: 0.16f,
                    isSpecialPhase: true
                );

                yield return pulseClear.ExecuteVisuals(sequencer);
                yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.03f));
            }
        }

        // Deferred PatchBot dashes — launch ALL in parallel for snappy feel
        if (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0 && patchbotService != null)
        {
            var usedTargets = new HashSet<TileView>();
            var allRequests = new List<BoardController.PatchbotDashRequest>();

            // Phase 1: Prepare all dash requests up-front
            for (int i = 0; i < deferredPatchBotCells.Count; i++)
            {
                var cell = deferredPatchBotCells[i];

                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                    continue;

                var tile = board.Tiles[cell.x, cell.y];
                if (tile == null)
                    continue;

                if (tile.GetSpecial() != TileSpecial.PatchBot)
                    continue;

                var pbTarget = patchbotService.FindTarget(tile, null, usedTargets);
                if (!pbTarget.hasCell)
                    continue;
                    
                if (pbTarget.tile != null)
                    usedTargets.Add(pbTarget.tile);

                var fromCell = new Vector2Int(tile.X, tile.Y);
                var toCell = new Vector2Int(pbTarget.x, pbTarget.y);
                var sourceType = tile.GetTileType();

                // Capture for closure
                var capturedTile = tile;
                var capturedFrom = fromCell;
                var capturedTo = toCell;
                var capturedSourceType = sourceType;
                var capturedTarget = pbTarget;

                board.ActiveBackgroundJobs++;

                allRequests.Add(new BoardController.PatchbotDashRequest
                {
                    from = capturedFrom,
                    to = capturedTo,
                    onStart = () =>
                    {
                        if (capturedTile == null)
                            return;

                        SpecialVisualService.HideTileVisualForCombo(capturedTile);

                        if (capturedFrom.x < 0 || capturedFrom.x >= board.Width || capturedFrom.y < 0 || capturedFrom.y >= board.Height)
                            return;

                        if (board.Tiles[capturedFrom.x, capturedFrom.y] == capturedTile)
                        {
                            board.ClearCell(capturedFrom.x, capturedFrom.y);
                            board.ClearCellVisualOnly(capturedFrom, capturedSourceType, capturedTile);
                        }
                    },
                    onArrived = () =>
                    {
                        var arrivalCtx = new ResolutionContext();
                        var dataMatches = new HashSet<TileData>();

                        bool hasObstacle = patchbotService.HasObstacleAt(capturedTarget.x, capturedTarget.y);
                        
                        patchbotService.ResolveTargetImpact(
                            dataMatches,
                            capturedTarget.x,
                            capturedTarget.y,
                            hasObstacle,
                            (x, y) => SpecialCellUtils.MarkAffectedCell(arrivalCtx, x, y, board),
                            t => SpecialCellUtils.MarkAffectedCell(arrivalCtx, t, board)
                        );

                        foreach (var data in dataMatches)
                        {
                            if (data == null) continue;
                            if (data.X < 0 || data.X >= board.Width || data.Y < 0 || data.Y >= board.Height) continue;
                            
                            var t = board.Tiles[data.X, data.Y];
                            if (t != null) arrivalCtx.Affected.Add(t);
                        }

                        var clearAction = new MatchClearAction(
                            arrivalCtx.Affected,
                            doShake: true,
                            animationMode: ClearAnimationMode.Default,
                            affectedCells: arrivalCtx.AffectedCells,
                            impactCells: arrivalCtx.ImpactCells,
                            isSpecialPhase: true
                        );
                        sequencer.Enqueue(new List<BoardAction> { clearAction });

                        board.ActiveBackgroundJobs--;
                    }
                });
            }

            // Phase 2: Fire all dashes in one parallel batch
            if (allRequests.Count > 0)
            {
                if (board.PatchbotDashUI != null)
                    board.PatchbotDashUI.PlayDashParallel(allRequests, board);
                else
                {
                    // Fallback: fire arrivals directly
                    foreach (var r in allRequests)
                    {
                        r.onStart?.Invoke();
                        r.onArrived?.Invoke();
                    }
                }
            }
        }

        if (originTile != null)
            SpecialVisualService.HideTileVisualForCombo(originTile);
    }
    private void PlayPulseCoreExplosionVfx(TileView tile)
    {
        if (tile == null)
            return;

        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(tile), radiusCells: 1, tileSize: board.TileSize);

        if (board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);
            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        if (board.EnablePulseMicroShake && board.PulseMicroShakeDuration > 0f && board.PulseMicroShakeStrength > 0f)
            board.StartCoroutine(board.boardAnimatorRef.MicroShake(board.PulseMicroShakeDuration, board.PulseMicroShakeStrength));

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(tile.X, tile.Y));
    }

    private Vector2 GetTileAnchoredPos(TileView tile)
    {
        var tileRect = tile.GetComponent<RectTransform>();
        if (tileRect == null)
            return Vector2.zero;

        var vfxRoot = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;
        if (vfxRoot != null)
        {
            var worldPos = tileRect.TransformPoint(tileRect.rect.center);
            var localPos = vfxRoot.InverseTransformPoint(worldPos);
            return (Vector2)localPos;
        }

        var tilesRoot = board.Parent;
        var rootOffset = tilesRoot != null ? tilesRoot.anchoredPosition : Vector2.zero;
        return rootOffset + tileRect.anchoredPosition;
    }

    private HashSet<TileView> BuildPulseClearSet(
        Vector2Int centerCell,
        HashSet<Vector2Int> futurePulseCells)
    {
        var result = new HashSet<TileView>();

        // PulseCore etki alanı artık PulseCoreSpecial tarafından yürütülüyor.
        // Bu yüzden zincirdeki pulse temizliği burada doğrudan 3x3 olarak hesaplanıyor.
        const int half = 1;

        for (int x = centerCell.x - half; x <= centerCell.x + half; x++)
        {
            for (int y = centerCell.y - half; y <= centerCell.y + half; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                    continue;

                if (!SpecialUtils.CanAffectCell(board, x, y))
                    continue;

                var cell = new Vector2Int(x, y);

                // Sonraki pulse'ları erken yok etme
                if (futurePulseCells.Contains(cell))
                    continue;

                var tile = board.Tiles[x, y];
                if (tile == null)
                    continue;

                result.Add(tile);
            }
        }

        return result;
    }
}