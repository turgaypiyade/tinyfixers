using System.Collections.Generic;
using UnityEngine;

public enum RuntimeBotPolicy { Random, GoalAware, Greedy }

public readonly struct RuntimeBotMove : System.IEquatable<RuntimeBotMove>
{
    public readonly int AX, AY, BX, BY;
    public bool IsTap => AX == BX && AY == BY;
    public RuntimeBotMove(int ax, int ay, int bx, int by) { AX = ax; AY = ay; BX = bx; BY = by; }
    public bool Equals(RuntimeBotMove other) => AX == other.AX && AY == other.AY && BX == other.BX && BY == other.BY;
    public override bool Equals(object obj) => obj is RuntimeBotMove other && Equals(other);
    public override int GetHashCode() => unchecked(((AX * 397 + AY) * 397 + BX) * 397 + BY);
    public override string ToString() => IsTap ? $"tap({AX},{AY})" : $"swap({AX},{AY})->({BX},{BY})";
}

/// <summary>
/// Chooses inputs on the real board. Only a player heuristic lives here: it does not
/// predict obstacle damage or execute an alternative game. Match/special formation
/// queries use MatchFinder and SpecialCreationService from the shipped game.
/// </summary>
public sealed class RuntimeSimulationBot
{
    private readonly BoardController _board;
    private readonly MatchFinder _finder;
    private readonly SpecialCreationService _creation;
    private readonly RuntimeBotPolicy _policy;
    private readonly System.Random _decisions;
    private readonly List<RuntimeBotMove> _moves = new();
    private readonly List<TopHudController.ActiveGoal> _goals = new();

    public RuntimeSimulationBot(BoardController board, RuntimeBotPolicy policy, int seed)
    {
        _board = board;
        _policy = policy;
        _finder = new MatchFinder(board);
        _creation = new SpecialCreationService(_finder);
        _decisions = new System.Random(unchecked(seed ^ (int)0x9E3779B9));
    }

    public RuntimeBotMove? PickMove(HashSet<RuntimeBotMove> rejected)
    {
        _moves.Clear();
        _board.TopHud.GetActiveGoals(_goals);
        for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                var tile = _board.GetTileViewAt(x, y);
                if (_board.CanTapActivateSpecial(tile)) Add(new RuntimeBotMove(x, y, x, y), rejected);
                if (_finder.IsPlayableSwap(x, y, x + 1, y)) Add(new RuntimeBotMove(x, y, x + 1, y), rejected);
                if (_finder.IsPlayableSwap(x, y, x, y + 1)) Add(new RuntimeBotMove(x, y, x, y + 1), rejected);
            }
        if (_moves.Count == 0) return null;
        if (_policy == RuntimeBotPolicy.Random || (_policy == RuntimeBotPolicy.GoalAware && _decisions.NextDouble() < 0.10))
            return _moves[_decisions.Next(_moves.Count)];

        float best = float.NegativeInfinity;
        var chosen = _moves[0];
        foreach (var move in _moves)
        {
            float score = Score(move);
            // These are tunable bot behaviours, not claims about calibrated human skill.
            score += (float)_decisions.NextDouble() * (_policy == RuntimeBotPolicy.GoalAware ? 8f : 0.01f);
            if (score <= best) continue;
            best = score; chosen = move;
        }
        return chosen;
    }

    private void Add(RuntimeBotMove move, HashSet<RuntimeBotMove> rejected)
    {
        if (!rejected.Contains(move)) _moves.Add(move);
    }

    private float Score(RuntimeBotMove move)
    {
        var a = _board.Tiles[move.AX, move.AY];
        var b = _board.Tiles[move.BX, move.BY];
        var sa = a.GetSpecial(); var sb = b.GetSpecial();
        if (sa != TileSpecial.None || sb != TileSpecial.None)
        {
            float score = 8f + _creation.Score(sa) * 0.2f;
            if (!move.IsTap)
            {
                score += _creation.Score(sb) * 0.2f;
                if (sa != TileSpecial.None && sb != TileSpecial.None) score += 22f;
            }
            return score + GoalProximity(move.BX, move.BY);
        }

        // Synchronous, non-yielding preview, exactly as MatchFinder's deadlock check.
        // All references and coordinates are restored even if a query throws.
        var ad = _board.GridData[move.AX, move.AY];
        var bd = _board.GridData[move.BX, move.BY];
        try
        {
            _board.Tiles[move.AX, move.AY] = b; _board.Tiles[move.BX, move.BY] = a;
            a.SetCoords(move.BX, move.BY); b.SetCoords(move.AX, move.AY);
            _board.GridData[move.AX, move.AY] = bd; _board.GridData[move.BX, move.BY] = ad;
            ad?.SetCoords(move.BX, move.BY); bd?.SetCoords(move.AX, move.AY);
            _finder.InvalidateRunCache();
            var matches = _finder.FindAllMatches(logDiagnostics: false);
            var views = new HashSet<TileView>();
            float score = 0f;
            foreach (var tile in matches)
            {
                score += 1f + GoalProximity(tile.X, tile.Y);
                foreach (var goal in _goals)
                    if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Tile && goal.tileType == tile.Type)
                        score += 6f;
                var view = _board.Tiles[tile.X, tile.Y];
                if (view != null) views.Add(view);
            }
            var creations = _creation.DecideUpToTwoFromMatches(views,
                new SpecialCreationService.CreationRequest(a, b, true));
            foreach (var creation in creations) score += _creation.Score(creation.special) * 0.7f;
            return score;
        }
        finally
        {
            _board.Tiles[move.AX, move.AY] = a; _board.Tiles[move.BX, move.BY] = b;
            a.SetCoords(move.AX, move.AY); b.SetCoords(move.BX, move.BY);
            _board.GridData[move.AX, move.AY] = ad; _board.GridData[move.BX, move.BY] = bd;
            ad?.SetCoords(move.AX, move.AY); bd?.SetCoords(move.BX, move.BY);
            _finder.InvalidateRunCache();
        }
    }

    private float GoalProximity(int x, int y)
    {
        if (_board.ObstacleStateService == null) return 0;
        float score = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > 1) continue;
                int cx = x + dx, cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= _board.Width || cy >= _board.Height) continue;
                var id = _board.ObstacleStateService.GetObstacleIdAt(cx, cy);
                foreach (var goal in _goals)
                    if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == id)
                        score += 4f;
            }
        return score;
    }

    public void Submit(RuntimeBotMove move)
    {
        var tile = _board.GetTileViewAt(move.AX, move.AY);
        if (move.IsTap) _board.OnTileClicked(tile);
        else _board.RequestSwapFromDrag(tile, move.BX - move.AX, move.BY - move.AY);
    }

    public string BoardFingerprint()
    {
        // Stable across processes; records diagnostics without serializing Unity objects.
        uint hash = 2166136261;
        for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                var tile = _board.GridData[x, y];
                hash = unchecked((hash ^ (uint)(tile == null ? 0 : 1 + (int)tile.Type * 16 + (int)tile.Special)) * 16777619);
                var obs = _board.ObstacleStateService;
                hash = unchecked((hash ^ (uint)(obs == null ? 0 : (int)obs.GetObstacleIdAt(x, y))) * 16777619);
                hash = unchecked((hash ^ (uint)(obs == null ? 0 : obs.GetRemainingHitsAt(x, y))) * 16777619);
            }
        return hash.ToString("X8");
    }
}
