using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the SystemOverride "fan-out" phase:
/// after the initial combo/activation produces a set of fan-out targets,
/// this service builds the placement action, triggers implants,
/// hides the origin tile, and kicks off post-fan-out chain processing.
///
/// Extracted from the fan-out blocks that appeared in both
/// ResolveSpecialSwap and ResolveSpecialSolo.
/// </summary>
public class SpecialFanoutService
{
    private readonly BoardController board;
    private readonly SpecialImplantService implantService;
    private readonly ActivationQueueProcessor queueProcessor;
    private readonly SpecialVisualService visualService;

    public SpecialFanoutService(
        BoardController board,
        SpecialImplantService implantService,
        ActivationQueueProcessor queueProcessor,
        SpecialVisualService visualService)
    {
        this.board = board;
        this.implantService = implantService;
        this.queueProcessor = queueProcessor;
        this.visualService = visualService;
    }

    /// <summary>
    /// Processes the SystemOverride fan-out phase:
    /// 1) Builds SystemOverrideFanoutPlacementAction if there are targets
    /// 2) Applies pending implants
    /// 3) Hides the origin tile
    /// 4) Processes any chain specials spawned by the fan-out
    ///
    /// Returns any BoardActions generated (e.g. SystemOverrideFanoutPlacementAction).
    /// </summary>
    public List<BoardAction> ProcessFanout(ResolutionContext ctx, TileView soloSpecialTile = null)
    {
        var actions = new List<BoardAction>();

        if (ctx.OverrideFanoutOrigin != null && ctx.OverrideFanoutTargets.Count > 0)
        {
            ctx.DeferOverrideImplantVisualRefresh = true;

            List<Vector2Int> targetCoords = new List<Vector2Int>();
            foreach (var t in ctx.OverrideFanoutTargets)
            {
                targetCoords.Add(new Vector2Int(t.X, t.Y));
            }
            Vector2Int originCoord = new Vector2Int(ctx.OverrideFanoutOrigin.X, ctx.OverrideFanoutOrigin.Y);

            actions.Add(new SystemOverrideFanoutPlacementAction(board, originCoord, targetCoords, ctx.OverrideFanoutNormalSelectionPulse));

            if (ctx.PendingOverrideImplants.Count > 0)
                implantService.ApplyPendingOverrideImplants(ctx);
        }
        else if (ctx.PendingOverrideImplants.Count > 0)
        {
            implantService.ApplyPendingOverrideImplants(ctx);
        }

        // Fan-out tamamlandı — override taşını artık gizle
        if (ctx.OverrideFanoutOrigin != null)
        {
           // SpecialVisualService.HideTileVisualForCombo(ctx.OverrideFanoutOrigin);
        }
        else if (soloSpecialTile != null && soloSpecialTile.GetSpecial() == TileSpecial.SystemOverride)
        {
            // Solo activation: fan-out yoksa soloSpecialTile'ı gizle (deferHide durumu)
          //  SpecialVisualService.HideTileVisualForCombo(soloSpecialTile);
        }

        // Fan-out'tan eklenen chain special'ları işle
        if (ctx.Queue.Count > 0)
        {
            queueProcessor.EnqueueChainSpecials(ctx);
            queueProcessor.ProcessQueue(ctx);
        }

        return actions;
    }
}