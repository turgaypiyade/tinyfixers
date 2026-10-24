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
        var creation = effect as SpecialCreationFormationEffectDescriptor;
        if (creation == null || context == null)
            yield break;

        BoardAnimator animator = board.boardAnimatorRef;
        if (animator == null)
            yield break;

        var contributors = creation.TargetTiles != null
            ? new List<TileView>(creation.TargetTiles)
            : new List<TileView>();

        for (int i = contributors.Count - 1; i >= 0; i--)
            if (!context.IsCurrentTile(contributors[i])) contributors.RemoveAt(i);

        // Eski davranış: created special formation
        if (creation.CreatedTile != null)
        {
            if (!context.TryGetCurrentTileLifetime(creation.CreatedTile, out int createdLifetime))
                yield break;
            contributors.RemoveAll(tile => tile == creation.CreatedTile);

            if (creation.CreatedTile != null)
                creation.CreatedTile.SetRuntimeState(TileRuntimeState.Special);

            try
            {
                yield return creation.CreatedTile.RunForLifetime(animator.PlayCreatedSpecialFormation(
                    creation.CreatedTile,
                    contributors,
                    creation.Duration,
                    context), createdLifetime);

                // Animasyon beklerken aynı GameObject havuzdan yeni bir taş olarak çıkmış olabilir.
                // Reveal ve finally yalnız kuyrukta yakalanan kullanımın görselini/durumunu değiştirir.
                if (creation.CreatedTile != null && creation.CreatedTile.IsCurrentLifetime(createdLifetime)
                    && creation.CreatedTile.GetSpecial() != TileSpecial.None)
                {
                    // Re-enforce correct layout right before reveal — the merge animation may
                    // have left the tile's icon in fill-cell state if it was previously a normal tile.
                    creation.CreatedTile.ApplyTileSize(board.TileSize);

                    creation.CreatedTile.PlaySpecialCreationReveal(
                        creation.CreatedTile.GetSpecial(),
                        board.TileSize);
                }

                if (context != null && context.NotifyCellImpactNow != null)
                {
                    for (int i = 0; i < contributors.Count; i++)
                    {
                        var tile = contributors[i];
                        if (!context.IsCurrentTile(tile))
                            continue;

                        context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
                    }
                }

                if (creation.TailHoldSeconds > 0f)
                    yield return new WaitForSeconds(creation.TailHoldSeconds);
            }
            finally
            {
                if (creation.CreatedTile != null
                    && creation.CreatedTile.IsCurrentLifetime(createdLifetime)
                    && creation.CreatedTile.RuntimeState == TileRuntimeState.Special)
                    creation.CreatedTile.SetRuntimeState(TileRuntimeState.Idle);
            }

            yield break;
        }

        // Yeni davranış: pulse implode-to-center
        if (creation.MergeTargetCell.HasValue)
        {
            if (contributors.Count == 0)
                yield break;

            yield return animator.PlayTilesImplodeToCell(
                creation.MergeTargetCell.Value,
                contributors,
                creation.Duration,
                creation.ClearAtNormalizedTime,
                tile =>
                {
                    if (!context.IsCurrentTile(tile))
                        return;

                    context.NotifyCellImpactNow?.Invoke(new Vector2Int(tile.X, tile.Y));
                    context.ClearTileNow?.Invoke(tile);
                }, context);

            if (creation.TailHoldSeconds > 0f)
                yield return new WaitForSeconds(creation.TailHoldSeconds);
        }
    }
}
