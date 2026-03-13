using System.Collections.Generic;
using UnityEngine;

public class MatchFinder
{
    private readonly BoardController board;

    public MatchFinder(BoardController board)
    {
        this.board = board;
    }

    private bool IsNormalMatchable(TileData data)
    {
        return data != null && data.Special == TileSpecial.None;
    }

    private bool IsNormalMatchable(TileView tile)
    {
        return tile != null && tile.GetSpecial() == TileSpecial.None;
    }

    public void Add2x2Matches(HashSet<TileData> result)
    {
        for (int y = 0; y < board.Height - 1; y++)
        {
            for (int x = 0; x < board.Width - 1; x++)
            {
                if (board.Holes[x, y] || board.Holes[x + 1, y] || board.Holes[x, y + 1] || board.Holes[x + 1, y + 1])
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

    public HashSet<TileView> FindMatchesAt(int x, int y)
    {
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
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(data);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(data);
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
                var data = board.GridData[x, y];

                if (board.Holes[x, y] || !IsNormalMatchable(data))
                {
                    FlushRun(run, runTiles, result);
                    run = 0;
                    runTiles.Clear();
                    continue;
                }

                var t = data.Type;

                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runTiles.Add(data);
                }
                else if (t.Equals(runType))
                {
                    run++;
                    runTiles.Add(data);
                }
                else
                {
                    FlushRun(run, runTiles, result);
                    run = 1;
                    runType = t;
                    runTiles.Clear();
                    runTiles.Add(data);
                }
            }

            FlushRun(run, runTiles, result);
        }

        Add2x2Matches(result);

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
        {
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

        return result;
    }

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

    public (int hLen, int vLen) GetRunLengths(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return (0, 0);
        if (board.Holes[x, y] || !IsNormalMatchable(board.GridData[x, y])) return (0, 0);

        TileType type = board.GridData[x, y].Type;

        int h = 1;
        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && IsNormalMatchable(board.GridData[lx, y]) && board.GridData[lx, y].Type.Equals(type))
        {
            h++;
            lx--;
        }

        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && IsNormalMatchable(board.GridData[rx, y]) && board.GridData[rx, y].Type.Equals(type))
        {
            h++;
            rx++;
        }

        int v = 1;
        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && IsNormalMatchable(board.GridData[x, uy]) && board.GridData[x, uy].Type.Equals(type))
        {
            v++;
            uy--;
        }

        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && IsNormalMatchable(board.GridData[x, dy]) && board.GridData[x, dy].Type.Equals(type))
        {
            v++;
            dy++;
        }

        return (h, v);
    }

    public TileSpecial DecideSpecialAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return TileSpecial.None;
        if (!IsNormalMatchable(board.GridData[x, y])) return TileSpecial.None;

        var (hLen, vLen) = GetRunLengths(x, y);

        int best = Mathf.Max(hLen, vLen);

        if (best >= 5) return TileSpecial.SystemOverride;

        if (hLen >= 3 && vLen >= 3) return TileSpecial.PulseCore;

        if (best == 4) return (hLen >= vLen) ? TileSpecial.LineH : TileSpecial.LineV;

        if (Has2x2At(x, y)) return TileSpecial.PatchBot;

        return TileSpecial.None;
    }

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
        var snapshot = new List<TileView>(result);

        for (int i = 0; i < snapshot.Count; i++)
        {
            var tile = snapshot[i];
            if (tile == null) continue;

            Add2x2CandidatesOfType(result, tile.X, tile.Y, type);
        }
    }

    private void ExpandByRuns(HashSet<TileView> result, TileType type)
    {
        var snapshot = new List<TileView>(result);

        for (int i = 0; i < snapshot.Count; i++)
        {
            var tile = snapshot[i];
            if (tile == null) continue;

            AddHorizontalRunIfAny(result, tile.X, tile.Y, type);
            AddVerticalRunIfAny(result, tile.X, tile.Y, type);
        }
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

    private void AddHorizontalRunIfAny(HashSet<TileView> result, int x, int y, TileType type)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.Holes[x, y])
            return;

        var center = board.Tiles[x, y];
        if (!IsNormalMatchable(center) || !center.GetTileType().Equals(type))
            return;

        var run = new List<TileView> { center };

        int lx = x - 1;
        while (lx >= 0 && !board.Holes[lx, y] && IsNormalMatchable(board.Tiles[lx, y]) && board.Tiles[lx, y].GetTileType().Equals(type))
        {
            run.Add(board.Tiles[lx, y]);
            lx--;
        }

        int rx = x + 1;
        while (rx < board.Width && !board.Holes[rx, y] && IsNormalMatchable(board.Tiles[rx, y]) && board.Tiles[rx, y].GetTileType().Equals(type))
        {
            run.Add(board.Tiles[rx, y]);
            rx++;
        }

        if (run.Count >= 3)
        {
            for (int i = 0; i < run.Count; i++)
                result.Add(run[i]);
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

        var run = new List<TileView> { center };

        int uy = y - 1;
        while (uy >= 0 && !board.Holes[x, uy] && IsNormalMatchable(board.Tiles[x, uy]) && board.Tiles[x, uy].GetTileType().Equals(type))
        {
            run.Add(board.Tiles[x, uy]);
            uy--;
        }

        int dy = y + 1;
        while (dy < board.Height && !board.Holes[x, dy] && IsNormalMatchable(board.Tiles[x, dy]) && board.Tiles[x, dy].GetTileType().Equals(type))
        {
            run.Add(board.Tiles[x, dy]);
            dy++;
        }

        if (run.Count >= 3)
        {
            for (int i = 0; i < run.Count; i++)
                result.Add(run[i]);
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
}