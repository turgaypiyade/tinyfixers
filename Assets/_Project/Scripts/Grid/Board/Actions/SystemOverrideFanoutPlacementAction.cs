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

            float timeout = Mathf.Max(duration, board.ApplySpecialChainTempo(0.03f)) + board.ApplySpecialChainTempo(0.02f);

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
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.01f));

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

                yield return new WaitForSeconds(board.ApplySpecialChainTempo(0.015f));
            }
        }

        // Override+PatchBot fanout:
        // Override only performs placement. Generated PatchBots are activated through
        // PatchBotSpecial so target selection, dash, and impact remain owned by PatchBotSpecial.
        if (deferredPatchBotCells != null && deferredPatchBotCells.Count > 0 && patchbotService != null)
        {
            yield return LaunchDeferredPatchBotsViaSpecial(patchbotService);
        }

        if (originTile != null)
            SpecialVisualService.HideTileVisualForCombo(originTile);
    }

    private IEnumerator LaunchDeferredPatchBotsViaSpecial(PatchbotComboService patchbotService)
    {
        var patchBotEntries = new List<TileView>();

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

            patchBotEntries.Add(tile);
        }

        if (patchBotEntries.Count == 0)
            yield break;

        var coordinator = new PatchBotTargetCoordinator(board, patchbotService);
        var visualService = new SpecialVisualService(board, board.boardAnimatorRef, patchbotService);
        var patchBotSpecial = new PatchBotSpecial();

        const float staggerInterval = 0.04f;
        Debug.Log($"[SystemOverrideFanoutPlacementAction] PatchBot sequence count={patchBotEntries.Count}");

        for (int i = 0; i < patchBotEntries.Count; i++)
        {
            var patchBot = patchBotEntries[i];
            if (patchBot == null)
                continue;

            if (patchBot.GetSpecial() != TileSpecial.PatchBot)
                continue;

            var cell = new Vector2Int(patchBot.X, patchBot.Y);
            Debug.Log($"[SystemOverrideFanoutPlacementAction] PatchBot sequence step={i + 1}/{patchBotEntries.Count} cell={cell}");

            var ctx = new ResolutionContext();
            patchBotSpecial.Execute(new PatchBotExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = patchBot,
                Partner = null,
                PatchbotService = patchbotService,
                TargetCoordinator = coordinator,
                VisualService = visualService,
                Effects = null,
                FinalizeAtEnd = false,
                ClearOriginOnDashStart = true
            });

            yield return new WaitForSeconds(board.ApplySpecialChainTempo(staggerInterval));
        }
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

        // Güvenli fallback
        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(tile), radiusCells: 2, tileSize: board.TileSize);

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