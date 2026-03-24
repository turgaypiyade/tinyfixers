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
}

public sealed class PulseCorePatchBotComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class PulseCorePatchBotCombo
{
    private readonly int affectedCellCount;

    public PulseCorePatchBotCombo(int affectedCellCount = 9)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
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

        float travelDuration = rt.Board.PatchbotDashUI != null
            ? rt.Board.PatchbotDashUI.EstimateDashDuration(
                rt.Board,
                new Vector2Int(patchBotTile.X, patchBotTile.Y),
                new Vector2Int(tx, ty))
            : 0.22f;

        rt.VisualService.PlayTeleportMarkers(patchBotTile, tx, ty);
        rt.VisualService.PlayTeleportMarkers(pulseTile, tx, ty);

        rt.VisualService.PlayTravelingSpecialPairGhost(
            patchBotTile,
            pulseTile,
            new Vector2Int(patchBotTile.X, patchBotTile.Y),
            new Vector2Int(tx, ty),
            travelDuration,
            true);

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

        rt.PatchbotService.EnqueueDash(patchBotTile, tx, ty, null, () =>
        {
            var arrivalCtx = new ResolutionContext();
            var arrivalRt = new PulseCorePatchBotComboExecutionRuntime
            {
                Board = rt.Board,
                Context = arrivalCtx,
                Origin = patchBotTile,
                Partner = pulseTile,
                PatchbotService = rt.PatchbotService,
                VisualService = rt.VisualService,
                Effects = rt.Effects,
                ActivateSpecial = rt.ActivateSpecial,
                ProcessFanout = rt.ProcessFanout,
                CleanupImplantedTiles = rt.CleanupImplantedTiles,
                FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                EmitBoardSignal = rt.EmitBoardSignal,
                FinalizeAtEnd = true
            };

            SchedulePulseExplosionVfx(arrivalRt, tx, ty, 0f);

            CollectAreaAtTarget(arrivalRt, tx, ty);
            ExecuteChain(arrivalRt, pulseTile, tx, ty);

            var deferredActions = new List<BoardAction>();

            if (arrivalRt.FinalizeAtEnd)
            {
                if (arrivalRt.ProcessFanout != null)
                {
                    var fanoutActions = arrivalRt.ProcessFanout(arrivalRt.Context);
                    if (fanoutActions != null && fanoutActions.Count > 0)
                        deferredActions.AddRange(fanoutActions);
                }

                if (arrivalRt.Context.OverrideDeferredPulseExplosions.Count == 0)
                    arrivalRt.CleanupImplantedTiles?.Invoke(arrivalRt.Context);

                if (arrivalRt.Context.OverrideRadialClearDelays != null && arrivalRt.Context.OverrideRadialClearDelays.Count > 0)
                    arrivalRt.FireOverrideOverrideSpecialVisuals?.Invoke(arrivalRt.Context.Affected, arrivalRt.Context.OverrideRadialClearDelays);

                var clearAction = BuildClearAction(arrivalRt);
                if (clearAction != null)
                    deferredActions.Add(clearAction);

                arrivalRt.EmitBoardSignal?.Invoke(new SpecialBoardSignal(
                    SpecialBoardSignalType.SpecialPassFinished,
                    new Vector2Int(tx, ty),
                    pulseTile));
            }

            var sequencer = arrivalRt.Board.GetComponent<ActionSequencer>();
            if (sequencer != null && deferredActions.Count > 0)
            {
                sequencer.Enqueue(deferredActions);
            }
        });

        return result;
    }

    // ---------------------------------------------------------------
    //  Pulse VFX
    //  PulseCoreSpecial ile aynı patlama oyuncusunu kullanır;
    //  tile olmasa da (obstacle/boş) doğrudan hücre merkezinde oynatır.
    // ---------------------------------------------------------------

    private void SchedulePulseExplosionVfx(PulseCorePatchBotComboExecutionRuntime rt, int cellX, int cellY, float delay)
    {
        int radiusCells = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount)) / 2;
        if (radiusCells < 1)
            radiusCells = 1;

        rt.Board.StartCoroutine(CoPlayDelayedPulseVfx(rt.Board, cellX, cellY, radiusCells, delay));
    }

    private IEnumerator CoPlayDelayedPulseVfx(
        BoardController board, int cellX, int cellY, int radiusCells, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (board?.PulseCoreImpactService != null)
            board.PulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell(cellX, cellY, radiusCells);
    }

    // ---------------------------------------------------------------
    //  Mevcut mantık
    // ---------------------------------------------------------------

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

    private void CollectAreaAtTarget(PulseCorePatchBotComboExecutionRuntime rt, int centerX, int centerY)
    {
        PulseBehaviorEvents.EmitPulseExplosionPlayed(new Vector2Int(centerX, centerY));

        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0) side += 1;
        int half = side / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
        {
            for (int y = centerY - half; y <= centerY + half; y++)
            {
                if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
                    continue;

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

    private void ExecuteChain(PulseCorePatchBotComboExecutionRuntime rt, TileView pulseTile, int centerX, int centerY)
    {
        var pending = new Queue<TileView>();

        SeedAreaSpecials(rt, pulseTile, centerX, centerY, pending);

        while (pending.Count > 0)
        {
            var tile = pending.Dequeue();
            if (tile == null)
                continue;

            var pos = new Vector2Int(tile.X, tile.Y);

            if (rt.Context.Processed.Contains(pos))
                continue;

            var special = tile.GetSpecial();
            if (special == TileSpecial.None)
                continue;

            rt.Context.Queued.Remove(pos);

            if (special == TileSpecial.PulseCore)
            {
                new PulseCoreSpecial(affectedCellCount).Execute(new PulseCoreExecutionRuntime
                {
                    Board = rt.Board,
                    Context = rt.Context,
                    Origin = tile,
                    Partner = null,
                    FinalizeAtEnd = false,
                    ActivateSpecial = rt.ActivateSpecial,
                    ProcessFanout = rt.ProcessFanout,
                    CleanupImplantedTiles = rt.CleanupImplantedTiles,
                    FireOverrideOverrideSpecialVisuals = rt.FireOverrideOverrideSpecialVisuals,
                    EmitBoardSignal = rt.EmitBoardSignal
                });
            }
            else
            {
                if (!rt.Context.Processed.Contains(pos))
                    rt.ActivateSpecial?.Invoke(rt.Context, tile, null);

                rt.Context.Processed.Add(pos);
            }

            EnqueueNewlyAffectedSpecials(rt, pulseTile, pending);
        }
    }

    private void SeedAreaSpecials(
        PulseCorePatchBotComboExecutionRuntime rt,
        TileView pulseTile,
        int centerX,
        int centerY,
        Queue<TileView> pending)
    {
        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0) side += 1;
        int half = side / 2;

        for (int x = centerX - half; x <= centerX + half; x++)
        {
            for (int y = centerY - half; y <= centerY + half; y++)
            {
                if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
                    continue;

                var tile = rt.Board.Tiles[x, y];
                if (tile == null || tile == pulseTile)
                    continue;

                TryQueue(rt, pending, tile);
            }
        }
    }

    private void EnqueueNewlyAffectedSpecials(
        PulseCorePatchBotComboExecutionRuntime rt,
        TileView pulseTile,
        Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
        {
            if (tile == null || tile == pulseTile)
                continue;

            TryQueue(rt, pending, tile);
        }
    }

    private void TryQueue(PulseCorePatchBotComboExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
            return;

        if (tile.GetSpecial() == TileSpecial.None)
            return;

        if (tile == rt.Origin || tile == rt.Partner)
            return;

        var pos = new Vector2Int(tile.X, tile.Y);

        if (rt.Context.Processed.Contains(pos))
            return;

        if (rt.Context.Queued.Contains(pos))
            return;

        rt.Context.Queued.Add(pos);
        pending.Enqueue(tile);
    }

    private MatchClearAction BuildClearAction(PulseCorePatchBotComboExecutionRuntime rt)
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
            animationMode: ClearAnimationMode.Default,
            affectedCells: ctx.AffectedCells,
            impactCells: ctx.ImpactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: null,
            lightningLineStrikes: null,
            suppressPerTileClearVfx: ctx.OverrideSuppressPerTileClearVfx,
            perTileClearDelays: ctx.OverrideRadialClearDelays,
            isSpecialPhase: true,
            presentationPlan: null
        );
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
