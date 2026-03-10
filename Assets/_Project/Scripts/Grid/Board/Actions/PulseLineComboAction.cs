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

        // If no VFX configured, just clear immediately
        if (board.lineTravelPlayer == null)
        {
            foreach (var kvp in targetVisuals)
            {
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            }
            yield break;
        }

        void OnStep(Vector2Int cell)
        {
            if (!targets.Contains(cell)) return;
            if (!cleared.Add(cell)) return;
            
            if (targetVisuals.TryGetValue(cell, out var visualData))
            {
                board.ClearCellVisualOnly(cell, visualData.type, visualData.view);
            }
        }

        float maxEnd = 0f;
        int width = board.Width;
        int height = board.Height;
        float tileSize = board.TileSize;

        foreach (var h in hOrigins)
        {
            int steps = Mathf.Max(cx, width - 1 - cx);
            float end = board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Horizontal,
                h.anch,
                h.cell,
                steps, tileSize, 0f, OnStep);

            if (end > maxEnd) maxEnd = end;
        }

        foreach (var v in vOrigins)
        {
            int steps = Mathf.Max(cy, height - 1 - cy);
            float end = board.PlayLineTravelInstanceWithStep(
                LineTravelSplitSwapTestUI.LineAxis.Vertical,
                v.anch,
                v.cell,
                steps, tileSize, 0f, OnStep);

            if (end > maxEnd) maxEnd = end;
        }

        if (maxEnd > 0f)
            yield return new WaitForSecondsRealtime(maxEnd);

        // Fallback for any targets missed by the visual sweep
        foreach (var kvp in targetVisuals)
        {
            if (cleared.Add(kvp.Key))
            {
                board.ClearCellVisualOnly(kvp.Key, kvp.Value.type, kvp.Value.view);
            }
        }
    }
}
