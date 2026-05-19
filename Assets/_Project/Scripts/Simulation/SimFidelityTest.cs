using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Attach to any GameObject in a Play-mode scene.
// Right-click the component → "Run Fidelity Test"
// Compares SimMatchFinder output against a fresh MatchFinder on the live board.
public sealed class SimFidelityTest : MonoBehaviour
{
    [SerializeField] private BoardController board;
    [Tooltip("Her frame otomatik koş, sadece match > 0 olanları logla")]
    [SerializeField] private bool autoRun = false;

    private float _nextAutoRun;

    private void Reset()
    {
        board = FindAnyObjectByType<BoardController>();
    }

    private void Update()
    {
        if (!autoRun || board == null) return;
        if (Time.time < _nextAutoRun) return;
        _nextAutoRun = Time.time + 1f; // saniyede bir
        RunFidelityTestInternal(onlyLogWhenMatchesExist: true);
    }

    [ContextMenu("Run Fidelity Test")]
    public void RunFidelityTest()
    {
        RunFidelityTestInternal(onlyLogWhenMatchesExist: false);
    }

    private void RunFidelityTestInternal(bool onlyLogWhenMatchesExist)
    {
        if (board == null)
        {
            Debug.LogError("[SimFidelity] board is null — assign BoardController in inspector.");
            return;
        }

        // ── Live MatchFinder run ──────────────────────────────────────────
        var liveFinder = new MatchFinder(board);
        var liveMatches = liveFinder.FindAllMatches();

        if (onlyLogWhenMatchesExist && liveMatches.Count == 0)
            return;

        var liveCoords = new HashSet<(int, int)>();
        foreach (var td in liveMatches)
            liveCoords.Add((td.X, td.Y));

        // ── Snapshot board state into SimState ────────────────────────────
        int w = board.Width, h = board.Height;
        var grid = new TileData[w, h];
        var holes = new bool[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                holes[x, y] = board.Holes[x, y];

                var src = board.GridData[x, y];
                if (src == null) continue;

                var copy = new TileData(x, y, src.Type);
                if (src.Special != TileSpecial.None)
                {
                    copy.SetSpecial(src.Special);
                    if (src.HasOverrideBaseType)
                        copy.SetOverrideBaseType(src.OverrideBaseType);
                }
                grid[x, y] = copy;
            }
        }

        // Pass the live ObstacleStateService — read-only for match queries
        var simState = new SimState(w, h, grid, holes, board.ObstacleStateService);

        // ── SimMatchFinder run ─────────────────────────────────────────────
        var simFinder = new SimMatchFinder(simState);
        var simMatches = simFinder.FindAllMatches();

        var simCoords = new HashSet<(int, int)>();
        foreach (var td in simMatches)
            simCoords.Add((td.X, td.Y));

        // ── Compare ───────────────────────────────────────────────────────
        var onlyInLive = new List<(int, int)>();
        var onlyInSim = new List<(int, int)>();

        foreach (var c in liveCoords)
            if (!simCoords.Contains(c)) onlyInLive.Add(c);

        foreach (var c in simCoords)
            if (!liveCoords.Contains(c)) onlyInSim.Add(c);

        bool pass = onlyInLive.Count == 0 && onlyInSim.Count == 0;

        var sb = new StringBuilder();
        sb.AppendLine($"[SimFidelity] {(pass ? "PASS" : "FAIL")}  " +
                      $"live={liveMatches.Count} sim={simMatches.Count}  board={w}x{h}");

        if (!pass)
        {
            if (onlyInLive.Count > 0)
            {
                sb.AppendLine($"  MISSING in sim ({onlyInLive.Count}):");
                foreach (var c in onlyInLive) sb.AppendLine($"    ({c.Item1},{c.Item2})");
            }
            if (onlyInSim.Count > 0)
            {
                sb.AppendLine($"  EXTRA in sim ({onlyInSim.Count}):");
                foreach (var c in onlyInSim) sb.AppendLine($"    ({c.Item1},{c.Item2})");
            }
        }

        sb.Append(BuildBoardDump(w, h, holes, grid, liveCoords, simCoords));

        if (pass) Debug.Log(sb.ToString());
        else Debug.LogError(sb.ToString());
    }

    private static string BuildBoardDump(
        int w, int h,
        bool[,] holes, TileData[,] grid,
        HashSet<(int, int)> liveCoords, HashSet<(int, int)> simCoords)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  Board (L=live match, S=sim match, X=both, .=none, #=hole):");

        for (int y = 0; y < h; y++)
        {
            sb.Append($"  row{y}: ");
            for (int x = 0; x < w; x++)
            {
                if (holes[x, y]) { sb.Append("[# ]"); continue; }

                var td = grid[x, y];
                string tile = td != null ? td.ToDebugString().PadRight(2) : "· ";

                bool inL = liveCoords.Contains((x, y));
                bool inS = simCoords.Contains((x, y));
                char mark = (inL && inS) ? 'X' : inL ? 'L' : inS ? 'S' : '.';

                sb.Append($"[{tile}{mark}]");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
