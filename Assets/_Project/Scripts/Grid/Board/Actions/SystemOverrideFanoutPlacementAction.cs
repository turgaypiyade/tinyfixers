using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SystemOverrideFanoutPlacementAction : BoardAction
{
    private readonly BoardController board;
    private readonly Vector2Int origin;
    private readonly List<Vector2Int> targets;
    private readonly bool doSelectionPulse;

    public SystemOverrideFanoutPlacementAction(
        BoardController board,
        Vector2Int origin,
        List<Vector2Int> targets,
        bool doPulse)
    {
        this.board = board;
        this.origin = origin;
        this.targets = targets;
        this.doSelectionPulse = doPulse;
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
                onTargetBeamSpawned: _ => beamReached = true);

            float timeout = Mathf.Max(duration, 0.08f) + 0.02f;
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

            yield return new WaitForSeconds(0.04f);
        }

        yield return new WaitForSeconds(0.15f);
    }
}