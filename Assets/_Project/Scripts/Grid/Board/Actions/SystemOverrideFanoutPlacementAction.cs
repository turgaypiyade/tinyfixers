using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SystemOverrideFanoutPlacementAction : BoardAction
{
    private const float PersistentBeamFadeOutSeconds = 0.10f;

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

        var persistentBeams = new List<LightningBeam>();

        foreach (var pos in targets)
        {
            if (pos.x < 0 || pos.x >= board.Width || pos.y < 0 || pos.y >= board.Height)
                continue;

            TileView target = board.Tiles[pos.x, pos.y];
            if (target == null)
                continue;

            bool beamReached = false;
            LightningBeam persistentBeam = null;
            Vector3 lastOriginWorld = ResolveTrackedWorldCenter(originTile, origin, Vector3.zero);
            Vector3 lastTargetWorld = ResolveTrackedWorldCenter(target, pos, Vector3.zero);

            float duration = board.PlayLightningStrikeForTiles(
                new List<TileView> { target },
                originTile: originTile,
                fallbackOriginCell: origin,
                visualTargets: new List<TileView> { target },
                allowCondense: false,
                onTargetBeamSpawned: _ =>
                {
                    beamReached = true;
                    persistentBeam ??= board.BeginPersistentLightning(
                        () =>
                        {
                            lastOriginWorld = ResolveTrackedWorldCenter(originTile, origin, lastOriginWorld);
                            return lastOriginWorld;
                        },
                        () =>
                        {
                            TileView liveTarget = board.Tiles[pos.x, pos.y] != null
                                ? board.Tiles[pos.x, pos.y]
                                : target;

                            lastTargetWorld = ResolveTrackedWorldCenter(liveTarget, pos, lastTargetWorld);
                            return lastTargetWorld;
                        },
                        PickBeamColor(persistentBeams.Count));

                    if (persistentBeam != null && !persistentBeams.Contains(persistentBeam))
                        persistentBeams.Add(persistentBeam);
                });

            float timeout = Mathf.Max(duration, board.ApplySpecialChainTempo(0.03f)) + board.ApplySpecialChainTempo(0.04f);

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

            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.05f));
        }

        yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.002f));

        if (persistentBeams.Count > 0)
        {
            for (int i = 0; i < persistentBeams.Count; i++)
            {
                if (persistentBeams[i] != null)
                    persistentBeams[i].FadeOutAndDestroy(PersistentBeamFadeOutSeconds);
            }

            yield return new WaitForSeconds(PersistentBeamFadeOutSeconds);
        }

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

                // Sadece görsel patlama — chain tetikleme OverrideSpecializedCombo
                // tarafından PulseCoreSpecial üzerinden zaten yapıldı.
                PlayPulseCoreExplosionVfx(tile);

                yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.05f));
            }
        }

        if (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0)
        {
            var airborne = new OverridePatchBotAirborneGroupAction(board, deferredPatchBotCells);
            yield return airborne.ExecuteVisuals(sequencer);
        }

        if (originTile != null)
            SpecialVisualService.HideTileVisualForCombo(originTile);
    }

    private Vector3 ResolveTrackedWorldCenter(TileView preferredTile, Vector2Int cell, Vector3 fallback)
    {
        if (preferredTile != null)
            return board.GetTileWorldCenter(preferredTile);

        if (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
        {
            var liveTile = board.Tiles[cell.x, cell.y];
            if (liveTile != null)
                return board.GetTileWorldCenter(liveTile);
        }

        if (board.Parent != null)
        {
            var local = new Vector3(
                cell.x * board.TileSize + board.TileSize * 0.5f,
                -cell.y * board.TileSize - board.TileSize * 0.5f,
                0f);
            return board.Parent.TransformPoint(local);
        }

        return fallback;
    }

    private static Color PickBeamColor(int index)
    {
        float hue = Mathf.Repeat(0.54f + index * 0.61803398875f, 1f);
        Color color = Color.HSVToRGB(hue, 0.78f, 1f);
        color.a = 0.92f;
        return color;
    }

    private void PlayPulseCoreExplosionVfx(TileView tile)
    {
        if (tile == null)
            return;

        if (board.PulseCoreImpactService != null)
        {
            board.PulseCoreImpactService.PlayPulseCoreExplosionVfxAtTile(tile, radiusCells: 2);
            return;
        }

        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(tile), radiusCells: 2, tileSize: board.TileSize);

        if (GameSettings.SoundEnabled && board.SfxSource != null)
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
