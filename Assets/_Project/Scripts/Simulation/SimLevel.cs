using System;

/// <summary>
/// Main-thread snapshot of the authored level. Worker games never access a Unity
/// object, and editing a level while a batch runs cannot alter that batch's rules.
/// </summary>
public sealed class SimLevel
{
    public readonly string name;
    public readonly int width, height, moves, damagePerClearedTile;
    public readonly LevelKind levelKind;
    public readonly TileType[] randomPool;
    public readonly int[] cells, obstacles, obstacleOrigins, pinnedTileTypes, pinnedSpecialTypes;
    public readonly TubeEntry[] tubes;
    public readonly MagnetEntry[] magnets;
    public readonly SafeEntry[] safes;
    public readonly StackedObstacleEntry[] stackedObstacles;
    public readonly LevelGoalDefinition[] goals;

    public SimLevel(LevelData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        name = source.name;
        width = source.width; height = source.height; moves = source.moves;
        if (width < 1 || height < 1 || moves < 1)
            throw new ArgumentException("Level dimensions and moves must be positive.");
        damagePerClearedTile = source.damagePerClearedTile;
        levelKind = source.levelKind;
        randomPool = Copy(source.randomPool);
        cells = Copy(source.cells);
        obstacles = Copy(source.obstacles);
        obstacleOrigins = Copy(source.obstacleOrigins);
        if (cells.Length != width * height || obstacles.Length != cells.Length || obstacleOrigins.Length != cells.Length)
            throw new ArgumentException("Level grid arrays must have width * height entries.");
        pinnedTileTypes = Copy(source.pinnedTileTypes);
        pinnedSpecialTypes = Copy(source.pinnedSpecialTypes);
        tubes = Copy(source.tubes);
        safes = Copy(source.safes);
        stackedObstacles = Copy(source.stackedObstacles);
        magnets = Copy(source.magnets);
        for (int i = 0; i < magnets.Length; i++)
            magnets[i].pathCellIndices = Copy(magnets[i].pathCellIndices);
        goals = new LevelGoalDefinition[source.goals?.Length ?? 0];
        for (int i = 0; i < goals.Length; i++)
        {
            var g = source.goals[i] ?? throw new ArgumentException("Null level goal.");
            goals[i] = new LevelGoalDefinition
            {
                targetType = g.targetType, tileType = g.tileType, obstacleId = g.obstacleId,
                collectibleId = g.collectibleId, amount = g.amount
            };
        }
    }

    public int Index(int x, int y) => y * width + x;
    private static T[] Copy<T>(T[] values) => values == null ? Array.Empty<T>() : (T[])values.Clone();
}
