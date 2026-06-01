using System.Collections.Generic;
using UnityEngine;

public class CascadeLogic
{
    private readonly BoardController board;

    // Buffer for active goals
    private readonly List<TopHudController.ActiveGoal> _activeGoalsBuffer = new List<TopHudController.ActiveGoal>(4);

    public CascadeLogic(BoardController board)
    {
        this.board = board;
    }

    private class VirtualTile
    {
        public TileView View;
        public TileType SpawnType;
        public bool IsSpawned;

        public bool IsMovableObstacle;
        public ObstacleId SpawnObstacleId;

        // How many times this tile has slid diagonally in this cascade simulation.
        // Capped by BoardController.MaxDiagonalSlidesPerCascade to limit spread while
        // still allowing tiles to reach their rest position in fewer cascade rounds.
        public int DiagonalSlideCount;

        public List<Vector2Int> Path = new List<Vector2Int>();
    }

    public List<BoardAction> CalculateCascades()
    {
        board.IncrementFallGeneration();

        VirtualTile[,] virtualBoard = new VirtualTile[board.Width, board.Height];

        // 1. Initialize Virtual Board
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                var view = board.Tiles[x, y];
                if (view != null)
                {
                    view.MarkPlannedToMoveThisFallPass(false);
                    virtualBoard[x, y] = new VirtualTile
                    {
                        View = view,
                        IsSpawned = false,
                        Path = new List<Vector2Int> { new Vector2Int(x, y) }
                    };
                    board.Tiles[x, y] = null; // Clear actual board, we will re-assign at the end
                }
            }
        }

        HashSet<Vector2Int> verticalOnlyGaps = new HashSet<Vector2Int>();
        bool changed = true;

        // SIMULATION LOOP
        const int MAX_ITERATIONS = 32;
        int iter = 0;
        bool spawnedMovableThisPass = false;
        Dictionary<ObstacleId, int> spawnedMovableCounts = new Dictionary<ObstacleId, int>();

        while (changed && iter < MAX_ITERATIONS)
        {
            changed = false;
            iter++;

            // Step 1: Vertical Collapse & Spawn
            for (int x = 0; x < board.Width; x++)
            {
                changed |= ProcessVerticalGravityAndSpawn(virtualBoard, x, ref spawnedMovableThisPass, spawnedMovableCounts);
            }

            // Step 2: Diagonal Slide
            bool slided = false;
            for (int y = board.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (IsSlotEmpty(virtualBoard, x, y) && !verticalOnlyGaps.Contains(new Vector2Int(x, y)))
                    {
                        // Right-top priority
                        if (TrySlide(virtualBoard, x + 1, y - 1, x, y, verticalOnlyGaps))
                        {
                            slided = true;
                            continue;
                        }
                        
                        // Left-top fallback
                        if (TrySlide(virtualBoard, x - 1, y - 1, x, y, verticalOnlyGaps))
                        {
                            slided = true;
                            continue;
                        }
                    }
                }
            }
            changed |= slided;

            // Prune unfillable VerticalOnly gaps
            PruneVerticalOnlyGaps(virtualBoard, verticalOnlyGaps);
        }

        // COMPILE ACTION
        var action = new FallAction();

        // Step 3: Apply virtual board back to actual board
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = board.Height - 1; y >= 0; y--)
            {
                var vTile = virtualBoard[x, y];
                if (vTile == null)
                {
                    board.Tiles[x, y] = null;
                    board.SyncTileData(x, y);
                    continue;
                }

                var compressedPath = CompressPath(vTile.Path);
                int finalX = compressedPath[compressedPath.Count - 1].x;
                int finalY = compressedPath[compressedPath.Count - 1].y;

                TileView view = vTile.View;

                if (view == null)
                {
                    // Create newly spawned tile!
                    if (vTile.IsMovableObstacle)
                    {
                        view = SpawnMovableObstacleTileForFall(finalX, finalY, compressedPath[0].x, compressedPath[0].y, vTile.SpawnObstacleId);
                    }
                    else
                    {
                        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                        view = go.GetComponent<TileView>();
                        view.Init(board, finalX, finalY);
                        board.ConfigureTileView(view);
                        view.SetCoords(compressedPath[0].x, compressedPath[0].y); // start coord visually
                        view.SnapToGrid(board.TileSize);
                        
                        view.SetCoords(finalX, finalY); // final coord internally
                        view.SetType(vTile.SpawnType);
                        view.SetSpecial(TileSpecial.None);
                        board.RefreshTileObstacleVisual(view);
                    }
                }
                if (view != null)
                {
                    view.MarkPlannedToMoveThisFallPass(true);
                    board.Tiles[finalX, finalY] = view;
                    view.SetCoords(finalX, finalY);

                    // REGISTER newly spawned movable obstacles
                    if (vTile.IsSpawned && vTile.IsMovableObstacle && board.ObstacleStateService != null)
                    {
                        board.ObstacleStateService.TrySpawnSingleCellObstacleAt(finalX, finalY, vTile.SpawnObstacleId);
                    }
                }
                else
                {
                    board.Tiles[finalX, finalY] = null;
                }
                board.SyncTileData(finalX, finalY);

                if (view == null) continue;

                if (compressedPath.Count > 1)
                {
                    // If it was already an obstacle on the board (not spawned this pass), move its logical state
                    if (board.ObstacleStateService != null && !vTile.IsSpawned && vTile.View != null)
                    {
                        if (board.ObstacleStateService.IsMovableObstacleAt(compressedPath[0].x, compressedPath[0].y))
                        {
                            board.ObstacleStateService.MoveObstacle(compressedPath[0].x, compressedPath[0].y, finalX, finalY);
                        }
                    }

                    // Calculate segments duration
                    float[] segmentDurations = new float[compressedPath.Count - 1];
                    for (int i = 0; i < compressedPath.Count - 1; i++)
                    {
                        segmentDurations[i] = board.GetFallDurationForMove(compressedPath[i].x, compressedPath[i].y, compressedPath[i + 1].x, compressedPath[i + 1].y);
                    }

                    // Settle logic (only if resting on something solid that isn't moving)
                    bool useSettle = false;
                    float settleDur = board.FallSettleDuration;
                    float settleStr = board.FallSettleStrength;

                    if (board.ShouldEnableFallSettleThisPass())
                    {
                        int belowY = finalY + 1;
                        bool hasSupport = false;

                        if (belowY >= board.Height) {
                            hasSupport = true;
                        } else if (IsGravityBlockedCell(finalX, belowY)) {
                            hasSupport = true;
                        } else {
                            var belowVTile = virtualBoard[finalX, belowY];
                            // If there is a tile below, and it didn't move (path count == 1), then it's solid
                            if (belowVTile != null && belowVTile.Path.Count <= 1) {
                                hasSupport = true;
                            }
                        }

                        if (hasSupport)
                        {
                            useSettle = true;
                            // Calculate total distance for settle strength modifier
                            float totalDistY = Mathf.Abs(finalY - compressedPath[0].y);
                            float dist01 = Mathf.Clamp01((totalDistY - 1f) / 4f);
                            settleDur *= Mathf.Lerp(0.92f, 1.12f, dist01);
                            settleStr *= Mathf.Lerp(0.88f, 1.08f, dist01);
                        }
                    }

                    action.AddPathMove(
                        view,
                        compressedPath.ToArray(),
                        segmentDurations,
                        useSettle,
                        settleDur,
                        settleStr,
                        board.FallMoveCurve
                    );
                }
                else
                {
                    // Didn't move, just placed back
                    view.MarkPlannedToMoveThisFallPass(false);
                }
            }
        }

        board.RefreshAllTileObstacleVisuals();

        if (action.HasMoves)
            return new List<BoardAction> { action };

        return new List<BoardAction>();
    }

    public bool HasAnyEmptyPlayableCell()
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Tiles[x, y] == null && IsTileSlotCell(x, y))
                {
                    // Basic check: if it's not blocked, consider it empty and playable
                    if (!IsGravityBlockedCell(x, y)) return true;
                }
            }
        }
        return false;
    }

    private bool ProcessVerticalGravityAndSpawn(VirtualTile[,] virtualBoard, int x, ref bool spawnedMovableThisPass, Dictionary<ObstacleId, int> spawnedMovableCounts)
    {
        bool moved = false;

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
            bool touchesSpawn = IsSegmentConnectedToSpawnEdge(x, topY);

            // 1. Compact segment
            List<VirtualTile> segmentTiles = new List<VirtualTile>();
            for (int y = segmentTop; y >= topY; y--)
            {
                if (!IsTileSlotCell(x, y)) continue;
                if (virtualBoard[x, y] != null)
                {
                    segmentTiles.Add(virtualBoard[x, y]);
                    virtualBoard[x, y] = null;
                }
            }

            List<int> slotYs = new List<int>();
            for (int y = segmentTop; y >= topY; y--)
            {
                if (IsTileSlotCell(x, y)) slotYs.Add(y);
            }

            for (int i = 0; i < segmentTiles.Count; i++)
            {
                int toY = slotYs[i];
                var tile = segmentTiles[i];
                virtualBoard[x, toY] = tile;

                if (tile.Path[tile.Path.Count - 1].y != toY || tile.Path[tile.Path.Count - 1].x != x)
                {
                    tile.Path.Add(new Vector2Int(x, toY));
                    moved = true;
                }
            }

            // 2. Spawn for remaining slots
            if (touchesSpawn)
            {
                int nextSpawnY = topY + board.SpawnStartOffsetY;
                
                for (int i = segmentTiles.Count; i < slotYs.Count; i++)
                {
                    int toY = slotYs[i];
                    int spawnFromY = nextSpawnY;

                    var newTile = new VirtualTile
                    {
                        IsSpawned = true,
                        Path = new List<Vector2Int> { new Vector2Int(x, spawnFromY), new Vector2Int(x, toY) }
                    };

                    if (!spawnedMovableThisPass && TryPickMovableGoalToSpawn(out var goalObstacleId))
                    {
                        newTile.IsMovableObstacle = true;
                        newTile.SpawnObstacleId = goalObstacleId;
                        spawnedMovableThisPass = true;
                    }
                    else
                    {
                        newTile.SpawnType = GetRandomTypeAvoidingImmediateMatch(virtualBoard, x, toY);
                    }

                    virtualBoard[x, toY] = newTile;
                    moved = true;
                }
            }

            segmentTop = segmentBottom - 1;
        }

        return moved;
    }

    private bool TrySlide(VirtualTile[,] virtualBoard, int fromX, int fromY, int toX, int toY, HashSet<Vector2Int> verticalOnlyGaps)
    {
        if (fromX < 0 || fromX >= board.Width || fromY < 0 || fromY >= board.Height) return false;
        
        VirtualTile sourceTile = virtualBoard[fromX, fromY];
        int sourceY = fromY;

        if (sourceTile != null)
        {
            if (!TryGetCellState(fromX, fromY, out var state)) return false;
            if (state.isObstacleBlocked) return false;
        }
        else
        {
            // Try stealing from below if fromY is a Mask Hole or PassThrough empty cell
            if (TryGetCellState(fromX, fromY, out var state) && !state.isObstacleBlocked)
            {
                // Find a tile below that passed through this cell
                for (int y = fromY + 1; y < board.Height; y++)
                {
                    var belowTile = virtualBoard[fromX, y];
                    if (belowTile != null)
                    {
                        if (belowTile.Path.Count > 0 && belowTile.Path[0].y <= fromY)
                        {
                            sourceTile = belowTile;
                            sourceY = y;
                        }
                        break;
                    }
                    if (IsGravityBlockedCell(fromX, y)) break;
                }
            }
        }

        if (sourceTile == null) return false;

        if (sourceTile.DiagonalSlideCount >= board.MaxDiagonalSlidesPerCascade) return false;

        // Check if diagonal pass is possible (at least one corner must be open)
        bool cornerA = IsDiagonalPassableCell(fromX, toY);
        bool cornerB = IsDiagonalPassableCell(toX, fromY);

        if (!cornerA && !cornerB) return false;

        // Move it
        sourceTile.DiagonalSlideCount++;
        virtualBoard[fromX, sourceY] = null;
        virtualBoard[toX, toY] = sourceTile;

        if (sourceY > fromY)
        {
            for (int i = sourceTile.Path.Count - 1; i >= 0; i--)
            {
                if (sourceTile.Path[i].y > fromY)
                {
                    sourceTile.Path.RemoveAt(i);
                }
            }
            if (sourceTile.Path.Count == 0 || sourceTile.Path[sourceTile.Path.Count - 1].y != fromY)
            {
                sourceTile.Path.Add(new Vector2Int(fromX, fromY));
            }
        }

        sourceTile.Path.Add(new Vector2Int(toX, toY));

        verticalOnlyGaps.Add(new Vector2Int(fromX, sourceY));
        return true;
    }

    private void PruneVerticalOnlyGaps(VirtualTile[,] virtualBoard, HashSet<Vector2Int> verticalOnlyGaps)
    {
        if (verticalOnlyGaps.Count == 0) return;

        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var gap in verticalOnlyGaps)
        {
            bool stillEmpty = IsSlotEmpty(virtualBoard, gap.x, gap.y);

            // Keep the lock only if it is empty AND can be filled vertically
            bool keepLock = stillEmpty && HasVerticalFillPathFor(virtualBoard, gap.x, gap.y);

            if (!keepLock)
            {
                toRemove.Add(gap);
            }
        }

        foreach (var gap in toRemove)
        {
            verticalOnlyGaps.Remove(gap);
        }
    }

    private bool HasVerticalFillPathFor(VirtualTile[,] virtualBoard, int x, int y)
    {
        int segmentTop = FindSegmentTopY(x, y);

        if (IsSegmentConnectedToSpawnEdge(x, segmentTop))
            return true;

        for (int yy = y - 1; yy >= segmentTop; yy--)
        {
            if (virtualBoard[x, yy] != null) return true;
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

    private List<Vector2Int> CompressPath(List<Vector2Int> rawPath)
    {
        if (rawPath.Count <= 2) return new List<Vector2Int>(rawPath);

        List<Vector2Int> optimized = new List<Vector2Int>();
        optimized.Add(rawPath[0]);

        for (int i = 1; i < rawPath.Count - 1; i++)
        {
            Vector2Int prev = optimized[optimized.Count - 1];
            Vector2Int curr = rawPath[i];
            Vector2Int next = rawPath[i + 1];

            bool isVertical = (prev.x == curr.x && curr.x == next.x);
            bool isHorizontal = (prev.y == curr.y && curr.y == next.y);

            if (!isVertical && !isHorizontal)
            {
                optimized.Add(curr);
            }
        }

        optimized.Add(rawPath[rawPath.Count - 1]);
        return optimized;
    }

    // ====== Helper Methods ======

    private bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state)
    {
        state = default;
        return board != null && board.TryGetCellState(x, y, out state);
    }

    private bool IsTileSlotCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        return state.canContainTile;
    }

    private bool IsSlotEmpty(VirtualTile[,] virtualBoard, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (!TryGetCellState(x, y, out var state)) return false;
        if (!state.canContainTile || state.isObstacleBlocked) return false;
        return virtualBoard[x, y] == null;
    }

    private bool IsGravityBlockedCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        if (state.isObstacleBlocked || board.IsPendingTriggeredSpecialCell(x, y)) return true;
        // holdsTile=true obstacle (Oil): tile dikey olarak bu hücreden kayamaz, kalır
        return board.ObstacleStateService?.HoldsTileAt(x, y) ?? false;
    }

    private bool IsSegmentConnectedToSpawnEdge(int x, int topY)
    {
        if (topY <= 0) return true;

        for (int y = topY - 1; y >= 0; y--)
        {
            if (IsGravityBlockedCell(x, y)) return false;
            if (!board.IsSpawnPassThroughCell(x, y)) return false;
        }

        return true;
    }

    private bool IsDiagonalPassableCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        if (!state.inBounds) return false;
        if (state.isPendingTriggeredSpecial) return false;
        if (state.isObstacleBlocked) return false;
        return true;
    }

    private TileType GetRandomTypeAvoidingImmediateMatch(VirtualTile[,] virtualBoard, int x, int y)
    {
        if (board.RandomPool == null || board.RandomPool.Length == 0)
            return TileType.Gear;

        int len = board.RandomPool.Length;
        int start = UnityEngine.Random.Range(0, len);

        for (int i = 0; i < len; i++)
        {
            TileType candidate = board.RandomPool[(start + i) % len];
            if (!WouldCreateImmediateMatch(virtualBoard, x, y, candidate))
                return candidate;
        }

        return board.RandomPool[start];
    }

    private bool WouldCreateImmediateMatch(VirtualTile[,] virtualBoard, int x, int y, TileType type)
    {
        // Horizontal 3-run
        if (HasTypeAt(virtualBoard, x - 1, y, type) && HasTypeAt(virtualBoard, x - 2, y, type)) return true;
        if (HasTypeAt(virtualBoard, x + 1, y, type) && HasTypeAt(virtualBoard, x + 2, y, type)) return true;
        if (HasTypeAt(virtualBoard, x - 1, y, type) && HasTypeAt(virtualBoard, x + 1, y, type)) return true;

        // Vertical 3-run
        if (HasTypeAt(virtualBoard, x, y - 1, type) && HasTypeAt(virtualBoard, x, y - 2, type)) return true;
        if (HasTypeAt(virtualBoard, x, y + 1, type) && HasTypeAt(virtualBoard, x, y + 2, type)) return true;
        if (HasTypeAt(virtualBoard, x, y - 1, type) && HasTypeAt(virtualBoard, x, y + 1, type)) return true;

        // 2x2 patterns
        if (HasTypeAt(virtualBoard, x - 1, y, type) && HasTypeAt(virtualBoard, x - 1, y - 1, type) && HasTypeAt(virtualBoard, x, y - 1, type)) return true;
        if (HasTypeAt(virtualBoard, x + 1, y, type) && HasTypeAt(virtualBoard, x + 1, y - 1, type) && HasTypeAt(virtualBoard, x, y - 1, type)) return true;
        if (HasTypeAt(virtualBoard, x - 1, y, type) && HasTypeAt(virtualBoard, x - 1, y + 1, type) && HasTypeAt(virtualBoard, x, y + 1, type)) return true;
        if (HasTypeAt(virtualBoard, x + 1, y, type) && HasTypeAt(virtualBoard, x + 1, y + 1, type) && HasTypeAt(virtualBoard, x, y + 1, type)) return true;

        return false;
    }

    private bool HasTypeAt(VirtualTile[,] virtualBoard, int x, int y, TileType type)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        var vTile = virtualBoard[x, y];
        if (vTile == null) return false;
        if (vTile.IsMovableObstacle) return false;

        if (vTile.View != null)
        {
            var model = vTile.View.GetComponent<TileModel>();
            if (model != null) return model.type == type;
        }
        else
        {
            return vTile.SpawnType == type;
        }

        return false;
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
            if (goal.targetType != LevelGoalTargetType.Obstacle) continue;
            if (goal.remaining <= 0) continue;

            var def = board.ActiveLevelData.obstacleLibrary != null
                ? board.ActiveLevelData.obstacleLibrary.Get(goal.obstacleId)
                : null;

            if (def == null) continue;

            if (!def.IsMovableObstacleForRemainingHits(Mathf.Max(1, def.hits))) continue;

            int alive = board.ObstacleStateService.CountAliveOrigins(goal.obstacleId);
            if (alive < goal.remaining)
            {
                obstacleId = goal.obstacleId;
                return true;
            }
        }

        return false;
    }

    private TileView SpawnMovableObstacleTileForFall(int targetX, int targetY, int startX, int startY, ObstacleId obstacleId)
    {
        if (board.ObstacleStateService == null) return null;

        if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(targetX, targetY, obstacleId))
            return null;

        var def = board.ActiveLevelData != null && board.ActiveLevelData.obstacleLibrary != null
            ? board.ActiveLevelData.obstacleLibrary.Get(obstacleId)
            : null;

        if (def == null) return null;

        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
        var view = go.GetComponent<TileView>();
        if (view == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        view.Init(board, targetX, targetY);
        board.ConfigureTileView(view);
        view.SetUseFullCellIcon(false);
        view.SetMovableObstacleTile(true);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        
        // Setup initial visual coordinate before falling
        view.SetCoords(startX, startY);
        view.SnapToGrid(board.TileSize);

        // Target logical coordinate
        view.SetCoords(targetX, targetY);

        TileType dummyType = board.RandomPool != null && board.RandomPool.Length > 0
            ? board.RandomPool[0]
            : TileType.Gear;

        view.SetType(dummyType);
        view.SetSpecial(TileSpecial.None);

        Sprite obstacleSprite = def.GetPreviewSprite();
        if (obstacleSprite != null && view.IconImage != null)
            view.IconImage.sprite = obstacleSprite;

        return view;
    }
}