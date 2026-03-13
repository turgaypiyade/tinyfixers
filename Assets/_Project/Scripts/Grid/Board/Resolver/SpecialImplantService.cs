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
    private struct DeferredPatchBotActivation
    {
        public TileView AutoPatchBot;
        public TileView PatchPartner;
        public TileView SystemOverride;
    }

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
        var deferredPatchBots = new List<DeferredPatchBotActivation>();

        for (int i = 0; i < ctx.PendingOverrideImplants.Count; i++)
            ApplyPendingOverrideImplant(ctx, ctx.PendingOverrideImplants[i], deferredPatchBots);

        // Override + PatchBot: önce tüm hücrelere implantı uygula,
        // sonra patchbotları üst satırdan başlayacak şekilde sırayla çalıştır.
        deferredPatchBots.Sort((left, right) =>
        {
            int byRow = right.AutoPatchBot.Y.CompareTo(left.AutoPatchBot.Y);
            if (byRow != 0) return byRow;
            return left.AutoPatchBot.X.CompareTo(right.AutoPatchBot.X);
        });

        float perPatchbotStartDelay = board.ApplySpecialChainTempo(0.05f);

        for (int i = 0; i < deferredPatchBots.Count; i++)
        {
            var activation = deferredPatchBots[i];
            float sequenceDelay = i * perPatchbotStartDelay;
            AutoPatchBotTeleportHitAndVanish(
                ctx,
                activation.AutoPatchBot,
                activation.PatchPartner,
                activation.SystemOverride,
                sequenceDelay);
        }

        ctx.PendingOverrideImplants.Clear();
    }

    private void ApplyPendingOverrideImplant(
        ResolutionContext ctx,
        PendingOverrideImplant pending,
        List<DeferredPatchBotActivation> deferredPatchBots)
    {
        TileView pendingTarget = board.Tiles[pending.targetCell.x, pending.targetCell.y];
        if (pendingTarget == null)
            return;

        pendingTarget.SetSpecial(pending.special, deferVisualUpdate: ctx.DeferOverrideImplantVisualRefresh);
        SpecialCellUtils.SyncAfterSpecialChange(board, pendingTarget);

        if (pending.special != TileSpecial.LineH && pending.special != TileSpecial.LineV)
        {
            ctx.OverrideImplantedTiles.Add(pendingTarget);
        }

        if (pending.special == TileSpecial.PatchBot)
        {
            TileView patchPartner = pending.partnerCell.HasValue
                ? board.Tiles[pending.partnerCell.Value.x, pending.partnerCell.Value.y]
                : null;
            TileView patchOverride = board.Tiles[pending.overrideCell.x, pending.overrideCell.y];

            deferredPatchBots.Add(new DeferredPatchBotActivation
            {
                AutoPatchBot = pendingTarget,
                PatchPartner = patchPartner,
                SystemOverride = patchOverride,
            });
            return;
        }

        // Override + PulseCore:
        // Pulse target'ı placement bitmeden Affected'e sokma.
        // Aksi halde final MatchClearAction içinde erken clear/patlama animasyonu oynuyor.
        // PulseCore'lar placement tamamlandıktan sonra SystemOverrideFanoutPlacementAction içinde
        // sırayla tetiklenecek.
        if (pending.special == TileSpecial.PulseCore)
        {
            ctx.OverrideDeferredPulseExplosions.Add(new Vector2Int(pendingTarget.X, pendingTarget.Y));
            return;
        }

        ctx.Affected.Add(pendingTarget);
        SpecialCellUtils.MarkAffectedCell(ctx, pendingTarget, board);

        TileView activePartner = pending.partnerCell.HasValue
            ? board.Tiles[pending.partnerCell.Value.x, pending.partnerCell.Value.y]
            : null;
        queueProcessor.EnqueueActivation(ctx, pendingTarget, activePartner);
    }
    /// <summary>
    /// Auto-fires a PatchBot that was implanted via Override+PatchBot conversion.
    /// Optional queued delay lets Override fan-out run PatchBots in a deterministic sequence.
    /// </summary>
    private void AutoPatchBotTeleportHitAndVanish(
        ResolutionContext ctx,
        TileView autoPatchBot,
        TileView patchBotTile,
        TileView systemOverrideTile,
        float queuedDelay = 0f)
    {
        if (autoPatchBot == null) return;

        // Override fan-out deferred refresh aktif olsa bile,
        // patchbot kaynak hücresinde görünür kalmamalı.
        SpecialVisualService.HideTileVisualForCombo(autoPatchBot);

        ctx.Affected.Add(autoPatchBot);
        SpecialCellUtils.MarkAffectedCell(ctx, autoPatchBot, board);

        var target = patchbotComboService.FindTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        float dashDelay = (ctx.DeferOverrideImplantVisualRefresh ? 0.10f : 0f) + Mathf.Max(0f, queuedDelay);
        visualService.FireImmediateDash(autoPatchBot.X, autoPatchBot.Y, target.x, target.y, dashDelay);

        var matchSetData = new HashSet<TileData>();
        patchbotComboService.HitCellOnce(matchSetData, target.x, target.y, target.tile,
            (x, y) => SpecialCellUtils.MarkAffectedCell(ctx, x, y, board),
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));

        foreach (var data in matchSetData)
        {
            if (board.Tiles[data.X, data.Y] != null) ctx.Affected.Add(board.Tiles[data.X, data.Y]);
        }
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
