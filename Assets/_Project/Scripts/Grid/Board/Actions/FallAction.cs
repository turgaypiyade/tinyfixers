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
        public float duration;       // YOK SAYILIR (ivmeli fizik türetir)
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

    /// <summary>
    /// Baska bir FallAction'in tum move'larini bu action'a ekler.
    /// delayOffset ile pass'lar arasi overlap saglanir.
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

    // ============================================================
    // İVMELİ FİZİK + SPAWN RİTMİ
    //
    // ÖLÇÜMLER (referans videonun 4. sütunundan, frame 6):
    //   Üstteki taşlar arası aralık: ~1.2 hücre
    //   Ortadaki: ~1.6 hücre
    //   Alttaki: ~2.0 hücre (terminal)
    //
    // MANTIK:
    //   Taşlar aynı ivme ile düşer (TileView.MoveToGrid içinde).
    //   Her taş, öncekinden SPAWN_INTERVAL kadar sonra başlar.
    //   SPAWN_INTERVAL ~0.10s → 2 hücre terminal aralığa denk.
    //   Üstteki taşlar henüz ivmelenmekte olduğu için araları dar (1.2h).
    //   Alttaki taşlar terminal'e ulaştığı için araları geniş (2.0h).
    //   Bu desen KENDİLİĞİNDEN oluşur — müdahale gereksiz.
    // ============================================================

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0) yield break;

        float _faStart = Time.realtimeSinceStartup;

        float columnStep = sequencer.Board.FallColumnStep;

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

        Debug.Log($"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} (physics-driven timing)");

        sequencer.Board.PlayTileFallSfx(fallRecords.Count, maxDist);

        // === SPAWN RİTMİ PARAMETRELERİ ===
        // Ardışık taşlar arası başlama farkı
        // 0.10s × 20 cell/s (V_MAX) = 2.0 hücre aralık (terminal kısımda)
        const float SPAWN_INTERVAL = 0.10f;

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

            // duration ve curve YOK SAYILIR — TileView.MoveToGrid ivmeli fizik kullanır
            moves.Add(r.tile.MoveToGrid(
                sequencer.Board.TileSize,
                r.duration,              // ignored
                r.curve,                 // ignored
                r.useSettle,
                r.settleDuration,
                r.settleStrength,
                sequencer.Board.FallSettleStretchX,
                sequencer.Board.FallSettleOvershoot));

            int col = r.tile.X;
            int maxToY = maxToYPerColumn[col];

            // SPAWN RİTMİ: en dipteki (rank=0) hemen, her üst taş SPAWN_INTERVAL kadar sonra
            int rankFromBottom = maxToY - r.toY;
            float spawnDelay = rankFromBottom * SPAWN_INTERVAL;

            // Sütunlar arası stagger (mevcut sistem korundu, çok küçük)
            float colDelay = columnStep > 0f ? col * columnStep : 0f;

            float totalDelay = r.startDelay + colDelay + spawnDelay;

            delays.Add(totalDelay);
            if (totalDelay > maxTotalDelay) maxTotalDelay = totalDelay;
        }

        Debug.Log($"[Fall] spawn rhythm maxDelay={maxTotalDelay:0.000}s interval={SPAWN_INTERVAL}s");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - _faStart):0.000}s");
    }
}