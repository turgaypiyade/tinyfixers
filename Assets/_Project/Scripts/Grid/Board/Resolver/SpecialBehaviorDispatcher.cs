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
    private readonly LineHPatchBotCombo lineHPatchBotCombo = new();
    private readonly LineVPatchBotCombo lineVPatchBotCombo = new();
    private readonly PulseCorePatchBotCombo pulseCorePatchBotCombo = new();
    private readonly OverrideSpecial overrideSpecial = new();
    private readonly OverrideSpecializedCombo overrideSpecializedCombo = new();
    private readonly OverrideOverrideCombo overrideOverrideCombo = new();
    private readonly PatchBotCombo patchBotCombo = new();
    private readonly PulsePulseCombo pulsePulseCombo = new();
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
        if (sa == TileSpecial.PulseCore && sb == TileSpecial.PulseCore)
        {
            pulsePulseCombo.Execute(new PulsePulseComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                Effects = effectOrchestrator,
                ActivateSpecial = ApplySpecialActivation,
                EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                ExecuteSpecialActions = ExecuteSpecialActions,
                DebugLog = msg => Debug.Log(msg),
                EmitComboTriggered = (comboSa, comboSb, cell) => effectOrchestrator.EmitComboTriggered(comboSa, comboSb, cell),
                EmitPulseEmitterComboTriggered = cell => effectOrchestrator.EmitPulseEmitterComboTriggered(cell)
            });

            return;
        }

        if ((sa == TileSpecial.PatchBot && sb == TileSpecial.LineH) ||
            (sb == TileSpecial.PatchBot && sa == TileSpecial.LineH))
        {
            lineHPatchBotCombo.Execute(new LineHPatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = ApplySpecialActivation
            });

            return;
        }

        if ((sa == TileSpecial.PatchBot && sb == TileSpecial.LineV) ||
            (sb == TileSpecial.PatchBot && sa == TileSpecial.LineV))
        {
            lineVPatchBotCombo.Execute(new LineVPatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = ApplySpecialActivation
            });

            return;
        }

        if ((sa == TileSpecial.PatchBot && sb == TileSpecial.PulseCore) ||
            (sb == TileSpecial.PatchBot && sa == TileSpecial.PulseCore))
        {
            pulseCorePatchBotCombo.Execute(new PulseCorePatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = ApplySpecialActivation
            });

            return;
        }
        
        if (sa == TileSpecial.SystemOverride && sb == TileSpecial.SystemOverride)
        {
            overrideOverrideCombo.Execute(new OverrideOverrideComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                VisualService = visualService,
                Effects = effectOrchestrator
            });

            return;
        }

        if ((sa == TileSpecial.SystemOverride && (sb == TileSpecial.LineH || sb == TileSpecial.LineV || sb == TileSpecial.PulseCore || sb == TileSpecial.PatchBot)) ||
            (sb == TileSpecial.SystemOverride && (sa == TileSpecial.LineH || sa == TileSpecial.LineV || sa == TileSpecial.PulseCore || sa == TileSpecial.PatchBot)))
        {
            overrideSpecializedCombo.Execute(new OverrideSpecializedComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                EnqueueActivation = (resolution, tile, partner) => QueueProcessor.EnqueueActivation(resolution, tile, partner)
            });

            return;
        }

        if (sa == TileSpecial.PatchBot && sb == TileSpecial.PatchBot)
        {
            patchBotCombo.Execute(new PatchBotComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                FinalizeAtEnd = false,
                PatchbotService = patchbotComboService,
                VisualService = visualService,
                Effects = effectOrchestrator,
                ActivateSpecial = ApplySpecialActivation
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
                    ActivateSpecial = ApplySpecialActivation,
                    EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                    ActivateSpecial = ApplySpecialActivation,
                    EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                    ActivateSpecial = ApplySpecialActivation,
                    EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                    ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                {
                    var cell = new Vector2Int(specialTile.X, specialTile.Y);

                    if (ctx.HasLineActivation && ctx.LightningLineStrikes != null && ctx.LightningLineStrikes.Count > 0)
                    {
                        if (!ctx.DeferredLineHitOverrideCells.Contains(cell))
                            ctx.DeferredLineHitOverrideCells.Add(cell);

                        break;
                    }

                    if (ctx.IsPulsePulseComboActive)
                    {
                        if (!ctx.DeferredPulseComboOverrideCells.Contains(cell))
                            ctx.DeferredPulseComboOverrideCells.Add(cell);

                        break;
                    }

                    overrideSpecial.Execute(new OverrideExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = specialTile,
                        Partner = partnerTile,
                        FinalizeAtEnd = false
                    });
                    break;
                }

            default:
                ActivateViaRegistry(ctx, special, ox, oy);
                break;
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
                        FinalizeAtEnd = false,
                        ActivateSpecial = ApplySpecialActivation,
                        EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);
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
                        FinalizeAtEnd = false,
                        ActivateSpecial = ApplySpecialActivation,
                        EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);
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
                        FinalizeAtEnd = false,
                        ActivateSpecial = ApplySpecialActivation,
                        EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                        ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                        FinalizeAtEnd = false,
                        PatchbotService = patchbotComboService,
                        VisualService = visualService,
                        Effects = effectOrchestrator,
                        ActivateSpecial = ApplySpecialActivation
                    });

                    if (res != null && res.Actions != null)
                        actions.AddRange(res.Actions);
                    break;
                }

            case TileSpecial.SystemOverride:
                {
                    overrideSpecial.Execute(new OverrideExecutionRuntime
                    {
                        Board = board,
                        Context = ctx,
                        Origin = tile,
                        Partner = partner,
                        FinalizeAtEnd = false
                    });
                    break;
                }
        }

        return actions;
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
