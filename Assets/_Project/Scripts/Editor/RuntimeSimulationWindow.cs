using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs the shipped gameplay scene with an automated player, without editing level assets.</summary>
[InitializeOnLoad]
public sealed class RuntimeSimulationWindow : EditorWindow
{
    private const string PreviousStartSceneKey = "TinyFixers.RuntimeSimulation.PreviousStartScene";
    private const string GameScene = "Assets/_Project/Scenes/01_Game.unity";
    [SerializeField] private LevelData _level;
    [SerializeField] private bool _allLevels;
    [SerializeField] private int _games = 20;
    [SerializeField] private int _seed = 42;
    [SerializeField] private float _speed = 1;
    [SerializeField] private float _timeout = 120;
    [SerializeField] private RuntimeBotPolicy _policy = RuntimeBotPolicy.GoalAware;
    private Vector2 _scroll;
    private string _summary;

    static RuntimeSimulationWindow()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !RuntimeSimulationSession.IsActive) return;
            SessionState.SetBool(RuntimeSimulationSession.ActiveKey, false);
            var previous = SessionState.GetString(PreviousStartSceneKey, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        };
    }

    [MenuItem("TinyFixers/Level Difficulty — Real Game Bot")]
    [MenuItem("TinyFixers/Run Sim Bot")]
    public static void Open() => GetWindow<RuntimeSimulationWindow>("Level Difficulty").Show();

    private void OnEnable()
    {
        if (_level == null) _level = Selection.activeObject as LevelData;
        EditorApplication.update += Repaint;
    }
    private void OnDisable() => EditorApplication.update -= Repaint;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Gerçek oyun motoruyla level testi", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Bot oyun sahnesini açar, taşları tıklar/takas eder ve sonuçları kaydeder. Yeni engeller oyundaki mevcut kodlarıyla çalışır. Kazanma oranı seçilen botun performansıdır.", MessageType.Info);
        bool running = RuntimeSimulationSession.IsActive;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || running))
        {
            _allLevels = EditorGUILayout.Toggle("Tüm production leveller", _allLevels);
            if (!_allLevels) _level = (LevelData)EditorGUILayout.ObjectField("Level", _level, typeof(LevelData), false);
            _games = Mathf.Max(1, EditorGUILayout.IntField("Oyun / level", _games));
            _seed = EditorGUILayout.IntField("Başlangıç seed", _seed);
            _policy = (RuntimeBotPolicy)EditorGUILayout.EnumPopup("Bot", _policy);
            _speed = EditorGUILayout.Slider("Oyun hızı", _speed, 1f, 5f);
            _timeout = Mathf.Clamp(EditorGUILayout.FloatField("Adım zaman aşımı (sn)", _timeout), 5f, 600f);
            if (_speed > 1.01f)
                EditorGUILayout.HelpBox("Hızlı koşu gerçek zamanlı olayların sırasını etkileyebilir. Referans ölçüm için 1× kullan.", MessageType.Info);
            if (GUILayout.Button("Play Mode'da başlat", GUILayout.Height(30))) StartRun();
        }
        string status = SessionState.GetString(RuntimeSimulationSession.StatusKey, "");
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        if (running)
        {
            if (GUILayout.Button("Koşuyu durdur")) SessionState.SetBool(RuntimeSimulationSession.CancelKey, true);
        }
        string reportPath = SessionState.GetString(RuntimeSimulationSession.ReportKey, "");
        if (!string.IsNullOrEmpty(reportPath) && File.Exists(reportPath))
        {
            if (GUILayout.Button("Son raporu oku"))
            {
                try
                {
                    var report = JsonUtility.FromJson<RuntimeSimulationSession.Report>(File.ReadAllText(reportPath));
                    if (report == null || report.config == null || report.games == null)
                        throw new InvalidDataException("Rapor içeriği eksik.");
                    _summary = RuntimeSimulationReport.Summary(report);
                }
                catch (Exception ex) { _summary = "Rapor okunamadı: " + ex.Message; }
            }
            if (GUILayout.Button("JSON + CSV klasörünü göster")) EditorUtility.RevealInFinder(reportPath);
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (!string.IsNullOrEmpty(_summary)) EditorGUILayout.TextArea(_summary, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void StartRun()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene);
        if (scene == null) { Debug.LogError("[RuntimeSim] Gameplay scene not found: " + GameScene); return; }
        string[] paths = _allLevels
            ? AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/_Project/Settings/ProductionLevels" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray()
            : _level != null ? new[] { AssetDatabase.GetAssetPath(_level) } : Array.Empty<string>();
        if (paths.Length == 0) { Debug.LogError("[RuntimeSim] Test edilecek level seç."); return; }
        var config = new RuntimeSimulationSession.Config
        {
            levelPaths = paths, gamesPerLevel = _games, seed = _seed, policy = _policy,
            timeScale = _speed, stepTimeoutSeconds = _timeout, scenePath = GameScene,
            outputPath = Path.GetFullPath(Path.Combine("Library", "LevelSimulation",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"))
        };
        SessionState.SetString(PreviousStartSceneKey, EditorSceneManager.playModeStartScene == null
            ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(RuntimeSimulationSession.ConfigKey, JsonUtility.ToJson(config));
        SessionState.SetBool(RuntimeSimulationSession.CancelKey, false);
        SessionState.SetBool(RuntimeSimulationSession.ActiveKey, true);
        SessionState.SetString(RuntimeSimulationSession.StatusKey, "Başlatılıyor…");
        _summary = null;
        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.EnterPlaymode();
    }
}
