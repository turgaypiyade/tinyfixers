using System.Collections.Generic;
using UnityEngine;

public class SpecialBehaviorDispatcher
{
    private readonly BoardController board;
    private readonly PatchbotComboService patchbotComboService;
    private readonly SpecialVisualService visualService;
    private readonly SpecialEffectOrchestrator effectOrchestrator;
    private readonly LineVSpecial lineVSpecial = new();
    private readonly LineHSpecial lineHSpecial = new();
    private readonly LineVLineHCombo lineVLineHCombo = new();
    private readonly PulseCoreSpecial pulseCoreSpecial = new();
    private readonly PatchBotSpecial patchBotSpecial = new();
    private readonly LineVHPulseCoreCombo lineVHPulseCoreCombo = new();
    internal ActivationQueueProcessor QueueProcessor;

    private readonly ComboExecutionContext execCtx = new();

    public SpecialBehaviorDispatcher(
        BoardController board,
        PatchbotComboService patchbotComboService,
        SpecialVisualService visualService,
        SpecialEffectOrchestrator effectOrchestrator)
    {
        this.board = board;
        this.patchbotComboService = patchbotComboService;
        this.visualService = visualService;
        this.effectOrchestrator = effectOrchestrator;
    }

    public void ApplyComboEffect(ResolutionContext ctx, TileView a, TileView b, TileSpecial sa, TileSpecial sb)
    {
        if (IsLineCombo(sa, sb))
        {
            //var comboOrigin = sa == TileSpecial.LineV ? a : b;
            //var comboPartner = comboOrigin == a ? b : a;

            var comboOrigin = a;
            var comboPartner = b;

            lineVLineHCombo.Execute(new LineVLineHComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                Center = b,
                FinalizeAtEnd = false,
                ActivateSpecial = ApplySpecialActivation
            });

            return;
        }

        if (IsPulseLineCombo(sa, sb))
        {
            lineVHPulseCoreCombo.Execute(new LineVHPulseCoreComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                ActivateSpecial = ApplySpecialActivation,
                EmitComboTriggered = (comboSa, comboSb, cell) => effectOrchestrator.EmitComboTriggered(comboSa, comboSb, cell),
                EmitPulseEmitterComboTriggered = cell => effectOrchestrator.EmitPulseEmitterComboTriggered(cell)
            });

            return;
        }

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
        execCtx.Effects = effectOrchestrator;
    }

    public void ApplySpecialActivation(ResolutionContext ctx, TileView specialTile, TileView partnerTile)
    {
        if (specialTile == null) return;

        var special = specialTile.GetSpecial();
        int ox = specialTile.X;
        int oy = specialTile.Y;

        switch (special)
        {
            case TileSpecial.LineV:
                lineVSpecial.Execute(new LineVExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = partnerTile,
                    FinalizeAtEnd = false,
                    ActivateSpecial = ApplySpecialActivation
                });
                break;

            case TileSpecial.LineH:
                lineHSpecial.Execute(new LineHExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = partnerTile,
                    FinalizeAtEnd = false,
                    ActivateSpecial = ApplySpecialActivation
                });
                break;

            case TileSpecial.PulseCore:
                pulseCoreSpecial.Execute(new PulseCoreExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = partnerTile,
                    FinalizeAtEnd = false,
                    ActivateSpecial = ApplySpecialActivation
                });
                break;

            case TileSpecial.PatchBot:
                patchBotSpecial.Execute(new PatchBotExecutionRuntime
                {
                    Board = board,
                    Context = ctx,
                    Origin = specialTile,
                    Partner = partnerTile,
                    FinalizeAtEnd = false,
                    PatchbotService = patchbotComboService,
                    VisualService = visualService,
                    Effects = effectOrchestrator,
                    ActivateSpecial = ApplySpecialActivation
                });
                break;

            case TileSpecial.SystemOverride:
                ActivateSystemOverride(ctx, specialTile, partnerTile, ox, oy);
                break;

            default:
                ActivateViaRegistry(ctx, special, ox, oy);
                break;
        }
    }

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

    private static bool IsLineCombo(TileSpecial a, TileSpecial b)
    {
        return IsLine(a) && IsLine(b);
    }

    private static bool IsLine(TileSpecial special)
    {
        return special == TileSpecial.LineH || special == TileSpecial.LineV;
    }

    private static bool IsPulseLineCombo(TileSpecial a, TileSpecial b)
    {
        return (IsPulse(a) && IsLine(b)) || (IsPulse(b) && IsLine(a));
    }

    private static bool IsPulse(TileSpecial special)
    {
        return special == TileSpecial.PulseCore;
    }

}
