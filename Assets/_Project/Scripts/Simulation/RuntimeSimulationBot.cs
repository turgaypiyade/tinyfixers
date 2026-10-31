using System.Collections.Generic;
using UnityEngine;

// GoalAware = "Acemi" (iyi eşleşme arar, seviye stratejisi yok), Expert = "Usta" (oyun başında seviye stratejisi:
// renk önceliği, kilit açıcı, yayılan tehdit). Auto: seviye ≤50 Acemi, 51+ Usta (kullanıcı kararı, 2026-10-29).
public enum RuntimeBotPolicy { Random, GoalAware, Greedy, Expert, Auto }

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
    private float[,] _goalValue = new float[0, 0];
    private readonly Dictionary<TileType, float> _colorNeed = new();
    private bool Expert => _policy == RuntimeBotPolicy.Expert;
    private bool Skilled => _policy == RuntimeBotPolicy.GoalAware || _policy == RuntimeBotPolicy.Expert;
    private const float ExpertUnlockValue = 0.8f;      // hedefin üstündeki örtü engeli (kır → hedef açılır)
    private const float ExpertSpreaderValue = 1.2f;    // yayılan tehdit (Yağ / Yayılan Jel) erken bastırılır
    private const float ExpertColorWeight = 0.35f;     // gereken renkteki eşleşme (eşleşen taş başına)   // hücre başına hedef değeri (engel / hedef taş), PickMove başında

    // İnsan oyun tarzı (kullanıcı, 2026-10-28): aşağıdan oyna (cascade + yer açma), special/kombo aşağıda ve
    // hedeflerin üzerinde patlat. Rastgelelik az: insan her zaman en iyiyi bulmaz ama zar da atmaz.
    private const double GoalAwareRandomMove = 0.03;   // eskiden 0.10
    private const float GoalAwareNoise = 1.5f;         // eskiden 8 (3'lü eşleşme 3 puandı → neredeyse rastgeleydi)
    private const float DepthWeight = 3f;              // en alt sıradaki eşleşme bu kadar fazla puan alır
    private const float FootprintGoalWeight = 1.5f;    // special etki alanındaki her hedef hücre
    // PatchBot hedefe kendisi uçar: az ve dağınık hedef kaldığında en değerli special (insan son engelleri
    // PatchBot'la bitirir; 2026-10-28 L9 kayıpları hep 1-3 çamur kala). Kalan engel hedefi bu eşiğin altına
    // indikçe PatchBot yapmak / kullanmak artan bonus alır.
    private const int PatchBotEndgameUnits = 10;
    private const float PatchBotEndgameWeight = 2.5f;
    private int _obstacleGoalUnitsLeft;

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
        if (_policy == RuntimeBotPolicy.Random || (Skilled && _decisions.NextDouble() < GoalAwareRandomMove))
            return _moves[_decisions.Next(_moves.Count)];
        BuildGoalValueMap();
        _obstacleGoalUnitsLeft = 0;
        foreach (var goal in _goals)
            if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Obstacle) _obstacleGoalUnitsLeft += goal.remaining;

        float best = float.NegativeInfinity;
        var chosen = _moves[0];
        foreach (var move in _moves)
        {
            float score = Score(move);
            // These are tunable bot behaviours, not claims about calibrated human skill.
            score += (float)_decisions.NextDouble() * (Skilled ? GoalAwareNoise : 0.01f);
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
            // Special'ın patlayacağı yer: swap'ta special karşı hücreye gider. Etki alanındaki hedefler + derinlik.
            int ex = move.BX, ey = move.BY;
            score += FootprintGoalWeight * FootprintGoals(sa, move.IsTap ? TileSpecial.None : sb, ex, ey);
            score += DepthWeight * Depth(ey);
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
                score += 1f + GoalProximity(tile.X, tile.Y, tile.Type);
                if (Expert && _colorNeed.TryGetValue(tile.Type, out float need)) score += ExpertColorWeight * need;
                foreach (var goal in _goals)
                    if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Tile && goal.tileType == tile.Type)
                        score += 6f;
                var view = _board.Tiles[tile.X, tile.Y];
                if (view != null) views.Add(view);
            }
            var creations = _creation.DecideUpToTwoFromMatches(views,
                new SpecialCreationService.CreationRequest(a, b, true));
            foreach (var creation in creations)
            {
                score += _creation.Score(creation.special) * 0.7f;
                if (creation.special == TileSpecial.PatchBot) score += PatchBotEndgameBonus();
            }
            // Aşağıdaki eşleşme üst sıraları düşürür → cascade + tahtada yer açılır (insan önce alttan oynar).
            if (matches.Count > 0)
            {
                float depth = 0f;
                foreach (var tile in matches) depth += Depth(tile.Y);
                score += DepthWeight * depth / matches.Count;
            }
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

    // Kalan engel hedefi azaldıkça büyür (10 birim kala 0, 1 birim kala ~22 puan): son hedefleri PatchBot bitirir.
    private float PatchBotEndgameBonus()
    {
        if (_obstacleGoalUnitsLeft <= 0 || _obstacleGoalUnitsLeft >= PatchBotEndgameUnits) return 0f;
        return (PatchBotEndgameUnits - _obstacleGoalUnitsLeft) * PatchBotEndgameWeight;
    }

    // 0 = en üst sıra, 1 = en alt sıra (aşağı = y+1, taban = Height-1).
    private float Depth(int y) => _board.Height <= 1 ? 0f : Mathf.Clamp01(y / (float)(_board.Height - 1));

    // Hücrenin hedef değeri: hedef engel ya da hedef taş türü. Special etki alanını puanlamak için.
    private void BuildGoalValueMap()
    {
        int w = _board.Width, h = _board.Height;
        if (_goalValue.GetLength(0) != w || _goalValue.GetLength(1) != h) _goalValue = new float[w, h];
        var obs = _board.ObstacleStateService;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = 0f;
                var id = obs != null ? obs.GetObstacleIdAt(x, y) : ObstacleId.None;
                var data = _board.GridData[x, y];
                foreach (var goal in _goals)
                {
                    if (goal.remaining <= 0) continue;
                    if (goal.targetType == LevelGoalTargetType.Obstacle && id != ObstacleId.None && goal.obstacleId == id) v += 1f;
                    else if (goal.targetType == LevelGoalTargetType.Tile && data != null && goal.tileType == data.Type) v += 0.5f;
                }
                if (Expert && obs != null)
                {
                    // Kilit açıcı: üstteki engel hedef değil ama altında hedef var → onu kırmak hedefi açar.
                    if (obs.TryGetStampedBeneathObstacleIdAt(x, y, out var beneath) && IsObstacleGoal(beneath)) v += ExpertUnlockValue;
                    // Yayılan tehdit büyümeden.
                    if (id == ObstacleId.Oil || id == ObstacleId.SpreadingGel) v += ExpertSpreaderValue;
                }
                _goalValue[x, y] = v;
            }
        if (Expert) BuildColorNeed();
    }

    private bool IsObstacleGoal(ObstacleId id)
    {
        foreach (var goal in _goals)
            if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == id) return true;
        return false;
    }

    // Renk önceliği: yalnız bazı renklerden hasar alan hedef engeller (Kırmızı/Mavi Plastik, açık Renkli Sandık)
    // hangi renkleri istiyor? Oyunun kendi kuralıyla (AcceptsNormalMatchColorAt) sorulur; kısıtsız engel sayılmaz.
    private void BuildColorNeed()
    {
        _colorNeed.Clear();
        var obs = _board.ObstacleStateService;
        var pool = _board.RandomPool;
        if (obs == null || pool == null || pool.Length == 0) return;
        for (int y = 0; y < _board.Height; y++)
            for (int x = 0; x < _board.Width; x++)
            {
                var id = obs.GetObstacleIdAt(x, y);
                if (id == ObstacleId.None || !IsObstacleGoal(id)) continue;
                int accepted = 0;
                foreach (var t in pool) if (obs.AcceptsNormalMatchColorAt(x, y, t)) accepted++;
                if (accepted == 0 || accepted == pool.Length) continue;
                foreach (var t in pool)
                    if (obs.AcceptsNormalMatchColorAt(x, y, t))
                        _colorNeed[t] = (_colorNeed.TryGetValue(t, out float c) ? c : 0f) + 1f / accepted;
            }
        // Ölçek: en çok istenen renk ~1.
        float max = 0f;
        foreach (var kv in _colorNeed) max = Mathf.Max(max, kv.Value);
        if (max > 0f)
        {
            var keys = new List<TileType>(_colorNeed.Keys);
            foreach (var k in keys) _colorNeed[k] /= max;
        }
    }

    // Special (ya da iki special'ın kombosu) (x, y)'de patlarsa etki alanındaki toplam hedef değeri — yaklaşık.
    private float FootprintGoals(TileSpecial a, TileSpecial b, int x, int y)
    {
        bool Has(TileSpecial s) => a == s || b == s;
        bool line = Has(TileSpecial.LineH) || Has(TileSpecial.LineV);
        if (Has(TileSpecial.SystemOverride)) return SumRect(0, 0, _board.Width - 1, _board.Height - 1) * (a != TileSpecial.None && b != TileSpecial.None ? 0.6f : 0.25f);
        if (a == TileSpecial.PulseCore && b == TileSpecial.PulseCore) return SumRect(x - 3, y - 3, x + 3, y + 3);
        if (Has(TileSpecial.PulseCore) && line) return SumRect(0, y - 1, _board.Width - 1, y + 1) + SumRect(x - 1, 0, x + 1, _board.Height - 1);
        if (a != TileSpecial.None && b != TileSpecial.None && line && !Has(TileSpecial.PatchBot)) return SumRect(0, y, _board.Width - 1, y) + SumRect(x, 0, x, _board.Height - 1);
        if (Has(TileSpecial.PulseCore)) return SumRect(x - 2, y - 2, x + 2, y + 2);
        if (Has(TileSpecial.LineH) && !Has(TileSpecial.LineV)) return SumRect(0, y, _board.Width - 1, y);
        if (Has(TileSpecial.LineV)) return SumRect(x, 0, x, _board.Height - 1);
        if (Has(TileSpecial.PatchBot)) return 2f + PatchBotEndgameBonus() / FootprintGoalWeight;   // hedefi kendisi seçer
        return 0f;
    }

    private float SumRect(int x0, int y0, int x1, int y1)
    {
        x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0);
        x1 = Mathf.Min(_board.Width - 1, x1); y1 = Mathf.Min(_board.Height - 1, y1);
        float sum = 0f;
        for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++) sum += _goalValue[xx, yy];
        return sum;
    }

    private float GoalProximity(int x, int y) => GoalProximity(x, y, null);

    // matchType verilirse renk kuralı uygulanır: Kırmızı/Mavi Plastik ve açık Renkli Sandık yalnız uygun renkten
    // hasar alır — insan bunu görür, bot eskiden yanlış renkle hamle harcıyordu (L24/L29, 2026-10-29).
    private float GoalProximity(int x, int y, TileType? matchType)
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
                if (id == ObstacleId.None) continue;
                if (matchType.HasValue && !_board.ObstacleStateService.AcceptsNormalMatchColorAt(cx, cy, matchType.Value)) continue;
                foreach (var goal in _goals)
                    if (goal.remaining > 0 && goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == id)
                        score += 4f;
                if (Expert)
                {
                    if (!IsObstacleGoal(id) && _board.ObstacleStateService.TryGetStampedBeneathObstacleIdAt(cx, cy, out var beneath)
                        && IsObstacleGoal(beneath)) score += 3f;
                    if (!IsObstacleGoal(id) && (id == ObstacleId.Oil || id == ObstacleId.SpreadingGel)) score += 2f;
                }
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
