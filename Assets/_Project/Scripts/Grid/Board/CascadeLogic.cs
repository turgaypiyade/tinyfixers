using System.Collections.Generic;
using UnityEngine;

public class CascadeLogic
{
    private readonly BoardController board;

    public CascadeLogic(BoardController board)
    {
        this.board = board;
    }

    public List<BoardAction> CalculateCascades()
    {
        var actions = new List<BoardAction>();
        const int maxPass = 32;

        for (int pass = 0; pass < maxPass; pass++)
        {
            if (!HasAnyEmptyPlayableCell()) break;

            var collapseAction = CalculateCollapseAndSpawn();
            if (collapseAction != null && collapseAction.HasMoves)
                actions.Add(collapseAction);

            if (!HasAnyEmptyPlayableCell()) break;

            var slideAction = CalculateSlideFill();
            if (slideAction != null && slideAction.HasMoves)
            {
                actions.Add(slideAction);
                
                // Usually a slide opens up spaces above it, so we need to do another direct collapse straight away.
                var postSlideCollapse = CalculateCollapseColumns();
                if (postSlideCollapse != null && postSlideCollapse.HasMoves)
                {
                    actions.Add(postSlideCollapse);
                }
            }
            else
            {
                // No more slides and falls possible
                break;
            }
        }

        return actions;
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

        for (int x = 0; x < board.Width; x++)
        {
            var colTiles = new List<TileView>(board.Height);
            var colTargetY = new List<int>(board.Height);
            var colDuration = new List<float>(board.Height);
            var colDist = new List<int>(board.Height);
            var colFromY = new List<int>(board.Height);

            int segmentTop = board.Height - 1;
            while (segmentTop >= 0)
            {
                while (segmentTop >= 0 && IsObstacleBlockedCell(x, segmentTop))
                    segmentTop--;

                if (segmentTop < 0)
                    break;

                int segmentBottom = segmentTop;
                while (segmentBottom >= 0 && !IsObstacleBlockedCell(x, segmentBottom))
                    segmentBottom--;

                int topY = segmentBottom + 1;
                bool touchesSpawnEdge = IsSegmentConnectedToSpawnEdge(x, topY);

                var slots = new List<int>();
                var existing = new List<TileView>();

                for (int y = segmentTop; y >= topY; y--)
                {
                    if (board.Holes[x, y]) continue;
                    slots.Add(y);

                    if (board.Tiles[x, y] != null)
                        existing.Add(board.Tiles[x, y]);
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    board.Tiles[x, slots[i]] = null;
                    board.SyncTileData(x, slots[i]);
                }

                for (int i = 0; i < existing.Count && i < slots.Count; i++)
                {
                    int targetY = slots[i];
                    var tile = existing[i];
                    int fromY = tile.Y;

                    board.Tiles[x, targetY] = tile;
                    tile.SetCoords(x, targetY);
                    board.SyncTileData(x, targetY);

                    int dist = Mathf.Abs(targetY - fromY);
                    if (dist > 0)
                    {
                        tile.MarkPlannedToMoveThisFallPass(true);
                        float duration = board.GetFallDurationForDistance(dist);
                        colTiles.Add(tile);
                        colTargetY.Add(targetY);
                        colDuration.Add(duration);
                        colDist.Add(dist);
                        colFromY.Add(fromY);
                    }
                }

                if (touchesSpawnEdge)
                {
                    int nextSpawnY = topY + board.SpawnStartOffsetY;

                    for (int y = topY; y <= segmentTop; y++)
                    {
                        if (board.Holes[x, y]) continue;
                        if (board.Tiles[x, y] != null) continue;

                        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                        var view = go.GetComponent<TileView>();

                        view.Init(board, x, y);
                        view.MarkPlannedToMoveThisFallPass(true);

                        int spawnFromY = nextSpawnY;
                        view.SetCoords(x, spawnFromY);
                        view.SnapToGrid(board.TileSize);
                        nextSpawnY--;

                        view.SetCoords(x, y);
                        board.Tiles[x, y] = view;

                        view.SetType(GetRandomTypeAvoidingImmediateMatch(x, y));
                        view.SetSpecial(TileSpecial.None);
                        board.SyncTileData(x, y);
                        board.RefreshTileObstacleVisual(view);

                        int dist = Mathf.Abs(y - spawnFromY);
                        float duration = board.GetFallDurationForDistance(dist);

                        colTiles.Add(view);
                        colTargetY.Add(y);
                        colDuration.Add(duration);
                        colDist.Add(dist);
                        colFromY.Add(spawnFromY);
                    }
                }

                segmentTop = segmentBottom - 1;
            }

            for (int i = 0; i < colTiles.Count; i++)
            {
                var tile = colTiles[i];
                int targetY = colTargetY[i];
                int dist = colDist[i];
                int fromY = colFromY[i];

                bool useFallSettle = false;
                if (board.ShouldEnableFallSettleThisPass())
                {
                    int belowY = targetY + 1;
                    if (belowY < board.Height)
                    {
                        var belowTile = board.Tiles[x, belowY];
                        if (belowTile != null && !belowTile.IsPlannedToMoveThisFallPass && dist == 1)
                            useFallSettle = true;
                    }
                }

                action.AddMove(tile, fromY, targetY, colDuration[i], useFallSettle, board.FallSettleDuration, board.FallSettleStrength, board.FallMoveCurve);
            }
        }

        board.RefreshAllTileObstacleVisuals();
        return action;
    }

    public FallAction CalculateSlideFill()
    {
        var action = new FallAction();
        var movedThisPass = new HashSet<TileView>();

        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.IsMaskHoleCell(x, y) || IsObstacleBlockedCell(x, y))
                    continue;

                if (board.Tiles[x, y] != null) continue;
                if (!IsSlideFillTarget(x, y)) continue;

                bool TrySource(int sx, int sy)
                {
                    if (sx < 0 || sx >= board.Width || sy < 0 || sy >= board.Height) return false;
                    if (board.IsMaskHoleCell(sx, sy) || IsObstacleBlockedCell(sx, sy)) return false;

                    var t = board.Tiles[sx, sy];
                    if (t == null) return false;

                    bool targetIsObstaclePocket = IsObstacleBlockedCell(x, y - 1);

                    bool HasUsableOtherSource()
                    {
                        int otherSx = (sx == x - 1) ? (x + 1) : (x - 1);
                        int otherSy = y - 1;

                        if (otherSx < 0 || otherSx >= board.Width || otherSy < 0 || otherSy >= board.Height)
                            return false;

                        if (board.IsMaskHoleCell(otherSx, otherSy) || IsObstacleBlockedCell(otherSx, otherSy))
                            return false;

                        return board.Tiles[otherSx, otherSy] != null;
                    }

                    bool otherSourceExists = HasUsableOtherSource();

                    if (!targetIsObstaclePocket && otherSourceExists && CanTileFallStraightDown(sx, sy))
                        return false;

                    return TryDiagonalFrom(sx, sy, x, y, movedThisPass, action);
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
                bool isBoundary = (y == -1) || IsObstacleBlockedCell(x, y);

                if (!isBoundary)
                    continue;

                int segEndY = y + 1;
                if (segEndY <= segStartY)
                {
                    var slots = new List<int>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        slots.Add(yy);
                    }

                    var existing = new List<TileView>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        var tv = board.Tiles[x, yy];
                        if (tv != null)
                            existing.Add(tv);
                    }

                    for (int i = 0; i < slots.Count; i++)
                    {
                        board.Tiles[x, slots[i]] = null;
                        board.SyncTileData(x, slots[i]);
                    }

                    for (int i = 0; i < existing.Count && i < slots.Count; i++)
                    {
                        int toY = slots[i];
                        var tile = existing[i];
                        int fromY = tile.Y;

                        board.Tiles[x, toY] = tile;
                        tile.SetCoords(x, toY);
                        board.SyncTileData(x, toY);

                        if (fromY != toY)
                        {
                            action.AddMove(tile, fromY, toY, board.GetFallDurationForDistance(Mathf.Abs(toY - fromY)), board.EnableFallSettle, board.FallSettleDuration, board.FallSettleStrength, board.FallMoveCurve);
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

        board.Tiles[fromX, fromY] = null;
        board.Tiles[toX, toY] = tile;
        tile.SetCoords(toX, toY);
        board.SyncTileData(fromX, fromY);
        board.SyncTileData(toX, toY);

        float slideDuration = board.GetFallDurationForDistance(1) * 0.6f;
        action.AddMove(tile, fromY, toY, slideDuration, false, 0f, 0f, board.FallMoveCurve);

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
            if (IsObstacleBlockedCell(x, y))
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

        bool obstacleAbove = IsObstacleBlockedCell(x, y - 1);
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
        while (topY > 0 && !IsObstacleBlockedCell(x, topY - 1))
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
            if (IsObstacleBlockedCell(x, yy)) break;
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
            if (IsObstacleBlockedCell(fromX, y)) return false;
            if (board.Holes[fromX, y] && !IsObstacleBlockedCell(fromX, y))
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
}
