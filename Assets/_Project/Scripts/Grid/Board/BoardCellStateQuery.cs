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
        bool isPendingTriggeredSpecial = board.IsPendingTriggeredSpecialCell(x, y);

        // Legacy hole flag: BoardController.Holes bazen mask + blocker bilgisini birlikte taşır.
        // Bu yüzden cascade kararında doğrudan kullanılmamalı.
        bool[,] holes = board.Holes;
        bool isHoleCell = holes != null && holes[x, y];

        var obstacleStateService = board.ObstacleStateService;
        bool hasObstacle = obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y);
        ObstacleId obstacleId = obstacleStateService != null
            ? obstacleStateService.GetObstacleIdAt(x, y)
            : ObstacleId.None;

        TileView[,] tiles = board.Tiles;
        TileView tile = tiles != null ? tiles[x, y] : null;
        bool hasTile = tile != null;

        // Tile'ın kaynak olarak kullanılabilmesi için hem board hücresi hem TileView koordinatı
        // aynı hücreyi göstermeli. Bu guard stale/misplaced TileView problemini yakalar.
        bool tileCoordinatesMatch = tile != null && tile.X == x && tile.Y == y;

        bool canContainTile =
            inBoundsCell &&
            !isMaskHole &&
            !isObstacleBlocked &&
            !isPendingTriggeredSpecial;

        bool canAcceptTile = canContainTile && !hasTile;
        bool canProvideTile = canContainTile && hasTile && tileCoordinatesMatch;

        bool isPassThroughVoid = isMaskHole && !isObstacleBlocked;

        // Diagonal corner geçişi:
        // - Mask hole ve pending special asla geçiş alanı değil.
        // - Normal boş/dolu aktif hücre geçilebilir.
        // - Blocker obstacle ancak allowDiagonal true ise corner olarak geçilebilir.

        bool allowsDiagonalPassThrough =
            inBoundsCell &&
            !isMaskHole &&
            !isPendingTriggeredSpecial &&
            (!isObstacleBlocked || (obstacleStateService != null && obstacleStateService.IsDiagonalAllowedAt(x, y)));


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
            isPendingTriggeredSpecial,
            canContainTile,
            canAcceptTile,
            canProvideTile,
            isPassThroughVoid,
            allowsDiagonalPassThrough);

        return true;
    }
}
