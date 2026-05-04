using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class OilSpreadAction : BoardAction
{
    private readonly BoardController _board;
    private readonly List<OilSpreadPair> _pairs;

    public OilSpreadAction(BoardController board, List<OilSpreadPair> pairs)
    {
        _board = board;
        _pairs = pairs;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (_board == null || _pairs == null || _pairs.Count == 0)
            yield break;

        var animator = _board.GetComponent<OilSpreadAnimator>();
        if (animator == null)
            Debug.LogWarning("[OilAnim] OilSpreadAnimator component NOT found on BoardController GameObject — animation skipped.");
        else
            yield return animator.PlaySpread(_pairs);

        foreach (var pair in _pairs)
        {
            if (_board.ObstacleStateService?.TryAddOilAt(pair.Target.x, pair.Target.y) == true)
                _board.RaiseObstacleCreatedDynamic(pair.Target.x, pair.Target.y);
        }

        _board.RefreshOilOverlays();
    }
}
