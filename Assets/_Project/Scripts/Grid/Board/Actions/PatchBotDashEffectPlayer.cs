using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotDashEffectPlayer : IClearEffectPlayer
{
    public bool CanPlay(IClearEffectDescriptor effect)
    {
        return effect is PatchBotDashEffectDescriptor;
    }

    public IEnumerator Play(IClearEffectDescriptor effect, BoardController board, ClearEffectPlaybackContext context)
    {
        PatchBotDashEffectDescriptor dash = effect as PatchBotDashEffectDescriptor;
        if (dash == null)
            yield break;

        if (!dash.OriginCell.HasValue || !dash.TargetCell.HasValue)
            yield break;

        if (board.PatchbotDashUI != null)
        {
            var requests = new List<BoardController.PatchbotDashRequest>(1);
            requests.Add(new BoardController.PatchbotDashRequest
            {
                from = dash.OriginCell.Value,
                to = dash.TargetCell.Value
            });

            yield return board.PatchbotDashUI.PlayDashParallel(requests, board);
        }

        if (context != null && context.NotifyCellImpactNow != null)
            context.NotifyCellImpactNow(dash.TargetCell.Value);

        yield return new WaitForSeconds(dash.Timing.TailHoldSeconds);
    }
}