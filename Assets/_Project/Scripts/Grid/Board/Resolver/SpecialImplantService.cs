using System.Collections.Generic;
using UnityEngine;
using static ResolutionContext;

/// <summary>
/// Handles the "implant" phase of SystemOverride combos:
/// when Override+Special fan-out places special types onto normal tiles,
/// each implanted tile is tracked, activated, and eventually cleaned up.
///
/// Also manages PatchBot auto-teleport for Override+PatchBot conversion.
/// </summary>
public class SpecialImplantService
{

    private readonly BoardController board;
    private readonly PatchbotComboService patchbotComboService;
    private readonly SpecialVisualService visualService;
    private readonly ActivationQueueProcessor queueProcessor;

    public SpecialImplantService(
        BoardController board,
        PatchbotComboService patchbotComboService,
        SpecialVisualService visualService,
        ActivationQueueProcessor queueProcessor)
    {
        this.board = board;
        this.patchbotComboService = patchbotComboService;
        this.visualService = visualService;
        this.queueProcessor = queueProcessor;
    }

    /// <summary>
    /// Applies all pending override implants: sets specials on target tiles,
    /// triggers PatchBot auto-dashes, enqueues resulting activations.
    /// </summary>
    public void ApplyPendingOverrideImplants(ResolutionContext ctx)
    {
        for (int i = 0; i < ctx.PendingOverrideImplants.Count; i++)
            ApplyPendingOverrideImplant(ctx, ctx.PendingOverrideImplants[i]);

        ctx.PendingOverrideImplants.Clear();
    }

    private void ApplyPendingOverrideImplant(
       ResolutionContext ctx,
       PendingOverrideImplant pending)
    {
        TileView pendingTarget = board.Tiles[pending.targetCell.x, pending.targetCell.y];
        if (pendingTarget == null)
            return;

        pendingTarget.SetSpecial(pending.special, deferVisualUpdate: ctx.DeferOverrideImplantVisualRefresh);
        SpecialCellUtils.SyncAfterSpecialChange(board, pendingTarget);

        if (pending.special != TileSpecial.LineH &&
            pending.special != TileSpecial.LineV &&
            pending.special != TileSpecial.PatchBot)
        {
            ctx.OverrideImplantedTiles.Add(pendingTarget);
        }

        if (pending.special == TileSpecial.PulseCore)
        {
            ctx.OverrideDeferredPulseExplosions.Add(new Vector2Int(pendingTarget.X, pendingTarget.Y));
            return;
        }

        ctx.Affected.Add(pendingTarget);
        SpecialCellUtils.MarkAffectedCell(ctx, pendingTarget, board);

        TileView activePartner = pending.special == TileSpecial.PatchBot
            ? null
            : (pending.partnerCell.HasValue
                ? board.Tiles[pending.partnerCell.Value.x, pending.partnerCell.Value.y]
                : null);

        queueProcessor.EnqueueActivation(ctx, pendingTarget, activePartner);
    }
    /// <summary>
    /// Cleans up implanted special visuals after resolution completes.
    /// Resets specials on implanted tiles back to None.
    /// </summary>
    public void CleanupImplantedTiles(ResolutionContext ctx)
    {
        if (ctx.OverrideImplantedTiles.Count == 0) return;

        foreach (var tile in ctx.OverrideImplantedTiles)
        {
            if (tile != null && tile.GetSpecial() != TileSpecial.None)
            {
                tile.SetSpecial(TileSpecial.None);
                SpecialCellUtils.SyncAfterSpecialChange(board, tile);
            }
        }
        ctx.OverrideImplantedTiles.Clear();
    }
}
