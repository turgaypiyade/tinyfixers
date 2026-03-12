using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PulseLineComboAction : BoardAction
{
    private BoardController board;
    private int cx, cy;
    private HashSet<Vector2Int> targets;

    private List<(Vector2Int cell, Vector2 anch)> hOrigins;
    private List<(Vector2Int cell, Vector2 anch)> vOrigins;
    private Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals;

    public PulseLineComboAction(
        BoardController board,
        int cx, int cy,
        HashSet<Vector2Int> targets,
        List<(Vector2Int cell, Vector2 anch)> hOrigins,
        List<(Vector2Int cell, Vector2 anch)> vOrigins,
        Dictionary<Vector2Int, (TileType type, TileView view)> targetVisuals)
    {
        this.board = board;
        this.cx = cx;
        this.cy = cy;
        this.targets = targets;
        this.hOrigins = hOrigins;
        this.vOrigins = vOrigins;
        this.targetVisuals = targetVisuals;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        var cleared = new HashSet<Vector2Int>();
        var hiddenOrigins = new HashSet<TileView>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    string CellsToString(IEnumerable<Vector2Int> cells)
    {
        return string.Join(", ", cells);
    }

    Debug.Log(
        $"[LineTravelAction] START targets={targets.Count} " +
        $"hOrigins={hOrigins.Count} vOrigins={vOrigins.Count} " +
        $"targetCells=[{CellsToString(targets)}]");
#endif

        foreach (var h in hOrigins)
        {
            var view = board.GetTileViewAt(h.cell.x, h.cell.y);
            if (view != null && hiddenOrigins.Add(view))
                SpecialVisualService.HideTileVisualForCombo(view);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LineTravelAction] HIDE origin H {h.cell}");
#endif
        }

        foreach (var v in vOrigins)
        {
            var view = board.GetTileViewAt(v.cell.x, v.cell.y);
            if (view != null && hiddenOrigins.Add(view))
                SpecialVisualService.HideTileVisualForCombo(view);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LineTravelAction] HIDE origin V {v.cell}");
#endif
        }

        if (board.lineTravelPlayer == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[LineTravelAction] lineTravelPlayer == null, fallback clear all targets immediately.");
#endif
            foreach (var kvp in targetVisuals)
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            yield break;
        }

        void OnStep(Vector2Int cell)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool isTarget = targets.Contains(cell);
        bool alreadyCleared = cleared.Contains(cell);
        bool hasVisual = targetVisuals.ContainsKey(cell);

        Debug.Log(
            $"[LineTravelAction] STEP cell={cell} " +
            $"isTarget={isTarget} alreadyCleared={alreadyCleared} hasVisual={hasVisual}");
#endif

            if (!targets.Contains(cell)) return;
            if (!cleared.Add(cell)) return;

            if (targetVisuals.TryGetValue(cell, out var visualData))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[LineTravelAction] CLEAR cell={cell} " +
                $"type={visualData.type} view={(visualData.view != null ? visualData.view.name : "null")}");
#endif
                board.ClearCellVisualOnly(cell, visualData.type, visualData.view);
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LineTravelAction] TARGET WITHOUT VISUAL DATA cell={cell}");
#endif
            }
        }

        int pendingTravels = 0;
        int travelIdSeed = 0;

        System.Action<string, Vector2Int> makeCompletedLogger = (axisLabel, originCell) =>
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[LineTravelAction] COMPLETE axis={axisLabel} origin={originCell} " +
            $"pendingBeforeDec={pendingTravels}");
#endif
            pendingTravels = Mathf.Max(0, pendingTravels - 1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[LineTravelAction] COMPLETE axis={axisLabel} origin={originCell} " +
            $"pendingAfterDec={pendingTravels}");
#endif
        };

        int width = board.Width;
        int height = board.Height;
        float tileSize = board.TileSize;

        foreach (var h in hOrigins)
        {
            int steps = Mathf.Max(h.cell.x, width - 1 - h.cell.x);
            pendingTravels++;
            int travelId = ++travelIdSeed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[LineTravelAction] BEGIN travelId={travelId} axis=H origin={h.cell} " +
            $"steps={steps} pendingAfterInc={pendingTravels}");
#endif

            board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Horizontal,
                h.anch,
                h.cell,
                steps,
                tileSize,
                0f,
                OnStep,
                () => makeCompletedLogger("H", h.cell));
        }

        foreach (var v in vOrigins)
        {
            int steps = Mathf.Max(v.cell.y, height - 1 - v.cell.y);
            pendingTravels++;
            int travelId = ++travelIdSeed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[LineTravelAction] BEGIN travelId={travelId} axis=V origin={v.cell} " +
            $"steps={steps} pendingAfterInc={pendingTravels}");
#endif

            board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Vertical,
                v.anch,
                v.cell,
                steps,
                tileSize,
                0f,
                OnStep,
                () => makeCompletedLogger("V", v.cell));
        }

        while (pendingTravels > 0)
            yield return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    var missedBeforeFallback = new List<Vector2Int>();
    foreach (var kvp in targetVisuals)
    {
        if (!cleared.Contains(kvp.Key))
            missedBeforeFallback.Add(kvp.Key);
    }

    Debug.Log(
        $"[LineTravelAction] PRE-FALLBACK cleared={cleared.Count}/{targetVisuals.Count} " +
        $"missed=[{CellsToString(missedBeforeFallback)}]");
#endif

        foreach (var kvp in targetVisuals)
        {
            if (cleared.Add(kvp.Key))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LineTravelAction] FALLBACK CLEAR cell={kvp.Key}");
#endif
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[LineTravelAction] END totalCleared={cleared.Count}");
#endif
    }
}