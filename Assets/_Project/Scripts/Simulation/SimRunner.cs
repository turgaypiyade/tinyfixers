using System.Collections.Generic;
using System.Text;

// Drives N headless random-bot games on a LevelData and reports aggregate stats.
// Random bot = average player model: picks a uniformly random valid swap each move.
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

    public static RunStats Run(LevelData level, int gameCount, int seed = 42)
    {
        var stats = new RunStats { GameCount = gameCount };
        var rng = new System.Random(seed);

        int totalMovesWin = 0, totalMovesLoss = 0;

        for (int g = 0; g < gameCount; g++)
        {
            var r = PlayOneGame(level, rng);

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

    private static GameResult PlayOneGame(LevelData level, System.Random rng)
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

            var swap = SimMoves.PickRandom(state, rng);
            if (swap == null) { result.DeadlockMoves++; break; }

            SimMoves.Apply(state, swap.Value);
            result.MovesUsed++;
            movesLeft--;

            RunCascade(state, rng, finder, ref result, goalTracker);
        }

        if (!result.Won && goalTracker.AllMet)
            result.Won = true;

        result.ObstacleClears = obs.GetTotalClearedCount();

        return result;
    }

    // ── Cascade loop ─────────────────────────────────────────────────────────

    private static void RunCascade(
        SimState state, System.Random rng, SimMatchFinder finder,
        ref GameResult result, GoalTracker goals)
    {
        int chainLen = 0;
        var obs = state.Obstacles as SimObstacleLayer;

        while (true)
        {
            var matches = finder.FindAllMatches();
            if (matches.Count == 0) break;

            chainLen++;
            result.CascadeSteps++;

            bool specialThisStep = false;
            foreach (var td in matches)
            {
                if (!specialThisStep && finder.DecideSpecialAt(td.X, td.Y) != TileSpecial.None)
                    specialThisStep = true;

                goals.RecordTile(td.Type);
                CountTile(ref result, td.Type);

                obs?.ProcessMatchClear(td.X, td.Y, td.Type);

                state.Grid[td.X, td.Y] = null;
                result.TilesCleared++;
            }

            if (specialThisStep) result.SpecialsFormed++;

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
