using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PulseCorePatchBotComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public PatchbotComboService PatchbotService;
    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
}

public sealed class PulseCorePatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCorePatchBotCombo
{
    private readonly int affectedCellCount;
    private readonly PulseCoreSpecial pulseCoreSpecial;

    public PulseCorePatchBotCombo(int affectedCellCount = 9)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
        pulseCoreSpecial = new PulseCoreSpecial(this.affectedCellCount);
    }

    public PulseCorePatchBotComboExecutionResult Execute(PulseCorePatchBotComboExecutionRuntime rt)
    {
        var result = new PulseCorePatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var patchBotTile = GetPatchBotTile(rt);
        var pulseTile = GetPulseTile(rt);

        RegisterComboTiles(rt, patchBotTile, pulseTile);

        var target = rt.PatchbotService.FindTarget(patchBotTile, pulseTile, null);
        if (!target.hasCell)
            return result;

        int tx = target.x;
        int ty = target.y;

        rt.VisualService.PlayTeleportMarkers(patchBotTile, tx, ty);
        rt.VisualService.PlayTeleportMarkers(pulseTile, tx, ty);

        rt.Context.Affected.Add(patchBotTile);
        rt.Context.Affected.Add(pulseTile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, patchBotTile, rt.Board);
        SpecialCellUtils.MarkAffectedCell(rt.Context, pulseTile, rt.Board);

        if (rt.FinalizeAtEnd)
        {
            var initialClearAction = new MatchClearAction(
                new HashSet<TileView> { patchBotTile, pulseTile },
                doShake: false,
                animationMode: ClearAnimationMode.Default,
                isSpecialPhase: true
            );
            result.Actions.Add(initialClearAction);
        }

        rt.PatchbotService.EnqueueDash(patchBotTile, tx, ty, pulseTile, null, () =>
        {
            var arrivalCtx = new ResolutionContext();

            var pulseResult = pulseCoreSpecial.ExecuteAtTarget(new PulseCoreExecutionRuntime
            {
                Board = rt.Board,
                Context = arrivalCtx,

                // Kaynak pulse taşı arrival anında artık canlı olmayabilir.
                Origin = pulseTile,
                Partner = patchBotTile,

                FinalizeAtEnd = true,
                ActivateSpecial = rt.ActivateSpecial,
                ProcessFanout = rt.ProcessFanout,
                CleanupImplantedTiles = rt.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = rt.EmitBoardSignal,
                EnqueueChainSpecials = rt.EnqueueChainSpecials,
                ProcessQueue = rt.ProcessQueue,

                SuppressVisualSideEffects = false,
                SkipOriginRegistration = true,
                ForcedOriginSpecial = TileSpecial.PulseCore,
                SignalSourceTile = pulseTile
            }, tx, ty);

            var sequencer = rt.Board.GetComponent<ActionSequencer>();
            if (sequencer != null && pulseResult != null && pulseResult.Actions.Count > 0)
                sequencer.Enqueue(pulseResult.Actions);
        });

        return result;
    }

    private bool CanExecute(PulseCorePatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot;
        bool partnerIsPatchBot = rt.Partner.GetSpecial() == TileSpecial.PatchBot;
        bool originIsPulse = rt.Origin.GetSpecial() == TileSpecial.PulseCore;
        bool partnerIsPulse = rt.Partner.GetSpecial() == TileSpecial.PulseCore;

        return (originIsPatchBot && partnerIsPulse) || (partnerIsPatchBot && originIsPulse);
    }

    private void RegisterComboTiles(PulseCorePatchBotComboExecutionRuntime rt, TileView patchBotTile, TileView pulseTile)
    {
        AddPatchBotOrigin(rt, patchBotTile);
        AddPulseReference(rt, pulseTile);
    }

    private void AddPatchBotOrigin(PulseCorePatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private void AddPulseReference(PulseCorePatchBotComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private TileView GetPatchBotTile(PulseCorePatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
    }

    private TileView GetPulseTile(PulseCorePatchBotComboExecutionRuntime rt)
    {
        return rt.Origin.GetSpecial() == TileSpecial.PulseCore ? rt.Origin : rt.Partner;
    }
}
