using System;
using System.Collections;
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

    /// <summary>
    /// Factory that builds a ready-to-use PatchBotExecutionRuntime for a single tile.
    /// Wired by SpecialResolver — keeps all resolver-level service refs out of this class.
    /// </summary>
    public Func<TileView, PatchBotTargetCoordinator, PatchBotExecutionRuntime> BuildPatchBotRuntime;

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

            Vector2Int originCoord = new Vector2Int(
                ctx.OverrideFanoutOrigin.X,
                ctx.OverrideFanoutOrigin.Y);

            if (ctx.PendingOverrideImplants.Count > 0)
                implantService.ApplyPendingOverrideImplants(ctx);

            // Override+PatchBot: PatchBotSpecial.Execute akışını kullan →
            // PatchbotDashUI yeni animasyonları (blade spinner, body separation, afterimage).
            System.Action<List<Vector2Int>> patchBotLauncher = null;
            if (ctx.OverrideDeferredPatchBotDashes.Count > 0 && BuildPatchBotRuntime != null)
            {
                patchBotLauncher = cells => LaunchPatchBotsViaPatchBotSpecial(cells);
            }

            actions.Add(new SystemOverrideFanoutPlacementAction(
                board,
                originCoord,
                targetCoords,
                ctx.OverrideFanoutNormalSelectionPulse,
                deferredPulseExplosionCells: null,
                deferredPatchBotCells: new List<Vector2Int>(ctx.OverrideDeferredPatchBotDashes),
                launchPatchBots: patchBotLauncher));
        }
        else if (ctx.PendingOverrideImplants.Count > 0)
        {
            implantService.ApplyPendingOverrideImplants(ctx);
        }

        // KRITIK:
        // Override+Line batch modunda erken queue/process yapma.
        // Yoksa line special'lar batch aksiyonuna kalmadan patlar.
        if (!ctx.SuppressImmediateOverrideQueueProcessing)
        {
            queueProcessor.EnqueueChainSpecials(ctx);
            if (ctx.Queue.Count > 0)
                queueProcessor.ProcessQueue(ctx);
        }

        return actions;
    }

    /// <summary>
    /// PatchBotSpecial.Execute üzerinden her implant edilmiş PatchBot'u sıralı fırlatır.
    /// Shared PatchBotTargetCoordinator ile grup hedef koordinasyonu sağlanır.
    /// PatchbotDashUI arka planda uçuş animasyonlarını çalıştırır.
    /// </summary>
    private void LaunchPatchBotsViaPatchBotSpecial(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0 || BuildPatchBotRuntime == null)
            return;

        // Ortak grup fırlatıcı: shared coordinator + solo PatchBotSpecial akışı
        // (PatchbotDashUI yeni animasyonları + canlı retargeting). Senkron enqueue + tek
        // PlayDashParallel → tüm botlar neredeyse aynı anda kalkar (pompaya bölünmez).
        PatchBotSpecial.LaunchGroupParallel(board, cells, BuildPatchBotRuntime);
    }
}