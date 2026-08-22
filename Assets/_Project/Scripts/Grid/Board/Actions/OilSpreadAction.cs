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

        var committedPairs = new List<OilSpreadPair>(_pairs.Count);
        foreach (var pair in _pairs)
        {
            // Gameplay state first: once oil starts spreading into a cell, that cell must no longer
            // participate in match/swap even if the visual blob is still flying in.
            if (_board.ObstacleStateService?.TryAddOilAt(pair.Target.x, pair.Target.y) == true)
            {
                committedPairs.Add(pair);
                _board.RaiseObstacleCreatedDynamic(pair.Target.x, pair.Target.y);
            }
        }

        if (committedPairs.Count == 0)
        {
            _board.RefreshOilOverlays();
            yield break;
        }

        var animator = _board.GetComponent<OilSpreadAnimator>();
        if (animator == null)
            Debug.LogWarning("[OilAnim] OilSpreadAnimator component NOT found on BoardController GameObject — animation skipped.");
        else
            yield return animator.PlaySpread(committedPairs);

        _board.RefreshOilOverlays();
    }
}
