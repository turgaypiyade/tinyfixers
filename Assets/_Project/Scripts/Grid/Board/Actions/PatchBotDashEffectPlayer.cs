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

        FlushLegacyDashQueue(board);

        if (board.PatchbotDashUI != null)
        {
            var requests = new List<BoardController.PatchbotDashRequest>(1);
            requests.Add(new BoardController.PatchbotDashRequest
            {
                from = dash.OriginCell.Value,
                to = dash.TargetCell.Value
            });

            board.PatchbotDashUI.PlayDashParallel(requests, board);
        }

        if (context != null && context.NotifyCellImpactNow != null)
            context.NotifyCellImpactNow(dash.TargetCell.Value);

        FlushLegacyDashQueue(board);

        yield return new WaitForSeconds(dash.Timing.TailHoldSeconds);
    }

    private void FlushLegacyDashQueue(BoardController board)
    {
        if (board == null || board.TempPatchbotDashRequests == null)
            return;

        board.TempPatchbotDashRequests.Clear();
        board.ConsumePatchbotDashRequests(board.TempPatchbotDashRequests);
        board.TempPatchbotDashRequests.Clear();
    }
}