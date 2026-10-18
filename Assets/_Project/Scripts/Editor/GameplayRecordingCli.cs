using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Command-line entry for social gameplay clips (bot plays the real game scene, Game view → ffmpeg):
///   Unity -projectPath … -executeMethod GameplayRecordingCli.Run -recordConfig /path/config.json
/// config.json = RuntimeSimulationSession.Config (levelPaths, gamesPerLevel, recordDir, …).
/// Sets a 1080x1920 Game view, enters Play Mode and quits the editor when the run ends.
/// </summary>
public static class GameplayRecordingCli
{
    private const string GameScene = "Assets/_Project/Scenes/01_Game.unity";
    private const string QuitKey = "TinyFixers.Recording.QuitWhenDone";
    private const string ResolutionName = "Reels 1080x1920";

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-recordConfig");
        if (i < 0 || i + 1 >= args.Length) { Debug.LogError("[Recording] -recordConfig <json> missing."); EditorApplication.Exit(2); return; }
        var config = JsonUtility.FromJson<RuntimeSimulationSession.Config>(File.ReadAllText(args[i + 1]));
        if (config?.levelPaths == null || config.levelPaths.Length == 0 || string.IsNullOrEmpty(config.recordDir))
        { Debug.LogError("[Recording] Config needs levelPaths and recordDir."); EditorApplication.Exit(2); return; }
        if (string.IsNullOrEmpty(config.scenePath)) config.scenePath = GameScene;
        if (string.IsNullOrEmpty(config.outputPath)) config.outputPath = Path.Combine(config.recordDir, "report.json");
        if (config.stepTimeoutSeconds < 300f) config.stepTimeoutSeconds = 600f;   // encoding slows real time

        PrepareGameView(1080, 1920);
        SessionState.SetString(RuntimeSimulationSession.ConfigKey, JsonUtility.ToJson(config));
        SessionState.SetBool(RuntimeSimulationSession.CancelKey, false);
        SessionState.SetBool(RuntimeSimulationSession.ActiveKey, true);
        SessionState.SetBool(QuitKey, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(config.scenePath);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void HookQuit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(QuitKey, false)) return;
            SessionState.SetBool(QuitKey, false);
            SessionState.SetBool(RuntimeSimulationSession.ActiveKey, false);
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        };
    }

    // Game view resolution via the (internal) GameViewSizes API — adds/selects a fixed portrait size.
    private static void PrepareGameView(int width, int height)
    {
        try
        {
            var asm = typeof(EditorWindow).Assembly;
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            var names = (string[])groupType.GetMethod("GetDisplayTexts").Invoke(group, null);
            int index = Array.FindIndex(names, n => n.StartsWith(ResolutionName, StringComparison.Ordinal));
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructors().First(c => c.GetParameters().Length == 4);
                var size = ctor.Invoke(new object[] { Enum.ToObject(sizeKind, 1), width, height, ResolutionName }); // 1 = FixedResolution
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = ((string[])groupType.GetMethod("GetDisplayTexts").Invoke(group, null)).Length - 1;
            }
            var gameView = EditorWindow.GetWindow(gameViewType);
            var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            select?.Invoke(gameView, new object[] { index, null });
            gameView.Focus();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Recording] Game view size could not be set (" + ex.Message + "). Clip uses the current Game view size.");
        }
    }
}
