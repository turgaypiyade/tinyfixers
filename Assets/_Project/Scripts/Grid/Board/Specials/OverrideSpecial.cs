using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
}

public sealed class OverrideExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideSpecial
{
    public OverrideExecutionResult Execute(OverrideExecutionRuntime rt)
    {
        var result = new OverrideExecutionResult();

        if (!TryResolveTiles(rt, out var overrideTile, out var partnerTile))
            return result;
        Debug.Log($"[Override.Execute] origin=({overrideTile.X},{overrideTile.Y}) partner={(partnerTile != null ? $"({partnerTile.X},{partnerTile.Y})/{partnerTile.GetSpecial()}" : "null")} finalize={rt.FinalizeAtEnd}");
        if (rt.Context.OverrideFanoutOrigin != null && rt.Context.OverrideFanoutOrigin != overrideTile)
        {
            AddAffected(rt, overrideTile);
            return result;
        }

        AddAffected(rt, overrideTile);

        TileType type;
        if (partnerTile != null)
        {
            type = partnerTile.GetTileType();
        }
        else if (overrideTile.GetOverrideBaseType(out var storedType))
        {
            type = storedType;
        }
        else
        {
            type = overrideTile.GetTileType();
        }

        TileSpecial partnerSpecial = partnerTile != null ? partnerTile.GetSpecial() : TileSpecial.None;

        rt.Context.OverrideFanoutNormalSelectionPulse = (partnerTile == null) || (partnerSpecial == TileSpecial.None);
        rt.Context.OverrideFanoutPulseHitCount = 0;
        rt.Context.OverrideFanoutOrigin = overrideTile;

        rt.Context.OverrideForceDefaultClearAnim = true;
        rt.Context.OverrideSuppressPerTileClearVfx = false;

        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(
            new Vector2Int(overrideTile.X, overrideTile.Y),
            TileSpecial.None);

        SpecialCellUtils.CollectAllOfType(rt.Context.OverrideFanoutTargets, rt.Board, type, excludeSpecials: true);
        SpecialCellUtils.AddAllOfType(rt.Context.Affected, rt.Context, rt.Board, type, excludeSpecials: true);
        Debug.Log($"[Override.Execute] fanoutType={(partnerTile != null ? partnerTile.GetTileType().ToString() : overrideTile.GetTileType().ToString())} fanoutTargets={rt.Context.OverrideFanoutTargets.Count} affected={rt.Context.Affected.Count}");
        if (rt.FinalizeAtEnd)
            Finalize(rt, result);

        return result;
    }

    private bool TryResolveTiles(
        OverrideExecutionRuntime rt,
        out TileView overrideTile,
        out TileView partnerTile)
    {
        overrideTile = null;
        partnerTile = null;

        if (rt == null || rt.Board == null || rt.Context == null || rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() == TileSpecial.SystemOverride)
        {
            overrideTile = rt.Origin;
            partnerTile = rt.Partner;
            return true;
        }

        if (rt.Partner != null && rt.Partner.GetSpecial() == TileSpecial.SystemOverride)
        {
            overrideTile = rt.Partner;
            partnerTile = rt.Origin;
            return true;
        }

        return false;
    }

    private void AddAffected(OverrideExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }
    private void Finalize(OverrideExecutionRuntime rt, OverrideExecutionResult result)
    {
        if (rt.ProcessFanout != null)
        {
            var fanoutActions = rt.ProcessFanout(rt.Context);
            if (fanoutActions != null && fanoutActions.Count > 0)
                result.Actions.AddRange(fanoutActions);
        }

        if (rt.Context.OverrideDeferredPulseExplosions == null ||
            rt.Context.OverrideDeferredPulseExplosions.Count == 0)
        {
            rt.CleanupImplantedTiles?.Invoke(rt.Context);
        }

        if (rt.Context.OverrideRadialClearDelays != null &&
            rt.Context.OverrideRadialClearDelays.Count > 0)
        {
            Debug.Log($"[Override.Finalize] origin={(rt.Context.OverrideFanoutOrigin != null ? $"({rt.Context.OverrideFanoutOrigin.X},{rt.Context.OverrideFanoutOrigin.Y})" : "null")} radialDelays={(rt.Context.OverrideRadialClearDelays != null ? rt.Context.OverrideRadialClearDelays.Count : 0)} affected={rt.Context.Affected.Count}");
            rt.FireOverrideOverrideSpecialVisuals?.Invoke(
                rt.Context.Affected,
                rt.Context.OverrideRadialClearDelays);
        }

        result.Actions.Add(BuildClearAction(rt.Context));
    }

    private MatchClearAction BuildClearAction(ResolutionContext ctx)
    {
        var presentationPlan = BuildOverridePresentationPlanIfNeeded(ctx);

        bool overridePresentationOwnsVisuals =
            presentationPlan != null &&
            presentationPlan.Effects != null &&
            presentationPlan.Effects.Count > 0 &&
            presentationPlan.Effects[0] is OverrideRadialEffectDescriptor;

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: true,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: overridePresentationOwnsVisuals ? null : ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: presentationPlan);
    }

    private ClearPresentationPlan BuildOverridePresentationPlanIfNeeded(ResolutionContext ctx)
    {
        if (!IsPureSoloOverridePresentationCandidate(ctx))
            return null;

        List<TileView> targetTiles = new();
        List<Vector2Int> targetCells = new();

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;
            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (targetTiles.Count == 0)
            return null;

        TileView originTile = null;
        Vector2Int? originCell = null;

        foreach (var tile in targetTiles)
        {
            if (tile == null) continue;

            if (tile.GetSpecial() == TileSpecial.SystemOverride)
            {
                originTile = tile;
                originCell = new Vector2Int(tile.X, tile.Y);
                break;
            }
        }

        var delayMap = ctx.OverrideRadialClearDelays != null
            ? new Dictionary<TileView, float>(ctx.OverrideRadialClearDelays)
            : new Dictionary<TileView, float>();

        var plan = new ClearPresentationPlan();
        plan.DoBoardShake = true;
        plan.IncludeAdjacentOverTileBlockerDamage = true;
        plan.ObstacleHitContext = ObstacleHitContext.SpecialActivation;

        plan.Effects.Add(new OverrideRadialEffectDescriptor(
            targetTiles,
            targetCells,
            delayMap,
            originTile,
            originCell));

        foreach (var tile in targetTiles)
            plan.FinalClearTiles.Add(tile);

        return plan;
    }

    private bool IsPureSoloOverridePresentationCandidate(ResolutionContext ctx)
    {
        if (ctx == null) return false;
        if (ctx.HasLineActivation) return false;
        if (ctx.LightningLineStrikes != null && ctx.LightningLineStrikes.Count > 0) return false;
        if (ctx.LightningVisualTargets != null && ctx.LightningVisualTargets.Count > 0) return false;
        if (ctx.OverrideDeferredPulseExplosions != null && ctx.OverrideDeferredPulseExplosions.Count > 0) return false;
        if (ctx.OverrideDeferredPatchBotDashes != null && ctx.OverrideDeferredPatchBotDashes.Count > 0) return false;
        if (ctx.PendingOverrideImplants != null && ctx.PendingOverrideImplants.Count > 0) return false;

        int specialCount = 0;
        bool hasNonOverrideSpecial = false;

        foreach (var tile in ctx.Affected)
        {
            if (tile == null) continue;

            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None) continue;

            specialCount++;
            if (special != TileSpecial.SystemOverride)
            {
                hasNonOverrideSpecial = true;
                break;
            }
        }

        return !(hasNonOverrideSpecial || specialCount > 1);
    }
}