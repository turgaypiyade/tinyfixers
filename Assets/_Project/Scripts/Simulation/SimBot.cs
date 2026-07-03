using System.Collections.Generic;

/// <summary>
/// Levelın hedeflerini bilen bağlam — bot skorlaması için.
/// </summary>
public sealed class SimGoalContext
{
    public readonly HashSet<TileType> GoalTiles = new();
    public readonly HashSet<ObstacleId> GoalObstacles = new();

    public SimGoalContext(LevelData level)
    {
        if (level?.goals == null) return;
        foreach (var g in level.goals)
        {
            if (g.targetType == LevelGoalTargetType.Tile) GoalTiles.Add(g.tileType);
            else if (g.targetType == LevelGoalTargetType.Obstacle) GoalObstacles.Add(g.obstacleId);
        }
    }
}

/// <summary>
/// "Ortalama oyuncu" hamle seçici. Rastgele yerine her geçerli takası ANLIK sonucuna göre puanlar
/// (match boyu, özel oluşturma, hedef taş, hedef-obstacle komşuluğu, elde özel kullanımı) ve en
/// iyisini seçer — ama insan gibi mistakeChance oranında rastgele oynar (kusurlu). Cascade
/// ileri-görüşü yok (hız için); bu, greedy-anlık + kusur = makul bir average-human modeli.
/// </summary>
public static class SimBot
{
    private static readonly HashSet<(int, int)> _matchCells = new();
    private static readonly HashSet<(int, int)> _impactCells = new();
    private static readonly List<float> _scores = new();
    private static readonly List<SimSwap> _best = new();

    public static SimSwap? PickMove(SimState s, System.Random rng, SimGoalContext goals, float mistakeChance)
    {
        var valid = SimMoves.FindValid(s);
        if (valid.Count == 0) return null;

        // İnsan kusuru: bazen düşünmeden oyna.
        if (rng.NextDouble() < mistakeChance)
            return valid[rng.Next(valid.Count)];

        _scores.Clear();
        float best = float.NegativeInfinity;
        for (int i = 0; i < valid.Count; i++)
        {
            float sc = ScoreSwap(s, valid[i], goals);
            _scores.Add(sc);
            if (sc > best) best = sc;
        }

        _best.Clear();
        for (int i = 0; i < valid.Count; i++)
            if (_scores[i] >= best - 0.01f) _best.Add(valid[i]);

        return _best[rng.Next(_best.Count)];
    }

    // ── Skorlama ──────────────────────────────────────────────────────────────

    // Oyuncu stratejisi: combo fırsatını kaçırma, yeni special üret, eldeki special'ı
    // obstacle'a dokunacak yerde kullan, normal match gerekiyorsa obstacle yakınında ve alttan yap.
    private static float ScoreSwap(SimState s, SimSwap sw, SimGoalContext g)
    {
        var beforeA = s.Grid[sw.AX, sw.AY];
        var beforeB = s.Grid[sw.BX, sw.BY];
        var obs = s.Obstacles as SimObstacleLayer;

        bool aSpec = beforeA != null && beforeA.Special != TileSpecial.None;
        bool bSpec = beforeB != null && beforeB.Special != TileSpecial.None;

        SimMoves.Apply(s, sw);   // geçici takas

        float score = 0f;

        if (aSpec || bSpec)
            score += ScoreSpecialActivation(s, sw, beforeA, beforeB, obs, g);

        _matchCells.Clear();
        CollectMatchCellsAt(s, sw.AX, sw.AY, _matchCells);
        CollectMatchCellsAt(s, sw.BX, sw.BY, _matchCells);

        if (_matchCells.Count > 0)
        {
            score += ScoreSpecialCreation(s, sw, obs, g);
            score += ScoreMatchCells(s, obs, g);
        }

        if (obs != null)
        {
            score += ObstacleProximity(obs, g, sw.AX, sw.AY, 3) * 1.5f;
            score += ObstacleProximity(obs, g, sw.BX, sw.BY, 3) * 1.5f;
        }

        score += BottomPreference(s, sw);

        SimMoves.Apply(s, sw);   // geri al
        return score;
    }

    private static float ScoreSpecialActivation(
        SimState s,
        SimSwap sw,
        TileData beforeA,
        TileData beforeB,
        SimObstacleLayer obs,
        SimGoalContext g)
    {
        bool aSpec = beforeA != null && beforeA.Special != TileSpecial.None;
        bool bSpec = beforeB != null && beforeB.Special != TileSpecial.None;

        _impactCells.Clear();

        if (aSpec && bSpec)
        {
            CollectComboImpact(
                s,
                sw.BX,
                sw.BY,
                beforeA.Special,
                ColorOf(beforeB),
                sw.AX,
                sw.AY,
                beforeB.Special,
                ColorOf(beforeA),
                _impactCells,
                obs,
                g);

            return 280f
                 + ComboValue(beforeA.Special, beforeB.Special)
                 + ScoreImpactCells(s, obs, g, _impactCells, 7f);
        }

        if (aSpec)
        {
            CollectSpecialImpact(s, sw.BX, sw.BY, beforeA.Special, ColorOf(beforeB), _impactCells, obs, g);
            return 80f
                 + SingleSpecialValue(beforeA.Special)
                 + ScoreImpactCells(s, obs, g, _impactCells, 5f);
        }

        CollectSpecialImpact(s, sw.AX, sw.AY, beforeB.Special, ColorOf(beforeA), _impactCells, obs, g);
        return 80f
             + SingleSpecialValue(beforeB.Special)
             + ScoreImpactCells(s, obs, g, _impactCells, 5f);
    }

    private static float ScoreSpecialCreation(SimState s, SimSwap sw, SimObstacleLayer obs, SimGoalContext g)
    {
        float best = 0f;
        bool? swapHorizontal = SwapHorizontal(sw);

        foreach (var (x, y) in _matchCells)
        {
            bool isSwapEnd = (x == sw.AX && y == sw.AY) || (x == sw.BX && y == sw.BY);
            var sp = DecideSpecialAt(s, x, y, isSwapEnd ? swapHorizontal : null);
            float value = SpecialCreateValue(sp);
            if (value <= 0f) continue;

            if (isSwapEnd) value += 25f; // canlı oyundaki gibi oyuncunun oynadığı taşı tercih et.
            if (obs != null)
                value += ObstacleProximity(obs, g, x, y, 3) * 4f;
            value += y * 0.5f;

            if (value > best) best = value;
        }

        return best;
    }

    private static float ScoreMatchCells(SimState s, SimObstacleLayer obs, SimGoalContext g)
    {
        float score = _matchCells.Count * 2f;

        foreach (var (x, y) in _matchCells)
        {
            var tile = s.Grid[x, y];
            if (tile != null && g.GoalTiles.Contains(tile.Type)) score += 7f;

            if (obs != null)
            {
                score += ObstacleNeighbourhood(obs, g, x, y) * 8f;
                score += ObstacleProximity(obs, g, x, y, 3) * 1.25f;
            }

            score += y * 0.45f;
        }

        return score;
    }

    private static float ScoreImpactCells(
        SimState s,
        SimObstacleLayer obs,
        SimGoalContext g,
        HashSet<(int, int)> cells,
        float obstacleWeight)
    {
        float score = 0f;

        foreach (var (x, y) in cells)
        {
            if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) continue;

            var tile = s.Grid[x, y];
            if (tile != null && tile.Special == TileSpecial.None)
            {
                score += 1f;
                if (g.GoalTiles.Contains(tile.Type)) score += 5f;
            }

            if (obs != null)
            {
                score += ObstacleNeighbourhood(obs, g, x, y) * obstacleWeight;
                score += ObstacleProximity(obs, g, x, y, 2) * 0.8f;
            }
        }

        return score;
    }

    private static float BottomPreference(SimState s, SimSwap sw)
    {
        int maxY = sw.AY > sw.BY ? sw.AY : sw.BY;
        foreach (var (_, y) in _matchCells)
            if (y > maxY) maxY = y;

        if (s.Height <= 1) return 0f;
        return ((float)maxY / (s.Height - 1)) * 10f;
    }

    private static float SpecialCreateValue(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.SystemOverride: return 230f;
            case TileSpecial.PulseCore:      return 175f;
            case TileSpecial.PatchBot:       return 145f;
            case TileSpecial.LineH:
            case TileSpecial.LineV:          return 115f;
            default:                         return 0f;
        }
    }

    private static float SingleSpecialValue(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.SystemOverride: return 95f;
            case TileSpecial.PatchBot:       return 80f;
            case TileSpecial.PulseCore:      return 75f;
            case TileSpecial.LineH:
            case TileSpecial.LineV:          return 55f;
            default:                         return 0f;
        }
    }

    private static float ComboValue(TileSpecial a, TileSpecial b)
    {
        if (a == TileSpecial.SystemOverride && b == TileSpecial.SystemOverride) return 190f;
        if (a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride) return 150f;
        if (a == TileSpecial.PulseCore && b == TileSpecial.PulseCore) return 135f;
        if ((a == TileSpecial.PulseCore && b == TileSpecial.PatchBot) ||
            (b == TileSpecial.PulseCore && a == TileSpecial.PatchBot)) return 130f;
        if ((IsLine(a) && b == TileSpecial.PulseCore) || (IsLine(b) && a == TileSpecial.PulseCore)) return 120f;
        if (a == TileSpecial.PatchBot && b == TileSpecial.PatchBot) return 115f;
        if ((IsLine(a) && b == TileSpecial.PatchBot) || (IsLine(b) && a == TileSpecial.PatchBot)) return 105f;
        if (IsLine(a) && IsLine(b)) return 95f;
        return 80f;
    }

    private static void CollectComboImpact(
        SimState s,
        int ax,
        int ay,
        TileSpecial a,
        TileType aTargetColor,
        int bx,
        int by,
        TileSpecial b,
        TileType bTargetColor,
        HashSet<(int, int)> into,
        SimObstacleLayer obs,
        SimGoalContext goals)
    {
        if (a == TileSpecial.SystemOverride && b == TileSpecial.SystemOverride)
        {
            AddBoard(s, into);
            return;
        }

        if (a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride)
        {
            var other = a == TileSpecial.SystemOverride ? b : a;
            var color = a == TileSpecial.SystemOverride ? aTargetColor : bTargetColor;
            CollectOverrideComboImpact(s, other, color, into, obs, goals);
            return;
        }

        if (a == TileSpecial.PulseCore && b == TileSpecial.PulseCore)
        {
            AddSquare(s, ax, ay, 4, into);
            return;
        }

        if ((IsLine(a) && b == TileSpecial.PulseCore) || (IsLine(b) && a == TileSpecial.PulseCore))
        {
            int cx = IsLine(a) ? ax : bx;
            int cy = IsLine(a) ? ay : by;
            AddRows(s, cy, 1, into);
            AddColumns(s, cx, 1, into);
            return;
        }

        if ((a == TileSpecial.PatchBot && b == TileSpecial.PatchBot) ||
            (a == TileSpecial.PatchBot && b == TileSpecial.PulseCore) ||
            (b == TileSpecial.PatchBot && a == TileSpecial.PulseCore))
        {
            var target = BestPatchBotTarget(s, obs, goals, ax, ay);
            AddSquare(s, target.x, target.y, 2, into);
            if (a == TileSpecial.PatchBot && b == TileSpecial.PatchBot)
                AddSquare(s, target.x, target.y, 3, into);
            return;
        }

        if ((a == TileSpecial.PatchBot && IsLine(b)) || (b == TileSpecial.PatchBot && IsLine(a)))
        {
            var line = IsLine(a) ? a : b;
            var target = BestPatchBotTarget(s, obs, goals, ax, ay);
            AddSquare(s, target.x, target.y, 2, into);
            if (line == TileSpecial.LineH) AddRow(s, target.y, into);
            else AddColumn(s, target.x, into);
            return;
        }

        CollectSpecialImpact(s, ax, ay, a, aTargetColor, into, obs, goals);
        CollectSpecialImpact(s, bx, by, b, bTargetColor, into, obs, goals);
    }

    private static void CollectOverrideComboImpact(
        SimState s,
        TileSpecial convertedSpecial,
        TileType color,
        HashSet<(int, int)> into,
        SimObstacleLayer obs,
        SimGoalContext goals)
    {
        if (convertedSpecial == TileSpecial.PulseCore)
        {
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (IsNormalColor(s, x, y, color))
                        AddSquare(s, x, y, 2, into);
            return;
        }

        if (convertedSpecial == TileSpecial.PatchBot)
        {
            var target = BestPatchBotTarget(s, obs, goals, 0, s.Height - 1);
            AddSquare(s, target.x, target.y, 3, into);
            return;
        }

        if (IsLine(convertedSpecial))
        {
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (IsNormalColor(s, x, y, color))
                    {
                        if (convertedSpecial == TileSpecial.LineH) AddRow(s, y, into);
                        else AddColumn(s, x, into);
                    }
            return;
        }

        CollectColor(s, color, into);
    }

    private static void CollectSpecialImpact(
        SimState s,
        int x,
        int y,
        TileSpecial special,
        TileType targetColor,
        HashSet<(int, int)> into,
        SimObstacleLayer obs,
        SimGoalContext goals)
    {
        switch (special)
        {
            case TileSpecial.LineH:
                AddRow(s, y, into);
                break;
            case TileSpecial.LineV:
                AddColumn(s, x, into);
                break;
            case TileSpecial.PatchBot:
            {
                var target = BestPatchBotTarget(s, obs, goals, x, y);
                AddSquare(s, target.x, target.y, 2, into);
                break;
            }
            case TileSpecial.PulseCore:
                AddSquare(s, x, y, 2, into);
                break;
            case TileSpecial.SystemOverride:
                CollectColor(s, targetColor, into);
                break;
        }
    }

    private static (int x, int y) BestPatchBotTarget(
        SimState s,
        SimObstacleLayer obs,
        SimGoalContext goals,
        int fallbackX,
        int fallbackY)
    {
        if (obs == null) return (fallbackX, fallbackY);

        float bestScore = 0f;
        int bestX = fallbackX;
        int bestY = fallbackY;

        for (int y = 0; y < s.Height; y++)
        {
            for (int x = 0; x < s.Width; x++)
            {
                float score = 0f;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        score += ObstacleCellValue(obs, goals, x + dx, y + dy);

                score += ObstacleProximity(obs, goals, x, y, 3) * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        return (bestX, bestY);
    }

    private static void CollectMatchCellsAt(SimState s, int x, int y, HashSet<(int, int)> into)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return;
        if (s.Holes[x, y]) return;
        var tile = s.Grid[x, y];
        if (tile == null || tile.Special != TileSpecial.None) return;
        var t = tile.Type;

        int lx = x; while (lx - 1 >= 0 && !s.Holes[lx - 1, y] && Same(s, lx - 1, y, t)) lx--;
        int rx = x; while (rx + 1 < s.Width && !s.Holes[rx + 1, y] && Same(s, rx + 1, y, t)) rx++;
        if (rx - lx + 1 >= 3) for (int i = lx; i <= rx; i++) into.Add((i, y));

        int uy = y; while (uy - 1 >= 0 && !s.Holes[x, uy - 1] && Same(s, x, uy - 1, t)) uy--;
        int dy = y; while (dy + 1 < s.Height && !s.Holes[x, dy + 1] && Same(s, x, dy + 1, t)) dy++;
        if (dy - uy + 1 >= 3) for (int i = uy; i <= dy; i++) into.Add((x, i));

        Add2x2MatchesAt(s, x, y, into);
    }

    private static void Add2x2MatchesAt(SimState s, int x, int y, HashSet<(int, int)> into)
    {
        var tile = s.Grid[x, y];
        if (tile == null || tile.Special != TileSpecial.None) return;
        var t = tile.Type;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = x + ox;
                int sy = y + oy;
                if (!Is2x2Match(s, sx, sy, t)) continue;
                into.Add((sx, sy));
                into.Add((sx + 1, sy));
                into.Add((sx, sy + 1));
                into.Add((sx + 1, sy + 1));
            }
        }
    }

    private static bool Is2x2Match(SimState s, int sx, int sy, TileType t)
    {
        if (sx < 0 || sy < 0 || sx >= s.Width - 1 || sy >= s.Height - 1) return false;
        if (s.Holes[sx, sy] || s.Holes[sx + 1, sy] || s.Holes[sx, sy + 1] || s.Holes[sx + 1, sy + 1]) return false;
        if (IsHigherPriorityRunAt(s, sx, sy) || IsHigherPriorityRunAt(s, sx + 1, sy) ||
            IsHigherPriorityRunAt(s, sx, sy + 1) || IsHigherPriorityRunAt(s, sx + 1, sy + 1)) return false;

        return Same(s, sx, sy, t)
            && Same(s, sx + 1, sy, t)
            && Same(s, sx, sy + 1, t)
            && Same(s, sx + 1, sy + 1, t);
    }

    private static TileSpecial DecideSpecialAt(SimState s, int x, int y, bool? swapHorizontal)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height || s.Holes[x, y]) return TileSpecial.None;
        var tile = s.Grid[x, y];
        if (tile == null || tile.Special != TileSpecial.None) return TileSpecial.None;
        var t = tile.Type;

        int h = RunLength(s, x, y, t, horizontal: true);
        int v = RunLength(s, x, y, t, horizontal: false);
        int best = h >= v ? h : v;

        if (best >= 5) return TileSpecial.SystemOverride;
        if (h >= 3 && v >= 3) return TileSpecial.PulseCore;
        if (best == 4)
        {
            if (swapHorizontal.HasValue)
                return swapHorizontal.Value ? TileSpecial.LineH : TileSpecial.LineV;
            return h >= v ? TileSpecial.LineH : TileSpecial.LineV;
        }
        if (Has2x2At(s, x, y, t)) return TileSpecial.PatchBot;
        return TileSpecial.None;
    }

    private static bool Has2x2At(SimState s, int x, int y, TileType t)
    {
        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
                if (Is2x2Match(s, x + ox, y + oy, t))
                    return true;
        return false;
    }

    private static int RunLength(SimState s, int x, int y, TileType t, bool horizontal)
    {
        int count = 1;
        if (horizontal)
        {
            for (int lx = x - 1; lx >= 0 && !s.Holes[lx, y] && Same(s, lx, y, t); lx--) count++;
            for (int rx = x + 1; rx < s.Width && !s.Holes[rx, y] && Same(s, rx, y, t); rx++) count++;
        }
        else
        {
            for (int uy = y - 1; uy >= 0 && !s.Holes[x, uy] && Same(s, x, uy, t); uy--) count++;
            for (int dy = y + 1; dy < s.Height && !s.Holes[x, dy] && Same(s, x, dy, t); dy++) count++;
        }
        return count;
    }

    private static bool IsHigherPriorityRunAt(SimState s, int x, int y)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height || s.Holes[x, y]) return false;
        var tile = s.Grid[x, y];
        if (tile == null || tile.Special != TileSpecial.None) return false;
        var t = tile.Type;

        int h = RunLength(s, x, y, t, horizontal: true);
        int v = RunLength(s, x, y, t, horizontal: false);
        return h >= 4 || v >= 4 || (h >= 3 && v >= 3);
    }

    private static bool? SwapHorizontal(SimSwap sw)
    {
        if (sw.AY == sw.BY && sw.AX != sw.BX) return true;
        if (sw.AX == sw.BX && sw.AY != sw.BY) return false;
        return null;
    }

    private static void AddRow(SimState s, int y, HashSet<(int, int)> into)
    {
        if (y < 0 || y >= s.Height) return;
        for (int x = 0; x < s.Width; x++) into.Add((x, y));
    }

    private static void AddColumn(SimState s, int x, HashSet<(int, int)> into)
    {
        if (x < 0 || x >= s.Width) return;
        for (int y = 0; y < s.Height; y++) into.Add((x, y));
    }

    private static void AddRows(SimState s, int centerY, int radius, HashSet<(int, int)> into)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++) AddRow(s, y, into);
    }

    private static void AddColumns(SimState s, int centerX, int radius, HashSet<(int, int)> into)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++) AddColumn(s, x, into);
    }

    private static void AddSquare(SimState s, int cx, int cy, int radius, HashSet<(int, int)> into)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if (x >= 0 && y >= 0 && x < s.Width && y < s.Height)
                    into.Add((x, y));
    }

    private static void AddBoard(SimState s, HashSet<(int, int)> into)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                into.Add((x, y));
    }

    private static void CollectColor(SimState s, TileType color, HashSet<(int, int)> into)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                if (IsNormalColor(s, x, y, color))
                    into.Add((x, y));
    }

    private static bool IsNormalColor(SimState s, int x, int y, TileType color)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height || s.Holes[x, y]) return false;
        var tile = s.Grid[x, y];
        return tile != null && tile.Special == TileSpecial.None && tile.Type == color;
    }

    private static float ObstacleNeighbourhood(SimObstacleLayer obs, SimGoalContext g, int x, int y)
    {
        return ObstScore(obs, g, x, y)
             + ObstScore(obs, g, x - 1, y)
             + ObstScore(obs, g, x + 1, y)
             + ObstScore(obs, g, x, y - 1)
             + ObstScore(obs, g, x, y + 1);
    }

    private static float ObstScore(SimObstacleLayer obs, SimGoalContext g, int x, int y)
    {
        var id = obs.ObstacleIdAt(x, y);
        if (id == ObstacleId.None) return 0f;
        return g.GoalObstacles.Contains(id) ? 5f : 0.5f;   // hedef obstacle güçlü, diğerleri hafif
    }

    private static float ObstacleProximity(SimObstacleLayer obs, SimGoalContext g, int x, int y, int radius)
    {
        float score = 0f;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int dist = Abs(dx) + Abs(dy);
                if (dist > radius) continue;
                float value = ObstacleCellValue(obs, g, x + dx, y + dy);
                if (value <= 0f) continue;
                score += value / (dist + 1);
            }
        }

        return score;
    }

    private static float ObstacleCellValue(SimObstacleLayer obs, SimGoalContext g, int x, int y)
    {
        var id = obs.ObstacleIdAt(x, y);
        if (id == ObstacleId.None) return 0f;
        return g.GoalObstacles.Contains(id) ? 10f : 1.5f;
    }

    private static int Abs(int v)
    {
        return v < 0 ? -v : v;
    }

    private static bool Same(SimState s, int x, int y, TileType t)
    {
        var d = s.Grid[x, y];
        return d != null && d.Special == TileSpecial.None && d.Type == t;
    }

    private static TileType ColorOf(TileData t)
        => t != null && t.HasOverrideBaseType ? t.OverrideBaseType : (t != null ? t.Type : TileType.Gear);

    private static bool IsLine(TileSpecial special)
        => special == TileSpecial.LineH || special == TileSpecial.LineV;
}
