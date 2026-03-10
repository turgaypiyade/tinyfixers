using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleDamageAction : BoardAction
{
    private readonly IEnumerable<ObstacleVisualChange> visualChanges;
    
    public ObstacleDamageAction(IEnumerable<ObstacleVisualChange> visualChanges)
    {
        this.visualChanges = visualChanges;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (visualChanges == null) yield break;

        foreach (var change in visualChanges)
        {
            sequencer.Board.TriggerObstacleVisualChange(change);
        }

        yield return null;
    }
}
