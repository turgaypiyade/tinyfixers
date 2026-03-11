using System.Collections.Generic;
using UnityEngine;

public class MatchFinder
{
    private readonly BoardController board;

    public MatchFinder(BoardController board)
    {
        this.board = board;
    }

    public void Add2x2Matches(HashSet<TileData> result)
    {
        for (int y = 0; y < board.Height - 1; y++)
            for (int x = 0; x < board.Width - 1; x++)
            {
                if (board.Holes[x, y] || board.Holes[x + 1, y] || board.Holes[x, y + 1] || board.Holes[x + 1, y + 1]) continue;

                var a = board.GridData[x, y];
                var b = board.GridData[x + 1, y];
                var c = board.GridData[x, y + 1];
                var d = board.GridData[x + 1, y + 1];

                if (a == null || b == null || c == null || d == null) continue;

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

    public HashSet<TileData> FindMatchesAt(int x, int y)
    {
        var result = new HashSet<TileData>();

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return result;
        if (board.Holes[x, y]) return result;

        var center = board.GridData[x, y];
        if (center == null) return result;

        TileType type = center.Type;

        // Horizontal
        var hor = new List<TileData> { center };

        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && board.GridData[lx, y] != null && board.GridData[lx, y].Type.Equals(type))
        {
            hor.Add(board.GridData[lx, y]);
            lx--;
        }

        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && board.GridData[rx, y] != null && board.GridData[rx, y].Type.Equals(type))
        {
            hor.Add(board.GridData[rx, y]);
            rx++;
        }

        if (hor.Count >= 3)
            for (int i = 0; i < hor.Count; i++) result.Add(hor[i]);

        // Vertical
        var ver = new List<TileData> { center };

        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && board.GridData[x, uy] != null && board.GridData[x, uy].Type.Equals(type))
        {
            ver.Add(board.GridData[x, uy]);
            uy--;
        }

        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && board.GridData[x, dy] != null && board.GridData[x, dy].Type.Equals(type))
        {
            ver.Add(board.GridData[x, dy]);
            dy++;
        }

        if (ver.Count >= 3)
            for (int i = 0; i < ver.Count; i++) result.Add(ver[i]);

        return result;
    }

    public HashSet<TileData> FindAllMatches()
    {
        var result = new HashSet<TileData>();

        // Horizontal
        for (int y = 0; y < board.Height; y++)
        {
            int run = 0;
            TileType runType = default;
            var runTiles = new List<TileData>();

            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y] || board.GridData[x, y] == null)
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = board.GridData[x, y].Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(board.GridData[x, y]);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(board.GridData[x, y]);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(board.GridData[x, y]);
                }
            }

            FlushRun(run, runTiles, result);
        }

        // Vertical
        for (int x = 0; x < board.Width; x++)
        {
            int run = 0;
            TileType runType = default;
            var runTiles = new List<TileData>();

            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] || board.GridData[x, y] == null)
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = board.GridData[x, y].Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(board.GridData[x, y]);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(board.GridData[x, y]);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(board.GridData[x, y]);
                }
            }

            FlushRun(run, runTiles, result);
        }

        // ─── DEBUG: board snapshot + matches ───────────────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        sb.AppendLine("  GridData snapshot (H=Hole, ·=null, else type):");
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        {
            sb.Append($"  row{dbgY}: ");
            for (int dbgX = 0; dbgX < board.Width; dbgX++)
            {
                if (board.Holes[dbgX, dbgY]) sb.Append("[H ]");
                else if (board.GridData[dbgX, dbgY] == null) sb.Append("[· ]");
                else sb.Append($"[{board.GridData[dbgX, dbgY].ToDebugString().PadRight(2)}]");
            }
            sb.AppendLine();
        }

        sb.AppendLine("  TileView snapshot (H=Hole, ·=null, else type):");
        int mismatchCount = 0;
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        {
            sb.Append($"  row{dbgY}: ");
            for (int dbgX = 0; dbgX < board.Width; dbgX++)
            {
               if (board.Holes[dbgX, dbgY]) sb.Append("[H ]");
               else sb.Append($"[{TileViewDebugString(board.Tiles[dbgX, dbgY]).PadRight(2)}]");
            }
           sb.AppendLine();
        }

        sb.AppendLine("  GridData vs TileView mismatch scan:");
        for (int dbgY = 0; dbgY < board.Height; dbgY++)
        for (int dbgX = 0; dbgX < board.Width; dbgX++)
        {
            if (board.Holes[dbgX, dbgY])
                continue;

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
        sb.AppendLine($"  Mismatch count: {mismatchCount}");

        if (result.Count > 0)
        {
            sb.AppendLine("  Matched cells:");
            foreach (var m in result)
                sb.AppendLine($"    ({m.X},{m.Y}) {m.Type}");
        }
        Debug.Log(sb.ToString());
#endif
        // ─── END DEBUG ─────────────────────────────────────────────────────────

        return result;
    }

    public void Add2x2Candidates(HashSet<TileData> candidates, int x, int y)
    {
        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1) continue;
                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1]) continue;

                var a = board.GridData[sx, sy];
                var b = board.GridData[sx + 1, sy];
                var c = board.GridData[sx, sy + 1];
                var d = board.GridData[sx + 1, sy + 1];

                if (a == null || b == null || c == null || d == null) continue;
                if (!a.Type.Equals(b.Type)) continue;
                if (!a.Type.Equals(c.Type)) continue;
                if (!a.Type.Equals(d.Type)) continue;

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
                if (board.GridData[x, y] == null)
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = board.GridData[x, y].Type;

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
                if (board.Holes[x, y] || board.GridData[x, y] == null)
                {
                    if (run >= minLen) return true;
                    run = 0;
                    continue;
                }

                var t = board.GridData[x, y].Type;

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
        if (board.Holes[x, y] || board.GridData[x, y] == null) return (0, 0);

        TileType type = board.GridData[x, y].Type;

        int h = 1;
        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && board.GridData[lx, y] != null && board.GridData[lx, y].Type.Equals(type)) { h++; lx--; }
        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && board.GridData[rx, y] != null && board.GridData[rx, y].Type.Equals(type)) { h++; rx++; }

        int v = 1;
        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && board.GridData[x, uy] != null && board.GridData[x, uy].Type.Equals(type)) { v++; uy--; }
        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && board.GridData[x, dy] != null && board.GridData[x, dy].Type.Equals(type)) { v++; dy++; }

        return (h, v);
    }

    public TileSpecial DecideSpecialAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return TileSpecial.None;
        if (board.GridData[x, y] == null) return TileSpecial.None;

        var (hLen, vLen) = GetRunLengths(x, y);

        int best = Mathf.Max(hLen, vLen);

        // 5+ straight => System Override
        if (best >= 5) return TileSpecial.SystemOverride;

        bool isFiveCluster = FindMatchesAt(x, y).Count >= 5;

        // L/T (3+3) or 5-cluster => Pulse Core
        if (hLen >= 3 && vLen >= 3 || isFiveCluster) return TileSpecial.PulseCore;

        // 4 straight => Line
        if (best == 4) return (hLen >= vLen) ? TileSpecial.LineH : TileSpecial.LineV;

        // 2x2 => PatchBot (yalnızca çağrıldığı bağlamda; global auto-match listesine eklenmez)
        if (Has2x2At(x, y)) return TileSpecial.PatchBot;

        return TileSpecial.None;
    }

    public bool Has2x2At(int x, int y)
    {
        if (board.GridData[x, y] == null) return false;
        var t = board.GridData[x, y].Type;

        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;

                if (sx < 0 || sx >= board.Width - 1 || sy < 0 || sy >= board.Height - 1) continue;
                if (board.Holes[sx, sy] || board.Holes[sx + 1, sy] || board.Holes[sx, sy + 1] || board.Holes[sx + 1, sy + 1]) continue;

                var a = board.GridData[sx, sy];
                var b = board.GridData[sx + 1, sy];
                var c = board.GridData[sx, sy + 1];
                var d = board.GridData[sx + 1, sy + 1];

                if (a == null || b == null || c == null || d == null) continue;

                if (!a.Type.Equals(t)) continue;
                if (!b.Type.Equals(t)) continue;
                if (!c.Type.Equals(t)) continue;
                if (!d.Type.Equals(t)) continue;

                return true;
            }

        return false;
    }

    void FlushRun(int run, List<TileData> runTiles, HashSet<TileData> result)
    {
        if (run >= 3)
            for (int i = 0; i < runTiles.Count; i++)
                result.Add(runTiles[i]);
    }
}
