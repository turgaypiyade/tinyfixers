using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dispatches special activations and combo effects.
///
/// Combo logic is fully delegated to registry-based IComboBehavior/IComboExecutor classes.
/// Solo activation for Line/Pulse goes through ISpecialBehavior registry.
/// SystemOverride and PatchBot solo activations remain here (they need ResolutionContext side-effects).
/// </summary>
public class SpecialBehaviorDispatcher
{
    private readonly BoardController board;
    private readonly PatchbotComboService patchbotComboService;
    private readonly SpecialVisualService visualService;

    // Set after construction (circular dep)
    internal ActivationQueueProcessor QueueProcessor;

    // Reusable execution context — populated per-combo
    private readonly ComboExecutionContext execCtx = new();

    public SpecialBehaviorDispatcher(
        BoardController board,
        PatchbotComboService patchbotComboService,
        SpecialVisualService visualService)
    {
        this.board = board;
        this.patchbotComboService = patchbotComboService;
        this.visualService = visualService;
    }

    // ═══════════════════════════════════════════════
    //  Combo Dispatch — fully registry-based
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Finds and executes the combo for two specials. All combo logic lives
    /// in IComboBehavior/IComboExecutor classes registered in SpecialBehaviorRegistry.
    /// </summary>
    public void ApplyComboEffect(ResolutionContext ctx, TileView a, TileView b, TileSpecial sa, TileSpecial sb)
    {
        var combo = board.SpecialBehaviors.FindCombo(sa, sb);
        if (combo == null) return;

        if (combo is IComboExecutor executor)
        {
            PopulateExecCtx(ctx, a, b, sa, sb);
            executor.Execute(execCtx);
        }
        else
        {
            ApplyGenericCombo(ctx, combo, a, b, sa, sb);
        }
    }

    private void ApplyGenericCombo(ResolutionContext ctx, IComboBehavior combo, TileView a, TileView b,
        TileSpecial sa, TileSpecial sb)
    {
        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(a.X, a.Y));

        var cells = combo.CalculateAffectedCells(board, a.X, a.Y, sa, sb);
        foreach (var c in cells)
        {
            SpecialCellUtils.MarkAffectedCell(ctx, c.x, c.y, board);
            if (board.Tiles[c.x, c.y] != null) ctx.Affected.Add(board.Tiles[c.x, c.y]);
        }

        if (combo is ILightningComboBehavior lb)
        {
            var strikes = lb.GetLineStrikes(a.X, a.Y, sa, sb);
            if (strikes != null)
                ctx.LightningLineStrikes.AddRange(strikes);

            foreach (var c in cells)
                if (board.Tiles[c.x, c.y] != null)
                    ctx.LightningVisualTargets.Add(board.Tiles[c.x, c.y]);

            ctx.HasLineActivation = true;
        }
    }

    private void PopulateExecCtx(ResolutionContext ctx, TileView a, TileView b, TileSpecial sa, TileSpecial sb)
    {
        execCtx.Resolution = ctx;
        execCtx.Board = board;
        execCtx.TileA = a;
        execCtx.TileB = b;
        execCtx.SpecialA = sa;
        execCtx.SpecialB = sb;
        execCtx.VisualService = visualService;
        execCtx.PatchbotService = patchbotComboService;
        execCtx.QueueProcessor = QueueProcessor;
    }

    // ═══════════════════════════════════════════════
    //  Solo Activation Dispatch
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Dispatches a single special activation.
    /// Line/Pulse → registry ISpecialBehavior.
    /// SystemOverride/PatchBot → inline (need ResolutionContext side-effects).
    /// </summary>
    public void ApplySpecialActivation(ResolutionContext ctx, TileView specialTile, TileView partnerTile)
    {
        if (specialTile == null) return;

        var special = specialTile.GetSpecial();
        int ox = specialTile.X;
        int oy = specialTile.Y;

        switch (special)
        {
            case TileSpecial.SystemOverride:
                ActivateSystemOverride(ctx, specialTile, partnerTile, ox, oy);
                break;

            case TileSpecial.PatchBot:
                ActivatePatchBot(ctx, specialTile, partnerTile);
                break;

            default:
                ActivateViaRegistry(ctx, special, ox, oy);
                break;
        }
    }

    // ── SystemOverride solo activation ──

    private void ActivateSystemOverride(ResolutionContext ctx, TileView specialTile, TileView partnerTile, int ox, int oy)
    {
        if (ctx.OverrideFanoutOrigin != null && ctx.OverrideFanoutOrigin != specialTile)
        {
            ctx.Affected.Add(specialTile);
            SpecialCellUtils.MarkAffectedCell(ctx, specialTile, board);
            return;
        }

        TileType type = partnerTile != null ? partnerTile.GetTileType() : specialTile.GetTileType();
        var partnerSpecial = partnerTile != null ? partnerTile.GetSpecial() : TileSpecial.None;

        ctx.OverrideFanoutNormalSelectionPulse = (partnerTile == null) || (partnerSpecial == TileSpecial.None);
        ctx.OverrideFanoutPulseHitCount = 0;
        ctx.OverrideFanoutOrigin = specialTile;

        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(new Vector2Int(ox, oy), TileSpecial.None);
        SpecialCellUtils.CollectAllOfType(ctx.OverrideFanoutTargets, board, type, excludeSpecials: true);
        ctx.OverrideForceDefaultClearAnim = true;
        ctx.OverrideSuppressPerTileClearVfx = false;
        SpecialCellUtils.AddAllOfType(ctx.Affected, ctx, board, type, excludeSpecials: true);
    }

    // ── PatchBot solo / teleport activation ──

    private void ActivatePatchBot(ResolutionContext ctx, TileView specialTile, TileView partnerTile)
    {
        if (partnerTile != null)
        {
            if (ApplyPatchBotTeleportHit(ctx, specialTile, partnerTile))
                ctx.HasLineActivation = true;
        }
        else
        {
            ApplyPatchBotSoloHit(ctx, specialTile);
        }
    }

    private void ApplyPatchBotSoloHit(ResolutionContext ctx, TileView patchBotTile)
    {
        if (patchBotTile == null) return;

        ctx.Affected.Add(patchBotTile);
        SpecialCellUtils.MarkAffectedCell(ctx, patchBotTile, board);

        var target = patchbotComboService.FindTarget(patchBotTile, null, null);
        if (!target.hasCell) return;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
        visualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(target.x, target.y);
        var dataMatches = new HashSet<TileData>();
        patchbotComboService.ResolveTargetImpact(dataMatches, target.x, target.y, hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(ctx, x, y, board),
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));

        foreach (var data in dataMatches)
            if (board.Tiles[data.X, data.Y] != null) ctx.Affected.Add(board.Tiles[data.X, data.Y]);
    }

    private bool ApplyPatchBotTeleportHit(ResolutionContext ctx, TileView patchBotTile, TileView partnerTile)
    {
        if (patchBotTile == null || partnerTile == null) return false;

        var target = patchbotComboService.FindTarget(patchBotTile, partnerTile, null);
        if (!target.hasCell) return false;

        patchbotComboService.EnqueueDash(patchBotTile, target.x, target.y);
        visualService.PlayTeleportMarkers(patchBotTile, target.x, target.y);

        bool partnerIsSpecial = partnerTile.GetSpecial() != TileSpecial.None;
        if (partnerIsSpecial)
            return TriggerPartnerEffectAt(ctx, patchBotTile, partnerTile, target.x, target.y);

        ApplyPatchBotTeleportToCell(ctx, patchBotTile, partnerTile, target.x, target.y);
        return false;
    }

  /*  private void ApplyPatchBotTeleportToCell(ResolutionContext ctx, TileView patchBotTile, TileView partnerTile,
        int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;
        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        if (board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        patchbotComboService.ConsumeSwapSource(ctx.Affected, patchBotTile, partnerTile,
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));
        var matchDatas = new HashSet<TileData>();
        patchbotComboService.ResolveTargetImpact(matchDatas, targetX, targetY, hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(ctx, x, y, board),
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));
        foreach (var data in matchDatas)
            if (board.Tiles[data.X, data.Y] != null) ctx.Affected.Add(board.Tiles[data.X, data.Y]);
    }*/

    private void ApplyPatchBotTeleportToCell(ResolutionContext ctx, TileView patchBotTile, TileView partnerTile,
        int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;

        bool hasObstacleAtTarget = patchbotComboService.HasObstacleAt(targetX, targetY);
        if (board.Holes[targetX, targetY] && !hasObstacleAtTarget) return;

        // Yeni kural:
        // PatchBot + normal swap'ta normal partner otomatik tüketilmez.
        patchbotComboService.ConsumePatchBotOnly(
            ctx.Affected,
            patchBotTile,
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));

        var matchDatas = new HashSet<TileData>();
        patchbotComboService.ResolveTargetImpact(
            matchDatas,
            targetX,
            targetY,
            hasObstacleAtTarget,
            (x, y) => SpecialCellUtils.MarkAffectedCell(ctx, x, y, board),
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));

        foreach (var data in matchDatas)
            if (board.Tiles[data.X, data.Y] != null)
                ctx.Affected.Add(board.Tiles[data.X, data.Y]);
    }

    /// <summary>
    /// PatchBot partner is a special tile → fire that special's effect at teleport target.
    /// Handles Line partner (lightning), Pulse partner (3×3), SystemOverride partner (conversion).
    /// </summary>
    private bool TriggerPartnerEffectAt(ResolutionContext ctx, TileView patchBotTile, TileView partnerTile,
        int originX, int originY)
    {
        var special = partnerTile.GetSpecial();
        if (special == TileSpecial.None) return false;

        if (special == TileSpecial.LineH || special == TileSpecial.LineV)
        {
            visualService.PlayTeleportMarkers(partnerTile, originX, originY);
            visualService.PlayTransientSpecialVisualAt(partnerTile, originX, originY);

            var cells = board.SpecialBehaviors.CalculateEffect(special, board, originX, originY);
            foreach (var c in cells)
            {
                SpecialCellUtils.MarkAffectedCell(ctx, c.x, c.y, board);
                if (board.Tiles[c.x, c.y] != null) ctx.Affected.Add(board.Tiles[c.x, c.y]);
                if (board.Tiles[c.x, c.y] != null) ctx.LightningVisualTargets.Add(board.Tiles[c.x, c.y]);
            }
            ctx.LightningLineStrikes.Add(new LightningLineStrike(new Vector2Int(originX, originY), special == TileSpecial.LineH));
            return true;
        }

        if (special == TileSpecial.PulseCore)
        {
            visualService.PlayTeleportMarkers(partnerTile, originX, originY);
            visualService.PlayTransientSpecialVisualAt(partnerTile, originX, originY);
            board.PlayPulsePulseExplosionVfxAtCell(originX, originY);
            SpecialCellUtils.AddSquare(ctx.Affected, ctx, board, originX, originY, 2);
            return false;
        }

        if (special == TileSpecial.SystemOverride)
        {
            visualService.PlayTeleportMarkers(partnerTile, originX, originY);
            TriggerSystemOverridePatchBotConversion(ctx, patchBotTile, partnerTile);
        }
        return false;
    }

    private void TriggerSystemOverridePatchBotConversion(ResolutionContext ctx, TileView patchBotTile, TileView systemOverrideTile)
    {
        if (systemOverrideTile == null) return;

        TileType baseType = systemOverrideTile.GetOverrideBaseType(out var storedType)
            ? storedType
            : systemOverrideTile.GetTileType();

        int activationIndex = 0;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;

                var tile = board.Tiles[x, y];
                if (tile == null || tile == patchBotTile || tile == systemOverrideTile) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                tile.SetSpecial(TileSpecial.PatchBot);
                SpecialCellUtils.SyncAfterSpecialChange(board, tile);

                // Override+LineV benzeri his:
                // PatchBot yerleşir, sonra kendi sırası geldiğinde ghost olarak çıkıp source cell'i boşaltır.
                AutoPatchBotTeleportHitAndVanish(ctx, tile, patchBotTile, systemOverrideTile, activationIndex);
                activationIndex++;
            }
        }
    }

    private void AutoPatchBotTeleportHitAndVanish(
        ResolutionContext ctx,
        TileView autoPatchBot,
        TileView patchBotTile,
        TileView systemOverrideTile,
        int activationIndex)
    {
        if (autoPatchBot == null) return;

        ctx.Affected.Add(autoPatchBot);
        SpecialCellUtils.MarkAffectedCell(ctx, autoPatchBot, board);

        var sourceCell = new Vector2Int(autoPatchBot.X, autoPatchBot.Y);
        var sourceType = autoPatchBot.GetTileType();

        var target = patchbotComboService.FindTarget(autoPatchBot, patchBotTile, null, systemOverrideTile);
        if (!target.hasCell) return;

        const float sequentialActivationStep = 0.03f;
        float dashDelay = (ctx.DeferOverrideImplantVisualRefresh ? 0.10f : 0f)
            + Mathf.Max(0, activationIndex) * sequentialActivationStep;

        visualService.FireImmediateDash(
            autoPatchBot.X,
            autoPatchBot.Y,
            target.x,
            target.y,
            dashDelay,
            onDashStart: () =>
            {
                if (autoPatchBot == null) return;

                SpecialVisualService.HideTileVisualForCombo(autoPatchBot);

                if (sourceCell.x < 0 || sourceCell.x >= board.Width || sourceCell.y < 0 || sourceCell.y >= board.Height)
                    return;

                if (board.Tiles[sourceCell.x, sourceCell.y] == autoPatchBot)
                {
                    board.ClearCell(sourceCell.x, sourceCell.y);
                    board.ClearCellVisualOnly(sourceCell, sourceType, autoPatchBot);
                }
            });

        var matchSetData = new HashSet<TileData>();
        patchbotComboService.HitCellOnce(
            matchSetData,
            target.x,
            target.y,
            target.tile,
            (x, y) => SpecialCellUtils.MarkAffectedCell(ctx, x, y, board),
            (tile) => SpecialCellUtils.MarkAffectedCell(ctx, tile, board));

        foreach (var data in matchSetData)
        {
            if (board.Tiles[data.X, data.Y] != null)
                ctx.Affected.Add(board.Tiles[data.X, data.Y]);
        }
    }

    // ── Registry-based activation (Line, Pulse, etc.) ──

    private void ActivateViaRegistry(ResolutionContext ctx, TileSpecial special, int ox, int oy)
    {
        var behavior = board.SpecialBehaviors.Get(special);
        if (behavior == null) return;

        var cells = behavior.CalculateAffectedCells(board, ox, oy);
        foreach (var c in cells)
        {
            SpecialCellUtils.MarkAffectedCell(ctx, c.x, c.y, board);
            if (board.Tiles[c.x, c.y] != null) ctx.Affected.Add(board.Tiles[c.x, c.y]);
        }

        if (behavior is ILightningBehavior lb)
        {
            ctx.HasLineActivation |= lb.HasLineActivation;
            var strikes = lb.GetLineStrikes(ox, oy);
            if (strikes != null) ctx.LightningLineStrikes.AddRange(strikes);

            foreach (var c in behavior.CalculateAffectedCells(board, ox, oy))
                if (board.Tiles[c.x, c.y] != null) ctx.LightningVisualTargets.Add(board.Tiles[c.x, c.y]);
        }
    }
}
