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

    // ============================================================
    // FallAction.cs — ExecuteVisuals metodunu BU VERSIYONLA değiştir
    //
    // SORUN: CascadeLogic taşları üstten aşağı spawn ediyor:
    //   - İlk yaratılan taş: fromY=-2, toY=en_üst_boş (kısa mesafe)
    //   - Son yaratılan taş: fromY=-9, toY=en_dip (uzun mesafe)
    //   Hepsi aynı hızla düşünce, ilk yaratılan (kısa mesafe) önce duruyor,
    //   arkadan gelen (uzun mesafe) onun üstüne biniyor.
    //
    // ÇÖZÜM: toY'si büyük olan (dibe gidecek) önce başlasın,
    //        toY'si küçük olan (üste yerleşecek) sonra başlasın.
    //        Aradaki delay = 1 hücre düşme süresi ≈ 0.05s
    //        Böylece doğal tren oluşur: alttaki önce, üstteki son.
    // ============================================================

    // ============================================================
    // FallAction.cs — ExecuteVisuals metodunu BU VERSIYONLA değiştir
    //
    // SORUN ZİNCİRİ:
    //  1. Taşlar farklı Y'lerden spawn oluyordu (-2, -3, -4, -5...)
    //     → üst üste binme, ekran dışında "yığılma"
    //  2. Şimdi (CascadeLogic düzeltmesiyle) aynı Y'den spawn olacaklar
    //     → hepsi aynı mesafe gitmeyecek, dibe giden 14, üste giden 4 hücre
    //
    // ÇÖZÜM: toY'si büyük olan (dibe gidecek) önce başlasın.
    //        rowDelay = (maxToY - r.toY) * PER_CELL_DELAY
    //        PER_CELL_DELAY ≈ 1 hücre düşme süresi = 1 / v_max = 1 / 19.9 ≈ 0.050s
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
        Debug.Log($"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} dur=[{minDur:0.000}-{maxDur:0.000}]s");

        sequencer.Board.PlayTileFallSfx(fallRecords.Count, maxDist);

        // Bir hücre düşme süresi — v_max = 19.9 hücre/s'ye karşılık gelir.
        // MoveToGrid v3'teki sabit hızla birebir uyumlu.
        const float PER_CELL_DELAY = 0.050f;

        // Her sütunda en büyük toY'yi bul (dip referans)
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
            if (r.tile != null)
            {
                moves.Add(r.tile.MoveToGrid(
                    sequencer.Board.TileSize, r.duration, r.curve,
                    r.useSettle, r.settleDuration, r.settleStrength,
                    sequencer.Board.FallSettleStretchX, sequencer.Board.FallSettleOvershoot));

                // Dibe giden taş önce başlar (delay=0),
                // üste gidecek taş her 1 hücre üst için +PER_CELL_DELAY bekler.
                int maxToY = maxToYPerColumn[r.tile.X];
                float rowDelay = (maxToY - r.toY) * PER_CELL_DELAY;

                float colDelay = columnStep > 0f ? r.tile.X * columnStep : 0f;
                float totalDelay = r.startDelay + colDelay + rowDelay;

                delays.Add(totalDelay);
                if (totalDelay > maxTotalDelay) maxTotalDelay = totalDelay;
            }
        }

        Debug.Log($"[Fall] stagger maxDelay={maxTotalDelay:0.000}s perCellDelay={PER_CELL_DELAY}");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - _faStart):0.000}s");
    }
}