using UnityEditor;
using UnityEngine;

// TinyFixers > Run Sim Bot
// LevelData seç → oyuncu profili + oyun sayısı → koş → Console'da detaylı rapor.
public class SimRunnerWindow : EditorWindow
{
    private LevelData _level;
    private int _gameCount = 400;
    private int _seed = 42;
    private int _profileIndex = 1;
    private bool _parallel = true;
    private bool _compareProfiles;

    private static readonly string[] ProfileNames = { "Novice", "Average", "Expert", "Perfect" };

    private string _lastReport;
    private Vector2 _scroll;
    private System.Threading.CancellationTokenSource _runCancellation;
    private string _status;

    private void OnDisable() => _runCancellation?.Cancel();

    [MenuItem("TinyFixers/Legacy Headless/Run Sim Bot")]
    public static void Open() => GetWindow<SimRunnerWindow>("Sim Bot").Show();

    private void OnGUI()
    {
        GUILayout.Label("Headless Oyun Simülasyonu", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUI.enabled = _runCancellation == null;
        _level     = (LevelData)EditorGUILayout.ObjectField("Level Data", _level, typeof(LevelData), false);
        _gameCount = EditorGUILayout.IntField("Oyun sayısı", _gameCount);
        _seed      = EditorGUILayout.IntField("Seed", _seed);

        _profileIndex = EditorGUILayout.Popup("Oyuncu profili", _profileIndex, ProfileNames);
        _parallel     = EditorGUILayout.Toggle("Paralel (çok thread)", _parallel);
        _compareProfiles = EditorGUILayout.Toggle("Tüm profilleri karşılaştır", _compareProfiles);

        EditorGUILayout.Space();

        GUI.enabled = _level != null && _gameCount > 0 && _runCancellation == null;
        if (GUILayout.Button(_compareProfiles ? "Karşılaştır" : "Çalıştır", GUILayout.Height(28)))
            RunSim();
        GUI.enabled = true;
        if (_runCancellation != null)
        {
            EditorGUILayout.HelpBox(_status ?? "Simülasyon çalışıyor…", MessageType.Info);
            if (GUILayout.Button("İptal")) _runCancellation.Cancel();
        }
        EditorGUILayout.HelpBox("Oranlar seçilen botun performansıdır. İnsan kazanma oranı ve levelin çözülemezliği anlamına gelmez.", MessageType.Info);

        if (_level == null)
            EditorGUILayout.HelpBox("LevelData asset seç.", MessageType.Info);

        if (!string.IsNullOrEmpty(_lastReport))
        {
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private async void RunSim()
    {
        if (_level == null || _runCancellation != null || _gameCount < 1) return;
        var cancellation = new System.Threading.CancellationTokenSource();
        _runCancellation = cancellation;
        SimRules.ClearCache();
        var sb = new System.Text.StringBuilder();
        try
        {
            var names = _compareProfiles ? ProfileNames : new[] { ProfileNames[_profileIndex] };
            foreach (var name in names)
            {
                _status = $"{_level.name} — {name}, {_gameCount} oyun";
                Repaint();
                var stats = await SimRunner.RunAsync(_level, _gameCount,
                    SimPlayerProfile.ByName(name), _seed, _parallel, cancellation.Token);
                if (sb.Length > 0) sb.AppendLine("\n────────────────────────────");
                sb.AppendLine(SimRunner.FormatStats(stats));
                _lastReport = sb.ToString();
                Repaint();
                if (stats.Fidelity == SimGoalFidelity.NotSimulated) break;
            }
            Debug.Log(_lastReport);
        }
        catch (System.OperationCanceledException)
        {
            _lastReport = sb + "\nKoşu iptal edildi; tamamlanmamış profil rapora katılmadı.";
        }
        catch (System.Exception ex)
        {
            _lastReport = sb + $"\nSimülasyon hatası: {ex.GetBaseException().Message}\nBu koşudan zorluk sonucu çıkarılmadı.";
            Debug.LogException(ex);
        }
        finally
        {
            _runCancellation = null;
            cancellation.Dispose();
            Repaint();
        }
    }
}
