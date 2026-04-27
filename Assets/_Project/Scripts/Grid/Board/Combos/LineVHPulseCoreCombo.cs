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

        rt.EmitComboTriggered?.Invoke(lineTile.GetSpecial(), pulseTile.GetSpecial(), comboCenterCell);

        RegisterComboTiles(rt, lineTile, pulseTile);

        // Alan üstündeki special'lar aşağıdaki LineVHPulseCoreComboAction içinde
        // executeSpecialActions üzerinden kendi special davranışlarıyla tetiklenir.
        var comboLightningVisualTargets = new List<TileView>();
        BuildAffectedArea(rt, comboCenterTile, comboLightningVisualTargets);
        // Combo'nun kendi alanını snapshot al.
        // Path üstünde sonradan tetiklenecek special'lar bu clear'a karışmasın.
        var comboAffected = new HashSet<TileView>(rt.Context.Affected);
        var comboAffectedCells = new HashSet<Vector2Int>(rt.Context.AffectedCells);

        RemoveDeferredOverrideOriginsFromClear(comboAffected, comboAffectedCells, rt);

        if (rt.FinalizeAtEnd && comboAffected.Count > 0)
        {
            result.Actions.Add(new MatchClearAction(
                comboAffected,
                doShake: true,
                animationMode: ClearAnimationMode.Default,
                affectedCells: comboAffectedCells,
                impactCells: rt.Context.ImpactCells,
                obstacleHitContext: null,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: null,
                lightningOriginCell: null,
                lightningVisualTargets: null,
                lightningLineStrikes: null,
                suppressPerTileClearVfx: true,
                isSpecialPhase: true,
                presentationPlan: null
            ));
        }

        var pulseAction = CreatePulseEmitterComboAction(
            rt.Board,
            comboCenterCell.x,
            comboCenterCell.y,
            rt.EmitPulseEmitterComboTriggered,
            rt.Context?.DeferredLineHitOverrideCells,
            rt.Context,
            rt.ExecuteSpecialActions,
            lineTile,
            pulseTile);

        if (pulseAction != null)
            result.Actions.Insert(0, pulseAction);

        return result;
    }

    private LineVHPulseCoreComboExecutionResult ExecuteLineVCombo(
        LineVHPulseCoreComboExecutionRuntime rt,
        Vector2Int comboCenterCell)
    {
        var result = new LineVHPulseCoreComboExecutionResult();
        var origins = BuildLineVVirtualOrigins(rt.Board, comboCenterCell);

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] LineV delegate origins={origins.Count} center={comboCenterCell}");
        rt.EmitPulseEmitterComboTriggered?.Invoke(comboCenterCell);

        for (int i = 0; i < origins.Count; i++)
        {
            var originCell = origins[i];
            bool finalizeAtEnd = rt.FinalizeAtEnd && i == origins.Count - 1;

            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] delegate LineV origin=virtual({originCell.x},{originCell.y}) finalize={finalizeAtEnd}");

            var lineResult = ExecuteLineVAtVirtualOrigin(rt, originCell, finalizeAtEnd);
            if (lineResult != null && lineResult.Actions != null && lineResult.Actions.Count > 0)
                result.Actions.AddRange(lineResult.Actions);
        }

        return result;
    }

    private LineVHPulseCoreComboExecutionResult ExecuteLineHCombo(
        LineVHPulseCoreComboExecutionRuntime rt,
        Vector2Int comboCenterCell)
    {
        var result = new LineVHPulseCoreComboExecutionResult();
        var origins = BuildLineHVirtualOrigins(rt.Board, comboCenterCell);

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] LineH delegate origins={origins.Count} center={comboCenterCell}");
        rt.EmitPulseEmitterComboTriggered?.Invoke(comboCenterCell);

        for (int i = 0; i < origins.Count; i++)
        {
            var originCell = origins[i];
            bool finalizeAtEnd = rt.FinalizeAtEnd && i == origins.Count - 1;

            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] delegate LineH origin=virtual({originCell.x},{originCell.y}) finalize={finalizeAtEnd}");

            var lineResult = ExecuteLineHAtVirtualOrigin(rt, originCell, finalizeAtEnd);
            if (lineResult != null && lineResult.Actions != null && lineResult.Actions.Count > 0)
                result.Actions.AddRange(lineResult.Actions);
        }

        return result;
    }

    private static List<Vector2Int> BuildLineVVirtualOrigins(BoardController board, Vector2Int comboCenterCell)
    {
        var origins = new List<Vector2Int>();
        if (board == null)
            return origins;

        for (int x = comboCenterCell.x - 1; x <= comboCenterCell.x + 1; x++)
        {
            if (x < 0 || x >= board.Width)
                continue;

            if (comboCenterCell.y < 0 || comboCenterCell.y >= board.Height)
                continue;

            origins.Add(new Vector2Int(x, comboCenterCell.y));
        }

        return origins;
    }

    private static List<Vector2Int> BuildLineHVirtualOrigins(BoardController board, Vector2Int comboCenterCell)
    {
        var origins = new List<Vector2Int>();
        if (board == null)
            return origins;

        if (comboCenterCell.x < 0 || comboCenterCell.x >= board.Width)
            return origins;

        for (int y = comboCenterCell.y - 1; y <= comboCenterCell.y + 1; y++)
        {
            if (y < 0 || y >= board.Height)
                continue;

            origins.Add(new Vector2Int(comboCenterCell.x, y));
        }

        return origins;
    }

    private LineVExecutionResult ExecuteLineVAtVirtualOrigin(
        LineVHPulseCoreComboExecutionRuntime rt,
        Vector2Int virtualOriginCell,
        bool finalizeAtEnd)
    {
        var pending = new Queue<TileView>();
        var nestedResult = new LineVHPulseCoreComboExecutionResult();
        var lineV = new LineVSpecial();

        var result = lineV.Execute(new LineVExecutionRuntime
        {
            Board = rt.Board,
            Context = rt.Context,
            Origin = null,
            Partner = null,
            VirtualOriginCell = virtualOriginCell,
            FinalizeAtEnd = finalizeAtEnd,
            SuppressVisualSideEffects = false,
            ActivateSpecial = (resolution, tile, partner) =>
            {
                rt.ExecuteSpecialActions?.Invoke(resolution, tile, partner);
            },
            EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
            ProcessQueue = resolution => ProcessPendingChainQueue(rt, pending, nestedResult, "LineV")
        });

        if (result != null && nestedResult.Actions.Count > 0)
            result.Actions.InsertRange(0, nestedResult.Actions);

        return result;
    }

    private LineHExecutionResult ExecuteLineHAtVirtualOrigin(
        LineVHPulseCoreComboExecutionRuntime rt,
        Vector2Int virtualOriginCell,
        bool finalizeAtEnd)
    {
        var pending = new Queue<TileView>();
        var nestedResult = new LineVHPulseCoreComboExecutionResult();
        var lineH = new LineHSpecial();

        var result = lineH.Execute(new LineHExecutionRuntime
        {
            Board = rt.Board,
            Context = rt.Context,
            Origin = null,
            Partner = null,
            VirtualOriginCell = virtualOriginCell,
            FinalizeAtEnd = finalizeAtEnd,
            SuppressVisualSideEffects = false,
            ActivateSpecial = (resolution, tile, partner) =>
            {
                rt.ExecuteSpecialActions?.Invoke(resolution, tile, partner);
            },
            EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
            ProcessQueue = resolution => ProcessPendingChainQueue(rt, pending, nestedResult, "LineH")
        });

        if (result != null && nestedResult.Actions.Count > 0)
            result.Actions.InsertRange(0, nestedResult.Actions);

        return result;
    }

    private void ProcessPendingChainQueue(
        LineVHPulseCoreComboExecutionRuntime rt,
        Queue<TileView> pending,
        LineVHPulseCoreComboExecutionResult result,
        string ownerLabel)
    {
        if (rt == null || pending == null || result == null)
            return;

        ownerLabel = string.IsNullOrEmpty(ownerLabel) ? "Line" : ownerLabel;

        rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] {ownerLabel} chain seed count={pending.Count}");

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
                $"[LineVHPulseCoreCombo] {ownerLabel} candidate cell={cell} special={special} processed={rt.Context.Processed.Contains(cell)}");

            if (rt.Context.Processed.Contains(cell))
                continue;

            if (special == TileSpecial.None)
                continue;

            rt.Context.Processed.Add(cell);

            if (!rt.Context.ChainExecutionOrder.Contains(cell))
                rt.Context.ChainExecutionOrder.Add(cell);

            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] {ownerLabel} EXECUTE special={special} cell={cell}");

            rt.Context.Processed.Remove(cell);
            var handledWithoutFinalize = TryExecuteNestedLineWithoutFinalize(rt, pending, tile, ownerLabel);
            var nestedActions = handledWithoutFinalize
                ? null
                : rt.ExecuteSpecialActions?.Invoke(rt.Context, tile, null);
            rt.Context.Processed.Add(cell);

            if (nestedActions != null && nestedActions.Count > 0)
            {
                rt.DebugLog?.Invoke(
                    $"[LineVHPulseCoreCombo] {ownerLabel} IGNORE nested finalize actions count={nestedActions.Count} from {special} at {cell}; final {ownerLabel} clear owns the accumulated context");
            }

            EnqueueNewlyAffectedSpecials(rt, pending);
        }
    }

    private bool TryExecuteNestedLineWithoutFinalize(
        LineVHPulseCoreComboExecutionRuntime rt,
        Queue<TileView> pending,
        TileView tile,
        string ownerLabel)
    {
        if (rt == null || pending == null || tile == null)
            return false;

        var special = tile.GetSpecial();
        if (special == TileSpecial.LineV)
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] {ownerLabel} nested LineV no-finalize cell=({tile.X},{tile.Y})");
            var lineV = new LineVSpecial();
            lineV.Execute(new LineVExecutionRuntime
            {
                Board = rt.Board,
                Context = rt.Context,
                Origin = tile,
                Partner = null,
                FinalizeAtEnd = false,
                SuppressVisualSideEffects = false,
                ActivateSpecial = (resolution, nestedTile, partner) =>
                {
                    rt.ExecuteSpecialActions?.Invoke(resolution, nestedTile, partner);
                },
                EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
                ProcessQueue = resolution => { }
            });
            return true;
        }

        if (special == TileSpecial.LineH)
        {
            rt.DebugLog?.Invoke($"[LineVHPulseCoreCombo] {ownerLabel} nested LineH no-finalize cell=({tile.X},{tile.Y})");
            var lineH = new LineHSpecial();
            lineH.Execute(new LineHExecutionRuntime
            {
                Board = rt.Board,
                Context = rt.Context,
                Origin = tile,
                Partner = null,
                FinalizeAtEnd = false,
                SuppressVisualSideEffects = false,
                ActivateSpecial = (resolution, nestedTile, partner) =>
                {
                    rt.ExecuteSpecialActions?.Invoke(resolution, nestedTile, partner);
                },
                EnqueueChainSpecials = resolution => EnqueueNewlyAffectedSpecials(rt, pending),
                ProcessQueue = resolution => { }
            });
            return true;
        }

        return false;
    }

    private static void RemoveDeferredOverrideOriginsFromClear(
        HashSet<TileView> affected,
        HashSet<Vector2Int> affectedCells,
        LineVHPulseCoreComboExecutionRuntime rt)
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

            affected.Remove(tile);
            affectedCells.Remove(cell);
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
        Action<Vector2Int> emitPulseEmitterComboTriggered,
        ICollection<Vector2Int> deferredLineHitOverrideCells,
        ResolutionContext context,
        Func<ResolutionContext, TileView, TileView, List<BoardAction>> executeSpecialActions,
        TileView orbitLineTile,
        TileView orbitPulseTile)
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

        var protectedOverrideCells = new HashSet<Vector2Int>();
        if (deferredLineHitOverrideCells != null)
        {
            foreach (var cell in deferredLineHitOverrideCells)
            {
                if (!targets.Contains(cell))
                    continue;

                if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height)
                    continue;

                var tile = board.Tiles[cell.x, cell.y];
                if (tile == null)
                    continue;

                if (tile.GetSpecial() != TileSpecial.SystemOverride)
                    continue;

                protectedOverrideCells.Add(cell);
            }
        }

        return new LineVHPulseCoreComboAction(
            board,
            targets,
            hOrigins,
            vOrigins,
            targetVisuals,
            protectedOverrideCells,
            new Vector2Int(cx, cy),
            emitPulseEmitterComboTriggered,
            context,
            executeSpecialActions,
            orbitLineTile,
            orbitPulseTile);
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
    private readonly HashSet<Vector2Int> protectedOverrideCells;
    private readonly Vector2Int comboCenterCell;
    private readonly Action<Vector2Int> emitPulseEmitterComboTriggered;
    private readonly ResolutionContext context;
    private readonly Func<ResolutionContext, TileView, TileView, List<BoardAction>> executeSpecialActions;
    private readonly Queue<BoardAction> pendingInlineActions = new();
    private readonly TileView orbitLineTile;
    private readonly TileView orbitPulseTile;

    // Orbit intro animasyon ayarları
    private const float OrbitRiseHeight = 60f;     // yukarı kalkma yüksekliği (pixel)
    private const float OrbitRiseDuration = 0.20f; // yukarı kalkma süresi
    private const float OrbitSpinDuration = 0.50f; // dönme süresi
    private const float OrbitTurns = 2f;            // tam tur sayısı
    private const float OrbitGapPixels = 3f;        // ikonlar arası ekstra boşluk

    public LineVHPulseCoreComboAction(
       BoardController board,
       HashSet<Vector2Int> targets,
       List<(Vector2Int cell, Vector2 anch)> hOrigins,
       List<(Vector2Int cell, Vector2 anch)> vOrigins,
       Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals,
       HashSet<Vector2Int> protectedOverrideCells,
       Vector2Int comboCenterCell,
       Action<Vector2Int> emitPulseEmitterComboTriggered,
       ResolutionContext context,
       Func<ResolutionContext, TileView, TileView, List<BoardAction>> executeSpecialActions,
       TileView orbitLineTile,
       TileView orbitPulseTile)
    {
        this.board = board;
        this.targets = targets;
        this.hOrigins = hOrigins;
        this.vOrigins = vOrigins;
        this.targetVisuals = targetVisuals;
        this.protectedOverrideCells = protectedOverrideCells ?? new HashSet<Vector2Int>();
        this.comboCenterCell = comboCenterCell;
        this.emitPulseEmitterComboTriggered = emitPulseEmitterComboTriggered;
        this.context = context;
        this.executeSpecialActions = executeSpecialActions;
        this.orbitLineTile = orbitLineTile;
        this.orbitPulseTile = orbitPulseTile;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        // ===== ORBIT INTRO ANIMASYONU =====
        // Akış:
        //  1) Her iki tile'a PlaySpecialCreationReveal çağır → halo + grow efekti (proje stili)
        //  2) Tile'ları yukarı kaldır
        //  3) Pivot etrafında birbirinin etrafında döndür (aralarında 2-3 px gap)
        //  4) Başlangıç pozisyonlarına geri koy
        //  5) Mevcut akış (line travel + clear) normal çalışsın
        Debug.Log($"[OrbitIntro] ENTER line={(orbitLineTile != null ? $"({orbitLineTile.X},{orbitLineTile.Y})" : "NULL")} pulse={(orbitPulseTile != null ? $"({orbitPulseTile.X},{orbitPulseTile.Y})" : "NULL")}");

        if (orbitLineTile != null && orbitPulseTile != null
            && orbitLineTile.gameObject != null && orbitPulseTile.gameObject != null)
        {
            var lineRect = orbitLineTile.GetComponent<RectTransform>();
            var pulseRect = orbitPulseTile.GetComponent<RectTransform>();

            if (lineRect != null && pulseRect != null)
            {
                // HALO yok — sadece yukarı kalkma + dönme. Önce temelin çalıştığını görelim.
                // (Daha sonra projedeki halo metoduyla zenginleştirebiliriz)

                // Başlangıç pozisyonlarını kaydet
                Vector2 lineStart = lineRect.anchoredPosition;
                Vector2 pulseStart = pulseRect.anchoredPosition;
                Vector2 pivot = (lineStart + pulseStart) * 0.5f;

                // En üstte gözüksün
                int lineSibling = lineRect.GetSiblingIndex();
                int pulseSibling = pulseRect.GetSiblingIndex();
                lineRect.SetAsLastSibling();
                pulseRect.SetAsLastSibling();

                // ===== AŞAMA A: yukarı kalkma =====
                float t = 0f;
                while (t < OrbitRiseDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / OrbitRiseDuration);
                    float eased = k * k * (3f - 2f * k);

                    float rise = OrbitRiseHeight * eased;
                    lineRect.anchoredPosition = lineStart + new Vector2(0f, rise);
                    pulseRect.anchoredPosition = pulseStart + new Vector2(0f, rise);

                    yield return null;
                }

                // Yükselmiş pivot
                Vector2 risenPivot = pivot + new Vector2(0f, OrbitRiseHeight);

                // Yarıçap = orijinal mesafenin yarısı + 2-3 px gap
                float halfDist = Vector2.Distance(lineStart, pulseStart) * 0.5f;
                float radius = halfDist + OrbitGapPixels;

                // Başlangıç açısı (pulse'un pivota göre yönü)
                Vector2 pulseDir = pulseStart - pivot;
                if (pulseDir.sqrMagnitude < 0.0001f) pulseDir = Vector2.right;
                float startAngle = Mathf.Atan2(pulseDir.y, pulseDir.x);

                // ===== AŞAMA B: pivot etrafında dönüş =====
                float twoPi = Mathf.PI * 2f;
                t = 0f;
                while (t < OrbitSpinDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / OrbitSpinDuration);
                    float eased = k * k * (3f - 2f * k);

                    float angle = startAngle + eased * OrbitTurns * twoPi;

                    Vector2 pulseOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Vector2 lineOffset = -pulseOffset;

                    pulseRect.anchoredPosition = risenPivot + pulseOffset;
                    lineRect.anchoredPosition = risenPivot + lineOffset;

                    yield return null;
                }

                // ===== BİTİŞ: pozisyonları başlangıca geri koy =====
                lineRect.anchoredPosition = lineStart;
                pulseRect.anchoredPosition = pulseStart;

                // Sibling sırasını geri al
                lineRect.SetSiblingIndex(lineSibling);
                pulseRect.SetSiblingIndex(pulseSibling);

                Debug.Log("[OrbitIntro] DONE");
            }
        }
        // ===== /ORBIT INTRO ANIMASYONU =====

        var cleared = new HashSet<Vector2Int>();
        var hiddenOrigins = new HashSet<TileView>();

        bool IsProtected(Vector2Int cell) => protectedOverrideCells.Contains(cell);

        foreach (var h in hOrigins)
        {
            var view = board.GetTileViewAt(h.cell.x, h.cell.y);
            if (view != null && hiddenOrigins.Add(view))
            {
                SpecialVisualService.HideTileVisualForCombo(view);

                if (!IsProtected(h.cell) &&
                    cleared.Add(h.cell) &&
                    targetVisuals.TryGetValue(h.cell, out var originVisual))
                {
                    board.ClearCellVisualOnly(h.cell, originVisual.type, originVisual.view);
                }
            }
        }

        foreach (var v in vOrigins)
        {
            var view = board.GetTileViewAt(v.cell.x, v.cell.y);
            if (view != null && hiddenOrigins.Add(view))
            {
                SpecialVisualService.HideTileVisualForCombo(view);

                if (!IsProtected(v.cell) &&
                    cleared.Add(v.cell) &&
                    targetVisuals.TryGetValue(v.cell, out var originVisual))
                {
                    board.ClearCellVisualOnly(v.cell, originVisual.type, originVisual.view);
                }
            }
        }
        if (board.lineTravelPlayer == null)
        {
            bool IsComboOrigin(Vector2Int cell) =>
                hOrigins.Exists(h => h.cell == cell) ||
                vOrigins.Exists(v => v.cell == cell);

            foreach (var kvp in targetVisuals)
            {
                var cell = kvp.Key;

                if (IsProtected(cell))
                    continue;

                var tile = board.GetTileViewAt(cell.x, cell.y);

                if (tile != null && tile.GetSpecial() != TileSpecial.None && !IsComboOrigin(cell))
                {
                    context.Queued.Remove(cell);
                    context.Processed.Remove(cell);

                    var nestedActions = executeSpecialActions?.Invoke(context, tile, null);

                    context.Processed.Add(cell);

                    if (nestedActions != null)
                    {
                        foreach (var action in nestedActions)
                        {
                            if (action != null)
                                yield return action.ExecuteVisuals(sequencer);
                        }
                    }

                    continue;
                }

                board.ClearCellDataOnly(cell);
                board.ClearCellVisualOnly(cell, kvp.Value.type, kvp.Value.view);
            }

            yield break;
        }

        // emitPulseEmitterComboTriggered?.Invoke(comboCenterCell);

        void OnStep(Vector2Int cell)
        {
            if (!targets.Contains(cell))
                return;

            if (IsProtected(cell))
                return;

            if (!cleared.Add(cell))
                return;

            var tile = board.GetTileViewAt(cell.x, cell.y);

            if (tile != null && tile.GetSpecial() != TileSpecial.None)
            {
                var specialCell = new Vector2Int(tile.X, tile.Y);

                bool isComboOrigin =
                    hOrigins.Exists(h => h.cell == specialCell) ||
                    vOrigins.Exists(v => v.cell == specialCell);

                if (!isComboOrigin && context != null)
                {
                    context.Queued.Remove(specialCell);
                    context.Processed.Remove(specialCell);

                    var nestedActions = executeSpecialActions?.Invoke(context, tile, null);

                    context.Processed.Add(specialCell);

                    if (nestedActions != null)
                    {
                        foreach (var action in nestedActions)
                        {
                            if (action != null)
                                pendingInlineActions.Enqueue(action);
                        }
                    }
                }

                return;
            }

            board.ClearCellDataOnly(cell);

            if (targetVisuals.TryGetValue(cell, out var visualData))
                board.ClearCellVisualOnly(cell, visualData.type, visualData.view);
        }

        int pendingTravels = 0;
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

            while (pendingInlineActions.Count > 0)
            {
                var action = pendingInlineActions.Dequeue();
                if (action != null)
                    yield return action.ExecuteVisuals(sequencer);
            }

            yield return null;
        }

        while (pendingInlineActions.Count > 0)
        {
            var action = pendingInlineActions.Dequeue();
            if (action != null)
                yield return action.ExecuteVisuals(sequencer);
        }

        foreach (var kvp in targetVisuals)
        {
            if (IsProtected(kvp.Key))
                continue;

            if (!cleared.Add(kvp.Key))
                continue;

            var tile = board.GetTileViewAt(kvp.Key.x, kvp.Key.y);

            if (tile == null || tile.GetSpecial() == TileSpecial.None)
            {
                board.ClearCellDataOnly(kvp.Key);
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            }
        }
    }
}