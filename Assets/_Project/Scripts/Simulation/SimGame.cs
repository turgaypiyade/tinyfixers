using System.Collections.Generic;

/// <summary>Tek bir hamlenin ürettiği ölçümler.</summary>
public struct SimMoveStats
{
    public int TilesCleared;
    public int SpecialsCreated;
    public int SpecialsActivated;
    public int CombosActivated;
    public int CascadeSteps;
    public int MaxChain;
    public int ObstaclesCleared;

    public void Add(in SimMoveStats o)
    {
        TilesCleared      += o.TilesCleared;
        SpecialsCreated   += o.SpecialsCreated;
        SpecialsActivated += o.SpecialsActivated;
        CombosActivated   += o.CombosActivated;
        CascadeSteps      += o.CascadeSteps;
        ObstaclesCleared  += o.ObstaclesCleared;
        if (o.MaxChain > MaxChain) MaxChain = o.MaxChain;
    }
}

/// <summary>
/// TEK YETKİLİ oyun motoru: board + obstacle + hedefler + hamle çözümü tek yerde.
///
/// Eskiden hamle çözümü SimRunner'da, aynı mantığın "tahmin" kopyası SimBot'ta duruyordu —
/// ikisi birbirinden kayıyordu. Artık bot da bu motorun KENDİSİNİ klonlayıp oynatarak
/// karar veriyor. Canlı motorla uyum regresyon testleriyle ayrıca denetlenir.
/// </summary>
public sealed class SimGame
{
    public readonly SimState State;
    public readonly SimObstacleLayer Obstacles;
    public readonly SimGoalSet Goals;
    public readonly SimLevel Level;

    private readonly SimMatchFinder _finder;
    private readonly SimRules _rules;
    private System.Random _rng;

    private readonly HashSet<(int, int)> _visited = new();
    private readonly List<(int x, int y)> _colorBuf = new();
    private readonly List<SimSpecialCreation.Creation> _creations = new(2);
    private readonly HashSet<(int x, int y)> _protectedCreations = new();

    // Bir special "pass"i (veya bir cascade dalgası) boyunca toplanan magnet uç vuruşları.
    // Canlı oyun gibi: önce topla, sonra uygula → tek roket mıknatısı 1 adım kısaltır.
    private readonly List<(int x, int y)> _magnetHits = new();

    /// <summary>Son hamlede temizlenen hücreler — bot skorlaması "hedefe yakın mı oynadım" diye bakar.</summary>
    public readonly List<(int x, int y)> ClearedCells = new();

    // Magnet special kafesi: hamle BAŞINDA magnet'e komşu duran special'ların anlık görüntüsü.
    private readonly List<(int x, int y, TileData tile)> _cageSnapshot = new();

    /// <summary>Bu oyunda magnet tarafından kafeslenen special sayısı (teşhis).</summary>
    public int SpecialsCaged { get; private set; }

    public int MovesLeft { get; private set; }
    public int MovesUsed { get; private set; }
    public int Shuffles  { get; private set; }
    public SimMoveStats Totals;

    // ── Kurulum ──────────────────────────────────────────────────────────────

    public SimGame(LevelData level, SimRules rules, System.Random rng) : this(new SimLevel(level), rules, rng) { }

    public SimGame(SimLevel level, SimRules rules, System.Random rng)
    {
        Level      = level;
        _rules     = rules;
        _rng       = rng;
        Obstacles  = new SimObstacleLayer(level, rules);
        State      = new SimState(level.width, level.height, level.randomPool, Obstacles);
        Goals      = new SimGoalSet(level);
        _finder    = new SimMatchFinder(State);
        MovesLeft  = level.moves;
    }

    /// <summary>Arama dalı için aynı boyutta boş ikiz.</summary>
    private SimGame(SimGame src)
    {
        Level      = src.Level;
        _rules     = src._rules;
        _rng       = new System.Random(12345);
        Obstacles  = src.Obstacles.CreateScratch();
        State      = new SimState(Level.width, Level.height, Level.randomPool, Obstacles);
        Goals      = src.Goals.CreateScratch();
        _finder    = new SimMatchFinder(State);
    }

    public SimGame CreateScratch() => new(this);

    /// <summary>Arama dalını mevcut duruma eşitler — allocation'sız.</summary>
    public void CopyFrom(SimGame src, int branchSeed)
    {
        Obstacles.CopyFrom(src.Obstacles);
        State.CopyFrom(src.State);
        Goals.CopyFrom(src.Goals);
        _magnetHits.Clear();
        MovesLeft = src.MovesLeft;
        MovesUsed = src.MovesUsed;
        Shuffles  = src.Shuffles;
        SpecialsCaged = src.SpecialsCaged;
        _cageSnapshot.Clear();
        Totals    = src.Totals;
        _rng      = new System.Random(branchSeed);
        _finder.InvalidateRunCache();
    }

    /// <summary>Tahtayı kurar: hazır eşleşmesiz + oynanabilir. Hedefe bedava kredi YOK.</summary>
    public void Start()
    {
        Obstacles.InitHoles(State);
        State.FillInitial(Level, _rng);
        SimCascade.Settle(State, Obstacles, Goals, _rng);
        _finder.InvalidateRunCache();

        // FillInitial hazır eşleşme bırakmaz ama movable obstacle oturması sonrası
        // teorik olarak oluşabilir — sessizce çöz, hedefe yazmadan.
        ResolveCascades(ref Totals, creditGoals: false);
        Goals.SyncObstacles(Obstacles);
    }

    // ── Hamle ────────────────────────────────────────────────────────────────

    /// <summary>Bir takası oynar ve tahta durulana kadar çözer.</summary>
    public SimMoveStats PlayMove(SimSwap swap)
    {
        if (MovesLeft <= 0) throw new System.InvalidOperationException("No moves remaining.");
        if (!SimMoves.IsLegal(State, swap)) throw new System.ArgumentException($"Illegal move: {swap}");
        var stats = new SimMoveStats();
        ClearedCells.Clear();
        _protectedCreations.Clear();
        Obstacles.BeginMove();
        CaptureCageCandidates();

        SimMoves.Apply(State, swap);
        MovesUsed++;
        MovesLeft--;

        int clearedBefore = Obstacles.TotalCleared;

        ActivateSwapSpecials(swap, ref stats);
        _protectedCreations.Clear();
        ResolveCascades(ref stats, creditGoals: true, playerSwap: swap);

        stats.ObstaclesCleared = Obstacles.TotalCleared - clearedBefore;
        EvaluateCaging();
        Goals.SyncObstacles(Obstacles);
        Totals.Add(stats);
        return stats;
    }

    /// <summary>Geçerli hamle kalmadıysa canlı oyun gibi karıştırır.</summary>
    // ── Magnet special kafesi (MagnetObstacleService ile aynı kural) ─────────

    /// <summary>
    /// Hamle BAŞINDA: magnet hücresine 4-komşu duran her special işaretlenir. Bu hamlede YENİ
    /// oluşan special'lar henüz tahtada olmadığı için listeye girmez — onlar bir hamle bedava alır.
    /// </summary>
    private void CaptureCageCandidates()
    {
        _cageSnapshot.Clear();
        if (!Obstacles.HasAnyMagnet) return;

        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
            {
                var t = State.Grid[x, y];
                if (t == null || t.Special == TileSpecial.None) continue;
                if (State.SpecialLocked[x, y]) continue;
                if (!Obstacles.IsAdjacentToMagnet(x, y)) continue;
                _cageSnapshot.Add((x, y, t));
            }
    }

    /// <summary>
    /// Hamle SONUNDA: işaretli special hâlâ AYNI hücrede, AYNI taş ve hâlâ special ise — yani bu
    /// hamlede ne kımıldadı ne kullanıldı — kafeslenir. Oyuncu onu bir daha oynayamaz; yalnız
    /// düşer ve bir AoE'ye girerse patlar. Magnet'li levellarda special'ı BEKLETMEK maliyetlidir.
    /// </summary>
    private void EvaluateCaging()
    {
        for (int i = 0; i < _cageSnapshot.Count; i++)
        {
            var (x, y, tile) = _cageSnapshot[i];
            if (!ReferenceEquals(State.Grid[x, y], tile)) continue;   // kımıldadı / tüketildi
            if (tile.Special == TileSpecial.None) continue;           // kullanıldı
            if (!Obstacles.IsAdjacentToMagnet(x, y)) continue;
            if (State.SpecialLocked[x, y]) continue;

            State.SpecialLocked[x, y] = true;
            SpecialsCaged++;
        }
        _cageSnapshot.Clear();
    }

    public bool TryShuffle()
    {
        if (!State.Shuffle(_rng)) return false;
        Shuffles++;
        _finder.InvalidateRunCache();
        ResolveCascades(ref Totals, creditGoals: true);
        Goals.SyncObstacles(Obstacles);
        return true;
    }

    public void FindMoves(List<SimSwap> into) => SimMoves.FindValid(State, into);

    // ── Cascade ──────────────────────────────────────────────────────────────

    private void ResolveCascades(ref SimMoveStats stats, bool creditGoals, SimSwap? playerSwap = null)
    {
        int chain = 0;
        bool preferSwapTiles = playerSwap.HasValue;

        while (true)
        {
            var matches = _finder.FindAllMatches();
            if (matches.Count == 0) break;

            chain++;
            stats.CascadeSteps++;

            if (chain > 256)
                throw new System.InvalidOperationException("Cascade did not settle after 256 waves.");
            int magnetMark = _magnetHits.Count;
            ResolveMatchWave(matches, preferSwapTiles ? playerSwap : null, ref stats, creditGoals);
            preferSwapTiles = false;

            Obstacles.ApplyDeferredMagnetHits(_magnetHits, magnetMark);
            Goals.SyncObstacles(Obstacles);
            Obstacles.SyncHoles(State);
            SimCascade.Settle(State, Obstacles, Goals, _rng);
            _finder.InvalidateRunCache();
        }

        if (chain > stats.MaxChain) stats.MaxChain = chain;
    }

    private void ResolveMatchWave(HashSet<TileData> matches, SimSwap? swap,
        ref SimMoveStats stats, bool creditGoals, bool protectCreated = false)
    {
        SimSpecialCreation.Select(State, _finder, matches, swap, _creations);
        foreach (var creation in _creations)
        {
            creation.Tile.SetSpecial(creation.Special);
            if (creation.Special == TileSpecial.SystemOverride)
                creation.Tile.SetOverrideBaseType(creation.Tile.Type);
            if (protectCreated) _protectedCreations.Add((creation.Tile.X, creation.Tile.Y));
            stats.SpecialsCreated++;
        }
        foreach (var tile in matches)
            if (tile.Special == TileSpecial.None)
                ClearMatchedTile(tile.X, tile.Y, ref stats, creditGoals);
    }

    // Potential and creation ordering follow SpecialCreationService.Score.
    public static int SpecialRank(TileSpecial special) => SimSpecialCreation.Rank(special) / 10;

    // ── Taş temizleme ────────────────────────────────────────────────────────

    private void ClearMatchedTile(int x, int y, ref SimMoveStats stats, bool creditGoals)
    {
        var tile = State.Grid[x, y];
        if (tile == null) return;

        Obstacles.ProcessMatchClear(x, y, tile.Type, _magnetHits);
        if (creditGoals && !State.Phantom[x, y]) Goals.RecordTileCleared(tile.Type);

        ClearedCells.Add((x, y));
        State.ClearCell(x, y);
        stats.TilesCleared++;
    }

    // ── Special aktivasyonu ──────────────────────────────────────────────────

    private void ActivateSwapSpecials(SimSwap swap, ref SimMoveStats stats)
    {
        var ta = State.Grid[swap.AX, swap.AY];
        var tb = State.Grid[swap.BX, swap.BY];
        bool sa = ta != null && ta.Special != TileSpecial.None;
        bool sb = tb != null && tb.Special != TileSpecial.None;
        if (!sa && !sb) return;

        _visited.Clear();
        _magnetHits.Clear();

        if (swap.IsTap)
        {
            stats.SpecialsActivated++;
            Trigger(swap.AX, swap.AY, ta.Special, ColorOf(ta), ref stats);
        }
        else if (sa && sb)
        {
            stats.CombosActivated++;
            stats.SpecialsActivated += 2;
            // Original swapA ends at B, matching the live resolver's combo origin.
            TriggerCombo(swap.BX, swap.BY, tb.Special, ColorOf(ta),
                         swap.AX, swap.AY, ta.Special, ColorOf(tb), ref stats);
        }
        else
        {
            var special = sa ? ta : tb;
            var partner = sa ? tb : ta;
            var type = special.Special;
            var color = ColorOf(partner ?? special);
            int sx = special.X, sy = special.Y;

            // Live special+normal swaps clear the normal partner's match BEFORE activation,
            // without gravity. Newly created specials are protected from that PulseCore.
            if (type != TileSpecial.SystemOverride)
            {
                int mark = _magnetHits.Count;
                var matches = _finder.FindAllMatches();
                if (matches.Count > 0)
                    ResolveMatchWave(matches, swap, ref stats, true, protectCreated: true);
                Obstacles.ApplyDeferredMagnetHits(_magnetHits, mark);
            }
            stats.SpecialsActivated++;
            Trigger(sx, sy, type, color, ref stats);
        }

        Goals.SyncObstacles(Obstacles);
        Obstacles.SyncHoles(State);
        SimCascade.Settle(State, Obstacles, Goals, _rng);
        _finder.InvalidateRunCache();
    }

    private void Trigger(int x, int y, TileSpecial sp, TileType targetColor, ref SimMoveStats stats)
    {
        if (!_visited.Add((x, y))) return;
        int magnetMark = _magnetHits.Count;
        Consume(x, y, ref stats);

        switch (sp)
        {
            case TileSpecial.LineH:
                ClearRow(y, ref stats);
                break;
            case TileSpecial.LineV:
                ClearColumn(x, ref stats);
                break;
            case TileSpecial.PatchBot:
            {
                var (tx, ty) = BestPatchBotTarget(x, y);
                HitPatchBotTarget(tx, ty, ref stats);
                break;
            }
            case TileSpecial.PulseCore:
                ClearSquare(x, y, 2, ref stats);
                break;
            case TileSpecial.SystemOverride:
                ClearColor(targetColor, ref stats);
                break;
        }

        Obstacles.ApplyDeferredMagnetHits(_magnetHits, magnetMark);
    }

    private void TriggerCombo(
        int ax, int ay, TileSpecial a, TileType aColor,
        int bx, int by, TileSpecial b, TileType bColor, ref SimMoveStats stats)
    {
        int comboMark = _magnetHits.Count;
        TriggerComboInner(ax, ay, a, aColor, bx, by, b, bColor, ref stats);
        Obstacles.ApplyDeferredMagnetHits(_magnetHits, comboMark);
    }

    private void TriggerComboInner(
        int ax, int ay, TileSpecial a, TileType aColor,
        int bx, int by, TileSpecial b, TileType bColor, ref SimMoveStats stats)
    {
        Consume(ax, ay, ref stats);
        Consume(bx, by, ref stats);

        if (a == TileSpecial.SystemOverride && b == TileSpecial.SystemOverride)
        {
            ClearBoard(ref stats);
            return;
        }

        if (a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride)
        {
            var converted = a == TileSpecial.SystemOverride ? b : a;
            var color     = a == TileSpecial.SystemOverride ? aColor : bColor;
            TriggerOverrideCombo(converted, color, ref stats);
            return;
        }

        if (a == TileSpecial.PulseCore && b == TileSpecial.PulseCore)
        {
            ClearSquare(ax, ay, 4, ref stats);
            return;
        }

        if ((IsLine(a) && b == TileSpecial.PulseCore) || (IsLine(b) && a == TileSpecial.PulseCore))
        {
            int cx = IsLine(a) ? ax : bx;
            int cy = IsLine(a) ? ay : by;
            for (int y = cy - 1; y <= cy + 1; y++) ClearRow(y, ref stats);
            for (int x = cx - 1; x <= cx + 1; x++) ClearColumn(x, ref stats);
            return;
        }

        if (a == TileSpecial.PatchBot && b == TileSpecial.PatchBot)
        {
            // The live combo launches two source bots and converts one eligible normal
            // tile into the bonus bot. No eligible tile means only two launches.
            int bonus = ConsumeBonusPatchBot(ax, ay, bx, by, ref stats) ? 1 : 0;
            for (int i = 0; i < 2 + bonus; i++)
            {
                var target = BestPatchBotTarget(ax, ay);
                HitPatchBotTarget(target.x, target.y, ref stats);
            }
            return;
        }

        if ((a == TileSpecial.PatchBot && b == TileSpecial.PulseCore) ||
            (b == TileSpecial.PatchBot && a == TileSpecial.PulseCore))
        {
            var (tx, ty) = BestPatchBotTarget(ax, ay);
            ClearSquare(tx, ty, 2, ref stats);
            return;
        }

        if ((a == TileSpecial.PatchBot && IsLine(b)) || (b == TileSpecial.PatchBot && IsLine(a)))
        {
            var line = IsLine(a) ? a : b;
            var (tx, ty) = BestPatchBotTarget(ax, ay);
            if (line == TileSpecial.LineH) ClearRow(ty, ref stats);
            else                            ClearColumn(tx, ref stats);
            return;
        }

        if (IsLine(a) && IsLine(b))
        {
            ClearRow(ay, ref stats);
            ClearColumn(ax, ref stats);
            return;
        }

        Trigger(ax, ay, a, aColor, ref stats);
        Trigger(bx, by, b, bColor, ref stats);
    }

    private void TriggerOverrideCombo(TileSpecial converted, TileType color, ref SimMoveStats stats)
    {
        if (converted == TileSpecial.PulseCore)
        {
            CollectColorCells(color);
            foreach (var (x, y) in _colorBuf) ClearSquare(x, y, 2, ref stats);
            return;
        }

        if (converted == TileSpecial.PatchBot)
        {
            CollectColorCells(color);
            int bots = _colorBuf.Count;
            foreach (var cell in _colorBuf) Consume(cell.x, cell.y, ref stats);
            stats.SpecialsActivated += bots;
            for (int i = 0; i < bots; i++)
            {
                var target = BestPatchBotTarget(0, State.Height - 1);
                HitPatchBotTarget(target.x, target.y, ref stats);
            }
            return;
        }

        if (IsLine(converted))
        {
            CollectColorCells(color);
            foreach (var (x, y) in _colorBuf)
            {
                if (converted == TileSpecial.LineH) ClearRow(y, ref stats);
                else                                 ClearColumn(x, ref stats);
            }
            return;
        }

        ClearColor(color, ref stats);
    }

    // ── Footprint temizleyiciler ─────────────────────────────────────────────

    private void ClearForSpecial(int x, int y, ref SimMoveStats stats)
    {
        if (!State.InBounds(x, y)) return;
        if (_visited.Contains((x, y))) return;
        if (_protectedCreations.Contains((x, y))) return;

        var t = State.Grid[x, y];

        // Footprint başka bir special'a değdi → zincirle (kendi trigger'ıyla).
        if (t != null && t.Special != TileSpecial.None)
        {
            stats.SpecialsActivated++;
            Trigger(x, y, t.Special, ColorOf(t), ref stats);
            return;
        }

        _visited.Add((x, y));

        // Taş olsun olmasın, footprint hücredeki obstacle'a hasar verir.
        Obstacles.ProcessSpecialImpact(x, y, _magnetHits);

        if (t == null) return;
        if (!State.Phantom[x, y]) Goals.RecordTileCleared(t.Type);
        ClearedCells.Add((x, y));
        State.ClearCell(x, y);
        stats.TilesCleared++;
    }

    private void Consume(int x, int y, ref SimMoveStats stats)
    {
        if (!State.InBounds(x, y)) return;
        _visited.Add((x, y));
        var tile = State.Grid[x, y];
        if (tile == null) return;
        Obstacles.ProcessSpecialImpact(x, y, _magnetHits);
        if (!State.Phantom[x, y]) Goals.RecordTileCleared(tile.Type);
        ClearedCells.Add((x, y));
        State.ClearCell(x, y);
        stats.TilesCleared++;
    }

    private void ClearRow(int y, ref SimMoveStats stats)
    {
        if (y < 0 || y >= State.Height) return;
        for (int x = 0; x < State.Width; x++) ClearForSpecial(x, y, ref stats);
    }

    private void ClearColumn(int x, ref SimMoveStats stats)
    {
        if (x < 0 || x >= State.Width) return;
        for (int y = 0; y < State.Height; y++) ClearForSpecial(x, y, ref stats);
    }

    private void ClearSquare(int cx, int cy, int radius, ref SimMoveStats stats)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                ClearForSpecial(x, y, ref stats);
    }

    private void ClearBoard(ref SimMoveStats stats)
    {
        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
                ClearForSpecial(x, y, ref stats);
    }

    private void ClearColor(TileType color, ref SimMoveStats stats)
    {
        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
                if (State.IsColor(x, y, color))
                    ClearForSpecial(x, y, ref stats);
    }

    private void CollectColorCells(TileType color)
    {
        _colorBuf.Clear();
        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
                if (State.IsColor(x, y, color))
                    _colorBuf.Add((x, y));
    }

    private void HitPatchBotTarget(int x, int y, ref SimMoveStats stats)
    {
        if (!State.InBounds(x, y)) return;
        // Separate bots can hit the same multi-hit obstacle, just like reservations in
        // PatchBotTargetCoordinator. A tile already consumed cannot be selected again.
        int mark = _magnetHits.Count;
        if (Obstacles.CanTakeSpecialHit(x, y)) _visited.Remove((x, y));
        ClearForSpecial(x, y, ref stats);
        Obstacles.ApplyDeferredMagnetHits(_magnetHits, mark);
        Goals.SyncObstacles(Obstacles);
    }

    private bool ConsumeBonusPatchBot(int ax, int ay, int bx, int by, ref SimMoveStats stats)
    {
        int best = int.MaxValue, tx = -1, ty = -1;
        for (int x = 0; x < State.Width; x++)
            for (int y = 0; y < State.Height; y++)
            {
                if (!State.IsMatchable(x, y) || Obstacles.HasObstacleAt(x, y)) continue;
                int distance = System.Math.Abs(2 * x - ax - bx) + System.Math.Abs(2 * y - ay - by);
                if (distance >= best) continue;
                best = distance; tx = x; ty = y;
            }
        if (tx < 0) return false;
        Consume(tx, ty, ref stats);
        stats.SpecialsActivated++;
        return true;
    }

    /// <summary>
    /// Same target buckets as PatchBotTargetCoordinator: cargo path, goal obstacle,
    /// goal tile, other obstacle, other tile. Density ranks actual target cells within
    /// the highest-priority bucket; empty centres of a 5x5 square are never targets.
    /// </summary>
    public (int x, int y) BestPatchBotTarget(int fallbackX, int fallbackY)
    {
        var candidates = new List<(int x, int y)>();
        int bestPriority = -1;
        for (int x = 0; x < State.Width; x++)
            for (int y = 0; y < State.Height; y++)
            {
                int priority = -1;
                var id = Obstacles.ObstacleIdAt(x, y);
                var tile = State.Grid[x, y];
                if (Obstacles.CanTakeSpecialHit(x, y))
                    priority = Goals.NeedsObstacle(id) ? 4 : 2;
                else if (id == ObstacleId.None && tile != null && !State.Holes[x, y]
                    && !State.Phantom[x, y] && !_visited.Contains((x, y)))
                {
                    priority = Goals.NeedsTile(tile.Type) ? 3 : 1;
                    if (y > 0 && Obstacles.IsCargoAt(x, y - 1)
                        && Goals.NeedsObstacle(Obstacles.ObstacleIdAt(x, y - 1))) priority = 5;
                }
                if (priority < 0 || priority < bestPriority) continue;
                if (priority > bestPriority) { candidates.Clear(); bestPriority = priority; }
                candidates.Add((x, y));
            }
        if (candidates.Count == 0) return (-1, -1);

        int bestDensity = -1, ties = 0;
        var chosen = candidates[0];
        foreach (var candidate in candidates)
        {
            int density = 0;
            foreach (var other in candidates)
                if (System.Math.Abs(other.x - candidate.x) <= 2
                    && System.Math.Abs(other.y - candidate.y) <= 2) density++;
            if (density < bestDensity) continue;
            if (density > bestDensity) { bestDensity = density; ties = 0; }
            if (_rng.Next(++ties) == 0) chosen = candidate;
        }
        return chosen;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;

    private static TileType ColorOf(TileData t)
        => t == null ? TileType.Gear : (t.HasOverrideBaseType ? t.OverrideBaseType : t.Type);

    /// <summary>Tahtadaki special'ların toplam "gücü" (rank toplamı) — potansiyel ölçüsü.</summary>
    public int SpecialPotential()
    {
        int sum = 0;
        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
            {
                var t = State.Grid[x, y];
                if (t != null && t.Special != TileSpecial.None && !State.SpecialLocked[x, y])
                    sum += SpecialRank(t.Special);
            }
        return sum;
    }

    /// <summary>Tahtada duran (kullanılmayı bekleyen) special sayısı.</summary>
    public int CountSpecialsOnBoard()
    {
        int n = 0;
        for (int y = 0; y < State.Height; y++)
            for (int x = 0; x < State.Width; x++)
            {
                var t = State.Grid[x, y];
                if (t != null && t.Special != TileSpecial.None) n++;
            }
        return n;
    }
}
