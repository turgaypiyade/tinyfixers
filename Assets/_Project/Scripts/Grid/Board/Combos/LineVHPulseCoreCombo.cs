using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVHPulseCoreComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;

    // Origin = line tile
    // Partner = pulse tile
    public TileView Origin;
    public TileView Partner;

    public bool FinalizeAtEnd;

    // Nested special execute sonucu action listesi döner
    public Func<ResolutionContext, TileView, TileView, List<BoardAction>> ExecuteSpecialActions;

    public Action<string> DebugLog;
    public Action<TileSpecial, TileSpecial, Vector2Int> EmitComboTriggered;
    public Action<Vector2Int> EmitPulseEmitterComboTriggered;
}

public sealed class LineVHPulseCoreComboExecutionResult
{
    public readonly List<BoardAction> Actions = new();
}

public sealed class LineVHPulseCoreCombo
{
    public LineVHPulseCoreComboExecutionResult Execute(LineVHPulseCoreComboExecutionRuntime rt)
    {
        var result = new LineVHPulseCoreComboExecutionResult();

        if (!CanExecute(rt))
            return result;

        var pulseTile = GetPulseTile(rt);
        var lineTile = GetLineTile(rt);
        var pulseCell = new Vector2Int(pulseTile.X, pulseTile.Y);

        // Combo presentation bilgisini local tutuyoruz.
        // Shared context'e yazmıyoruz ki zincirdeki diğer special'lara sızmasın.
        var comboLightningVisualTargets = new List<TileView>();

        rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), pulseCell);
        rt.EmitPulseEmitterComboTriggered?.Invoke(pulseCell);

        RegisterComboTiles(rt, lineTile, pulseTile);
        BuildAffectedArea(rt, lineTile, pulseTile, comboLightningVisualTargets);

        ExpandChain(rt, result);

        if (rt.FinalizeAtEnd && rt.Context.Affected.Count > 0)
        {
            result.Actions.Add(new MatchClearAction(
                rt.Context.Affected,
                doShake: true,
                animationMode: ClearAnimationMode.LightningStrike,
                affectedCells: rt.Context.AffectedCells,
                obstacleHitContext: null,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: null,
                lightningOriginCell: null,
                lightningVisualTargets: comboLightningVisualTargets,
                lightningLineStrikes: null, // ekstra combo line-strike bind etmiyoruz
                isSpecialPhase: true,
                presentationPlan: null
            ));
        }

        var pulseAction = PulseLineCombo.CreatePulseEmitterComboAction(rt.Board, pulseTile.X, pulseTile.Y);
        if (pulseAction != null)
            result.Actions.Insert(0, pulseAction);

        return result;
    }

    private bool CanExecute(LineVHPulseCoreComboExecutionRuntime rt)
    {
        if (rt == null || rt.Board == null || rt.Context == null)
            return false;

        if (rt.Origin == null || rt.Partner == null)
            return false;

        bool originIsLine = IsLine(rt.Origin.GetSpecial());
        bool partnerIsLine = IsLine(rt.Partner.GetSpecial());
        bool originIsPulse = IsPulse(rt.Origin.GetSpecial());
        bool partnerIsPulse = IsPulse(rt.Partner.GetSpecial());

        return (originIsLine && partnerIsPulse) || (originIsPulse && partnerIsLine);
    }

    private void RegisterComboTiles(LineVHPulseCoreComboExecutionRuntime rt, TileView lineTile, TileView pulseTile)
    {
        AddOrigin(rt, lineTile);
        AddOrigin(rt, pulseTile);

        // BURADA rt.Context.HasLineActivation = true DEMİYORUZ.
        // Çünkü bu flag zincirdeki diğer special'lara da pulse+line sunumu taşıyor.
    }

    private void BuildAffectedArea(
        LineVHPulseCoreComboExecutionRuntime rt,
        TileView lineTile,
        TileView pulseTile,
        List<TileView> comboLightningVisualTargets)
    {
        int cx = pulseTile.X;
        int cy = pulseTile.Y;

        if (lineTile.GetSpecial() == TileSpecial.LineH)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                for (int x = 0; x < rt.Board.Width; x++)
                    AddCell(rt, x, y, comboLightningVisualTargets);
            }
        }
        else
        {
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                for (int y = 0; y < rt.Board.Height; y++)
                    AddCell(rt, x, y, comboLightningVisualTargets);
            }
        }
    }

    private void ExpandChain(LineVHPulseCoreComboExecutionRuntime rt, LineVHPulseCoreComboExecutionResult result)
    {
        var pending = new Queue<TileView>();
        EnqueueNewlyAffectedSpecials(rt, pending);

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] seed count={pending.Count}");

        while (pending.Count > 0)
        {
            var tile = pending.Dequeue();
            if (tile == null)
                continue;

            var cell = new Vector2Int(tile.X, tile.Y);
            var special = tile.GetSpecial();

            rt.Context.Queued.Remove(cell);

            if (tile == rt.Origin || tile == rt.Partner)
                continue;

            rt.DebugLog?.Invoke(
                $"[LineVHPulseCoreCombo] candidate cell={cell} special={special} processed={rt.Context.Processed.Contains(cell)}");

            if (rt.Context.Processed.Contains(cell))
                continue;

            if (special == TileSpecial.None)
                continue;

            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] EXECUTE special={special} cell={cell}");

            // execute FIRST
            var nestedActions = rt.ExecuteSpecialActions?.Invoke(rt.Context, tile, null);
            if (nestedActions != null && nestedActions.Count > 0)
            {
                rt.DebugLog?.Invoke(
                    $"[LineVHPulseCoreCombo] MERGE nested actions count={nestedActions.Count} from {special} at {cell}");
                result.Actions.AddRange(nestedActions);
            }

            // processed AFTER execute
            rt.Context.Processed.Add(cell);

            if (!rt.Context.ChainExecutionOrder.Contains(cell))
                rt.Context.ChainExecutionOrder.Add(cell);

            EnqueueNewlyAffectedSpecials(rt, pending);
        }
    }

    private void EnqueueNewlyAffectedSpecials(LineVHPulseCoreComboExecutionRuntime rt, Queue<TileView> pending)
    {
        foreach (var tile in rt.Context.Affected)
            TryQueue(rt, pending, tile);
    }

    private void TryQueue(LineVHPulseCoreComboExecutionRuntime rt, Queue<TileView> pending, TileView tile)
    {
        if (tile == null)
            return;

        if (tile == rt.Origin || tile == rt.Partner)
            return;

        if (tile.GetSpecial() == TileSpecial.None)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);

        if (rt.Context.Processed.Contains(cell))
            return;

        if (rt.Context.Queued.Contains(cell))
            return;

        rt.Context.Queued.Add(cell);
        pending.Enqueue(tile);
    }

    private void AddCell(
        LineVHPulseCoreComboExecutionRuntime rt,
        int x,
        int y,
        List<TileView> comboLightningVisualTargets)
    {
        if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
            return;

        if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
            return;

        var cell = new Vector2Int(x, y);
        rt.Context.AffectedCells.Add(cell);

        var tile = rt.Board.GetTileViewAt(x, y);
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        comboLightningVisualTargets.Add(tile);

        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] AddCell cell={cell} special={tile.GetSpecial()}");
    }

    private void AddOrigin(LineVHPulseCoreComboExecutionRuntime rt, TileView tile)
    {
        if (tile == null)
            return;

        var cell = new Vector2Int(tile.X, tile.Y);

        rt.Context.Processed.Add(cell);
        rt.Context.Affected.Add(tile);

        SpecialCellUtils.MarkAffectedCell(rt.Context, tile, rt.Board);

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] AddOrigin cell={cell} special={tile.GetSpecial()}");
    }

    private TileView GetPulseTile(LineVHPulseCoreComboExecutionRuntime rt)
    {
        return IsPulse(rt.Origin.GetSpecial()) ? rt.Origin : rt.Partner;
    }

    private TileView GetLineTile(LineVHPulseCoreComboExecutionRuntime rt)
    {
        return IsLine(rt.Origin.GetSpecial()) ? rt.Origin : rt.Partner;
    }

    private static bool IsLine(TileSpecial s)
    {
        return s == TileSpecial.LineH || s == TileSpecial.LineV;
    }

    private static bool IsPulse(TileSpecial s)
    {
        return s == TileSpecial.PulseCore;
    }
}