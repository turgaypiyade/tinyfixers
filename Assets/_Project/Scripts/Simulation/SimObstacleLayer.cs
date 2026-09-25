using System.Collections.Generic;

/// <summary>Bir obstacle'a hasarın nereden geldiği — damageRule bunu kullanır.</summary>
public enum SimDamageSource { NormalMatch, Special, Booster }

/// <summary>
/// Simülasyonun saf C# obstacle katmanı. LevelData dizilerini klonlar, asset'e dokunmaz.
///
/// Eski sürümden farklar:
///  • ObstacleLibrary yerine SimRules snapshot'ı kullanır → thread-safe, paralel koşabilir.
///  • Hasar kaynağı ayrımı var (NormalMatch / Special) → SpecialOnly obstacle'lar artık
///    gerçekten yalnızca special ile kırılıyor (eskiden special hasarı "normal" sayılıyordu).
///  • CopyFrom ile allocation'sız derin kopya (bot lookahead'i için şart).
///  • Cargo (exitAtBottom) tabandan çıkınca toplanıyor.
///  • stackedObstacles: üstteki kırılınca alttaki authored obstacle geri geliyor.
/// </summary>
public sealed class SimObstacleLayer : ISimObstacleQuery
{
    private readonly int[] _obstacles;   // ObstacleId, hücre başına
    private readonly int[] _origins;     // origin hücre index'i (-1 = yok)
    private readonly int[] _remaining;   // kalan vuruş, origin index'iyle
    private readonly bool[] _permanentHoles;

    // stackedObstacles: üstteki obstacle kırılınca geri gelecek authored içerik
    private readonly int[] _beneathObstacles;
    private readonly int[] _beneathOrigins;

    private readonly SimRules _rules;
    private readonly int _width, _height;

    // ── Magnet: iki uç, aralarında enerji yolu. Her vuruş bir ucu bir adım içeri çeker;
    //    uçlar buluşunca çift yok olur (MagnetObstacleService ile aynı kural).
    //    Yol dizileri değişmez (paylaşılır); yalnız head/tail ilerler → kopyalaması ucuz.
    private int[][] _magnetPaths;
    private int[] _magnetOfCell;    // hücre → magnet index (-1 yok)
    private int[] _magnetHead;
    private int[] _magnetTail;

    private readonly Dictionary<int, int> _cleared = new();
    private readonly Dictionary<int, int> _initialCounts = new();

    public int TotalCleared { get; private set; }
    public int TotalInitial { get; private set; }
    private bool _plasticTwoStageSpawnedThisMove;

    public void BeginMove() => _plasticTwoStageSpawnedThisMove = false;

    public SimObstacleLayer(LevelData level, SimRules rules) : this(new SimLevel(level), rules) { }

    public SimObstacleLayer(SimLevel level, SimRules rules)
    {
        _width  = level.width;
        _height = level.height;
        _rules  = rules;

        int size = _width * _height;
        _obstacles = new int[size];
        _origins   = new int[size];
        _remaining = new int[size];
        _permanentHoles   = new bool[size];
        _beneathObstacles = new int[size];
        _beneathOrigins   = new int[size];

        for (int i = 0; i < size; i++)
        {
            _obstacles[i] = i < level.obstacles.Length ? level.obstacles[i] : 0;
            _origins[i]   = i < level.obstacleOrigins.Length ? level.obstacleOrigins[i] : -1;
            _remaining[i] = -1;
            _beneathObstacles[i] = 0;
            _beneathOrigins[i]   = -1;
            _permanentHoles[i]   = i < level.cells.Length && (CellType)level.cells[i] == CellType.Empty;
        }

        _magnetOfCell = new int[size];
        for (int i = 0; i < size; i++) _magnetOfCell[i] = -1;

        InitRemaining();
        StampMultiCellObstacles(level);
        StampStackedObstacles(level);
        SnapshotInitialCounts();
    }

    /// <summary>Arama dalları için boş ikiz — içerik CopyFrom ile dolar.</summary>
    private SimObstacleLayer(SimObstacleLayer src)
    {
        _width  = src._width;
        _height = src._height;
        _rules  = src._rules;

        int size = _width * _height;
        _obstacles        = new int[size];
        _origins          = new int[size];
        _remaining        = new int[size];
        _beneathObstacles = new int[size];
        _beneathOrigins   = new int[size];
        _permanentHoles   = src._permanentHoles;   // hiç değişmez → paylaşılabilir
        _magnetPaths      = src._magnetPaths;       // yollar değişmez → paylaşılabilir
        _magnetOfCell     = src._magnetOfCell;
        _magnetHead       = new int[src._magnetHead.Length];
        _magnetTail       = new int[src._magnetTail.Length];
        CopyFrom(src);
    }

    public SimObstacleLayer CreateScratch() => new(this);

    public void CopyFrom(SimObstacleLayer src)
    {
        System.Array.Copy(src._obstacles,        _obstacles,        _obstacles.Length);
        System.Array.Copy(src._origins,          _origins,          _origins.Length);
        System.Array.Copy(src._remaining,        _remaining,        _remaining.Length);
        System.Array.Copy(src._beneathObstacles, _beneathObstacles, _beneathObstacles.Length);
        System.Array.Copy(src._beneathOrigins,   _beneathOrigins,   _beneathOrigins.Length);

        System.Array.Copy(src._magnetHead, _magnetHead, _magnetHead.Length);
        System.Array.Copy(src._magnetTail, _magnetTail, _magnetTail.Length);

        _cleared.Clear();
        foreach (var kv in src._cleared) _cleared[kv.Key] = kv.Value;
        TotalCleared = src.TotalCleared;
        TotalInitial = src.TotalInitial;
        _plasticTwoStageSpawnedThisMove = src._plasticTwoStageSpawnedThisMove;

        if (_initialCounts.Count == 0)
            foreach (var kv in src._initialCounts) _initialCounts[kv.Key] = kv.Value;
    }

    // ── Kurulum ──────────────────────────────────────────────────────────────

    private void InitRemaining()
    {
        for (int idx = 0; idx < _obstacles.Length; idx++)
        {
            var id = (ObstacleId)_obstacles[idx];
            if (id == ObstacleId.None || _origins[idx] != idx) continue;
            _remaining[idx] = HitsOf(id);
        }
    }

    // Çok-hücreli obstacle'lar asset'te obstacles[]'a DEĞİL ayrı dizilerde tutulur
    // (oyun runtime'da stamp'ler). Sim de aynısını yapmalı, yoksa Tube/Magnet/Safe
    // levellarında hedef asla dolmaz (yanlış %0).
    private void StampMultiCellObstacles(SimLevel level)
    {
        if (level.tubes != null) foreach (var t in level.tubes) StampTube(t);

        int magnetCount = level.magnets?.Length ?? 0;
        _magnetPaths = new int[magnetCount][];
        _magnetHead  = new int[magnetCount];
        _magnetTail  = new int[magnetCount];
        for (int i = 0; i < magnetCount; i++) StampMagnet(level.magnets[i], i);

        if (level.safes != null) foreach (var sf in level.safes) StampSafe(sf);
    }

    private void StampTube(TubeEntry t)
    {
        int ox = t.originCellIndex % _width, oy = t.originCellIndex / _width;
        int dx = t.direction == TubeDirection.Left ? -1 : t.direction == TubeDirection.Right ? 1 : 0;
        int dy = t.direction == TubeDirection.Up   ? -1 : t.direction == TubeDirection.Down  ? 1 : 0;
        int len = t.length < 2 ? 2 : t.length;

        for (int i = 0; i < len; i++)
        {
            int cx = ox + dx * i, cy = oy + dy * i;
            if (!InBounds(cx, cy)) break;
            Stamp(Idx(cx, cy), t.originCellIndex, ObstacleId.Tube);
        }
        SetRemaining(t.originCellIndex, HitsOf(ObstacleId.Tube, 3));
    }

    private void StampMagnet(MagnetEntry m, int magnetIndex)
    {
        var path = m.pathCellIndices;
        _magnetPaths[magnetIndex] = path ?? System.Array.Empty<int>();
        _magnetHead[magnetIndex]  = 0;
        _magnetTail[magnetIndex]  = (path?.Length ?? 0) - 1;

        if (path == null || path.Length == 0) return;

        int origin = path[0];
        foreach (int cell in path)
        {
            Stamp(cell, origin, ObstacleId.Magnet);
            if (cell >= 0 && cell < _magnetOfCell.Length) _magnetOfCell[cell] = magnetIndex;
        }

        // Kalan "vuruş" = uçların buluşması için gereken adım sayısı.
        SetRemaining(origin, path.Length - 1);
    }

    // ── Magnet hasarı ────────────────────────────────────────────────────────

    /// <summary>
    /// Yalnız GÜNCEL UÇ hücresi vuruş alır (ara yol hücreleri inert). Vuruş o ucu bir adım
    /// içeri çeker, boşalan hücre açılır. Uçlar buluşunca çift yok olur → hedef +1.
    /// </summary>
    private void DamageMagnet(int cell)
    {
        int m = _magnetOfCell[cell];
        if (m < 0 || m >= _magnetPaths.Length) return;

        var path = _magnetPaths[m];
        int head = _magnetHead[m], tail = _magnetTail[m];
        if (head > tail) return;   // zaten yok olmuş

        bool isHead = path[head] == cell;
        bool isTail = path[tail] == cell;
        if (!isHead && !isTail) return;   // ara hücre → inert

        FreeCell(cell);
        if (isHead) head++; else tail--;

        _magnetHead[m] = head;
        _magnetTail[m] = tail;

        if (head < tail)
        {
            int origin = path[0];
            if (origin >= 0 && origin < _remaining.Length) _remaining[origin] = tail - head;
            return;
        }

        // Uçlar buluştu → çift yok oldu.
        for (int i = 0; i < path.Length; i++) FreeCell(path[i]);
        int o = path[0];
        if (o >= 0 && o < _remaining.Length) _remaining[o] = -1;
        RecordCleared(ObstacleId.Magnet);
    }

    private void FreeCell(int cell)
    {
        if (cell < 0 || cell >= _obstacles.Length) return;
        if (_beneathObstacles[cell] != 0)
        {
            _obstacles[cell] = _beneathObstacles[cell];
            _origins[cell]   = _beneathOrigins[cell];
            _beneathObstacles[cell] = 0;
            _beneathOrigins[cell]   = -1;
            return;
        }
        _obstacles[cell] = 0;
        _origins[cell]   = -1;
    }

    private void StampSafe(SafeEntry sf)
    {
        int ox = sf.originCellIndex % _width, oy = sf.originCellIndex / _width;
        int w = sf.width < 1 ? 1 : sf.width, h = sf.height < 1 ? 1 : sf.height;

        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                int cx = ox + dx, cy = oy + dy;
                if (!InBounds(cx, cy)) continue;
                int cell = Idx(cx, cy);
                StashBeneath(cell);                       // Safe altındaki içerik saklanır
                Stamp(cell, sf.originCellIndex, ObstacleId.Safe);
            }

        int locks = sf.redHits + sf.yellowHits + sf.greenHits;
        SetRemaining(sf.originCellIndex, locks < 1 ? 1 : locks);
    }

    private void StampStackedObstacles(SimLevel level)
    {
        if (level.stackedObstacles == null) return;

        foreach (var entry in level.stackedObstacles)
        {
            if (entry.obstacleId == ObstacleId.None) continue;
            var rule = _rules?.Get(entry.obstacleId);
            int w = rule?.SizeX ?? 1, h = rule?.SizeY ?? 1;
            int ox = entry.originCellIndex % _width, oy = entry.originCellIndex / _width;

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    int cx = ox + dx, cy = oy + dy;
                    if (!InBounds(cx, cy)) continue;
                    int cell = Idx(cx, cy);
                    StashBeneath(cell);
                    Stamp(cell, entry.originCellIndex, entry.obstacleId);
                }

            SetRemaining(entry.originCellIndex, HitsOf(entry.obstacleId));
        }
    }

    private void StashBeneath(int cell)
    {
        if (_obstacles[cell] == 0) return;
        _beneathObstacles[cell] = _obstacles[cell];
        _beneathOrigins[cell]   = _origins[cell];
    }

    private void Stamp(int cell, int origin, ObstacleId id)
    {
        if (cell < 0 || cell >= _obstacles.Length) return;
        _obstacles[cell] = (int)id;
        _origins[cell]   = origin;
    }

    private void SetRemaining(int origin, int hits)
    {
        if (origin >= 0 && origin < _remaining.Length) _remaining[origin] = hits < 1 ? 1 : hits;
    }

    private void SnapshotInitialCounts()
    {
        for (int i = 0; i < _obstacles.Length; i++)
        {
            var id = (ObstacleId)_obstacles[i];
            if (id == ObstacleId.None || _origins[i] != i) continue;
            _initialCounts.TryGetValue((int)id, out int prev);
            _initialCounts[(int)id] = prev + 1;
            TotalInitial++;
        }

        // Altta saklı (stacked/safe beneath) olanlar da sayılır — hedef olabilirler.
        for (int i = 0; i < _beneathObstacles.Length; i++)
        {
            var id = (ObstacleId)_beneathObstacles[i];
            if (id == ObstacleId.None || _beneathOrigins[i] != i) continue;
            _initialCounts.TryGetValue((int)id, out int prev);
            _initialCounts[(int)id] = prev + 1;
            TotalInitial++;
        }
    }

    private int HitsOf(ObstacleId id, int fallback = 1)
    {
        var rule = _rules?.Get(id);
        return rule != null ? rule.Hits : fallback;
    }

    // ── ISimObstacleQuery ────────────────────────────────────────────────────

    public bool HasObstacleAt(int x, int y)
        => InBounds(x, y) && _obstacles[Idx(x, y)] != 0;

    public ObstacleId ObstacleIdAt(int x, int y)
        => InBounds(x, y) ? (ObstacleId)_obstacles[Idx(x, y)] : ObstacleId.None;

    public bool IsMovableObstacleAt(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int rem)) return false;
        return rule.IsMovable(rem);
    }

    public bool IsInteractionLockedAt(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int rem)) return false;
        if (rule.LocksInteraction(rem)) return true;
        // Hücreyi kaplayan blocker (Stone, chest...) — üstünde swap yapılamaz.
        return rule.IsOverTileDamage(rem) && !rule.IsMovable(rem);
    }

    public bool IsCellBlocked(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int rem)) return false;
        return rule.BlocksCells(rem);
    }

    /// <summary>Bu hücre diagonal kayma için köşe olarak geçilebilir mi? (ObstacleDef.allowDiagonal)</summary>
    public bool IsDiagonalAllowedAt(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int rem)) return false;
        int i = rule.StageIndex(rem);
        return i >= 0 && rule.Stages[i].AllowDiagonal;
    }

    public bool HoldsTileAt(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int rem)) return false;
        return rule.HoldsTile(rem);
    }

    public bool IsCargoAt(int x, int y)
        => TryRule(x, y, out var rule, out _) && rule.ExitAtBottom;

    public bool CanTakeSpecialHit(int x, int y)
    {
        if (!TryRule(x, y, out var rule, out int remaining) || remaining <= 0 || rule.ExitAtBottom)
            return false;
        if (ObstacleIdAt(x, y) == ObstacleId.Magnet && !IsMagnetEndpoint(x, y)) return false;
        return AllowsDamage(rule.Damage(remaining), SimDamageSource.Special);
    }

    private bool TryRule(int x, int y, out SimRules.Rule rule, out int remaining)
    {
        rule = null; remaining = 0;
        if (!InBounds(x, y)) return false;
        int idx = Idx(x, y);
        var id = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return false;
        rule = _rules?.Get(id);
        if (rule == null) return false;
        remaining = ResolveRemaining(idx, rule);
        return true;
    }

    // ── Hasar ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normal eşleşmeyle temizlenen taş. Canlı oyun (BoardAnimator.CollectAdjacentOverTileBlockers)
    /// ile aynı kapsam: aynı hücredeki obstacle + KOMŞU over-tile blocker / overlay (Oil, Grass).
    /// </summary>
    public void ProcessMatchClear(int x, int y, TileType clearedTileType,
        List<(int x, int y)> deferredMagnetHits = null)
    {
        TryDamage(x, y, clearedTileType, SimDamageSource.NormalMatch, adjacent: false, deferredMagnetHits);

        TryDamage(x - 1, y, clearedTileType, SimDamageSource.NormalMatch, adjacent: true, deferredMagnetHits);
        TryDamage(x + 1, y, clearedTileType, SimDamageSource.NormalMatch, adjacent: true, deferredMagnetHits);
        TryDamage(x, y - 1, clearedTileType, SimDamageSource.NormalMatch, adjacent: true, deferredMagnetHits);
        TryDamage(x, y + 1, clearedTileType, SimDamageSource.NormalMatch, adjacent: true, deferredMagnetHits);
    }

    /// <summary>
    /// Special footprint'i bu hücreye değdi: yalnız hücrenin KENDİ obstacle'ı hasar alır.
    ///
    /// MAGNET İSTİSNASI: canlı oyun (SpecialCellUtils.TryAddMagnetEndpointImpact) footprint'in
    /// etki hücrelerini ÖNCE toplar, SONRA hasarı uygular — magnet yolundan yalnız o anki UÇ
    /// listeye girer. Hasarı tarama sırasında uygularsak uç taramanın önüne kaçar ve tek roket
    /// mıknatısı boydan boya keser. O yüzden magnet vuruşları ertelenir.
    /// </summary>
    public void ProcessSpecialImpact(int x, int y, List<(int x, int y)> deferredMagnetHits = null)
    {
        if (ObstacleIdAt(x, y) == ObstacleId.Magnet)
        {
            if (deferredMagnetHits == null)
            {
                if (IsMagnetEndpoint(x, y)) DamageMagnet(Idx(x, y));
                return;
            }

            if (IsMagnetEndpoint(x, y)) deferredMagnetHits.Add((x, y));
            return;
        }

        TryDamage(x, y, TileType.Gear, SimDamageSource.Special, adjacent: false);
    }

    /// <summary>Bu hücre bir mıknatısın GÜNCEL ucu mu? (ara yol hücreleri inert)</summary>
    public bool IsMagnetEndpoint(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int cell = Idx(x, y);
        if ((ObstacleId)_obstacles[cell] != ObstacleId.Magnet) return false;

        int m = _magnetOfCell[cell];
        if (m < 0 || m >= _magnetPaths.Length) return false;

        int head = _magnetHead[m], tail = _magnetTail[m];
        if (head > tail) return false;
        return _magnetPaths[m][head] == cell || _magnetPaths[m][tail] == cell;
    }

    /// <summary>Toplanan magnet uç vuruşlarını uygular (footprint taraması bittikten SONRA).</summary>
    public void ApplyDeferredMagnetHits(List<(int x, int y)> hits, int fromIndex)
    {
        for (int i = fromIndex; i < hits.Count; i++)
        {
            var (x, y) = hits[i];
            if (IsMagnetEndpoint(x, y)) DamageMagnet(Idx(x, y));
        }
        hits.RemoveRange(fromIndex, hits.Count - fromIndex);
    }

    private void TryDamage(int x, int y, TileType sourceTile, SimDamageSource source, bool adjacent,
        List<(int x, int y)> deferredMagnetHits = null)
    {
        if (!InBounds(x, y)) return;

        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return;
        if (id == ObstacleId.EnergyContainer) return;   // ayrı sistem, tasarımla dışarıda

        var rule = _rules?.Get(id);
        if (rule == null) return;

        int rem = ResolveRemaining(idx, rule);
        if (rem <= 0) return;

        if (rule.ExitAtBottom) return;              // Cargo: kırılmaz, yalnız tabandan çıkar

        // Grass kendi hücresindeki match'ten HASAR ALMAZ; yalnız komşu match aşındırır.
        if (id == ObstacleId.Grass && source == SimDamageSource.NormalMatch && !adjacent) return;

        // Komşuluk hasarı yalnız over-tile blocker'lara ve overlay'lere (Oil/Grass) gider;
        // taşın ALTINDAKİ katman (Mud) sadece kendi hücresindeki taş temizlenince hasar alır.
        if (adjacent && !rule.IsOverTileDamage(rem) && !rule.IsOverlay(rem)) return;

        if (!AllowsDamage(rule.Damage(rem), source)) return;

        if (source == SimDamageSource.NormalMatch &&
            rule.RestrictNormalMatchTileType && rule.RequiredNormalMatchTileType != sourceTile)
            return;

        if (id == ObstacleId.Magnet)
        {
            if (!IsMagnetEndpoint(x, y)) return;
            if (deferredMagnetHits != null) deferredMagnetHits.Add((x, y));
            else DamageMagnet(idx);
            return;
        }

        int origin = _origins[idx];
        if (origin < 0 || origin >= _remaining.Length) return;

        _remaining[origin] = rem - 1;
        if (_remaining[origin] <= 0) ClearObstacle(origin, id);
    }

    private static bool AllowsDamage(ObstacleDamageSourceRule rule, SimDamageSource source) => rule switch
    {
        ObstacleDamageSourceRule.Any         => source != SimDamageSource.Booster,
        ObstacleDamageSourceRule.NormalOnly  => source == SimDamageSource.NormalMatch,
        ObstacleDamageSourceRule.SpecialOnly => source == SimDamageSource.Special,
        _                                    => false,   // BoosterOnly / Disabled / FullyDisabled
    };

    private void ClearObstacle(int origin, ObstacleId id)
    {
        for (int i = 0; i < _obstacles.Length; i++)
        {
            if ((ObstacleId)_obstacles[i] != id || _origins[i] != origin) continue;

            // Altında saklı authored içerik varsa geri gelir (Safe / stackedObstacles).
            if (_beneathObstacles[i] != 0)
            {
                _obstacles[i] = _beneathObstacles[i];
                _origins[i]   = _beneathOrigins[i];
                _beneathObstacles[i] = 0;
                _beneathOrigins[i]   = -1;

                int belowOrigin = _origins[i];
                if (belowOrigin >= 0 && belowOrigin < _remaining.Length && _remaining[belowOrigin] < 0)
                    _remaining[belowOrigin] = HitsOf((ObstacleId)_obstacles[i]);
            }
            else
            {
                _obstacles[i] = 0;
                _origins[i]   = -1;
            }
        }

        _remaining[origin] = -1;
        RecordCleared(id);
    }

    private void RecordCleared(ObstacleId id)
    {
        _cleared.TryGetValue((int)id, out int prev);
        _cleared[(int)id] = prev + 1;
        TotalCleared++;
    }

    // ── Sayaçlar ─────────────────────────────────────────────────────────────

    public int GetClearedCount(ObstacleId id)
    {
        _cleared.TryGetValue((int)id, out int c);
        return c;
    }

    public int GetInitialCount(ObstacleId id)
    {
        _initialCounts.TryGetValue((int)id, out int c);
        return c;
    }

    /// <summary>
    /// Hedef obstacle'larda kalan TOPLAM vuruş. Bot için kritik shaping sinyali:
    /// 3 vuruşluk taşa 1 vuruş vurmak da ilerlemedir, hedef sayacı henüz artmasa bile.
    /// </summary>
    public int GoalDamageRemaining(SimGoalSet goals)
    {
        int sum = 0;
        for (int i = 0; i < _obstacles.Length; i++)
        {
            var id = (ObstacleId)_obstacles[i];
            if (id == ObstacleId.None || _origins[i] != i) continue;
            if (!goals.NeedsObstacle(id)) continue;
            sum += _remaining[i] > 0 ? _remaining[i] : 0;
        }
        return sum;
    }

    /// <summary>Hedef obstacle'a olan en kısa Manhattan mesafesi (yoksa -1).</summary>
    public int DistanceToNearestGoalObstacle(int x, int y, SimGoalSet goals, int maxRadius)
    {
        for (int r = 0; r <= maxRadius; r++)
            for (int dy = -r; dy <= r; dy++)
            {
                int dx = r - (dy < 0 ? -dy : dy);
                if (IsGoalObstacleAt(x + dx, y + dy, goals) || IsGoalObstacleAt(x - dx, y + dy, goals))
                    return r;
            }
        return -1;
    }

    private bool IsGoalObstacleAt(int x, int y, SimGoalSet goals)
    {
        var id = ObstacleIdAt(x, y);
        return id != ObstacleId.None && goals.NeedsObstacle(id);
    }

    /// <summary>Tahtada hâlâ duran (kırılmamış) origin sayısı.</summary>
    public int GetRemainingCount(ObstacleId id)
    {
        int count = 0;
        for (int i = 0; i < _obstacles.Length; i++)
            if ((ObstacleId)_obstacles[i] == id && _origins[i] == i) count++;
        return count;
    }

    // ── Holes senkronu ───────────────────────────────────────────────────────

    /// <summary>
    /// Güncel obstacle durumundan state.Holes'ü yeniden kurar. Kırılan obstacle'ın hücresi
    /// delik olmaktan çıkar (taş akabilir), yaşayan obstacle'ın hücresi delik kalır.
    /// </summary>
    public void SyncHoles(SimState state)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int idx = Idx(x, y);
                bool blocked = _obstacles[idx] != 0 && IsCellBlocked(x, y);
                bool occupied = _obstacles[idx] != 0 && IsMovableObstacleAt(x, y);
                state.Holes[x, y] = _permanentHoles[idx] || blocked || occupied;
            }
        }
    }

    public void InitHoles(SimState state)
    {
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                state.PermanentHoles[x, y] = _permanentHoles[Idx(x, y)];
        SyncHoles(state);
    }

    /// <summary>Bu hücre bir magnet hücresine 4-komşu mu? (special kafesleme kuralı)</summary>
    public bool IsAdjacentToMagnet(int x, int y)
        => ObstacleIdAt(x + 1, y) == ObstacleId.Magnet
        || ObstacleIdAt(x - 1, y) == ObstacleId.Magnet
        || ObstacleIdAt(x, y + 1) == ObstacleId.Magnet
        || ObstacleIdAt(x, y - 1) == ObstacleId.Magnet;

    /// <summary>Her magnet için uçların buluşmasına kalan adım (teşhis raporu).</summary>
    public string DescribeMagnets()
    {
        if (_magnetPaths == null || _magnetPaths.Length == 0) return "-";
        var parts = new List<string>();
        for (int i = 0; i < _magnetPaths.Length; i++)
        {
            int left = _magnetTail[i] - _magnetHead[i];
            parts.Add(left < 0 ? "bitti" : $"{left} adım");
        }
        return string.Join(" | ", parts);
    }

    public bool HasAnyMagnet
    {
        get
        {
            if (_magnetPaths == null) return false;
            for (int i = 0; i < _magnetPaths.Length; i++)
                if (_magnetHead[i] <= _magnetTail[i]) return true;
            return false;
        }
    }

    // ── Movable hedef obstacle'ın yeniden üretimi ────────────────────────────

    /// <summary>
    /// Canlı oyun (CascadeLogic.TryPickMovableGoalToSpawn) kuralı: hedefi HÂLÂ eksik olan bir
    /// MOVABLE obstacle, refill sırasında taş yerine tepeden yeniden doğar. Yani plastic_red gibi
    /// obstacle'ların levelda hedef sayısı kadar durması GEREKMEZ — kırdıkça yenisi gelir.
    /// Sınır: cargo dışındaki movable'lar için geçiş (pass) başına 1 spawn, ve
    /// tahtadaki canlı sayı kalan hedefi karşılıyorsa yenisi doğmaz.
    /// </summary>
    public ObstacleId TrySpawnMovableGoal(SimState state, int x, int y, SimGoalSet goals, bool spawnedThisPass)
    {
        if (goals == null) return ObstacleId.None;

        for (int i = 0; i < goals.GoalCount; i++)
        {
            var entry = goals.GetEntry(i);
            if (entry.Kind != LevelGoalTargetType.Obstacle || entry.Met) continue;

            var rule = _rules?.Get(entry.Obstacle);
            if (rule == null || rule.IsFallback) continue;
            if (!rule.IsMovable(rule.Hits)) continue;
            if (!rule.ExitAtBottom && spawnedThisPass) continue;
            if (entry.Obstacle == ObstacleId.PlasticTwoStage && _plasticTwoStageSpawnedThisMove) continue;

            int remaining = goals.RemainingFor(entry.Obstacle);
            if (GetRemainingCount(entry.Obstacle) >= remaining) continue;

            int cell = Idx(x, y);
            _obstacles[cell] = (int)entry.Obstacle;
            _origins[cell]   = cell;
            _remaining[cell] = rule.Hits;
            state.Holes[x, y] = true;   // movable hücreyi işgal eder
            if (entry.Obstacle == ObstacleId.PlasticTwoStage) _plasticTwoStageSpawnedThisMove = true;
            return entry.Obstacle;
        }

        return ObstacleId.None;
    }

    // ── MovableObstacle yerçekimi (tek adım) ─────────────────────────────────

    /// <summary>
    /// 1x1 movable obstacle'ları bir adım aşağı düşürür. Cargo (exitAtBottom) en alt satırdan
    /// çıkınca toplanır. Değişiklik olduysa true döner.
    /// </summary>
    public bool ApplyGravityStep(SimState state)
    {
        bool moved = false;

        for (int y = _height - 1; y >= 0; y--)
        {
            for (int x = 0; x < _width; x++)
            {
                int idx = Idx(x, y);
                var id = (ObstacleId)_obstacles[idx];
                if (id == ObstacleId.None || _origins[idx] != idx) continue;

                var rule = _rules?.Get(id);
                if (rule == null) continue;

                int rem = ResolveRemaining(idx, rule);
                if (!rule.IsMovable(rem)) continue;

                // Cargo: en alt satıra indiyse board'dan çıkar → toplandı.
                if (rule.ExitAtBottom && y == _height - 1)
                {
                    _obstacles[idx] = 0;
                    _origins[idx]   = -1;
                    _remaining[idx] = -1;
                    state.Holes[x, y] = _permanentHoles[idx];
                    RecordCleared(id);
                    moved = true;
                    continue;
                }

                int ny = y + 1;
                if (ny >= _height) continue;
                int nIdx = Idx(x, ny);

                if (_permanentHoles[nIdx]) continue;
                if (state.Grid[x, ny] != null) continue;
                if (_obstacles[nIdx] != 0) continue;

                _obstacles[nIdx] = _obstacles[idx];
                _origins[nIdx]   = nIdx;
                _remaining[nIdx] = _remaining[idx];

                _obstacles[idx] = 0;
                _origins[idx]   = -1;
                _remaining[idx] = -1;

                state.Holes[x, y]  = _permanentHoles[idx];
                state.Holes[x, ny] = true;
                moved = true;
            }
        }

        return moved;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private int ResolveRemaining(int idx, SimRules.Rule rule)
    {
        int origin = _origins[idx];
        if (origin >= 0 && origin < _remaining.Length && _remaining[origin] >= 0)
            return _remaining[origin];
        return rule != null ? rule.Hits : 1;
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
    private int Idx(int x, int y) => y * _width + x;
}
