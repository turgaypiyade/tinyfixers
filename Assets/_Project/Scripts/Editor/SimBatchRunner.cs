using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tüm ProductionLevels'ı headless koşturup toplu zorluk tablosu çıkarır.
/// Menü: TinyFixers > Run Sim Bot — All Production Levels.
/// Sonuç Console'a + CSV'ye (ProductionLevels/_SimStats.csv) yazılır.
///
/// Win% = simüle edilen ortalama oyuncunun kazanma oranı; yanındaki güven aralığı
/// "bu fark gerçek mi yoksa gürültü mü" sorusunun cevabıdır.
/// </summary>
public static class SimBatchRunner
{
    private const string Dir = "Assets/_Project/Settings/ProductionLevels";
    private const int GamesPerLevel = 300;
    private const int Seed = 42;

    [MenuItem("TinyFixers/Legacy Headless/Run Sim Bot — All Production Levels")]
    public static async void RunAll() => await RunBatch(false);

    [MenuItem("TinyFixers/Legacy Headless/Run Sim Bot — All Levels (Novice + Average + Expert)")]
    public static async void RunAllProfiles() => await RunBatch(true);

    private static System.Threading.CancellationTokenSource _cancellation;

    [MenuItem("TinyFixers/Legacy Headless/Sim — Cancel Batch")]
    public static void CancelBatch() => _cancellation?.Cancel();

    private static async System.Threading.Tasks.Task RunBatch(bool allProfiles)
    {
        if (_cancellation != null) { Debug.LogWarning("[SimBatch] Bir koşu zaten çalışıyor."); return; }
        var cancellation = new System.Threading.CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            if (allProfiles)
            {
                await RunAll(SimPlayerProfile.Novice, "_SimStats_novice.csv", cancellation.Token);
                await RunAll(SimPlayerProfile.Average, "_SimStats.csv", cancellation.Token);
                await RunAll(SimPlayerProfile.Expert, "_SimStats_expert.csv", cancellation.Token);
            }
            else await RunAll(SimPlayerProfile.Average, "_SimStats.csv", cancellation.Token);
        }
        catch (System.OperationCanceledException) { Debug.Log("[SimBatch] İptal edildi. Tamamlanan leveller CSV'ye kaydedildi."); }
        catch (System.Exception ex) { Debug.LogException(ex); }
        finally { _cancellation = null; cancellation.Dispose(); }
    }

    private static async System.Threading.Tasks.Task RunAll(SimPlayerProfile profile, string fileName,
        System.Threading.CancellationToken cancellationToken)
    {
        SimRules.ClearCache();

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

        var csv = new StringBuilder();
        csv.AppendLine(SimRunner.CsvHeader());

        var console = new StringBuilder();
        console.AppendLine($"[SimBatch] {profile.Name} profili — {levels.Count} level × {GamesPerLevel} oyun");
        console.AppendLine($"{"Level",-22}{"Win%",7}{"Aralık",14}  {"Zorluk",-11}{"Tıkayan hedef",-24}Uyarı");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
        for (int i = 0; i < levels.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var level = levels[i];
            Debug.Log($"[SimBatch] {profile.Name}: {level.name} ({i + 1}/{levels.Count}) — TinyFixers/Sim — Cancel Batch ile durdurulabilir.");
            var s = await SimRunner.RunAsync(level, GamesPerLevel, profile, Seed, true, cancellationToken);
            csv.AppendLine(SimRunner.CsvRow(s));

            string bottleneck = s.BottleneckGoalIndex >= 0 && s.GoalLabels.Length > 1
                ? s.GoalLabels[s.BottleneckGoalIndex] : "-";
            string warn = s.Fidelity == SimGoalFidelity.NotSimulated ? "⚠ tam simüle edilmiyor"
                        : s.Fidelity == SimGoalFidelity.Approximate ? "~ yaklaşık" : "";

            if (s.Fidelity == SimGoalFidelity.NotSimulated)
            {
                console.AppendLine($"{level.name,-22}     —   MODELLENMİYOR: {string.Join(" | ", s.Warnings)}");
                continue;
            }
            console.AppendLine(
                $"{level.name,-22}{s.WinRate * 100f,6:F0}%  [{s.WinRateLow * 100f,3:F0}–{s.WinRateHigh * 100f,3:F0}]  " +
                $"{s.DifficultyLabel,-11}{bottleneck,-24}{warn}");
        }

        }
        finally
        {
        sw.Stop();

        string path = $"{Dir}/{fileName}";
        File.WriteAllText(path, csv.ToString());
        AssetDatabase.Refresh();

        console.AppendLine();
        console.AppendLine($"Süre: {sw.ElapsedMilliseconds} ms → {path}");
        Debug.Log(console.ToString());
        }
    }
}
