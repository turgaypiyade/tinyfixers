using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideOverrideComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public SpecialVisualService VisualService;
    public SpecialEffectOrchestrator Effects;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
}

public sealed class OverrideOverrideComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideOverrideCombo
{
    public OverrideOverrideComboExecutionResult Execute(OverrideOverrideComboExecutionRuntime rt)
    {
        var result = new OverrideOverrideComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        RegisterOrigins(rt);
        CollectAllTargets(rt);
        PreparePresentation(rt);

        if (rt.FinalizeAtEnd)
        {
            if (rt.ProcessFanout != null)
            {
                var fanoutActions = rt.ProcessFanout(rt.Context);
                if (fanoutActions != null && fanoutActions.Count > 0)
                    result.Actions.AddRange(fanoutActions);
            }

            if (rt.Context.OverrideDeferredPulseExplosions.Count == 0)
                rt.CleanupImplantedTiles?.Invoke(rt.Context);

            if (rt.Context.OverrideRadialClearDelays != null && rt.Context.OverrideRadialClearDelays.Count > 0)
                rt.FireOverrideOverrideSpecialVisuals?.Invoke(rt.Context.Affected, rt.Context.OverrideRadialClearDelays);

            result.Actions.Add(BuildClearAction(rt.Context));
        }

        return result;
    }

    private bool CanExecute(OverrideOverrideComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            && rt.Partner.GetSpecial() == TileSpecial.SystemOverride;
    }

    private void RegisterOrigins(OverrideOverrideComboExecutionRuntime rt)
    {
        AddAffected(rt, rt.Origin);
        AddAffected(rt, rt.Partner);
    }

    private void CollectAllTargets(OverrideOverrideComboExecutionRuntime rt)
    {
        SpecialCellUtils.AddAllTiles(rt.Context.Affected, rt.Context, rt.Board);
    }

    private void PreparePresentation(OverrideOverrideComboExecutionRuntime rt)
    {
        rt.Context.OverrideFanoutOrigin = rt.Origin;
        rt.Context.OverrideFanoutNormalSelectionPulse = false;
        rt.Context.OverrideForceDefaultClearAnim = true;
        rt.Context.OverrideSuppressPerTileClearVfx = false;

        Vector2Int originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        float comboVfxDuration = rt.Effects != null
            ? rt.Effects.PlayOverrideComboVfxAndQueue(TileSpecial.SystemOverride, TileSpecial.SystemOverride, originCell)
            : 0f;

        rt.Context.OverrideVfxDuration = comboVfxDuration;

        float maxDelay = comboVfxDuration > 0f
            ? Mathf.Max(ResolutionContext.OverrideRadialClearDuration, comboVfxDuration * 0.55f)
            : ResolutionContext.OverrideRadialClearDuration;

        rt.Context.OverrideRadialClearDelays = rt.VisualService != null
            ? rt.VisualService.BuildCenterOutClearDelays(rt.Context.Affected, maxDelay)
            : null;
    }

    private void AddAffected(OverrideOverrideComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private MatchClearAction BuildClearAction(ResolutionContext ctx)
    {
        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }
}
