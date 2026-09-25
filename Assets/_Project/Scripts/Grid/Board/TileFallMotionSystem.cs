using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kesintisiz düşüş: board'daki TÜM düşen taşları tek döngü sürer (BoardController.Update → Tick).
///
/// Kurallar:
///   1. Her taş kendi hızını taşır (aktif düşüş profili: v0 + ivme, tavan hız). Sıfırdan başlayan
///      düşüş eski süre-tabanlı eğriyle birebir aynı kinematiktir.
///   2. Havadaki taş yeniden hedeflenirse (altında yeni boşluk açıldı) OLDUĞU YERDEN, MEVCUT HIZIYLA
///      devam eder: kalan yolu yeni yolun başına eklenir. Işınlanma / hız sıfırlama yok.
///   3. Aynı sütunda dikey inen taş, altındaki hareketli taşla asgari aralığı korur;
///      yaklaşınca onun hızına iner.
///   4. Tek sahip: taş TileView move-token'ını alır. Başka bir hareket token'ı alırsa ya da taşın
///      konumunu dışarıdan değiştirirse döngü o taşı bırakır (kavga yok).
///   5. Taş hedefine vardığında mantıksal hücresi de oraysa oturur (snap + FallArrived + settle + Idle).
///      Mantıksal olarak zaten başka hücreye planlandıysa oturmaz, momentumunu saklayıp yeni hareketi
///      bekler; yeni hareket gelmezse sahipliği doğrulanarak hedefe düşüş yeniden başlatılır.
///   6. Duran taş da yol kaplar: planı yapılmış ama hareketi henüz başlamamış (sıradaki aksiyonu
///      bekleyen) ya da park etmiş taş, sütun aralığında ve çapraz girişte engel sayılır. Bir taş
///      ona takılırsa bekletilmez, duranın planlı rotası HEMEN başlatılır. Aynı sütunda daha alta
///      planlı taş varsa önce o kalkar. İki taş hiçbir zaman birbirinin içinden geçmez.
///
/// FallAction bu sisteme kayıt verip biletlerinin bitmesini bekler; sequencer/job semantiği değişmez.
/// </summary>
internal sealed class TileFallMotionSystem
{
    internal sealed class Ticket
    {
        public bool Done { get; internal set; }
    }

    private sealed class Motion
    {
        public TileView tile;
        public int lifetime;
        public int token;
        public int generation;
        public long sequence;
        public DiagonalTransit diagonal;
        public Vector2 pos;
        public Vector2 lastWritten;
        public readonly List<Vector2> points = new();
        public float velocity;          // hücre/sn
        public float delay;             // başlangıç stagger'ı (yalnız ilk kalkışta)
        public float elapsed;
        public float stretchEnv;
        public bool stationary;         // yalnız bu karelik engel: planlı/park etmiş, kıpırdamayan taş
        public float stuckTime;         // ilerlemeden geçen süre (gecikme hariç)
        public bool stuckWarningRaised;
        public int waitKind;            // son bekleme: 0 yok, 1 çapraz dönüş, 2 sütun aralığı (teşhis)
        public Motion waitBlocker;      // sütun aralığında beklenen taş
        public Vector2Int target;
        public bool settle;
        public float settleDuration;
        public float settleStrength;
        public bool arrivalRaised;
        public Ticket ticket;
    }

    // A turn owns only the cells it crosses, not its eventual destination column.
    // Retargeting can extend a route without losing ownership of the current turn.
    private sealed class DiagonalTransit
    {
        public Motion owner;
        public DiagonalRegion region;
        public bool completed;
    }

    private struct DiagonalRegion
    {
        public int left, right, top, bottom;
    }

    private sealed class Parked
    {
        public int lifetime;
        public int token;
        public Vector2 pos;
        public float velocity;
        public float time;
    }

    private sealed class PlannedRoute
    {
        public int lifetime;
        public int generation;
        public bool isSpawn;
        public bool settle;
        public float settleDuration;
        public float settleStrength;
        public readonly List<Vector2> points = new();
    }

    private struct KickedPlan
    {
        public int lifetime;
        public int generation;
    }

    // Erken başlatılan planlar: sıradaki aksiyon aynı planla Start çağırınca no-op olur
    // (taş oturmuşsa yolu baştan tekrar yürümesin).
    private readonly Dictionary<TileView, KickedPlan> kicked = new();

    // Preserve routes at planning time: a newer action can execute before an older one.
    // Skipping the older ticket must not discard its spawn origin or obstacle waypoints.
    private readonly Dictionary<TileView, PlannedRoute> planned = new();

    public void Prepare(TileView tile, int lifetime, int generation, Vector2[] path, bool isSpawn,
        bool settle = false, float settleDuration = 0f, float settleStrength = 0f)
    {
        if (tile == null || !tile.IsCurrentLifetime(lifetime) || path == null || path.Length == 0)
            return;
        if (!planned.TryGetValue(tile, out var route) || route.lifetime != lifetime)
        {
            route = new PlannedRoute { lifetime = lifetime, generation = -1, isSpawn = isSpawn };
            planned[tile] = route;
        }
        if (generation <= route.generation) return;
        route.generation = generation;
        route.settle = settle;
        route.settleDuration = settleDuration;
        route.settleStrength = settleStrength;
        foreach (var point in path)
            if (route.points.Count == 0 || (route.points[route.points.Count - 1] - point).sqrMagnitude >= 0.25f)
                route.points.Add(point);
        if (tile.IsRuntimeIdle)
            tile.SetRuntimeState(TileRuntimeState.Falling);
    }

    // Varıp da yeni hareketini bekleyen taşın momentumu bu süre içinde yeni harekete aktarılır.
    private const float ParkMomentumWindow = 0.15f;
    private const float ParkRecoveryTimeout = 0.5f;
    // Aynı sütunda iki hareketli taş arasındaki asgari mesafe (hücre). <1: ikon boşluğu içinde kalır,
    // special'ın dikey ofseti takılma yaratmaz.
    private const float ColumnGapCells = 0.92f;
    // Diagnose a stalled route without letting a follower pass through its leader.
    private const float StuckWarningSeconds = 2f;
    private const float StretchRampIn = 0.03f;
    private const float StretchRecover = 0.06f;

    private readonly BoardController board;
    private readonly Dictionary<TileView, Motion> active = new();
    private readonly Dictionary<TileView, Parked> parked = new();
    private readonly List<Motion> order = new();
    private readonly List<TileView> parkedScratch = new();
    private readonly Dictionary<int, Motion> belowInColumn = new();
    private readonly List<DiagonalTransit> diagonalTransits = new();
    private long nextSequence;

    public TileFallMotionSystem(BoardController board) => this.board = board;

    public bool HasWork => active.Count > 0 || parked.Count > 0 || planned.Count > 0;

    // Oturan (Land) taş sayacı: çapraz dolum ancak bir taş oturunca yeniden denenir (BoardFlowPump).
    public int LandedCount { get; private set; }

    // Includes queued routes and entries awaiting ownership cleanup in the next Tick.
    // IsMoving alone is insufficient: it deliberately ignores a revoked move token.
    public bool HasPendingMotion(TileView tile) => tile != null &&
        ((active.TryGetValue(tile, out var m) && tile.IsCurrentLifetime(m.lifetime))
         || (parked.TryGetValue(tile, out var p) && tile.IsCurrentLifetime(p.lifetime))
         || (planned.TryGetValue(tile, out var route) && tile.IsCurrentLifetime(route.lifetime)));

    public bool IsMoving(TileView tile) => tile != null &&
        ((active.TryGetValue(tile, out var m) && IsOwned(m))
         || (parked.TryGetValue(tile, out var p) && tile.IsCurrentLifetime(p.lifetime)
             && tile.IsMoveTokenCurrent(p.token)));

    public void Reset()
    {
        foreach (var m in active.Values)
        {
            m.ticket.Done = true;
            board.ClearReservedTileTargetCell(m.target);
            if (!IsOwned(m)) continue;
            m.tile.ClaimMoveToken();
            if (m.tile.RuntimeState == TileRuntimeState.Falling)
                m.tile.SetRuntimeState(TileRuntimeState.Idle);
        }
        foreach (var pair in parked)
            if (pair.Key != null && pair.Key.IsCurrentLifetime(pair.Value.lifetime)
                && pair.Key.IsMoveTokenCurrent(pair.Value.token))
            {
                pair.Key.ClaimMoveToken();
                if (pair.Key.RuntimeState == TileRuntimeState.Falling)
                    pair.Key.SetRuntimeState(TileRuntimeState.Idle);
            }
        foreach (var pair in planned)
            if (pair.Key != null && pair.Key.IsCurrentLifetime(pair.Value.lifetime)
                && pair.Key.RuntimeState == TileRuntimeState.Falling)
                pair.Key.SetRuntimeState(TileRuntimeState.Idle);
        active.Clear();
        parked.Clear();
        planned.Clear();
        kicked.Clear();
        diagonalTransits.Clear();
    }

    /// <summary>
    /// Taşı <paramref name="path"/> (anchored px) boyunca hedef hücreye sürer. Taş zaten hareketteyse
    /// yol kaldığı yerden uzatılır. Kayıt bayatsa (taş bu plandan sonra yeniden planlandı) no-op.
    /// </summary>
    public Ticket Start(
        TileView tile,
        int lifetime,
        int planGeneration,
        Vector2[] path,
        Vector2Int target,
        bool isSpawn,
        float delay,
        bool settle,
        float settleDuration,
        float settleStrength)
    {
        var ticket = new Ticket();
        if (tile == null || !tile || !tile.IsCurrentLifetime(lifetime) || path == null || path.Length == 0
            || planGeneration < tile.PlannedFallGeneration)
        {
            ticket.Done = true;
            return ticket;
        }

        if (kicked.TryGetValue(tile, out var early))
        {
            if (early.lifetime == lifetime && planGeneration <= early.generation)
            {
                ticket.Done = true;
                return ticket;
            }
            kicked.Remove(tile);
        }

        if (planned.TryGetValue(tile, out var route) && route.lifetime == lifetime
            && route.generation == planGeneration)
        {
            path = route.points.ToArray();
            isSpawn = route.isSpawn;
            planned.Remove(tile);
        }
        else if (route != null && (route.lifetime != lifetime || route.generation < planGeneration))
        {
            // Daha yeni bir plan doğrudan başladı; eski rota artık hiçbir aksiyona ait değil.
            planned.Remove(tile);
        }

        board.GetFallKinematics(out float v0, out _, out _);
        float gap = board.TileSize * ColumnGapCells;

        if (active.TryGetValue(tile, out var m) && IsOwned(m))
        {
            if (planGeneration <= m.generation)
            {
                ticket.Done = true;
                return ticket;
            }
            // Havada yeniden hedefleme: kalan yol + yeni yolun devamı, hız korunur.
            m.ticket.Done = true;
            board.ClearReservedTileTargetCell(m.target);

            Vector2 oldEnd = m.points.Count > 0 ? m.points[m.points.Count - 1] : m.pos;
            int from = 0;
            if ((oldEnd - path[0]).sqrMagnitude < 1f)
                from = 1;            // eski yol yeni yolun başlangıcında bitiyor → düz ekle
            else
            {
                m.points.Clear();
                ReleaseDiagonal(m);
                from = 1; // Never return to the old logical source behind the visible tile.
            }

            for (int i = from; i < path.Length; i++)
                AddPoint(m, path[i]);

            if (m.delay > 0f)
                m.delay = Mathf.Min(m.delay, Mathf.Max(0f, delay));
        }
        else
        {
            if (m != null)
                Drop(m);

            // Aynı sütunda daha alta planlı ama henüz kalkmamış taş önce yola çıkar;
            // yoksa bu taş ona ya da o bu taşın içinden geçer.
            KickPlannedBelow(tile, path[path.Length - 1]);

            bool ownsParked = parked.TryGetValue(tile, out var previous)
                && previous.lifetime == lifetime && tile.IsMoveTokenCurrent(previous.token);
            m = new Motion
            {
                tile = tile,
                lifetime = lifetime,
                token = tile.ClaimMoveToken(),
                sequence = nextSequence++,
                velocity = v0,
                delay = Mathf.Max(0f, delay),
            };

            // Board üzerindeki taş görsel konumundan başlar (sıralama dışı gelen eski planlar
            // taşı ışınlamasın); spawn taşı havuzdan yeni geldiği için yolun başından başlar.
            Vector2 start = isSpawn ? path[0] : tile.RectTransform.anchoredPosition;

            if (parked.TryGetValue(tile, out var p))
            {
                parked.Remove(tile);
                if (ownsParked && Time.time - p.time <= ParkMomentumWindow
                    && (p.pos - start).sqrMagnitude < 1f)
                {
                    m.velocity = Mathf.Max(v0, p.velocity);
                    m.delay = 0f;
                }
            }

            if (isSpawn)
            {
                // Yeni taş sütundaki akışın ARKASINA dizilir ve onun hızını devralır → kesintisiz akış.
                Motion top = FindTopMotionInColumn(start.x, tile);
                if (top != null)
                {
                    start.y = Mathf.Max(start.y, top.pos.y + gap);
                    if (top.delay > 0f)
                        m.delay = Mathf.Max(m.delay, top.delay);
                    else
                    {
                        m.velocity = Mathf.Max(v0, top.velocity);
                        m.delay = 0f;
                    }
                }
            }

            m.pos = start;
            // The current visual position is the origin. path[0] is a planning source,
            // not a destination (especially after a settle bounce or a retarget).
            for (int i = 1; i < path.Length; i++)
                AddPoint(m, path[i]);

            tile.RectTransform.anchoredPosition = start;
            m.lastWritten = start;
            active[tile] = m;
        }

        m.target = target;
        m.generation = planGeneration;
        m.settle = settle;
        m.settleDuration = settleDuration;
        m.settleStrength = settleStrength;
        m.arrivalRaised = false;
        m.ticket = ticket;

        board.ReserveTileTargetCell(target);
        if (tile.IsRuntimeIdle || tile.RuntimeState == TileRuntimeState.Falling)
            tile.SetRuntimeState(TileRuntimeState.Falling);

        if (m.points.Count == 0)
            Land(m);

        return ticket;
    }

    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        // Keep completed turns through the end of their frame, so even a large dt cannot
        // launch a leader and its follower together. Revoked lifetimes never retain a turn.
        for (int i = diagonalTransits.Count - 1; i >= 0; i--)
            if (diagonalTransits[i].completed || !IsOwned(diagonalTransits[i].owner))
                diagonalTransits.RemoveAt(i);
        // Discard routes belonging to destroyed or recycled tiles, including queued actions
        // that will never execute. The dictionary must not retain dead views across clears.
        parkedScratch.Clear();
        foreach (var pair in planned)
            if (pair.Key == null || !pair.Key.IsCurrentLifetime(pair.Value.lifetime))
                parkedScratch.Add(pair.Key);
        foreach (var tile in parkedScratch)
            planned.Remove(tile);
        parkedScratch.Clear();
        foreach (var pair in kicked)
            if (pair.Key == null || !pair.Key.IsCurrentLifetime(pair.Value.lifetime))
                parkedScratch.Add(pair.Key);
        foreach (var tile in parkedScratch)
            kicked.Remove(tile);

        if (parked.Count > 0)
            ProcessParked();

        if (active.Count == 0)
            return;

        board.GetFallKinematics(out float v0, out float accel, out float vmax);
        int tileSize = board.TileSize;
        float gap = tileSize * ColumnGapCells;
        float lead = board.FallArrivalLeadCells * tileSize;

        order.Clear();
        foreach (var m in active.Values)
            order.Add(m);

        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (!IsOwned(order[i]))
            {
                Drop(order[i]);
                order.RemoveAt(i);
            }
        }

        AddStationaryBlockers();

        // Alttan üste işle: her taş, altındaki komşusunun BU karedeki konumuna göre sınırlanır.
        order.Sort(CompareFlowOrder);
        belowInColumn.Clear();

        for (int i = 0; i < order.Count; i++)
        {
            var m = order[i];
            if (m.stationary)
            {
                belowInColumn[Mathf.RoundToInt(m.pos.x)] = m;
                continue;
            }
            // Arrival callbacks may pool/reuse/retarget a tile in this same Tick.
            if (!active.TryGetValue(m.tile, out var current) || current != m || !IsOwned(m))
                continue;
            int column = Mathf.RoundToInt(m.pos.x);
            float moveDt = dt;
            if (m.delay > 0f)
            {
                float consumed = Mathf.Min(m.delay, moveDt);
                m.delay -= consumed;
                moveDt -= consumed;
                if (moveDt <= 0f)
                {
                    belowInColumn[column] = m;
                    continue;
                }
            }

            m.elapsed += moveDt;
            // Integrate the accelerated and capped phases exactly, including a cap reached
            // mid-frame. Semi-implicit Euler made the same profile faster at lower FPS.
            float initialVelocity = Mathf.Clamp(m.velocity, v0, vmax);
            float acceleratingTime = accel > 0f ? Mathf.Min(moveDt, (vmax - initialVelocity) / accel) : 0f;
            float distance = initialVelocity * moveDt
                + accel * acceleratingTime * (moveDt - 0.5f * acceleratingTime);
            m.velocity = Mathf.Min(initialVelocity + accel * moveDt, vmax);
            Vector2 before = m.pos;
            m.waitKind = 0;
            m.waitBlocker = null;
            Advance(m, distance * tileSize, gap, initialVelocity);
            WatchStuck(m, before, moveDt);

            m.tile.RectTransform.anchoredPosition = m.pos;
            m.lastWritten = m.pos;

            bool vertical = IsNextSegmentVertical(m);
            // Include a stone waiting at a diagonal entrance, so its vertical followers
            // cannot fall through it while another stone owns the turn.
            belowInColumn[Mathf.RoundToInt(m.pos.x)] = m;
            if (m.diagonal != null)
            {
                // The vertical stream still follows this stone while it turns. Previously
                // turning stones disappeared from column spacing until the diagonal ended.
                belowInColumn[m.diagonal.region.left * tileSize] = m;
                belowInColumn[m.diagonal.region.right * tileSize] = m;
            }

            if (m.points.Count == 0)
            {
                Land(m);
                continue;
            }

            float remaining = RemainingLength(m);
            UpdateStretch(m, vertical, remaining, tileSize, dt);

            if (!m.arrivalRaised && lead > 0f && remaining <= lead && IsAtLogicalTarget(m))
            {
                m.arrivalRaised = true;
                m.tile.RaiseFallArrivedFromMotion();
            }
        }
    }

    private void WatchStuck(Motion m, Vector2 before, float moveDt)
    {
        if ((m.pos - before).sqrMagnitude > 0.01f || m.waitKind == 0)
        {
            m.stuckTime = 0f;
            return;
        }

        m.stuckTime += moveDt;
        if (m.stuckWarningRaised || m.stuckTime < StuckWarningSeconds) return;

        m.stuckWarningRaised = true;
        Vector2 next = m.points.Count > 0 ? m.points[0] : m.pos;
        var b = m.waitBlocker;
        string reason = m.waitKind == 1 ? "diagonal-turn"
            : b == null ? "column-gap"
            : $"column-gap below=({b.tile.X},{b.tile.Y}) stationary={b.stationary} belowDelay={b.delay:0.00} " +
              $"belowPoints={b.points.Count} belowActive={active.ContainsKey(b.tile)} belowParked={parked.ContainsKey(b.tile)} " +
              $"belowPlanned={planned.ContainsKey(b.tile)} belowState={b.tile.RuntimeState}";
        Debug.LogWarning($"[FallMotion] Taş ({m.tile.X},{m.tile.Y}) {StuckWarningSeconds:0}sn ilerleyemedi, güvenli aralık korunuyor. " +
                         $"sebep={reason} hedef={m.target} pos={m.pos} next={next} kalanNokta={m.points.Count} " +
                         $"active={active.Count} parked={parked.Count} planned={planned.Count} transits={diagonalTransits.Count}");
    }

    // Consume distance across path segments; vertical followers respect the current column.
    private void Advance(Motion m, float step, float gap, float initialVelocity)
    {
        while (step > 0f && m.points.Count > 0)
        {
            Vector2 next = m.points[0];
            if (Mathf.Abs(next.x - m.pos.x) >= 0.5f && !TryEnterDiagonal(m, next))
            {
                // Waiting at the mouth of a turn must not build up artificial acceleration.
                m.velocity = initialVelocity;
                m.waitKind = 1;
                return;
            }
            bool verticalDown = Mathf.Abs(next.x - m.pos.x) < 0.5f && next.y < m.pos.y;
            // A path can turn into another column in one frame. Re-query for EACH segment.
            belowInColumn.TryGetValue(Mathf.RoundToInt(m.pos.x), out var below);
            if (below == m || (below != null && (!below.tile.IsCurrentLifetime(below.lifetime)
                || (!below.stationary && !below.tile.IsMoveTokenCurrent(below.token))))) below = null;
            float minY = below != null ? below.pos.y + gap : float.NegativeInfinity;
            bool clamped = false;
            if (verticalDown && next.y < minY)
            {
                m.waitKind = 2;
                m.waitBlocker = below;
                // Duran taşa takıldı: onu beklemek yerine planlı rotasını başlat (sonraki kare kalkar).
                if (below.stationary)
                    Kick(below.tile);
                if (m.pos.y <= minY)
                {
                    m.velocity = below.delay > 0f ? 0f : Mathf.Min(m.velocity, below.velocity);
                    return;
                }
                next.y = minY;
                clamped = true;
            }

            Vector2 delta = next - m.pos;
            float dist = delta.magnitude;
            if (dist <= step)
            {
                m.pos = next;
                step -= dist;
                if (clamped)
                {
                    m.velocity = below.delay > 0f ? 0f : Mathf.Min(m.velocity, below.velocity);
                    return;
                }
                m.points.RemoveAt(0);
                ReleaseDiagonal(m);
            }
            else
            {
                m.pos += delta * (step / dist);
                step = 0f;
            }
        }
    }

    private static int CompareFlowOrder(Motion a, Motion b)
    {
        int byHeight = a.pos.y.CompareTo(b.pos.y);
        if (byHeight != 0) return byHeight;
        int byColumn = a.pos.x.CompareTo(b.pos.x);
        return byColumn != 0 ? byColumn : a.sequence.CompareTo(b.sequence);
    }

    private DiagonalRegion DescribeDiagonal(Motion m, Vector2 end)
    {
        int size = board.TileSize;
        int x0 = Mathf.RoundToInt(m.pos.x / size), x1 = Mathf.RoundToInt(end.x / size);
        int y0 = Mathf.RoundToInt(-m.pos.y / size), y1 = Mathf.RoundToInt(-end.y / size);
        return new DiagonalRegion
        {
            left = x0 < x1 ? x0 : x1, right = x0 > x1 ? x0 : x1,
            top = y0 < y1 ? y0 : y1, bottom = y0 > y1 ? y0 : y1,
        };
    }

    private static bool SharesTurn(DiagonalRegion a, DiagonalRegion b) =>
        a.left <= b.right && b.left <= a.right && a.top <= b.bottom && b.top <= a.bottom;

    private bool TryEnterDiagonal(Motion m, Vector2 end)
    {
        if (m.diagonal != null) return true;
        var next = DescribeDiagonal(m, end);
        foreach (var transit in diagonalTransits)
            if (transit.owner != m && transit.owner.tile.IsCurrentLifetime(transit.owner.lifetime)
                && transit.owner.tile.IsMoveTokenCurrent(transit.owner.token) && SharesTurn(next, transit.region))
                return false;

        // A lower stone can be waiting for its explicit start delay or for another turn.
        // It retains priority at this entrance, independent of FallAction registration order.
        foreach (var leader in order)
        {
            if (leader == m) break;
            if (!leader.stationary && !IsOwned(leader)) continue;
            // A logically vacant outlet can still contain the preceding falling stone.
            // Wait until it physically clears the destination before turning into it.
            if (Mathf.Abs(leader.pos.x - end.x) < 0.5f
                && leader.pos.y > end.y - board.TileSize * ColumnGapCells)
            {
                if (leader.stationary)
                    Kick(leader.tile);
                return false;
            }
            if (leader.points.Count == 0 || Mathf.Abs(leader.points[0].x - leader.pos.x) < 0.5f) continue;
            if (SharesTurn(next, DescribeDiagonal(leader, leader.points[0]))) return false;
        }

        m.diagonal = new DiagonalTransit { owner = m, region = next };
        diagonalTransits.Add(m.diagonal);
        return true;
    }

    private static void ReleaseDiagonal(Motion m)
    {
        if (m.diagonal == null) return;
        m.diagonal.completed = true;
        m.diagonal = null;
    }

    private void Land(Motion m)
    {
        var tile = m.tile;
        active.Remove(tile);
        board.ClearReservedTileTargetCell(m.target);
        m.ticket.Done = true;
        tile.ResetFallStretch();

        if (IsAtLogicalTarget(m))
        {
            LandedCount++;
            tile.SnapToGrid(board.TileSize);
            if (m.settle && tile.RuntimeState == TileRuntimeState.Falling)
                tile.PlayLandingSettle(board.TileSize, m.settleDuration, m.settleStrength);
            if (tile.RuntimeState == TileRuntimeState.Falling)
                tile.SetRuntimeState(TileRuntimeState.Idle);
            // Event handlers may immediately start another motion or recycle this tile.
            // No writes to the old lifetime/owner are allowed after notification.
            if (!m.arrivalRaised)
                tile.RaiseFallArrivedFromMotion();
            return;
        }

        // Taş mantıksal olarak zaten daha aşağı planlandı; yeni hareketi gelene kadar momentumu tut.
        parked[tile] = new Parked
        {
            lifetime = m.lifetime,
            token = m.token,
            pos = m.pos,
            velocity = m.velocity,
            time = Time.time,
        };
    }

    private void Drop(Motion m)
    {
        ReleaseDiagonal(m);
        active.Remove(m.tile);
        board.ClearReservedTileTargetCell(m.target);
        if (m.ticket != null)
            m.ticket.Done = true;
        if (m.tile != null && m.tile.IsCurrentLifetime(m.lifetime) && m.tile.IsMoveTokenCurrent(m.token))
        {
            m.tile.ResetFallStretch();
            if (m.tile.RuntimeState == TileRuntimeState.Falling && !planned.ContainsKey(m.tile))
                m.tile.SetRuntimeState(TileRuntimeState.Idle);
        }
    }

    private void ProcessParked()
    {
        parkedScratch.Clear();
        foreach (var kv in parked)
            parkedScratch.Add(kv.Key);

        foreach (var tile in parkedScratch)
        {
            var p = parked[tile];
            if (tile == null || !tile || !tile.IsCurrentLifetime(p.lifetime) || active.ContainsKey(tile)
                || !tile.IsMoveTokenCurrent(p.token) || tile.RuntimeState != TileRuntimeState.Falling
                || (tile.RectTransform.anchoredPosition - p.pos).sqrMagnitude > 1f)
            {
                parked.Remove(tile);
                continue;
            }

            if (Time.time - p.time < ParkRecoveryTimeout)
                continue;

            // A queued action owns the complete route and its timing; never teleport over it.
            if (planned.ContainsKey(tile)) continue;

            // Recovery uses motion, never a visible snap across the board.
            parked.Remove(tile);
            kicked.Remove(tile);
            Debug.LogWarning($"[FallMotion] Taş ({tile.X},{tile.Y}) yeni hareketini alamadı; hedefe düşüş yeniden başlatıldı.");
            Start(tile, p.lifetime, tile.PlannedFallGeneration,
                new[] { p.pos, tile.GetFallCellPosition(tile.X, tile.Y, board.TileSize) },
                new Vector2Int(tile.X, tile.Y), false, 0f, false, 0f, 0f);
        }
    }

    // Kıpırdamayan ama yolu kaplayan taşlar (planlı-bekleyen, park etmiş) bu karelik engel olarak
    // sıraya girer. Spawn planları tahta dışında beklediği için engel değildir.
    private void AddStationaryBlockers()
    {
        foreach (var pair in planned)
        {
            var tile = pair.Key;
            if (pair.Value.isSpawn || active.ContainsKey(tile)
                || pair.Value.generation != tile.PlannedFallGeneration) continue;
            AddStationary(tile, pair.Value.lifetime);
        }
        foreach (var pair in parked)
        {
            var tile = pair.Key;
            if (active.ContainsKey(tile) || planned.ContainsKey(tile)) continue;
            AddStationary(tile, pair.Value.lifetime);
        }
    }

    private void AddStationary(TileView tile, int lifetime)
    {
        if (tile == null || !tile || !tile.IsCurrentLifetime(lifetime) || !tile.gameObject.activeInHierarchy)
            return;
        Vector2 pos = tile.RectTransform.anchoredPosition;
        order.Add(new Motion
        {
            tile = tile,
            lifetime = lifetime,
            stationary = true,
            pos = pos,
            lastWritten = pos,
            sequence = -1,
        });
    }

    // Bekleyen planı şimdi başlatır. Sonra gelen aynı plan Start'ı no-op olur (kicked).
    private bool Kick(TileView tile)
    {
        if (tile == null || !tile || active.ContainsKey(tile)
            || !planned.TryGetValue(tile, out var route) || !tile.IsCurrentLifetime(route.lifetime)
            || route.generation != tile.PlannedFallGeneration || route.points.Count == 0)
            return false;

        int lifetime = route.lifetime, generation = route.generation;
        Start(tile, lifetime, generation, route.points.ToArray(), new Vector2Int(tile.X, tile.Y),
            route.isSpawn, 0f, route.settle, route.settleDuration, route.settleStrength);
        if (tile.IsCurrentLifetime(lifetime))
            kicked[tile] = new KickedPlan { lifetime = lifetime, generation = generation };
        return true;
    }

    // starter'ın varış sütununda, ondan daha alta planlı bekleyen taşları alttan üste kaldırır.
    private void KickPlannedBelow(TileView starter, Vector2 end)
    {
        List<(TileView tile, float y)> lower = null;
        foreach (var pair in planned)
        {
            var route = pair.Value;
            if (pair.Key == starter || route.points.Count == 0 || active.ContainsKey(pair.Key)) continue;
            Vector2 dest = route.points[route.points.Count - 1];
            if (Mathf.Abs(dest.x - end.x) >= 0.5f || dest.y >= end.y - 0.5f) continue;
            (lower ??= new List<(TileView, float)>()).Add((pair.Key, dest.y));
        }
        if (lower == null) return;

        lower.Sort((a, b) => a.y.CompareTo(b.y));
        foreach (var entry in lower)
            Kick(entry.tile);
    }

    private bool IsOwned(Motion m) =>
        m.tile != null && m.tile
        && m.tile.IsCurrentLifetime(m.lifetime)
        && m.tile.IsMoveTokenCurrent(m.token)
        && (m.tile.RectTransform.anchoredPosition - m.lastWritten).sqrMagnitude <= 1f;

    private static bool IsAtLogicalTarget(Motion m) =>
        m.tile.X == m.target.x && m.tile.Y == m.target.y
        && m.tile.PlannedFallGeneration == m.generation;

    private static bool IsNextSegmentVertical(Motion m) =>
        m.points.Count > 0 && Mathf.Abs(m.points[0].x - m.pos.x) < 0.5f;

    private Motion FindTopMotionInColumn(float x, TileView exclude)
    {
        Motion top = null;
        foreach (var m in active.Values)
        {
            if (m.tile == exclude || !IsOwned(m) || Mathf.Abs(m.pos.x - x) >= 1f)
                continue;
            if (top == null || m.pos.y > top.pos.y)
                top = m;
        }
        return top;
    }

    private static void AddPoint(Motion m, Vector2 p)
    {
        Vector2 last = m.points.Count > 0 ? m.points[m.points.Count - 1] : m.pos;
        if ((last - p).sqrMagnitude >= 0.25f)
            m.points.Add(p);
    }

    private static float RemainingLength(Motion m)
    {
        float length = 0f;
        Vector2 prev = m.pos;
        for (int i = 0; i < m.points.Count; i++)
        {
            length += Vector2.Distance(prev, m.points[i]);
            prev = m.points[i];
        }
        return length;
    }

    // Düşüş esnemesi: yalnız dikey inişte; kalkışta hızla gerilir, varıştan hemen önce toparlar.
    private static void UpdateStretch(Motion m, bool vertical, float remaining, int tileSize, float dt)
    {
        float target = 0f;
        if (vertical)
        {
            float speedPx = Mathf.Max(0.0001f, m.velocity * tileSize);
            float rampIn = Mathf.Clamp01(m.elapsed / StretchRampIn);
            float rampOut = Mathf.Clamp01(remaining / speedPx / StretchRecover);
            target = Mathf.SmoothStep(0f, 1f, Mathf.Min(rampIn, rampOut));
        }

        m.stretchEnv = Mathf.MoveTowards(m.stretchEnv, target, dt / 0.04f);
        m.tile.ApplyFallStretch(m.stretchEnv);
    }
}
