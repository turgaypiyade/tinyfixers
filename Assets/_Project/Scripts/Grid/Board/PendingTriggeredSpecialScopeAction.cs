using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PendingTriggeredSpecialScopeAction : BoardAction
{
    private readonly List<Vector2Int> cells;
    private readonly bool enable;
    private readonly object owner;

    public override bool Blocking => true;

    // owner: aç/kapa çiftinin ORTAK sahibi (aynı nesne verilmeli; ör. combo runtime'ı).
    public PendingTriggeredSpecialScopeAction(IEnumerable<Vector2Int> cells, bool enable, object owner)
    {
        this.owner = owner;
        this.cells = cells != null ? new List<Vector2Int>(cells) : new List<Vector2Int>();
        this.enable = enable;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (sequencer == null || sequencer.Board == null || cells == null || cells.Count == 0)
            yield break;

        if (enable)
            sequencer.Board.SetPendingTriggeredSpecialCells(cells, owner);
        else
            sequencer.Board.ClearPendingTriggeredSpecialCells(cells, owner);
    }
}