using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tüm ProductionLevels'ı headless SimRunner ile oynatıp toplu istatistik tablosu çıkarır.
/// Menü: TinyFixers > Run Sim Bot — All Production Levels.
/// Sonuç Console'a + CSV'ye (ProductionLevels/_SimStats.csv) yazılır → hızlıca paylaşılır.
///
/// Win% = ortalama oyuncu (smart sim bot) kazanma oranı. Zorluk kalibrasyonu için taban veri.
/// </summary>
public static class SimBatchRunner
{
    private const string Dir = "Assets/_Project/Settings/ProductionLevels";
    private const int GamesPerLevel = 150;   // akıllı bot daha ağır; 150 yeterli istatistik
    private const int Seed = 42;

    [MenuItem("TinyFixers/Run Sim Bot — All Production Levels")]
    public static void RunAll()
    {
        var levels = AssetDatabase.FindAssets("t:LevelData", new[] { Dir })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null)
            .OrderBy(l => l.name)
            .ToList();

        if (levels.Count == 0)
        {
            Debug.LogError($"[SimBatch] {Dir} altında LevelData bulunamadı.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Level,Win%,AvgMovesWin,AvgMovesLoss,Moves,ObsInLevel,AvgObsClears,Deadlocks");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int i = 0;
        foreach (var level in levels)
        {
            EditorUtility.DisplayProgressBar("Sim Bot — All Levels",
                $"{level.name} ({i + 1}/{levels.Count})", (float)i / levels.Count);

            var s = SimRunner.Run(level, GamesPerLevel, Seed);
            sb.AppendLine(
                $"{level.name}," +
                $"{s.WinRate * 100f:F0}," +
                $"{s.AvgMovesOnWin:F0}," +
                $"{s.AvgMovesOnLoss:F0}," +
                $"{level.moves}," +
                $"{s.ObstacleCountInLevel}," +
                $"{s.AvgObstacleClearsPerGame:F0}," +
                $"{s.TotalDeadlockMoves}");
            i++;
        }
        sw.Stop();
        EditorUtility.ClearProgressBar();

        string csv = sb.ToString();
        string path = Dir + "/_SimStats.csv";
        File.WriteAllText(path, csv);
        AssetDatabase.Refresh();

        Debug.Log($"[SimBatch] {levels.Count} level × {GamesPerLevel} oyun, {sw.ElapsedMilliseconds} ms\n{csv}\n→ {path}");
    }
}
