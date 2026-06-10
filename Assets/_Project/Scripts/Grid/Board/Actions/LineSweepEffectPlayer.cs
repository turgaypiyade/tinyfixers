using System.Collections;
using UnityEngine;
using System.Collections.Generic;
public sealed class LineSweepEffectPlayer : IClearEffectPlayer
{
    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is LineSweepEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        LineSweepEffectDescriptor line = effect as LineSweepEffectDescriptor;
        if (line == null)
            yield break;

        if (line.LineStrikes == null || line.LineStrikes.Count == 0)
            yield break;

        var strikes = new List<LightningLineStrike>(line.LineStrikes);

        if (board.Audio != null)
        {
            TileSpecial lineSpecial = TileSpecial.LineH;

            if (strikes.Count > 0 && !strikes[0].isHorizontal)
                lineSpecial = TileSpecial.LineV;

            board.Audio.Emit(
                BoardSfxRequest.SpecialActivate(
                    lineSpecial,
                    intensity: Mathf.Max(1, strikes.Count)
                )
            );
        }

        float duration = board.PlayLightningLineStrikes(
            strikes,
            delegate (Vector2Int cell)
            {
                // Fire arrival trigger first — deferred specials start concurrently here.
                if (line.ArrivalTriggers != null &&
                    line.ArrivalTriggers.TryGetValue(cell, out var trigger))
                    trigger?.Invoke();

                if (context != null && context.NotifyCellImpactNow != null)
                    context.NotifyCellImpactNow(cell);

                if (context != null && context.ClearTileNow != null)
                {
                    if (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
                    {
                        TileView tile = board.Tiles[cell.x, cell.y];
                        if (tile != null)
                            context.ClearTileNow(tile);
                    }
                }
            });

        if (duration > 0f)
            yield return new WaitForSeconds(duration + line.Timing.TailHoldSeconds);
    }
}