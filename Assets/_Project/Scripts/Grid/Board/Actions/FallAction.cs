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

        // Multi-segment path desteği. null ise klasik tek segment hareket
        // (fromX,fromY -> toX,toY) kullanılır.
        public Vector2Int[] pathWaypoints;
        public float[] pathSegmentDurations;
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

    /// <summary>
    /// Multi-segment yörüngeli hareket ekler. Diagonal slide gibi durumlarda
    /// taşın "L şekli" yörünge çizmesi için kullanılır:
    ///   waypoints = [(fromX,fromY), (fromX,toY), (toX,toY)]
    ///
    /// Animasyon TileView.MoveToGridPath ile oynatılır.
    /// Phase delay/stagger hesabı için record'un toX/toY'si SON waypoint olur.
    /// duration = toplam süre (TÜM segment'lerin toplamı).
    /// </summary>
    public void AddPathMove(
        TileView tile,
        Vector2Int[] waypoints,
        float[] segmentDurations,
        bool useSettle,
        float settleDur,
        float settleStr,
        AnimationCurve curve,
        float startDelay = 0f)
    {
        if (tile == null || !tile)
            return;

        if (waypoints == null || waypoints.Length < 2)
            return;

        if (segmentDurations == null || segmentDurations.Length < waypoints.Length - 1)
            return;

        // Toplam süre ve from/to bilgisi (stagger ve mesafe hesabı için)
        float totalDuration = 0f;
        for (int i = 0; i < waypoints.Length - 1; i++)
            totalDuration += Mathf.Max(0.0001f, segmentDurations[i]);

        Vector2Int firstWp = waypoints[0];
        Vector2Int lastWp = waypoints[waypoints.Length - 1];

        fallRecords.Add(new FallRecord
        {
            tile = tile,
            fromX = firstWp.x,
            fromY = firstWp.y,
            toX = lastWp.x,
            toY = lastWp.y,
            duration = Mathf.Max(0.0001f, totalDuration),
            useSettle = useSettle,
            settleDuration = Mathf.Max(0f, settleDur),
            settleStrength = settleStr,
            curve = curve,
            startDelay = Mathf.Max(0f, startDelay),
            pathWaypoints = waypoints,
            pathSegmentDurations = segmentDurations,
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

        var maxToYPerVerticalSpawnSource = BuildMaxToYPerVerticalSpawnSource();

        float maxEnd = 0f;

        foreach (var r in fallRecords)
        {
            if (r.tile == null || !r.tile)
                continue;

            float settleTime = r.useSettle ? Mathf.Max(0f, r.settleDuration) : 0f;
            float moveDuration = GetEffectiveMoveDuration(board, r, maxToYPerVerticalSpawnSource);

            float endTime = r.startDelay + r.phaseDelay + moveDuration + settleTime;

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

    // ============================================================
    // VISUAL-ONLY SPAWN SEPARATION
    //
    // CascadeLogic'teki Path / nextSpawnY / diagonal slide mantigina
    // dokunmadan, sadece ayni negatif spawn kaynagindan gelen DIKEY
    // spawn hareketlerini gorselde ayirir.
    //
    // Guvenlik kurallari:
    // - Original r.pathWaypoints mutate edilmez.
    // - Sadece kopya array animasyona verilir.
    // - Diagonal path etkilenmez.
    // - Board state / tile coords etkilenmez.
    // ============================================================

    private static bool IsVerticalSpawnForVisualSpacing(FallRecord r)
    {
        if (r == null)
            return false;

        // Sadece yukaridan spawn edilenler.
        if (r.fromY >= 0)
            return false;

        // Path varsa, sadece tamamen dikey path kabul edilir.
        if (r.pathWaypoints != null && r.pathWaypoints.Length >= 2)
        {
            int x = r.pathWaypoints[0].x;

            for (int i = 1; i < r.pathWaypoints.Length; i++)
            {
                if (r.pathWaypoints[i].x != x)
                    return false;
            }

            return true;
        }

        // Path yoksa klasik tek segment dikey hareket.
        return r.fromX == r.toX;
    }

    private static long GetVerticalSpawnSourceKey(FallRecord r)
    {
        return ((long)r.fromX << 32) ^ (uint)r.fromY;
    }

    private Dictionary<long, int> BuildMaxToYPerVerticalSpawnSource()
    {
        var result = new Dictionary<long, int>();

        foreach (var r in fallRecords)
        {
            if (r.tile == null || !r.tile)
                continue;

            if (!IsVerticalSpawnForVisualSpacing(r))
                continue;

            long key = GetVerticalSpawnSourceKey(r);

            if (!result.ContainsKey(key) || r.toY > result[key])
                result[key] = r.toY;
        }

        return result;
    }

    private static int GetVerticalSpawnVisualOffsetCells(
        FallRecord r,
        Dictionary<long, int> maxToYPerVerticalSpawnSource)
    {
        if (!IsVerticalSpawnForVisualSpacing(r))
            return 0;

        if (maxToYPerVerticalSpawnSource == null)
            return 0;

        long key = GetVerticalSpawnSourceKey(r);

        if (!maxToYPerVerticalSpawnSource.TryGetValue(key, out int maxToY))
            return 0;

        // En alttaki hedefe giden taş offset 0 alır.
        // Üst hedeflere gidenler 1, 2, 3... hücre daha yukarıdan görünür.
        return Mathf.Max(0, maxToY - r.toY);
    }

    private Vector2Int[] BuildVisualWaypoints(
        FallRecord r,
        Dictionary<long, int> maxToYPerVerticalSpawnSource)
    {
        if (r.pathWaypoints == null || r.pathWaypoints.Length < 2)
            return r.pathWaypoints;

        int visualOffset = GetVerticalSpawnVisualOffsetCells(
            r,
            maxToYPerVerticalSpawnSource);

        if (visualOffset <= 0)
            return r.pathWaypoints;

        // Original path mutate edilmez.
        var visualWaypoints = new Vector2Int[r.pathWaypoints.Length];

        for (int i = 0; i < r.pathWaypoints.Length; i++)
            visualWaypoints[i] = r.pathWaypoints[i];

        // Sadece ilk waypoint gorsel olarak yukariya alinir.
        visualWaypoints[0] = new Vector2Int(
            visualWaypoints[0].x,
            visualWaypoints[0].y - visualOffset);

        return visualWaypoints;
    }

    private float[] BuildVisualSegmentDurations(
        BoardController board,
        FallRecord r,
        Vector2Int[] visualWaypoints)
    {
        if (r.pathSegmentDurations == null ||
            visualWaypoints == null ||
            visualWaypoints.Length < 2)
        {
            return r.pathSegmentDurations;
        }

        var visualDurations = new float[r.pathSegmentDurations.Length];

        for (int i = 0; i < r.pathSegmentDurations.Length; i++)
            visualDurations[i] = r.pathSegmentDurations[i];

        if (board != null)
        {
            // Sadece ilk segment uzadiysa, onun suresini yeniden hesapla.
            // Diger segmentler aynen kalir.
            visualDurations[0] = board.GetFallDurationForMove(
                visualWaypoints[0].x,
                visualWaypoints[0].y,
                visualWaypoints[1].x,
                visualWaypoints[1].y);
        }

        return visualDurations;
    }

    private float GetEffectiveMoveDuration(
        BoardController board,
        FallRecord r,
        Dictionary<long, int> maxToYPerVerticalSpawnSource)
    {
        int visualOffset = GetVerticalSpawnVisualOffsetCells(
            r,
            maxToYPerVerticalSpawnSource);

        if (visualOffset <= 0)
            return r.duration;

        bool isPath = r.pathWaypoints != null && r.pathWaypoints.Length >= 2;

        if (isPath)
        {
            Vector2Int[] visualWaypoints = BuildVisualWaypoints(
                r,
                maxToYPerVerticalSpawnSource);

            float[] visualDurations = BuildVisualSegmentDurations(
                board,
                r,
                visualWaypoints);

            if (visualDurations == null)
                return r.duration;

            float total = 0f;

            for (int i = 0; i < visualDurations.Length; i++)
                total += Mathf.Max(0.0001f, visualDurations[i]);

            return total;
        }

        if (board == null)
            return r.duration;

        int visualFromY = r.fromY - visualOffset;

        return board.GetFallDurationForMove(
            r.fromX,
            visualFromY,
            r.toX,
            r.toY);
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (fallRecords.Count == 0)
            yield break;

        float faStart = Time.realtimeSinceStartup;

        var maxToYPerVerticalSpawnSource = BuildMaxToYPerVerticalSpawnSource();

        int maxDist = 0;

        foreach (var r in fallRecords)
        {
            int visualOffset = GetVerticalSpawnVisualOffsetCells(
                r,
                maxToYPerVerticalSpawnSource);

            int visualFromY = r.fromY - visualOffset;

            int dist = Mathf.CeilToInt(Vector2.Distance(
                new Vector2(r.fromX, visualFromY),
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

            bool isPath = r.pathWaypoints != null && r.pathWaypoints.Length >= 2;

            int visualOffset = GetVerticalSpawnVisualOffsetCells(
                r,
                maxToYPerVerticalSpawnSource);

            int visualFromY = r.fromY - visualOffset;

            float moveDuration = GetEffectiveMoveDuration(
                sequencer.Board,
                r,
                maxToYPerVerticalSpawnSource);

            string visualNote = visualOffset > 0
                ? $" visualFromY={visualFromY} visualOffset={visualOffset} visualDuration={moveDuration:0.000}"
                : string.Empty;

            Debug.Log($"[FallExec] tile=({r.fromX},{r.fromY})->({r.toX},{r.toY}) path={isPath} delay={r.startDelay + r.phaseDelay:0.000}{visualNote}");

            IEnumerator move;

            if (isPath)
            {
                Vector2Int[] visualWaypoints = BuildVisualWaypoints(
                    r,
                    maxToYPerVerticalSpawnSource);

                float[] visualSegmentDurations = BuildVisualSegmentDurations(
                    sequencer.Board,
                    r,
                    visualWaypoints);

                // Multi-segment yörünge.
                // Eğer path tamamen dikey spawn ise sadece ilk waypoint kopyası görsel olarak yukarı alınır.
                // Diagonal path ise original path aynen kullanılır.
                move = r.tile.MoveToGridPath(
                    sequencer.Board.TileSize,
                    visualWaypoints,
                    visualSegmentDurations,
                    r.curve,
                    r.useSettle,
                    r.settleDuration,
                    r.settleStrength,
                    sequencer.Board.FallSettleStretchX,
                    sequencer.Board.FallSettleOvershoot);
            }
            else
            {
                // Klasik tek segment hareket.
                // Ayni negatif spawn kaynagindan gelen dikey taşlar sadece
                // gorsel baslangicta ayrilir; CascadeLogic path'i degismez.
                move = r.tile.MoveToGridCell(
                    sequencer.Board.TileSize,
                    r.fromX,
                    visualFromY,
                    r.toX,
                    r.toY,
                    moveDuration,
                    r.curve,
                    r.useSettle,
                    r.settleDuration,
                    r.settleStrength,
                    sequencer.Board.FallSettleStretchX,
                    sequencer.Board.FallSettleOvershoot);
            }

            moves.Add(move);

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