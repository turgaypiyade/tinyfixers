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
        if (creation == null)
            yield break;

        BoardAnimator animator = board.boardAnimatorRef;
        if (animator == null)
            yield break;

        var contributors = creation.TargetTiles != null
            ? new List<TileView>(creation.TargetTiles)
            : new List<TileView>();

        contributors.RemoveAll(tile => tile == null);
        if (contributors.Count == 0)
            yield break;

        // Eski davranış: created special formation
        if (creation.CreatedTile != null)
        {
            contributors.RemoveAll(tile => tile == creation.CreatedTile);
            if (contributors.Count == 0)
                yield break;

            yield return animator.PlayCreatedSpecialFormation(
                creation.CreatedTile,
                contributors,
                creation.Duration);

            // Yaratım reveal: imaj + halo birlikte büyür, sonra 360° döner ve oturur.
            // Animasyondan sonra tile destroy edilmiş olabilir (örn. PatchBot uçuşu)
            // bu yüzden Unity null kontrolü (== null) ile destroyed kontrolü yapıyoruz.
            if (creation.CreatedTile != null
                && creation.CreatedTile.gameObject != null
                && creation.CreatedTile.gameObject.activeInHierarchy
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
                    if (tile == null)
                        continue;

                    context.NotifyCellImpactNow(new Vector2Int(tile.X, tile.Y));
                }
            }

            if (creation.TailHoldSeconds > 0f)
                yield return new WaitForSeconds(creation.TailHoldSeconds);

            yield break;
        }

        // Yeni davranış: pulse implode-to-center
        if (creation.MergeTargetCell.HasValue)
        {
            yield return animator.PlayTilesImplodeToCell(
                creation.MergeTargetCell.Value,
                contributors,
                creation.Duration,
                creation.ClearAtNormalizedTime,
                tile =>
                {
                    if (tile == null || context == null)
                        return;

                    context.NotifyCellImpactNow?.Invoke(new Vector2Int(tile.X, tile.Y));
                    context.ClearTileNow?.Invoke(tile);
                });

            if (creation.TailHoldSeconds > 0f)
                yield return new WaitForSeconds(creation.TailHoldSeconds);
        }
    }
}