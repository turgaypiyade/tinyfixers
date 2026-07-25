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
    private bool settleDisabled = false;

    public bool HasMoves => fallRecords.Count > 0;

    // Settle (iniş bounce'u) yalnızca SON inişte oynamalı. Ara cascade'lerde su gibi
    // akış için bu çağrılır → tüm record'ların settle'ı kapatılır.
    public void DisableSettle()
    {
        settleDisabled = true;
        foreach (var r in fallRecords)
            r.useSettle = false;
    }

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

        if (board != null && board.UseReferenceFallMotion)
            return GetReferenceEstimatedVisualDuration(board);

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

        if (board != null && board.UseReferenceFallMotion)
        {
            EnsureReferencePhaseDelays(board);
            return;
        }

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

            float spawnStaggerMul = board != null ? board.FallSpawnStaggerMultiplier : 1f;
            float spawnDelay = CumulativeSpawnDelay(rankFromBottom) * spawnStaggerMul;
            float colDelay = columnStep > 0f ? r.toX * columnStep : 0f;

            r.phaseDelay = colDelay + spawnDelay;
            r.hasPhaseDelay = true;
        }
    }

    private void EnsureReferencePhaseDelays(BoardController board)
    {
        var maxTargetRowPerSpawnColumn = new Dictionary<int, int>();
        var maxTargetRowPerExistingColumn = new Dictionary<int, int>();
        var legacyMaxToYPerColumn = new Dictionary<int, int>();

        foreach (var r in fallRecords)
        {
            if (r.hasPhaseDelay || r.tile == null || !r.tile)
                continue;

            if (IsStrictVerticalRecord(r))
            {
                if (IsSpawnRecord(r))
                {
                    int spawnColumn = GetSpawnColumn(r);
                    if (!maxTargetRowPerSpawnColumn.ContainsKey(spawnColumn) || r.toY > maxTargetRowPerSpawnColumn[spawnColumn])
                        maxTargetRowPerSpawnColumn[spawnColumn] = r.toY;
                }
                else
                {
                    // Mevcut (board üzerindeki) dikey düşüşler: yeni taşlardan AYRI stagger.
                    int col = r.toX;
                    if (!maxTargetRowPerExistingColumn.ContainsKey(col) || r.toY > maxTargetRowPerExistingColumn[col])
                        maxTargetRowPerExistingColumn[col] = r.toY;
                }
            }
            else
            {
                int col = r.toX;
                if (!legacyMaxToYPerColumn.ContainsKey(col) || r.toY > legacyMaxToYPerColumn[col])
                    legacyMaxToYPerColumn[col] = r.toY;
            }
        }

        // Intermediate cascade (DisableSettle çağrıldı): stagger & waterfall olmadan hızlı bitir.
        // Sadece son cascade'de (settle aktif) referans oyundaki waterfall efekti oynasın.
        // Yeni taş (spawn) ve mevcut taş gecikmeleri BİRBİRİNDEN AYRI (spec §2).
        float spawnInterval = settleDisabled ? 0f : board.ReferenceFallMotion.ReferenceFramesToSeconds(board.ReferenceFallMotion.spawnIntervalFrames);
        float existingInterval = settleDisabled ? 0f : board.ReferenceFallMotion.ReferenceFramesToSeconds(board.ReferenceFallMotion.existingIntervalFrames);
        float columnStep = settleDisabled ? 0f : board.FallColumnStep;
        float spawnStaggerMul = settleDisabled ? 0f : board.FallSpawnStaggerMultiplier;

        foreach (var r in fallRecords)
        {
            if (r.hasPhaseDelay)
                continue;

            if (r.tile == null || !r.tile)
                continue;

            if (settleDisabled)
            {
                r.phaseDelay = 0f;
                r.hasPhaseDelay = true;
                continue;
            }

            int rankFromBottom = 0;

            if (IsStrictVerticalRecord(r))
            {
                if (IsSpawnRecord(r))
                {
                    int spawnColumn = GetSpawnColumn(r);
                    if (maxTargetRowPerSpawnColumn.TryGetValue(spawnColumn, out int maxToY))
                        rankFromBottom = Mathf.Max(0, maxToY - r.toY);

                    r.phaseDelay = rankFromBottom * spawnInterval;
                }
                else
                {
                    if (maxTargetRowPerExistingColumn.TryGetValue(r.toX, out int maxToY))
                        rankFromBottom = Mathf.Max(0, maxToY - r.toY);

                    r.phaseDelay = rankFromBottom * existingInterval;
                }
            }
            else
            {
                if (legacyMaxToYPerColumn.TryGetValue(r.toX, out int maxToY))
                    rankFromBottom = Mathf.Max(0, maxToY - r.toY);

                float spawnDelay = CumulativeSpawnDelay(rankFromBottom) * spawnStaggerMul;
                float colDelay = columnStep > 0f ? r.toX * columnStep : 0f;
                r.phaseDelay = colDelay + spawnDelay;
            }

            r.hasPhaseDelay = true;
        }
    }

    private static bool IsSpawnRecord(FallRecord r)
    {
        return r != null && r.fromY < 0;
    }

    private static int GetSpawnColumn(FallRecord r)
    {
        if (r != null && r.pathWaypoints != null && r.pathWaypoints.Length > 0)
            return r.pathWaypoints[0].x;

        return r != null ? r.fromX : 0;
    }

    private static bool IsStrictVerticalRecord(FallRecord r)
    {
        if (r == null)
            return false;

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

        return r.fromX == r.toX;
    }

    private static float EstimateReferenceMoveDurationSeconds(ReferenceFallMotionSettings settings, float distanceCells)
    {
        if (settings == null)
            settings = new ReferenceFallMotionSettings();

        float remaining = Mathf.Max(0f, distanceCells);
        if (remaining <= 0.0001f)
            return 0f;

        float frames = 0f;
        float velocity = Mathf.Max(0f, settings.initialSpeedCellsPerFrame);
        float acceleration = Mathf.Max(0f, settings.accelerationCellsPerFrameSquared);
        float maxVelocity = Mathf.Max(0.001f, settings.maxSpeedCellsPerFrame);

        const int maxFrames = 1000;
        for (int i = 0; i < maxFrames && remaining > 0f; i++)
        {
            velocity = Mathf.Min(velocity + acceleration, maxVelocity);
            remaining -= Mathf.Max(0.001f, velocity);
            frames += 1f;
        }

        return settings.ReferenceFramesToSeconds(frames);
    }

    private float GetReferenceEstimatedVisualDuration(BoardController board)
    {
        var settings = board.ReferenceFallMotion;
        float landingTime = settleDisabled ? 0f :
            settings.ReferenceFramesToSeconds(settings.landingOvershootFrames) +
            settings.ReferenceFramesToSeconds(settings.impactHoldFrames) +
            settings.ReferenceFramesToSeconds(settings.landingReturnFrames);

        float maxEnd = 0f;

        foreach (var r in fallRecords)
        {
            if (r.tile == null || !r.tile)
                continue;

            float distanceCells = 0f;

            if (r.pathWaypoints != null && r.pathWaypoints.Length >= 2)
            {
                for (int i = 0; i < r.pathWaypoints.Length - 1; i++)
                    distanceCells += Vector2.Distance(r.pathWaypoints[i], r.pathWaypoints[i + 1]);
            }
            else
            {
                distanceCells = Vector2.Distance(
                    new Vector2(r.fromX, r.fromY),
                    new Vector2(r.toX, r.toY));
            }

            float endTime = r.startDelay + r.phaseDelay +
                            EstimateReferenceMoveDurationSeconds(settings, distanceCells) +
                            landingTime;

            if (endTime > maxEnd)
                maxEnd = endTime;
        }

        return maxEnd;
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
        BoardController board = sequencer.Board;
        bool useReferenceMotion = board != null && board.UseReferenceFallMotion;
        ReferenceFallMotionSettings referenceSettings = useReferenceMotion ? board.ReferenceFallMotion : null;

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

        bool trace = board != null && board.BoardFlowTraceEnabled;

        if (trace)
            Debug.Log(useReferenceMotion
                ? $"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} (reference-frame accelerated motion)"
                : $"[Fall] START tiles={fallRecords.Count} maxDist={maxDist} (cell-to-cell constant velocity)");

        board.PlayTileFallSfx(fallRecords.Count, maxDist);

        // Phase delay'ler action merge edilmeden once dondurulur.
        // Boylece ayni tile'in sonraki diagonal/dikey segmentleri, onceki
        // segmentin rank/stagger hesabini sonradan degistirmez.
        EnsurePhaseDelays(sequencer.Board);

        // Spawn taşlarının başlangıç dizilimi legacy ile AYNI kaynaktan gelir:
        // GetVerticalSpawnVisualOffsetCells(maxToYPerVerticalSpawnSource) → visualFromY.
        // Bu, mevcut (board üstündeki) taşların oluşturduğu sürekli kolonla HİZALI bir
        // stream kurar. Reference'ın eski toY-tabanlı "1+rank" hesabı bu kolonla
        // hizalanmıyordu → özellikle combo/pulse+pulse'ın dağınık çok-hücreli clear'larında
        // spawn taşları yığılıp "karışıyordu". Artık tek doğruluk kaynağı: visualFromY.
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

            if (trace)
                Debug.Log($"[FallExec] tile=({r.fromX},{r.fromY})->({r.toX},{r.toY}) path={isPath} delay={r.startDelay + r.phaseDelay:0.000}{visualNote}");

            IEnumerator move;

            bool useReferenceForRecord = useReferenceMotion;

            if (useReferenceForRecord)
            {
                float referenceStartDelay = r.startDelay + r.phaseDelay;
                float spawnReferenceFrame = referenceStartDelay * Mathf.Max(1f, referenceSettings.referenceFps);
                bool debugLog = board.DebugReferenceFallMotionLogs;
                int column = IsSpawnRecord(r) ? GetSpawnColumn(r) : r.toX;

                // Diagonal kayma (L-path) için landing yok: taş "düşmüyor", yatay-dikey kayıyor.
                // Landing (overshoot+return) yalnızca strictly-vertical tile'larda anlamlıdır.
                bool enableLanding = r.useSettle && IsStrictVerticalRecord(r);

                if (isPath)
                {
                    // Legacy ile aynı: dikey spawn path'inin İLK waypoint'i visualOffset kadar
                    // yukarı alınır → sürekli kolon. explicitStart YOK (TileView fromY/waypoint'ten
                    // doğru pozisyonu — special/movable offset dahil — kendi hesaplar).
                    Vector2Int[] refWaypoints = BuildVisualWaypoints(r, maxToYPerVerticalSpawnSource);

                    move = r.tile.MoveToGridPathReference(
                        board.TileSize,
                        refWaypoints,
                        null,
                        enableLanding,
                        referenceSettings,
                        debugLog,
                        column,
                        r.fromY,
                        r.toY,
                        spawnReferenceFrame,
                        referenceStartDelay,
                        faStart);
                }
                else
                {
                    // Legacy ile aynı: başlangıç satırı visualFromY (= r.fromY - visualOffset).
                    move = r.tile.MoveToGridCellReference(
                        board.TileSize,
                        r.fromX,
                        visualFromY,
                        r.toX,
                        r.toY,
                        null,
                        enableLanding,
                        referenceSettings,
                        debugLog,
                        column,
                        r.fromY,
                        r.toY,
                        spawnReferenceFrame,
                        referenceStartDelay,
                        faStart);
                }
            }
            else if (isPath)
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
                    board.TileSize,
                    visualWaypoints,
                    visualSegmentDurations,
                    r.curve,
                    r.useSettle,
                    r.settleDuration,
                    r.settleStrength,
                    board.FallSettleStretchX,
                    board.FallSettleOvershoot);
            }
            else
            {
                // Klasik tek segment hareket.
                // Ayni negatif spawn kaynagindan gelen dikey taşlar sadece
                // gorsel baslangicta ayrilir; CascadeLogic path'i degismez.
                // Aktif düşüş profili açıksa progress eğrisi Royal-referans ivme
                // formundan pişirilir (mesafeye özel); kapalıysa eski r.curve.
                float straightDistance = Vector2.Distance(
                    new Vector2(r.fromX, visualFromY),
                    new Vector2(r.toX, r.toY));
                AnimationCurve moveCurve = board.GetFallProgressCurve(straightDistance);
                if (moveCurve == null) moveCurve = r.curve;

                move = r.tile.MoveToGridCell(
                    board.TileSize,
                    r.fromX,
                    visualFromY,
                    r.toX,
                    r.toY,
                    moveDuration,
                    moveCurve,
                    r.useSettle,
                    r.settleDuration,
                    r.settleStrength,
                    board.FallSettleStretchX,
                    board.FallSettleOvershoot);
            }

            moves.Add(move);

            float totalDelay = useReferenceForRecord ? 0f : r.startDelay + r.phaseDelay;

            delays.Add(totalDelay);

            if (totalDelay > maxTotalDelay)
                maxTotalDelay = totalDelay;
        }

        if (trace)
            Debug.Log($"[Fall] stagger maxDelay={maxTotalDelay:0.000}s estimatedEnd={GetEstimatedVisualDuration(sequencer.Board):0.000}s");

        yield return sequencer.Animator.RunManyWithDelays(moves, delays);

        if (trace)
            Debug.Log($"[Fall] DONE +{(Time.realtimeSinceStartup - faStart):0.000}s");
    }
}
