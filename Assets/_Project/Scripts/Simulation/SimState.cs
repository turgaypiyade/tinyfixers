using System;

// Pure C# board state for headless simulation.
// No Unity scene objects — safe to create/clone outside Play mode.
public sealed class SimState
{
    public readonly int Width;
    public readonly int Height;

    // Grid[x,y] == null  →  cell is empty (tile has fallen out or was cleared)
    // Holes[x,y] == true →  permanent hole — never holds a tile
    public readonly TileData[,] Grid;
    public readonly bool[,] Holes;

    // null = no obstacle layer; all non-hole cells are freely matchable.
    // When set, IsMovableObstacleAt / IsInteractionLockedAt are forwarded here.
    public readonly ISimObstacleQuery Obstacles;

    public SimState(int width, int height, TileData[,] grid, bool[,] holes,
        ISimObstacleQuery obstacles = null)
    {
        Width = width;
        Height = height;
        Grid = grid;
        Holes = holes;
        Obstacles = obstacles;
    }

    // ── Factories ────────────────────────────────────────────────────────────

    // Build a blank board (all holes false, all grid null) of the given size.
    public static SimState Empty(int width, int height)
    {
        return new SimState(width, height,
            new TileData[width, height],
            new bool[width, height]);
    }

    // Fill a board from LevelData layout with random basic tile types.
    // Obstacle-blocked cells are kept as holes so the match finder skips them.
    // Pass a seeded System.Random for reproducible runs.
    public static SimState RandomFill(LevelData level, System.Random rng,
        SimObstacleLayer obstacles = null)
    {
        int w = level.width, h = level.height;
        var holes = new bool[w, h];
        var grid = new TileData[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = level.Index(x, y);

                bool isHole = idx < level.cells.Length &&
                              (CellType)level.cells[idx] == CellType.Empty;

                if (!isHole && obstacles != null && obstacles.IsCellBlocked(x, y))
                    isHole = true;

                holes[x, y] = isHole;

                if (!isHole)
                    grid[x, y] = new TileData(x, y, RandomMatchableType(rng));
            }
        }

        return new SimState(w, h, grid, holes, obstacles);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static TileType RandomMatchableType(System.Random rng)
    {
        // Gear=0, Core=1, Bolt=2, Plate=3 in the enum
        return (TileType)rng.Next(4);
    }

    // Deep-copy the grid so mutations don't affect the original SimState.
    public SimState CloneGrid()
    {
        var newGrid = new TileData[Width, Height];
        var newHoles = new bool[Width, Height];
        Array.Copy(Holes, newHoles, Holes.Length);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var src = Grid[x, y];
                if (src == null) continue;

                var copy = new TileData(x, y, src.Type);
                if (src.Special != TileSpecial.None)
                {
                    copy.SetSpecial(src.Special);
                    if (src.HasOverrideBaseType)
                        copy.SetOverrideBaseType(src.OverrideBaseType);
                }
                newGrid[x, y] = copy;
            }
        }

        return new SimState(Width, Height, newGrid, newHoles, Obstacles);
    }
}
