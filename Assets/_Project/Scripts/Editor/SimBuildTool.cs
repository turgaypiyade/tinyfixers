using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Arka plan simülasyonu için penceresiz Mac build'i (RuntimeSimulationSession, TF_SIM_BUILD).
/// Yalnız oyun sahnesi girer; ürün adı geçici olarak "TinyFixersSim" yapılır → PlayerPrefs ayrı dosyada,
/// oyunun kaydına dokunmaz. Mağaza build'leri TF_SIM_BUILD almaz, sim kodu onlara hiç girmez.
/// Çalıştırma: Tools/Sim/run_sim.py (seviye aralığı, oyun sayısı, hız, paralel iş).
/// Komut satırı: Unity -batchmode -quit -projectPath . -executeMethod SimBuildTool.Build
/// </summary>
public static class SimBuildTool
{
    public const string OutputApp = "Builds/Sim/TinyFixersSim.app";
    private const string SimProductName = "TinyFixersSim";

    [MenuItem("TinyFixers/Sim/Build Headless Sim (Mac)")]
    public static void Build()
    {
        string product = PlayerSettings.productName;
        BuildReport report = null;
        try
        {
            PlayerSettings.productName = SimProductName;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/01_Game.unity" },
                locationPathName = OutputApp,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "TF_SIM_BUILD" },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(OutputApp));
            report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[SimBuild] {summary.result} — {summary.totalSize / 1e6:0} MB, {summary.totalTime.TotalSeconds:0} sn → {OutputApp}");
        }
        finally
        {
            PlayerSettings.productName = product;   // proje ayarı değişmeden kalsın
            AssetDatabase.SaveAssets();
        }
        if (Application.isBatchMode && (report == null || report.summary.result != BuildResult.Succeeded))
            EditorApplication.Exit(1);
    }
}
