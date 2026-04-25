using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PulseCoreImpactService
{
    private readonly BoardController board;
    private readonly BoardAnimator boardAnimator;

    public PulseCoreImpactService(BoardController board, BoardAnimator boardAnimator)
    {
        this.board = board;
        this.boardAnimator = boardAnimator;
    }

    public Dictionary<TileView, float> BuildStaggerDelays(HashSet<TileView> affected, HashSet<TileView> processed)
    {
        Dictionary<TileView, float> stagger = null;
        var pulseCenters = new List<TileView>();

        foreach (var t in processed)
        {
            if (t == null)
                continue;

            if (t.GetSpecial() == TileSpecial.PulseCore)
                pulseCenters.Add(t);
        }

        // Burada VFX oynatma yok.
        // Bu method sadece delay hesaplamalı.
        // Pulse VFX zaten PulseCoreSpecial.PlayPulseActivationVisual içinde doğru radius ile oynatılıyor.

        if (pulseCenters.Count > 0)
        {
            stagger = new Dictionary<TileView, float>(affected.Count);

            foreach (var tile in affected)
            {
                if (tile == null)
                    continue;

                int best = int.MaxValue;

                for (int i = 0; i < pulseCenters.Count; i++)
                {
                    var c = pulseCenters[i];

                    int dist = Mathf.Abs(tile.X - c.X) + Mathf.Abs(tile.Y - c.Y);
                    if (dist < best)
                        best = dist;
                }

                stagger[tile] = best * board.PulseImpactDelayStep;
            }
        }

        return stagger;
    }


    public void PlayPulseCoreExplosionVfxAtCell(int x, int y, int radiusCells = 2)
    {
        Vector3 worldCenter = board.GetCellWorldCenterPosition(x, y);

        if (board.BoardVfxPlayer != null)
        {
            var vfxRoot = board.BoardVfxPlayer.VfxRoot;
            Vector2 anchored = vfxRoot != null
                ? board.WorldToAnchoredIn(vfxRoot, worldCenter)
                : Vector2.zero;

            board.BoardVfxPlayer.PlayPulseVfx(anchored, radiusCells: radiusCells, tileSize: board.TileSize);
        }

        if (board.Audio != null)
        {
            board.Audio.Emit(BoardSfxRequest.SpecialActivate(TileSpecial.PulseCore, intensity: radiusCells));
        }
        else if (board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);

            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        if (board.EnablePulseMicroShake && board.PulseMicroShakeDuration > 0f && board.PulseMicroShakeStrength > 0f)
            board.StartCoroutine(boardAnimator.MicroShake(board.PulseMicroShakeDuration, board.PulseMicroShakeStrength));

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(x, y));
    }

    public IEnumerator PlayPreExplosionPulse(TileView tile, float peakScale = 1.42f, float upTime = 0.06f, float downTime = 0.06f, float postDelay = 0.05f)
    {
        if (tile == null)
            yield break;

        boardAnimator.PlaySelectionPulse(tile, delay: 0f, peakScale: peakScale, upTime: upTime, downTime: downTime);

        if (postDelay > 0f)
            yield return new WaitForSeconds(board.ApplySpecialChainTempo(postDelay));
    }

    public void PlayPulseCoreExplosionVfxAtTile(TileView tile, int radiusCells = 2)
    {
        if (tile == null)
            return;

        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(tile), radiusCells: radiusCells, tileSize: board.TileSize);

        if (board.Audio != null)
        {
            board.Audio.Emit(BoardSfxRequest.SpecialActivate(TileSpecial.PulseCore, intensity: radiusCells));
        }
        else if (board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);

            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        if (board.EnablePulseMicroShake && board.PulseMicroShakeDuration > 0f && board.PulseMicroShakeStrength > 0f)
            board.StartCoroutine(boardAnimator.MicroShake(board.PulseMicroShakeDuration, board.PulseMicroShakeStrength));

        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(tile.X, tile.Y));
    }

    Vector2 GetTileAnchoredPos(TileView tile)
    {
        if (tile == null) return Vector2.zero;
        var tileRect = tile.GetComponent<RectTransform>();
        if (tileRect == null) return Vector2.zero;

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
}
