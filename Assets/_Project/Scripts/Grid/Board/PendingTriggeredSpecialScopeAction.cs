using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PendingTriggeredSpecialScopeAction : BoardAction
{
    private readonly List<Vector2Int> cells;
    private readonly bool enable;

    public override bool Blocking => true;

    public PendingTriggeredSpecialScopeAction(IEnumerable<Vector2Int> cells, bool enable)
    {
        this.cells = cells != null ? new List<Vector2Int>(cells) : new List<Vector2Int>();
        this.enable = enable;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (sequencer == null || sequencer.Board == null || cells == null || cells.Count == 0)
            yield break;

        if (enable)
            sequencer.Board.SetPendingTriggeredSpecialCells(cells);
        else
            sequencer.Board.ClearPendingTriggeredSpecialCells(cells);
    }
}