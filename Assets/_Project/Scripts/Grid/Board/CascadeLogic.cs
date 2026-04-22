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

    // Goal buffer
    private readonly List<TopHudController.ActiveGoal> _activeGoalsBuffer = new List<TopHudController.ActiveGoal>(4);

    public CascadeLogic(BoardController board)
    {
        this.board = board;
    }

    public List<BoardAction> CalculateCascades()
    {
        _actionsBuffer.Clear();
        const int maxPass = 32;

        // Tüm pass'ları tek FallAction'a birleştir
        // Her pass öncekinin %40'ı kadar gecikmeli başlar → overlap
        // Eski: 6 sıralı FallAction = 0.65s
        // Yeni: 1 merged FallAction = ~0.25s
        var merged = new FallAction();
        float cumulativeDelay = 0f;
        const float overlapRatio = 1f; // önceki pass'ın %40'ında sonraki başlar

        for (int pass = 0; pass < maxPass; pass++)
        {
            if (!HasAnyEmptyPlayableCell()) break;

            var collapseAction = CalculateCollapseAndSpawn();
            if (collapseAction != null && collapseAction.HasMoves)
            {
                float dur = collapseAction.GetMaxMoveDuration();
                merged.MergeFrom(collapseAction, cumulativeDelay);
                cumulativeDelay += dur * overlapRatio;
            }

            if (!HasAnyEmptyPlayableCell()) break;

            var slideAction = CalculateSlideFill();
            if (slideAction != null && slideAction.HasMoves)
            {
                float slideDur = slideAction.GetMaxMoveDuration();
                merged.MergeFrom(slideAction, cumulativeDelay);
                cumulativeDelay += slideDur * overlapRatio;

                var postSlideCollapse = CalculateCollapseColumns();
                if (postSlideCollapse != null && postSlideCollapse.HasMoves)
                {
                    float postDur = postSlideCollapse.GetMaxMoveDuration();
                    merged.MergeFrom(postSlideCollapse, cumulativeDelay);
                    cumulativeDelay += postDur * overlapRatio;
                }
            }
            else
            {
                break;
            }
        }

        if (merged.HasMoves)
            _actionsBuffer.Add(merged);

        return new List<BoardAction>(_actionsBuffer);
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
                    if (board.Holes[x, y]) continue;
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
                        float duration = board.GetFallDurationForDistance(dist);
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
                        if (board.Holes[x, y]) continue;
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

                        nextSpawnY--;

                        int dist = Mathf.Abs(y - spawnFromY);
                        float duration = board.GetFallDurationForDistance(dist);

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
                    fromY,
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

        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.IsMaskHoleCell(x, y) || IsGravityBlockedCell(x, y))
                    continue;

                if (board.Tiles[x, y] != null) continue;
                if (!IsSlideFillTarget(x, y)) continue;

                bool TrySource(int sx, int sy)
                {
                    if (sx < 0 || sx >= board.Width || sy < 0 || sy >= board.Height) return false;
                    if (board.IsMaskHoleCell(sx, sy) || IsGravityBlockedCell(sx, sy)) return false;

                    var t = board.Tiles[sx, sy];
                    if (t == null) return false;

                    bool targetIsObstaclePocket = IsGravityBlockedCell(x, y - 1);

                    bool HasUsableOtherSource()
                    {
                        int otherSx = (sx == x - 1) ? (x + 1) : (x - 1);
                        int otherSy = y - 1;

                        if (otherSx < 0 || otherSx >= board.Width || otherSy < 0 || otherSy >= board.Height)
                            return false;

                        if (board.IsMaskHoleCell(otherSx, otherSy) || IsGravityBlockedCell(otherSx, otherSy))
                            return false;

                        return board.Tiles[otherSx, otherSy] != null;
                    }

                    bool otherSourceExists = HasUsableOtherSource();

                    if (!targetIsObstaclePocket && otherSourceExists && CanTileFallStraightDown(sx, sy))
                        return false;

                    return TryDiagonalFrom(sx, sy, x, y, _movedThisPassSet, action);
                }

                bool _ = TrySource(x - 1, y - 1) || TrySource(x + 1, y - 1);
            }
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
                        if (board.Holes[x, yy]) continue;
                        _slots.Add(yy);
                    }

                    _existing.Clear();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
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
                            float moveDuration = board.GetFallDurationForDistance(dist);

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
                                fromY,
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
        int cax = fromX, cay = toY;
        int cbx = toX, cby = fromY;

        if (cax < 0 || cax >= board.Width || cay < 0 || cay >= board.Height) return false;
        if (cbx < 0 || cbx >= board.Width || cby < 0 || cby >= board.Height) return false;
        if (board.IsMaskHoleCell(cax, cay) || board.IsMaskHoleCell(cbx, cby)) return false;

        if (IsGravityBlockedCell(cax, cay) || IsGravityBlockedCell(cbx, cby))
            return false;

        var obs = board.ObstacleStateService;
        if (obs != null)
        {
            if (obs.IsCellBlocked(cax, cay) && !obs.GetAllowDiagonalAt(cax, cay)) return false;
            if (obs.IsCellBlocked(cbx, cby) && !obs.GetAllowDiagonalAt(cbx, cby)) return false;
        }

        return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, action);
    }

    private bool TrySlideFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        FallAction action)
    {
        if (fromX < 0 || fromX >= board.Width || fromY < 0 || fromY >= board.Height) return false;
        if (board.Holes[fromX, fromY]) return false;

        var tile = board.Tiles[fromX, fromY];
        if (tile == null || movedThisPass.Contains(tile)) return false;

        // ── MovableObstacle: diagonal/slide taşıma sırasında pozisyon sync ──
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

        float slideDuration = board.GetFallDurationForDistance(1);

        bool useSlideSettle = board.EnableFallSettle;
        float slideSettleDur = board.FallSettleDuration * 0.82f;
        float slideSettleStr = board.FallSettleStrength * 0.60f;

        action.AddMove(
            tile,
            fromY,
            toY,
            slideDuration,
            useSlideSettle,
            slideSettleDur,
            slideSettleStr,
            board.FallMoveCurve);

        movedThisPass.Add(tile);
        return true;
    }

    public bool HasAnyEmptyPlayableCell()
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y]) continue;
                if (board.Tiles[x, y] == null) return true;
            }
        }
        return false;
    }

    private bool IsObstacleBlockedCell(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return false;

        return obstacleStateService.IsCellBlocked(x, y);
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
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;
        if (board.Tiles[x, y] != null)
            return false;
        if (IsNonObstacleHoleCell(x, y))
            return false;

        bool obstacleAbove = IsGravityBlockedCell(x, y - 1);
        if (IsAdjacentToMaskHole(x, y) && !obstacleAbove)
            return false;

        if (obstacleAbove) return true;
        if (IsFloorPocketTarget(x, y)) return true;
        if (IsInNonSpawnableSegment(x, y)) return true;

        return false;
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
        return hx >= 0 && hx < board.Width && hy >= 0 && hy < board.Height
               && board.Holes[hx, hy]
               && !IsObstacleBlockedCell(hx, hy);
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
        if (fromX < 0 || fromX >= board.Width || fromY < 0 || fromY >= board.Height)
            return false;

        int y = fromY + 1;
        while (y < board.Height)
        {
            if (IsGravityBlockedCell(fromX, y)) return false;
            if (board.Holes[fromX, y] && !IsGravityBlockedCell(fromX, y))
            {
                y++;
                continue;
            }
            return board.Tiles[fromX, y] == null;
        }
        return false;
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
        if (board.Holes[x, y])
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
        return IsObstacleBlockedCell(x, y) || board.IsPendingTriggeredSpecialCell(x, y);
    }
}