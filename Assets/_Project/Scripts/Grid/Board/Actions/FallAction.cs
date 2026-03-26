using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallAction : BoardAction
{
    private class FallRecord
    {
        public TileView tile;
        public int fromY;
        public int toY;
        public float duration;
        public bool useSettle;
        public float settleDuration;
        public float settleStrength;
        public AnimationCurve curve;
    }

    private List<FallRecord> fallRecords = new List<FallRecord>();
    public bool HasMoves => fallRecords.Count > 0;

    public void AddMove(TileView tile, int fromY, int toY, float duration, bool useSettle, float settleDur, float settleStr, AnimationCurve curve)
    {
        fallRecords.Add(new FallRecord
        {
            tile = tile,
            fromY = fromY,
            toY = toY,
            duration = duration,
            useSettle = useSettle,
            settleDuration = settleDur,
            settleStrength = settleStr,
            curve = curve
        });
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0) yield break;

        float columnStep = sequencer.Board.FallColumnStep;
        bool useStagger = columnStep > 0f;

        var moves = new List<IEnumerator>(fallRecords.Count);
        var delays = useStagger ? new List<float>(fallRecords.Count) : null;

        foreach (var r in fallRecords)
        {
            if (r.tile != null)
            {
                moves.Add(r.tile.MoveToGrid(sequencer.Board.TileSize, r.duration, r.curve, r.useSettle, r.settleDuration, r.settleStrength));
                if (useStagger)
                    delays.Add(r.tile.X * columnStep);
            }
        }

        if (useStagger)
            yield return sequencer.Animator.RunManyWithDelays(moves, delays);
        else
            yield return sequencer.Animator.RunMany(moves);
    }
}