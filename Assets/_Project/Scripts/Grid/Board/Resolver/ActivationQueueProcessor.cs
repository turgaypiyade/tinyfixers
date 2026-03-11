using System.Collections.Generic;
using UnityEngine;
using static ResolutionContext;

/// <summary>
/// Manages the activation queue during special resolution.
/// Handles enqueueing specials, discovering chain reactions in the affected area,
/// and driving the processing loop.
///
/// Extracted from SpecialResolver — contains zero gameplay/visual logic,
/// only queue mechanics and iteration.
/// </summary>
public class ActivationQueueProcessor
{
    private readonly BoardController board;
    private readonly SpecialBehaviorDispatcher dispatcher;

    public ActivationQueueProcessor(BoardController board, SpecialBehaviorDispatcher dispatcher)
    {
        this.board = board;
        this.dispatcher = dispatcher;
    }

    /// <summary>
    /// Enqueues a single special tile for activation if it hasn't been queued yet.
    /// </summary>
    public void EnqueueActivation(ResolutionContext ctx, TileView special, TileView partner)
    {
        if (special == null) return;
        Vector2Int pos = new Vector2Int(special.X, special.Y);
        if (ctx.Queued.Contains(pos)) return;
        if (special.GetSpecial() == TileSpecial.None) return;

        ctx.Queued.Add(pos);
        Vector2Int? partnerPos = partner != null ? new Vector2Int(partner.X, partner.Y) : (Vector2Int?)null;
        ctx.Queue.Enqueue(new SpecialActivation(pos, partnerPos));
    }

    /// <summary>
    /// Scans the affected set for unprocessed specials and enqueues them (chain reaction discovery).
    /// </summary>
    public void EnqueueChainSpecials(ResolutionContext ctx)
    {
        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() == TileSpecial.None) continue;

            Vector2Int pos = new Vector2Int(tile.X, tile.Y);
            if (ctx.Processed.Contains(pos)) continue;
            EnqueueActivation(ctx, tile, null);
        }
    }

    /// <summary>
    /// Processes all queued activations, dispatching each to SpecialBehaviorDispatcher.
    /// After each activation, rescans for chain reactions.
    /// </summary>
    public void ProcessQueue(ResolutionContext ctx)
    {
        while (ctx.Queue.Count > 0)
        {
            var activation = ctx.Queue.Dequeue();
            ctx.Queued.Remove(activation.cell);
            if (ctx.Processed.Contains(activation.cell)) continue;

            ctx.Processed.Add(activation.cell);

            TileView actSpecial = board.Tiles[activation.cell.x, activation.cell.y];
            TileView actPartner = activation.partnerCell.HasValue
                ? board.Tiles[activation.partnerCell.Value.x, activation.partnerCell.Value.y]
                : null;

            dispatcher.ApplySpecialActivation(ctx, actSpecial, actPartner);
            EnqueueChainSpecials(ctx);
        }
    }
}