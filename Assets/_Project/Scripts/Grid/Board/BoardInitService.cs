using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pure board initialization logic: initial type generation, match avoidance,
/// and safe shuffle generation.
/// No MonoBehaviour dependency, no coroutines, no side effects.
/// </summary>
public class BoardInitService
{
    public TileType[,] SimulateInitialTypes(int width, int height, bool[,] holes, TileType[] randomPool)
    {
        var types = new TileType[width, height];
        var filled = new bool[width, height];

        TileType[,] fallbackCandidate = null;

        const int maxAttempts = 96;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            System.Array.Clear(types, 0, types.Length);
            System.Array.Clear(filled, 0, filled.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (holes[x, y])
                        continue;

                    types[x, y] = PickTypeAvoidingMatch(
                        types,
                        filled,
                        x,
                        y,
                        width,
                        height,
                        holes,
                        randomPool);

                    filled[x, y] = true;
                }
            }

            if (HasImmediateMatch(types, holes))
                continue;

            if (HasAnyPlayableMove(types, holes))
                return CloneTypes(types);

            fallbackCandidate = CloneTypes(types);
        }

        if (fallbackCandidate != null &&
            TryInjectPlayableMove(fallbackCandidate, holes, randomPool) &&
            !HasImmediateMatch(fallbackCandidate, holes) &&
            HasAnyPlayableMove(fallbackCandidate, holes))
        {
            return fallbackCandidate;
        }

        return fallbackCandidate ?? CloneTypes(types);
    }

    private TileType PickTypeAvoidingMatch(TileType[,] types, bool[,] filled, int x, int y,
        int width, int height, bool[,] holes, TileType[] randomPool)
    {
        if (randomPool == null || randomPool.Length == 0) return default;

        int start = Random.Range(0, randomPool.Length);
        for (int i = 0; i < randomPool.Length; i++)
        {
            var candidate = randomPool[(start + i) % randomPool.Length];
            if (!CreatesMatch(types, filled, x, y, candidate, width, height, holes)) return candidate;
        }
        return randomPool[start];
    }

    private bool CreatesMatch(TileType[,] types, bool[,] filled, int x, int y, TileType candidate,
        int width, int height, bool[,] holes)
    {
        int count = 1;
        int lx = x - 1;
        while (lx >= 0 && !holes[lx, y] && filled[lx, y] && types[lx, y].Equals(candidate)) { count++; lx--; }
        int rx = x + 1;
        while (rx < width && !holes[rx, y] && filled[rx, y] && types[rx, y].Equals(candidate)) { count++; rx++; }
        if (count >= 3) return true;

        if (x > 0 && y > 0)
        {
            if (!holes[x - 1, y] && filled[x - 1, y] &&
                !holes[x, y - 1] && filled[x, y - 1] &&
                !holes[x - 1, y - 1] && filled[x - 1, y - 1] &&
                types[x - 1, y].Equals(candidate) &&
                types[x, y - 1].Equals(candidate) &&
                types[x - 1, y - 1].Equals(candidate))
                return true;
        }

        count = 1;
        int uy = y - 1;
        while (uy >= 0 && !holes[x, uy] && filled[x, uy] && types[x, uy].Equals(candidate)) { count++; uy--; }
        int dy = y + 1;
        while (dy < height && !holes[x, dy] && filled[x, dy] && types[x, dy].Equals(candidate)) { count++; dy++; }
        return count >= 3;
    }

    public bool TryBuildSafeShuffleTypes(
        TileType[,] currentTypes,
        bool[,] lockedMask,
        TileType[] randomPool,
        out TileType[,] resultTypes)
    {
        resultTypes = null;

        if (currentTypes == null || lockedMask == null)
            return false;

        int width = currentTypes.GetLength(0);
        int height = currentTypes.GetLength(1);

        var unlockedCells = new List<Vector2Int>(width * height);
        var shuffledTypes = new List<TileType>(width * height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lockedMask[x, y]) continue;
                unlockedCells.Add(new Vector2Int(x, y));
                shuffledTypes.Add(currentTypes[x, y]);
            }
        }

        if (unlockedCells.Count < 2)
            return false;

        TileType[,] fallbackCandidate = null;     // no immediate match, no playable move yet
        TileType[,] lastResortCandidate = null;  // has immediate match — absolute last resort

        const int maxAttempts = 200;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = CloneTypes(currentTypes);
            ShuffleTypes(shuffledTypes);

            for (int i = 0; i < unlockedCells.Count; i++)
            {
                var c = unlockedCells[i];
                candidate[c.x, c.y] = shuffledTypes[i];
            }

            if (HasImmediateMatch(candidate, lockedMask))
            {
                lastResortCandidate ??= candidate;
                continue;
            }

            if (HasAnyPlayableMove(candidate, lockedMask))
            {
                resultTypes = candidate;
                return true;
            }

            fallbackCandidate ??= candidate;
        }

        // Try to inject a playable move into the best clean candidate.
        var injectTarget = fallbackCandidate ?? lastResortCandidate;
        if (injectTarget != null &&
            TryInjectPlayableMove(injectTarget, lockedMask, randomPool))
        {
            resultTypes = injectTarget;
            return true;
        }

        // Accept any shuffled arrangement rather than leaving board unchanged.
        if (injectTarget != null)
        {
            resultTypes = injectTarget;
            return true;
        }

        return false;
    }

    private bool HasImmediateMatch(TileType[,] types, bool[,] lockedMask)
    {
        int width = types.GetLength(0);
        int height = types.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lockedMask[x, y]) continue;
                if (HasMatchAt(types, lockedMask, x, y))
                    return true;
            }
        }

        return false;
    }

    private bool HasAnyPlayableMove(TileType[,] types, bool[,] lockedMask)
    {
        int width = types.GetLength(0);
        int height = types.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lockedMask[x, y]) continue;

                if (x + 1 < width && !lockedMask[x + 1, y] &&
                    WouldSwapCreateMatch(types, lockedMask, x, y, x + 1, y))
                    return true;

                if (y + 1 < height && !lockedMask[x, y + 1] &&
                    WouldSwapCreateMatch(types, lockedMask, x, y, x, y + 1))
                    return true;
            }
        }

        return false;
    }

    private bool TryInjectPlayableMove(TileType[,] types, bool[,] lockedMask, TileType[] randomPool)
    {
        var pool = BuildCandidatePool(types, lockedMask, randomPool);
        int width = types.GetLength(0);
        int height = types.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lockedMask[x, y]) continue;

                if (x + 1 < width && !lockedMask[x + 1, y] &&
                    TryInjectOnPair(types, lockedMask, pool, x, y, x + 1, y))
                    return true;

                if (y + 1 < height && !lockedMask[x, y + 1] &&
                    TryInjectOnPair(types, lockedMask, pool, x, y, x, y + 1))
                    return true;
            }
        }

        return false;
    }

    private bool TryInjectOnPair(
        TileType[,] types,
        bool[,] lockedMask,
        List<TileType> pool,
        int ax, int ay, int bx, int by)
    {
        TileType oldA = types[ax, ay];
        TileType oldB = types[bx, by];

        for (int i = 0; i < pool.Count; i++)
        {
            for (int j = 0; j < pool.Count; j++)
            {
                types[ax, ay] = pool[i];
                types[bx, by] = pool[j];

                if (!HasImmediateMatch(types, lockedMask) &&
                    HasAnyPlayableMove(types, lockedMask))
                    return true;
            }
        }

        types[ax, ay] = oldA;
        types[bx, by] = oldB;
        return false;
    }

    private List<TileType> BuildCandidatePool(TileType[,] types, bool[,] lockedMask, TileType[] randomPool)
    {
        var result = new List<TileType>();
        var seen = new HashSet<TileType>();

        if (randomPool != null)
        {
            for (int i = 0; i < randomPool.Length; i++)
            {
                if (seen.Add(randomPool[i]))
                    result.Add(randomPool[i]);
            }
        }

        int width = types.GetLength(0);
        int height = types.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (lockedMask[x, y]) continue;
                if (seen.Add(types[x, y]))
                    result.Add(types[x, y]);
            }
        }

        return result;
    }

    private bool WouldSwapCreateMatch(
        TileType[,] types,
        bool[,] lockedMask,
        int ax, int ay,
        int bx, int by)
    {
        TileType a = types[ax, ay];
        TileType b = types[bx, by];

        types[ax, ay] = b;
        types[bx, by] = a;

        bool ok = HasMatchAt(types, lockedMask, ax, ay) ||
                  HasMatchAt(types, lockedMask, bx, by);

        types[ax, ay] = a;
        types[bx, by] = b;
        return ok;
    }

    private bool HasMatchAt(TileType[,] types, bool[,] lockedMask, int x, int y)
    {
        int width = types.GetLength(0);
        int height = types.GetLength(1);

        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;
        if (lockedMask[x, y])
            return false;

        TileType type = types[x, y];

        int h = 1;
        for (int lx = x - 1; lx >= 0 && !lockedMask[lx, y] && types[lx, y].Equals(type); lx--) h++;
        for (int rx = x + 1; rx < width && !lockedMask[rx, y] && types[rx, y].Equals(type); rx++) h++;
        if (h >= 3) return true;

        int v = 1;
        for (int uy = y - 1; uy >= 0 && !lockedMask[x, uy] && types[x, uy].Equals(type); uy--) v++;
        for (int dy = y + 1; dy < height && !lockedMask[x, dy] && types[x, dy].Equals(type); dy++) v++;
        if (v >= 3) return true;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sy < 0 || sx >= width - 1 || sy >= height - 1)
                    continue;

                if (lockedMask[sx, sy] || lockedMask[sx + 1, sy] ||
                    lockedMask[sx, sy + 1] || lockedMask[sx + 1, sy + 1])
                    continue;

                if (!types[sx, sy].Equals(type)) continue;
                if (!types[sx + 1, sy].Equals(type)) continue;
                if (!types[sx, sy + 1].Equals(type)) continue;
                if (!types[sx + 1, sy + 1].Equals(type)) continue;

                return true;
            }
        }

        return false;
    }

    private TileType[,] CloneTypes(TileType[,] source)
    {
        int width = source.GetLength(0);
        int height = source.GetLength(1);
        var clone = new TileType[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                clone[x, y] = source[x, y];

        return clone;
    }

    private void ShuffleTypes(List<TileType> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
