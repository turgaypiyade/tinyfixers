using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PulsePulseComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    public SpecialEffectOrchestrator Effects;
    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;

    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
}

public sealed class PulsePulseComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulsePulseCombo
{
    private readonly int radius;

    public PulsePulseCombo(int radius = 4)
    {
        this.radius = Mathf.Max(1, radius);
    }

    public PulsePulseComboExecutionResult Execute(PulsePulseComboExecutionRuntime rt)
    {
        rt.Context.IsPulsePulseComboActive = true;

        var result = new PulsePulseComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        int centerX = rt.Origin.X;
        int centerY = rt.Origin.Y;

        RegisterOrigins(rt);
        PlayComboVisuals(rt, centerX, centerY);
        CollectArea(rt, centerX, centerY);
        ExecuteQueuedChain(rt);
        rt.Context.IsPulsePulseComboActive = false;

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

            var clearAction = BuildClearAction(rt);
            if (clearAction != null)
                result.Actions.Add(clearAction);

            rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                SpecialBoardSignalType.SpecialPassFinished,
                new Vector2Int(centerX, centerY),
                rt.Origin));
        }

        return result;
    }

    private bool CanExecute(PulsePulseComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        return rt.Origin.GetSpecial() == TileSpecial.PulseCore
            && rt.Partner.GetSpecial() == TileSpecial.PulseCore;
    }

    private void RegisterOrigins(PulsePulseComboExecutionRuntime rt)
    {
        AddOrigin(rt, rt.Origin);
        AddOrigin(rt, rt.Partner);
    }

    private void AddOrigin(PulsePulseComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);
        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);
        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);
    }

    private void PlayComboVisuals(PulsePulseComboExecutionRuntime rt, int centerX, int centerY)
    {
        rt.Effects?.EmitComboTriggered(TileSpecial.PulseCore, TileSpecial.PulseCore, new Vector2Int(centerX, centerY));
        // VFX zaten BoardController'da charge öncesi spawn edildi — burada tekrar spawn etmiyoruz
    }

    private void CollectArea(PulsePulseComboExecutionRuntime rt, int centerX, int centerY)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
                    continue;

                SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board);

                var tile = rt.Board.Tiles[x, y];
                if (tile == null)
                    continue;

                rt.Context.Affected.Add(tile);
            }
        }
    }

    private void ExecuteQueuedChain(PulsePulseComboExecutionRuntime rt)
    {
        if (rt.EnqueueChainSpecials == null || rt.ProcessQueue == null)
            return;

        rt.EnqueueChainSpecials(rt.Context);
        rt.ProcessQueue(rt.Context);
    }

    private MatchClearAction BuildClearAction(PulsePulseComboExecutionRuntime rt)
    {
        var ctx = rt.Context;

        HashSet<TileView> processedViews = new HashSet<TileView>();
        foreach (var pos in ctx.Processed)
        {
            if (rt.Board.Tiles[pos.x, pos.y] != null)
                processedViews.Add(rt.Board.Tiles[pos.x, pos.y]);
        }

        Dictionary<TileView, float> stagger =
            rt.Board.PulseCoreImpactService.BuildStaggerDelays(ctx.Affected, processedViews);

        return new MatchClearAction(
            ctx.Affected,
            doShake: true,
            staggerDelays: stagger,
            staggerAnimTime: rt.Board.ApplySpecialChainTempo(rt.Board.PulseImpactAnimTime),
            animationMode: ctx.HasLineActivation && !ctx.OverrideForceDefaultClearAnim
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: ctx.LightningVisualTargets,
            lightningLineStrikes: ctx.LightningLineStrikes,
            suppressPerTileClearVfx: true,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
    }
    private ClearPresentationPlan BuildPresentationPlan(PulsePulseComboExecutionRuntime rt)
    {
        if (rt == null || rt.Context == null || rt.Board == null)
            return null;

        var plan = new ClearPresentationPlan
        {
            DoBoardShake = true,
            IncludeAdjacentOverTileBlockerDamage = false,
            ObstacleHitContext = ObstacleHitContext.SpecialActivation
        };

        var targetTiles = new List<TileView>();
        var targetCells = new List<Vector2Int>();
        var delayMap = BuildPulseDelayMap(rt);

        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            if (tile.GetSpecial() != TileSpecial.None && tile != rt.Origin && tile != rt.Partner)
                continue;

            targetTiles.Add(tile);
            targetCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (targetTiles.Count == 0)
            return null;

        int centerX = rt.Origin.X;
        int centerY = rt.Origin.Y;

        plan.Effects.Add(new PulseWaveEffectDescriptor(
            targetTiles,
            targetCells,
            delayMap,
            rt.Board.PulseImpactAnimTime,
            new Vector2Int(centerX, centerY),
            centerRadiusCells: radius + 1,
            clearOnImpact: true,
            tailHoldSecondsOverride: 0.55f));

        return plan;
    }

    private Dictionary<TileView, float> BuildPulseDelayMap(PulsePulseComboExecutionRuntime rt)
    {
        var result = new Dictionary<TileView, float>();
        if (rt == null || rt.Context == null || rt.Board == null || rt.Origin == null)
            return result;

        int cx = rt.Origin.X;
        int cy = rt.Origin.Y;

        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null)
                continue;

            int dx = Mathf.Abs(tile.X - cx);
            int dy = Mathf.Abs(tile.Y - cy);

            // kare alan temizliyorsun; chebyshev dalga hissi burada daha iyi olur
            int dist = Mathf.Max(dx, dy);

            result[tile] = dist * rt.Board.PulseImpactDelayStep;
        }

        return result;
    }
    private void RemoveDeferredOverrideOriginsFromPulseClear(PulsePulseComboExecutionRuntime rt)
    {
        if (rt?.Context?.DeferredPulseComboOverrideCells == null || rt.Context.DeferredPulseComboOverrideCells.Count == 0)
            return;

        foreach (var cell in rt.Context.DeferredPulseComboOverrideCells)
        {
            if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                continue;

            var tile = rt.Board.Tiles[cell.x, cell.y];
            if (tile == null)
                continue;

            if (tile.GetSpecial() != TileSpecial.SystemOverride)
                continue;

            rt.Context.Affected.Remove(tile);
        }
    }
}