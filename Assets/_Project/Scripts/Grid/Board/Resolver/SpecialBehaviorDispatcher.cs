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
    private readonly PulsePulseCombo pulsePulseCombo = new(radius: 3);   // 7x7 (2*3+1)
    internal ActivationQueueProcessor QueueProcessor;

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
            lineVLineHCombo.Execute(new LineVLineHComboExecutionRuntime
            {
                Board = board,
                Context = ctx,
                Origin = a,
                Partner = b,
                Center = b,
                FinalizeAtEnd = false,
                ActivateSpecial = ApplySpecialActivation,
                EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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
                ActivateSpecial = ApplySpecialActivation,
                EnqueueChainSpecials = resolution => QueueProcessor.EnqueueChainSpecials(resolution),
                ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution)
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

        ApplyGenericCombo(ctx, combo, a, b, sa, sb);
    }

    private void ApplyGenericCombo(ResolutionContext ctx, IComboBehavior combo, TileView a, TileView b, TileSpecial sa, TileSpecial sb)
    {
        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(a.X, a.Y));

        var cells = combo.CalculateAffectedCells(board, a.X, a.Y, sa, sb);
        foreach (var c in cells)
        {
            SpecialCellUtils.MarkAffectedCell(ctx, c.x, c.y, board);
            if (SpecialUtils.CanTargetTileContent(board, c.x, c.y) && board.Tiles[c.x, c.y] != null)
                ctx.Affected.Add(board.Tiles[c.x, c.y]);
        }
    }

    public void ApplySpecialActivation(ResolutionContext ctx, TileView specialTile, TileView partnerTile)
    {
        if (specialTile == null) return;
        if (IsInteractionLocked(specialTile.X, specialTile.Y)) return;

        var special = specialTile.GetSpecial();
        int ox = specialTile.X;
        int oy = specialTile.Y;

        // Tekil special aktivasyon sinyali (zincirdekiler dahil) — BossDuel bonus hasarı dinler.
        board.RaiseSpecialActivated(special, new Vector2Int(ox, oy));

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
                DrainDeferredLineOverrides(ctx);
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
                DrainDeferredLineOverrides(ctx);
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
                    ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution),
                    SuppressVisualSideEffects = ctx.IsPulsePulseComboActive
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

                    Debug.Log(
                        $"[OverrideActivation] cell={cell} " +
                        $"hasLine={ctx.HasLineActivation} " +
                        $"strikes={(ctx.LightningLineStrikes != null ? ctx.LightningLineStrikes.Count : -1)} " +
                        $"isPulsePulse={ctx.IsPulsePulseComboActive} " +
                        $"isPulseCore={ctx.IsPulseCoreActive} " +
                        $"processed={ctx.Processed.Contains(cell)} " +
                        $"queued={ctx.Queued.Contains(cell)}");

                    if (ctx.HasLineActivation)
                    {
                        if (!ctx.DeferredLineHitOverrideCells.Contains(cell))
                            ctx.DeferredLineHitOverrideCells.Add(cell);
                        break;
                    }

                    if (ctx.IsPulsePulseComboActive)
                    {
                        Debug.Log($"[OverrideDefer] reason=PulsePulse cell={cell} " +
                                  $"deferredBefore={ctx.DeferredPulseComboOverrideCells.Count}");

                        if (!ctx.DeferredPulseComboOverrideCells.Contains(cell))
                            ctx.DeferredPulseComboOverrideCells.Add(cell);

                        Debug.Log($"[OverrideDefer] reason=PulsePulse cell={cell} " +
                                  $"deferredAfter={ctx.DeferredPulseComboOverrideCells.Count}");
                        break;
                    }

                    if (ctx.IsPulseCoreActive)
                    {
                        Debug.Log($"[OverrideDefer] reason=PulseCore cell={cell} " +
                                  $"deferredBefore={ctx.DeferredPulseComboOverrideCells.Count}");

                        if (!ctx.DeferredPulseComboOverrideCells.Contains(cell))
                            ctx.DeferredPulseComboOverrideCells.Add(cell);

                        Debug.Log($"[OverrideDefer] reason=PulseCore cell={cell} " +
                                  $"deferredAfter={ctx.DeferredPulseComboOverrideCells.Count}");
                        break;
                    }

                    Debug.Log($"[OverrideExecuteNow] cell={cell}");

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

        if (IsInteractionLocked(tile.X, tile.Y))
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
                    if (res != null && res.Actions != null) actions.AddRange(res.Actions);
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
                    if (res != null && res.Actions != null) actions.AddRange(res.Actions);
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
                        ProcessQueue = resolution => QueueProcessor.ProcessQueue(resolution),
                        SuppressVisualSideEffects = ctx.IsPulsePulseComboActive
                    });
                    if (res != null && res.Actions != null) actions.AddRange(res.Actions);
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
                    if (res != null && res.Actions != null) actions.AddRange(res.Actions);
                    break;
                }

            case TileSpecial.SystemOverride:
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
            if (SpecialUtils.CanTargetTileContent(board, c.x, c.y) && board.Tiles[c.x, c.y] != null)
                ctx.Affected.Add(board.Tiles[c.x, c.y]);
        }
    }

    // When a Line special fires inside a chain (FinalizeAtEnd=false), Override tiles in its
    // path are deferred instead of immediately cleared. Drain them here so they still activate.
    private void DrainDeferredLineOverrides(ResolutionContext ctx)
    {
        if (ctx?.DeferredLineHitOverrideCells == null || ctx.DeferredLineHitOverrideCells.Count == 0)
            return;

        var deferred = new System.Collections.Generic.List<Vector2Int>(ctx.DeferredLineHitOverrideCells);
        ctx.DeferredLineHitOverrideCells.Clear();

        foreach (var cell in deferred)
        {
            if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                continue;

            var tile = board.Tiles[cell.x, cell.y];
            if (tile == null || tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            if (IsInteractionLocked(cell.x, cell.y))
                continue;

            ctx.Processed.Remove(cell);
            ctx.Queued.Remove(cell);

            ExecuteSpecialActions(ctx, tile, null);
        }
    }

    private static bool IsLineCombo(TileSpecial a, TileSpecial b) => IsLine(a) && IsLine(b);
    private static bool IsLine(TileSpecial special) => special == TileSpecial.LineH || special == TileSpecial.LineV;
    private static bool IsPulseLineCombo(TileSpecial a, TileSpecial b) => (IsPulse(a) && IsLine(b)) || (IsPulse(b) && IsLine(a));
    private static bool IsPulse(TileSpecial special) => special == TileSpecial.PulseCore;

    private bool IsInteractionLocked(int x, int y)
    {
        if (board == null || board.ObstacleStateService == null)
            return false;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        return board.ObstacleStateService.IsInteractionLockedAt(x, y);
    }
}
