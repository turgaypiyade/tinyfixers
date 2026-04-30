using System.Collections.Generic;
using UnityEngine;

public class CascadeLogic
{
    private readonly BoardController board;

    // ── Pooled / reusable buffers (zero GC per call) ──
    private readonly List<BoardAction> _actionsBuffer = new List<BoardAction>(8);

    // Per-column buffers for CalculateCollapseAndSpawn
    private readonly List<TileView> _colTiles = new List<TileView>(16);
    private readonly List<int> _colTargetY = new List<int>(16);
    private readonly List<float> _colDuration = new List<float>(16);
    private readonly List<int> _colDist = new List<int>(16);
    private readonly List<int> _colFromY = new List<int>(16);

    // Segment buffers
    private readonly List<int> _slots = new List<int>(16);
    private readonly List<TileView> _existing = new List<TileView>(16);

    // Slide fill
    private readonly HashSet<TileView> _movedThisPassSet = new HashSet<TileView>();
    private readonly HashSet<Vector2Int> _reservedSlideTargets = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> _slideTargetsSnapshot = new List<Vector2Int>(16);
    private readonly HashSet<Vector2Int> _verticalOnlySlideGaps = new HashSet<Vector2Int>();
    // NOT: _verticalOnlySlideColumns kaldırıldı (Aşama 1).
    // Kolon-bazlı kilit, aynı kolondaki bağımsız boşlukları yanlışlıkla
    // diagonal hedef olmaktan çıkarıyordu. Artık sadece hücre-bazlı kilit var.

    // Goal buffer
    private readonly List<TopHudController.ActiveGoal> _activeGoalsBuffer = new List<TopHudController.ActiveGoal>(4);

    public CascadeLogic(BoardController board)
    {
        this.board = board;
    }
    public List<BoardAction> CalculateCascades()
    {
        _actionsBuffer.Clear();

        // Cascade sadece FLOW ile doldurulabilir boşlukları çözer.
        //
        // ÖNEMLİ:
        // state.canAcceptTile olan ama taş akışı olmayan kapalı cepler
        // cascade boşluğu değildir. Bunlar MatchFinder/shuffle'ı bloklamamalı.
        //
        // Faz sırası:
        //   1) Dikey düşüş/spawn gidebildiği yere kadar.
        //   2) Diyagonal slide; obstacle içinden/üstünden geçiş yok.
        //   3) Slide sonrası tekrar dikey düşüş/spawn.
        //   4) Hâlâ flow-fillable boşluk varsa 1-2-3 tekrar.
        const int maxSettleCycles = 16;

        for (int cycle = 0; cycle < maxSettleCycles; cycle++)
        {
            bool movedThisCycle = false;

            movedThisCycle |= AppendVerticalSettleActions();

            if (!HasAnyEmptyPlayableCell())
                break;

            var slideAction = CalculateSlideFill();
            if (slideAction != null && slideAction.HasMoves)
            {
                _actionsBuffer.Add(slideAction);
                movedThisCycle = true;

                movedThisCycle |= AppendVerticalSettleActions();
            }

            if (!HasAnyEmptyPlayableCell())
                break;

            if (!movedThisCycle)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CascadeLogic] Flow-fillable empty cells remain but no cascade action was produced.");
#endif
                break;
            }
        }

        return new List<BoardAction>(_actionsBuffer);
    }


    private bool AppendVerticalSettleActions()
    {
        bool moved = false;

        // CalculateCollapseAndSpawn bir çağrıda segment içindeki taşları tamamen
        // sıkıştırır ve spawn'a bağlı boşlukları doldurur. Yine de obstacle/slide
        // gibi ara durumlarda güvenli olmak için sınırlı tekrar bırakıyoruz.
        const int maxVerticalPasses = 8;

        for (int i = 0; i < maxVerticalPasses; i++)
        {
            var action = CalculateCollapseAndSpawn();
            if (action == null || !action.HasMoves)
                break;

            _actionsBuffer.Add(action);
            moved = true;
        }

        return moved;
    }

    public FallAction CalculateCollapseAndSpawn()
    {
        board.IncrementFallGeneration();

        for (int xx = 0; xx < board.Width; xx++)
        {
            for (int yy = 0; yy < board.Height; yy++)
            {
                var tv = board.Tiles[xx, yy];
                if (tv != null)
                    tv.MarkPlannedToMoveThisFallPass(false);
            }
        }

        var action = new FallAction();
        bool spawnedMovableThisPass = false;

        for (int x = 0; x < board.Width; x++)
        {
            _colTiles.Clear();
            _colTargetY.Clear();
            _colDuration.Clear();
            _colDist.Clear();
            _colFromY.Clear();

            int segmentTop = board.Height - 1;
            while (segmentTop >= 0)
            {
                while (segmentTop >= 0 && IsGravityBlockedCell(x, segmentTop))
                    segmentTop--;

                if (segmentTop < 0)
                    break;

                int segmentBottom = segmentTop;
                while (segmentBottom >= 0 && !IsGravityBlockedCell(x, segmentBottom))
                    segmentBottom--;

                int topY = segmentBottom + 1;
                bool touchesSpawnEdge = IsSegmentConnectedToSpawnEdge(x, topY);

                _slots.Clear();
                _existing.Clear();

                for (int y = segmentTop; y >= topY; y--)
                {
                    if (!IsTileSlotCell(x, y)) continue;
                    _slots.Add(y);

                    if (board.Tiles[x, y] != null)
                        _existing.Add(board.Tiles[x, y]);
                }

                for (int i = 0; i < _slots.Count; i++)
                {
                    board.Tiles[x, _slots[i]] = null;
                    board.SyncTileData(x, _slots[i]);
                }

                for (int i = 0; i < _existing.Count && i < _slots.Count; i++)
                {
                    int targetY = _slots[i];
                    var tile = _existing[i];
                    int fromY = tile.Y;

                    if (fromY != targetY
                        && board.ObstacleStateService != null
                        && board.ObstacleStateService.IsMovableObstacleAt(x, fromY))
                    {
                        board.ObstacleStateService.MoveObstacle(x, fromY, x, targetY);
                    }

                    board.Tiles[x, targetY] = tile;
                    tile.SetCoords(x, targetY);
                    board.SyncTileData(x, targetY);

                    int dist = Mathf.Abs(targetY - fromY);
                    if (dist > 0)
                    {
                        tile.MarkPlannedToMoveThisFallPass(true);
                        float duration = board.GetFallDurationForMove(x, fromY, x, targetY);
                        _colTiles.Add(tile);
                        _colTargetY.Add(targetY);
                        _colDuration.Add(duration);
                        _colDist.Add(dist);
                        _colFromY.Add(fromY);
                    }
                }

                if (touchesSpawnEdge)
                {
                    int nextSpawnY = topY + board.SpawnStartOffsetY;

                    for (int y = topY; y <= segmentTop; y++)
                    {
                        if (!IsTileSlotCell(x, y)) continue;
                        if (board.Tiles[x, y] != null) continue;

                        int spawnFromY = nextSpawnY;
                        TileView view = null;

                        if (!spawnedMovableThisPass && TryPickMovableGoalToSpawn(out var goalObstacleId))
                        {
                            view = SpawnMovableObstacleTileForFall(x, y, spawnFromY, goalObstacleId);
                            if (view != null)
                                spawnedMovableThisPass = true;
                        }

                        if (view == null)
                        {
                            var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                            view = go.GetComponent<TileView>();

                            view.Init(board, x, y);
                            board.ConfigureTileView(view);
                            view.MarkPlannedToMoveThisFallPass(true);

                            view.SetCoords(x, spawnFromY);
                            view.SnapToGrid(board.TileSize);

                            view.SetCoords(x, y);
                            board.Tiles[x, y] = view;

                            view.SetType(GetRandomTypeAvoidingImmediateMatch(x, y));
                            view.SetSpecial(TileSpecial.None);
                            board.SyncTileData(x, y);
                            board.RefreshTileObstacleVisual(view);
                        }

                        //nextSpawnY--;

                        int dist = Mathf.Abs(y - spawnFromY);
                        float duration = board.GetFallDurationForMove(x, spawnFromY, x, y);

                        _colTiles.Add(view);
                        _colTargetY.Add(y);
                        _colDuration.Add(duration);
                        _colDist.Add(dist);
                        _colFromY.Add(spawnFromY);
                    }
                }

                segmentTop = segmentBottom - 1;
            }

            // Sütundaki maksimum mesafeyi bul — en geç inen taş bu
            // Alttaki taşlar bekler ki üstekilere yetişsin → sütun bütün halinde iner
            for (int i = 0; i < _colTiles.Count; i++)
            {
                var tile = _colTiles[i];
                int targetY = _colTargetY[i];
                int dist = _colDist[i];
                int fromY = _colFromY[i];

                bool useFallSettle = false;
                float settleDur = board.FallSettleDuration;
                float settleStr = board.FallSettleStrength;

                // Sadece gerçekten stabil bir desteğe oturuyorsa settle ver.
                // Referans videodaki his: havadaki zincir taşlara toplu jelly settle yok.
                if (board.ShouldEnableFallSettleThisPass() && dist > 0)
                {
                    int belowY = targetY + 1;

                    bool hasSupport =
                        belowY >= board.Height ||
                        IsGravityBlockedCell(x, belowY) ||
                        (belowY < board.Height &&
                         board.Tiles[x, belowY] != null &&
                         !board.Tiles[x, belowY].IsPlannedToMoveThisFallPass);

                    if (hasSupport)
                    {
                        useFallSettle = true;

                        float dist01 = Mathf.Clamp01((dist - 1f) / 4f);
                        settleDur *= Mathf.Lerp(0.92f, 1.12f, dist01);
                        settleStr *= Mathf.Lerp(0.88f, 1.08f, dist01);
                    }
                }

                action.AddMove(
                    tile,
                    x,
                    fromY,
                    x,
                    targetY,
                    _colDuration[i],
                    useFallSettle,
                    settleDur,
                    settleStr,
                    board.FallMoveCurve);
            }
        }

        board.RefreshAllTileObstacleVisuals();
        return action;
    }
    public FallAction CalculateSlideFill()
    {
        var action = new FallAction();
        _movedThisPassSet.Clear();
        _reservedSlideTargets.Clear();
        _slideTargetsSnapshot.Clear();

        PruneVerticalOnlySlideLocks();

        // ÖNEMLİ:
        // Diyagonal pass canlı board taramasıyla zincirleme çalışmamalı.
        // Önce bu pass başındaki hedef boşlukları snapshot alıyoruz.
        // Slide'ın açtığı yeni boşluklar bu pass içinde yeni diagonal target olmaz.
        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (IsSlideFillTarget(x, y))
                    _slideTargetsSnapshot.Add(new Vector2Int(x, y));
            }
        }

        for (int i = 0; i < _slideTargetsSnapshot.Count; i++)
        {
            int x = _slideTargetsSnapshot[i].x;
            int y = _slideTargetsSnapshot[i].y;

            // Önceki slide bu hedefi doldurmuş veya source column lock üretmiş olabilir.
            if (!IsSlideFillTarget(x, y))
                continue;

            bool TrySource(int sx, int sy)
            {
                if (!TryGetTileSource(sx, sy, out var sourceTile))
                    return false;

                if (sourceTile == null)
                    return false;

                // Dikey gravity her zaman öncelikli.
                // Kaynak taş kendi kolonunda aşağı gidebiliyorsa diagonal alma.
                if (CanTileFallStraightDown(sx, sy))
                    return false;

                return TryDiagonalFrom(sx, sy, x, y, _movedThisPassSet, action);
            }

            // Bir hedef boşluk yalnızca tek komşu sütundan doldurulur.
            // Sağ-üst (x+1) önce denenir, sol-üst (x-1) sonra.
            // Aşama 1: kaynak önceliği sağa çevrildi.
            bool _ = TrySource(x + 1, y - 1) || TrySource(x - 1, y - 1);
        }

        return action;
    }

    private FallAction CalculateCollapseColumns()
    {
        var action = new FallAction();

        for (int x = 0; x < board.Width; x++)
        {
            int segStartY = board.Height - 1;

            for (int y = board.Height - 1; y >= -1; y--)
            {
                bool isBoundary = (y == -1) || IsGravityBlockedCell(x, y);

                if (!isBoundary)
                    continue;

                int segEndY = y + 1;

                if (segEndY <= segStartY)
                {
                    _slots.Clear();

                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (!IsTileSlotCell(x, yy))
                            continue;

                        _slots.Add(yy);
                    }

                    _existing.Clear();

                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (!IsTileSlotCell(x, yy))
                            continue;

                        var tv = board.Tiles[x, yy];

                        if (tv != null)
                            _existing.Add(tv);
                    }

                    for (int i = 0; i < _slots.Count; i++)
                    {
                        board.Tiles[x, _slots[i]] = null;
                        board.SyncTileData(x, _slots[i]);
                    }

                    for (int i = 0; i < _existing.Count && i < _slots.Count; i++)
                    {
                        int toY = _slots[i];
                        var tile = _existing[i];
                        int fromY = tile.Y;

                        if (fromY != toY
                            && board.ObstacleStateService != null
                            && board.ObstacleStateService.IsMovableObstacleAt(x, fromY))
                        {
                            board.ObstacleStateService.MoveObstacle(x, fromY, x, toY);
                        }

                        board.Tiles[x, toY] = tile;
                        tile.SetCoords(x, toY);
                        board.SyncTileData(x, toY);

                        if (fromY != toY)
                        {
                            int dist = Mathf.Abs(toY - fromY);
                            float moveDuration = board.GetFallDurationForMove(x, fromY, x, toY);

                            bool useFallSettle = board.EnableFallSettle && dist > 0;
                            float settleDur = board.FallSettleDuration;
                            float settleStr = board.FallSettleStrength;

                            if (useFallSettle)
                            {
                                float dist01 = Mathf.Clamp01((dist - 1f) / 4f);
                                settleDur *= Mathf.Lerp(0.92f, 1.10f, dist01);
                                settleStr *= Mathf.Lerp(0.90f, 1.06f, dist01);
                            }

                            action.AddMove(
                                tile,
                                x,
                                fromY,
                                x,
                                toY,
                                moveDuration,
                                useFallSettle,
                                settleDur,
                                settleStr,
                                board.FallMoveCurve);
                        }
                    }
                }

                segStartY = y - 1;
            }
        }

        return action;
    }
    private bool TryDiagonalFrom(
      int fromX, int fromY,
      int toX, int toY,
      HashSet<TileView> movedThisPass,
      FallAction action)
    {
        int cax = fromX;
        int cay = toY;
        int cbx = toX;
        int cby = fromY;

        // Diagonal yol için EN AZ BİR köşe açık olsun yeter.
        // Obstacle bir köşeyi kapatabilir; ikisi de kapalıysa fiziksel yol yok.
        // (cax,cay) = kaynak sütun + hedef satır
        // (cbx,cby) = hedef sütun + kaynak satır
        bool cornerA = IsDiagonalPassableCell(cax, cay);
        bool cornerB = IsDiagonalPassableCell(cbx, cby);

        if (!cornerA && !cornerB)
            return false;

        return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, action);
    }
    private bool TrySlideFrom(
      int fromX, int fromY,
      int toX, int toY,
      HashSet<TileView> movedThisPass,
      FallAction action)
    {
        if (!TryGetTileSource(fromX, fromY, out var tile))
            return false;

        if (tile == null)
            return false;

        if (movedThisPass.Contains(tile))
            return false;

        if (!IsEmptyPlayableCell(toX, toY))
            return false;

        if (IsVerticalOnlySlideTarget(toX, toY))
            return false;

        var targetCell = new Vector2Int(toX, toY);
        if (_reservedSlideTargets.Contains(targetCell))
            return false;

        _reservedSlideTargets.Add(targetCell);

        if (board.ObstacleStateService != null
            && board.ObstacleStateService.IsMovableObstacleAt(fromX, fromY))
        {
            board.ObstacleStateService.MoveObstacle(fromX, fromY, toX, toY);
        }

        board.Tiles[fromX, fromY] = null;
        board.Tiles[toX, toY] = tile;

        tile.SetCoords(toX, toY);

        board.SyncTileData(fromX, fromY);
        board.SyncTileData(toX, toY);

        // Bu slide'ın açtığı kaynak boşluğu diagonal ile doldurulmayacak.
        // Aşama 1: sadece o hücre kilitlenir, kaynak sütunun tamamı değil.
        // Kaynak sütun aynı pass içinde başka boşluklar için hâlâ
        // diagonal hedef olabilir veya kaynak olmaya devam edebilir.
        var sourceGap = new Vector2Int(fromX, fromY);
        _verticalOnlySlideGaps.Add(sourceGap);

        float slideDuration = board.GetFallDurationForMove(fromX, fromY, toX, toY);

        bool useSlideSettle = board.EnableFallSettle;
        float slideSettleDur = board.FallSettleDuration * 0.82f;
        float slideSettleStr = board.FallSettleStrength * 0.60f;

        action.AddMove(
            tile,
            fromX,
            fromY,
            toX,
            toY,
            slideDuration,
            useSlideSettle,
            slideSettleDur,
            slideSettleStr,
            board.FallMoveCurve);

        movedThisPass.Add(tile);
        return true;
    }
    private bool IsDiagonalPassableCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        if (!state.inBounds)
            return false;

        if (state.isMaskHole)
            return false;

        if (state.isPendingTriggeredSpecial)
            return false;

        // Chest / Stone / OverTileBlocker içinden veya üstünden diagonal geçiş yok.
        // Diagonal sadece blocker'ın etrafındaki gerçek açık hücrelerden yapılır.
        if (state.isObstacleBlocked)
            return false;

        return true;
    }
    public bool HasAnyEmptyPlayableCell()
    {
        PruneVerticalOnlySlideLocks();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (IsCascadeReachableEmptyCell(x, y))
                    return true;
            }
        }

        return false;
    }
    private bool IsObstacleBlockedCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        return state.isObstacleBlocked;
    }

    private bool IsSegmentConnectedToSpawnEdge(int x, int topY)
    {
        if (topY <= 0) return true;

        for (int y = topY - 1; y >= 0; y--)
        {
            if (IsGravityBlockedCell(x, y))
                return false;

            if (!board.IsSpawnPassThroughCell(x, y))
                return false;
        }

        return true;
    }
    private bool IsSlideFillTarget(int x, int y)
    {
        // Target aktif boş olmalı ve şu anda gerçek diagonal kaynakla doldurulabilmeli.
        // Akışa bağlı olmayan kapalı cepler slide target değildir.
        //
        // Daha önce source olarak kullanılan hücreler diagonal target olamaz;
        // onları yalnızca kendi dikey akışı doldurabilir.
        if (IsVerticalOnlySlideTarget(x, y))
            return false;

        return IsEmptyPlayableCell(x, y) && HasValidDiagonalSourceFor(x, y);
    }
    private bool IsCascadeReachableEmptyCell(int x, int y)
    {
        if (!IsEmptyPlayableCell(x, y))
            return false;

        // Aynı kolon spawn edge'e veya üstteki tile'a bağlıysa vertical collapse/spawn doldurabilir.
        if (HasVerticalFillPathFor(x, y))
            return true;

        // Slide kaynak boşluğu vertical-only'dir.
        // Diğer sütunlardan diagonal ile doldurulmayacağı için cascade'i bloklamaz.
        if (IsVerticalOnlySlideTarget(x, y))
            return false;

        // Aksi halde yalnızca şu an geçerli diagonal kaynak varsa cascade doldurabilir.
        return HasValidDiagonalSourceFor(x, y);
    }

    private bool HasVerticalFillPathFor(int x, int y)
    {
        if (!IsEmptyPlayableCell(x, y))
            return false;

        int segmentTop = FindSegmentTopY(x, y);

        // Segment spawn edge'e bağlıysa CalculateCollapseAndSpawn bu boşluğu doldurabilir.
        if (IsSegmentConnectedToSpawnEdge(x, segmentTop))
            return true;

        // Spawn'a bağlı değilse bile, aynı gravity segmentinde yukarıda gerçek bir tile
        // varsa collapse ile aşağı sıkışabilir.
        for (int yy = y - 1; yy >= segmentTop; yy--)
        {
            if (IsGravityBlockedCell(x, yy))
                break;

            if (IsPassThroughVoidCell(x, yy))
                continue;

            if (TryGetTileSource(x, yy, out _))
                return true;
        }

        return false;
    }

    private int FindSegmentTopY(int x, int y)
    {
        int topY = y;

        while (topY > 0 && !IsGravityBlockedCell(x, topY - 1))
            topY--;

        return topY;
    }
    private bool HasValidDiagonalSourceFor(int x, int y)
    {
        if (IsVerticalOnlySlideTarget(x, y))
            return false;

        return CanSlideFromTo(x - 1, y - 1, x, y) ||
               CanSlideFromTo(x + 1, y - 1, x, y);
    }
    private bool CanSlideFromTo(int fromX, int fromY, int toX, int toY)
    {
        if (IsVerticalOnlySlideTarget(toX, toY))
            return false;

        if (!IsEmptyPlayableCell(toX, toY))
            return false;

        if (!TryGetTileSource(fromX, fromY, out _))
            return false;

        // Dikey düşüş hakkı varsa diagonal kaynak olarak kullanılmaz.
        if (CanTileFallStraightDown(fromX, fromY))
            return false;

        int cax = fromX;
        int cay = toY;
        int cbx = toX;
        int cby = fromY;

        // En az bir köşe açıksa diagonal yol mümkün.
        return IsDiagonalPassableCell(cax, cay) ||
               IsDiagonalPassableCell(cbx, cby);
    }




    private bool IsVerticalOnlySlideTarget(int x, int y)
    {
        // Aşama 1: kolon-bazlı kilit kaldırıldı.
        // Sadece slide kaynağı olarak kullanılmış spesifik hücre kilitlidir.
        return _verticalOnlySlideGaps.Contains(new Vector2Int(x, y));
    }

    private void PruneVerticalOnlySlideLocks()
    {
        if (_verticalOnlySlideGaps.Count == 0)
            return;

        List<Vector2Int> toRemove = null;

        foreach (var gap in _verticalOnlySlideGaps)
        {
            bool stillEmpty = IsEmptyPlayableCell(gap.x, gap.y);

            // Kilit ancak şu durumlarda anlamlıdır:
            //   - Hücre hâlâ boş VE vertical fill ile dolabilir.
            //
            // Hücre dolduysa kilit gereksiz.
            // Hücre boş ama vertical fill imkansızsa (üstü obstacle / segment closed),
            // kilit kalıcı deadlock yaratır — bırakırsak hücre asla dolmaz.
            // Bu durumda diagonal alıcı olabilmesi için kilidi kaldır.
            bool keepLock = stillEmpty && HasVerticalFillPathFor(gap.x, gap.y);

            if (!keepLock)
            {
                toRemove ??= new List<Vector2Int>();
                toRemove.Add(gap);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
                _verticalOnlySlideGaps.Remove(toRemove[i]);
        }
    }

    private bool IsInNonSpawnableSegment(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        int topY = y;
        while (topY > 0 && !IsGravityBlockedCell(x, topY - 1))
            topY--;

        return !IsSegmentConnectedToSpawnEdge(x, topY);
    }
    private bool IsNonObstacleHoleCell(int hx, int hy)
    {
        if (!TryGetCellState(hx, hy, out var state))
            return false;

        return state.isMaskHole && !state.isObstacleBlocked;
    }

    private bool HasAnyTileAboveInSameSegment(int x, int y)
    {
        for (int yy = y - 1; yy >= 0; yy--)
        {
            if (IsGravityBlockedCell(x, yy)) break;
            if (IsNonObstacleHoleCell(x, yy)) continue;
            if (board.Tiles[x, yy] != null) return true;
        }
        return false;
    }

    private bool IsFloorPocketTarget(int x, int y)
    {
        bool hasBottomVoid = (y >= board.Height - 1) || IsNonObstacleHoleCell(x, y + 1);
        if (!hasBottomVoid) return false;
        if (HasAnyTileAboveInSameSegment(x, y)) return false;
        return true;
    }

    private bool IsAdjacentToMaskHole(int x, int y)
    {
        if (IsNonObstacleHoleCell(x, y)) return true;
        if (x > 0 && IsNonObstacleHoleCell(x - 1, y)) return true;
        if (x < board.Width - 1 && IsNonObstacleHoleCell(x + 1, y)) return true;
        if (y > 0 && IsNonObstacleHoleCell(x, y - 1)) return true;
        if (y < board.Height - 1 && IsNonObstacleHoleCell(x, y + 1)) return true;
        return false;
    }
    private bool CanTileFallStraightDown(int fromX, int fromY)
    {
        if (!TryGetTileSource(fromX, fromY, out _))
            return false;

        int y = fromY + 1;
        while (y < board.Height)
        {
            if (IsGravityBlockedCell(fromX, y))
                return false;

            if (IsPassThroughVoidCell(fromX, y))
            {
                y++;
                continue;
            }

            return IsEmptyPlayableCell(fromX, y);
        }

        return false;
    }


    private bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state)
    {
        state = default;
        return board != null && board.TryGetCellState(x, y, out state);
    }
    private bool IsTileSlotCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        return state.canContainTile;
    }
    private bool IsEmptyPlayableCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        return state.canAcceptTile;
    }
    private bool TryGetTileSource(int x, int y, out TileView tile)
    {
        tile = null;

        if (!TryGetCellState(x, y, out var state))
            return false;

        if (!state.canProvideTile || state.tile == null)
            return false;

        tile = state.tile;

        // TileView kendi koordinatıyla board snapshot'ı aynı şeyi göstermeli.
        if (tile.X != x || tile.Y != y)
            return false;

        if (tile.TryGetCellState(out var tileState))
        {
            if (!tileState.inBounds || tileState.x != x || tileState.y != y)
                return false;

            if (!tileState.hasTile || tileState.tile != tile)
                return false;

            if (!tileState.canProvideTile)
                return false;
        }

        return true;
    }
    private bool IsPassThroughVoidCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        return state.isPassThroughVoid;
    }

    private TileType GetRandomType()
    {
        if (board.RandomPool == null || board.RandomPool.Length == 0)
            return TileType.Gear;

        return board.RandomPool[UnityEngine.Random.Range(0, board.RandomPool.Length)];
    }

    private TileType GetRandomTypeAvoidingImmediateMatch(int x, int y)
    {
        if (board.RandomPool == null || board.RandomPool.Length == 0)
            return TileType.Gear;

        int len = board.RandomPool.Length;
        int start = UnityEngine.Random.Range(0, len);

        for (int i = 0; i < len; i++)
        {
            TileType candidate = board.RandomPool[(start + i) % len];
            if (!WouldCreateImmediateMatch(x, y, candidate))
                return candidate;
        }

        return board.RandomPool[start];
    }

    private bool WouldCreateImmediateMatch(int x, int y, TileType type)
    {
        // Horizontal 3-run patterns including (x,y)
        if (HasTypeAt(x - 1, y, type) && HasTypeAt(x - 2, y, type)) return true;
        if (HasTypeAt(x + 1, y, type) && HasTypeAt(x + 2, y, type)) return true;
        if (HasTypeAt(x - 1, y, type) && HasTypeAt(x + 1, y, type)) return true;

        // Vertical 3-run patterns including (x,y)
        if (HasTypeAt(x, y - 1, type) && HasTypeAt(x, y - 2, type)) return true;
        if (HasTypeAt(x, y + 1, type) && HasTypeAt(x, y + 2, type)) return true;
        if (HasTypeAt(x, y - 1, type) && HasTypeAt(x, y + 1, type)) return true;

        // 2x2 patterns
        if (HasTypeAt(x - 1, y, type) && HasTypeAt(x - 1, y - 1, type) && HasTypeAt(x, y - 1, type)) return true;
        if (HasTypeAt(x + 1, y, type) && HasTypeAt(x + 1, y - 1, type) && HasTypeAt(x, y - 1, type)) return true;
        if (HasTypeAt(x - 1, y, type) && HasTypeAt(x - 1, y + 1, type) && HasTypeAt(x, y + 1, type)) return true;
        if (HasTypeAt(x + 1, y, type) && HasTypeAt(x + 1, y + 1, type) && HasTypeAt(x, y + 1, type)) return true;

        return false;
    }

    private bool HasTypeAt(int x, int y, TileType type)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;
        if (!IsTileSlotCell(x, y))
            return false;

        var tile = board.Tiles[x, y];
        if (tile == null)
            return false;

        return tile.GetTileType() == type;
    }

    private bool TryPickMovableGoalToSpawn(out ObstacleId obstacleId)
    {
        obstacleId = ObstacleId.None;

        var topHud = board.TopHud;
        if (topHud == null || board.ObstacleStateService == null || board.ActiveLevelData == null)
            return false;

        _activeGoalsBuffer.Clear();
        topHud.GetActiveGoals(_activeGoalsBuffer);

        for (int i = 0; i < _activeGoalsBuffer.Count; i++)
        {
            var goal = _activeGoalsBuffer[i];
            if (goal.targetType != LevelGoalTargetType.Obstacle)
                continue;
            if (goal.remaining <= 0)
                continue;

            var def = board.ActiveLevelData.obstacleLibrary != null
                ? board.ActiveLevelData.obstacleLibrary.Get(goal.obstacleId)
                : null;

            if (def == null)
                continue;

            if (!def.IsMovableObstacleForRemainingHits(Mathf.Max(1, def.hits)))
                continue;

            int alive = board.ObstacleStateService.CountAliveOrigins(goal.obstacleId);
            if (alive < goal.remaining)
            {
                obstacleId = goal.obstacleId;
                return true;
            }
        }

        return false;
    }

    private TileView SpawnMovableObstacleTileForFall(int x, int y, int spawnFromY, ObstacleId obstacleId)
    {
        if (board.ObstacleStateService == null)
            return null;

        if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(x, y, obstacleId))
            return null;

        var def = board.ActiveLevelData != null && board.ActiveLevelData.obstacleLibrary != null
            ? board.ActiveLevelData.obstacleLibrary.Get(obstacleId)
            : null;

        if (def == null)
            return null;

        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
        var view = go.GetComponent<TileView>();
        if (view == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        view.Init(board, x, y);
        board.ConfigureTileView(view);
        view.MarkPlannedToMoveThisFallPass(true);
        view.SetUseFullCellIcon(false);
        view.SetMovableObstacleTile(true);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        view.SetCoords(x, spawnFromY);
        view.SnapToGrid(board.TileSize);

        view.SetCoords(x, y);
        board.Tiles[x, y] = view;

        TileType dummyType = board.RandomPool != null && board.RandomPool.Length > 0
            ? board.RandomPool[0]
            : TileType.Gear;

        view.SetType(dummyType);
        view.SetSpecial(TileSpecial.None);

        Sprite obstacleSprite = def.GetPreviewSprite();
        if (obstacleSprite != null && view.IconImage != null)
            view.IconImage.sprite = obstacleSprite;

        board.SyncTileData(x, y);
        board.RefreshTileObstacleVisual(view);

        return view;
    }
    private bool IsGravityBlockedCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state))
            return false;

        return state.isObstacleBlocked || board.IsPendingTriggeredSpecialCell(x, y);
    }
}