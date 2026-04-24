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
        public float duration;       // YOK SAYILIR (sabit hız kullanılıyor)
        public bool useSettle;
        public float settleDuration;
        public float settleStrength;
        public AnimationCurve curve; // YOK SAYILIR
        public float startDelay;
    }

    private List<FallRecord> fallRecords = new List<FallRecord>();
    public bool HasMoves => fallRecords.Count > 0;

    public void AddMove(TileView tile, int fromY, int toY, float duration, bool useSettle, float settleDur, float settleStr, AnimationCurve curve, float startDelay = 0f)
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
            startDelay = startDelay
        });
    }

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

    public float GetMaxMoveDuration()
    {
        float max = 0f;
        foreach (var r in fallRecords)
            if (r.duration > max) max = r.duration;
        return max;
    }

    // ============================================================
    // SABİT HIZ + ADAPTIVE START DELAY (kullanıcı vizyonu — hızlandırıldı)
    //
    // Tüm taşlar V_MAX sabit hızda iner (TileView.FALL_VELOCITY = 42 cell/s).
    // Aralarındaki görsel mesafe sadece "başlama zamanı" farkından gelir.
    //
    // AKORDIYON — rank bazlı cumulative delay (hızlandırıldı):
    //   rank 0 (en dip): delay = 0         (hemen başlar)
    //   rank 0 → 1:      0.018s            (üstte sıkışık)
    //   rank 1 → 2:      0.023s
    //   rank 2 → 3:      0.030s
    //   rank 3+:         0.035s            (dipte rahat)
    //
    // Eski: [0.025, 0.032, 0.040, 0.045] → Yeni: [0.018, 0.023, 0.030, 0.035]
    // %28 daha sıkı, akordiyon hissi korundu (oranlar aynı).
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
        if (rank <= 0) return 0f;
        float total = 0f;
        for (int i = 0; i < rank; i++)
        {
            total += (i < SPAWN_INTERVALS.Length)
                ? SPAWN_INTERVALS[i]
                : SPAWN_INTERVAL_TERMINAL;
        }
        return total;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0) yield break;

        float _faStart = Time.realtimeSinceStartup;

        float columnStep = sequencer.Board.FallColumnStep;

        int maxDist = 0;
        foreach (var r in fallRecords)
        {
            int dist = Mathf.Abs(r.toY - r.fromY);
            if (dist > maxDist) maxDist = dist;
        }

        Debug.Log($"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} (constant velocity, adaptive delay)");

        sequencer.Board.PlayTileFallSfx(fallRecords.Count, maxDist);

        // Her sütunda en dip hedefi bul (rank hesabı için)
        var maxToYPerColumn = new Dictionary<int, int>();
        foreach (var r in fallRecords)
        {
            if (r.tile == null) continue;
            int col = r.tile.X;
            if (!maxToYPerColumn.ContainsKey(col) || r.toY > maxToYPerColumn[col])
                maxToYPerColumn[col] = r.toY;
        }

        var moves = new List<IEnumerator>(fallRecords.Count);
        var delays = new List<float>(fallRecords.Count);

        float maxTotalDelay = 0f;

        foreach (var r in fallRecords)
        {
            if (r.tile == null) continue;

            moves.Add(r.tile.MoveToGrid(
                sequencer.Board.TileSize,
                r.duration,
                r.curve,
                r.useSettle,
                r.settleDuration,
                r.settleStrength,
                sequencer.Board.FallSettleStretchX,
                sequencer.Board.FallSettleOvershoot));

            int col = r.tile.X;
            int maxToY = maxToYPerColumn[col];

            // Tek stagger: rank'e göre cumulative spawn delay
            int rankFromBottom = maxToY - r.toY;
            float spawnDelay = CumulativeSpawnDelay(rankFromBottom);

            float colDelay = columnStep > 0f ? col * columnStep : 0f;
            float totalDelay = r.startDelay + colDelay + spawnDelay;

            delays.Add(totalDelay);
            if (totalDelay > maxTotalDelay) maxTotalDelay = totalDelay;
        }

        Debug.Log($"[Fall] stagger maxDelay={maxTotalDelay:0.000}s");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - _faStart):0.000}s");
    }
}