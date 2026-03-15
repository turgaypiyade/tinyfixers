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

        // PatchBot dash tracking
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
                visualTargets: new List<TileView> { target },
                allowCondense: false,
                onTargetBeamSpawned: _ =>
                {
                    beamReached = true;
                });

            float timeout =
                Mathf.Max(duration, board.ApplySpecialChainTempo(0.08f)) +
                board.ApplySpecialChainTempo(0.02f);

            float elapsed = 0f;
            while (!beamReached && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Beam hedefe vardığında data/view senkronunu zorla
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

            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.04f));
        }

        yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.12f));

        if (deferredPulseExplosionCells != null && deferredPulseExplosionCells.Count > 0)
        {
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.05f));

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

        // ── Deferred PatchBot dashes — beam loop'unda yerleştirildi, şimdi sırayla fırlat ──
        if (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0 && patchbotService != null)
        {
            float maxDashDur = 0f;

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

                var pbTarget = patchbotService.FindTarget(tile, null, null);
                if (!pbTarget.hasCell)
                    continue;

                var fromCell = new Vector2Int(tile.X, tile.Y);
                var toCell = new Vector2Int(pbTarget.x, pbTarget.y);

                patchbotService.EnqueueDash(tile, pbTarget.x, pbTarget.y);
                SpecialVisualService.HideTileVisualForCombo(tile);

                float dd = board.PatchbotDashUI != null
                    ? board.PatchbotDashUI.EstimateDashDuration(board, fromCell, toCell)
                    : 0.22f;
                if (dd > maxDashDur) maxDashDur = dd;

                launchedPatchBots.Add((tile, pbTarget.x, pbTarget.y));

                if (i < deferredPatchBotCells.Count - 1)
                    yield return new WaitForSeconds(0.003f);
            }

            // En uzun dash'in bitmesini bekle
            if (maxDashDur > 0f)
                yield return new WaitForSeconds(maxDashDur);

            // Tüm hedefleri tek seferde temizle
            var allClearTiles = new HashSet<TileView>();

            foreach (var (tile, targetX, targetY) in launchedPatchBots)
            {
                bool hasObstacle = patchbotService.HasObstacleAt(targetX, targetY);
                if (hasObstacle)
                {
                    board.MarkPatchBotForcedObstacleHit(targetX, targetY);
                }
                else
                {
                    var targetTile = board.Tiles[targetX, targetY];
                    if (targetTile != null)
                        allClearTiles.Add(targetTile);
                }

                allClearTiles.Add(tile);
            }

            if (allClearTiles.Count > 0)
            {
                var patchClear = new MatchClearAction(
                    allClearTiles,
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

                yield return patchClear.ExecuteVisuals(sequencer);
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
        var cells = board.SpecialBehaviors.CalculateEffect(
            TileSpecial.PulseCore,
            board,
            centerCell.x,
            centerCell.y);

        foreach (var cell in cells)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            // Sonraki pulse'ları erken yok etme
            if (futurePulseCells.Contains(cell))
                continue;

            result.Add(tile);
        }

        return result;
    }
}