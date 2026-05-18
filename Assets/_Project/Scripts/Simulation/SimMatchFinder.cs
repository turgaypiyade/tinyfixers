using System.Collections.Generic;

// Headless match finder that mirrors MatchFinder logic exactly.
// Operates on SimState instead of BoardController — no Unity scene objects needed.
// Return type matches MatchFinder.FindAllMatches() → HashSet<TileData>.
public sealed class SimMatchFinder
{
    private readonly SimState _s;

    // Reusable buffers (zero extra GC per call, same pattern as MatchFinder)
    private readonly List<TileData> _runBuf = new(16);

    // Run-length cache — rebuilt lazily, same logic as MatchFinder
    private int[,] _hRun;
    private int[,] _vRun;
    private int _cacheW, _cacheH;
    private bool _cacheValid;

    public SimMatchFinder(SimState state)
    {
        _s = state;
    }

    // Pooled result set — reused every call, same pattern as live MatchFinder.
    private readonly HashSet<TileData> _result = new(64);

    // ── Public API ───────────────────────────────────────────────────────────

    public void InvalidateRunCache() => _cacheValid = false;

    // Main entry — mirrors MatchFinder.FindAllMatches().
    // Always force-rebuilds the cache (same as the live version).
    // NOTE: returned HashSet is reused on the next call — copy if you need to hold it.
    public HashSet<TileData> FindAllMatches()
    {
        _result.Clear();
        var result = _result;

        RebuildRunCache();

        // ── Horizontal runs ──
        for (int y = 0; y < _s.Height; y++)
        {
            _runBuf.Clear();
            int run = 0;
            TileType runType = default;

            for (int x = 0; x < _s.Width; x++)
            {
                var data = _s.Grid[x, y];

                if (_s.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, _runBuf, result);
                    run = 0;
                    _runBuf.Clear();
                    continue;
                }

                var t = data.Type;
                if (run == 0)
                {
                    run = 1; runType = t; _runBuf.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++; _runBuf.Add(data);
                }
                else
                {
                    FlushRun(run, _runBuf, result);
                    run = 1; runType = t; _runBuf.Clear(); _runBuf.Add(data);
                }
            }

            FlushRun(run, _runBuf, result);
        }

        // ── Vertical runs ──
        for (int x = 0; x < _s.Width; x++)
        {
            _runBuf.Clear();
            int run = 0;
            TileType runType = default;

            for (int y = 0; y < _s.Height; y++)
            {
                var data = _s.Grid[x, y];

                if (_s.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, _runBuf, result);
                    run = 0;
                    _runBuf.Clear();
                    continue;
                }

                var t = data.Type;
                if (run == 0)
                {
                    run = 1; runType = t; _runBuf.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++; _runBuf.Add(data);
                }
                else
                {
                    FlushRun(run, _runBuf, result);
                    run = 1; runType = t; _runBuf.Clear(); _runBuf.Add(data);
                }
            }

            FlushRun(run, _runBuf, result);
        }

        // ── 2×2 matches ──
        Add2x2Matches(result);

        return result;
    }

    // Mirrors MatchFinder.DecideSpecialAt — picks the special that would form at (x,y).
    public TileSpecial DecideSpecialAt(int x, int y)
    {
        if (x < 0 || x >= _s.Width || y < 0 || y >= _s.Height) return TileSpecial.None;
        if (!IsNormalMatchable(_s.Grid[x, y])) return TileSpecial.None;

        EnsureRunCache();
        var (hLen, vLen) = (_hRun[x, y], _vRun[x, y]);
        int best = hLen > vLen ? hLen : vLen;

        if (best >= 5) return TileSpecial.SystemOverride;
        if (hLen >= 3 && vLen >= 3) return TileSpecial.PulseCore;
        if (best == 4) return hLen >= vLen ? TileSpecial.LineH : TileSpecial.LineV;
        if (Has2x2At(x, y)) return TileSpecial.PatchBot;

        return TileSpecial.None;
    }

    // Returns true if any H/V run ≥ minLen exists (early-exit scan, no cache needed).
    public bool HasAnyRunAtLeast(int minLen)
    {
        for (int y = 0; y < _s.Height; y++)
        {
            int run = 0; TileType runType = default;
            for (int x = 0; x < _s.Width; x++)
            {
                var data = _s.Grid[x, y];
                if (_s.Holes[x, y] || !IsNormalMatchable(data))
                { if (run >= minLen) return true; run = 0; continue; }
                var t = data.Type;
                if (run == 0) { run = 1; runType = t; }
                else if (t.Equals(runType)) run++;
                else { if (run >= minLen) return true; run = 1; runType = t; }
            }
            if (run >= minLen) return true;
        }

        for (int x = 0; x < _s.Width; x++)
        {
            int run = 0; TileType runType = default;
            for (int y = 0; y < _s.Height; y++)
            {
                var data = _s.Grid[x, y];
                if (_s.Holes[x, y] || !IsNormalMatchable(data))
                { if (run >= minLen) return true; run = 0; continue; }
                var t = data.Type;
                if (run == 0) { run = 1; runType = t; }
                else if (t.Equals(runType)) run++;
                else { if (run >= minLen) return true; run = 1; runType = t; }
            }
            if (run >= minLen) return true;
        }

        return false;
    }

    // ── Run-length cache ─────────────────────────────────────────────────────

    private void EnsureRunCache()
    {
        if (!_cacheValid) RebuildRunCache();
    }

    private void RebuildRunCache()
    {
        int w = _s.Width, h = _s.Height;

        if (_hRun == null || _cacheW != w || _cacheH != h)
        {
            _hRun = new int[w, h];
            _vRun = new int[w, h];
            _cacheW = w; _cacheH = h;
        }
        else
        {
            System.Array.Clear(_hRun, 0, w * h);
            System.Array.Clear(_vRun, 0, w * h);
        }

        // Horizontal
        for (int y = 0; y < h; y++)
        {
            int runStart = 0, runLen = 0; TileType runType = default;
            for (int x = 0; x <= w; x++)
            {
                bool extend = false;
                if (x < w && !_s.Holes[x, y])
                {
                    var data = _s.Grid[x, y];
                    if (IsNormalMatchable(data))
                    {
                        var t = data.Type;
                        if (runLen > 0 && t.Equals(runType)) { runLen++; extend = true; }
                        else { FlushRunCache(_hRun, runStart, y, runLen, true); runStart = x; runLen = 1; runType = t; extend = true; }
                    }
                }
                if (!extend) { FlushRunCache(_hRun, runStart, y, runLen, true); runStart = x + 1; runLen = 0; }
            }
        }

        // Vertical
        for (int x = 0; x < w; x++)
        {
            int runStart = 0, runLen = 0; TileType runType = default;
            for (int y = 0; y <= h; y++)
            {
                bool extend = false;
                if (y < h && !_s.Holes[x, y])
                {
                    var data = _s.Grid[x, y];
                    if (IsNormalMatchable(data))
                    {
                        var t = data.Type;
                        if (runLen > 0 && t.Equals(runType)) { runLen++; extend = true; }
                        else { FlushRunCache(_vRun, x, runStart, runLen, false); runStart = y; runLen = 1; runType = t; extend = true; }
                    }
                }
                if (!extend) { FlushRunCache(_vRun, x, runStart, runLen, false); runStart = y + 1; runLen = 0; }
            }
        }

        _cacheValid = true;
    }

    private void FlushRunCache(int[,] cache, int fx, int fy, int runLen, bool horizontal)
    {
        if (runLen <= 0) return;
        if (horizontal) for (int i = 0; i < runLen; i++) cache[fx + i, fy] = runLen;
        else for (int i = 0; i < runLen; i++) cache[fx, fy + i] = runLen;
    }

    // ── 2×2 helpers ──────────────────────────────────────────────────────────

    private void Add2x2Matches(HashSet<TileData> result)
    {
        for (int y = 0; y < _s.Height - 1; y++)
        {
            for (int x = 0; x < _s.Width - 1; x++)
            {
                if (_s.Holes[x, y] || _s.Holes[x + 1, y] || _s.Holes[x, y + 1] || _s.Holes[x + 1, y + 1])
                    continue;

                if (SquareOverlapsHigherPriorityRun(x, y))
                    continue;

                var a = _s.Grid[x, y];
                var b = _s.Grid[x + 1, y];
                var c = _s.Grid[x, y + 1];
                var d = _s.Grid[x + 1, y + 1];

                if (!IsNormalMatchable(a) || !IsNormalMatchable(b) || !IsNormalMatchable(c) || !IsNormalMatchable(d))
                    continue;

                var t = a.Type;
                if (!b.Type.Equals(t) || !c.Type.Equals(t) || !d.Type.Equals(t))
                    continue;

                result.Add(a); result.Add(b); result.Add(c); result.Add(d);
            }
        }
    }

    private bool Has2x2At(int x, int y)
    {
        if (!IsNormalMatchable(_s.Grid[x, y])) return false;
        var t = _s.Grid[x, y].Type;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox, sy = y + oy;
                if (sx < 0 || sx >= _s.Width - 1 || sy < 0 || sy >= _s.Height - 1) continue;
                if (_s.Holes[sx, sy] || _s.Holes[sx + 1, sy] || _s.Holes[sx, sy + 1] || _s.Holes[sx + 1, sy + 1]) continue;
                if (SquareOverlapsHigherPriorityRun(sx, sy)) continue;

                var a = _s.Grid[sx, sy]; var b = _s.Grid[sx + 1, sy];
                var c = _s.Grid[sx, sy + 1]; var d = _s.Grid[sx + 1, sy + 1];

                if (!IsNormalMatchable(a) || !IsNormalMatchable(b) || !IsNormalMatchable(c) || !IsNormalMatchable(d)) continue;
                if (!a.Type.Equals(t) || !b.Type.Equals(t) || !c.Type.Equals(t) || !d.Type.Equals(t)) continue;
                return true;
            }
        }
        return false;
    }

    private bool SquareOverlapsHigherPriorityRun(int sx, int sy)
    {
        if (sx < 0 || sx >= _s.Width - 1 || sy < 0 || sy >= _s.Height - 1) return false;
        return IsHigherPriorityRunAt(sx, sy) || IsHigherPriorityRunAt(sx + 1, sy)
            || IsHigherPriorityRunAt(sx, sy + 1) || IsHigherPriorityRunAt(sx + 1, sy + 1);
    }

    private bool IsHigherPriorityRunAt(int x, int y)
    {
        if (_s.Holes[x, y] || !IsNormalMatchable(_s.Grid[x, y])) return false;
        EnsureRunCache();
        int hLen = _hRun[x, y], vLen = _vRun[x, y];
        int best = hLen > vLen ? hLen : vLen;
        return best >= 4 || (hLen >= 3 && vLen >= 3);
    }

    // ── IsNormalMatchable — mirrors MatchFinder exactly ──────────────────────

    private bool IsNormalMatchable(TileData data)
    {
        if (data == null || data.Special != TileSpecial.None)
            return false;

        if (_s.Obstacles != null)
        {
            if (_s.Obstacles.IsMovableObstacleAt(data.X, data.Y))
                return false;
            if (_s.Obstacles.IsInteractionLockedAt(data.X, data.Y))
                return false;
        }

        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void FlushRun(int run, List<TileData> buf, HashSet<TileData> result)
    {
        if (run >= 3)
            for (int i = 0; i < buf.Count; i++)
                result.Add(buf[i]);
    }
}
