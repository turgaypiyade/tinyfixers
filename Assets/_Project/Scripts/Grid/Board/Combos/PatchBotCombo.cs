using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotComboExecutionRuntime
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
}

public sealed class PatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PatchBotCombo
{
    public PatchBotComboExecutionResult Execute(PatchBotComboExecutionRuntime rt)
    {
        var result = new PatchBotComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var firstPatchBot = rt.Origin.GetSpecial() == TileSpecial.PatchBot ? rt.Origin : rt.Partner;
        var secondPatchBot = firstPatchBot == rt.Origin ? rt.Partner : rt.Origin;

        // Source tiles'ı processed olarak işaretle — cascade/chain tekrar aktive etmesin.
        rt.Context.Processed.Add(new Vector2Int(firstPatchBot.X, firstPatchBot.Y));
        rt.Context.Processed.Add(new Vector2Int(secondPatchBot.X, secondPatchBot.Y));

        RegisterComboTiles(rt, firstPatchBot, secondPatchBot);

        ComboBehaviorEvents.EmitComboTriggered(
            TileSpecial.PatchBot,
            TileSpecial.PatchBot,
            new Vector2Int(firstPatchBot.X, firstPatchBot.Y));

        if (!rt.FinalizeAtEnd)
            return result;

        // OverridePatchBotAirborneGroupAction akışı:
        //   1. Her iki source tile hemen boşaltılır (ClearCell + visual hide)
        //   2. Cascade hover sırasında çalışır — taşlar yerleşir
        //   3. Cascade bittikten SONRA her botun hedefi yeniden değerlendirilir
        //   4. Her iki bot AYNI ANDA dive eder ve TEK clear action üretir
        // Bu sıralama, ilk botun cascade'inin ikinci botun hedefini silmesi
        // durumunda ghost hedefe gidilmesini önler.
        result.Actions.Add(new OverridePatchBotAirborneGroupAction(
            rt.Board,
            new List<Vector2Int>
            {
                new Vector2Int(firstPatchBot.X, firstPatchBot.Y),
                new Vector2Int(secondPatchBot.X, secondPatchBot.Y)
            },
            bonusPhantomBots: 1));

        if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
            rt.CleanupImplantedTiles?.Invoke(rt.Context);

        return result;
    }

    private bool CanExecute(PatchBotComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.PatchbotService == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.PatchBot &&
               rt.Partner.GetSpecial() == TileSpecial.PatchBot;
    }

    private void RegisterComboTiles(
        PatchBotComboExecutionRuntime rt,
        TileView a,
        TileView b)
    {
        if (a != null)
        {
            rt.Context.Affected.Add(a);
            SpecialCellUtils.MarkAffectedCell(rt.Context, a, rt.Board);
        }

        if (b != null)
        {
            rt.Context.Affected.Add(b);
            SpecialCellUtils.MarkAffectedCell(rt.Context, b, rt.Board);
        }
    }
}
