using UnityEditor;
using UnityEngine;

// TinyFixers > Run Sim Bot
// Select a LevelData asset → set game count → run → see stats in Console.
public class SimRunnerWindow : EditorWindow
{
    private LevelData _level;
    private int _gameCount = 500;
    private int _seed = 42;

    [MenuItem("TinyFixers/Run Sim Bot")]
    public static void Open()
    {
        GetWindow<SimRunnerWindow>("Sim Bot").Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Random Bot Simulation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _level = (LevelData)EditorGUILayout.ObjectField("Level Data", _level, typeof(LevelData), false);
        _gameCount = EditorGUILayout.IntField("Games", _gameCount);
        _seed = EditorGUILayout.IntField("Seed", _seed);

        EditorGUILayout.Space();

        GUI.enabled = _level != null;
        if (GUILayout.Button("Run"))
            RunSim();
        GUI.enabled = true;

        if (_level == null)
            EditorGUILayout.HelpBox("LevelData asset seç.", MessageType.Info);
    }

    private void RunSim()
    {
        if (_level == null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stats = SimRunner.Run(_level, _gameCount, _seed);
        sw.Stop();

        string report = SimRunner.FormatStats(stats, _level);
        report += $"\n  Elapsed              : {sw.ElapsedMilliseconds} ms";

        Debug.Log(report);
    }
}
