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

    private void AddCell(
        LineVHPulseCoreComboExecutionRuntime rt,
        int x,
        int y,
        List<TileView> comboLightningVisualTargets)
    {
        if (x < 0 || x >= rt.Board.Width || y < 0 || y >= rt.Board.Height)
            return;

        if (!SpecialUtils.CanAffectCell(rt.Board, x, y))
        {
            // Emitter blockers (HatLauncher/EnergyContainer) inside the combo footprint still emit once.
            SpecialCellUtils.TryMarkEmitterImpact(rt.Context, rt.Board, x, y);
            return;
        }

        var cell = new Vector2Int(x, y);
        SpecialCellUtils.MarkAffectedCell(rt.Context, x, y, rt.Board);

        var tile = rt.Board.GetTileViewAt(x, y);
        if (tile == null)
            return;

        if (!SpecialUtils.CanTargetTileContent(rt.Board, x, y))
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

        // Tile parent is used as fallback when board.Tiles[x,y] is null (combo tiles are
        // data-cleared before ExecuteVisuals runs). Parent transform is stable and gives
        // correct world positions for any grid cell regardless of which tile is the center.
        var anyTileRect = orbitPulseTile?.GetComponent<RectTransform>()
                          ?? orbitLineTile?.GetComponent<RectTransform>();
        var tileParent = anyTileRect?.parent as RectTransform;

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= board.Height)
                continue;

            Vector2 anchoredPos;
            var originTile = board.Tiles[cx, yy];
            if (originTile != null)
            {
                var originRect = originTile.GetComponent<RectTransform>();
                var worldCenter = originRect.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
                anchoredPos = board.WorldToAnchoredIn(space, worldCenter);
            }
            else if (tileParent != null)
            {
                var localCenter = new Vector3(cx * board.TileSize + board.TileSize * 0.5f,
                                              -yy * board.TileSize - board.TileSize * 0.5f, 0f);
                anchoredPos = board.WorldToAnchoredIn(space, tileParent.TransformPoint(localCenter));
            }
            else
                continue;

            hOrigins.Add((new Vector2Int(cx, yy), anchoredPos));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= board.Width)
                continue;

            Vector2 anchoredPos;
            var originTile = board.Tiles[xx, cy];
            if (originTile != null)
            {
                var originRect = originTile.GetComponent<RectTransform>();
                var worldCenter = originRect.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
                anchoredPos = board.WorldToAnchoredIn(space, worldCenter);
            }
            else if (tileParent != null)
            {
                var localCenter = new Vector3(xx * board.TileSize + board.TileSize * 0.5f,
                                              -cy * board.TileSize - board.TileSize * 0.5f, 0f);
                anchoredPos = board.WorldToAnchoredIn(space, tileParent.TransformPoint(localCenter));
            }
            else
                continue;

            vOrigins.Add((new Vector2Int(xx, cy), anchoredPos));
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
    private int runningNested;   // aynı anda çalışan nested (pulse vb.) special sayısı — paralel yürütme
    private readonly TileView orbitLineTile;
    private readonly TileView orbitPulseTile;

    // Sadece orbit intro'yu oynatıp duran mod (combo'nun gerçek temizliği SpecialChainRunner'da).
    private readonly bool introOnly;

    // Orbit intro animasyon ayarları
    private const float OrbitRiseHeight = 36f;          // ghost ikonların yukarı kalkma yüksekliği (pixel)
    private const float OrbitRiseDuration = 0.12f;      // kısa sahneye alma süresi
    private readonly float orbitSpinDuration = 0.55f;   // PulseCore/emitter ana dönüş süresi (kısaltıldı: eski 1.00 → takılma azaltma; intro-only: 0.75)
    private const float EmitterOrbitTurns = 2f;         // emitter PulseCore etrafında Y eksenli orbit yapar
    private const float EmitterSelfSpinTurns = 2f;      // emitter orbit sırasında kendi etrafında döner
    private const float PulseCoreSelfSpinTurns = 2f;    // PulseCore kendi merkezinde döner
    private const float OrbitGapPixels = 3f;            // ikon kenarları arasındaki boşluk
    private const float LightningThickness = 5f;        // bağ şimşeği kalınlığı
    private const float LightningJitter = 8f;           // şimşeğin kırıklı görünmesi için sapma

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

    // Sadece orbit intro: gerçek temizlik artık SpecialChainRunner (cross) tarafından yapılıyor.
    public LineVHPulseCoreComboAction(
        BoardController board,
        TileView orbitLineTile,
        TileView orbitPulseTile,
        float orbitSpinDuration)
    {
        this.board = board;
        this.orbitLineTile = orbitLineTile;
        this.orbitPulseTile = orbitPulseTile;
        this.orbitSpinDuration = Mathf.Max(0.01f, orbitSpinDuration);
        this.introOnly = true;
        this.protectedOverrideCells = new HashSet<Vector2Int>();
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        yield return PlayOrbitIntroGhost();

        // Intro-only mod: gösteriyi oynat ve dur (cross'u SpecialChainRunner sürüyor).
        if (introOnly)
            yield break;


        var cleared = new HashSet<Vector2Int>();
        var hiddenOrigins = new HashSet<TileView>();

        bool IsProtected(Vector2Int cell) => protectedOverrideCells.Contains(cell);

        // Magnet: bu combo bir uca EN FAZLA 1 hit vursun. Uç vurulunca içeri kayıp yeni hücre
        // "güncel uç" olur; snapshot olmadan combo o hücreyi de işleyip ucu tüm path boyunca
        // yürütür (magnet tek combo'da komple biter). Çözüm: hasar fazından ÖNCE güncel uç
        // hücrelerini dondur; yalnız bunları (her biri 1 kez) vur → kapsanan uç başına 1 hit.
        var magnetEndpointSnapshot = new HashSet<Vector2Int>();
        if (board.ObstacleStateService != null)
        {
            void SnapMagnet(Vector2Int c)
            {
                if (board.ObstacleStateService.GetObstacleIdAt(c.x, c.y) == ObstacleId.Magnet
                    && board.ObstacleStateService.IsMagnetEndpoint(c.x, c.y))
                    magnetEndpointSnapshot.Add(c);
            }
            foreach (var c in targets) SnapMagnet(c);
            foreach (var c in targetVisuals.Keys) SnapMagnet(c);
        }

        // Magnet hücresi mi? Öyleyse yalnız snapshot'taki güncel uç bir kez vurulur; orta yol
        // ya da kaymış-uç hücreleri no-op. Dönen true → çağıran tile-clear ETMEZ (magnet korunur).
        bool TryHitMagnet(Vector2Int cell)
        {
            if (board.ObstacleStateService == null
                || board.ObstacleStateService.GetObstacleIdAt(cell.x, cell.y) != ObstacleId.Magnet)
                return false;

            if (magnetEndpointSnapshot.Remove(cell))
            {
                var magHit = board.ApplyObstacleDamageAt(cell.x, cell.y, ObstacleHitContext.SpecialActivation);
                if (magHit.didHit) board.TriggerObstacleVisualChange(magHit.visualChange);
            }
            return true;
        }

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

                // Magnet: yalnız snapshot'taki güncel uç bir kez küçülür (uç-yürüyüşü engellendi).
                // Magnet hücresi tile-clear EDİLMEZ.
                if (TryHitMagnet(cell))
                    continue;

                if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(cell.x, cell.y))
                {
                    var movableHit = board.ApplyObstacleDamageAt(cell.x, cell.y, ObstacleHitContext.SpecialActivation);
                    if (movableHit.didHit) board.TriggerObstacleVisualChange(movableHit.visualChange);
                    continue;
                }

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

            // Magnet: yalnız snapshot'taki güncel uç bir kez küçülür (uç-yürüyüşü engellendi).
            if (TryHitMagnet(cell))
                return;

            // Movable obstacle (Plastic vb.): tile-clear değil obstacle hasarı —
            // yıkılırsa view'ı HandleObstacleDestroyed kaldırır; çok-hit'liyse view'la
            // birlikte yaşamaya devam eder (orphan-state önlenir).
            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(cell.x, cell.y))
            {
                var movableHit = board.ApplyObstacleDamageAt(cell.x, cell.y, ObstacleHitContext.SpecialActivation);
                if (movableHit.didHit) board.TriggerObstacleVisualChange(movableHit.visualChange);
                return;
            }

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

            // Görsel temizlik CANLI view ile: gravity araya girdiyse hücredeki taş,
            // action başında snapshot'lanan (targetVisuals) view olmayabilir — eski
            // view'ı yok etmek "data yaşıyor, görsel öldü" orphan'ı üretir.
            if (tile != null)
            {
                board.BreakFx?.PlayTileBreak(tile);
                board.ClearCellDataOnly(cell);
                board.ClearCellVisualOnly(cell, tile.GetTileType(), tile);
            }
            else
            {
                board.ClearCellDataOnly(cell);
            }
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
            int stepsPos = (width - 1 - h.cell.x) + 3;
            int stepsNeg = h.cell.x + 3;
            expectedMaxDuration = Mathf.Max(
                expectedMaxDuration,
                board.lineTravelPlayer != null ? board.lineTravelPlayer.EstimateDuration(stepsPos, stepsNeg) : 0f);

            pendingTravels++;

            board.PlayLineTravelInstanceAsymmetric(
                LineTravelSplitSwapTestUI.LineAxis.Horizontal,
                h.anch,
                h.cell,
                stepsPos,
                stepsNeg,
                tileSize,
                0f,
                OnStep,
                () => makeCompletedLogger("H", h.cell));
        }

        foreach (var v in vOrigins)
        {
            int stepsPos = (height - 1 - v.cell.y) + 3;
            int stepsNeg = v.cell.y + 3;
            expectedMaxDuration = Mathf.Max(
                expectedMaxDuration,
                board.lineTravelPlayer != null ? board.lineTravelPlayer.EstimateDuration(stepsPos, stepsNeg) : 0f);

            pendingTravels++;

            board.PlayLineTravelInstanceAsymmetric(
                LineTravelSplitSwapTestUI.LineAxis.Vertical,
                v.anch,
                v.cell,
                stepsPos,
                stepsNeg,
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

            // Nested special'ları (yakalanan PulseCore vb.) SIRAYLA beklemek yerine PARALEL başlat:
            // hepsi aynı anda patlar, toplam süre = EN UZUN pulse (art arda toplama değil).
            while (pendingInlineActions.Count > 0)
            {
                var action = pendingInlineActions.Dequeue();
                if (action != null)
                    board.StartCoroutine(RunNestedParallel(action, sequencer));
            }

            yield return null;
        }

        while (pendingInlineActions.Count > 0)
        {
            var action = pendingInlineActions.Dequeue();
            if (action != null)
                board.StartCoroutine(RunNestedParallel(action, sequencer));
        }

        // Cascade'den (taş düşüşü) ÖNCE paralel nested pulse'ların bitmesini bekle — hücreler
        // temizlenmeden gravity başlarsa senkron bozulur. Süre = en uzun pulse (güvenlik timeout'lu).
        float nestedWait = 0f;
        const float nestedTimeout = 2.5f;
        while (runningNested > 0 && nestedWait < nestedTimeout)
        {
            nestedWait += Time.unscaledDeltaTime;
            yield return null;
        }

        foreach (var kvp in targetVisuals)
        {
            if (IsProtected(kvp.Key))
                continue;

            if (!cleared.Add(kvp.Key))
                continue;

            // Magnet: yalnız snapshot'taki güncel uç bir kez küçülür (uç-yürüyüşü engellendi).
            if (TryHitMagnet(kvp.Key))
                continue;

            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(kvp.Key.x, kvp.Key.y))
            {
                var movableHit = board.ApplyObstacleDamageAt(kvp.Key.x, kvp.Key.y, ObstacleHitContext.SpecialActivation);
                if (movableHit.didHit) board.TriggerObstacleVisualChange(movableHit.visualChange);
                continue;
            }

            var tile = board.GetTileViewAt(kvp.Key.x, kvp.Key.y);

            if (tile == null || tile.GetSpecial() == TileSpecial.None)
            {
                board.ClearCellDataOnly(kvp.Key);
                if (tile != null)
                    board.ClearCellVisualOnly(kvp.Key, tile.GetTileType(), tile);
            }
        }

        // Beam'ler bitti, hücreler boşaldı: taş düşüşü HEMEN başlasın (SweepLine ile
        // aynı ilerlemeli his). Arkadan gelen sessiz MatchClearAction origin tile'larını
        // ve obstacle hasarını işler; kalan boşlukları board resolve cascade'leri doldurur.
        var cascades = board.CascadeLogic.CalculateCascades();
        if (cascades != null)
        {
            for (int i = 0; i < cascades.Count; i++)
                board.StartImmediateAction(cascades[i]);
        }
        board.RefreshAllSortingOrders();
    }
    // Nested special'ı ayrı bir coroutine olarak çalıştırır; çalışan sayacını tutar ki
    // ana akış cascade'den önce hepsinin bitmesini (paralel) bekleyebilsin.
    private IEnumerator RunNestedParallel(BoardAction action, ActionSequencer sequencer)
    {
        runningNested++;
        yield return action.ExecuteVisuals(sequencer);
        runningNested = Mathf.Max(0, runningNested - 1);
    }

    private IEnumerator PlayOrbitIntroGhost()
    {
        if (board == null || orbitLineTile == null || orbitPulseTile == null)
            yield break;

        var emitterSprite = ResolveOrbitSprite(orbitLineTile);
        var pulseSprite = ResolveOrbitSprite(orbitPulseTile);

        if (emitterSprite == null || pulseSprite == null)
            yield break;

        var parent = ResolveOrbitGhostParent();
        if (parent == null)
            yield break;

        Vector2 emitterStart = GetTileCenterAnchoredPosition(parent, orbitLineTile);
        Vector2 pulseStart = GetTileCenterAnchoredPosition(parent, orbitPulseTile);

        var orbitGo = new GameObject("LinePulseYOrbitIntroGhost", typeof(RectTransform), typeof(CanvasGroup));
        var orbitRt = orbitGo.GetComponent<RectTransform>();
        orbitRt.SetParent(parent, false);
        orbitRt.SetAsLastSibling();
        orbitRt.anchorMin = new Vector2(0.5f, 0.5f);
        orbitRt.anchorMax = new Vector2(0.5f, 0.5f);
        orbitRt.pivot = new Vector2(0.5f, 0.5f);
        orbitRt.sizeDelta = new Vector2(board.TileSize * 3.80f, board.TileSize * 3.80f);
        orbitRt.anchoredPosition = pulseStart;
        orbitRt.localScale = Vector3.one;
        orbitRt.localRotation = Quaternion.identity;

        var cg = orbitGo.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        float iconSize = ResolveOrbitIconSize();
        float orbitRadius = iconSize + OrbitGapPixels;

        var lightningSegments = CreateLightningSegments(orbitRt, 4);
        var pulseImage = CreateOrbitGhostImage(orbitRt, "PulseCoreGhost", pulseSprite, iconSize);
        var emitterImage = CreateOrbitGhostImage(orbitRt, "EmitterGhost", emitterSprite, iconSize);

        pulseImage.rectTransform.anchoredPosition = Vector2.zero;
        emitterImage.rectTransform.anchoredPosition = Vector2.right * orbitRadius;

        // Combo pulse ghost'una idle fitili ÖLÇEKLİ bas: doğru koordinat = fuseLocalOffset * (iconSize/TileSize).
        // Ghost pulse döndükçe fitil onunla döner (child).
        orbitPulseTile?.StartComboFuse(pulseImage.rectTransform, iconSize / Mathf.Max(1f, board.TileSize), 3.1f, 0f);

        // Gerçek tile root'larını oynatma; sadece ghost ikonları oynat.
        // Böylece beyaz tile/background karesi görünmez.
        HideTileCanvasGroup(orbitLineTile);
        HideTileCanvasGroup(orbitPulseTile);

        // Kısa sahneye alma: PulseCore merkezine taşınıp biraz yukarı alınır.
        float t = 0f;
        Vector2 sceneStart = pulseStart;
        Vector2 sceneEnd = pulseStart + new Vector2(0f, OrbitRiseHeight);
        Vector2 emitterLocalStart = emitterStart - pulseStart;
        if (emitterLocalStart.sqrMagnitude < 0.0001f)
            emitterLocalStart = Vector2.right * orbitRadius;
        else
            emitterLocalStart = emitterLocalStart.normalized * orbitRadius;

        while (t < OrbitRiseDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, OrbitRiseDuration));
            float eased = EaseInOut(k);

            orbitRt.anchoredPosition = Vector2.LerpUnclamped(sceneStart, sceneEnd, eased);
            orbitRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.96f, 1.04f, eased);

            Vector2 emitterPos = Vector2.LerpUnclamped(emitterLocalStart, Vector2.right * orbitRadius, eased);
            pulseImage.rectTransform.anchoredPosition = Vector2.zero;
            emitterImage.rectTransform.anchoredPosition = emitterPos;
            UpdateLightningSegments(lightningSegments, Vector2.zero, emitterPos, 1f, 0f);

            yield return null;
        }

        orbitRt.anchoredPosition = sceneEnd;
        orbitRt.localScale = Vector3.one;

        // Ana 2 saniyelik animasyon:
        // 1) PulseCore merkezde kalır ve kendi merkezinde döner.
        // 2) Emitter, PulseCore etrafında Y eksenli orbit yapar.
        //    Y ekseni hissi için ekranda sağ-sol hareket + öne/arkaya scale/alpha kullanılır.
        // 3) Emitter orbit yaparken kendi merkezinde de döner.
        // 4) PulseCore ve emitter arasındaki 3 px boşluk korunur; aralarında kırıklı lightning bağ kalır.
        t = 0f;
        float twoPi = Mathf.PI * 2f;
        while (t < orbitSpinDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, orbitSpinDuration));
            float linear = k;

            float emitterOrbitAngle = linear * EmitterOrbitTurns * twoPi;
            float x = Mathf.Cos(emitterOrbitAngle) * orbitRadius;
            float depth = Mathf.Sin(emitterOrbitAngle); // +1: önde, -1: arkada

            Vector2 emitterPos = new Vector2(x, 0f);
            emitterImage.rectTransform.anchoredPosition = emitterPos;
            pulseImage.rectTransform.anchoredPosition = Vector2.zero;

            float pulseSelfZ = -linear * PulseCoreSelfSpinTurns * 360f;
            float emitterSelfZ = linear * EmitterSelfSpinTurns * 360f;

            pulseImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, pulseSelfZ);
            emitterImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, emitterSelfZ);

            float depth01 = (depth + 1f) * 0.5f;
            float emitterScale = Mathf.LerpUnclamped(0.72f, 1.12f, depth01);
            float emitterAlpha = Mathf.LerpUnclamped(0.42f, 1f, depth01);

            emitterImage.rectTransform.localScale = Vector3.one * emitterScale;
            SetImageAlpha(emitterImage, emitterAlpha);

            float pulsePunch = 1f + Mathf.Sin(linear * Mathf.PI * 4f) * 0.035f;
            pulseImage.rectTransform.localScale = Vector3.one * pulsePunch;
            SetImageAlpha(pulseImage, 1f);

            UpdateLightningSegments(lightningSegments, Vector2.zero, emitterPos, Mathf.LerpUnclamped(0.55f, 1f, depth01), linear);

            // Emitter arkaya geçtiğinde PulseCore'un arkasında görünsün,
            // öne geldiğinde üstte görünsün. Lightning her zaman ikonların arasında kalsın.
            if (depth < 0f)
            {
                foreach (var segment in lightningSegments)
                    if (segment != null) segment.rectTransform.SetSiblingIndex(0);
                emitterImage.rectTransform.SetSiblingIndex(1);
                pulseImage.rectTransform.SetAsLastSibling();
            }
            else
            {
                pulseImage.rectTransform.SetSiblingIndex(0);
                foreach (var segment in lightningSegments)
                    if (segment != null) segment.rectTransform.SetAsLastSibling();
                emitterImage.rectTransform.SetAsLastSibling();
            }

            yield return null;
        }

        pulseImage.rectTransform.anchoredPosition = Vector2.zero;
        pulseImage.rectTransform.localRotation = Quaternion.identity;
        pulseImage.rectTransform.localScale = Vector3.one;
        SetImageAlpha(pulseImage, 1f);

        emitterImage.rectTransform.anchoredPosition = Vector2.right * orbitRadius;
        emitterImage.rectTransform.localRotation = Quaternion.identity;
        emitterImage.rectTransform.localScale = Vector3.one;
        SetImageAlpha(emitterImage, 1f);
        UpdateLightningSegments(lightningSegments, Vector2.zero, Vector2.right * orbitRadius, 1f, 0f);

        // Çok kısa kapanış; bundan sonra mevcut LineV/H tetiklenir.
        // Settle hedefi: PulseCore'un yeni (swap sonrası) konumu — comboCenterCell değil,
        // çünkü comboCenterCell her zaman PulseCore hücresi olmayabilir (Line sürüklenirse a=Line).
        const float settleDuration = 0.08f;
        Vector2 endCenter = pulseStart;
        Vector2 settleStart = orbitRt.anchoredPosition;

        t = 0f;
        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, settleDuration));
            float eased = EaseInOut(k);

            orbitRt.anchoredPosition = Vector2.LerpUnclamped(settleStart, endCenter, eased);
            orbitRt.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.90f, eased);
            cg.alpha = Mathf.LerpUnclamped(1f, 0f, eased);

            yield return null;
        }

        if (orbitRt != null)
            UnityEngine.Object.Destroy(orbitRt.gameObject);
    }

    private static List<UnityEngine.UI.Image> CreateLightningSegments(RectTransform parent, int count)
    {
        var segments = new List<UnityEngine.UI.Image>();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"LightningLink_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1f, LightningThickness);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(0.55f, 0.95f, 1f, 0.95f);
            segments.Add(image);
        }

        return segments;
    }

    private static void UpdateLightningSegments(
        List<UnityEngine.UI.Image> segments,
        Vector2 from,
        Vector2 to,
        float alpha,
        float phase)
    {
        if (segments == null || segments.Count == 0)
            return;

        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
        {
            foreach (var segment in segments)
                SetImageAlpha(segment, 0f);
            return;
        }

        Vector2 dir = delta / distance;
        Vector2 normal = new Vector2(-dir.y, dir.x);

        var points = new Vector2[segments.Count + 1];
        points[0] = from;
        points[segments.Count] = to;

        for (int i = 1; i < points.Length - 1; i++)
        {
            float p = i / (float)segments.Count;
            float sign = (i % 2 == 0) ? -1f : 1f;
            float flicker = Mathf.Sin((phase * 24f + i * 1.73f) * Mathf.PI) * 0.45f;
            points[i] = Vector2.LerpUnclamped(from, to, p) + normal * sign * LightningJitter * (1f + flicker);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment == null)
                continue;

            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 mid = (a + b) * 0.5f;
            Vector2 segDelta = b - a;
            float segLength = segDelta.magnitude;
            float angle = Mathf.Atan2(segDelta.y, segDelta.x) * Mathf.Rad2Deg;

            var rt = segment.rectTransform;
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(segLength, LightningThickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            var c = segment.color;
            c.a = alpha;
            segment.color = c;
        }
    }


    private Sprite ResolveOrbitSprite(TileView tile)
    {
        if (tile == null)
            return null;

        var special = tile.GetSpecial();
        if (board != null && special != TileSpecial.None)
        {
            var specialSprite = board.GetSpecialIcon(special);
            if (specialSprite != null)
                return specialSprite;
        }

        return tile.GetIconSprite();
    }

    private RectTransform ResolveOrbitGhostParent()
    {
        if (board == null)
            return null;

        // ContentRoot = spawnParent (tilesRoot.parent). Ghost burada SetAsLastSibling()
        // yapılınca gridLinesRoot ve obstaclesRoot'un üstünde render edilir.
        if (board.ContentRoot != null)
            return board.ContentRoot;

        if (board.Parent != null)
            return board.Parent;

        if (board.LineTravelSpawnParent is RectTransform lineTravelParent)
            return lineTravelParent;

        if (board.lineTravelPlayer != null && board.lineTravelPlayer.afterImageParent != null)
            return board.lineTravelPlayer.afterImageParent;

        if (orbitLineTile != null && orbitLineTile.transform.parent is RectTransform lineParent)
            return lineParent;

        if (orbitPulseTile != null && orbitPulseTile.transform.parent is RectTransform pulseParent)
            return pulseParent;

        return null;
    }

    private Vector2 GetTileCenterAnchoredPosition(RectTransform parent, TileView tile)
    {
        if (parent == null || tile == null)
            return Vector2.zero;

        var rt = tile.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
            return board.WorldToAnchoredIn(parent, worldCenter);
        }

        return GetCellAnchoredPosition(tile.X, tile.Y);
    }

    private Vector2 GetCellAnchoredPosition(int x, int y)
    {
        return new Vector2(
            x * board.TileSize + board.TileSize * 0.5f,
            -y * board.TileSize - board.TileSize * 0.5f);
    }

    private float ResolveOrbitIconSize()
    {
        float fallback = board != null ? board.TileSize * 1.08f : 56f;

        float lineSize = GetIconVisualSize(orbitLineTile, fallback);
        float pulseSize = GetIconVisualSize(orbitPulseTile, fallback);

        return Mathf.Max(1f, Mathf.Max(lineSize, pulseSize) * 1.22f);
    }

    private static float GetIconVisualSize(TileView tile, float fallback)
    {
        if (tile == null || tile.IconImage == null)
            return fallback;

        var rt = tile.IconImage.rectTransform;
        if (rt == null)
            return fallback;

        Vector2 size = rt.rect.size;
        if (size.x <= 1f || size.y <= 1f)
            size = rt.sizeDelta;

        float max = Mathf.Max(size.x, size.y);
        return max > 1f ? max : fallback;
    }

    private static UnityEngine.UI.Image CreateOrbitGhostImage(
        RectTransform parent,
        string name,
        Sprite sprite,
        float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        // Null sprite'lı Image düz beyaz kare çizer — sprite yoksa görünmez kalsın.
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.95f);

        return image;
    }

    private static void HideTileCanvasGroup(TileView tile)
    {
        if (tile == null)
            return;

        if (!tile.TryGetComponent<CanvasGroup>(out var cg))
            cg = tile.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    private static void SetImageAlpha(UnityEngine.UI.Image image, float alpha)
    {
        if (image == null)
            return;

        var color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

}
