public static class BoardCellStateQuery
{
    public static bool TryGet(BoardController board, int x, int y, out BoardCellStateSnapshot state)
    {
        state = default;
        if (board == null)
            return false;

        int width = board.Width;
        int height = board.Height;
        bool inBoundsCell = x >= 0 && x < width && y >= 0 && y < height;
        if (!inBoundsCell)
            return false;

        int index = y * width + x;
        bool isMaskHole = board.IsMaskHoleCell(x, y);
        bool isObstacleBlocked = board.IsObstacleBlockedCell(x, y);

        bool[,] holes = board.Holes;
        bool isHoleCell = holes != null && holes[x, y];

        var obstacleStateService = board.ObstacleStateService;
        bool hasObstacle = obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y);
        ObstacleId obstacleId = obstacleStateService != null ? obstacleStateService.GetObstacleIdAt(x, y) : ObstacleId.None;

        TileView[,] tiles = board.Tiles;
        TileView tile = tiles != null ? tiles[x, y] : null;
        bool hasTile = tile != null;

        bool isPlayableCell = inBoundsCell && !isMaskHole;
        bool isActiveButEmpty = isPlayableCell && !isObstacleBlocked && !hasTile;

        state = new BoardCellStateSnapshot(
            x,
            y,
            index,
            inBoundsCell,
            isMaskHole,
            isObstacleBlocked,
            isHoleCell,
            hasObstacle,
            obstacleId,
            hasTile,
            tile,
            isPlayableCell,
            isActiveButEmpty);

        return true;
    }
}
