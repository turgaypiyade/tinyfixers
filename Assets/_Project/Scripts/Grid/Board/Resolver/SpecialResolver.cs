using System;
using System.Collections.Generic;
using UnityEngine;

public class SpecialResolver
{
    private readonly BoardController board;
    private readonly PulseCoreImpactService pulseCoreImpactService;
    private readonly SpecialVisualService visualService;
    private readonly SpecialBehaviorDispatcher dispatcher;
    private readonly ActivationQueueProcessor queueProcessor;
    private readonly SpecialImplantService implantService;
    private readonly SpecialFanoutService fanoutService;
    private readonly SpecialEffectOrchestrator effectOrchestrator;
    private readonly PatchbotComboService patchbotComboService;
    private readonly LineVSpecial lineVSpecial = new();
    private readonly LineHSpecial lineHSpecial = new();
    private readonly LineVLineHCombo lineVLineHCombo = new();
    private readonly PulseCoreSpecial pulseCoreSpecial = new();
    private readonly PatchBotSpecial patchBotSpecial = new();
    private readonly LineVHPulseCoreCombo lineVHPulseCoreCombo = new();
    private readonly LineHPatchBotCombo lineHPatchBotCombo = new();
    private readonly LineVPatchBotCombo lineVPatchBotCombo = new();
    private readonly PulseCorePatchBotCombo pulseCorePatchBotCombo = new(25);
    private readonly OverrideSpecial overrideSpecial = new();
    private readonly PatchBotCombo patchBotCombo = new();
    private readonly OverrideSpecializedCombo overrideSpecializedCombo = new();
    private readonly OverrideOverrideCombo overrideOverrideCombo = new();
    private readonly PulsePulseCombo pulsePulseCombo = new();
    private readonly ResolutionContext ctx = new();
    public bool SuppressVisualSideEffects;
    public SpecialResolver(BoardController board, BoardAnimator boardAnimator, PulseCoreImpactService pulseCoreImpactService)
    {
        this.board = board;
        this.pulseCoreImpactService = pulseCoreImpactService;

        patchbotComboService = new PatchbotComboService(board);

        visualService = new SpecialVisualService(board, boardAnimator, patchbotComboService);
        effectOrchestrator = new SpecialEffectOrchestrator(board);
        dispatcher = new SpecialBehaviorDispatcher(board, patchbotComboService, visualService, effectOrchestrator);
        queueProcessor = new ActivationQueueProcessor(board, dispatcher);
        implantService = new SpecialImplantService(board, patchbotComboService, visualService, queueProcessor);
        fanoutService = new SpecialFanoutService(board, implantService, queueProcessor, visualService);

        dispatcher.QueueProcessor = queueProcessor;
    }

    public List<BoardAction> ResolveSpecialSwap(TileView a, TileView b, TileSpecial originalSa, TileSpecial originalSb, TileType? capturedOverridePartnerType = null, HashSet<Vector2Int> externalProtectedCells = null)
    {
        var actions = new List<BoardAction>();

        if (a == null && b == null)
        {
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        Debug.Log($"[ResolveSpecialSwap] a={((a != null) ? $"({a.X},{a.Y}) currentA={a.GetSpecial()}" : "null")} originalA={originalSa} | b={((b != null) ? $"({b.X},{b.Y}) currentB={b.GetSpecial()}" : "null")} originalB={originalSb}");

        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        bool aOriginallySpecial = originalSa != TileSpecial.None;
        bool bOriginallySpecial = originalSb != TileSpecial.None;
        bool bothOriginallySpecial = aOriginallySpecial && bOriginallySpecial;

        // Bu metod sadece special swap resolve için; normal/no-match swap akışında
        // buraya düşülse bile zincir çözümüne girmeden güvenle çık.
        if (!aOriginallySpecial && !bOriginallySpecial)
        {
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        bool originalSaIsLine = originalSa == TileSpecial.LineH || originalSa == TileSpecial.LineV;
        bool originalSbIsLine = originalSb == TileSpecial.LineH || originalSb == TileSpecial.LineV;
        bool originalSaIsPulse = originalSa == TileSpecial.PulseCore;
        bool originalSbIsPulse = originalSb == TileSpecial.PulseCore;

        bool consumeNormalPartner = bothOriginallySpecial;
        if (a != null && b != null)
            visualService.HideSwapSourceVisuals(a, b, originalSa, originalSb, consumeNormalPartner);

        ctx.Reset();

        bool originalSaIsPatchBot = originalSa == TileSpecial.PatchBot;
        bool originalSbIsPatchBot = originalSb == TileSpecial.PatchBot;

        if (originalSaIsPatchBot && originalSbIsPatchBot)
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = patchBotCombo.Execute(new PatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = true,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                    visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.PatchBot", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if ((originalSaIsPatchBot && originalSb == TileSpecial.PulseCore) ||
            (originalSbIsPatchBot && originalSa == TileSpecial.PulseCore))
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var patchBotTile = originalSaIsPatchBot ? a : b;
            var pulseTile = originalSaIsPulse ? a : b;

            var result = pulseCorePatchBotCombo.Execute(new PulseCorePatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = patchBotTile,
                Partner = pulseTile,
                FinalizeAtEnd = true,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                    visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EmitBoardSignal = signal => { },
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.PulseCorePatchBot", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }
        if ((originalSaIsPatchBot && originalSb == TileSpecial.LineV) ||
            (originalSbIsPatchBot && originalSa == TileSpecial.LineV))
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = lineVPatchBotCombo.Execute(new LineVPatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = true,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.LineVPatchBot", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }


        if ((originalSaIsPatchBot && originalSb == TileSpecial.LineH) ||
            (originalSbIsPatchBot && originalSa == TileSpecial.LineH))
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = lineHPatchBotCombo.Execute(new LineHPatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = true,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.LineHPatchBot", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if (bothOriginallySpecial && originalSa == TileSpecial.PulseCore && originalSb == TileSpecial.PulseCore)
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = pulsePulseCombo.Execute(new PulsePulseComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = true,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                    visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EmitBoardSignal = signal => { },
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
            });

            actions.AddRange(result.Actions);
            DrainDeferredPulseComboOverrides(actions);
            TraceSpecialChain("ResolveSpecialSwap.PulsePulse", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if ((originalSaIsPulse && originalSbIsLine) || (originalSbIsPulse && originalSaIsLine))
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = lineVHPulseCoreCombo.Execute(new LineVHPulseCoreComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = b,
                Partner = a,
                FinalizeAtEnd = true,
                ExecuteSpecialActions = (resolution, tile, partner) =>
                    ExecuteSpecialActions(resolution, tile, partner),
                DebugLog = msg => Debug.Log(msg),
                EmitComboTriggered = (sa, sb, cell) => effectOrchestrator.EmitComboTriggered(sa, sb, cell),
                EmitPulseEmitterComboTriggered = cell => effectOrchestrator.EmitPulseEmitterComboTriggered(cell)
            });

            actions.AddRange(result.Actions);
            DrainDeferredLineOverrides(actions);
            TraceSpecialChain("ResolveSpecialSwap.LineVHPulseCore", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        bool originalSaIsOverride = originalSa == TileSpecial.SystemOverride;
        bool originalSbIsOverride = originalSb == TileSpecial.SystemOverride;
        bool originalSaIsOverrideSupported = originalSa == TileSpecial.LineH || originalSa == TileSpecial.LineV || originalSa == TileSpecial.PulseCore || originalSa == TileSpecial.PatchBot;
        bool originalSbIsOverrideSupported = originalSb == TileSpecial.LineH || originalSb == TileSpecial.LineV || originalSb == TileSpecial.PulseCore || originalSb == TileSpecial.PatchBot;

        Debug.Log($"[ResolveSpecialSwap] OverrideRouting: aIsOvr={originalSaIsOverride} bIsOvr={originalSbIsOverride} aIsSupported={originalSaIsOverrideSupported} bIsSupported={originalSbIsOverrideSupported} bothOrigSpecial={bothOriginallySpecial}");

        if (originalSaIsOverride && originalSbIsOverride)
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = overrideOverrideCombo.Execute(new OverrideOverrideComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = true,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                    visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.OverrideOverride", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if ((originalSaIsOverride && originalSbIsOverrideSupported) ||
            (originalSbIsOverride && originalSaIsOverrideSupported))
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = overrideSpecializedCombo.Execute(new OverrideSpecializedComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = b,
                Partner = a,
                FinalizeAtEnd = true,
                EnqueueActivation = (resolution, tile, partner) => queueProcessor.EnqueueActivation(resolution, tile, partner),
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution),
                UseBatchClearSpike = true,
                DeferredSpecialBatchSize = 15,
                EnqueueCascadeBetweenBatches = true
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.OverrideSpecialized", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if (bothOriginallySpecial && originalSaIsLine && originalSbIsLine)
        {
            ctx.Affected.Add(a);
            ctx.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(ctx, a, board);
            SpecialCellUtils.MarkAffectedCell(ctx, b, board);

            var result = lineVLineHCombo.Execute(new LineVLineHComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = b,      // source
                Partner = a,     // target
                Center = a,      // merkez hedef hücre
                FinalizeAtEnd = true,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution),
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            actions.AddRange(result.Actions);
            DrainDeferredLineOverrides(actions);
            TraceSpecialChain("ResolveSpecialSwap.LineVLineH", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        bool originalSaIsNormal = originalSa == TileSpecial.None;
        bool originalSbIsNormal = originalSb == TileSpecial.None;

        if ((originalSaIsOverride && originalSbIsNormal) ||
            (originalSbIsOverride && originalSaIsNormal))
        {
            var overrideTile = originalSaIsOverride ? a : b;
            var normalTile = overrideTile == a ? b : a;

            var result = overrideSpecial.Execute(new OverrideExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = overrideTile,
                Partner = normalTile,
                FinalizeAtEnd = true,
                ForcedPartnerType = capturedOverridePartnerType,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals =
                    (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSwap.OverrideNormal", a, b);
            board.IsSpecialActivationPhase = false;
            return actions;
        }


        if (!bothOriginallySpecial)
        {
            var specialTile = aOriginallySpecial ? a : b;
            var originalSpecial = aOriginallySpecial ? originalSa : originalSb;

            if (originalSpecial == TileSpecial.LineV)
            {
                ctx.Affected.Add(specialTile);
                SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

                var result = lineVSpecial.Execute(new LineVExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = null,
                    FinalizeAtEnd = true,
                    ProtectedCells = externalProtectedCells,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });
                actions.AddRange(result.Actions);
                DrainDeferredLineOverrides(actions);
                TraceSpecialChain("ResolveSpecialSwap.LineV", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;
            }

            if (originalSpecial == TileSpecial.LineH)
            {
                ctx.Affected.Add(specialTile);
                SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

                var result = lineHSpecial.Execute(new LineHExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = null,
                    FinalizeAtEnd = true,
                    ProtectedCells = externalProtectedCells,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });

                actions.AddRange(result.Actions);
                DrainDeferredLineOverrides(actions);
                TraceSpecialChain("ResolveSpecialSwap.LineH", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;
            }

            if (originalSpecial == TileSpecial.PulseCore)
            {
                // Pulse-only zincir (2+) ve swap'tan korunacak yeni special yoksa:
                // sıralı patlama + gravity + havada yakalama. Aksi hâlde eski birleşik yol.
                var normalPartnerForGuard = aOriginallySpecial ? b : a;
                bool hasProtectedForChain =
                    (externalProtectedCells != null && externalProtectedCells.Count > 0)
                    || (normalPartnerForGuard != null && normalPartnerForGuard.GetSpecial() != TileSpecial.None);

                if (!hasProtectedForChain && IsSupportedPulseChain(specialTile, PulseChainAreaHalf))
                {
                    actions.Add(new PulseChainSequenceAction(
                        board,
                        new List<TileView> { specialTile },
                        PulseChainAreaHalf,
                        board.PulseChainCatchOverlap,
                        ResolveOtherSpecialInline));
                    TraceSpecialChain("ResolveSpecialSwap.PulseChain", a, b);
                    board.IsSpecialActivationPhase = false;
                    return actions;
                }

                ctx.Affected.Add(specialTile);
                SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

                // Swap sırasında normal taraftan oluşan yeni special hücrelerini koru.
                // externalProtectedCells: ProcessSwap'tan gelen kesin liste (winner herhangi bir tile olabilir).
                // normalPartner fallback: externalProtectedCells yoksa veya eksikse ek güvence.
                HashSet<Vector2Int> protectedCells = externalProtectedCells != null
                    ? new HashSet<Vector2Int>(externalProtectedCells)
                    : null;

                var normalPartner = aOriginallySpecial ? b : a;
                if (normalPartner != null && normalPartner.GetSpecial() != TileSpecial.None)
                {
                    var cell = new Vector2Int(normalPartner.X, normalPartner.Y);
                    (protectedCells ??= new HashSet<Vector2Int>()).Add(cell);
                    Debug.Log($"[PulseCore] protecting normalPartner special at ({cell.x},{cell.y})");
                }

                if (protectedCells != null && protectedCells.Count > 0)
                    Debug.Log($"[PulseCore] total protected cells={protectedCells.Count}: [{string.Join(", ", protectedCells)}]");

                var result = pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = null,
                    FinalizeAtEnd = true,
                    ProtectedCells = protectedCells,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });
                actions.AddRange(result.Actions);
                DrainDeferredPulseComboOverrides(actions);
                TraceSpecialChain("ResolveSpecialSwap.PulseCore", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;
            }

            if (originalSpecial == TileSpecial.PatchBot)
            {
                ctx.Affected.Add(specialTile);
                SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

                var result = patchBotSpecial.Execute(new PatchBotExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = null,
                    FinalizeAtEnd = true,
                    PatchbotService = patchbotComboService,
                    VisualService = visualService,
                    Effects = effectOrchestrator,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
                });

                actions.AddRange(result.Actions);
                TraceSpecialChain("ResolveSpecialSwap.PatchBot", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;
            }
        }

        return actions;
        /*         if (aOriginallySpecial)
                {
                    ctx.Affected.Add(a);
                    SpecialCellUtils.MarkAffectedCell(ctx, a, board);
                }

                if (bOriginallySpecial)
                {
                    ctx.Affected.Add(b);
                    SpecialCellUtils.MarkAffectedCell(ctx, b, board);
                }

                if (consumeNormalPartner)
                {
                    ctx.Affected.Add(a);
                    ctx.Affected.Add(b);
                    SpecialCellUtils.MarkAffectedCell(ctx, a, board);
                    SpecialCellUtils.MarkAffectedCell(ctx, b, board);
                }

                bool suppressPulseImpactAnimations = (originalSaIsPulse && originalSbIsPulse) || (originalSaIsOverride && originalSbIsOverride);
                bool suppressPerTileClearVfx = (originalSaIsPulse && originalSbIsLine) || (originalSbIsPulse && originalSaIsLine);

                ctx.HasLineActivation = originalSaIsLine || originalSbIsLine;

                if (bothOriginallySpecial)
                {
                    dispatcher.ApplyComboEffect(ctx, a, b, originalSa, originalSb);
                    ctx.Processed.Add(new Vector2Int(a.X, a.Y));
                    ctx.Processed.Add(new Vector2Int(b.X, b.Y));
                }
                else
                {
                    var specialTile = aOriginallySpecial ? a : b;
                    var partnerTile = aOriginallySpecial ? b : a;
                    var originalSpecial = aOriginallySpecial ? originalSa : originalSb;
                    var originalPartner = aOriginallySpecial ? originalSb : originalSa;

                    TileView partnerForActivation = null;
                    if (originalSpecial == TileSpecial.SystemOverride)
                        partnerForActivation = partnerTile;
                    else if (originalSpecial == TileSpecial.PatchBot && originalPartner != TileSpecial.None)
                        partnerForActivation = partnerTile;

                    queueProcessor.EnqueueActivation(ctx, specialTile, partnerForActivation);
                }

                queueProcessor.EnqueueChainSpecials(ctx);
                queueProcessor.ProcessQueue(ctx);

                var fanoutActions = fanoutService.ProcessFanout(ctx);
                actions.AddRange(fanoutActions);

                if (ctx.OverrideDeferredPulseExplosions.Count == 0)
                    implantService.CleanupImplantedTiles(ctx);

                if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0)
                    visualService.FireOverrideOverrideSpecialVisuals(ctx.Affected, ctx.OverrideRadialClearDelays);

                actions.Add(BuildMatchClearAction(suppressPulseImpactAnimations, suppressPerTileClearVfx));

                TraceSpecialChain("ResolveSpecialSwap", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;  */
    }

    public List<BoardAction> ResolveSpecialSolo(TileView specialTile)
    {
        var actions = new List<BoardAction>();
        if (specialTile == null) return actions;

        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        bool deferHide = specialTile.GetSpecial() == TileSpecial.SystemOverride;
        if (!deferHide)
            SpecialVisualService.HideTileVisualForCombo(specialTile);

        ctx.Reset();

        TileSpecial spec = specialTile.GetSpecial();

        if (spec == TileSpecial.LineV)
        {
            var result = lineVSpecial.Execute(new LineVExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = specialTile,
                Partner = null,
                FinalizeAtEnd = true,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
            });

            actions.AddRange(result.Actions);
            DrainDeferredLineOverrides(actions);
            TraceSpecialChain("ResolveSpecialSolo.LineV", specialTile, null);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if (spec == TileSpecial.LineH)
        {
            var result = lineHSpecial.Execute(new LineHExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = specialTile,
                Partner = null,
                FinalizeAtEnd = true,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
            });

            actions.AddRange(result.Actions);
            DrainDeferredLineOverrides(actions);
            TraceSpecialChain("ResolveSpecialSolo.LineH", specialTile, null);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if (spec == TileSpecial.PulseCore)
        {
            // Pulse-only zincir (2+ PulseCore, alanlarında pulse-olmayan special yok):
            // sıralı patlama + her patlamada anında clear + gravity + havada yakalama.
            if (IsSupportedPulseChain(specialTile, PulseChainAreaHalf))
            {
                actions.Add(new PulseChainSequenceAction(
                    board,
                    new List<TileView> { specialTile },
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline));
                TraceSpecialChain("ResolveSpecialSolo.PulseChain", specialTile, null);
                board.IsSpecialActivationPhase = false;
                return actions;
            }

            var result = pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = specialTile,
                Partner = null,
                FinalizeAtEnd = true,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
            });

            actions.AddRange(result.Actions);
            DrainDeferredPulseComboOverrides(actions);
            TraceSpecialChain("ResolveSpecialSolo.PulseCore", specialTile, null);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        if (spec == TileSpecial.PatchBot)
        {
            var result = patchBotSpecial.Execute(new PatchBotExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = specialTile,
                Partner = null,
                FinalizeAtEnd = true,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                FireOverrideOverrideSpecialVisuals = (affected, delays) => visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
            });

            actions.AddRange(result.Actions);
            TraceSpecialChain("ResolveSpecialSolo.PatchBot", specialTile, null);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        ctx.Affected.Add(specialTile);
        SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

        ctx.HasLineActivation = spec == TileSpecial.LineH || spec == TileSpecial.LineV;

        queueProcessor.EnqueueActivation(ctx, specialTile, null);
        queueProcessor.EnqueueChainSpecials(ctx);
        queueProcessor.ProcessQueue(ctx);

        var fanoutActions = fanoutService.ProcessFanout(ctx, soloSpecialTile: deferHide ? specialTile : null);
        actions.AddRange(fanoutActions);

        if (ctx.OverrideDeferredPulseExplosions.Count == 0)
            implantService.CleanupImplantedTiles(ctx);

        actions.Add(BuildMatchClearAction(suppressPulseImpact: false, suppressPerTileClearVfx: false));

        TraceSpecialChain("ResolveSpecialSolo", specialTile, null);
        board.IsSpecialActivationPhase = false;
        return actions;
    }

    // Tekli PulseCore patlama alanı yarıçapı (affectedCellCount=25 → 5x5 → half=2).
    private const int PulseChainAreaHalf = 2;

    // BFS ile PulseCore zincirini keşfeder. Zincir pulse + DESTEKLENEN special'lardan
    // (PulseCore, LineV, LineH) oluşuyor ve 2+ special içeriyorsa true → ilerlemeli
    // sıralı motor (PulseChainSequenceAction) kullanılır. DESTEKLENMEYEN bir special
    // (Override/PatchBot/Bomb vb.) bulunursa false → eski birleşik yol (o kombolar
    // bozulmasın). Sadece guard kararı için; sıralı action zinciri kendi keşfeder.
    private bool IsSupportedPulseChain(TileView first, int areaHalf)
    {
        if (first == null || !first || first.GetSpecial() != TileSpecial.PulseCore)
            return false;

        var pulses = new HashSet<TileView> { first };
        var others = new HashSet<TileView>();
        var queue = new Queue<TileView>();
        queue.Enqueue(first);

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == null || !p) continue;

            int cx = p.X, cy = p.Y;
            for (int x = cx - areaHalf; x <= cx + areaHalf; x++)
            {
                for (int y = cy - areaHalf; y <= cy + areaHalf; y++)
                {
                    if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;

                    var t = board.Tiles[x, y];
                    if (t == null) continue;

                    var sp = t.GetSpecial();
                    if (sp == TileSpecial.None) continue;

                    if (sp == TileSpecial.PulseCore)
                    {
                        if (pulses.Add(t)) queue.Enqueue(t);
                    }
                    else if (sp == TileSpecial.LineV || sp == TileSpecial.LineH || sp == TileSpecial.PatchBot)
                    {
                        others.Add(t); // inline aktive edilir; alanı burada gezilmez
                    }
                    else
                    {
                        // Desteklenmeyen special (Override vb.) → eski yola düş.
                        return false;
                    }
                }
            }
        }

        return (pulses.Count + others.Count) >= 2;
    }

    // PulseChainSequenceAction'ın playback'te pulse-olmayan bir special'ı (Line/PatchBot)
    // inline aktive etmesi için callback. Mevcut per-special mantığını scoped bir ctx ile
    // FinalizeAtEnd=true çalıştırıp clear/effect action'larını döndürür. Desteklenmeyen
    // tip → null (action normal taş gibi kırar).
    private List<BoardAction> ResolveOtherSpecialInline(TileView tile)
    {
        if (tile == null || !tile) return null;

        var sp = tile.GetSpecial();
        var scoped = new ResolutionContext();
        scoped.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(scoped, tile, board);

        switch (sp)
        {
            case TileSpecial.LineV:
                scoped.HasLineActivation = true;
                return lineVSpecial.Execute(new LineVExecutionRuntime
                {
                    Board = board, Context = scoped, Origin = tile, Partner = null, FinalizeAtEnd = true,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = c => fanoutService.ProcessFanout(c),
                    CleanupImplantedTiles = c => implantService.CleanupImplantedTiles(c),
                    FireOverrideOverrideSpecialVisuals = (a, d) => visualService.FireOverrideOverrideSpecialVisuals(a, d),
                    EnqueueChainSpecials = res => queueProcessor.EnqueueChainSpecials(res),
                    ProcessQueue = res => queueProcessor.ProcessQueue(res)
                }).Actions;

            case TileSpecial.LineH:
                scoped.HasLineActivation = true;
                return lineHSpecial.Execute(new LineHExecutionRuntime
                {
                    Board = board, Context = scoped, Origin = tile, Partner = null, FinalizeAtEnd = true,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = c => fanoutService.ProcessFanout(c),
                    CleanupImplantedTiles = c => implantService.CleanupImplantedTiles(c),
                    FireOverrideOverrideSpecialVisuals = (a, d) => visualService.FireOverrideOverrideSpecialVisuals(a, d),
                    EnqueueChainSpecials = res => queueProcessor.EnqueueChainSpecials(res),
                    ProcessQueue = res => queueProcessor.ProcessQueue(res)
                }).Actions;

            case TileSpecial.PatchBot:
                return patchBotSpecial.Execute(new PatchBotExecutionRuntime
                {
                    Board = board, Context = scoped, Origin = tile, Partner = null, FinalizeAtEnd = true,
                    PatchbotService = patchbotComboService,
                    VisualService = visualService,
                    Effects = effectOrchestrator,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = c => fanoutService.ProcessFanout(c),
                    CleanupImplantedTiles = c => implantService.CleanupImplantedTiles(c),
                    FireOverrideOverrideSpecialVisuals = (a, d) => visualService.FireOverrideOverrideSpecialVisuals(a, d)
                }).Actions;

            default:
                return null; // Override vb. henüz desteklenmiyor → guard zaten engeller.
        }
    }

    private List<BoardAction> ExecuteSpecialActionsNoFinalize(ResolutionContext ctx, TileView tile, TileView partner)
    {
        var actions = new List<BoardAction>();

        if (tile == null)
            return actions;

        string partnerLabel = "null";
        if (partner != null)
            partnerLabel = $"({partner.X},{partner.Y})/{partner.GetSpecial()}";

        Debug.Log($"[ExecuteSpecialActionsNoFinalize] tile=({tile.X},{tile.Y}) special={tile.GetSpecial()} partner={partnerLabel}");

        var special = tile.GetSpecial();

        switch (special)
        {
            case TileSpecial.LineH:
                {
                    lineHSpecial.Execute(new LineHExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = false,
                        SuppressVisualSideEffects = false,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });

                    break;
                }

            case TileSpecial.LineV:
                {
                    lineVSpecial.Execute(new LineVExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = false,
                        SuppressVisualSideEffects = false,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });

                    break;
                }

            case TileSpecial.PulseCore:
                {
                    pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = false,
                        SuppressVisualSideEffects = false,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });
                    break;
                }

            case TileSpecial.PatchBot:
                {
                    patchBotSpecial.Execute(new PatchBotExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = false,
                        PatchbotService = patchbotComboService,
                        VisualService = visualService,
                        Effects = effectOrchestrator,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
                    });
                    break;
                }

            case TileSpecial.SystemOverride:
                {
                    dispatcher.ApplySpecialActivation(ctx, tile, partner);
                    break;
                }
        }

        return actions;
    }
    private List<BoardAction> ExecuteSpecialActions(ResolutionContext ctx, TileView tile, TileView partner)
    {
        var actions = new List<BoardAction>();

        if (tile == null)
            return actions;

        var special = tile.GetSpecial();

        switch (special)
        {
            case TileSpecial.LineH:
                {
                    var res = lineHSpecial.Execute(new LineHExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);

                    DrainDeferredLineOverrides(actions);
                    break;
                }

            case TileSpecial.LineV:
                {
                    var res = lineVSpecial.Execute(new LineVExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);

                    DrainDeferredLineOverrides(actions);
                    break;

                }

            case TileSpecial.PulseCore:
                {
                    var res = pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = true,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                        EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);

                    break;
                }

            case TileSpecial.PatchBot:
                {
                    var res = patchBotSpecial.Execute(new PatchBotExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = true,
                        PatchbotService = patchbotComboService,
                        VisualService = visualService,
                        Effects = effectOrchestrator,
                        ActivateSpecial = dispatcher.ApplySpecialActivation,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);

                    break;
                }

            case TileSpecial.SystemOverride:
                {
                    var res = overrideSpecial.Execute(new OverrideExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = true,
                        ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                        CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                        FireOverrideOverrideSpecialVisuals = (affected, delays) =>
                            visualService.FireOverrideOverrideSpecialVisuals(affected, delays)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);

                    break;
                }
        }

        return actions;
    }

    public void ExpandSpecialChain(
        HashSet<TileView> affected,
        HashSet<Vector2Int> affectedCells,
        out bool hasLineActivation,
        out bool hasAnySpecialActivation,
        HashSet<TileView> lightningVisualTargets = null,
        List<LightningLineStrike> lightningLineStrikes = null)
    {
        hasLineActivation = false;
        hasAnySpecialActivation = false;
        if (affected == null || affected.Count == 0) return;

        bool previousSpecialPhase = board.IsSpecialActivationPhase;
        var savedAffectedCells = ctx.AffectedCells;

        board.IsSpecialActivationPhase = true;
        ctx.AffectedCells = affectedCells ?? new HashSet<Vector2Int>();

        ctx.Processed.Clear();
        ctx.Queued.Clear();
        ctx.Queue.Clear();
        ctx.Affected.Clear();

        if (affectedCells != null)
        {
            foreach (var cell in affectedCells)
            {
                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) continue;
                var tileAtCell = board.Tiles[cell.x, cell.y];
                if (tileAtCell == null) continue;
                affected.Add(tileAtCell);
            }
        }

        foreach (var tile in affected)
        {
            if (tile == null) continue;
            ctx.Affected.Add(tile);
            SpecialCellUtils.MarkAffectedCell(ctx, tile, board);
            if (tile.GetSpecial() == TileSpecial.None) continue;

            if (tile.GetSpecial() == TileSpecial.LineV)
            {
                hasAnySpecialActivation = true;
                lineVSpecial.Execute(new LineVExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (set, delays) => visualService.FireOverrideOverrideSpecialVisuals(set, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });
            }
            else if (tile.GetSpecial() == TileSpecial.LineH)
            {
                hasAnySpecialActivation = true;
                lineHSpecial.Execute(new LineHExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (set, delays) => visualService.FireOverrideOverrideSpecialVisuals(set, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });
            }
            else if (tile.GetSpecial() == TileSpecial.PulseCore)
            {
                hasAnySpecialActivation = true;
                pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (set, delays) => visualService.FireOverrideOverrideSpecialVisuals(set, delays),
                    EnqueueChainSpecials = resolution => queueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution)
                });
            }
            else if (tile.GetSpecial() == TileSpecial.PatchBot)
            {
                hasAnySpecialActivation = true;
                patchBotSpecial.Execute(new PatchBotExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    PatchbotService = patchbotComboService,
                    VisualService = visualService,
                    Effects = effectOrchestrator,
                    ActivateSpecial = dispatcher.ApplySpecialActivation,
                    ProcessFanout = fanoutCtx => fanoutService.ProcessFanout(fanoutCtx),
                    CleanupImplantedTiles = cleanupCtx => implantService.CleanupImplantedTiles(cleanupCtx),
                    FireOverrideOverrideSpecialVisuals = (set, delays) => visualService.FireOverrideOverrideSpecialVisuals(set, delays)
                });
            }
            else
            {
                queueProcessor.EnqueueActivation(ctx, tile, null);
            }
        }

        if (lightningVisualTargets != null)
            ctx.LightningVisualTargets.Clear();
        if (lightningLineStrikes != null)
            ctx.LightningLineStrikes.Clear();

        if (ctx.Queue.Count > 0)
        {
            hasAnySpecialActivation = true;
            queueProcessor.ProcessQueue(ctx);
        }

        hasLineActivation = ctx.HasLineActivation;

        if (lightningVisualTargets != null)
            foreach (var t in ctx.LightningVisualTargets) lightningVisualTargets.Add(t);
        if (lightningLineStrikes != null)
            lightningLineStrikes.AddRange(ctx.LightningLineStrikes);

        foreach (var tile in ctx.Affected) affected.Add(tile);

        board.IsSpecialActivationPhase = previousSpecialPhase;
        ctx.AffectedCells = savedAffectedCells;
    }

    public TileView ApplyCreatedSpecial(TileView winner, TileSpecial special)
    {
        if (winner == null) return null;
        if (special == TileSpecial.None) return null;

        if (winner.GetSpecial() != TileSpecial.None)
            return winner;

        if (special == TileSpecial.SystemOverride)
        {
            // Önce visual update'i ertele, override base type'ı set et,
            // sonra RefreshIcon çağır — böylece hasOverrideBaseType = true
            // olduğunda doğru renk ikonu seçilir.
            winner.SetSpecial(special, deferVisualUpdate: true);
            winner.SetOverrideBaseType(winner.GetTileType());
            winner.RefreshIcon();
        }
        else
        {
            winner.SetSpecial(special);
        }

        SpecialCellUtils.SyncAfterSpecialChange(board, winner);
        board.RefreshTileObstacleVisual(winner);

        return winner;
    }

    private bool IsPureSoloPulsePresentationCandidate(HashSet<TileView> affected, bool suppressPulseImpact)
    {
        if (suppressPulseImpact || affected == null || affected.Count == 0) return false;
        if (ctx.HasLineActivation) return false;
        if (ctx.LightningLineStrikes != null && ctx.LightningLineStrikes.Count > 0) return false;
        if (ctx.LightningVisualTargets != null && ctx.LightningVisualTargets.Count > 0) return false;
        if (ctx.OverrideForceDefaultClearAnim) return false;
        if (ctx.OverrideSuppressPerTileClearVfx) return false;
        if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0) return false;
        if (ctx.OverrideDeferredPulseExplosions != null && ctx.OverrideDeferredPulseExplosions.Count > 0) return false;
        if (ctx.OverrideDeferredPatchBotDashes != null && ctx.OverrideDeferredPatchBotDashes.Count > 0) return false;
        if (ctx.PendingOverrideImplants != null && ctx.PendingOverrideImplants.Count > 0) return false;

        int specialCount = 0;
        bool hasNonPulseSpecial = false;
        foreach (var tile in affected)
        {
            if (tile == null) continue;
            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None) continue;
            specialCount++;
            if (special != TileSpecial.PulseCore)
            {
                hasNonPulseSpecial = true;
                break;
            }
        }

        return !(hasNonPulseSpecial || specialCount > 1);
    }

    private bool IsPureSoloLinePresentationCandidate()
    {
        if (!ctx.HasLineActivation) return false;
        if (ctx.LightningLineStrikes == null || ctx.LightningLineStrikes.Count == 0) return false;
        if (ctx.LightningVisualTargets == null || ctx.LightningVisualTargets.Count == 0) return false;
        if (ctx.OverrideForceDefaultClearAnim) return false;
        if (ctx.OverrideSuppressPerTileClearVfx) return false;
        if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0) return false;
        if (ctx.OverrideDeferredPulseExplosions != null && ctx.OverrideDeferredPulseExplosions.Count > 0) return false;
        if (ctx.OverrideDeferredPatchBotDashes != null && ctx.OverrideDeferredPatchBotDashes.Count > 0) return false;
        if (ctx.PendingOverrideImplants != null && ctx.PendingOverrideImplants.Count > 0) return false;
        // Deferred line-hit Override'lar ArrivalTrigger'larını MatchClearAction üzerinden ateşler;
        // bunlar yalnızca ClearMatchesAnimated (lightning) yolunda okunur. PresentationPlan yoluna
        // girersek erken yield break ile trigger'lar düşer → takılı Override. O yüzden bu durumda
        // pure-solo-line presentation plan'ı kullanma.
        if (ctx.DeferredLineHitOverrideCells != null && ctx.DeferredLineHitOverrideCells.Count > 0) return false;

        int specialCount = 0;
        bool hasNonLineSpecial = false;

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None) continue;
            specialCount++;
            bool isLine = special == TileSpecial.LineH || special == TileSpecial.LineV;
            if (!isLine)
            {
                hasNonLineSpecial = true;
                break;
            }
        }

        return !(hasNonLineSpecial || specialCount > 1);
    }

    private bool IsPureSoloOverridePresentationCandidate()
    {
        if (ctx == null) return false;
        if (ctx.HasLineActivation) return false;
        if (ctx.LightningLineStrikes != null && ctx.LightningLineStrikes.Count > 0) return false;
        if (ctx.LightningVisualTargets != null && ctx.LightningVisualTargets.Count > 0) return false;
        if (ctx.OverrideDeferredPulseExplosions != null && ctx.OverrideDeferredPulseExplosions.Count > 0) return false;
        if (ctx.OverrideDeferredPatchBotDashes != null && ctx.OverrideDeferredPatchBotDashes.Count > 0) return false;
        if (ctx.PendingOverrideImplants != null && ctx.PendingOverrideImplants.Count > 0) return false;

        int specialCount = 0;
        bool hasNonOverrideSpecial = false;
        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None) continue;
            specialCount++;
            if (special != TileSpecial.SystemOverride)
            {
                hasNonOverrideSpecial = true;
                break;
            }
        }

        return !(hasNonOverrideSpecial || specialCount > 1);
    }

    private bool IsPureSoloPatchBotPresentationCandidate()
    {
        if (ctx == null) return false;
        if (ctx.HasLineActivation) return false;
        if (ctx.LightningLineStrikes != null && ctx.LightningLineStrikes.Count > 0) return false;
        if (ctx.LightningVisualTargets != null && ctx.LightningVisualTargets.Count > 0) return false;
        if (ctx.OverrideForceDefaultClearAnim) return false;
        if (ctx.OverrideSuppressPerTileClearVfx) return false;
        if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0) return false;
        if (ctx.OverrideDeferredPulseExplosions != null && ctx.OverrideDeferredPulseExplosions.Count > 0) return false;
        if (ctx.OverrideDeferredPatchBotDashes != null && ctx.OverrideDeferredPatchBotDashes.Count > 0) return false;
        if (ctx.PendingOverrideImplants != null && ctx.PendingOverrideImplants.Count > 0) return false;

        int specialCount = 0;
        bool hasNonPatchBotSpecial = false;
        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None) continue;
            specialCount++;
            if (special != TileSpecial.PatchBot)
            {
                hasNonPatchBotSpecial = true;
                break;
            }
        }

        return !(hasNonPatchBotSpecial || specialCount > 1);
    }

    private ClearPresentationPlan BuildPatchBotPresentationPlanIfNeeded()
    {
        if (!IsPureSoloPatchBotPresentationCandidate()) return null;

        List<TileView> targetTiles = new List<TileView>();
        List<Vector2Int> targetCells = new List<Vector2Int>();

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (targetTiles.Count == 0) return null;

        TileView originTile = null;
        Vector2Int? originCell = null;

        foreach (var tile in targetTiles)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() == TileSpecial.PatchBot)
            {
                originTile = tile;
                originCell = new Vector2Int(tile.X, tile.Y);
                break;
            }
        }

        if (originTile == null || !originCell.HasValue) return null;

        var target = patchbotComboService.FindTarget(originTile, null, null);
        if (!target.hasCell) return null;

        TileView targetTile = target.tile;
        Vector2Int targetCell = new Vector2Int(target.x, target.y);

        if (targetTile != null && !targetTiles.Contains(targetTile))
        {
            targetTiles.Add(targetTile);
            targetCells.Add(targetCell);
        }

        var plan = new ClearPresentationPlan();
        plan.DoBoardShake = true;
        plan.IncludeAdjacentOverTileBlockerDamage = false;
        plan.ObstacleHitContext = ObstacleHitContext.SpecialActivation;

        plan.Effects.Add(new PatchBotDashEffectDescriptor(
            targetTiles,
            targetCells,
            originTile,
            originCell,
            targetTile,
            targetCell
        ));

        foreach (var tile in targetTiles)
        {
            if (tile != null)
                plan.FinalClearTiles.Add(tile);
        }

        return plan;
    }

    private ClearPresentationPlan BuildOverridePresentationPlanIfNeeded()
    {
        if (!IsPureSoloOverridePresentationCandidate()) return null;

        List<TileView> targetTiles = new List<TileView>();
        List<Vector2Int> targetCells = new List<Vector2Int>();

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (targetTiles.Count == 0) return null;

        TileView originTile = null;
        Vector2Int? originCell = null;

        foreach (var tile in targetTiles)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() == TileSpecial.SystemOverride)
            {
                originTile = tile;
                originCell = new Vector2Int(tile.X, tile.Y);
                break;
            }
        }

        var delayMap = ctx.OverrideRadialClearDelays != null
            ? new Dictionary<TileView, float>(ctx.OverrideRadialClearDelays)
            : new Dictionary<TileView, float>();

        var plan = new ClearPresentationPlan();
        plan.DoBoardShake = true;
        plan.IncludeAdjacentOverTileBlockerDamage = false;
        plan.ObstacleHitContext = ObstacleHitContext.SpecialActivation;

        plan.Effects.Add(new OverrideRadialEffectDescriptor(
            targetTiles,
            targetCells,
            delayMap,
            originTile,
            originCell));

        foreach (var tile in targetTiles)
            plan.FinalClearTiles.Add(tile);

        return plan;
    }

    private ClearPresentationPlan BuildPulsePresentationPlanIfNeeded(HashSet<TileView> affected, HashSet<TileView> processedViews, bool suppressPulseImpact)
    {
        if (!IsPureSoloPulsePresentationCandidate(affected, suppressPulseImpact))
            return null;

        Dictionary<TileView, float> stagger = BuildPulseStagger(affected, processedViews, suppressPulseImpact);
        if (stagger == null || stagger.Count == 0)
            return null;

        List<TileView> targetTiles = new List<TileView>();
        List<Vector2Int> targetCells = new List<Vector2Int>();

        TileView centerTile = null;
        float bestDelay = float.MaxValue;

        foreach (var tile in affected)
        {
            if (tile == null) continue;

            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));

            float delay;
            if (stagger.TryGetValue(tile, out delay))
            {
                if (delay < bestDelay)
                {
                    bestDelay = delay;
                    centerTile = tile;
                }
            }
        }

        if (targetTiles.Count == 0)
            return null;

        Vector2Int centerCell = centerTile != null
            ? new Vector2Int(centerTile.X, centerTile.Y)
            : new Vector2Int(targetTiles[0].X, targetTiles[0].Y);

        var plan = new ClearPresentationPlan();
        plan.DoBoardShake = true;
        plan.IncludeAdjacentOverTileBlockerDamage = false;
        plan.ObstacleHitContext = ObstacleHitContext.SpecialActivation;

        plan.Effects.Add(new PulseWaveEffectDescriptor(
            targetTiles,
            targetCells,
            stagger,
            board.ApplySpecialChainTempo(board.PulseImpactAnimTime),
            centerCell
        ));

        foreach (var tile in targetTiles)
            plan.FinalClearTiles.Add(tile);

        return plan;
    }

    private ClearPresentationPlan BuildLinePresentationPlanIfNeeded()
    {
        if (!IsPureSoloLinePresentationCandidate())
            return null;

        List<TileView> targetTiles = new List<TileView>();
        List<Vector2Int> targetCells = new List<Vector2Int>();

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (targetTiles.Count == 0)
            return null;

        IList<LightningLineStrike> strikes = new List<LightningLineStrike>();
        for (int i = 0; i < ctx.LightningLineStrikes.Count; i++)
            strikes.Add(ctx.LightningLineStrikes[i]);

        TileView originTile = null;
        Vector2Int? originCell = null;

        foreach (var tile in targetTiles)
        {
            if (tile == null) continue;

            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.LineH || special == TileSpecial.LineV)
            {
                originTile = tile;
                originCell = new Vector2Int(tile.X, tile.Y);
                break;
            }
        }

        var plan = new ClearPresentationPlan();
        plan.DoBoardShake = true;
        plan.IncludeAdjacentOverTileBlockerDamage = false;
        plan.ObstacleHitContext = ObstacleHitContext.SpecialActivation;

        plan.Effects.Add(new LineSweepEffectDescriptor(
            targetTiles,
            targetCells,
            strikes,
            originTile,
            originCell
        ));

        foreach (var tile in targetTiles)
            plan.FinalClearTiles.Add(tile);

        return plan;
    }

    private MatchClearAction BuildMatchClearAction(bool suppressPulseImpact, bool suppressPerTileClearVfx)
    {
        HashSet<TileView> processedViews = new HashSet<TileView>();
        foreach (var pos in ctx.Processed)
        {
            if (board.Tiles[pos.x, pos.y] != null)
                processedViews.Add(board.Tiles[pos.x, pos.y]);
        }

        ClearPresentationPlan presentationPlan =
            BuildPulsePresentationPlanIfNeeded(ctx.Affected, processedViews, suppressPulseImpact);
        if (presentationPlan == null)
            presentationPlan = BuildLinePresentationPlanIfNeeded();
        if (presentationPlan == null)
            presentationPlan = BuildOverridePresentationPlanIfNeeded();
        // DISABLED: PatchBotSpecial now manages its own dash via EnqueueDash+onArrived.
        // The legacy PatchBotDashEffectPlayer path would conflict (flush the dash queue,
        // never decrement ActiveBackgroundJobs → deadlock in ResolveBoard).
        // if (presentationPlan == null)
        //     presentationPlan = BuildPatchBotPresentationPlanIfNeeded();

        Dictionary<TileView, float> stagger = presentationPlan != null
            ? null
            : BuildPulseStagger(ctx.Affected, processedViews, suppressPulseImpact);

        bool linePresentationOwnsVisuals =
            presentationPlan != null
            && presentationPlan.Effects != null
            && presentationPlan.Effects.Count > 0
            && presentationPlan.Effects[0] is LineSweepEffectDescriptor;

        bool overridePresentationOwnsVisuals =
            presentationPlan != null
            && presentationPlan.Effects != null
            && presentationPlan.Effects.Count > 0
            && presentationPlan.Effects[0] is OverrideRadialEffectDescriptor;

        bool patchBotPresentationOwnsVisuals =
            presentationPlan != null
            && presentationPlan.Effects != null
            && presentationPlan.Effects.Count > 0
            && presentationPlan.Effects[0] is PatchBotDashEffectDescriptor;

        var animationMode = (ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim && !linePresentationOwnsVisuals)
            ? ClearAnimationMode.LightningStrike
            : ClearAnimationMode.Default;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            staggerDelays: stagger,
            staggerAnimTime: board.ApplySpecialChainTempo(board.PulseImpactAnimTime),
            animationMode: animationMode,
            affectedCells: ctx.AffectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: linePresentationOwnsVisuals ? null : ctx.LightningVisualTargets,
            lightningLineStrikes: linePresentationOwnsVisuals ? null : ctx.LightningLineStrikes,
            suppressPerTileClearVfx: (suppressPerTileClearVfx || ctx.OverrideSuppressPerTileClearVfx),
            perTileClearDelays: (overridePresentationOwnsVisuals || patchBotPresentationOwnsVisuals) ? null : ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: presentationPlan);
    }

    private void DrainDeferredPulseComboOverrides(List<BoardAction> actions)
    {
        if (ctx.DeferredPulseComboOverrideCells == null || ctx.DeferredPulseComboOverrideCells.Count == 0)
            return;

        MatchClearAction sweepAction = null;
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i] is MatchClearAction mca)
            {
                sweepAction = mca;
                break;
            }
        }

        var deferred = new List<Vector2Int>(ctx.DeferredPulseComboOverrideCells);
        ctx.DeferredPulseComboOverrideCells.Clear();

        foreach (var cell in deferred)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            ctx.Processed.Remove(cell);
            ctx.Queued.Remove(cell);

            var overrideActions = ExecuteSpecialActions(ctx, tile, null);
            if (overrideActions == null || overrideActions.Count == 0)
                continue;

            if (sweepAction != null)
            {
                sweepAction.RemoveFromMatches(tile);
                var capturedActions = new List<BoardAction>(overrideActions);
                var capturedCell = cell;
                sweepAction.AddArrivalTrigger(capturedCell, () =>
                    board.StartImmediateActionSequence(capturedActions));
            }
            else
            {
                actions.AddRange(overrideActions);
            }
        }
    }
    private void TraceSpecialChain(string stage, TileView a, TileView b)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!board.EnableSpecialChainTrace) return;
        var sb = new System.Text.StringBuilder();
        sb.Append("[SpecialChainTrace] ").Append(stage);
        if (a != null) sb.Append(" A=").Append(a.GetSpecial()).Append("@").Append(a.X).Append(",").Append(a.Y);
        if (b != null) sb.Append(" B=").Append(b.GetSpecial()).Append("@").Append(b.X).Append(",").Append(b.Y);
        sb.Append(" processed=").Append(ctx.Processed.Count);
        sb.Append(" affected=").Append(ctx.Affected.Count);
        Debug.Log(sb.ToString());
#endif
    }

    // Processes deferred Override cells hit by a line sweep.
    // Finds the sweep's MatchClearAction in the actions list, removes each
    // Override tile from its matches (Override clears itself), and registers
    // an ArrivalTrigger so Override fires exactly when the sweep reaches it.
    private void DrainDeferredLineOverrides(List<BoardAction> actions)
    {
        int deferredLineCount = ctx.DeferredLineHitOverrideCells != null ? ctx.DeferredLineHitOverrideCells.Count : 0;
        Debug.Log($"[DrainDeferredLineOverrides] start count={deferredLineCount}");
        if (ctx.DeferredLineHitOverrideCells == null || ctx.DeferredLineHitOverrideCells.Count == 0)
            return;

        MatchClearAction sweepAction = null;
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i] is MatchClearAction mca)
            {
                sweepAction = mca;
                break;
            }
        }

        var deferred = new List<Vector2Int>(ctx.DeferredLineHitOverrideCells);
        ctx.DeferredLineHitOverrideCells.Clear();

        foreach (var cell in deferred)
        {
            Debug.Log($"[DrainDeferredLineOverrides] processing cell={cell}");
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            ctx.Processed.Remove(cell);
            ctx.Queued.Remove(cell);

            var overrideActions = ExecuteSpecialActions(ctx, tile, null);
            if (overrideActions == null || overrideActions.Count == 0)
                continue;

            if (sweepAction != null)
            {
                sweepAction.RemoveFromMatches(tile);
                var capturedActions = new List<BoardAction>(overrideActions);
                var capturedCell = cell;
                sweepAction.AddArrivalTrigger(capturedCell, () =>
                    board.StartImmediateActionSequence(capturedActions));
            }
            else
            {
                board.StartImmediateActionSequence(overrideActions);
            }
        }
    }

    public readonly struct SpecialActivation
    {
        public readonly Vector2Int cell;
        public readonly Vector2Int? partnerCell;

        public SpecialActivation(Vector2Int cell, Vector2Int? partnerCell)
        {
            this.cell = cell;
            this.partnerCell = partnerCell;
        }
    }

    private static void RestoreLineVisualState(
        ResolutionContext ctx,
        bool savedHasLineActivation,
        HashSet<TileView> savedLightningTargets,
        List<LightningLineStrike> savedLightningStrikes)
    {
        ctx.HasLineActivation = savedHasLineActivation;

        ctx.LightningVisualTargets.Clear();
        foreach (var tile in savedLightningTargets)
            ctx.LightningVisualTargets.Add(tile);

        ctx.LightningLineStrikes.Clear();
        ctx.LightningLineStrikes.AddRange(savedLightningStrikes);
    }

    private Dictionary<TileView, float> BuildPulseStagger(HashSet<TileView> affected, HashSet<TileView> processedViews, bool suppressPulseImpact)
    {
        if (suppressPulseImpact)
            return null;

        return pulseCoreImpactService.BuildStaggerDelays(affected, processedViews);
    }
}