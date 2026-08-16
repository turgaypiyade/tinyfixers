public readonly struct BoardCellStateSnapshot
{
    public readonly int x;
    public readonly int y;
    public readonly int index;

    public readonly bool inBounds;

    // Level mask hole: gerçek board dışı/boş alan. Tile target/source değildir.
    public readonly bool isMaskHole;

    // Obstacle veya pending-special gibi gravity/cascade'i bloklayan durum.
    public readonly bool isObstacleBlocked;

    // BoardController.Holes legacy flag'i. Bu flag mask + blocker karışımı olabilir;
    // cascade kararı için tek başına kullanılmamalı.
    public readonly bool isHole;

    public readonly bool hasObstacle;
    public readonly ObstacleId obstacleId;

    public readonly bool hasTile;
    public readonly TileView tile;
    public readonly TileRuntimeState tileRuntimeState;
    public readonly bool isReservedForTile;

    // Fiziksel olarak tile tutabilecek aktif hücre.
    // Mask hole, blocker obstacle ve pending-triggered special hücreleri false döner.
    public readonly bool isPlayableCell;

    // Backward compatible isim: cascade açısından gerçek boş target.
    public readonly bool isActiveButEmpty;

    // Yeni açık semantik alanlar.
    public readonly bool isPendingTriggeredSpecial;
    public readonly bool canContainTile;
    public readonly bool canAcceptTile;
    public readonly bool canProvideTile;
    public readonly bool isPassThroughVoid;
    public readonly bool allowsDiagonalPassThrough;

    // Eski constructor korunuyor; başka kodlar eski imzayla new'liyorsa kırılmasın.
    public BoardCellStateSnapshot(
        int x,
        int y,
        int index,
        bool inBounds,
        bool isMaskHole,
        bool isObstacleBlocked,
        bool isHole,
        bool hasObstacle,
        ObstacleId obstacleId,
        bool hasTile,
        TileView tile,
        bool isPlayableCell,
        bool isActiveButEmpty)
    {
        this.x = x;
        this.y = y;
        this.index = index;
        this.inBounds = inBounds;
        this.isMaskHole = isMaskHole;
        this.isObstacleBlocked = isObstacleBlocked;
        this.isHole = isHole;
        this.hasObstacle = hasObstacle;
        this.obstacleId = obstacleId;
        this.hasTile = hasTile;
        this.tile = tile;
        this.tileRuntimeState = tile != null ? tile.RuntimeState : TileRuntimeState.Idle;
        this.isReservedForTile = false;
        this.isPlayableCell = isPlayableCell;
        this.isActiveButEmpty = isActiveButEmpty;

        this.isPendingTriggeredSpecial = false;
        this.canContainTile = inBounds && !isMaskHole && !isObstacleBlocked;
        this.canAcceptTile = this.canContainTile && !hasTile;
        this.canProvideTile = this.canContainTile && hasTile && tile != null && tile.X == x && tile.Y == y;
        this.isPassThroughVoid = isMaskHole && !isObstacleBlocked;
        this.allowsDiagonalPassThrough = inBounds && !isMaskHole && !isObstacleBlocked;
    }

    public BoardCellStateSnapshot(
        int x,
        int y,
        int index,
        bool inBounds,
        bool isMaskHole,
        bool isObstacleBlocked,
        bool isHole,
        bool hasObstacle,
        ObstacleId obstacleId,
        bool hasTile,
        TileView tile,
        TileRuntimeState tileRuntimeState,
        bool isReservedForTile,
        bool isPendingTriggeredSpecial,
        bool canContainTile,
        bool canAcceptTile,
        bool canProvideTile,
        bool isPassThroughVoid,
        bool allowsDiagonalPassThrough)
    {
        this.x = x;
        this.y = y;
        this.index = index;
        this.inBounds = inBounds;
        this.isMaskHole = isMaskHole;
        this.isObstacleBlocked = isObstacleBlocked;
        this.isHole = isHole;
        this.hasObstacle = hasObstacle;
        this.obstacleId = obstacleId;
        this.hasTile = hasTile;
        this.tile = tile;
        this.tileRuntimeState = tileRuntimeState;
        this.isReservedForTile = isReservedForTile;

        this.isPendingTriggeredSpecial = isPendingTriggeredSpecial;
        this.canContainTile = canContainTile;
        this.canAcceptTile = canAcceptTile;
        this.canProvideTile = canProvideTile;
        this.isPassThroughVoid = isPassThroughVoid;
        this.allowsDiagonalPassThrough = allowsDiagonalPassThrough;

        // Eski alan adlarını yeni, net semantiğe bağlıyoruz.
        this.isPlayableCell = canContainTile;
        this.isActiveButEmpty = canAcceptTile;
    }
}
