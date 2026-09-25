using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HIZLI level denetimi — simülasyon koşturmadan (saniyeler içinde) yapısal hataları bulur:
///   • hedef tahtada yok / yetersiz → kazanmak imkânsız
///   • hiç hedef tanımlanmamış
///   • başlangıçta oynanabilir hamle yok
///   • ObstacleLibrary'de tanımı olmayan obstacle
///   • simüle edilmeyen mekanik (win% ölçümü anlamsız olacak leveller)
/// Toplu win% koşusundan ÖNCE bunu çalıştır: burada çıkan level için win% zaten anlamsızdır.
/// </summary>
public static class SimLevelAudit
{
    private const string Dir = "Assets/_Project/Settings/ProductionLevels";

    [MenuItem("TinyFixers/Legacy Headless/Sim — Level Audit (hızlı, simülasyonsuz)")]
    public static void Audit()
    {
        SimRules.ClearCache();

        var levels = AssetDatabase.FindAssets("t:LevelData", new[] { Dir })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null)
            .OrderBy(l => l.name)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"═══ LEVEL DENETİMİ — {levels.Count} level ═══");

        int broken = 0, warned = 0;

        foreach (var level in levels)
        {
            var issues = Inspect(level, out bool fatal);
            if (issues.Count == 0) continue;

            if (fatal) broken++; else warned++;
            sb.AppendLine($"  {(fatal ? "✖" : "~")} {level.name}");
            foreach (var i in issues) sb.AppendLine($"      {i}");
        }

        sb.AppendLine();
        sb.AppendLine($"SONUÇ: {broken} level KAZANILAMAZ, {warned} level uyarılı, " +
                      $"{levels.Count - broken - warned} level temiz.");

        if (broken > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    private static List<string> Inspect(LevelData level, out bool fatal)
    {
        fatal = false;
        var issues = new List<string>();

        if (level.goals == null || level.goals.Length == 0)
        {
            fatal = true;
            issues.Add("hedef tanımlı değil → kazanmak imkânsız");
            return issues;
        }

        var rules = SimRules.From(level.obstacleLibrary);
        var layer = new SimObstacleLayer(level, rules);

        foreach (var g in level.goals)
        {
            if (g.targetType != LevelGoalTargetType.Obstacle) continue;
            if (SimRunner.CanAppearDuringPlay(g.obstacleId, rules)) continue;

            int available = layer.GetInitialCount(g.obstacleId);
            if (available >= g.amount) continue;

            fatal = true;
            issues.Add($"hedef {g.obstacleId} x{g.amount} — levelda {available} tane var → kazanılamaz");
        }

        // Başlangıç tahtası oynanabilir mi?
        var game = new SimGame(level, rules, new System.Random(1));
        game.Start();
        var moves = new List<SimSwap>();
        game.FindMoves(moves);
        if (moves.Count == 0)
        {
            fatal = true;
            issues.Add("başlangıçta hiç geçerli hamle yok (shuffle da açamıyor) → tahta kilitli");
        }

        var missing = rules.MissingDefs;
        if (missing.Length > 0)
            issues.Add($"ObstacleLibrary'de tanımsız: {string.Join(", ", missing)}");

        var goals = new SimGoalSet(level);
        if (goals.Fidelity == SimGoalFidelity.NotSimulated)
            issues.Add("hedefi simüle edilmiyor → win% ölçümü anlamsız");
        else if (goals.Fidelity == SimGoalFidelity.Approximate)
            issues.Add("hedefi yaklaşık simüle ediliyor → win% yön gösterir");

        return issues;
    }

}
