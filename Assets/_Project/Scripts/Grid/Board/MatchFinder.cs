using System.Collections.Generic;
using UnityEngine;

public class MatchFinder
{
    private readonly BoardController board;

    // ── Pooled / reusable buffers (zero GC per call) ──
    private readonly List<TileData> _runTilesBuffer = new List<TileData>(16);
    private readonly List<TileView> _snapshotBuffer = new List<TileView>(32);
    private readonly List<TileView> _runViewBuffer = new List<TileView>(16);

    // ── Pooled result sets — callers borrow these, never allocate ──
    private readonly HashSet<TileData> _findAllResult = new HashSet<TileData>();
    private readonly HashSet<TileView> _findAtResult = new HashSet<TileView>();

    // ── Cached run-length grid ──
    private int[,] _hRunCache;   // horizontal run length passing through (x,y)
    private int[,] _vRunCache;   // vertical   run length passing through (x,y)
    private int _cacheW, _cacheH;
    private bool _cacheValid;    // false → cache must be rebuilt before reading

    public MatchFinder(BoardController board)
    {
        this.board = board;
    }

    // ─────────────────────────────────────────────────────────────
    //  Cache Invalidation — call when the board changes
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the run-length cache as stale. Call this whenever
    /// tiles are swapped, cleared, spawned, or moved on the board
    /// so that GetRunLengths / DecideSpecialAt / FindMatchesAt
    /// see the current board state.
    ///
    /// Lightweight — just flips a flag; actual rebuild happens
    /// lazily the next time the cache is queried.
    /// </summary>
    public void InvalidateRunCache()
    {
        _cacheValid = false;
    }

    /// <summary>
    /// Rebuilds the cache only if it has been invalidated.
    /// Safe to call frequently — no-ops when cache is fresh.
    /// </summary>
    private void EnsureRunCache()
    {
        if (!_cacheValid)
            RebuildRunCache();
    }

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private bool IsNormalMatchable(TileData data)
    {
        if (data == null || data.Special != TileSpecial.None)
            return false;

        if (board.ObstacleStateService != null
            && board.ObstacleStateService.IsMovableObstacleAt(data.X, data.Y))
            return false;

        return true;
    }

    private bool IsNormalMatchable(TileView tile)
    {
        if (tile == null || tile.GetSpecial() != TileSpecial.None)
            return false;

        if (board.ObstacleStateService != null
            && board.ObstacleStateService.IsMovableObstacleAt(tile.X, tile.Y))
            return false;

        return true;
    }
    // ─────────────────────────────────────────────────────────────
    //  Run-length cache — built once, queried O(1)
    // ─────────────────────────────────────────────────────────────

    private void EnsureCacheArrays(int w, int h)
    {
        if (_hRunCache == null || _cacheW != w || _cacheH != h)
        {
            _hRunCache = new int[w, h];
            _vRunCache = new int[w, h];
            _cacheW = w;
            _cacheH = h;
        }
    }

    /// <summary>
    /// Fills _hRunCache and _vRunCache in O(W*H).
    /// Each cell stores the total length of the contiguous run it belongs to.
    /// </summary>
    private void RebuildRunCache()
    {
        int w = board.Width, h = board.Height;
        EnsureCacheArrays(w, h);

        // ── Horizontal runs ──
        for (int y = 0; y < h; y++)
        {
            int runStart = 0;
            int runLen = 0;
            TileType runType = default;

            for (int x = 0; x <= w; x++) // x==w forces flush
            {
                bool extend = false;
                if (x < w && !board.Holes[x, y])
                {
                    var data = board.GridData[x, y];
                    if (IsNormalMatchable(data))
                    {
                        var t = data.Type;
                        if (runLen > 0 && t.Equals(runType))
                        {
                            runLen++;
                            extend = true;
                        }
                        else
                        {
                            FlushRunCache(_hRunCache, runStart, y, runLen, horizontal: true);
                            runStart = x;
                            runLen = 1;
                            runType = t;
                            extend = true;
                        }
                    }
                }

                if (!extend)
                {
                    FlushRunCache(_hRunCache, runStart, y, runLen, horizontal: true);
                    runStart = x + 1;
                    runLen = 0;
                }
            }
        }

        // ── Vertical runs ──
        for (int x = 0; x < w; x++)
        {
            int runStart = 0;
            int runLen = 0;
            TileType runType = default;

            for (int y = 0; y <= h; y++)
            {
                bool extend = false;
                if (y < h && !board.Holes[x, y])
                {
                    var data = board.GridData[x, y];
                    if (IsNormalMatchable(data))
                    {
                        var t = data.Type;
                        if (runLen > 0 && t.Equals(runType))
                        {
                            runLen++;
                            extend = true;
                        }
                        else
                        {
                            FlushRunCache(_vRunCache, x, runStart, runLen, horizontal: false);
                            runStart = y;
                            runLen = 1;
                            runType = t;
                            extend = true;
                        }
                    }
                }

                if (!extend)
                {
                    FlushRunCache(_vRunCache, x, runStart, runLen, horizontal: false);
                    runStart = y + 1;
                    runLen = 0;
                }
            }
        }

        _cacheValid = true;
    }

    private void FlushRunCache(int[,] cache, int fixedOrStartX, int fixedOrStartY, int runLen, bool horizontal)
    {
        if (runLen <= 0) return;

        if (horizontal)
        {
            int y = fixedOrStartY;
            for (int i = 0; i < runLen; i++)
                cache[fixedOrStartX + i, y] = runLen;
        }
        else
        {
            int x = fixedOrStartX;
            for (int i = 0; i < runLen; i++)
                cache[x, fixedOrStartY + i] = runLen;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Cached GetRunLengths — O(1) lookup, auto-rebuilds if stale
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// O(1) run-length query. Auto-rebuilds the cache if it was
    /// invalidated since the last build.
    /// </summary>
    public (int hLen, int vLen) GetRunLengths(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return (0, 0);
        if (board.Holes[x, y] || !IsNormalMatchable(board.GridData[x, y])) return (0, 0);

        EnsureRunCache();

        return (_hRunCache[x, y], _vRunCache[x, y]);
    }

    // ─────────────────────────────────────────────────────────────
    //  2×2 helpers (using cached run lengths)
    // ─────────────────────────────────────────────────────────────

    private bool IsHigherPriorityRunAt(int x, int y)
    {
        var (hLen, vLen) = GetRunLengths(x, y);
        int best = hLen > vLen ? hLen : vLen;
        return best >= 4 || (hLen >= 3 && vLen >= 3);
    }

    private bool SquareOverlapsHigherPriorityRun(int sx, int sy)
    {
        if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1)
            return false;

        return IsHigherPriorityRunAt(sx, sy)
            || IsHigherPriorityRunAt(sx + 1, sy)
            || IsHigherPriorityRunAt(sx, sy + 1)
            || IsHigherPriorityRunAt(sx + 1, sy + 1);
    }

    // ─────────────────────────────────────────────────────────────
    //  FindAllMatches — MAIN HOT PATH
    // ─────────────────────────────────────────────────────────────

    public HashSet<TileData> FindAllMatches()
    {
        _findAllResult.Clear();

        // Always force-rebuild — this is the authoritative entry point
        RebuildRunCache();

        // ── Horizontal matches ──
        for (int y = 0; y < board.Height; y++)
        {
            _runTilesBuffer.Clear();
            int run = 0;
            TileType runType = default;

            for (int x = 0; x < board.Width; x++)
            {
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, _runTilesBuffer, _findAllResult);
                    run = 0;
                    _runTilesBuffer.Clear();
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    _runTilesBuffer.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    _runTilesBuffer.Add(data);
                }
                else
                {
                    FlushRun(run, _runTilesBuffer, _findAllResult);
                    run = 1;
                    runType = t;
                    _runTilesBuffer.Clear();
                    _runTilesBuffer.Add(data);
                }
            }

            FlushRun(run, _runTilesBuffer, _findAllResult);
        }

        // ── Vertical matches ──
        for (int x = 0; x < board.Width; x++)
        {
            _runTilesBuffer.Clear();
            int run = 0;
            TileType runType = default;

            for (int y = 0; y < board.Height; y++)
            {
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, _runTilesBuffer, _findAllResult);
                    run = 0;
                    _runTilesBuffer.Clear();
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    _runTilesBuffer.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    _runTilesBuffer.Add(data);
                }
                else
                {
                    FlushRun(run, _runTilesBuffer, _findAllResult);
                    run = 1;
                    runType = t;
                    _runTilesBuffer.Clear();
                    _runTilesBuffer.Add(data);
                }
            }

            FlushRun(run, _runTilesBuffer, _findAllResult);
        }

        // ── 2×2 matches ──
        Add2x2Matches(_findAllResult);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogFindAllMatchesDebug(_findAllResult);
#endif

        return _findAllResult;
    }

    // ─────────────────────────────────────────────────────────────
    //  2×2 match collection
    // ─────────────────────────────────────────────────────────────

    public void Add2x2Matches(HashSet<TileData> result)
    {
        for (int y = 0; y < board.Height - 1; y++)
        {
            for (int x = 0; x < board.Width - 1; x++)
            {
                if (board.Holes[x, y] || board.Holes[x + 1, y] || board.Holes[x, y + 1] || board.Holes[x + 1, y + 1])
                    continue;

                if (SquareOverlapsHigherPriorityRun(x, y))
                    continue;

                var a = board.GridData[x, y];
                var b = board.GridData[x + 1, y];
                var c = board.GridData[x, y + 1];
                var d = board.GridData[x + 1, y + 1];

                if (!IsNormalMatchable(a) || !IsNormalMatchable(b) || !IsNormalMatchable(c) || !IsNormalMatchable(d))
                    continue;

                var t = a.Type;

                if (!b.Type.Equals(t)) continue;
                if (!c.Type.Equals(t)) continue;
                if (!d.Type.Equals(t)) continue;

                result.Add(a);
                result.Add(b);
                result.Add(c);
                result.Add(d);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  FindMatchesAt (per-tile query, used for swap validation)
    // ─────────────────────────────────────────────────────────────

    public HashSet<TileView> FindMatchesAt(int x, int y)
    {
        InvalidateRunCache();

        var result = new HashSet<TileView>();

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return result;

        if (board.Holes[x, y])
            return result;

        var center = board.Tiles[x, y];
        if (!IsNormalMatchable(center))
            return result;

        TileType type = center.GetTileType();

        AddHorizontalRunIfAny(result, x, y, type);
        AddVerticalRunIfAny(result, x, y, type);
        Add2x2CandidatesOfType(result, x, y, type);

        ExpandMatchGroupClosure(result, type);

        return result;
    }

    // ─────────────────────────────────────────────────────────────
    //  DecideSpecialAt — uses cached run lengths with auto-rebuild
    // ─────────────────────────────────────────────────────────────

    public TileSpecial DecideSpecialAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return TileSpecial.None;
        if (!IsNormalMatchable(board.GridData[x, y])) return TileSpecial.None;

        // EnsureRunCache inside GetRunLengths handles staleness automatically.
        // When called right after FindAllMatches (normal flow), cache is
        // already valid → zero cost. When called after a board mutation
        // (special chain, PatchBot arrival), cache rebuilds once.
        var (hLen, vLen) = GetRunLengths(x, y);

        int best = hLen > vLen ? hLen : vLen;

        if (best >= 5) return TileSpecial.SystemOverride;

        if (hLen >= 3 && vLen >= 3) return TileSpecial.PulseCore;

        if (best == 4) return (hLen >= vLen) ? TileSpecial.LineH : TileSpecial.LineV;

        if (Has2x2At(x, y)) return TileSpecial.PatchBot;

        return TileSpecial.None;
    }

    // ─────────────────────────────────────────────────────────────
    //  Has2x2At
    // ─────────────────────────────────────────────────────────────

    public bool Has2x2At(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!IsNormalMatchable(board.GridData[x, y]))
            return false;

        var t = board.GridData[x, y].Type;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1)
                    continue;

                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1])
                    continue;

                if (SquareOverlapsHigherPriorityRun(sx, sy))
                    continue;

                var a = board.GridData[sx, sy];
                var b = board.GridData[sx + 1, sy];
                var c = board.GridData[sx, sy + 1];
                var d = board.GridData[sx + 1, sy + 1];

                if (!IsNormalMatchable(a) || !IsNormalMatchable(b) || !IsNormalMatchable(c) || !IsNormalMatchable(d))
                    continue;

                if (!a.Type.Equals(t)) continue;
                if (!b.Type.Equals(t)) continue;
                if (!c.Type.Equals(t)) continue;
                if (!d.Type.Equals(t)) continue;

                return true;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  HasAnyRunAtLeast — early-exit scan (no cache needed)
    // ─────────────────────────────────────────────────────────────

    public bool HasAnyRunAtLeast(int minLen)
    {
        // Horizontal
        for (int y = 0; y < board.Height; y++)
        {
            int run = 0;
            TileType runType = default;

            for (int x = 0; x < board.Width; x++)
            {
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                }
                else if (t.Equals(runType))
                {
                    run++;
                }
                else
                {
                    if (run >= minLen) return true;
                    run = 1;
                    runType = t;
                }
            }

            if (run >= minLen) return true;
        }

        // Vertical
        for (int x = 0; x < board.Width; x++)
        {
            int run = 0;
            TileType runType = default;

            for (int y = 0; y < board.Height; y++)
            {
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                }
                else if (t.Equals(runType))
                {
                    run++;
                }
                else
                {
                    if (run >= minLen) return true;
                    run = 1;
                    runType = t;
                }
            }

            if (run >= minLen) return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  Expand closure helpers (pooled snapshot buffers)
    // ─────────────────────────────────────────────────────────────

    private void ExpandMatchGroupClosure(HashSet<TileView> result, TileType type)
    {
        bool changed;
        do
        {
            int before = result.Count;

            ExpandBy2x2(result, type);
            ExpandByRuns(result, type);

            changed = result.Count > before;
        }
        while (changed);
    }

    private void ExpandBy2x2(HashSet<TileView> result, TileType type)
    {
        _snapshotBuffer.Clear();
        _snapshotBuffer.AddRange(result);

        for (int i = 0; i < _snapshotBuffer.Count; i++)
        {
            var tile = _snapshotBuffer[i];
            if (tile == null) continue;

            Add2x2CandidatesOfType(result, tile.X, tile.Y, type);
        }
    }

    private void ExpandByRuns(HashSet<TileView> result, TileType type)
    {
        _snapshotBuffer.Clear();
        _snapshotBuffer.AddRange(result);

        for (int i = 0; i < _snapshotBuffer.Count; i++)
        {
            var tile = _snapshotBuffer[i];
            if (tile == null) continue;

            AddHorizontalRunIfAny(result, tile.X, tile.Y, type);
            AddVerticalRunIfAny(result, tile.X, tile.Y, type);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  2×2 candidate helpers (TileView-based, for FindMatchesAt)
    // ─────────────────────────────────────────────────────────────

    public void Add2x2Candidates(HashSet<TileView> candidates, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.Holes[x, y])
            return;

        var center = board.Tiles[x, y];
        if (!IsNormalMatchable(center))
            return;

        Add2x2CandidatesOfType(candidates, x, y, center.GetTileType());
    }

    private void Add2x2CandidatesOfType(HashSet<TileView> candidates, int x, int y, TileType type)
    {
        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1)
                    continue;

                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1])
                    continue;

                if (SquareOverlapsHigherPriorityRun(sx, sy))
                    continue;

                var a = board.Tiles[sx, sy];
                var b = board.Tiles[sx + 1, sy];
                var c = board.Tiles[sx, sy + 1];
                var d = board.Tiles[sx + 1, sy + 1];

                if (!IsNormalMatchable(a) || !IsNormalMatchable(b) || !IsNormalMatchable(c) || !IsNormalMatchable(d))
                    continue;

                if (!a.GetTileType().Equals(type)) continue;
                if (!b.GetTileType().Equals(type)) continue;
                if (!c.GetTileType().Equals(type)) continue;
                if (!d.GetTileType().Equals(type)) continue;

                candidates.Add(a);
                candidates.Add(b);
                candidates.Add(c);
                candidates.Add(d);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Run helpers (pooled _runViewBuffer)
    // ─────────────────────────────────────────────────────────────

    private void AddHorizontalRunIfAny(HashSet<TileView> result, int x, int y, TileType type)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.Holes[x, y])
            return;

        var center = board.Tiles[x, y];
        if (!IsNormalMatchable(center) || !center.GetTileType().Equals(type))
            return;

        _runViewBuffer.Clear();
        _runViewBuffer.Add(center);

        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && IsNormalMatchable(board.Tiles[lx, y]) && board.Tiles[lx, y].GetTileType().Equals(type))
        {
            _runViewBuffer.Add(board.Tiles[lx, y]);
            lx--;
        }

        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && IsNormalMatchable(board.Tiles[rx, y]) && board.Tiles[rx, y].GetTileType().Equals(type))
        {
            _runViewBuffer.Add(board.Tiles[rx, y]);
            rx++;
        }

        if (_runViewBuffer.Count >= 3)
        {
            for (int i = 0; i < _runViewBuffer.Count; i++)
                result.Add(_runViewBuffer[i]);
        }
    }

    private void AddVerticalRunIfAny(HashSet<TileView> result, int x, int y, TileType type)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.Holes[x, y])
            return;

        var center = board.Tiles[x, y];
        if (!IsNormalMatchable(center) || !center.GetTileType().Equals(type))
            return;

        _runViewBuffer.Clear();
        _runViewBuffer.Add(center);

        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && IsNormalMatchable(board.Tiles[x, uy]) && board.Tiles[x, uy].GetTileType().Equals(type))
        {
            _runViewBuffer.Add(board.Tiles[x, uy]);
            uy--;
        }

        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && IsNormalMatchable(board.Tiles[x, dy]) && board.Tiles[x, dy].GetTileType().Equals(type))
        {
            _runViewBuffer.Add(board.Tiles[x, dy]);
            dy++;
        }

        if (_runViewBuffer.Count >= 3)
        {
            for (int i = 0; i < _runViewBuffer.Count; i++)
                result.Add(_runViewBuffer[i]);
        }
    }

    private void FlushRun(int run, List<TileData> runTiles, HashSet<TileData> result)
    {
        if (run >= 3)
        {
            for (int i = 0; i < runTiles.Count; i++)
                result.Add(runTiles[i]);
        }
    }
    public bool HasAnyPlayableSwap()
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y]) continue;
                var tile = board.Tiles[x, y];
                if (tile == null) continue;

                if (HasAnySpecialSwapAt(x, y))
                    return true;

                if (!IsNormalSwapCandidate(tile))
                    continue;

                if (x + 1 < board.Width && WouldSwapCreateMatch(x, y, x + 1, y))
                    return true;

                if (y + 1 < board.Height && WouldSwapCreateMatch(x, y, x, y + 1))
                    return true;
            }
        }

        return false;
    }

    private bool HasAnySpecialSwapAt(int x, int y)
    {
        var tile = board.Tiles[x, y];
        if (tile == null || tile.GetSpecial() == TileSpecial.None)
            return false;

        return IsSwappableNeighbor(x - 1, y)
            || IsSwappableNeighbor(x + 1, y)
            || IsSwappableNeighbor(x, y - 1)
            || IsSwappableNeighbor(x, y + 1);
    }

    private bool IsSwappableNeighbor(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;
        if (board.Holes[x, y])
            return false;
        return board.Tiles[x, y] != null;
    }

    private bool IsNormalSwapCandidate(TileView tile)
    {
        if (tile == null)
            return false;
        if (tile.GetSpecial() != TileSpecial.None)
            return false;
        if (board.ObstacleStateService != null &&
            board.ObstacleStateService.IsMovableObstacleAt(tile.X, tile.Y))
            return false;

        return true;
    }

    private bool WouldSwapCreateMatch(int ax, int ay, int bx, int by)
    {
        if (bx < 0 || bx >= board.Width || by < 0 || by >= board.Height)
            return false;
        if (board.Holes[bx, by])
            return false;

        var a = board.Tiles[ax, ay];
        var b = board.Tiles[bx, by];
        if (a == null || b == null)
            return false;

        if (!IsNormalSwapCandidate(a) || !IsNormalSwapCandidate(b))
            return false;

        board.Tiles[ax, ay] = b;
        board.Tiles[bx, by] = a;

        a.SetCoords(bx, by);
        b.SetCoords(ax, ay);

        board.SyncTileData(ax, ay);
        board.SyncTileData(bx, by);
        InvalidateRunCache();

        bool hasMatch =
            FindMatchesAt(ax, ay).Count > 0 ||
            FindMatchesAt(bx, by).Count > 0;

        board.Tiles[ax, ay] = a;
        board.Tiles[bx, by] = b;

        a.SetCoords(ax, ay);
        b.SetCoords(bx, by);

        board.SyncTileData(ax, ay);
        board.SyncTileData(bx, by);
        InvalidateRunCache();

        return hasMatch;
    }
    // ─────────────────────────────────────────────────────────────
    //  Debug logging — isolated so release builds pay zero cost
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogFindAllMatchesDebug(HashSet<TileData> result)
    {
        static string TileViewDebugString(TileView tv)
        {
            if (tv == null) return "·";
            var baseChar = tv.GetTileType().ToString()[0];
            var sp = tv.GetSpecial();
            if (sp == TileSpecial.SystemOverride) return "S";
            if (sp == TileSpecial.LineH) return $"{baseChar}-";
            if (sp == TileSpecial.LineV) return $"{baseChar}|";
            if (sp == TileSpecial.PatchBot) return $"{baseChar}B";
            if (sp == TileSpecial.PulseCore) return $"{baseChar}*";
            return baseChar.ToString();
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[MatchFinder] FindAllMatches — {result.Count} matches found");
        sb.AppendLine("  GridData snapshot (#=obstacle, M=mask, ·=null, else type):");
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        {
            sb.Append($"  row{dbgY}: ");
            for (int dbgX = 0; dbgX < board.Width; dbgX++)
            {
                bool isObs = board.IsObstacleBlockedCell(dbgX, dbgY);
                bool isMask = board.IsMaskHoleCell(dbgX, dbgY);

                if (isObs) sb.Append("[# ]");
                else if (isMask) sb.Append("[M ]");
                else if (board.GridData[dbgX, dbgY] == null) sb.Append("[· ]");
                else sb.Append($"[{board.GridData[dbgX, dbgY].ToDebugString().PadRight(2)}]");
            }
            sb.AppendLine();
        }

        sb.AppendLine("  TileView snapshot (#=obstacle, M=mask, ·=null, else type):");
        int mismatchCount = 0;
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        {
            sb.Append($"  row{dbgY}: ");
            for (int dbgX = 0; dbgX < board.Width; dbgX++)
            {
                bool isObs = board.IsObstacleBlockedCell(dbgX, dbgY);
                bool isMask = board.IsMaskHoleCell(dbgX, dbgY);

                if (isObs) sb.Append("[# ]");
                else if (isMask) sb.Append("[M ]");
                else sb.Append($"[{TileViewDebugString(board.Tiles[dbgX, dbgY]).PadRight(2)}]");
            }
            sb.AppendLine();
        }

        sb.AppendLine("  GridData vs TileView mismatch scan:");
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        {
            for (int dbgX = 0; dbgX < board.Width; dbgX++)
            {
                // Obstacle veya mask hole hücreleri mismatch taramasından hariç.
                if (board.IsObstacleBlockedCell(dbgX, dbgY)) continue;
                if (board.IsMaskHoleCell(dbgX, dbgY)) continue;

                var gd = board.GridData[dbgX, dbgY];
                var tv = board.Tiles[dbgX, dbgY];

                if (gd == null && tv == null)
                    continue;

                bool mismatch = false;
                if (gd == null || tv == null)
                {
                    mismatch = true;
                }
                else
                {
                    if (!gd.Type.Equals(tv.GetTileType())) mismatch = true;
                    if (gd.Special != tv.GetSpecial()) mismatch = true;
                }

                if (!mismatch)
                    continue;

                mismatchCount++;
                string gdStr = gd != null ? gd.ToDebugString() : "·";
                string tvStr = TileViewDebugString(tv);
                sb.AppendLine($"    ({dbgX},{dbgY}) GD={gdStr} TV={tvStr} | GD_null={gd == null} TV_null={tv == null}");
            }
        }
        sb.AppendLine($"  Mismatch count: {mismatchCount}");

        if (result.Count > 0)
        {
            sb.AppendLine("  Matched cells:");
            foreach (var m in result)
                sb.AppendLine($"    ({m.X},{m.Y}) {m.Type}");
        }
        Debug.Log(sb.ToString());
    }
#endif
}