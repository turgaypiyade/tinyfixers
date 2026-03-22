public readonly struct BoardCellStateSnapshot
{
    public readonly int x;
    public readonly int y;
    public readonly int index;
    public readonly bool inBounds;
    public readonly bool isMaskHole;
    public readonly bool isObstacleBlocked;
    public readonly bool isHole;
    public readonly bool hasObstacle;
    public readonly ObstacleId obstacleId;
    public readonly bool hasTile;
    public readonly TileView tile;
    public readonly bool isPlayableCell;
    public readonly bool isActiveButEmpty;

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
        this.isPlayableCell = isPlayableCell;
        this.isActiveButEmpty = isActiveButEmpty;
    }
}
