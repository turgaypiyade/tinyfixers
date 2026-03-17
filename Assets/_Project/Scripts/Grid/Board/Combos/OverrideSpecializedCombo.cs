using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideSpecializedComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public Action<ResolutionContext, TileView, TileView> EnqueueActivation;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
}

public sealed class OverrideSpecializedComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class OverrideSpecializedCombo
{
    public OverrideSpecializedComboExecutionResult Execute(OverrideSpecializedComboExecutionRuntime rt)
    {
        var result = new OverrideSpecializedComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var overrideTile = rt.Origin.GetSpecial() == TileSpecial.SystemOverride ? rt.Origin : rt.Partner;
        var otherTile = overrideTile == rt.Origin ? rt.Partner : rt.Origin;
        var targetSpecial = otherTile.GetSpecial();

        AddOrigin(rt, overrideTile);
        AddOrigin(rt, otherTile);

        PrepareFanout(rt, overrideTile, targetSpecial);
        CollectTargets(rt, overrideTile, otherTile, targetSpecial);

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

    private bool CanExecute(OverrideSpecializedComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool hasOverride = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            || rt.Partner.GetSpecial() == TileSpecial.SystemOverride;

        if (!hasOverride)
            return false;

        var targetSpecial = rt.Origin.GetSpecial() == TileSpecial.SystemOverride
            ? rt.Partner.GetSpecial()
            : rt.Origin.GetSpecial();

        return targetSpecial == TileSpecial.LineH
            || targetSpecial == TileSpecial.LineV
            || targetSpecial == TileSpecial.PulseCore
            || targetSpecial == TileSpecial.PatchBot;
    }

    private void PrepareFanout(OverrideSpecializedComboExecutionRuntime rt, TileView overrideTile, TileSpecial targetSpecial)
    {
        rt.Context.OverrideFanoutOrigin = overrideTile;
        rt.Context.OverrideForceDefaultClearAnim = !(targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV);
        rt.Context.OverrideSuppressPerTileClearVfx = false;
        rt.Context.OverrideFanoutNormalSelectionPulse = false;

        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(
            new Vector2Int(overrideTile.X, overrideTile.Y),
            targetSpecial);
    }

    private void CollectTargets(OverrideSpecializedComboExecutionRuntime rt, TileView overrideTile, TileView otherTile, TileSpecial targetSpecial)
    {
        TileType baseType = otherTile.GetTileType();

        for (int x = 0; x < rt.Board.Width; x++)
        {
            for (int y = 0; y < rt.Board.Height; y++)
            {
                if (!SpecialCellUtils.CanAffectCell(rt.Board, x, y))
                    continue;

                var tile = rt.Board.Tiles[x, y];
                if (tile == null || !tile.GetTileType().Equals(baseType))
                    continue;

                if (tile.GetSpecial() != TileSpecial.None)
                {
                    rt.Context.Affected.Add(tile);
                    SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
                    rt.EnqueueActivation?.Invoke(rt.Context, tile, otherTile);
                    continue;
                }

                rt.Context.OverrideFanoutTargets.Add(tile);
                rt.Context.PendingOverrideImplants.Add(new ResolutionContext.PendingOverrideImplant(
                    new Vector2Int(tile.X, tile.Y),
                    targetSpecial,
                    new Vector2Int(otherTile.X, otherTile.Y),
                    new Vector2Int(overrideTile.X, overrideTile.Y)));
            }
        }
    }

    private void AddOrigin(OverrideSpecializedComboExecutionRuntime rt, TileView tile)
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
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null);
    }
}
