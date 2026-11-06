public static class DimerdalRules
{
    public const int OpeningHits = 4;
    public static bool IsColor(TileType type) => type == TileType.Bolt || type == TileType.Plate
        || type == TileType.Gear || type == TileType.Core;
    public static bool IsShutter(ObstacleId id) => (int)id >= 84 && (int)id <= 95;
    public static bool IsDiamond(ObstacleId id) => (int)id >= 96 && (int)id <= 99;
    public static bool IsDiamond(TileType type) => type >= TileType.DiamondBlue && type <= TileType.DiamondRed;
    public static TileType DiamondTile(ObstacleId id) => (TileType)((int)TileType.DiamondBlue + ColorIndex(id));
    public static ObstacleId DiamondObstacle(TileType type) => (ObstacleId)(96 + (int)type - (int)TileType.DiamondBlue);
    public const float BoardDiamondSize = .85f;
    public static int ColorIndex(ObstacleId id) => IsShutter(id) ? ((int)id - 84) / 3 : (int)id - 96;
    public static TileType MatchColor(ObstacleId id) => ColorIndex(id) switch
    {
        0 => TileType.Bolt, 1 => TileType.Plate, 2 => TileType.Gear, _ => TileType.Core
    };
    public static ObstacleId DiamondFor(ObstacleId shutter) => (ObstacleId)(96 + ColorIndex(shutter));
}
