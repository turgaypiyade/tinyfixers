using System.Collections;
using UnityEngine;

public class SwapAction : BoardAction
{
    private TileView tileA;
    private TileView tileB;
    private float duration;

    public SwapAction(TileView a, TileView b, float duration)
    {
        this.tileA = a;
        this.tileB = b;
        this.duration = duration;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (tileA == null || tileB == null) yield break;
        yield return sequencer.Animator.SwapTilesAnimated(tileA, tileB, duration);
    }
}
