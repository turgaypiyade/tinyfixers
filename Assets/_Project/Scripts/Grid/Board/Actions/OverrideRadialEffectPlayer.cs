using System.Collections;
using UnityEngine;

public sealed class OverrideRadialEffectPlayer : IClearEffectPlayer
{
    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is OverrideRadialEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        OverrideRadialEffectDescriptor radial = effect as OverrideRadialEffectDescriptor;
        if (radial == null || radial.TargetTiles == null || radial.TargetTiles.Count == 0)
            yield break;

        PlayOverrideCenterVfx(board, radial);

        float maxDelay = 0f;

        for (int i = 0; i < radial.TargetTiles.Count; i++)
        {
            TileView tile = radial.TargetTiles[i];
            if (tile == null)
                continue;

            float delay = 0f;
            if (radial.DelayMap != null && radial.DelayMap.TryGetValue(tile, out delay))
            {
                if (delay > maxDelay)
                    maxDelay = delay;
            }

            board.StartCoroutine(PlayTileImpact(tile, delay, context));
        }

        yield return new WaitForSeconds(maxDelay + radial.Timing.TailHoldSeconds);
    }

    private IEnumerator PlayTileImpact(
        TileView tile,
        float delay,
        ClearEffectPlaybackContext context)
    {
        if (tile == null)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (context != null && context.NotifyCellImpactNow != null)
            context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
    }

    private void PlayOverrideCenterVfx(BoardController board, OverrideRadialEffectDescriptor radial)
    {
        if (radial.OriginCell.HasValue)
            ComboBehaviorEvents.EmitComboTriggered(TileSpecial.SystemOverride, TileSpecial.None, radial.OriginCell.Value);

        if (radial.OriginTile == null)
            return;

        if (board.BoardVfxPlayer != null)
        {
            Vector2 pos = GetTileAnchoredPos(board, radial.OriginTile);
            board.BoardVfxPlayer.PlayPulseVfx(pos, 2, board.TileSize);
        }

        if (board.SfxSource != null && board.SfxPulseCoreBoom != null)
            board.SfxSource.PlayOneShot(board.SfxPulseCoreBoom);
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