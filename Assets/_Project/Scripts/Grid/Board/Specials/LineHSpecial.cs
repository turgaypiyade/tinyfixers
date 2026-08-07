using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineHExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;
    public TileView Origin;
    public TileView Partner;

    // Allows combos to delegate the horizontal line effect to LineH without
    // requiring a physical LineH tile at the target cell.
    public Vector2Int? VirtualOriginCell;

    public bool FinalizeAtEnd;

    // Only true for late/deferred PatchBot+LineH arrival clears that must own refill.
    // Default false keeps normal LineH behavior unchanged.
    public bool EnqueueCascadeOnComplete;

    public Action<ResolutionContext, TileView, TileView> ActivateSpecial;

    public Func<ResolutionContext, List<BoardAction>> ProcessFanout;
    public Action<ResolutionContext> CleanupImplantedTiles;
    public Action<HashSet<TileView>, Dictionary<TileView, float>> FireOverrideOverrideSpecialVisuals;
    public Action<SpecialBoardSignal> EmitBoardSignal;
    public Action<ResolutionContext> EnqueueChainSpecials;
    public Action<ResolutionContext> ProcessQueue;
    public bool SuppressVisualSideEffects;

    // Swap sırasında normal taraftan oluşan yeni special hücreler — LineH bu hücreleri tüketmez.
    public HashSet<Vector2Int> ProtectedCells;
}

public sealed class LineHExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}
public sealed class LineHSpecial
{
    public LineHExecutionResult Execute(LineHExecutionRuntime rt)
    {
        var result = new LineHExecutionResult();
        if (!CanExecute(rt))
            return result;

        RegisterOrigin(rt);
        CollectRow(rt);
        BuildLineVisuals(rt);
        ExecuteQueuedChain(rt);
        RemoveDeferredOverrideOriginsFromLineClear(rt);

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

            var originCell = GetOriginCell(rt);
            rt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                SpecialBoardSignalType.SpecialPassFinished,
                originCell,
                rt.Origin));
        }
        return result;
    }

    private void RemoveDeferredOverrideOriginsFromLineClear(LineHExecutionRuntime rt)
    {
        if (rt?.Context?.DeferredLineHitOverrideCells == null || rt.Context.DeferredLineHitOverrideCells.Count == 0)
            return;

        foreach (var cell in rt.Context.DeferredLineHitOverrideCells)
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
    private void ExecuteQueuedChain(LineHExecutionRuntime rt)
    {
        if (rt.EnqueueChainSpecials == null || rt.ProcessQueue == null)
            return;

        rt.EnqueueChainSpecials(rt.Context);
        rt.ProcessQueue(rt.Context);
    }

    private bool CanExecute(LineHExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.VirtualOriginCell.HasValue)
        {
            var cell = rt.VirtualOriginCell.Value;
            if (cell.x < 0 || cell.x >= rt.Board.Width || cell.y < 0 || cell.y >= rt.Board.Height)
                return false;

            return !rt.Context.Processed.Contains(cell) || rt.Origin == null;
        }

        if (rt.Origin == null)
            return false;

        if (rt.Origin.GetSpecial() != TileSpecial.LineH)
            return false;

        var originCell = new Vector2Int(rt.Origin.X, rt.Origin.Y);
        if (rt.Context.Processed.Contains(originCell))
            return false;

        return true;
    }

    private void RegisterOrigin(LineHExecutionRuntime rt)
    {
        var originCell = GetOriginCell(rt);

        if (rt.Origin != null)
        {
            rt.Context.Processed.Add(originCell);
            rt.Context.Affected.Add(rt.Origin);
            SpecialCellUtils.MarkAffectedCell(rt.Context, rt.Origin, rt.Board);
        }
        else
        {
            SpecialCellUtils.MarkAffectedCell(rt.Context, originCell.x, originCell.y, rt.Board);
        }

        if (!rt.SuppressVisualSideEffects)
            rt.Context.HasLineActivation = true;
    }

    private void CollectRow(LineHExecutionRuntime rt)
    {
        var originCell = GetOriginCell(rt);
        int y = originCell.y;

        for (int x = 0; x < rt.Board.Width; x++)
        {
            if (rt.ProtectedCells != null && rt.ProtectedCells.Contains(new Vector2Int(x, y)))
                continue;

            if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
            {
                SpecialCellUtils.TryAddMagnetEndpointImpact(rt.Board, x, y, rt.Context.ImpactCells);
                SpecialCellUtils.TryMarkEmitterImpact(rt.Context, rt.Board, x, y);
                continue;
            }

            // Movable obstacle (PlasticTwoStage vb.) → SADECE obstacle hasarı (ImpactCells).
            // Tile'ı Affected'a eklemek onu normal taş gibi YOK EDİYOR; çok-hit movable'da
            // obstacle verisi hücrede sağ kalıyordu (hayalet veri → sonraki düşüşte movable
            // hedefi dolu / veri ezilmesi). Sweep beam hücresini zaten vurur ve
            // lineHitDamagedObstacleCells dedup'u çift hasarı engeller; sweep koşmayan
            // modlarda (default anim) hasar ImpactCells üzerinden gelir.
            if (SpecialCellUtils.TryRouteMovableToImpact(rt.Context, rt.Board, x, y))
                continue;

            SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board);

            var tile = rt.Board.Tiles[x, y];
            if (tile == null)
                continue;
            rt.Context.Affected.Add(tile);

            if (!rt.SuppressVisualSideEffects)
                rt.Context.LightningVisualTargets.Add(tile);
        }
    }

    private void BuildLineVisuals(LineHExecutionRuntime rt)
    {
        if (rt.SuppressVisualSideEffects)
            return;
        var originCell = GetOriginCell(rt);
        rt.Context.LightningLineStrikes.Add(
            new LightningLineStrike(
                originCell,
                true));
    }


    private MatchClearAction BuildClearAction(LineHExecutionRuntime rt)
    {
        var ctx = rt.Context;

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
            presentationPlan: null,
            enqueueCascadeOnComplete: rt.EnqueueCascadeOnComplete
        );
    }
    private static Vector2Int GetOriginCell(LineHExecutionRuntime rt)
    {
        if (rt != null && rt.VirtualOriginCell.HasValue)
            return rt.VirtualOriginCell.Value;

        if (rt != null && rt.Origin != null)
            return new Vector2Int(rt.Origin.X, rt.Origin.Y);

        return Vector2Int.zero;
    }

    private static string DescribeOrigin(LineHExecutionRuntime rt)
    {
        if (rt == null)
            return "<null-runtime>";

        if (rt.VirtualOriginCell.HasValue)
            return $"virtual({rt.VirtualOriginCell.Value.x},{rt.VirtualOriginCell.Value.y})";

        return rt.Origin != null ? $"({rt.Origin.X},{rt.Origin.Y})" : "<null-origin>";
    }
}
