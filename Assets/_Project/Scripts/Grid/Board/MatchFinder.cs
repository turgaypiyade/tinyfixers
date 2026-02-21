using System.Collections.Generic;
using UnityEngine;

public class MatchFinder
{
    private readonly BoardController board;

    public MatchFinder(BoardController board)
    {
        this.board = board;
    }

    public void Add2x2Matches(HashSet<TileView> result)
    {
        for (int y = 0; y < board.Height - 1; y++)
            for (int x = 0; x < board.Width - 1; x++)
            {
                if (board.Holes[x, y] || board.Holes[x + 1, y] || board.Holes[x, y + 1] || board.Holes[x + 1, y + 1]) continue;

                var a = board.Tiles[x, y];
                var b = board.Tiles[x + 1, y];
                var c = board.Tiles[x, y + 1];
                var d = board.Tiles[x + 1, y + 1];

                if (a == null || b == null || c == null || d == null) continue;

                var t = a.GetTileType();
                if (!b.GetTileType().Equals(t)) continue;
                if (!c.GetTileType().Equals(t)) continue;
                if (!d.GetTileType().Equals(t)) continue;

                result.Add(a);
                result.Add(b);
                result.Add(c);
                result.Add(d);
            }
    }

    public HashSet<TileView> FindMatchesAt(int x, int y)
    {
        var result = new HashSet<TileView>();

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return result;
        if (board.Holes[x, y]) return result;

        var center = board.Tiles[x, y];
        if (center == null) return result;

        TileType type = center.GetTileType();

        // Horizontal
        var hor = new List<TileView> { center };

        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && board.Tiles[lx, y] != null && board.Tiles[lx, y].GetTileType().Equals(type))
        {
            hor.Add(board.Tiles[lx, y]);
            lx--;
        }

        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && board.Tiles[rx, y] != null && board.Tiles[rx, y].GetTileType().Equals(type))
        {
            hor.Add(board.Tiles[rx, y]);
            rx++;
        }

        if (hor.Count >= 3)
            for (int i = 0; i < hor.Count; i++) result.Add(hor[i]);

        // Vertical
        var ver = new List<TileView> { center };

        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && board.Tiles[x, uy] != null && board.Tiles[x, uy].GetTileType().Equals(type))
        {
            ver.Add(board.Tiles[x, uy]);
            uy--;
        }

        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && board.Tiles[x, dy] != null && board.Tiles[x, dy].GetTileType().Equals(type))
        {
            ver.Add(board.Tiles[x, dy]);
            dy++;
        }

        if (ver.Count >= 3)
            for (int i = 0; i < ver.Count; i++) result.Add(ver[i]);

        return result;
    }

    public HashSet<TileView> FindAllMatches()
    {
        var result = new HashSet<TileView>();

        // Horizontal
        for (int y = 0; y < board.Height; y++)
        {
            int run = 0;
            TileType runType = default;
            var runTiles = new List<TileView>();

            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y] || board.Tiles[x, y] == null)
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = board.Tiles[x, y].GetTileType();

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(board.Tiles[x, y]);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(board.Tiles[x, y]);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(board.Tiles[x, y]);
                }
            }

            FlushRun(run, runTiles, result);
        }

        // Vertical
        for (int x = 0; x < board.Width; x++)
        {
            int run = 0;
            TileType runType = default;
            var runTiles = new List<TileView>();

            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] || board.Tiles[x, y] == null)
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = board.Tiles[x, y].GetTileType();

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(board.Tiles[x, y]);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(board.Tiles[x, y]);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(board.Tiles[x, y]);
                }
            }

            FlushRun(run, runTiles, result);
        }
        Add2x2Matches(result);
        return result;
    }

    public void Add2x2Candidates(HashSet<TileView> candidates, int x, int y)
    {
        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1) continue;
                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1]) continue;

                var a = board.Tiles[sx, sy];
                var b = board.Tiles[sx + 1, sy];
                var c = board.Tiles[sx, sy + 1];
                var d = board.Tiles[sx + 1, sy + 1];

                if (a == null || b == null || c == null || d == null) continue;
                if (!a.GetTileType().Equals(b.GetTileType())) continue;
                if (!a.GetTileType().Equals(c.GetTileType())) continue;
                if (!a.GetTileType().Equals(d.GetTileType())) continue;

                candidates.Add(a);
                candidates.Add(b);
                candidates.Add(c);
                candidates.Add(d);
            }
    }

    public bool HasAnyRunAtLeast(int minLen)
    {
        // Horizontal
        for (int y = 0; y < board.Height; y++)
        {
            int run = 0;
            TileType runType = default;

            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y] || board.Tiles[x, y] == null)
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = board.Tiles[x, y].GetTileType();

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
                if (board.Holes[x, y] || board.Tiles[x, y] == null)
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = board.Tiles[x, y].GetTileType();

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

    public (int hLen, int vLen) GetRunLengths(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return (0, 0);
        if (board.Holes[x, y] || board.Tiles[x, y] == null) return (0, 0);

        TileType type = board.Tiles[x, y].GetTileType();

        int h = 1;
        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && board.Tiles[lx, y] != null && board.Tiles[lx, y].GetTileType().Equals(type)) { h++; lx--; }
        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && board.Tiles[rx, y] != null && board.Tiles[rx, y].GetTileType().Equals(type)) { h++; rx++; }

        int v = 1;
        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && board.Tiles[x, uy] != null && board.Tiles[x, uy].GetTileType().Equals(type)) { v++; uy--; }
        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && board.Tiles[x, dy] != null && board.Tiles[x, dy].GetTileType().Equals(type)) { v++; dy++; }

        return (h, v);
    }

    public TileSpecial DecideSpecialAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return TileSpecial.None;
        if (board.Holes[x, y] || board.Tiles[x, y] == null) return TileSpecial.None;

        var (hLen, vLen) = GetRunLengths(x, y);

        int best = Mathf.Max(hLen, vLen);

        // 5+ straight => System Override
        if (best >= 5) return TileSpecial.SystemOverride;

        bool isFiveCluster = FindMatchesAt(x, y).Count >= 5;

        // L/T (3+3) or 5-cluster => Pulse Core
        if (hLen >= 3 && vLen >= 3 || isFiveCluster) return TileSpecial.PulseCore;

        // 4 straight => Line
        if (best == 4) return (hLen >= vLen) ? TileSpecial.LineH : TileSpecial.LineV;

        // 2x2 => PatchBot (match olsa da olmasa da, biz FindAllMatches’e ekledik)
        if (Has2x2At(x, y)) return TileSpecial.PatchBot;

        return TileSpecial.None;
    }

    public bool Has2x2At(int x, int y)
    {
        if (board.Holes[x, y] || board.Tiles[x, y] == null) return false;
        var t = board.Tiles[x, y].GetTileType();

        // Hücrenin dahil olabileceği 2x2 blokların 4 ihtimali
        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1) continue;
                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1]) continue;

                var a = board.Tiles[sx, sy];
                var b = board.Tiles[sx + 1, sy];
                var c = board.Tiles[sx, sy + 1];
                var d = board.Tiles[sx + 1, sy + 1];

                if (a == null || b == null || c == null || d == null) continue;

                if (!a.GetTileType().Equals(t)) continue;
                if (!b.GetTileType().Equals(t)) continue;
                if (!c.GetTileType().Equals(t)) continue;
                if (!d.GetTileType().Equals(t)) continue;

                return true;
            }

        return false;
    }

    void FlushRun(int run, List<TileView> runTiles, HashSet<TileView> result)
    {
        if (run >= 3)
            for (int i = 0; i < runTiles.Count; i++)
                result.Add(runTiles[i]);
    }
}
