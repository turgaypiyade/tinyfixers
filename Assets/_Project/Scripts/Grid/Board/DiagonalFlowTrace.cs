#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Opt-in rolling history for one board. No per-frame logging or player-build code.
[InitializeOnLoad]
internal static class DiagonalFlowTrace
{
    private const string Menu = "TinyFixers/Debug/Diagonal Flow/";
    private const int MaxEvents = 240;
    private const int MaxCharacters = 48000;
    private const string EnabledKey = "TinyFixers.DiagonalFlowTrace.Enabled";
    private static readonly Queue<string> history = new Queue<string>();
    private static BoardController owner;
    private static bool armed;
    private static string pendingBefore;
    private static bool wrotePlan;
    private static int events, plans, characters, discarded;
    internal static string OutputPath => Path.GetFullPath("Library/DiagonalFlowTrace.log");

    static DiagonalFlowTrace()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Finish("play-ended");
        };
        AssemblyReloadEvents.beforeAssemblyReload += () => Finish("scripts-reloaded");
    }

    [MenuItem(Menu + "Rolling Capture")]
    private static void Toggle()
    {
        bool enabled = !SessionState.GetBool(EnabledKey, false);
        SessionState.SetBool(EnabledKey, enabled);
        if (!enabled) Finish("disabled");
        Debug.Log(enabled
            ? "[DiagonalFlow] Döngüsel kayıt açık. Normal oyna; sorunu görünce Diagonal Flow > Save Now. Son 240 olay / 48 KB tutulur."
            : "[DiagonalFlow] Döngüsel kayıt kapalı.");
    }

    [MenuItem(Menu + "Rolling Capture", true)]
    private static bool ValidateToggle()
    {
        UnityEditor.Menu.SetChecked(Menu + "Rolling Capture", SessionState.GetBool(EnabledKey, false));
        return true;
    }

    [MenuItem(Menu + "Save Now")]
    private static void SaveNow()
    {
        SessionState.SetBool(EnabledKey, false);
        Finish("manual");
    }

    [MenuItem(Menu + "Save Now", true)]
    private static bool CanSave() => armed;

    private static void Update()
    {
        if (armed && owner == null) Finish("board-destroyed");
        if (!Application.isPlaying || armed || !SessionState.GetBool(EnabledKey, false)) return;
        owner = UnityEngine.Object.FindFirstObjectByType<BoardController>();
        if (owner == null) return;
        history.Clear();
        events = plans = characters = discarded = 0;
        pendingBefore = null;
        wrotePlan = false;
        armed = true;
    }

    internal static bool Enabled(BoardController board) => armed && owner == board;

    internal static void BeginPlan(BoardController board)
    {
        if (!Enabled(board)) return;
        pendingBefore = Snapshot(board, "BEFORE");
        wrotePlan = false;
    }

    internal static void Slide(BoardController board, TileView tile, int spawnOrder,
        int fromX, int fromY, int sourceY, int toX, int toY, List<Vector2Int> path)
    {
        if (!Enabled(board)) return;
        StartPlan();
        Append($"PLAN-SLIDE g={board.FallGeneration} {Identity(tile)} spawn={spawnOrder} " +
               $"source=({fromX},{sourceY}) corner=({fromX},{fromY})->({toX},{toY}) path={PathText(path)}");
    }

    internal static void Route(BoardController board, TileView tile, int spawnOrder, List<Vector2Int> path)
    {
        if (!Enabled(board) || path.Count < 2) return;
        StartPlan();
        Append($"ROUTE g={board.FallGeneration} {Identity(tile)} spawn={spawnOrder} " +
               $"pos={Position(board, tile.RectTransform.anchoredPosition)} path={PathText(path)}");
    }

    internal static void EndPlan(BoardController board)
    {
        if (!Enabled(board) || !wrotePlan) return;
        Append(Snapshot(board, "AFTER"));
        plans++;
    }

    // Landing triggers many no-op cascade probes; keep the history for actual moves.
    private static void StartPlan()
    {
        if (wrotePlan) return;
        wrotePlan = true;
        if (pendingBefore != null) Append(pendingBefore);
    }

    internal static void Motion(BoardController board, string kind, TileView tile, int generation,
        Vector2 position, Vector2 next, Vector2Int target, float speed, TileView blocker = null)
    {
        if (!Enabled(board)) return;
        string blockedBy = blocker == null ? "-" :
            $"{Identity(blocker)} pos={Position(board, blocker.RectTransform.anchoredPosition)} " +
            $"target=({blocker.X},{blocker.Y}) state={blocker.RuntimeState}";
        Append($"{kind} g={generation} {Identity(tile)} pos={Position(board, position)} " +
               $"next={Position(board, next)} target=({target.x},{target.y}) speed={speed:0.00} " +
               $"blocker=[{blockedBy}]");
    }

    private static string Identity(TileView tile) => tile == null ? "-" :
        $"tile={tile.GetInstanceID()}/{tile.LifetimeVersion}";

    private static string Position(BoardController board, Vector2 p) =>
        $"({p.x / board.TileSize:0.00},{-p.y / board.TileSize:0.00})";

    private static string PathText(List<Vector2Int> path)
    {
        var text = new StringBuilder();
        foreach (var cell in path)
        {
            if (text.Length > 0) text.Append('>');
            text.Append('(').Append(cell.x).Append(',').Append(cell.y).Append(')');
        }
        return text.ToString();
    }

    private static string Snapshot(BoardController board, string label)
    {
        var text = new StringBuilder($"{label} g={board.FallGeneration}\n");
        if (board.Tiles == null) return text.Append("no-board").ToString();
        for (int y = 0; y < board.Height; y++)
        {
            text.Append(y).Append(": ");
            for (int x = 0; x < board.Width; x++)
                text.Append(board.IsPendingTriggeredSpecialCell(x, y) ? 'H' :
                    board.IsObstacleBlockedCell(x, y) ? '#' : board.IsMaskHoleCell(x, y) ? 'o' :
                    board.Tiles[x, y] == null ? '.' : 'T');
            text.AppendLine();
        }
        return text.ToString();
    }

    private static void Append(string message)
    {
        if (!armed) return;
        string line = $"f={Time.frameCount} t={Time.time:0.000} {message}\n";
        if (line.Length > MaxCharacters - 2000) line = line.Substring(0, MaxCharacters - 2000);
        while (history.Count > 0 && (history.Count >= MaxEvents || characters + line.Length > MaxCharacters - 2000))
        {
            characters -= history.Dequeue().Length;
            discarded++;
        }
        events++;
        history.Enqueue(line);
        characters += line.Length;
    }

    private static void Finish(string reason)
    {
        if (!armed) return;
        if (owner != null) Append(Snapshot(owner, "CAPTURE-END"));
        armed = false;
        var buffer = new StringBuilder();
        buffer.AppendLine($"level={owner?.ActiveLevelData?.name} board={owner?.Width}x{owner?.Height} " +
                          $"continuous={owner?.UseContinuousFallMotion} perColumn={owner?.UsePerColumnGravity} " +
                          $"maxSlides={owner?.MaxDiagonalSlidesPerCascade}");
        buffer.AppendLine("Cells: x right, y down, zero-based. # blocker, H held, o hole, . empty, T tile. " +
                          "pos values are visual cell coordinates; target is the already planned logical cell.");
        foreach (string line in history) buffer.Append(line);
        buffer.AppendLine($"END reason={reason} retained={history.Count} discarded={discarded} events={events} plans={plans}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, buffer.ToString());
            Debug.Log($"[DiagonalFlow] Kayıt tamam: son {history.Count} olay, {reason}. Dosya: {OutputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiagonalFlow] Kayıt yazılamadı: {ex.Message}");
        }
        owner = null;
    }
}
#endif
