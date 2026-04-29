using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallAction : BoardAction
{
    private class FallRecord
    {
        public TileView tile;

        public int fromX;
        public int fromY;
        public int toX;
        public int toY;

        public float duration;
        public bool useSettle;
        public float settleDuration;
        public float settleStrength;
        public AnimationCurve curve;
        public float startDelay;
        public float phaseDelay;
        public bool hasPhaseDelay;
    }

    private readonly List<FallRecord> fallRecords = new List<FallRecord>();

    public bool HasMoves => fallRecords.Count > 0;

    // Eski imza kalsin. Baska yerler kirilmasin.
    public void AddMove(
        TileView tile,
        int fromY,
        int toY,
        float duration,
        bool useSettle,
        float settleDur,
        float settleStr,
        AnimationCurve curve,
        float startDelay = 0f)
    {
        int x = tile != null ? tile.X : 0;

        AddMove(
            tile,
            x,
            fromY,
            x,
            toY,
            duration,
            useSettle,
            settleDur,
            settleStr,
            curve,
            startDelay);
    }

    // Yeni asil imza. Fall visual'i artik TileView.X/Y'den degil,
    // record icindeki from/to cell bilgisinden okunur.
    public void AddMove(
        TileView tile,
        int fromX,
        int fromY,
        int toX,
        int toY,
        float duration,
        bool useSettle,
        float settleDur,
        float settleStr,
        AnimationCurve curve,
        float startDelay = 0f)
    {
        if (tile == null || !tile)
            return;

        fallRecords.Add(new FallRecord
        {
            tile = tile,
            fromX = fromX,
            fromY = fromY,
            toX = toX,
            toY = toY,
            duration = Mathf.Max(0.0001f, duration),
            useSettle = useSettle,
            settleDuration = Mathf.Max(0f, settleDur),
            settleStrength = settleStr,
            curve = curve,
            startDelay = Mathf.Max(0f, startDelay)
        });
    }

    public void MergeFrom(FallAction other, float delayOffset)
    {
        if (other == null)
            return;

        foreach (var r in other.fallRecords)
        {
            r.startDelay += delayOffset;
            fallRecords.Add(r);
        }

        other.fallRecords.Clear();
    }

    public float GetMaxMoveDuration()
    {
        float max = 0f;

        foreach (var r in fallRecords)
        {
            if (r.duration > max)
                max = r.duration;
        }

        return max;
    }

    public float GetEstimatedVisualDuration(BoardController board)
    {
        if (fallRecords.Count == 0)
            return 0f;

        EnsurePhaseDelays(board);

        float maxEnd = 0f;

        foreach (var r in fallRecords)
        {
            if (r.tile == null || !r.tile)
                continue;

            float settleTime = r.useSettle ? Mathf.Max(0f, r.settleDuration) : 0f;
            float endTime = r.startDelay + r.phaseDelay + r.duration + settleTime;

            if (endTime > maxEnd)
                maxEnd = endTime;
        }

        return maxEnd;
    }

    // ============================================================
    // SABIT HIZ + ADAPTIVE START DELAY
    //
    // Sureler CascadeLogic/BoardController tarafinda tek fall velocity
    // kaynagindan hesaplanir. Bu sinif sadece scheduling yapar.
    // ============================================================

    private static readonly float[] SPAWN_INTERVALS = new float[]
    {
        0.018f,
        0.023f,
        0.030f,
    };

    private const float SPAWN_INTERVAL_TERMINAL = 0.035f;

    private static float CumulativeSpawnDelay(int rank)
    {
        if (rank <= 0)
            return 0f;

        float total = 0f;

        for (int i = 0; i < rank; i++)
        {
            total += (i < SPAWN_INTERVALS.Length)
                ? SPAWN_INTERVALS[i]
                : SPAWN_INTERVAL_TERMINAL;
        }

        return total;
    }

    private void EnsurePhaseDelays(BoardController board)
    {
        bool allFrozen = true;

        foreach (var r in fallRecords)
        {
            if (!r.hasPhaseDelay)
            {
                allFrozen = false;
                break;
            }
        }

        if (allFrozen)
            return;

        float columnStep = board != null ? board.FallColumnStep : 0f;
        var maxToYPerColumn = new Dictionary<int, int>();

        foreach (var r in fallRecords)
        {
            if (r.hasPhaseDelay)
                continue;

            if (r.tile == null || !r.tile)
                continue;

            int col = r.toX;

            if (!maxToYPerColumn.ContainsKey(col) || r.toY > maxToYPerColumn[col])
                maxToYPerColumn[col] = r.toY;
        }

        foreach (var r in fallRecords)
        {
            if (r.hasPhaseDelay)
                continue;

            if (r.tile == null || !r.tile)
                continue;

            int rankFromBottom = 0;
            if (maxToYPerColumn.TryGetValue(r.toX, out int maxToY))
                rankFromBottom = Mathf.Max(0, maxToY - r.toY);

            float spawnDelay = CumulativeSpawnDelay(rankFromBottom);
            float colDelay = columnStep > 0f ? r.toX * columnStep : 0f;

            r.phaseDelay = colDelay + spawnDelay;
            r.hasPhaseDelay = true;
        }
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0)
            yield break;

        float faStart = Time.realtimeSinceStartup;

        int maxDist = 0;

        foreach (var r in fallRecords)
        {
            int dist = Mathf.CeilToInt(Vector2.Distance(
                new Vector2(r.fromX, r.fromY),
                new Vector2(r.toX, r.toY)));

            if (dist > maxDist)
                maxDist = dist;
        }

        Debug.Log($"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} (cell-to-cell constant velocity)");

        sequencer.Board.PlayTileFallSfx(fallRecords.Count, maxDist);

        // Phase delay'ler action merge edilmeden once dondurulur.
        // Boylece ayni tile'in sonraki diagonal/dikey segmentleri, onceki
        // segmentin rank/stagger hesabini sonradan degistirmez.
        EnsurePhaseDelays(sequencer.Board);

        var moves = new List<IEnumerator>(fallRecords.Count);
        var delays = new List<float>(fallRecords.Count);

        float maxTotalDelay = 0f;

        foreach (var r in fallRecords)
        {
            if (r.tile == null || !r.tile)
                continue;

            moves.Add(r.tile.MoveToGridCell(
                sequencer.Board.TileSize,
                r.fromX,
                r.fromY,
                r.toX,
                r.toY,
                r.duration,
                r.curve,
                r.useSettle,
                r.settleDuration,
                r.settleStrength,
                sequencer.Board.FallSettleStretchX,
                sequencer.Board.FallSettleOvershoot));

            float totalDelay = r.startDelay + r.phaseDelay;

            delays.Add(totalDelay);

            if (totalDelay > maxTotalDelay)
                maxTotalDelay = totalDelay;
        }

        Debug.Log($"[Fall] stagger maxDelay={maxTotalDelay:0.000}s estimatedEnd={GetEstimatedVisualDuration(sequencer.Board):0.000}s");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - faStart):0.000}s");
    }
}
