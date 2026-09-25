using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Simülasyonun DOĞRU çalıştığını birkaç levelda hızlıca gösteren teşhis aracı.
///   • Sim — Quick Check : 5 level × 100 oyun, tam rapor + akıl sağlığı kontrolleri.
///   • Sim — Trace One Game : tek oyunu hamle hamle, tahta çizimiyle birlikte basar.
/// Toplu koşuyu (tüm leveller) ancak bunlar temiz çıktıktan sonra çalıştır.
/// </summary>
public static class SimSmokeTest
{
    private const string Dir = "Assets/_Project/Settings/ProductionLevels";
    private const int Games = 100;

    // ── 1) Hızlı kontrol ─────────────────────────────────────────────────────

    [MenuItem("TinyFixers/Legacy Headless/Sim — Quick Check (5 level)")]
    public static void QuickCheck()
    {
        SimRules.ClearCache();
        var levels = PickSpread(5);
        if (levels.Count == 0) { Debug.LogError("[SimCheck] Level bulunamadı."); return; }

        var sb = new StringBuilder();
        var problems = new List<string>();

        sb.AppendLine($"═══ SİM HIZLI KONTROL — {levels.Count} level × {Games} oyun ═══");

        foreach (var level in levels)
        {
            var s = SimRunner.Run(level, Games, SimPlayerProfile.Average, 7);
            sb.AppendLine();
            sb.AppendLine(SimRunner.FormatStats(s));

            // ── Akıl sağlığı kontrolleri ──
            if (s.AvgMovesUsed <= 0.5f)
                problems.Add($"{level.name}: bot neredeyse hiç hamle yapmıyor ({s.AvgMovesUsed:F1}) — hamle üretimi bozuk olabilir.");
            if (s.AvgTilesCleared <= 1f)
                problems.Add($"{level.name}: taş temizlenmiyor ({s.AvgTilesCleared:F1}) — eşleşme/cascade bozuk olabilir.");
            if (s.AvgMovesUsed > level.moves)
                problems.Add($"{level.name}: hamle bütçesi aşılmış ({s.AvgMovesUsed:F1} > {level.moves}).");
            if (s.WinRate >= 0.999f && s.AvgMovesLeftOnWin > level.moves * 0.6f)
                problems.Add($"{level.name}: %100 win + hamlelerin %{100f * s.AvgMovesLeftOnWin / level.moves:F0}'ı artıyor — hedefler bedava tamamlanıyor olabilir.");
            if (s.AvgSpecialsCreated <= 0.01f && level.moves > 10)
                problems.Add($"{level.name}: hiç special üretilmiyor — special oluşturma kuralları bozuk olabilir.");
            if (s.Fidelity == SimGoalFidelity.NotSimulated)
                problems.Add($"{level.name}: hedefi simüle edilmiyor — bu levelın win%'i zaten anlamsız (beklenen durum).");
        }

        sb.AppendLine();
        sb.AppendLine("═══ SONUÇ ═══");
        if (problems.Count == 0)
            sb.AppendLine("Tüm akıl sağlığı kontrolleri geçti.");
        else
            foreach (var p in problems) sb.AppendLine("  • " + p);

        Debug.Log(sb.ToString());
    }

    // ── 1b) Tek level derin inceleme ─────────────────────────────────────────

    /// <summary>
    /// Tek bir levelı 4 profille koşturur + tek oyun izi basar.
    /// Batch mode: -executeMethod SimSmokeTest.CheckOne, level adı SIM_LEVEL ortam değişkeninden.
    /// </summary>
    [MenuItem("TinyFixers/Legacy Headless/Sim — Check One Level (SIM_LEVEL)")]
    public static void CheckOne()
    {
        string name = System.Environment.GetEnvironmentVariable("SIM_LEVEL");
        if (string.IsNullOrEmpty(name)) { Debug.LogError("[SimCheck] SIM_LEVEL ortam değişkeni boş."); return; }

        var level = AssetDatabase.LoadAssetAtPath<LevelData>($"{Dir}/{name}.asset");
        if (level == null) { Debug.LogError($"[SimCheck] {name} bulunamadı."); return; }

        SimRules.ClearCache();

        var sb = new StringBuilder();
        sb.AppendLine($"═══ {name} DERİN İNCELEME ═══");
        sb.AppendLine($"  {level.width}x{level.height}, {level.moves} hamle");
        sb.AppendLine();
        sb.AppendLine($"  {"Profil",-9}{"Win%",7}{"Aralık",14}  {"Kazanınca",11}{"Artan hamle",13}  Zorluk");

        foreach (var profileName in new[] { "Novice", "Average", "Expert", "Perfect" })
        {
            var p = SimPlayerProfile.ByName(profileName);
            var st = SimRunner.Run(level, 300, p, 7);
            sb.AppendLine($"  {profileName,-9}{st.WinRate * 100f,6:F0}%   " +
                          $"[{st.WinRateLow * 100f,3:F0}–{st.WinRateHigh * 100f,3:F0}]  " +
                          $"{st.AvgMovesOnWin,10:F1}{st.AvgMovesLeftOnWin,12:F1}   {st.DifficultyLabel}");
        }

        sb.AppendLine();
        sb.Append(SimRunner.FormatStats(SimRunner.Run(level, 300, SimPlayerProfile.Average, 7)));

        Debug.Log(sb.ToString());
        Trace(level, seed: 4242, maxMovesToPrint: 0);
    }

    // ── 2) Tek oyun izi ──────────────────────────────────────────────────────

    [MenuItem("TinyFixers/Legacy Headless/Sim — Trace One Game")]
    public static void TraceOneGame()
    {
        SimRules.ClearCache();
        var level = PickSpread(1).FirstOrDefault();
        if (level == null) { Debug.LogError("[SimTrace] Level bulunamadı."); return; }
        Trace(level, seed: 12345, maxMovesToPrint: 6);
    }

    public static void Trace(LevelData level, int seed, int maxMovesToPrint)
    {
        var rules = SimRules.From(level.obstacleLibrary);
        var rng   = new System.Random(seed);
        var game  = new SimGame(level, rules, rng);
        var bot   = new SimBot(SimPlayerProfile.Average);

        game.Start();

        var sb = new StringBuilder();
        sb.AppendLine($"═══ TEK OYUN İZİ — {level.name} ({level.width}x{level.height}, {level.moves} hamle) ═══");
        sb.AppendLine(GoalsLine(game));
        sb.AppendLine("Başlangıç tahtası:");
        sb.AppendLine(Draw(game));

        int printed = 0;
        while (game.MovesLeft > 0 && !game.Goals.AllMet)
        {
            var swap = bot.PickMove(game, rng);
            if (swap == null)
            {
                sb.AppendLine("  (hamle kalmadı → shuffle)");
                if (!game.TryShuffle()) { sb.AppendLine("  shuffle da kurtaramadı — oyun bitti."); break; }
                continue;
            }

            var stats = game.PlayMove(swap.Value);

            sb.AppendLine($"  #{game.MovesUsed,2} {swap.Value,-22} taş={stats.TilesCleared,3} " +
                          $"sp+={stats.SpecialsCreated} sp!={stats.SpecialsActivated} " +
                          $"obs={stats.ObstaclesCleared}  |  {GoalsLine(game)}  |  magnet: {game.Obstacles.DescribeMagnets()}");

            if (printed < maxMovesToPrint)
            {
                sb.AppendLine($"── Hamle {game.MovesUsed}: {swap.Value}   " +
                              $"taş={stats.TilesCleared} special+={stats.SpecialsCreated} " +
                              $"special!={stats.SpecialsActivated} combo={stats.CombosActivated} " +
                              $"cascade={stats.CascadeSteps} obstacle={stats.ObstaclesCleared}");
                sb.AppendLine($"   hedef: {GoalsLine(game)}");
                sb.AppendLine(Draw(game));
                printed++;
            }
        }

        sb.AppendLine($"── SONUÇ: {(game.Goals.AllMet ? "KAZANDI" : "kaybetti")}  " +
                      $"{game.MovesUsed}/{level.moves} hamle, kalan {game.MovesLeft}, shuffle {game.Shuffles}");
        sb.AppendLine($"   toplam: taş={game.Totals.TilesCleared} special={game.Totals.SpecialsCreated} " +
                      $"combo={game.Totals.CombosActivated} en uzun zincir={game.Totals.MaxChain}");
        sb.AppendLine($"   {GoalsLine(game)}");

        Debug.Log(sb.ToString());
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static string GoalsLine(SimGame game)
    {
        var parts = new List<string>();
        for (int i = 0; i < game.Goals.GoalCount; i++)
        {
            var e = game.Goals.GetEntry(i);
            parts.Add($"{e.Label} {e.Done}/{e.Needed}{(e.Met ? " ✓" : "")}");
        }
        return parts.Count == 0 ? "(hedef yok)" : string.Join("   ", parts);
    }

    /// <summary>Tahtayı okunur biçimde çizer: harf=taş, büyük harf+işaret=special, #=engel, ·=boş.</summary>
    private static string Draw(SimGame game)
    {
        var s = game.State;
        var sb = new StringBuilder();

        for (int y = 0; y < s.Height; y++)
        {
            sb.Append("    ");
            for (int x = 0; x < s.Width; x++)
            {
                var obstacle = game.Obstacles.ObstacleIdAt(x, y);
                var tile = s.Grid[x, y];

                string cell;
                if (tile != null) cell = tile.ToDebugString();
                else if (obstacle != ObstacleId.None) cell = "#";
                else if (s.Holes[x, y]) cell = " ";
                else cell = "·";

                if (obstacle != ObstacleId.None && tile != null) cell += "*";   // taşın altında/üstünde engel
                sb.Append(cell.PadRight(3));
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static List<LevelData> PickSpread(int count)
    {
        var all = AssetDatabase.FindAssets("t:LevelData", new[] { Dir })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null && l.levelKind != LevelKind.BossDuel)
            .OrderBy(l => l.name)
            .ToList();

        if (all.Count <= count) return all;

        var picked = new List<LevelData>();
        for (int i = 0; i < count; i++)
            picked.Add(all[(int)((long)i * (all.Count - 1) / System.Math.Max(1, count - 1))]);
        return picked.Distinct().ToList();
    }
}
