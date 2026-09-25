using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// N adet headless oyunu (isteğe bağlı paralel) koşturur ve zengin istatistik üretir.
/// Eski sürüm tek thread'de, tek "mistakeChance" ile, yalnız win% + birkaç ortalama veriyordu.
/// </summary>
public static class SimRunner
{
    // ── Sonuç tipleri ────────────────────────────────────────────────────────

    public struct GameResult
    {
        public bool Won;
        public int MovesUsed;
        public int MovesLeft;
        public float GoalCompletion;
        public int TilesCleared;
        public int SpecialsCreated;
        public int SpecialsActivated;
        public int CombosActivated;
        public int CascadeSteps;
        public int MaxChain;
        public int Shuffles;
        public int SpecialsCaged;
        public bool Deadlocked;
        public float[] GoalRatios;
    }

    public sealed class RunStats
    {
        public string LevelName;
        public string ProfileName;
        public int GameCount;
        public int RequestedGameCount;
        public int Seed;
        public int MovesBudget;
        public long ElapsedMs;

        public int GamesWon;
        public float WinRate;
        public float WinRateLow, WinRateHigh;   // %95 Wilson güven aralığı

        public float AvgMovesUsed;
        public float AvgMovesOnWin;
        public float AvgMovesLeftOnWin;         // kazanınca ne kadar hamle artıyor → "çok kolay" sinyali
        public float AvgGoalCompletionOnLoss;   // kaybederken hedefin ne kadarına gelebildi

        public float AvgTilesCleared;
        public float AvgSpecialsCreated;
        public float AvgSpecialsActivated;
        public float AvgCombos;
        public float AvgCascadeSteps;
        public int MaxChainSeen;

        public int DeadlockGames;
        public float AvgShuffles;
        public float AvgSpecialsCaged;

        public string[] GoalLabels;
        public float[] GoalAvgRatio;            // TÜM oyunlarda hedef başına ortalama tamamlanma
        public float[] GoalAvgRatioOnLoss;      // yalnız kaybedilen oyunlar (0 kayıp varsa anlamsız)
        public int BottleneckGoalIndex = -1;    // en çok tıkayan hedef (kayıp varsa)

        public SimGoalFidelity Fidelity;
        public List<string> Warnings = new();

        public float DifficultyScore;           // 0..1 — 1 = çok zor
        public string DifficultyLabel;
    }

    // ── Giriş noktaları ──────────────────────────────────────────────────────

    /// <summary>Geriye dönük uyumlu kısa yol — ortalama oyuncu profili.</summary>
    public static RunStats Run(LevelData level, int gameCount, int seed = 42)
        => Run(level, gameCount, SimPlayerProfile.Average, seed);

    public static RunStats Run(LevelData level, int gameCount, SimPlayerProfile profile,
        int seed = 42, bool parallel = true)
    {
        var input = Prepare(level, gameCount, profile, seed);
        if (input.assessment.Fidelity == SimGoalFidelity.NotSimulated) return input.assessment;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = RunGames(input.level, input.rules, input.profile, gameCount, seed, parallel, default);
        return Finish(input.level, input.rules, input.profile, results, seed, sw.ElapsedMilliseconds);
    }

    /// <summary>Call on the editor main thread; only detached data enters the worker.</summary>
    public static async Task<RunStats> RunAsync(LevelData level, int gameCount, SimPlayerProfile profile,
        int seed = 42, bool parallel = true, System.Threading.CancellationToken cancellationToken = default)
    {
        var input = Prepare(level, gameCount, profile, seed);
        if (input.assessment.Fidelity == SimGoalFidelity.NotSimulated) return input.assessment;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await Task.Run(() => RunGames(input.level, input.rules, input.profile,
            gameCount, seed, parallel, cancellationToken), cancellationToken);
        return Finish(input.level, input.rules, input.profile, results, seed, sw.ElapsedMilliseconds);
    }

    private static (SimLevel level, SimRules rules, SimPlayerProfile profile, RunStats assessment)
        Prepare(LevelData level, int gameCount, SimPlayerProfile profile, int seed)
    {
        if (gameCount < 1) throw new System.ArgumentOutOfRangeException(nameof(gameCount));
        var snapshot = new SimLevel(level);
        profile = (profile ?? SimPlayerProfile.Average).Snapshot();
        if (profile.Depth < 1 || profile.Depth > 2 || profile.BeamWidth < 1)
            throw new System.ArgumentException("Bot depth must be 1 or 2 and beam width positive.");
        var rules = SimRules.From(level.obstacleLibrary);
        var assessment = Aggregate(snapshot, System.Array.Empty<GameResult>(), profile, seed);
        assessment.RequestedGameCount = gameCount;
        CollectWarnings(snapshot, rules, assessment);
        assessment.DifficultyLabel = "MODELLENMİYOR";
        return (snapshot, rules, profile, assessment);
    }

    private static GameResult[] RunGames(SimLevel level, SimRules rules, SimPlayerProfile profile,
        int count, int seed, bool parallel, System.Threading.CancellationToken cancellationToken)
    {
        var results = new GameResult[count];
        if (parallel && count > 1)
            Parallel.For(0, count, new ParallelOptions { CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = System.Math.Max(1, System.Environment.ProcessorCount - 1) },
                g => results[g] = PlayOneGame(level, rules, profile, unchecked(seed + g * 7919), cancellationToken));
        else
            for (int g = 0; g < count; g++)
                results[g] = PlayOneGame(level, rules, profile, unchecked(seed + g * 7919), cancellationToken);
        return results;
    }

    private static RunStats Finish(SimLevel level, SimRules rules, SimPlayerProfile profile,
        GameResult[] results, int seed, long elapsedMs)
    {
        var stats = Aggregate(level, results, profile, seed);
        stats.RequestedGameCount = results.Length;
        stats.ElapsedMs = elapsedMs;
        CollectWarnings(level, rules, stats);
        return stats;
    }

    // ── Tek oyun ─────────────────────────────────────────────────────────────

    private static GameResult PlayOneGame(SimLevel level, SimRules rules, SimPlayerProfile profile, int seed,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rng  = new System.Random(seed);
        // Thinking longer or changing profile must not advance the board's refill stream.
        var decisionRng = new System.Random(unchecked(seed ^ (int)0x9E3779B9));
        var game = new SimGame(level, rules, rng);
        var bot  = new SimBot(profile);

        game.Start();

        var result = new GameResult();

        while (game.MovesLeft > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game.Goals.AllMet) break;

            var swap = bot.PickMove(game, decisionRng);
            if (swap == null)
            {
                // Canlı oyun gibi: hamle kalmadıysa karıştır, olmazsa oyun biter.
                if (!game.TryShuffle()) { result.Deadlocked = true; break; }
                swap = bot.PickMove(game, decisionRng);
                if (swap == null) { result.Deadlocked = true; break; }
            }

            game.PlayMove(swap.Value);
        }

        result.Won               = game.Goals.AllMet;
        result.MovesUsed         = game.MovesUsed;
        result.MovesLeft         = game.MovesLeft < 0 ? 0 : game.MovesLeft;
        result.GoalCompletion    = game.Goals.Completion;
        result.TilesCleared      = game.Totals.TilesCleared;
        result.SpecialsCreated   = game.Totals.SpecialsCreated;
        result.SpecialsActivated = game.Totals.SpecialsActivated;
        result.CombosActivated   = game.Totals.CombosActivated;
        result.CascadeSteps      = game.Totals.CascadeSteps;
        result.MaxChain          = game.Totals.MaxChain;
        result.Shuffles          = game.Shuffles;
        result.SpecialsCaged     = game.SpecialsCaged;

        int goalCount = game.Goals.GoalCount;
        result.GoalRatios = new float[goalCount];
        for (int i = 0; i < goalCount; i++) result.GoalRatios[i] = game.Goals.GetEntry(i).Ratio;

        return result;
    }

    // ── Toplama ──────────────────────────────────────────────────────────────

    private static RunStats Aggregate(SimLevel level, GameResult[] results,
        SimPlayerProfile profile, int seed)
    {
        var goals = new SimGoalSet(level);
        int n = results.Length;

        var s = new RunStats
        {
            LevelName   = level.name,
            ProfileName = profile.Name,
            GameCount   = n,
            Seed        = seed,
            MovesBudget = level.moves,
            Fidelity    = goals.Fidelity,
            GoalLabels  = new string[goals.GoalCount],
            GoalAvgRatio = new float[goals.GoalCount],
            GoalAvgRatioOnLoss = new float[goals.GoalCount],
        };

        for (int i = 0; i < goals.GoalCount; i++)
        {
            var e = goals.GetEntry(i);
            s.GoalLabels[i] = $"{e.Label} x{e.Needed}";
        }

        if (n == 0) return s;

        int wins = 0, losses = 0;
        float movesOnWin = 0, movesLeftOnWin = 0, completionOnLoss = 0;

        foreach (var r in results)
        {
            if (r.Won)
            {
                wins++;
                movesOnWin     += r.MovesUsed;
                movesLeftOnWin += r.MovesLeft;
            }
            else
            {
                losses++;
                completionOnLoss += r.GoalCompletion;
                for (int i = 0; i < s.GoalAvgRatioOnLoss.Length && i < r.GoalRatios.Length; i++)
                    s.GoalAvgRatioOnLoss[i] += r.GoalRatios[i];
            }

            for (int i = 0; i < s.GoalAvgRatio.Length && i < r.GoalRatios.Length; i++)
                s.GoalAvgRatio[i] += r.GoalRatios[i];

            s.AvgMovesUsed         += r.MovesUsed;
            s.AvgTilesCleared      += r.TilesCleared;
            s.AvgSpecialsCreated   += r.SpecialsCreated;
            s.AvgSpecialsActivated += r.SpecialsActivated;
            s.AvgCombos            += r.CombosActivated;
            s.AvgCascadeSteps      += r.CascadeSteps;
            s.AvgShuffles          += r.Shuffles;
            s.AvgSpecialsCaged     += r.SpecialsCaged;
            if (r.Deadlocked) s.DeadlockGames++;
            if (r.MaxChain > s.MaxChainSeen) s.MaxChainSeen = r.MaxChain;
        }

        s.GamesWon = wins;
        s.WinRate  = (float)wins / n;
        (s.WinRateLow, s.WinRateHigh) = WilsonInterval(wins, n);

        for (int i = 0; i < s.GoalAvgRatio.Length; i++) s.GoalAvgRatio[i] /= n;

        s.AvgMovesUsed         /= n;
        s.AvgTilesCleared      /= n;
        s.AvgSpecialsCreated   /= n;
        s.AvgSpecialsActivated /= n;
        s.AvgCombos            /= n;
        s.AvgCascadeSteps      /= n;
        s.AvgShuffles          /= n;
        s.AvgSpecialsCaged     /= n;

        s.AvgMovesOnWin          = wins > 0 ? movesOnWin / wins : 0f;
        s.AvgMovesLeftOnWin      = wins > 0 ? movesLeftOnWin / wins : 0f;
        s.AvgGoalCompletionOnLoss = losses > 0 ? completionOnLoss / losses : 0f;

        if (losses > 0)
        {
            float worst = float.MaxValue;
            for (int i = 0; i < s.GoalAvgRatioOnLoss.Length; i++)
            {
                s.GoalAvgRatioOnLoss[i] /= losses;
                if (s.GoalAvgRatioOnLoss[i] < worst)
                {
                    worst = s.GoalAvgRatioOnLoss[i];
                    s.BottleneckGoalIndex = i;
                }
            }
        }

        s.DifficultyScore = 1f - s.WinRate;
        s.DifficultyLabel = LabelFor(s.WinRate);
        return s;
    }

    private static string LabelFor(float winRate)
    {
        if (winRate >= 0.90f) return "ÇOK KOLAY";
        if (winRate >= 0.70f) return "kolay";
        if (winRate >= 0.45f) return "dengeli";
        if (winRate >= 0.25f) return "zor";
        if (winRate >= 0.10f) return "ÇOK ZOR";
        return "GEÇİLEMEZ?";
    }

    /// <summary>
    /// %95 Wilson güven aralığı. 150 oyunda %40 gördüysen gerçek değer %32-%48 arasıdır —
    /// bu aralığı bilmeden level dengesi "ayarlamak" gürültüyü kovalamaktır.
    /// </summary>
    private static (float low, float high) WilsonInterval(int successes, int total)
    {
        if (total == 0) return (0f, 0f);

        const double z = 1.96;
        double p = (double)successes / total;
        double denom = 1 + z * z / total;
        double center = p + z * z / (2.0 * total);
        double margin = z * System.Math.Sqrt(p * (1 - p) / total + z * z / (4.0 * total * total));

        return ((float)((center - margin) / denom), (float)((center + margin) / denom));
    }

    private static void CollectWarnings(SimLevel level, SimRules rules, RunStats s)
    {
        s.Fidelity = InspectModel(level, rules, s.Warnings);
        if (s.Fidelity == SimGoalFidelity.NotSimulated)
            s.Warnings.Add("Bu levelda simüle EDİLMEYEN hedef var (EnergyOrb / KeyGenerator / RocketBasket gibi) — win% GÜVENİLMEZ.");
        else if (s.Fidelity == SimGoalFidelity.Approximate)
            s.Warnings.Add("Bellek motorunun refill, hedef seçimi ve servis zamanlamaları yaklaşık; oran yalnız bu bot/model için ölçümdür.");

        if (level.levelKind == LevelKind.BossDuel)
            s.Warnings.Add("BossDuel levelı: boss saldırıları/dalgaları simüle edilmiyor, yalnız hasar birikimi.");

        var missing = rules?.MissingDefs;
        if (missing != null && missing.Length > 0)
        {
            var names = new List<string>();
            foreach (var id in missing) names.Add(id.ToString());
            s.Warnings.Add($"ObstacleLibrary'de tanımı olmayan obstacle: {string.Join(", ", names)} (1 vuruşluk blocker varsayıldı).");
        }

        CheckGoalReachability(level, rules, s);

        if (level.goals == null || level.goals.Length == 0)
            s.Warnings.Add("Levelda hedef tanımlı değil — kazanmak imkânsız, win% 0 çıkar.");

        if (s.GameCount > 0 && s.DeadlockGames > s.GameCount / 10)
            s.Warnings.Add($"Oyunların %{100f * s.DeadlockGames / s.GameCount:F0}'ı hamlesiz kaldı (shuffle da kurtaramadı) — tahta çok kilitli.");
    }

    /// <summary>Whole-level coverage, including mechanics that are NOT level goals.</summary>
    public static SimGoalFidelity InspectModel(LevelData level, SimRules rules, List<string> warnings)
        => InspectModel(new SimLevel(level), rules, warnings);

    private static SimGoalFidelity InspectModel(SimLevel level, SimRules rules, List<string> warnings)
    {
        // Refill/target timing remains a headless model; no empirical claim of exact human difficulty.
        var fidelity = SimGoalFidelity.Approximate;
        var goalFidelity = new SimGoalSet(level).Fidelity;
        if (goalFidelity == SimGoalFidelity.NotSimulated) fidelity = goalFidelity;
        var ids = new HashSet<ObstacleId>();
        if (level.obstacles != null)
            foreach (int id in level.obstacles) if (id != 0) ids.Add((ObstacleId)id);
        if (level.stackedObstacles != null)
            foreach (var item in level.stackedObstacles) ids.Add(item.obstacleId);
        if (level.tubes != null && level.tubes.Length > 0) ids.Add(ObstacleId.Tube);
        if (level.magnets != null && level.magnets.Length > 0) ids.Add(ObstacleId.Magnet);
        if (level.safes != null && level.safes.Length > 0) ids.Add(ObstacleId.Safe);
        if (level.goals != null)
            foreach (var goal in level.goals)
                if (goal.targetType == LevelGoalTargetType.Obstacle) ids.Add(goal.obstacleId);

        foreach (var id in ids)
        {
            if (id == ObstacleId.None) continue;
            if (!rules.HasDef(id))
            {
                fidelity = SimGoalFidelity.NotSimulated;
                warnings.Add($"{id}: obstacle tanımı eksik; bu level için zorluk hesaplanmadı.");
            }
            switch (id)
            {
                case ObstacleId.EnergyContainer:
                case ObstacleId.HatLauncher:
                case ObstacleId.KeyGenerator:
                case ObstacleId.RocketBasket:
                case ObstacleId.Barrel:
                case ObstacleId.Barrell_v2:
                case ObstacleId.EggBird:
                case ObstacleId.BatteryBox:
                case ObstacleId.OverrideBatteryBox:
                case ObstacleId.SpreadingGel:
                case ObstacleId.Oil:
                    fidelity = SimGoalFidelity.NotSimulated;
                    warnings.Add($"{id}: üretim/yayılma/özel servis davranışı eksik; hedef olmasa da sonucu etkiler.");
                    break;
                case ObstacleId.Magnet:
                case ObstacleId.Safe:
                case ObstacleId.Tube:
                case ObstacleId.Wardrobe:
                case ObstacleId.Cargo:
                    warnings.Add($"{id}: servis zamanlaması/özel durumları yaklaşık modelleniyor.");
                    break;
            }
        }
        if (level.levelKind == LevelKind.BossDuel)
        {
            fidelity = SimGoalFidelity.NotSimulated;
            warnings.Add("Boss saldırıları ve savunması modellenmediği için düello zorluğu hesaplanmadı.");
        }
        return fidelity;
    }

    /// <summary>
    /// Yazım hatası avcısı: hedef X adet obstacle istiyor ama tahtada o kadarı YOK —
    /// level matematiksel olarak kazanılamaz. Win%'e bakmadan önce bunu bilmek gerekir.
    /// (Üreyen obstacle'lar — Barrel'ın saçtığı Mud gibi — hariç tutulur.)
    /// </summary>
    private static void CheckGoalReachability(SimLevel level, SimRules rules, RunStats s)
    {
        if (level.goals == null) return;

        var layer = new SimObstacleLayer(level, rules);

        foreach (var g in level.goals)
        {
            if (g.targetType != LevelGoalTargetType.Obstacle) continue;
            if (CanAppearDuringPlay(g.obstacleId, rules)) continue;

            int available = layer.GetInitialCount(g.obstacleId);
            if (available >= g.amount) continue;

            s.Warnings.Add(available == 0
                ? $"HEDEF TAHTADA YOK: {g.obstacleId} x{g.amount} isteniyor ama levelda hiç yok → kazanmak İMKÂNSIZ."
                : $"HEDEF YETERSİZ: {g.obstacleId} x{g.amount} isteniyor ama levelda {available} tane var → kazanmak İMKÂNSIZ.");
        }
    }

    /// <summary>
    /// Oyun sırasında YENİSİ doğabilen obstacle mı? Böyleyse başlangıç sayısının hedefi
    /// karşılaması gerekmez. MOVABLE hedefler (plastic_red vb.) refill sırasında tepeden
    /// yeniden üretilir — bu kural def'ten okunur, elle liste tutulmaz.
    /// </summary>
    public static bool CanAppearDuringPlay(ObstacleId id, SimRules rules)
    {
        var rule = rules?.Get(id);
        if (rule != null && !rule.IsFallback && rule.IsMovable(rule.Hits)) return true;

        return id switch
        {
            ObstacleId.Mud          => true,   // Barrel kırılınca saçılır
            ObstacleId.Oil          => true,   // yayılır
            ObstacleId.SpreadingGel => true,   // yayılır
            ObstacleId.KeyGenerator => true,   // Key üretir
            _                       => false
        };
    }

    // ── Rapor ────────────────────────────────────────────────────────────────

    public static string FormatStats(RunStats s)
    {
        var sb = new StringBuilder();
        if (s.Fidelity == SimGoalFidelity.NotSimulated)
        {
            sb.AppendLine($"[Sim] {s.LevelName} — MODELLENMİYOR (koşu yapılmadı)");
            foreach (var warning in s.Warnings) sb.AppendLine("  ⚠ " + warning);
            return sb.ToString().TrimEnd();
        }
        sb.AppendLine($"[Sim] {s.LevelName} — {s.ProfileName} profili, {s.GameCount} oyun, {s.MovesBudget} hamle ({s.ElapsedMs} ms)");
        sb.AppendLine("  Sonuç bot modeline aittir; insan kazanma oranı veya çözülebilirlik kanıtı değildir.");
        sb.AppendLine($"  Win rate            : {s.WinRate:P1}  [{s.WinRateLow:P0}–{s.WinRateHigh:P0}] → {s.DifficultyLabel}   ({s.GamesWon}/{s.GameCount})");

        if (s.GamesWon > 0)
            sb.AppendLine($"  Kazanınca           : {s.AvgMovesOnWin:F1} hamlede, {s.AvgMovesLeftOnWin:F1} hamle artarak");
        if (s.GamesWon < s.GameCount)
            sb.AppendLine($"  Kaybedince          : hedefin %{s.AvgGoalCompletionOnLoss * 100f:F0}'ına ulaşabildi");

        if (s.GoalLabels.Length > 0)
        {
            bool hasLosses = s.GamesWon < s.GameCount;
            sb.AppendLine(hasLosses
                ? "  Hedefler (ortalama tamamlanma — tüm oyunlar / kaybedilenler):"
                : "  Hedefler (ortalama tamamlanma):");

            for (int i = 0; i < s.GoalLabels.Length; i++)
            {
                string mark = hasLosses && i == s.BottleneckGoalIndex && s.GoalLabels.Length > 1 ? "  ← TIKAYAN" : "";
                string loss = hasLosses ? $"  /  %{s.GoalAvgRatioOnLoss[i] * 100f:F0}" : "";
                sb.AppendLine($"    {s.GoalLabels[i],-28} %{s.GoalAvgRatio[i] * 100f:F0}{loss}{mark}");
            }
        }

        sb.AppendLine($"  Taş / oyun          : {s.AvgTilesCleared:F1}");
        sb.AppendLine($"  Special üretildi    : {s.AvgSpecialsCreated:F2}   kullanıldı: {s.AvgSpecialsActivated:F2}   combo: {s.AvgCombos:F2}");
        sb.AppendLine($"  Cascade adımı       : {s.AvgCascadeSteps:F2}   en uzun zincir: {s.MaxChainSeen}");

        if (s.AvgSpecialsCaged > 0.01f)
            sb.AppendLine($"  Magnet kafesledi    : {s.AvgSpecialsCaged:F2} special / oyun (oyuncunun elinden gitti)");

        if (s.AvgShuffles > 0.01f || s.DeadlockGames > 0)
            sb.AppendLine($"  Shuffle / oyun      : {s.AvgShuffles:F2}   hamlesiz biten: {s.DeadlockGames}");

        foreach (var w in s.Warnings)
            sb.AppendLine($"  ⚠ {w}");

        return sb.ToString().TrimEnd();
    }

    public static string CsvHeader()
        => "Level,Profile,Games,Win%,WinLow%,WinHigh%,Difficulty,Moves,AvgMovesOnWin,AvgMovesLeftOnWin," +
           "GoalCompletionOnLoss%,BottleneckGoal,AvgTiles,AvgSpecials,AvgCombos,Deadlocks,AvgShuffles,Fidelity,Seed,Warnings";

    private static string CsvCell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static string Number(float value, string format)
        => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    public static string CsvRow(RunStats s)
    {
        bool measured = s.GameCount > 0 && s.Fidelity != SimGoalFidelity.NotSimulated;
        string bottleneck = s.BottleneckGoalIndex >= 0 && s.BottleneckGoalIndex < s.GoalLabels.Length
            ? s.GoalLabels[s.BottleneckGoalIndex] : "-";
        return string.Join(",", new[]
        {
            CsvCell(s.LevelName), CsvCell(s.ProfileName), s.GameCount.ToString(),
            measured ? Number(s.WinRate * 100, "F1") : "",
            measured ? Number(s.WinRateLow * 100, "F1") : "",
            measured ? Number(s.WinRateHigh * 100, "F1") : "",
            CsvCell(s.DifficultyLabel), s.MovesBudget.ToString(),
            measured ? Number(s.AvgMovesOnWin, "F1") : "",
            measured ? Number(s.AvgMovesLeftOnWin, "F1") : "",
            measured ? Number(s.AvgGoalCompletionOnLoss * 100, "F1") : "",
            CsvCell(bottleneck), measured ? Number(s.AvgTilesCleared, "F1") : "",
            measured ? Number(s.AvgSpecialsCreated, "F2") : "",
            measured ? Number(s.AvgCombos, "F2") : "", measured ? s.DeadlockGames.ToString() : "",
            measured ? Number(s.AvgShuffles, "F2") : "", s.Fidelity.ToString(), s.Seed.ToString(),
            CsvCell(string.Join(" | ", s.Warnings))
        });
    }
}
