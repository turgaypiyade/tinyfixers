// Headless gravity + tile refill for simulation.
// Holes split columns into independent segments — tiles compact downward within each segment.
// Simplified: no special tile effects, no diagonal slides.
public static class SimCascade
{
    // Fixed buffer — max board height, avoids per-column allocation.
    private static readonly TileData[] _buf = new TileData[LevelData.MaxHeight];

    // Compact all columns downward, fill empty non-hole cells from the top of each segment.
    // Returns the number of new tiles spawned.
    public static int ApplyGravityAndRefill(SimState s, System.Random rng)
    {
        int spawned = 0;
        for (int x = 0; x < s.Width; x++)
            spawned += ProcessColumn(s, x, rng);
        return spawned;
    }

    private static int ProcessColumn(SimState s, int x, System.Random rng)
    {
        int spawned = 0;
        int h = s.Height;

        int y = h - 1;
        while (y >= 0)
        {
            if (s.Holes[x, y]) { y--; continue; }

            int segBottom = y;
            int segTop = y;
            while (segTop > 0 && !s.Holes[x, segTop - 1])
                segTop--;

            // Collect existing tiles into fixed buffer (bottom→top order)
            int count = 0;
            for (int sy = segBottom; sy >= segTop; sy--)
            {
                if (s.Grid[x, sy] != null)
                    _buf[count++] = s.Grid[x, sy];
            }

            // Place tiles back from bottom, fill remainder from top with new tiles
            int writeY = segBottom;
            for (int i = 0; i < count; i++)
            {
                s.Grid[x, writeY] = _buf[i];
                s.Grid[x, writeY].SetCoords(x, writeY);
                writeY--;
            }
            while (writeY >= segTop)
            {
                var newTile = new TileData(x, writeY, SimState.RandomMatchableType(rng));
                s.Grid[x, writeY] = newTile;
                writeY--;
                spawned++;
            }

            y = segTop - 1;
        }

        return spawned;
    }
}
