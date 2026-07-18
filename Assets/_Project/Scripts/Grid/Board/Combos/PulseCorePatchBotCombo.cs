using System;
using System.Collections;
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

    /// Varış context'inde ertelenen SystemOverride'ları boşaltır (resolver'ın
    /// DrainDeferredPulseComboOverrides overload'ı). Yalnızca ESKİ varış yolu için
    /// fallback; BuildPulseBurstChain bağlıysa kullanılmaz.
    public Action<ResolutionContext, List<BoardAction>> DrainDeferredOverrides;

    /// DiveBurst (plan §1.1): varış hücresinde sanal pulse patlamasını TEK MOTORDAN
    /// (SpecialChainRunner, virtualPulseBurstCenters) üretir. Bağlıysa airborne varışı
    /// ExecuteAtTarget+deferral+drain yerine bunu çalıştırır — alandaki Override/special
    /// arrival'da gerçek sınıfıyla tetiklenir (solo pulse ile aynı yol).
    public Func<List<Vector2Int>, BoardAction> BuildPulseBurstChain;
}

public sealed class PulseCorePatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCorePatchBotCombo
{
    private readonly int affectedCellCount;
    private readonly PulseCoreSpecial pulseCoreSpecial;

    public PulseCorePatchBotCombo(int affectedCellCount = 25)
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

        // PulseCore + PatchBot artık PatchbotDashUI/onArrived callback modelini kullanmıyor.
        // Override + PatchBot ile aynı güvenli airborne action pipeline'ını kullanıyor:
        // takeoff -> source cascade -> live target resolve -> synchronized dive
        // -> PulseCore clear -> final cascade.
        if (rt.FinalizeAtEnd)
        {
            result.Actions.Add(new OverridePatchBotAirborneGroupAction(
                rt.Board,
                new Vector2Int(patchBotTile.X, patchBotTile.Y),
                patchBotTile,
                pulseTile,
                rt,
                affectedCellCount));
        }

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