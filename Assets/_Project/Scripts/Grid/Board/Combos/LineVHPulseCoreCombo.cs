using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineVHPulseCoreComboExecutionRuntime
{
    public BoardController Board;
    public ResolutionContext Context;

    // Origin/Partner rolleri çağıran akış tarafından belirlenir.
    // Line/Pulse kimliği combo içinde special type üzerinden çözülür.
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
        var comboCenterTile = rt.Partner != null ? rt.Partner : pulseTile;
        var comboCenterCell = new Vector2Int(comboCenterTile.X, comboCenterTile.Y);

        // Combo presentation bilgisini local tutuyoruz.
        // Shared context'e yazmıyoruz ki zincirdeki diğer special'lara sızmasın.
        var comboLightningVisualTargets = new List<TileView>();

        rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), comboCenterCell);
        rt.EmitPulseEmitterComboTriggered?.Invoke(comboCenterCell);

        RegisterComboTiles(rt, lineTile, pulseTile);
        BuildAffectedArea(rt, comboCenterTile, comboLightningVisualTargets);

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

        var pulseAction = CreatePulseEmitterComboAction(rt.Board, comboCenterCell.x, comboCenterCell.y);
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
        TileView comboCenterTile,
        List<TileView> comboLightningVisualTargets)
    {
        int cx = comboCenterTile.X;
        int cy = comboCenterTile.Y;

        // 3 satır
        for (int y = cy - 1; y <= cy + 1; y++)
        {
            for (int x = 0; x < rt.Board.Width; x++)
                AddCell(rt, x, y, comboLightningVisualTargets);
        }

        // 3 sütun
        for (int x = cx - 1; x <= cx + 1; x++)
        {
            for (int y = 0; y < rt.Board.Height; y++)
                AddCell(rt, x, y, comboLightningVisualTargets);
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

    private static BoardAction CreatePulseEmitterComboAction(BoardController board, int cx, int cy)
    {
        var targets = board.BuildPulseEmitterTargets(cx, cy);

        RectTransform space = null;
        if (board.lineTravelPlayer != null)
        {
            space = board.lineTravelPlayer.afterImageParent != null
                ? board.lineTravelPlayer.afterImageParent
                : board.LineTravelSpawnParent as RectTransform;
        }

        var hOrigins = new List<(Vector2Int cell, Vector2 anch)>();
        var vOrigins = new List<(Vector2Int cell, Vector2 anch)>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= board.Height)
                continue;

            var originTile = board.Tiles[cx, yy];
            if (originTile == null)
                continue;

            var originRect = originTile.GetComponent<RectTransform>();
            var worldCenter = originRect.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
            hOrigins.Add((new Vector2Int(cx, yy), board.WorldToAnchoredIn(space, worldCenter)));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= board.Width)
                continue;

            var originTile = board.Tiles[xx, cy];
            if (originTile == null)
                continue;

            var originRect = originTile.GetComponent<RectTransform>();
            var worldCenter = originRect.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
            vOrigins.Add((new Vector2Int(xx, cy), board.WorldToAnchoredIn(space, worldCenter)));
        }

        var targetVisuals = new Dictionary<Vector2Int, (TileType type, TileView view)>();
        foreach (var cell in targets)
        {
            var tile = board.Tiles[cell.x, cell.y];
            if (tile != null)
                targetVisuals[cell] = (tile.GetTileType(), tile);
        }

        foreach (var cell in targets)
            board.ClearCellDataOnly(cell);

        return new LineVHPulseCoreComboAction(board, targets, hOrigins, vOrigins, targetVisuals);
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

public sealed class LineVHPulseCoreComboAction : BoardAction
{
    private readonly BoardController board;
    private readonly HashSet<Vector2Int> targets;
    private readonly List<(Vector2Int cell, Vector2 anch)> hOrigins;
    private readonly List<(Vector2Int cell, Vector2 anch)> vOrigins;
    private readonly Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals;

    public LineVHPulseCoreComboAction(
        BoardController board,
        HashSet<Vector2Int> targets,
        List<(Vector2Int cell, Vector2 anch)> hOrigins,
        List<(Vector2Int cell, Vector2 anch)> vOrigins,
        Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals)
    {
        this.board = board;
        this.targets = targets;
        this.hOrigins = hOrigins;
        this.vOrigins = vOrigins;
        this.targetVisuals = targetVisuals;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        var cleared = new HashSet<Vector2Int>();
        var hiddenOrigins = new HashSet<TileView>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string CellsToString(IEnumerable<Vector2Int> cells)
        {
            return string.Join(", ", cells);
        }

        Debug.Log(
            $"[LineTravelAction] START targets={targets.Count} " +
            $"hOrigins={hOrigins.Count} vOrigins={vOrigins.Count} " +
            $"targetCells=[{CellsToString(targets)}]");
#endif

        foreach (var h in hOrigins)
        {
            var view = board.GetTileViewAt(h.cell.x, h.cell.y);
            if (view != null && hiddenOrigins.Add(view))
            {
                SpecialVisualService.HideTileVisualForCombo(view);

                if (cleared.Add(h.cell) && targetVisuals.TryGetValue(h.cell, out var originVisual))
                    board.ClearCellVisualOnly(h.cell, originVisual.type, originVisual.view);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LineTravelAction] HIDE origin H {h.cell}");
#endif
        }

        foreach (var v in vOrigins)
        {
            var view = board.GetTileViewAt(v.cell.x, v.cell.y);
            if (view != null && hiddenOrigins.Add(view))
            {
                SpecialVisualService.HideTileVisualForCombo(view);

                if (cleared.Add(v.cell) && targetVisuals.TryGetValue(v.cell, out var originVisual))
                    board.ClearCellVisualOnly(v.cell, originVisual.type, originVisual.view);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LineTravelAction] HIDE origin V {v.cell}");
#endif
        }

        if (board.lineTravelPlayer == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[LineTravelAction] lineTravelPlayer == null, fallback clear all targets immediately.");
#endif
            foreach (var kvp in targetVisuals)
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            yield break;
        }

        void OnStep(Vector2Int cell)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool isTarget = targets.Contains(cell);
            bool alreadyCleared = cleared.Contains(cell);
            bool hasVisual = targetVisuals.ContainsKey(cell);

            Debug.Log(
                $"[LineTravelAction] STEP cell={cell} " +
                $"isTarget={isTarget} alreadyCleared={alreadyCleared} hasVisual={hasVisual}");
#endif

            if (!targets.Contains(cell))
                return;

            if (!cleared.Add(cell))
                return;

            if (targetVisuals.TryGetValue(cell, out var visualData))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[LineTravelAction] CLEAR cell={cell} " +
                    $"type={visualData.type} view={(visualData.view != null ? visualData.view.name : "null")}");
#endif
                board.ClearCellVisualOnly(cell, visualData.type, visualData.view);
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[LineTravelAction] TARGET WITHOUT VISUAL DATA cell={cell}");
#endif
            }
        }

        int pendingTravels = 0;
        int travelIdSeed = 0;

        Action<string, Vector2Int> makeCompletedLogger = (axisLabel, originCell) =>
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LineTravelAction] COMPLETE axis={axisLabel} origin={originCell} " +
                $"pendingBeforeDec={pendingTravels}");
#endif
            pendingTravels = Mathf.Max(0, pendingTravels - 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LineTravelAction] COMPLETE axis={axisLabel} origin={originCell} " +
                $"pendingAfterDec={pendingTravels}");
#endif
        };

        int width = board.Width;
        int height = board.Height;
        float tileSize = board.TileSize;

        foreach (var h in hOrigins)
        {
            int steps = Mathf.Max(h.cell.x, width - 1 - h.cell.x);
            pendingTravels++;
            int travelId = ++travelIdSeed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LineTravelAction] BEGIN travelId={travelId} axis=H origin={h.cell} " +
                $"steps={steps} pendingAfterInc={pendingTravels}");
#endif

            board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Horizontal,
                h.anch,
                h.cell,
                steps,
                tileSize,
                0f,
                OnStep,
                () => makeCompletedLogger("H", h.cell));
        }

        foreach (var v in vOrigins)
        {
            int steps = Mathf.Max(v.cell.y, height - 1 - v.cell.y);
            pendingTravels++;
            int travelId = ++travelIdSeed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LineTravelAction] BEGIN travelId={travelId} axis=V origin={v.cell} " +
                $"steps={steps} pendingAfterInc={pendingTravels}");
#endif

            board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Vertical,
                v.anch,
                v.cell,
                steps,
                tileSize,
                0f,
                OnStep,
                () => makeCompletedLogger("V", v.cell));
        }

        while (pendingTravels > 0)
            yield return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var missedBeforeFallback = new List<Vector2Int>();
        foreach (var kvp in targetVisuals)
        {
            if (!cleared.Contains(kvp.Key))
                missedBeforeFallback.Add(kvp.Key);
        }

        Debug.Log(
            $"[LineTravelAction] PRE-FALLBACK cleared={cleared.Count}/{targetVisuals.Count} " +
            $"missed=[{CellsToString(missedBeforeFallback)}]");
#endif

        foreach (var kvp in targetVisuals)
        {
            if (cleared.Add(kvp.Key))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[LineTravelAction] FALLBACK CLEAR cell={kvp.Key}");
#endif
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LineTravelAction] END totalCleared={cleared.Count}");
#endif
    }
}
