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

    public SystemOverrideFanoutPlacementAction(BoardController board, Vector2Int origin, List<Vector2Int> targets, bool doPulse, List<Vector2Int> deferredPulseExplosionCells = null)
    {
        this.board = board;
        this.origin = origin;
        this.targets = targets;
        this.doSelectionPulse = doPulse;
        this.deferredPulseExplosionCells = deferredPulseExplosionCells ?? new List<Vector2Int>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (targets == null || targets.Count == 0)
            yield break;

        TileView originTile = null;
        if (origin.x >= 0 && origin.x < board.Width && origin.y >= 0 && origin.y < board.Height)
            originTile = board.Tiles[origin.x, origin.y];

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

            float timeout = Mathf.Max(duration, board.ApplySpecialChainTempo(0.08f)) + board.ApplySpecialChainTempo(0.02f);
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

                board.PlayPulsePulseExplosionVfxAtCell(cell.x, cell.y);

                var pulseClear = new MatchClearAction(
                    pulseMatches,
                    doShake: true,
                    animationMode: ClearAnimationMode.Default,
                    affectedCells: null,
                    obstacleHitContext: null,
                    includeAdjacentOverTileBlockerDamage: false,
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

        if (originTile != null)
        {
            SpecialVisualService.HideTileVisualForCombo(originTile);
        }
    }

    private HashSet<TileView> BuildPulseClearSet(Vector2Int centerCell, HashSet<Vector2Int> futurePulseCells)
    {
        var result = new HashSet<TileView>();
        var cells = board.SpecialBehaviors.CalculateEffect(TileSpecial.PulseCore, board, centerCell.x, centerCell.y);

        foreach (var cell in cells)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            // Gelecekte tetiklenecek pulsecore'ları erken temizleme
            if (futurePulseCells.Contains(cell))
                continue;

            result.Add(tile);
        }

        return result;
    }
}