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
        public float startDelay;
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
            curve = curve,
            startDelay = 0f
        });
    }

    /// <summary>
    /// Baska bir FallAction'in tum move'larini bu action'a ekler.
    /// delayOffset ile pass'lar arasi overlap saglanir.
    /// Ornek: pass1(dikey) + pass2(slide, +0.04s) + pass3(collapse, +0.08s)
    ///        hepsi TEK animasyon olarak paralel calisir.
    /// </summary>
    public void MergeFrom(FallAction other, float delayOffset)
    {
        if (other == null) return;
        foreach (var r in other.fallRecords)
        {
            r.startDelay += delayOffset;
            fallRecords.Add(r);
        }
        other.fallRecords.Clear();
    }

    /// <summary>
    /// En uzun move suresi (delay haric). Pass-arasi overlap hesabi icin.
    /// </summary>
    public float GetMaxMoveDuration()
    {
        float max = 0f;
        foreach (var r in fallRecords)
            if (r.duration > max) max = r.duration;
        return max;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0) yield break;

        float _faStart = Time.realtimeSinceStartup;

        float columnStep = sequencer.Board.FallColumnStep;
        float cascadeStep = sequencer.Board.FallCascadeStep;

        // Debug info
        float maxDur = 0f, minDur = float.MaxValue, maxBaseDelay = 0f;
        int maxDist = 0;
        foreach (var r in fallRecords)
        {
            if (r.duration > maxDur) maxDur = r.duration;
            if (r.duration < minDur) minDur = r.duration;
            int dist = Mathf.Abs(r.toY - r.fromY);
            if (dist > maxDist) maxDist = dist;
            if (r.startDelay > maxBaseDelay) maxBaseDelay = r.startDelay;
        }
        Debug.Log($"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} dur=[{minDur:0.000}-{maxDur:0.000}]s baseDelay=[0-{maxBaseDelay:0.000}]s");

        sequencer.Board.PlayTileFallSfx(fallRecords.Count, maxDist);

        var moves = new List<IEnumerator>(fallRecords.Count);
        var delays = new List<float>(fallRecords.Count);

        int globalMaxFromY = int.MinValue;
        if (cascadeStep > 0f)
        {
            foreach (var r in fallRecords)
                if (r.tile != null && r.fromY > globalMaxFromY) globalMaxFromY = r.fromY;
        }

        float maxTotalDelay = 0f;
        foreach (var r in fallRecords)
        {
            if (r.tile != null)
            {
                moves.Add(r.tile.MoveToGrid(
                    sequencer.Board.TileSize, r.duration, r.curve,
                    r.useSettle, r.settleDuration, r.settleStrength,
                    sequencer.Board.FallSettleStretchX, sequencer.Board.FallSettleOvershoot));

                float colDelay = columnStep > 0f ? r.tile.X * columnStep : 0f;
                float rowDelay = (cascadeStep > 0f && globalMaxFromY > int.MinValue)
                    ? Mathf.Max(0, globalMaxFromY - r.fromY) * cascadeStep
                    : 0f;
                float totalDelay = r.startDelay + colDelay + rowDelay;
                delays.Add(totalDelay);
                if (totalDelay > maxTotalDelay) maxTotalDelay = totalDelay;
            }
        }

        Debug.Log($"[Fall] stagger maxDelay={maxTotalDelay:0.000}s colStep={columnStep} cascStep={cascadeStep}");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - _faStart):0.000}s (expected ~{maxDur + maxTotalDelay:0.000}s)");
    }
}