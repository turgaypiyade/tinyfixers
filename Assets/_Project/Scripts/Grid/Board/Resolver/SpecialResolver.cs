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
    private readonly PulseCoreSpecial pulseCoreSpecial = new();
    private readonly PatchBotSpecial patchBotSpecial = new();
    private readonly LineHPatchBotCombo lineHPatchBotCombo = new();
    private readonly LineVPatchBotCombo lineVPatchBotCombo = new();
    private readonly PulseCorePatchBotCombo pulseCorePatchBotCombo = new(25);
    private readonly OverrideSpecial overrideSpecial = new();
    private readonly PatchBotCombo patchBotCombo = new();
    private readonly OverrideSpecializedCombo overrideSpecializedCombo = new();
    private readonly OverrideOverrideCombo overrideOverrideCombo = new();
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
                ProcessQueue = resolution => queueProcessor.ProcessQueue(resolution),
                DrainDeferredOverrides = (deferCtx, acts) => DrainDeferredPulseComboOverrides(deferCtx, acts),
                // DiveBurst: varıştaki pulse patlaması tek motordan — alandaki special'lar
                // (Override dahil) arrival'da gerçek sınıfıyla tetiklenir, deferral yok.
                BuildPulseBurstChain = cells => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    virtualPulseBurstCenters: cells)
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
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                // DiveBurst: varıştaki LineV beam'i tek motordan (sanal sweep, deferral yok).
                BuildLineBurstChain = cell => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    virtualLineSweeps: new List<LightningLineStrike> { new LightningLineStrike(cell, false) })
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
                ActivateSpecial = dispatcher.ApplySpecialActivation,
                // DiveBurst: varıştaki LineH beam'i tek motordan (sanal sweep, deferral yok).
                BuildLineBurstChain = cell => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    virtualLineSweeps: new List<LightningLineStrike> { new LightningLineStrike(cell, true) })
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

            // Pulse+Pulse artık TEK gerçek-yayılım motorundan (SpecialChainRunner): merkez=a,
            // yarıçap 4 tek patlama; alandaki special'lar zincirlenir (override resolveOtherSpecial
            // ile ANINDA patlar — eski DeferredPulseComboOverrideCells deferral'ı yok). İkinci
            // pulse (b) consume edilir → tek patlama.
            effectOrchestrator.EmitComboTriggered(TileSpecial.PulseCore, TileSpecial.PulseCore, new Vector2Int(a.X, a.Y));
            actions.Add(new SpecialChainRunner(
                board,
                new List<TileView> { a },
                3,                              // Pulse+Pulse yarıçapı = 7x7 (2*3+1)
                board.PulseChainCatchOverlap,
                ResolveOtherSpecialInline,
                consumeTiles: new List<TileView> { a, b }));

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

            // Önce orbit intro (ghost iki ikon Y-orbit + şimşek bağ), 0.75 sn — sadece gösteri.
            var pulseTile = originalSaIsPulse ? a : b;
            var lineTile = originalSaIsPulse ? b : a;
            actions.Add(new LineVHPulseCoreComboAction(board, lineTile, pulseTile, 0.75f));

            // Line+Pulse artık TEK gerçek-yayılım motorundan (SpecialChainRunner) geçer:
            // fused 3x3 cross süpürülür, yol üstündeki her special VARDIĞI AN gerçek sınıfıyla
            // (arrival sub-chain, paralel) tetiklenir — override dahil. Eski bespoke beam +
            // DeferredLineHitOverrideCells ("override bazen patlamıyor" bug'ı) artık yok.
            var crossCenter = new Vector2Int(a.X, a.Y);
            actions.Add(new SpecialChainRunner(
                board,
                new List<TileView>(),
                PulseChainAreaHalf,
                board.PulseChainCatchOverlap,
                ResolveOtherSpecialInline,
                root: null,
                linePulseCrossCenter: crossCenter,
                consumeTiles: new List<TileView> { a, b }));

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
                    visualService.FireOverrideOverrideSpecialVisuals(affected, delays),
                // Board'daki karışık special'lar tek motordan: hepsi anchor'lı + paralel sub-chain.
                BuildMixedChainForCells = cells => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    simultaneousMixedCells: cells)
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
                EnqueueCascadeBetweenBatches = true,
                // Implant edilen pulse'lar artık tek motordan (SpecialChainRunner) patlar: hepsi
                // AYNI ANDA (yer değiştirme yok), zincirleri arrival ile = tüm combolarla AYNI dispatch.
                BuildPulseChainForCells = cells => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    simultaneousPulseCells: cells),
                // Implant line'lar da tek motordan: hepsi aynı anda süpürür, zincir arrival'da.
                BuildLineChainForCells = cells => new SpecialChainRunner(
                    board,
                    new List<TileView>(),
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    simultaneousLineCells: cells)
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

            // Line+Line artık TEK gerçek-yayılım motorundan (SpecialChainRunner): "+" haçı
            // (crossHalf=0 → 1 satır + 1 sütun), merkez = a. Yol üstündeki special'lar Line+Pulse
            // ile AYNI dispatch'e düşer: pulse→ExplodePulse, line→SweepLine, override→
            // overrideSpecial.Execute (ANINDA, deferral yok).
            var crossCenter = new Vector2Int(a.X, a.Y);
            actions.Add(new SpecialChainRunner(
                board,
                new List<TileView>(),
                PulseChainAreaHalf,
                board.PulseChainCatchOverlap,
                ResolveOtherSpecialInline,
                root: null,
                linePulseCrossCenter: crossCenter,
                consumeTiles: new List<TileView> { a, b },
                crossHalf: 0));

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

            // FAZ 1: tekli swap aktivasyonu da tek motordan (SpecialChainRunner).
            // Swap sırasında normal taraftan oluşan yeni special hücreleri korunur:
            // externalProtectedCells = ProcessSwap'tan gelen kesin liste (winner herhangi
            // bir tile olabilir), normalPartner fallback = ek güvence. Motor bu hücrelere
            // hiç dokunmaz (clear yok, zincir yok).
            if (originalSpecial == TileSpecial.LineV || originalSpecial == TileSpecial.LineH ||
                originalSpecial == TileSpecial.PulseCore || originalSpecial == TileSpecial.PatchBot)
            {
                HashSet<Vector2Int> protectedCells = externalProtectedCells != null
                    ? new HashSet<Vector2Int>(externalProtectedCells)
                    : null;

                var normalPartner = aOriginallySpecial ? b : a;
                if (normalPartner != null && normalPartner.GetSpecial() != TileSpecial.None)
                    (protectedCells ??= new HashSet<Vector2Int>()).Add(new Vector2Int(normalPartner.X, normalPartner.Y));

                actions.Add(new SpecialChainRunner(
                    board,
                    new List<TileView> { specialTile },
                    PulseChainAreaHalf,
                    board.PulseChainCatchOverlap,
                    ResolveOtherSpecialInline,
                    protectedCells: protectedCells));
                TraceSpecialChain($"ResolveSpecialSwap.{originalSpecial}", a, b);
                board.IsSpecialActivationPhase = false;
                return actions;
            }
        }

        return actions;
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

        // FAZ 1: 5 solo special'ın tamamı tek gerçek-yayılım motorundan (SpecialChainRunner).
        // Line/Pulse natively süpürülür/patlar; PatchBot ve Override zincir düğümü olarak
        // GERÇEK sınıflarıyla tetiklenir (ResolveOtherSpecialInline → Execute + fanout
        // delegeleri = Yol A töreni). Eski Execute(FinalizeAtEnd=true) + deferred-drain
        // solo yolları emekli.
        if (spec == TileSpecial.LineV || spec == TileSpecial.LineH ||
            spec == TileSpecial.PulseCore || spec == TileSpecial.PatchBot ||
            spec == TileSpecial.SystemOverride)
        {
            actions.Add(new SpecialChainRunner(
                board,
                new List<TileView> { specialTile },
                PulseChainAreaHalf,
                board.PulseChainCatchOverlap,
                ResolveOtherSpecialInline));
            TraceSpecialChain($"ResolveSpecialSolo.{spec}", specialTile, null);
        }

        board.IsSpecialActivationPhase = false;
        return actions;
    }

    // Tekli PulseCore patlama alanı yarıçapı (affectedCellCount=25 → 5x5 → half=2).
    private const int PulseChainAreaHalf = 2;

    // SpecialChainRunner'ın playback'te pulse-olmayan bir special'ı (Line/PatchBot)
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

            case TileSpecial.SystemOverride:
                // Partner'sız (zincirde tetiklenen) Override: base type'ına göre fanout
                // → o tipteki taşları temizler (implant yok, NormalSelectionPulse=true).
                return overrideSpecial.Execute(new OverrideExecutionRuntime
                {
                    Board = board, Context = scoped, Origin = tile, Partner = null, FinalizeAtEnd = true,
                    ProcessFanout = c => fanoutService.ProcessFanout(c),
                    CleanupImplantedTiles = c => implantService.CleanupImplantedTiles(c),
                    FireOverrideOverrideSpecialVisuals = (a, d) => visualService.FireOverrideOverrideSpecialVisuals(a, d)
                }).Actions;

            default:
                return null; // Desteklenmeyen tip → guard zaten engeller.
        }
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

    // Havada taşınan pulse (PatchBot+PulseCore) varışta KENDİ arrival context'iyle patlar;
    // alandaki SystemOverride'lar o context'te ertelenir (IsPulseCoreActive). Airborne
    // action bu overload'ı delegate ile çağırıp ertelenenleri boşaltır — yoksa Override
    // hiç tetiklenmeden silinir.
    internal void DrainDeferredPulseComboOverrides(ResolutionContext targetCtx, List<BoardAction> actions)
    {
        if (targetCtx?.DeferredPulseComboOverrideCells == null || targetCtx.DeferredPulseComboOverrideCells.Count == 0)
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

        var deferred = new List<Vector2Int>(targetCtx.DeferredPulseComboOverrideCells);
        targetCtx.DeferredPulseComboOverrideCells.Clear();

        foreach (var cell in deferred)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            targetCtx.Processed.Remove(cell);
            targetCtx.Queued.Remove(cell);

            var overrideActions = ExecuteSpecialActions(targetCtx, tile, null);
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
}