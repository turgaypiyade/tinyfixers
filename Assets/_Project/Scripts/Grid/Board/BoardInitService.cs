using UnityEngine;

/// <summary>
/// Pure board initialization logic: initial type generation, match avoidance.
/// No MonoBehaviour dependency, no coroutines, no side effects.
/// </summary>
public class BoardInitService
{
    public TileType[,] SimulateInitialTypes(int width, int height, bool[,] holes, TileType[] randomPool)
    {
        var types = new TileType[width, height];
        var matched = new bool[width, height];
        var filled = new bool[width, height];

        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            System.Array.Clear(types, 0, types.Length);
            System.Array.Clear(matched, 0, matched.Length);
            System.Array.Clear(filled, 0, filled.Length);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (holes[x, y]) continue;
                types[x, y] = PickTypeAvoidingMatch(types, filled, x, y, width, height, holes, randomPool);
                filled[x, y] = true;
            }

            MarkInitialMatches(types, matched, width, height, holes);
            if (!HasAnyMatched(matched, width, height)) return types;
        }

        return types;
    }

    private void MarkInitialMatches(TileType[,] types, bool[,] matched, int width, int height, bool[,] holes)
    {
        for (int y = 0; y < height; y++)
        {
            int run = 0; TileType runType = default; int runStart = 0;
            for (int x = 0; x < width; x++)
            {
                if (holes[x, y]) { MarkRunIfNeeded(run, runStart, y, true, matched); run = 0; continue; }
                var t = types[x, y];
                if (run == 0) { run = 1; runType = t; runStart = x; continue; }
                if (t.Equals(runType)) { run++; continue; }
                MarkRunIfNeeded(run, runStart, y, true, matched);
                run = 1; runType = t; runStart = x;
            }
            MarkRunIfNeeded(run, runStart, y, true, matched);
        }

        for (int x = 0; x < width; x++)
        {
            int run = 0; TileType runType = default; int runStart = 0;
            for (int y = 0; y < height; y++)
            {
                if (holes[x, y]) { MarkRunIfNeeded(run, runStart, x, false, matched); run = 0; continue; }
                var t = types[x, y];
                if (run == 0) { run = 1; runType = t; runStart = y; continue; }
                if (t.Equals(runType)) { run++; continue; }
                MarkRunIfNeeded(run, runStart, x, false, matched);
                run = 1; runType = t; runStart = y;
            }
            MarkRunIfNeeded(run, runStart, x, false, matched);
        }
    }

    private void MarkRunIfNeeded(int run, int runStart, int fixedIndex, bool horizontal, bool[,] matched)
    {
        if (run < 3) return;
        for (int i = 0; i < run; i++)
        {
            int x = horizontal ? runStart + i : fixedIndex;
            int y = horizontal ? fixedIndex : runStart + i;
            matched[x, y] = true;
        }
    }

    private bool HasAnyMatched(bool[,] matched, int width, int height)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (matched[x, y]) return true;
        return false;
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
}