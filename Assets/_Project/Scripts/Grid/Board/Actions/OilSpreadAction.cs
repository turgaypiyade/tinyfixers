using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class OilSpreadAction : BoardAction
{
    private readonly BoardController _board;
    private readonly List<Vector2Int> _spreadTargets;

    public OilSpreadAction(BoardController board, List<Vector2Int> spreadTargets)
    {
        _board = board;
        _spreadTargets = spreadTargets;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        foreach (var cell in _spreadTargets)
            _board.ObstacleStateService.TryAddOilAt(cell.x, cell.y);

        var animator = _board.GetComponent<OilSpreadAnimator>();
        if (animator != null)
            yield return animator.PlaySpread(_spreadTargets);

        _board.RefreshOilOverlays();
    }
}
