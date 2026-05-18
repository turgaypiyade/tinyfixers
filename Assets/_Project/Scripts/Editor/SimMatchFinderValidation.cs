using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Menu: TinyFixers > Validate SimMatchFinder
// Runs deterministic test cases and logs PASS/FAIL to the Console.
// No test framework required — pure MenuItem.
public static class SimMatchFinderValidation
{
    [MenuItem("TinyFixers/Validate SimMatchFinder")]
    public static void RunAll()
    {
        int pass = 0, fail = 0;

        RunCase("H-run of 3",          TestHRun3,          ref pass, ref fail);
        RunCase("V-run of 3",          TestVRun3,          ref pass, ref fail);
        RunCase("H-run of 4",          TestHRun4,          ref pass, ref fail);
        RunCase("V-run of 5",          TestVRun5,          ref pass, ref fail);
        RunCase("2x2 match",           Test2x2,            ref pass, ref fail);
        RunCase("2x2 suppressed by H-run4", Test2x2SuppressedByHRun4, ref pass, ref fail);
        RunCase("2x2 suppressed by V-run4", Test2x2SuppressedByVRun4, ref pass, ref fail);
        RunCase("L-shape → PulseCore", TestPulseCoreLShape, ref pass, ref fail);
        RunCase("No match board",      TestNoMatches,      ref pass, ref fail);
        RunCase("Hole blocks run",     TestHoleBlocksRun,  ref pass, ref fail);
        RunCase("Special tile not matchable", TestSpecialNotMatchable, ref pass, ref fail);
        RunCase("DecideSpecial: LineH vs LineV", TestDecideSpecialLineHV, ref pass, ref fail);
        RunCase("DecideSpecial: PulseCore",      TestDecideSpecialPulseCore, ref pass, ref fail);
        RunCase("DecideSpecial: PatchBot",       TestDecideSpecialPatchBot, ref pass, ref fail);

        Debug.Log($"[SimMatchFinder] Validation complete: {pass} PASS, {fail} FAIL");
    }

    // ── Test cases ───────────────────────────────────────────────────────────

    static string TestHRun3()
    {
        // Row 0: G G G  →  3-match
        // Row 1: C B P  →  no match
        var s = Build(3, 2, new[,]
        {
            { G, G, G },
            { C, B, P },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        return AssertCoords(matches, (0,0),(1,0),(2,0));
    }

    static string TestVRun3()
    {
        // Col 1: G G G  →  3-match
        var s = Build(3, 3, new[,]
        {
            { C, G, B },
            { B, G, C },
            { P, G, P },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        return AssertCoords(matches, (1,0),(1,1),(1,2));
    }

    static string TestHRun4()
    {
        var s = Build(4, 1, new[,]
        {
            { G, G, G, G },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        return AssertCoords(matches, (0,0),(1,0),(2,0),(3,0));
    }

    static string TestVRun5()
    {
        var s = Build(1, 5, new[,]
        {
            { G },
            { G },
            { G },
            { G },
            { G },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        return AssertCoords(matches, (0,0),(0,1),(0,2),(0,3),(0,4));
    }

    static string Test2x2()
    {
        // Top-left 2x2 all G, no longer run anywhere
        var s = Build(3, 3, new[,]
        {
            { G, G, C },
            { G, G, B },
            { C, B, P },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        return AssertCoords(matches, (0,0),(1,0),(0,1),(1,1));
    }

    static string Test2x2SuppressedByHRun4()
    {
        // Row 0: G G G G  → H-run of 4 (priority > 2x2)
        // Row 1: G G C C  → the first 2 G's would form 2x2 with row0[0..1]
        //                    but SquareOverlapsHigherPriorityRun suppresses it
        var s = Build(4, 2, new[,]
        {
            { G, G, G, G },
            { G, G, C, C },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        // Expect: all 4 row0 G's from H-run4, NO extra 2x2 cells
        // Row1 G's at (0,1) and (1,1) should NOT appear since 2x2 is suppressed
        var ok = CoordsContain(matches, (0,0),(1,0),(2,0),(3,0))
                 && !CoordsContain(matches, (0,1))
                 && !CoordsContain(matches, (1,1));
        return ok ? null : "Expected H-run4 only; 2x2 cells appeared despite suppression";
    }

    static string Test2x2SuppressedByVRun4()
    {
        // Col 0: G G G G  → V-run of 4
        // Col 1: G G C C  → (0,0)+(1,0)+(0,1)+(1,1) would be 2x2 but suppressed
        var s = Build(2, 4, new[,]
        {
            { G, G },
            { G, G },
            { G, C },
            { G, C },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        var ok = CoordsContain(matches, (0,0),(0,1),(0,2),(0,3))
                 && !CoordsContain(matches, (1,0))
                 && !CoordsContain(matches, (1,1));
        return ok ? null : "Expected V-run4 only; 2x2 cells appeared despite suppression";
    }

    static string TestPulseCoreLShape()
    {
        // L-shape: H-run of 3 at row1 + V-run of 3 through (1,0..2)
        //   C G C
        //   G G G
        //   C G C
        // (1,0) to (1,2) → V-run of 3; (0,1) to (2,1) → H-run of 3
        var s = Build(3, 3, new[,]
        {
            { C, G, C },
            { G, G, G },
            { C, G, C },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        // Should include all 5 G's in the + shape
        return AssertCoords(matches, (1,0),(0,1),(1,1),(2,1),(1,2));
    }

    static string TestNoMatches()
    {
        // Checkerboard-like board — no 3-in-a-row possible
        //   G C G
        //   C G C
        //   G C G
        var s = Build(3, 3, new[,]
        {
            { G, C, G },
            { C, G, C },
            { G, C, G },
        });
        var matches = new SimMatchFinder(s).FindAllMatches();
        if (matches.Count != 0)
            return $"Expected 0 matches, got {matches.Count}";
        return null;
    }

    static string TestHoleBlocksRun()
    {
        // G G [hole] G G  — hole splits the run; each side only has 2
        var s = new SimState(5, 1,
            new TileData[5, 1]
            {
                { MakeData(0,0,G) }, { MakeData(1,0,G) }, { null }, { MakeData(3,0,G) }, { MakeData(4,0,G) },
            },
            new bool[5, 1] { {false},{false},{true},{false},{false} });
        var matches = new SimMatchFinder(s).FindAllMatches();
        if (matches.Count != 0)
            return $"Expected 0 matches (run split by hole), got {matches.Count}";
        return null;
    }

    static string TestSpecialNotMatchable()
    {
        // A LineH tile in the middle of a run: it is special → not matchable → run breaks
        var s = Build(3, 1, new[,]
        {
            { G, G, G },
        });
        s.Grid[1, 0].SetSpecial(TileSpecial.LineH);

        var matches = new SimMatchFinder(s).FindAllMatches();
        if (matches.Count != 0)
            return $"Expected 0 matches (special tile breaks run), got {matches.Count}";
        return null;
    }

    static string TestDecideSpecialLineHV()
    {
        // H-run of 4 → LineH at the matching cell (hLen >= vLen)
        var s = Build(4, 1, new[,] { { G, G, G, G } });
        var finder = new SimMatchFinder(s);
        finder.FindAllMatches(); // prime the cache
        var sp = finder.DecideSpecialAt(1, 0);
        if (sp != TileSpecial.LineH)
            return $"Expected LineH for H-run4, got {sp}";

        // V-run of 4 → LineV
        s = Build(1, 4, new[,] { { G }, { G }, { G }, { G } });
        finder = new SimMatchFinder(s);
        finder.FindAllMatches();
        sp = finder.DecideSpecialAt(0, 1);
        if (sp != TileSpecial.LineV)
            return $"Expected LineV for V-run4, got {sp}";

        return null;
    }

    static string TestDecideSpecialPulseCore()
    {
        var s = Build(3, 3, new[,]
        {
            { C, G, C },
            { G, G, G },
            { C, G, C },
        });
        var finder = new SimMatchFinder(s);
        finder.FindAllMatches();
        var sp = finder.DecideSpecialAt(1, 1);
        if (sp != TileSpecial.PulseCore)
            return $"Expected PulseCore for + shape, got {sp}";
        return null;
    }

    static string TestDecideSpecialPatchBot()
    {
        // 2x2 of G, no run ≥ 3 elsewhere → PatchBot
        var s = Build(3, 3, new[,]
        {
            { G, G, C },
            { G, G, B },
            { C, B, P },
        });
        var finder = new SimMatchFinder(s);
        finder.FindAllMatches();
        var sp = finder.DecideSpecialAt(0, 0);
        if (sp != TileSpecial.PatchBot)
            return $"Expected PatchBot for 2x2, got {sp}";
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Tile type shortcuts
    static readonly TileType G = TileType.Gear;
    static readonly TileType C = TileType.Core;
    static readonly TileType B = TileType.Bolt;
    static readonly TileType P = TileType.Plate;

    // grid[y, x] layout (row-first for readability in test literals)
    static SimState Build(int w, int h, TileType[,] layout)
    {
        var grid = new TileData[w, h];
        var holes = new bool[w, h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                grid[x, y] = new TileData(x, y, layout[y, x]);

        return new SimState(w, h, grid, holes);
    }

    static TileData MakeData(int x, int y, TileType t) => new TileData(x, y, t);

    // Returns null on pass, error message on fail.
    static string AssertCoords(HashSet<TileData> matches, params (int x, int y)[] expected)
    {
        var got = new HashSet<(int, int)>();
        foreach (var m in matches) got.Add((m.X, m.Y));

        var sb = new StringBuilder();

        foreach (var e in expected)
        {
            if (!got.Contains(e))
                sb.AppendLine($"  MISSING ({e.x},{e.y})");
        }

        var expectedSet = new HashSet<(int, int)>(expected);
        foreach (var g in got)
        {
            if (!expectedSet.Contains(g))
                sb.AppendLine($"  UNEXPECTED ({g.Item1},{g.Item2})");
        }

        return sb.Length == 0 ? null : sb.ToString().TrimEnd();
    }

    static bool CoordsContain(HashSet<TileData> matches, params (int x, int y)[] coords)
    {
        var got = new HashSet<(int, int)>();
        foreach (var m in matches) got.Add((m.X, m.Y));
        foreach (var c in coords)
            if (!got.Contains(c)) return false;
        return true;
    }

    // ── Runner ────────────────────────────────────────────────────────────────

    static void RunCase(string name, Func<string> test, ref int pass, ref int fail)
    {
        try
        {
            string error = test();
            if (error == null)
            {
                Debug.Log($"[SimMatchFinder] PASS  {name}");
                pass++;
            }
            else
            {
                Debug.LogError($"[SimMatchFinder] FAIL  {name}\n{error}");
                fail++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SimMatchFinder] FAIL  {name} (EXCEPTION)\n{ex}");
            fail++;
        }
    }
}
