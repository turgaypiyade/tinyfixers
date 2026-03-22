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

        var comboLightningVisualTargets = new List<TileView>();

        rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), comboCenterCell);

        RegisterComboTiles(rt, lineTile, pulseTile);
        BuildAffectedArea(rt, comboCenterTile, comboLightningVisualTargets);

        ExpandChain(rt, result);
        RemoveDeferredOverrideOriginsFromClear(rt);

        if (rt.FinalizeAtEnd && rt.Context.Affected.Count > 0)
        {
            // Chain'den gelen gerçek line strike var mı kontrol et.
            // LineVHPulseCoreComboAction (Blocking=true) roket sweep görsellerini yönetiyor.
            // Chain yoksa: Default mod + suppressPerTileClearVfx → beam yok, roketler zaten görseli halletti.
            // Chain varsa: LightningStrike mod → chain special'ların line sweep'leri çalışır.
            bool hasChainLightning =
                rt.Context.LightningLineStrikes != null &&
                rt.Context.LightningLineStrikes.Count > 0;

            result.Actions.Add(new MatchClearAction(
                rt.Context.Affected,
                doShake: true,
                animationMode: hasChainLightning
                    ? ClearAnimationMode.LightningStrike
                    : ClearAnimationMode.Default,
                affectedCells: rt.Context.AffectedCells,
                impactCells: rt.Context.ImpactCells,
                obstacleHitContext: null,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: null,
                lightningOriginCell: null,
                lightningVisualTargets: hasChainLightning
                    ? new List<TileView>(rt.Context.LightningVisualTargets)
                    : null,
                lightningLineStrikes: hasChainLightning
                    ? rt.Context.LightningLineStrikes
                    : null,
                suppressPerTileClearVfx: !hasChainLightning,
                isSpecialPhase: true,
                presentationPlan: null
            ));
        }

        var pulseAction = CreatePulseEmitterComboAction(
            rt.Board,
            comboCenterCell.x,
            comboCenterCell.y,
            rt.EmitPulseEmitterComboTriggered);

        if (pulseAction != null)
            result.Actions.Insert(0, pulseAction);

        return result;
    }
    private void RemoveDeferredOverrideOriginsFromClear(LineVHPulseCoreComboExecutionRuntime rt)
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

            // Ertelenen Override'ı combo'nun clear action'ından çıkar.
            // DrainDeferredLineOverrides onu kendi ayrı action'ıyla çalıştıracak.
            rt.Context.Affected.Remove(tile);
        }
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
        // Line bu combo'nun parçası → ekran kararır, line efektleri aktif.
        // Chain'de Override bulunursa deferral mekanizması bunu kullanır.
        rt.Context.HasLineActivation = true;
    }

    private void BuildAffectedArea(LineVHPulseCoreComboExecutionRuntime rt, TileView comboCenterTile, List<TileView> comboLightningVisualTargets)
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

            // Nested zincirde tekrar kuyruğa düşmemesi için önce işaretle,
            // ama execute sırasında geçici olarak kaldır — çünkü LineVSpecial.CanExecute,
            // LineHSpecial.CanExecute vs. Processed.Contains kontrolü yapıyor.
            rt.Context.Processed.Add(cell);

            if (!rt.Context.ChainExecutionOrder.Contains(cell))
                rt.Context.ChainExecutionOrder.Add(cell);

            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] EXECUTE special={special} cell={cell}");

            // Geçici olarak Processed'dan çıkar ki special kendi CanExecute kontrolünü geçsin
            rt.Context.Processed.Remove(cell);
            var nestedActions = rt.ExecuteSpecialActions?.Invoke(rt.Context, tile, null);
            // Geri ekle — artık çalıştı, tekrar kuyruğa düşmemeli
            rt.Context.Processed.Add(cell);
            if (nestedActions != null && nestedActions.Count > 0)
            {
                rt.DebugLog?.Invoke(
                    $"[LineVHPulseCoreCombo] MERGE nested actions count={nestedActions.Count} from {special} at {cell}");
                result.Actions.AddRange(nestedActions);
            }

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
        {
            rt.DebugLog?.Invoke("[LineVHPulseCoreCombo.TryQueue] skip reason=tile-null");
            return;
        }

        var cell = new Vector2Int(tile.X, tile.Y);
        var special = tile.GetSpecial();

        if (tile == rt.Origin || tile == rt.Partner)
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo.TryQueue] skip cell={cell} special={special} reason=origin-partner");
            return;
        }

        if (special == TileSpecial.None)
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo.TryQueue] skip cell={cell} special=None reason=no-special");
            return;
        }

        if (rt.Context.Processed.Contains(cell))
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo.TryQueue] skip cell={cell} special={special} reason=processed");
            return;
        }

        if (rt.Context.Queued.Contains(cell))
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo.TryQueue] skip cell={cell} special={special} reason=already-queued");
            return;
        }

        rt.Context.Queued.Add(cell);
        pending.Enqueue(tile);
        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo.TryQueue] enqueue cell={cell} special={special} pending={pending.Count}");
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
        SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board);

        var tile = rt.Board.GetTileViewAt(x, y);
        if (tile == null)
            return;

        rt.Context.Affected.Add(tile);
        comboLightningVisualTargets.Add(tile);

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

    private static BoardAction CreatePulseEmitterComboAction(
        BoardController board,
        int cx,
        int cy,
        Action<Vector2Int> emitPulseEmitterComboTriggered)
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

        return new LineVHPulseCoreComboAction(
            board,
            targets,
            hOrigins,
            vOrigins,
            targetVisuals,
            new Vector2Int(cx, cy),
            emitPulseEmitterComboTriggered);
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
    private readonly Vector2Int comboCenterCell;
    private readonly Action<Vector2Int> emitPulseEmitterComboTriggered;
    public override bool Blocking => true;

    public LineVHPulseCoreComboAction(BoardController board, HashSet<Vector2Int> targets, List<(Vector2Int cell, Vector2 anch)> hOrigins,
                                      List<(Vector2Int cell, Vector2 anch)> vOrigins, Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals,
                                    Vector2Int comboCenterCell, Action<Vector2Int> emitPulseEmitterComboTriggered)
    {
        this.board = board;
        this.targets = targets;
        this.hOrigins = hOrigins;
        this.vOrigins = vOrigins;
        this.targetVisuals = targetVisuals;
        this.comboCenterCell = comboCenterCell;
        this.emitPulseEmitterComboTriggered = emitPulseEmitterComboTriggered;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        var cleared = new HashSet<Vector2Int>();
        var hiddenOrigins = new HashSet<TileView>();

        foreach (var h in hOrigins)
        {
            var view = board.GetTileViewAt(h.cell.x, h.cell.y);
            if (view != null && hiddenOrigins.Add(view))
            {
                SpecialVisualService.HideTileVisualForCombo(view);

                if (cleared.Add(h.cell) && targetVisuals.TryGetValue(h.cell, out var originVisual))
                    board.ClearCellVisualOnly(h.cell, originVisual.type, originVisual.view);
            }
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
        }

        if (board.lineTravelPlayer == null)
        {
            foreach (var kvp in targetVisuals)
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            yield break;
        }

        emitPulseEmitterComboTriggered?.Invoke(comboCenterCell);

        void OnStep(Vector2Int cell)
        {
            if (!targets.Contains(cell))
                return;

            if (!cleared.Add(cell))
                return;

            if (targetVisuals.TryGetValue(cell, out var visualData))
                board.ClearCellVisualOnly(cell, visualData.type, visualData.view);
        }

        int pendingTravels = 0;
        int travelIdSeed = 0;
        float expectedMaxDuration = 0f;

        Action<string, Vector2Int> makeCompletedLogger = (axisLabel, originCell) =>
        {
            pendingTravels = Mathf.Max(0, pendingTravels - 1);
        };

        int width = board.Width;
        int height = board.Height;
        float tileSize = board.TileSize;

        foreach (var h in hOrigins)
        {
            int steps = Mathf.Max(h.cell.x, width - 1 - h.cell.x);
            expectedMaxDuration = Mathf.Max(
                expectedMaxDuration,
                board.lineTravelPlayer != null ? board.lineTravelPlayer.EstimateDuration(steps) : 0f);

            pendingTravels++;
            int travelId = ++travelIdSeed;

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
            expectedMaxDuration = Mathf.Max(
                expectedMaxDuration,
                board.lineTravelPlayer != null ? board.lineTravelPlayer.EstimateDuration(steps) : 0f);

            pendingTravels++;
            int travelId = ++travelIdSeed;

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

        float waitTimeout = expectedMaxDuration + 0.05f;
        float waited = 0f;

        while (pendingTravels > 0 && waited < waitTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        //pendingTravels = 0;

        foreach (var kvp in targetVisuals)
        {
            if (cleared.Add(kvp.Key))
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
        }
    }
}