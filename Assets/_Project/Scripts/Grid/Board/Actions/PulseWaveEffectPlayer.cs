using System.Collections;
using UnityEngine;

public sealed class PulseWaveEffectPlayer : IClearEffectPlayer
{
    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is PulseWaveEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        PulseWaveEffectDescriptor pulse = effect as PulseWaveEffectDescriptor;
        if (pulse == null || pulse.TargetTiles == null || pulse.TargetTiles.Count == 0)
            yield break;

        BoardAnimator animator = board.boardAnimatorRef;
        if (animator == null)
            yield break;

        PlayPulseCenterVfx(board, pulse.CenterCell);

        float maxDelay = 0f;

        for (int i = 0; i < pulse.TargetTiles.Count; i++)
        {
            TileView tile = pulse.TargetTiles[i];
            if (tile == null)
                continue;

            float delay = 0f;
            if (pulse.DelayMap != null && pulse.DelayMap.TryGetValue(tile, out delay))
            {
                if (delay > maxDelay)
                    maxDelay = delay;
            }

            board.StartCoroutine(PlayTileImpact(tile, delay, pulse.ImpactAnimTime, context, animator));
        }

        yield return new WaitForSeconds(maxDelay + pulse.ImpactAnimTime + pulse.Timing.TailHoldSeconds);
    }

    private IEnumerator PlayTileImpact(
        TileView tile,
        float delay,
        float impactAnimTime,
        ClearEffectPlaybackContext context,
        BoardAnimator animator)
    {
        if (tile == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        yield return animator.PlayPulseImpactSingle(tile, impactAnimTime);

        if (context != null && context.NotifyCellImpactNow != null)
            context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
    }

    private void PlayPulseCenterVfx(BoardController board, Vector2Int centerCell)
    {
        if (centerCell.x < 0 || centerCell.x >= board.Width || centerCell.y < 0 || centerCell.y >= board.Height)
            return;

        TileView tile = board.Tiles[centerCell.x, centerCell.y];
        if (tile == null)
            return;

        if (board.BoardVfxPlayer != null)
            board.BoardVfxPlayer.PlayPulseVfx(GetTileAnchoredPos(board, tile), 1, board.TileSize);

        if (board.SfxSource != null)
        {
            if (board.SfxPulseCoreBoom != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);

            if (board.SfxPulseCoreWave != null)
                board.SfxSource.PlayOneShot(board.SfxPulseCoreWave);
        }

        PulseBehaviorEvents.EmitPulseExplosionPlayed(centerCell);
    }

    private Vector2 GetTileAnchoredPos(BoardController board, TileView tile)
    {
        if (tile == null)
            return Vector2.zero;

        RectTransform tileRect = null;

        try
        {
            tileRect = tile.GetComponent<RectTransform>();
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }

        if (tileRect == null)
            return Vector2.zero;

        if (tileRect.gameObject == null)
            return Vector2.zero;

        RectTransform vfxRoot = board.BoardVfxPlayer != null ? board.BoardVfxPlayer.VfxRoot : null;
        if (vfxRoot != null)
        {
            Vector3 worldPos;
            try
            {
                worldPos = tileRect.TransformPoint(tileRect.rect.center);
            }
            catch (MissingReferenceException)
            {
                return Vector2.zero;
            }

            Vector3 localPos;
            try
            {
                localPos = vfxRoot.InverseTransformPoint(worldPos);
            }
            catch (MissingReferenceException)
            {
                return Vector2.zero;
            }

            return (Vector2)localPos;
        }

        RectTransform tilesRoot = board.Parent;
        Vector2 rootOffset = tilesRoot != null ? tilesRoot.anchoredPosition : Vector2.zero;

        try
        {
            return rootOffset + tileRect.anchoredPosition;
        }
        catch (MissingReferenceException)
        {
            return Vector2.zero;
        }
    }
}