using System.Collections.Generic;
using System.Text;

// Drives N headless smart-bot games on a LevelData and reports aggregate stats.
// Smart bot = average player model: scores valid swaps with human-like heuristics.
public static class SimRunner
{
    // ── Result types ─────────────────────────────────────────────────────────

    public struct GameResult
    {
        public int MovesUsed;
        public bool Won;
        public int TilesCleared;
        public int SpecialsFormed;
        public int CascadeSteps;
        public int MaxCascadeChain;
        public int DeadlockMoves;
        // Tile clears by type — for goal tracking
        public int GearsCleared, CoresCleared, BoltsCleared, PlatesCleared;
        // Diagnostic: how many obstacle origins were cleared this game
        public int ObstacleClears;
        // Diagnostic: how many obstacle origins existed at game start
        public int ObstacleCount;
    }

    public struct RunStats
    {
        public int GameCount;
        public int GamesWon;
        public float WinRate;
        public float AvgMovesPerGame;
        public float AvgMovesOnWin;       // avg moves used in won games
        public float AvgMovesOnLoss;      // avg moves used in lost games
        public float AvgTilesClearedPerGame;
        public float AvgSpecialsPerGame;
        public float AvgCascadeStepsPerGame;
        public int MaxCascadeChainSeen;
        public int TotalDeadlockMoves;
        // Diagnostic counters
        public float AvgObstacleClearsPerGame;
        public int ObstacleCountInLevel;
    }

    // ── Main entry ────────────────────────────────────────────────────────────

    // mistakeChance: bot'un "insan kusuru" — 0 = kusursuz greedy, 0.2 = %20 rastgele hamle.
    public static RunStats Run(LevelData level, int gameCount, int seed = 42, float mistakeChance = 0.2f)
    {
        var stats = new RunStats { GameCount = gameCount };
        var rng = new System.Random(seed);
        var goals = new SimGoalContext(level);

        int totalMovesWin = 0, totalMovesLoss = 0;

        for (int g = 0; g < gameCount; g++)
        {
            var r = PlayOneGame(level, rng, goals, mistakeChance);

            if (r.Won)
            {
                stats.GamesWon++;
                totalMovesWin += r.MovesUsed;
            }
            else
            {
                totalMovesLoss += r.MovesUsed;
            }

            stats.AvgTilesClearedPerGame    += r.TilesCleared;
            stats.AvgSpecialsPerGame        += r.SpecialsFormed;
            stats.AvgCascadeStepsPerGame    += r.CascadeSteps;
            stats.TotalDeadlockMoves        += r.DeadlockMoves;
            stats.AvgObstacleClearsPerGame  += r.ObstacleClears;
            if (g == 0) stats.ObstacleCountInLevel = r.ObstacleCount;

            if (r.MaxCascadeChain > stats.MaxCascadeChainSeen)
                stats.MaxCascadeChainSeen = r.MaxCascadeChain;
        }

        if (gameCount > 0)
        {
            stats.WinRate                   = (float)stats.GamesWon / gameCount;
            stats.AvgMovesPerGame           = (float)(totalMovesWin + totalMovesLoss) / gameCount;
            stats.AvgTilesClearedPerGame    /= gameCount;
            stats.AvgSpecialsPerGame        /= gameCount;
            stats.AvgCascadeStepsPerGame    /= gameCount;
            stats.AvgObstacleClearsPerGame  /= gameCount;
        }

        int lostGames = gameCount - stats.GamesWon;
        stats.AvgMovesOnWin  = stats.GamesWon > 0 ? (float)totalMovesWin  / stats.GamesWon  : 0;
        stats.AvgMovesOnLoss = lostGames      > 0 ? (float)totalMovesLoss / lostGames       : 0;

        return stats;
    }

    // ── Single game ───────────────────────────────────────────────────────────

    private static GameResult PlayOneGame(LevelData level, System.Random rng, SimGoalContext goals, float mistakeChance)
    {
        var obs    = new SimObstacleLayer(level);
        var state  = SimState.RandomFill(level, rng, obs);
        var result = new GameResult { ObstacleCount = obs.GetTotalObstacleCount() };
        var finder = new SimMatchFinder(state);
        var goalTracker = new GoalTracker(level, obs);

        // Initial cascade
        RunCascade(state, rng, finder, ref result, goalTracker);

        int movesLeft = level.moves;

        while (movesLeft > 0)
        {
            if (goalTracker.AllMet) { result.Won = true; break; }

            var swap = SimBot.PickMove(state, rng, goals, mistakeChance);
            if (swap == null) { result.DeadlockMoves++; break; }

            SimMoves.Apply(state, swap.Value);
            result.MovesUsed++;
            movesLeft--;

            // Takas bir special içeriyorsa aktive et (footprint temizler), sonra cascade.
            ActivateSwapSpecials(state, rng, finder, goalTracker, ref result, swap.Value);
            RunCascade(state, rng, finder, ref result, goalTracker, swap.Value);
        }

        if (!result.Won && goalTracker.AllMet)
            result.Won = true;

        result.ObstacleClears = obs.GetTotalClearedCount();

        return result;
    }

    // ── Cascade loop ─────────────────────────────────────────────────────────

    private static void RunCascade(
        SimState state, System.Random rng, SimMatchFinder finder,
        ref GameResult result, GoalTracker goals,
        SimSwap? playerSwap = null)
    {
        int chainLen = 0;
        var obs = state.Obstacles as SimObstacleLayer;
        bool preferSwapTiles = playerSwap.HasValue;

        while (true)
        {
            var matches = finder.FindAllMatches();
            if (matches.Count == 0) break;

            chainLen++;
            result.CascadeSteps++;

            // Bir special oluşacaksa pivot hücreyi TEMİZLEME — oraya special'ı YERLEŞTİR
            // (adım başına en fazla 1). Special tahtada kalır, sonra takasla aktive edilir.
            int spx, spy; TileSpecial spType;
            PickSpecialCreation(finder, matches, preferSwapTiles ? playerSwap : null, out spx, out spy, out spType);
            preferSwapTiles = false;

            if (spType != TileSpecial.None) result.SpecialsFormed++;

            foreach (var td in matches)
            {
                if (td.X == spx && td.Y == spy)
                {
                    state.Grid[td.X, td.Y]?.SetSpecial(spType);
                    continue;
                }

                goals.RecordTile(td.Type);
                CountTile(ref result, td.Type);
                obs?.ProcessMatchClear(td.X, td.Y, td.Type);
                state.Grid[td.X, td.Y] = null;
                result.TilesCleared++;
            }

            goals.SyncObstacleCounts();

            // Rebuild holes from current obstacle state so cleared cells stop being holes.
            // This allows MovableObstacle gravity to fall through recently-cleared cells
            // and allows chest1-cleared cells to receive tiles.
            obs?.SyncHoles(state);

            // MovableObstacle gravity before tile gravity so vacated cells get refilled.
            obs?.ApplyGravity(state);

            SimCascade.ApplyGravityAndRefill(state, rng);
            finder.InvalidateRunCache();
        }

        if (chainLen > result.MaxCascadeChain)
            result.MaxCascadeChain = chainLen;
    }

    private static void PickSpecialCreation(
        SimMatchFinder finder,
        HashSet<TileData> matches,
        SimSwap? playerSwap,
        out int spx,
        out int spy,
        out TileSpecial spType)
    {
        spx = -1;
        spy = -1;
        spType = TileSpecial.None;

        if (matches == null || matches.Count == 0)
            return;

        bool? swapHorizontal = null;
        if (playerSwap.HasValue)
        {
            var sw = playerSwap.Value;
            if (sw.AY == sw.BY && sw.AX != sw.BX) swapHorizontal = true;
            else if (sw.AX == sw.BX && sw.AY != sw.BY) swapHorizontal = false;

            foreach (var td in matches)
            {
                if (td == null || td.Special != TileSpecial.None) continue;
                bool isSwapEnd = (td.X == sw.AX && td.Y == sw.AY) || (td.X == sw.BX && td.Y == sw.BY);
                if (!isSwapEnd) continue;

                var candidate = finder.DecideSpecialAt(td.X, td.Y, swapHorizontal);
                if (SpecialCreationScore(candidate) <= SpecialCreationScore(spType)) continue;

                spx = td.X;
                spy = td.Y;
                spType = candidate;
            }

            if (spType != TileSpecial.None)
                return;
        }

        foreach (var td in matches)
        {
            if (td == null || td.Special != TileSpecial.None) continue;
            var candidate = finder.DecideSpecialAt(td.X, td.Y);
            if (SpecialCreationScore(candidate) <= SpecialCreationScore(spType)) continue;

            spx = td.X;
            spy = td.Y;
            spType = candidate;
        }
    }

    private static int SpecialCreationScore(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.SystemOverride: return 5;
            case TileSpecial.PulseCore:      return 4;
            case TileSpecial.PatchBot:       return 3;
            case TileSpecial.LineH:
            case TileSpecial.LineV:          return 2;
            default:                         return 0;
        }
    }

    // ── Goal tracker ─────────────────────────────────────────────────────────

    private sealed class GoalTracker
    {
        private readonly int[] _tileNeeded  = new int[4]; // Gear/Core/Bolt/Plate
        private readonly int[] _tileCleared = new int[4];
        private bool _hasTileGoals;

        // Obstacle goals: obstacleId(int) → needed count
        private readonly Dictionary<int, int> _obsNeeded  = new();
        private readonly Dictionary<int, int> _obsCleared = new();
        private bool _hasObstacleGoals;

        private readonly SimObstacleLayer _obs;

        public GoalTracker(LevelData level, SimObstacleLayer obs)
        {
            _obs = obs;
            if (level.goals == null) return;

            foreach (var g in level.goals)
            {
                if (g.targetType == LevelGoalTargetType.Tile)
                {
                    int idx = TileTypeIndex(g.tileType);
                    if (idx < 0) continue;
                    _tileNeeded[idx] += g.amount;
                    _hasTileGoals = true;
                }
                else if (g.targetType == LevelGoalTargetType.Obstacle && obs != null)
                {
                    int id = (int)g.obstacleId;
                    _obsNeeded.TryGetValue(id, out int prev);
                    _obsNeeded[id] = prev + g.amount;
                    _obsCleared[id] = 0;
                    _hasObstacleGoals = true;
                }
            }
        }

        public void RecordTile(TileType t)
        {
            int idx = TileTypeIndex(t);
            if (idx >= 0) _tileCleared[idx]++;
        }

        // Pull latest cleared counts from SimObstacleLayer after each cascade step.
        public void SyncObstacleCounts()
        {
            if (_obs == null || !_hasObstacleGoals) return;
            foreach (var id in _obsNeeded.Keys)
                _obsCleared[id] = _obs.GetClearedCount((ObstacleId)id);
        }

        public bool AllMet
        {
            get
            {
                if (!_hasTileGoals && !_hasObstacleGoals) return false;

                if (_hasTileGoals)
                    for (int i = 0; i < 4; i++)
                        if (_tileCleared[i] < _tileNeeded[i]) return false;

                if (_hasObstacleGoals)
                    foreach (var kv in _obsNeeded)
                        if (_obsCleared.GetValueOrDefault(kv.Key, 0) < kv.Value)
                            return false;

                return true;
            }
        }

        private static int TileTypeIndex(TileType t) => t switch
        {
            TileType.Gear  => 0,
            TileType.Core  => 1,
            TileType.Bolt  => 2,
            TileType.Plate => 3,
            _              => -1
        };
    }

    private static void CountTile(ref GameResult r, TileType t)
    {
        switch (t)
        {
            case TileType.Gear:  r.GearsCleared++;  break;
            case TileType.Core:  r.CoresCleared++;  break;
            case TileType.Bolt:  r.BoltsCleared++;  break;
            case TileType.Plate: r.PlatesCleared++; break;
        }
    }

    // ── Special aktivasyonu (yaklaşık footprint) ─────────────────────────────

    // Takas bir/iki special içeriyorsa aktive eder; hücreleri temizler + yerçekimi/doldurma yapar.
    private static void ActivateSwapSpecials(
        SimState s, System.Random rng, SimMatchFinder finder,
        GoalTracker goals, ref GameResult result, SimSwap swap)
    {
        var ta = s.Grid[swap.AX, swap.AY];
        var tb = s.Grid[swap.BX, swap.BY];
        bool sa = ta != null && ta.Special != TileSpecial.None;
        bool sb = tb != null && tb.Special != TileSpecial.None;
        if (!sa && !sb) return;

        var obs = s.Obstacles as SimObstacleLayer;
        var visited = new HashSet<(int, int)>();

        if (sa && sb)
        {
            TriggerSpecialCombo(
                s, obs, goals, ref result,
                swap.AX, swap.AY, ta.Special, ColorOf(tb),
                swap.BX, swap.BY, tb.Special, ColorOf(ta),
                visited);
        }
        else
        {
            if (sa) TriggerSpecial(s, obs, goals, ref result, swap.AX, swap.AY, ta.Special, ColorOf(tb ?? ta), visited);
            if (sb) TriggerSpecial(s, obs, goals, ref result, swap.BX, swap.BY, tb.Special, ColorOf(ta ?? tb), visited);
        }

        goals.SyncObstacleCounts();

        // Aktivasyon boşluk açtı → yerçekimi + doldur (yoksa RunCascade tetiklenmez).
        obs?.SyncHoles(s);
        obs?.ApplyGravity(s);
        SimCascade.ApplyGravityAndRefill(s, rng);
        finder.InvalidateRunCache();
    }

    private static void TriggerSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int x, int y, TileSpecial sp, TileType targetColor, HashSet<(int, int)> visited)
    {
        if (!visited.Add((x, y))) return;
        if (s.Grid[x, y] != null) { s.Grid[x, y] = null; result.TilesCleared++; }   // special'ı tüket

        switch (sp)
        {
            case TileSpecial.LineH:
                for (int i = 0; i < s.Width; i++) ClearForSpecial(s, obs, goals, ref result, i, y, visited);
                break;
            case TileSpecial.LineV:
                for (int i = 0; i < s.Height; i++) ClearForSpecial(s, obs, goals, ref result, x, i, visited);
                break;
            case TileSpecial.PatchBot:   // PatchBot gerçek oyunda obstacle hedefleyip 5x5 patlar.
            {
                var target = BestPatchBotTarget(s, obs, x, y);
                ClearSquareForSpecial(s, obs, goals, ref result, target.x, target.y, 2, visited);
                break;
            }
            case TileSpecial.PulseCore:  // L/T/5-küme ≈ geniş patlama 5x5
                ClearSquareForSpecial(s, obs, goals, ref result, x, y, 2, visited);
                break;
            case TileSpecial.SystemOverride:  // 5 düz ≈ renk bombası (hedef rengin hepsi)
                for (int yy = 0; yy < s.Height; yy++)
                    for (int xx = 0; xx < s.Width; xx++)
                    {
                        var t = s.Grid[xx, yy];
                        if (t != null && t.Special == TileSpecial.None && t.Type == targetColor)
                            ClearForSpecial(s, obs, goals, ref result, xx, yy, visited);
                    }
                break;
        }
    }

    private static void TriggerSpecialCombo(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int ax, int ay, TileSpecial a, TileType aTargetColor,
        int bx, int by, TileSpecial b, TileType bTargetColor,
        HashSet<(int, int)> visited)
    {
        ConsumeSpecial(s, ref result, ax, ay, visited);
        ConsumeSpecial(s, ref result, bx, by, visited);

        if (a == TileSpecial.SystemOverride && b == TileSpecial.SystemOverride)
        {
            ClearBoardForSpecial(s, obs, goals, ref result, visited);
            return;
        }

        if (a == TileSpecial.SystemOverride || b == TileSpecial.SystemOverride)
        {
            var converted = a == TileSpecial.SystemOverride ? b : a;
            var color = a == TileSpecial.SystemOverride ? aTargetColor : bTargetColor;
            TriggerOverrideCombo(s, obs, goals, ref result, converted, color, visited);
            return;
        }

        if (a == TileSpecial.PulseCore && b == TileSpecial.PulseCore)
        {
            ClearSquareForSpecial(s, obs, goals, ref result, ax, ay, 4, visited);
            return;
        }

        if ((IsLine(a) && b == TileSpecial.PulseCore) || (IsLine(b) && a == TileSpecial.PulseCore))
        {
            int cx = IsLine(a) ? ax : bx;
            int cy = IsLine(a) ? ay : by;
            ClearRowsForSpecial(s, obs, goals, ref result, cy, 1, visited);
            ClearColumnsForSpecial(s, obs, goals, ref result, cx, 1, visited);
            return;
        }

        if ((a == TileSpecial.PatchBot && b == TileSpecial.PatchBot) ||
            (a == TileSpecial.PatchBot && b == TileSpecial.PulseCore) ||
            (b == TileSpecial.PatchBot && a == TileSpecial.PulseCore))
        {
            var target = BestPatchBotTarget(s, obs, ax, ay);
            ClearSquareForSpecial(s, obs, goals, ref result, target.x, target.y, a == TileSpecial.PatchBot && b == TileSpecial.PatchBot ? 3 : 2, visited);
            return;
        }

        if ((a == TileSpecial.PatchBot && IsLine(b)) || (b == TileSpecial.PatchBot && IsLine(a)))
        {
            var line = IsLine(a) ? a : b;
            var target = BestPatchBotTarget(s, obs, ax, ay);
            ClearSquareForSpecial(s, obs, goals, ref result, target.x, target.y, 2, visited);
            if (line == TileSpecial.LineH) ClearRowForSpecial(s, obs, goals, ref result, target.y, visited);
            else ClearColumnForSpecial(s, obs, goals, ref result, target.x, visited);
            return;
        }

        if (IsLine(a) && IsLine(b))
        {
            ClearRowForSpecial(s, obs, goals, ref result, ay, visited);
            ClearColumnForSpecial(s, obs, goals, ref result, ax, visited);
            return;
        }

        TriggerSpecial(s, obs, goals, ref result, ax, ay, a, aTargetColor, visited);
        TriggerSpecial(s, obs, goals, ref result, bx, by, b, bTargetColor, visited);
    }

    private static void TriggerOverrideCombo(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        TileSpecial converted, TileType color, HashSet<(int, int)> visited)
    {
        if (converted == TileSpecial.PulseCore)
        {
            var cells = CollectColorCells(s, color);
            foreach (var (x, y) in cells)
                ClearSquareForSpecial(s, obs, goals, ref result, x, y, 2, visited);
            return;
        }

        if (converted == TileSpecial.PatchBot)
        {
            ClearColorForSpecial(s, obs, goals, ref result, color, visited);
            var target = BestPatchBotTarget(s, obs, 0, s.Height - 1);
            ClearSquareForSpecial(s, obs, goals, ref result, target.x, target.y, 3, visited);
            return;
        }

        if (converted == TileSpecial.LineH || converted == TileSpecial.LineV)
        {
            var cells = CollectColorCells(s, color);
            foreach (var (x, y) in cells)
            {
                if (converted == TileSpecial.LineH) ClearRowForSpecial(s, obs, goals, ref result, y, visited);
                else ClearColumnForSpecial(s, obs, goals, ref result, x, visited);
            }
            return;
        }

        ClearColorForSpecial(s, obs, goals, ref result, color, visited);
    }

    private static void ClearForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int x, int y, HashSet<(int, int)> visited)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return;
        if (s.Holes[x, y]) return;
        var t = s.Grid[x, y];
        if (t == null) return;

        if (t.Special != TileSpecial.None)   // footprint başka special'a değdi → zincirle
        {
            TriggerSpecial(s, obs, goals, ref result, x, y, t.Special, ColorOf(t), visited);
            return;
        }

        goals.RecordTile(t.Type);
        CountTile(ref result, t.Type);
        obs?.ProcessMatchClear(x, y, t.Type);
        s.Grid[x, y] = null;
        result.TilesCleared++;
    }

    private static void ConsumeSpecial(SimState s, ref GameResult result, int x, int y, HashSet<(int, int)> visited)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return;
        visited.Add((x, y));
        if (s.Grid[x, y] == null) return;
        s.Grid[x, y] = null;
        result.TilesCleared++;
    }

    private static void ClearBoardForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        HashSet<(int, int)> visited)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                ClearForSpecial(s, obs, goals, ref result, x, y, visited);
    }

    private static void ClearColorForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        TileType color, HashSet<(int, int)> visited)
    {
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                if (IsNormalColor(s, x, y, color))
                    ClearForSpecial(s, obs, goals, ref result, x, y, visited);
    }

    private static void ClearSquareForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int cx, int cy, int radius, HashSet<(int, int)> visited)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                ClearForSpecial(s, obs, goals, ref result, cx + dx, cy + dy, visited);
    }

    private static void ClearRowsForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int centerY, int radius, HashSet<(int, int)> visited)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
            ClearRowForSpecial(s, obs, goals, ref result, y, visited);
    }

    private static void ClearColumnsForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int centerX, int radius, HashSet<(int, int)> visited)
    {
        for (int x = centerX - radius; x <= centerX + radius; x++)
            ClearColumnForSpecial(s, obs, goals, ref result, x, visited);
    }

    private static void ClearRowForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int y, HashSet<(int, int)> visited)
    {
        if (y < 0 || y >= s.Height) return;
        for (int x = 0; x < s.Width; x++)
            ClearForSpecial(s, obs, goals, ref result, x, y, visited);
    }

    private static void ClearColumnForSpecial(
        SimState s, SimObstacleLayer obs, GoalTracker goals, ref GameResult result,
        int x, HashSet<(int, int)> visited)
    {
        if (x < 0 || x >= s.Width) return;
        for (int y = 0; y < s.Height; y++)
            ClearForSpecial(s, obs, goals, ref result, x, y, visited);
    }

    private static (int x, int y) BestPatchBotTarget(SimState s, SimObstacleLayer obs, int fallbackX, int fallbackY)
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
                        if (obs.ObstacleIdAt(x + dx, y + dy) != ObstacleId.None)
                            score += 1f;

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

    private static bool IsNormalColor(SimState s, int x, int y, TileType color)
    {
        if (x < 0 || y < 0 || x >= s.Width || y >= s.Height) return false;
        if (s.Holes[x, y]) return false;
        var tile = s.Grid[x, y];
        return tile != null && tile.Special == TileSpecial.None && tile.Type == color;
    }

    private static List<(int x, int y)> CollectColorCells(SimState s, TileType color)
    {
        var cells = new List<(int x, int y)>();
        for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
                if (IsNormalColor(s, x, y, color))
                    cells.Add((x, y));
        return cells;
    }

    private static TileType ColorOf(TileData t)
        => t != null && t.HasOverrideBaseType ? t.OverrideBaseType : (t != null ? t.Type : TileType.Gear);

    private static bool IsLine(TileSpecial special)
        => special == TileSpecial.LineH || special == TileSpecial.LineV;

    // ── Report ────────────────────────────────────────────────────────────────

    public static string FormatStats(RunStats s, LevelData level)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[SimRunner] Level: {level.name}  Games: {s.GameCount}  Moves/game: {level.moves}");
        sb.AppendLine($"  Win rate             : {s.WinRate:P1}  ({s.GamesWon}/{s.GameCount})");
        sb.AppendLine($"  Avg moves / game     : {s.AvgMovesPerGame:F1}");

        if (s.GamesWon > 0)
            sb.AppendLine($"  Avg moves on WIN     : {s.AvgMovesOnWin:F1}");
        if (s.GameCount - s.GamesWon > 0)
            sb.AppendLine($"  Avg moves on LOSS    : {s.AvgMovesOnLoss:F1}");

        sb.AppendLine($"  Avg tiles cleared    : {s.AvgTilesClearedPerGame:F1}");
        sb.AppendLine($"  Avg specials formed  : {s.AvgSpecialsPerGame:F2}");
        sb.AppendLine($"  Avg cascade steps    : {s.AvgCascadeStepsPerGame:F2}");
        sb.AppendLine($"  Max cascade chain    : {s.MaxCascadeChainSeen}");

        if (s.TotalDeadlockMoves > 0)
            sb.AppendLine($"  Total deadlock moves : {s.TotalDeadlockMoves}");

        sb.AppendLine($"  Obstacle origins in level   : {s.ObstacleCountInLevel}");
        sb.AppendLine($"  Avg obstacle clears / game  : {s.AvgObstacleClearsPerGame:F1}");

        AppendGoalInfo(sb, level);

        return sb.ToString().TrimEnd();
    }

    private static void AppendGoalInfo(StringBuilder sb, LevelData level)
    {
        if (level.goals == null || level.goals.Length == 0) return;

        sb.AppendLine("  Goals:");
        foreach (var g in level.goals)
        {
            string target = g.targetType switch
            {
                LevelGoalTargetType.Tile        => g.tileType.ToString(),
                LevelGoalTargetType.Obstacle    => g.obstacleId == ObstacleId.EnergyContainer
                                                    ? $"{g.obstacleId} (not simulated)"
                                                    : g.obstacleId.ToString(),
                LevelGoalTargetType.Collectible => $"{g.collectibleId} (not simulated)",
                _                               => "?"
            };
            sb.AppendLine($"    {target} x{g.amount}");
        }
    }
}
