using System.Collections.Generic;

public readonly struct SimSwap
{
    public readonly int AX, AY, BX, BY;
    public SimSwap(int ax, int ay, int bx, int by) { AX = ax; AY = ay; BX = bx; BY = by; }
}

// Enumerates valid adjacent swaps for a SimState.
// A swap is valid when:
//   (a) at least one tile is a special (special + anything → always playable), OR
//   (b) the swap would create a match-3 or longer run.
public static class SimMoves
{
    private static readonly List<SimSwap> _buf = new();

    // Returns all valid swaps. List is reused — copy if you need to hold it.
    public static List<SimSwap> FindValid(SimState s)
    {
        _buf.Clear();

        for (int y = 0; y < s.Height; y++)
        {
            for (int x = 0; x < s.Width; x++)
            {
                if (s.Holes[x, y]) continue;
                var a = s.Grid[x, y];
                if (a == null) continue;

                // Right neighbour
                if (x + 1 < s.Width && !s.Holes[x + 1, y])
                {
                    var b = s.Grid[x + 1, y];
                    if (b != null && IsValidSwap(s, a, b, x, y, x + 1, y))
                        _buf.Add(new SimSwap(x, y, x + 1, y));
                }

                // Down neighbour
                if (y + 1 < s.Height && !s.Holes[x, y + 1])
                {
                    var b = s.Grid[x, y + 1];
                    if (b != null && IsValidSwap(s, a, b, x, y, x, y + 1))
                        _buf.Add(new SimSwap(x, y, x, y + 1));
                }
            }
        }

        return _buf;
    }

    // Returns a random valid swap, or null if none exist.
    public static SimSwap? PickRandom(SimState s, System.Random rng)
    {
        var valid = FindValid(s);
        if (valid.Count == 0) return null;
        return valid[rng.Next(valid.Count)];
    }

    // Apply a swap to the state (mutates Grid in-place).
    public static void Apply(SimState s, SimSwap swap)
    {
        var a = s.Grid[swap.AX, swap.AY];
        var b = s.Grid[swap.BX, swap.BY];
        s.Grid[swap.AX, swap.AY] = b;
        s.Grid[swap.BX, swap.BY] = a;
        if (a != null) a.SetCoords(swap.BX, swap.BY);
        if (b != null) b.SetCoords(swap.AX, swap.AY);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsValidSwap(SimState s, TileData a, TileData b, int ax, int ay, int bx, int by)
    {
        // Obstacle-locked cells can't be swapped
        if (s.Obstacles != null)
        {
            if (s.Obstacles.IsInteractionLockedAt(ax, ay)) return false;
            if (s.Obstacles.IsInteractionLockedAt(bx, by)) return false;
            if (s.Obstacles.IsMovableObstacleAt(ax, ay)) return false;
            if (s.Obstacles.IsMovableObstacleAt(bx, by)) return false;
        }

        // Special + anything is always playable
        if (a.Special != TileSpecial.None || b.Special != TileSpecial.None)
            return true;

        return WouldCreateMatch(s, ax, ay, bx, by);
    }

    // Temporarily swap tiles and check if a match forms, then revert.
    private static bool WouldCreateMatch(SimState s, int ax, int ay, int bx, int by)
    {
        var a = s.Grid[ax, ay];
        var b = s.Grid[bx, by];

        // Swap
        s.Grid[ax, ay] = b; s.Grid[bx, by] = a;
        if (a != null) a.SetCoords(bx, by);
        if (b != null) b.SetCoords(ax, ay);

        bool match = CheckRunAt(s, ax, ay) || CheckRunAt(s, bx, by);

        // Revert
        s.Grid[ax, ay] = a; s.Grid[bx, by] = b;
        if (a != null) a.SetCoords(ax, ay);
        if (b != null) b.SetCoords(bx, by);

        return match;
    }

    // Quick H+V run-3 check at (x,y) — no cache, O(W+H) worst case.
    private static bool CheckRunAt(SimState s, int x, int y)
    {
        if (s.Holes[x, y]) return false;
        var tile = s.Grid[x, y];
        if (tile == null || tile.Special != TileSpecial.None) return false;

        var t = tile.Type;

        // Horizontal
        int count = 1;
        for (int lx = x - 1; lx >= 0 && !s.Holes[lx, y] && SameNormalType(s, lx, y, t); lx--) count++;
        for (int rx = x + 1; rx < s.Width && !s.Holes[rx, y] && SameNormalType(s, rx, y, t); rx++) count++;
        if (count >= 3) return true;

        // Vertical
        count = 1;
        for (int uy = y - 1; uy >= 0 && !s.Holes[x, uy] && SameNormalType(s, x, uy, t); uy--) count++;
        for (int dy = y + 1; dy < s.Height && !s.Holes[x, dy] && SameNormalType(s, x, dy, t); dy++) count++;
        return count >= 3;
    }

    private static bool SameNormalType(SimState s, int x, int y, TileType t)
    {
        var d = s.Grid[x, y];
        return d != null && d.Special == TileSpecial.None && d.Type == t;
    }
}
