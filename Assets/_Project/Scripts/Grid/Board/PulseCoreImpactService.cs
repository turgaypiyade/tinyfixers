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
            if (t == null) continue;
            if (t.GetSpecial() == TileSpecial.PulseCore)
                pulseCenters.Add(t);
        }

        for (int i = 0; i < pulseCenters.Count; i++)
        {
            var centerLocalPos = GetTileAnchoredPos(pulseCenters[i]);
            PlayPulseCoreVfxAndSfx(centerLocalPos);
        }

        if (pulseCenters.Count > 0)
        {
            stagger = new Dictionary<TileView, float>(affected.Count);
            foreach (var tile in affected)
            {
                if (tile == null) continue;

                // En yakın PulseCore merkezine göre delay
                int best = int.MaxValue;
                for (int i = 0; i < pulseCenters.Count; i++)
                {
                    var c = pulseCenters[i];
                    int dist = Mathf.Abs(tile.X - c.X) + Mathf.Abs(tile.Y - c.Y);
                    if (dist < best) best = dist;
                }

                stagger[tile] = best * board.PulseImpactDelayStep;
            }
        }

        return stagger;
    }

    void PlayPulseCoreVfxAndSfx(Vector2 centerLocalPos)
    {
        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(centerLocalPos, radiusCells: 2, tileSize: board.TileSize);

        if (board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);
            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        if (board.EnablePulseMicroShake && board.PulseMicroShakeDuration > 0f && board.PulseMicroShakeStrength > 0f)
            board.StartCoroutine(boardAnimator.MicroShake(board.PulseMicroShakeDuration, board.PulseMicroShakeStrength));
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
