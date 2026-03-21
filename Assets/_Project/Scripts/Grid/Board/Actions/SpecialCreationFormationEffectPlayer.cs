using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialCreationFormationEffectPlayer : IClearEffectPlayer
{
    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is SpecialCreationFormationEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        SpecialCreationFormationEffectDescriptor creation =
            effect as SpecialCreationFormationEffectDescriptor;

        if (creation == null || creation.CreatedTile == null)
            yield break;

        BoardAnimator animator = board.boardAnimatorRef;
        if (animator == null)
            yield break;

        var contributors = creation.TargetTiles != null
            ? new List<TileView>(creation.TargetTiles)
            : new List<TileView>();

        contributors.RemoveAll(tile => tile == null || tile == creation.CreatedTile);
        if (contributors.Count == 0)
            yield break;

        yield return animator.PlayCreatedSpecialFormation(
            creation.CreatedTile,
            contributors,
            creation.Duration);

        if (context != null && context.NotifyCellImpactNow != null)
        {
            for (int i = 0; i < contributors.Count; i++)
            {
                TileView tile = contributors[i];
                if (tile == null)
                    continue;

                context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
            }
        }

        yield return new WaitForSeconds(creation.Timing.TailHoldSeconds);
    }
}
