using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates special tile resolution: swap combos, solo activations, chain expansions.
///
/// After refactoring, this class contains NO gameplay logic, NO visual effects,
/// NO queue mechanics — only high-level flow control that delegates to:
///   - ActivationQueueProcessor  → queue management, chain discovery
///   - SpecialBehaviorDispatcher → activation dispatch, combo detection
///   - SpecialFanoutService      → SystemOverride fan-out phase
///   - SpecialImplantService     → pending override implant application
///   - SpecialVisualService      → all VFX/visual methods
///   - SpecialCellUtils          → static board cell helpers
/// </summary>
public class SpecialResolver
{
    private readonly BoardController board;
    private readonly PulseCoreImpactService pulseCoreImpactService;
    private readonly SpecialVisualService visualService;
    private readonly SpecialBehaviorDispatcher dispatcher;
    private readonly ActivationQueueProcessor queueProcessor;
    private readonly SpecialImplantService implantService;
    private readonly SpecialFanoutService fanoutService;

    // Reusable context — reset at the start of each resolution pass
    private readonly ResolutionContext ctx = new();

    public SpecialResolver(BoardController board, MatchFinder matchFinder, BoardAnimator boardAnimator, PulseCoreImpactService pulseCoreImpactService)
    {
        this.board = board;
        this.pulseCoreImpactService = pulseCoreImpactService;

        var patchbotComboService = new PatchbotComboService(board);

        visualService = new SpecialVisualService(board, boardAnimator, patchbotComboService);
        dispatcher = new SpecialBehaviorDispatcher(board, patchbotComboService, visualService);
        queueProcessor = new ActivationQueueProcessor(board, dispatcher);
        implantService = new SpecialImplantService(board, patchbotComboService, visualService, queueProcessor);
        fanoutService = new SpecialFanoutService(board, implantService, queueProcessor, visualService);

        // Resolve circular dependency: dispatcher needs queueProcessor for Override fan-out enqueueing
        dispatcher.QueueProcessor = queueProcessor;
    }

    // ═══════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════

    public List<BoardAction> ResolveSpecialSwap(TileView a, TileView b, TileSpecial originalSa, TileSpecial originalSb)
    {
        var actions = new List<BoardAction>();
        board.ShakeNextClear = true;
        board.LastSwapUserMove = false;
        board.IsSpecialActivationPhase = true;

        // Current board state sadece board üstündeki güncel taşları görmek için.
        TileSpecial currentSa = a.GetSpecial();
        TileSpecial currentSb = b.GetSpecial();

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SpecialResolver] ResolveSpecialSwap: " +
                $"a=({a.X},{a.Y}) current={currentSa} original={originalSa}, " +
                $"b=({b.X},{b.Y}) current={currentSb} original={originalSb}");
    #endif

        bool aOriginallySpecial = originalSa != TileSpecial.None;
        bool bOriginallySpecial = originalSb != TileSpecial.None;
        bool bothOriginallySpecial = aOriginallySpecial && bOriginallySpecial;

        bool originalSaIsLine  = originalSa == TileSpecial.LineH || originalSa == TileSpecial.LineV;
        bool originalSbIsLine  = originalSb == TileSpecial.LineH || originalSb == TileSpecial.LineV;
        bool originalSaIsPulse = originalSa == TileSpecial.PulseCore;
        bool originalSbIsPulse = originalSb == TileSpecial.PulseCore;

        // ÖNEMLİ:
        // PatchBot + normal artık normal partneri otomatik tüketmeyecek.
        bool consumeNormalPartner = bothOriginallySpecial;

        // Görselleri de original swap intent'e göre yönet.
        visualService.HideSwapSourceVisuals(a, b, originalSa, originalSb, consumeNormalPartner);

        ctx.Reset();

        // Pulse + Line erken combo yolu da original intent'e göre karar versin.
        if ((originalSaIsPulse && originalSbIsLine) || (originalSbIsPulse && originalSaIsLine))
        {
            ResolvePulseLineCombo(actions, a, b, originalSa, originalSb);
            board.IsSpecialActivationPhase = false;
            return actions;
        }

        // Initial affected set: sadece başlangıçta special olanlar
        if (aOriginallySpecial)
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

        bool originalSaIsOverride = originalSa == TileSpecial.SystemOverride;
        bool originalSbIsOverride = originalSb == TileSpecial.SystemOverride;
        bool suppressPulseImpactAnimations = (originalSaIsPulse && originalSbIsPulse) || (originalSaIsOverride && originalSbIsOverride);
        bool suppressPerTileClearVfx = (originalSaIsPulse && originalSbIsLine) || (originalSbIsPulse && originalSaIsLine);

        ctx.HasLineActivation = originalSaIsLine || originalSbIsLine;

        // Combo kararı sadece ORIGINAL state ile verilir.
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

            // Tek special + normal swap:
            // - Line/Pulse zaten partner kullanmıyor.
            // - SystemOverride partner type'ını kullanır, o yüzden partner geç.
            // - PatchBot + normal artık partneri combo partner gibi kullanmasın.
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

        if (ctx.OverrideRadialClearDelays != null && ctx.OverrideRadialClearDelays.Count > 0)
            visualService.FireOverrideOverrideSpecialVisuals(ctx.Affected, ctx.OverrideRadialClearDelays);

        actions.Add(BuildMatchClearAction(suppressPulseImpactAnimations, suppressPerTileClearVfx));

        TraceSpecialChain("ResolveSpecialSwap", a, b);
        board.IsSpecialActivationPhase = false;
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

        ctx.Affected.Add(specialTile);
        SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);

        TileSpecial spec = specialTile.GetSpecial();
        ctx.HasLineActivation = spec == TileSpecial.LineH || spec == TileSpecial.LineV;

        // Solo activation: partner = null
        queueProcessor.EnqueueActivation(ctx, specialTile, null);
        queueProcessor.EnqueueChainSpecials(ctx);
        queueProcessor.ProcessQueue(ctx);

        // ── SystemOverride fan-out phase ──
        var fanoutActions = fanoutService.ProcessFanout(ctx, soloSpecialTile: deferHide ? specialTile : null);
        actions.AddRange(fanoutActions);

        // ── Cleanup implanted tiles ──
        implantService.CleanupImplantedTiles(ctx);

        // ── Build final MatchClearAction ──
        actions.Add(BuildMatchClearAction(suppressPulseImpact: false, suppressPerTileClearVfx: false));

        TraceSpecialChain("ResolveSpecialSolo", specialTile, null);
        board.IsSpecialActivationPhase = false;
        return actions;
    }

    /// <summary>
    /// Expands chain reactions for specials already in an affected set.
    /// Used by external callers (e.g. PulseLineCombo chain expansion).
    /// </summary>
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

        // Save/restore outer state — ExpandSpecialChain can be called mid-resolution
        bool previousSpecialPhase = board.IsSpecialActivationPhase;
        var savedAffectedCells = ctx.AffectedCells;

        board.IsSpecialActivationPhase = true;
        ctx.AffectedCells = affectedCells ?? new HashSet<Vector2Int>();

        // Use a temporary context scope for expansion
        ctx.Processed.Clear();
        ctx.Queued.Clear();
        ctx.Queue.Clear();

        // Merge affected into ctx
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
            queueProcessor.EnqueueActivation(ctx, tile, null);
        }

        // Wire lightning tracking to caller's collections
        if (lightningVisualTargets != null)
        {
            ctx.LightningVisualTargets.Clear();
        }
        if (lightningLineStrikes != null)
        {
            ctx.LightningLineStrikes.Clear();
        }

        while (ctx.Queue.Count > 0)
        {
            var activation = ctx.Queue.Dequeue();
            ctx.Queued.Remove(activation.cell);
            if (ctx.Processed.Contains(activation.cell)) continue;

            ctx.Processed.Add(activation.cell);
            hasAnySpecialActivation = true;
            TileView actSpecial = board.Tiles[activation.cell.x, activation.cell.y];
            TileView actPartner = activation.partnerCell.HasValue
                ? board.Tiles[activation.partnerCell.Value.x, activation.partnerCell.Value.y]
                : null;

            dispatcher.ApplySpecialActivation(ctx, actSpecial, actPartner);
            queueProcessor.EnqueueChainSpecials(ctx);
        }

        hasLineActivation = ctx.HasLineActivation;

        // Copy lightning data back to caller
        if (lightningVisualTargets != null)
        {
            foreach (var t in ctx.LightningVisualTargets) lightningVisualTargets.Add(t);
        }
        if (lightningLineStrikes != null)
        {
            lightningLineStrikes.AddRange(ctx.LightningLineStrikes);
        }

        // Merge expanded affected back to caller
        foreach (var tile in ctx.Affected) affected.Add(tile);

        // Restore outer state
        board.IsSpecialActivationPhase = previousSpecialPhase;
        ctx.AffectedCells = savedAffectedCells;
    }

    public TileView ApplyCreatedSpecial(TileView winner, TileSpecial special)
    {
        if (winner == null) return null;
        if (special == TileSpecial.None) return null;

        // Kazanılmış existing special'ı creation ile ezme.
        if (winner.GetSpecial() != TileSpecial.None)
            return winner;

        winner.SetSpecial(special);

        if (special == TileSpecial.SystemOverride)
            winner.SetOverrideBaseType(winner.GetTileType());

        SpecialCellUtils.SyncAfterSpecialChange(board, winner);
        board.RefreshTileObstacleVisual(winner);

        return winner;
    }

    // ═══════════════════════════════════════════════
    //  Private Helpers
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Pulse+Line early path — creates PulseEmitterComboAction and chains specials in the combo area.
    /// </summary>
    private void ResolvePulseLineCombo(List<BoardAction> actions, TileView a, TileView b, TileSpecial sa, TileSpecial sb)
    {
        bool saIsPulse = sa == TileSpecial.PulseCore;
        var center = saIsPulse ? a : b;
        int cx = center.X;
        int cy = center.Y;
        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(cx, cy));

        var pulseAction = board.CreatePulseEmitterComboAction(cx, cy);
        actions.Add(pulseAction);

        // Chain: combo alanındaki special taşları tetikle
        var chainAffected = new HashSet<TileView>();
        var chainCells = new HashSet<Vector2Int>();
        for (int r = cy - 1; r <= cy + 1; r++)
            for (int c = 0; c < board.Width; c++)
            {
                if (r < 0 || r >= board.Height) continue;
                chainCells.Add(new Vector2Int(c, r));
                if (board.Tiles[c, r] != null) chainAffected.Add(board.Tiles[c, r]);
            }
        for (int c = cx - 1; c <= cx + 1; c++)
            for (int r = 0; r < board.Height; r++)
            {
                if (c < 0 || c >= board.Width) continue;
                chainCells.Add(new Vector2Int(c, r));
                if (board.Tiles[c, r] != null) chainAffected.Add(board.Tiles[c, r]);
            }

        bool chainHasLine, chainHasAny;
        var chainLightningTargets = new HashSet<TileView>();
        var chainLightningStrikes = new List<LightningLineStrike>();
        ExpandSpecialChain(chainAffected, chainCells,
            out chainHasLine, out chainHasAny,
            chainLightningTargets, chainLightningStrikes);

        if (chainHasAny && chainAffected.Count > 0)
        {
            var chainMode = chainHasLine
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            actions.Add(new MatchClearAction(
                chainAffected,
                doShake: true,
                animationMode: chainMode,
                affectedCells: chainCells,
                obstacleHitContext: null,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: null,
                lightningOriginCell: null,
                lightningVisualTargets: chainLightningTargets,
                lightningLineStrikes: chainLightningStrikes,
                isSpecialPhase: true
            ));
        }
    }

    /// <summary>
    /// Builds the final MatchClearAction from the current resolution context.
    /// </summary>
    private MatchClearAction BuildMatchClearAction(bool suppressPulseImpact, bool suppressPerTileClearVfx)
    {
        HashSet<TileView> processedViews = new HashSet<TileView>();
        foreach (var pos in ctx.Processed)
            if (board.Tiles[pos.x, pos.y] != null) processedViews.Add(board.Tiles[pos.x, pos.y]);

        Dictionary<TileView, float> stagger = suppressPulseImpact
            ? null
            : pulseCoreImpactService.BuildStaggerDelays(ctx.Affected, processedViews);

        var animationMode = (ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim)
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
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: (suppressPerTileClearVfx || ctx.OverrideSuppressPerTileClearVfx),
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true);
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

    // ── Legacy struct kept for external compatibility ──
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