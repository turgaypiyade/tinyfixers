using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automation of the REAL gameplay scene. No obstacle, cascade, refill, combo, or goal rules are
/// implemented here. Runs in the Editor (Play Mode) and in the headless SIM build (TF_SIM_BUILD,
/// `-batchmode -nographics -tfsim config.json`) so batches run unattended in the background.
/// Store builds never compile the runner: IsActive is always false there.
/// </summary>
public sealed class RuntimeSimulationSession : MonoBehaviour
{
    public const string ActiveKey = "TinyFixers.RuntimeSimulation.Active";
    public const string ConfigKey = "TinyFixers.RuntimeSimulation.Config";
    public const string ReportKey = "TinyFixers.RuntimeSimulation.Report";
    public const string StatusKey = "TinyFixers.RuntimeSimulation.Status";
    public const string CancelKey = "TinyFixers.RuntimeSimulation.Cancel";

    public static bool IsActive
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool(ActiveKey, false);
#elif TF_SIM_BUILD
            // Komut satırından okunur: BeforeSceneLoad kontrolleri (Firebase/Analytics kapatma) sıradan bağımsız.
            return SimArgs.Active;
#else
            return false;
#endif
        }
    }

    public static LevelData CurrentLevel { get; private set; }

    /// <summary>SIM build: oynanan katalog seviye numarası. Asset yolu build'de yok; GridSpawner katalogdan çözer.</summary>
    public static int CurrentLevelNumber { get; private set; }

#if TF_SIM_BUILD
    private static class SimArgs
    {
        private static bool parsed;
        private static string configPath;
        public static bool Active { get { Parse(); return configPath != null; } }
        public static string ConfigPath { get { Parse(); return configPath; } }
        private static void Parse()
        {
            if (parsed) return;
            parsed = true;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-tfsim") configPath = args[i + 1];
        }
    }
#endif

    [Serializable]
    public sealed class Config
    {
        public string[] levelPaths;
        public int[] levelNumbers;      // SIM build: katalog seviye numaraları (levelPaths yerine)
        public string scenePath = "Assets/_Project/Scenes/01_Game.unity";
        public int gamesPerLevel = 20;
        public int seed = 42;
        public RuntimeBotPolicy policy = RuntimeBotPolicy.GoalAware;
        public float timeScale = 1f;
        public float stepTimeoutSeconds = 120f;
        public string outputPath;
        // Social-clip recording (editor only). recordDir empty = normal simulation.
        public string recordDir;
        public string ffmpegPath = "/opt/homebrew/bin/ffmpeg";
        public int recordFps = 30;
        public float thinkSeconds = 0.75f;     // human-like pause before each move (game time)
        public float tailSeconds = 2.5f;       // keep filming after the last move
    }

    [Serializable]
    public sealed class GameResult
    {
        public string level;
        public string levelPath;
        public int sample, seed, movesUsed, movesLeft, rejectedInputs;
        public string outcome; // Won, Lost, NoMoves, Timeout, Error, Cancelled. Only Won/Lost enter win%.
        public string detail;
        public float elapsedSeconds;
        public int remainingGoalUnits;
        // Hedef bazında kalan: "SpreadingGel=40;KeyGenerator=12" — hangi hedefin takıldığını gösterir.
        public string goalsLeft;
        public string goalsStart;   // aynı biçim, seviye verisindeki başlangıç miktarları
        public string emptyCellsAtEnd;
        public List<string> trace = new();
    }

    [Serializable]
    public sealed class Report
    {
        public string engine = "Live BoardController / gameplay scene";
        public string unityVersion;
        public string note = "Bot performance, not a calibrated human win rate. Unity seed is diagnostic; frame timing can affect replay.";
        public Config config;
        public List<GameResult> games = new();
        public bool completed;
    }

#if UNITY_EDITOR || TF_SIM_BUILD
    private Config _config;
    private Report _report;
    private bool _forcedFail;
    private string _runtimeError;
    private float _oldTimeScale;
    private bool _oldRunInBackground;
    private UnityEngine.Random.State _oldRandomState;
    private GameResult _activeResult;
    private BoardController _activeBoard;
    private int _movesAtStart;
    private float _gameStarted;
    private bool _finished;
    private bool _sceneLoadFailed;
#if UNITY_EDITOR
    private RuntimeGameplayRecorder _recorder;
    private bool Recording => _recorder != null;
#else
    private bool Recording => false;
#endif
    private int LevelCount => _config.levelPaths != null && _config.levelPaths.Length > 0
        ? _config.levelPaths.Length : _config.levelNumbers != null ? _config.levelNumbers.Length : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        CurrentLevel = null;
        CurrentLevelNumber = 0;
        if (!IsActive) return;
#if UNITY_EDITOR
        var config = JsonUtility.FromJson<Config>(UnityEditor.SessionState.GetString(ConfigKey, ""));
#else
        Config config = null;
        try { config = JsonUtility.FromJson<Config>(File.ReadAllText(SimArgs.ConfigPath)); }
        catch (Exception ex) { Debug.LogError("[Sim] Config okunamadı: " + ex.Message); Application.Quit(2); return; }
#endif
        bool hasLevels = config != null && ((config.levelPaths != null && config.levelPaths.Length > 0)
            || (config.levelNumbers != null && config.levelNumbers.Length > 0));
        if (!hasLevels) return;
        var host = new GameObject("Runtime Level Simulation");
        DontDestroyOnLoad(host);
        var session = host.AddComponent<RuntimeSimulationSession>();
        session._config = config;
        session._report = new Report { config = config, unityVersion = Application.unityVersion };
        session._oldTimeScale = Time.timeScale;
        session._oldRandomState = UnityEngine.Random.state;
        session._oldRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = config.timeScale;
        // Before any scene Awake/Start: every real level consumer sees the selected asset.
        session.SelectLevel(0);
        UnityEngine.Random.InitState(config.seed);
        Application.logMessageReceived += session.OnLog;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(config.recordDir))
            session._recorder = RuntimeGameplayRecorder.Create(config.ffmpegPath, config.recordFps);
#endif
    }

    // Editör: asset yolu (davranış eskisi gibi). SIM build: katalog numarası — asset GridSpawner'da katalogdan
    // çözülür ve CurrentLevel.Global oynanan seviyeye eşitlenir (seviye numarasına bağlı kurallar — refill special
    // koruması vb. — doğru seviyeyle çalışsın). SIM build'in PlayerPrefs'i ayrı ürün adında, oyunun kaydına dokunmaz.
    private string SelectLevel(int index)
    {
#if UNITY_EDITOR
        if (_config.levelPaths != null && index < _config.levelPaths.Length)
        {
            CurrentLevel = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(_config.levelPaths[index]);
            CurrentLevelNumber = 0;
            return _config.levelPaths[index];
        }
#endif
        CurrentLevel = null;
        CurrentLevelNumber = _config.levelNumbers != null && index < _config.levelNumbers.Length ? _config.levelNumbers[index] : 0;
#if TF_SIM_BUILD
        if (CurrentLevelNumber > 0) PlayerPrefs.SetInt(global::CurrentLevel.PrefsKey, CurrentLevelNumber);
#endif
        return "L" + CurrentLevelNumber.ToString("000");
    }

    private IEnumerator Start()
    {
        if (_config == null) yield break;
        bool first = true;
        for (int levelIndex = 0; levelIndex < LevelCount; levelIndex++)
        {
            for (int sample = 0; sample < _config.gamesPerLevel; sample++)
            {
                if (Cancelled) { Finish(false); yield break; }
                int seed = unchecked(_config.seed + sample * 7919);
                string levelPath = SelectLevel(levelIndex);
                var result = new GameResult { levelPath = levelPath, level = CurrentLevel != null ? CurrentLevel.name : levelPath,
                    sample = sample, seed = seed };
                // Preserve errors emitted by Awake/Start in the first gameplay scene.
                if (!first) _runtimeError = null;
                _forcedFail = false;
                _activeResult = result;
                _activeBoard = null;
                _gameStarted = Time.realtimeSinceStartup;
                _report.games.Add(result);
                if (CurrentLevel == null && CurrentLevelNumber <= 0)
                {
                    result.outcome = "Error"; result.detail = "Level asset not found.";
                }
                else
                {
                    SetStatus($"{result.level}: {sample + 1}/{_config.gamesPerLevel} — {_config.policy}");
                    if (!first)
                    {
                        UnityEngine.Random.InitState(seed);
                        yield return LoadGameScene(result);
                    }
#if UNITY_EDITOR
                    if (Recording)
                        _recorder.BeginClip(Path.Combine(_config.recordDir, $"{result.level}_s{sample:00}.mp4"));
#endif
                    if (string.IsNullOrEmpty(result.outcome))
                        yield return GuardGame(PlayGame(result), result);
#if UNITY_EDITOR
                    if (Recording)
                    {
                        yield return new WaitForSeconds(_config.tailSeconds);
                        _recorder.EndClip();
                    }
#endif
                }
                first = false;
                CaptureResult(result);
                _activeResult = null;
                _activeBoard = null;
                SaveReport();
                if (Cancelled) { Finish(false); yield break; }
                // A failed scene load may still be running; never start another load over it.
                if (_sceneLoadFailed) { Finish(false); yield break; }
            }
        }
        Finish(true);
    }

    private IEnumerator LoadGameScene(GameResult result)
    {
        AsyncOperation loading = null;
        try { loading = SceneManager.LoadSceneAsync(_config.scenePath, LoadSceneMode.Single); }
        catch (Exception ex) { result.outcome = "Error"; result.detail = ex.ToString(); }
        if (loading == null)
        {
            result.outcome = "Error";
            result.detail ??= "Gameplay scene could not be loaded.";
            _sceneLoadFailed = true;
            yield break;
        }
        float started = Time.realtimeSinceStartup;
        while (!loading.isDone)
        {
            if (Cancelled || _runtimeError != null || Time.realtimeSinceStartup - started > _config.stepTimeoutSeconds)
            {
                result.outcome = Cancelled ? "Cancelled" : _runtimeError != null ? "Error" : "Timeout";
                result.detail = Cancelled ? "Stopped by user." : _runtimeError ?? "Gameplay scene load timed out.";
                _sceneLoadFailed = true;
                yield break;
            }
            yield return null;
        }
    }

    // Unity normally terminates a coroutine on an exception, which would strand the
    // batch in Play Mode. Drive nested enumerators here so the failed sample is saved.
    private IEnumerator GuardGame(IEnumerator routine, GameResult result)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0 && !Cancelled)
            {
                var current = stack.Peek();
                bool hasNext = false;
                object yielded = null;
                Exception failure = null;
                try { hasNext = current.MoveNext(); if (hasNext) yielded = current.Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null)
                {
                    result.outcome = "Error";
                    result.detail = failure.ToString();
                    yield break;
                }
                if (!hasNext) { stack.Pop(); (current as IDisposable)?.Dispose(); }
                else if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private IEnumerator PlayGame(GameResult result)
    {
        float started = Time.realtimeSinceStartup;
        BoardController board = null;
        while (board == null || board.ActiveLevelData == null || board.TopHud == null || !board.TopHud.IsInitialized)
        {
            if (Cancelled) yield break;
            if (Time.realtimeSinceStartup - started > _config.stepTimeoutSeconds || _runtimeError != null)
            {
                result.outcome = _runtimeError == null ? "Timeout" : "Error";
                result.detail = _runtimeError ?? "Gameplay scene did not initialize.";
                yield break;
            }
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        _activeBoard = board;
        _movesAtStart = board.RemainingMoves;
        result.goalsStart = DescribeGoals(board.ActiveLevelData);
        if (CurrentLevel == null && board.ActiveLevelData != null)
            result.level = $"L{CurrentLevelNumber:000} {board.ActiveLevelData.name}";
        board.OnLevelFailRequested += OnForcedFail;
        try
        {
            var bot = new RuntimeSimulationBot(board, _config.policy, result.seed);
            var rejected = new HashSet<RuntimeBotMove>();
            float waitingSince = Time.realtimeSinceStartup;
            int stableFrames = 0;
            bool resolvingNoMoves = false;
            while (!Cancelled)
            {
                if (_runtimeError != null)
                {
                    result.outcome = "Error"; result.detail = _runtimeError; break;
                }
                // The logical resolve can end before detached falls/clears finish.
                // Wait for both the flow scheduler and cell presentation before previewing inputs.
                bool working = IsBoardWorking(board, out string pendingWork);
                if (working || ++stableFrames < 3)
                {
                    if (working) stableFrames = 0;
                    if (Time.realtimeSinceStartup - waitingSince > _config.stepTimeoutSeconds)
                    {
                        result.outcome = "Timeout";
                        result.detail = pendingWork + "; " + board.DescribePendingAutoResolveForLevelEnd()
                            + "; emptySlots=" + DescribeEmptyCells(board);
                        break;
                    }
                    yield return null;
                    continue;
                }
                if (board.TopHud.AreAllGoalsCompleted) { result.outcome = "Won"; break; }
                if (_forcedFail || board.RemainingMoves <= 0) { result.outcome = "Lost"; break; }

                RuntimeBotMove? move = bot.PickMove(rejected);
                if (!move.HasValue)
                {
                    if (!resolvingNoMoves && rejected.Count == 0)
                    {
                        resolvingNoMoves = true;
                        // The real resolver performs the game's automatic deadlock shuffle.
                        yield return ResolveDeadlock(board, result);
                        if (!string.IsNullOrEmpty(result.outcome)) break;
                        waitingSince = Time.realtimeSinceStartup; stableFrames = 0;
                        continue;
                    }
                    result.outcome = rejected.Count > 0 ? "Error" : "NoMoves";
                    result.detail = rejected.Count > 0 ? "All generated inputs were rejected by the live board." : "No input after the live resolver/shuffle.";
                    break;
                }

                if (Recording) yield return new WaitForSeconds(_config.thinkSeconds);
                int before = board.RemainingMoves;
                result.trace.Add($"moves={before}; board={bot.BoardFingerprint()}; emptySlots={DescribeEmptyCells(board)}; {move.Value}");
                bot.Submit(move.Value); // Exactly the same input entry points as clicking/dragging.
                yield return null;
                float submitted = Time.realtimeSinceStartup;
                while (board.RemainingMoves == before && IsBoardWorking(board, out _)
                    && Time.realtimeSinceStartup - submitted < _config.stepTimeoutSeconds && !Cancelled && _runtimeError == null)
                    yield return null;
                if (_runtimeError != null)
                {
                    result.outcome = "Error"; result.detail = _runtimeError; break;
                }
                if (!Cancelled && board.RemainingMoves == before && IsBoardWorking(board, out pendingWork))
                {
                    result.outcome = "Timeout";
                    result.detail = "Live board did not finish processing the submitted input. "
                        + pendingWork + "; " + board.DescribePendingAutoResolveForLevelEnd();
                    break;
                }
                if (board.RemainingMoves == before)
                {
                    rejected.Add(move.Value);
                    result.rejectedInputs++;
                }
                else { rejected.Clear(); resolvingNoMoves = false; }
                waitingSince = Time.realtimeSinceStartup;
                stableFrames = 0;
            }
        }
        finally { if (board != null) board.OnLevelFailRequested -= OnForcedFail; }
    }

    private static bool IsBoardWorking(BoardController board, out string reason)
    {
        if (board.Flow.IsSettling || board.ActiveBackgroundJobs > 0 || board.InputLocked)
        {
            reason = $"flowSettling={board.Flow.IsSettling}, detached={board.DetachedSequencerActions}, "
                + $"activities={board.Flow.BlockingActivityCount}, jobs={board.ActiveBackgroundJobs}, inputLocked={board.InputLocked}";
            return true;
        }
        for (int x = 0; x < board.Width; x++)
        {
            if (board.IsColumnFallVisualInFlight(x) || board.Flow.IsInputColumnBlocked(x))
            {
                reason = $"Column {x} still falling/clearing.";
                return true;
            }
            for (int y = 0; y < board.Height; y++)
            {
                if (!board.TryGetCellState(x, y, out var cell)) continue;
                if (cell.isReservedForTile || (cell.hasTile && cell.tileRuntimeState != TileRuntimeState.Idle))
                {
                    reason = $"Cell ({x},{y}) reserved={cell.isReservedForTile}, state={cell.tileRuntimeState}";
                    return true;
                }
            }
        }
        bool pending = board.HasPendingAutoResolveForLevelEnd();
        reason = pending ? "Automatic board resolution pending." : null;
        return pending;
    }

    private static string DescribeEmptyCells(BoardController board)
    {
        var cells = new List<string>();
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.TryGetCellState(x, y, out var cell) || !cell.canContainTile || cell.hasTile) continue;
                // Keep isolated pockets visible in diagnostics without treating them as
                // fillable errors. The live cascade owns reachability and refill rules.
                string above = y > 0 && board.TryGetCellState(x, y - 1, out var upper) && upper.isObstacleBlocked
                    ? $", blockedAbove={upper.obstacleId}" : "";
                cells.Add($"({x},{y}{above})");
            }
        return cells.Count == 0 ? "none" : string.Join(" ", cells);
    }

    private IEnumerator ResolveDeadlock(BoardController board, GameResult result)
    {
        bool resolved = false;
        IEnumerator Resolve()
        {
            yield return board.ResolveInitial();
            resolved = true;
        }
        var resolver = StartCoroutine(GuardGame(Resolve(), result));
        float started = Time.realtimeSinceStartup;
        try
        {
            while (!resolved && !Cancelled && string.IsNullOrEmpty(result.outcome))
            {
                if (_runtimeError != null || Time.realtimeSinceStartup - started > _config.stepTimeoutSeconds)
                {
                    result.outcome = _runtimeError == null ? "Timeout" : "Error";
                    result.detail = _runtimeError ?? "Live deadlock resolver/shuffle timed out.";
                    yield break;
                }
                yield return null;
            }
        }
        finally { if (resolver != null) StopCoroutine(resolver); }
    }

    private void CaptureResult(GameResult result)
    {
        result.elapsedSeconds = Time.realtimeSinceStartup - _gameStarted;
        if (string.IsNullOrEmpty(result.outcome))
        {
            result.outcome = "Cancelled";
            result.detail = "Stopped before the game completed.";
        }
        if (_activeBoard == null) return;
        result.emptyCellsAtEnd = DescribeEmptyCells(_activeBoard);
        result.movesLeft = _activeBoard.RemainingMoves;
        result.movesUsed = _movesAtStart - result.movesLeft;
        if (_activeBoard.TopHud == null) return;
        var goals = new List<TopHudController.ActiveGoal>();
        _activeBoard.TopHud.GetActiveGoals(goals);
        result.remainingGoalUnits = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var goal in goals)
        {
            result.remainingGoalUnits += goal.remaining;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(GoalLabel(goal)).Append('=').Append(goal.remaining);
        }
        result.goalsLeft = sb.ToString();
    }

    private static string GoalLabel(TopHudController.ActiveGoal goal) =>
        GoalLabel(goal.targetType, goal.obstacleId, goal.collectibleId, goal.tileType);

    private static string GoalLabel(LevelGoalTargetType type, ObstacleId obstacle, CollectibleId collectible, TileType tile) => type switch
    {
        LevelGoalTargetType.Obstacle => obstacle.ToString(),
        LevelGoalTargetType.Collectible => "Collect_" + collectible,
        _ => "Tile_" + tile,
    };

    private static string DescribeGoals(LevelData level)
    {
        if (level == null || level.goals == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var g in level.goals)
        {
            if (g == null) continue;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(GoalLabel(g.targetType, g.obstacleId, g.collectibleId, g.tileType)).Append('=').Append(g.amount);
        }
        return sb.ToString();
    }

    private void OnForcedFail() => _forcedFail = true;
#if UNITY_EDITOR
    private bool Cancelled => UnityEditor.SessionState.GetBool(CancelKey, false);
    private static void SetStatus(string text) => UnityEditor.SessionState.SetString(StatusKey, text);
#else
    // SIM build: çıktı klasörüne STOP dosyası koyunca durur; durum log'a ve status.txt'ye yazılır.
    private bool Cancelled => _config != null && File.Exists(Path.Combine(Path.GetDirectoryName(_config.outputPath) ?? ".", "STOP"));
    private void SetStatus(string text)
    {
        Debug.Log("[Sim] " + text);
        try { File.WriteAllText(Path.ChangeExtension(_config.outputPath, ".status.txt"), text); } catch { }
    }
#endif
    private void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
            _runtimeError ??= condition + "\n" + stack;
    }

    private bool SaveReport()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_config.outputPath));
            File.WriteAllText(_config.outputPath, JsonUtility.ToJson(_report, true));
            File.WriteAllText(Path.ChangeExtension(_config.outputPath, ".csv"), RuntimeSimulationReport.ToCsv(_report));
#if UNITY_EDITOR
            UnityEditor.SessionState.SetString(ReportKey, _config.outputPath);
#endif
            return true;
        }
        catch (Exception ex)
        {
            if (GameDiagnostics.CriticalErrors)
                Debug.LogError("[RuntimeSim] Report could not be written: " + ex.Message);
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool(CancelKey, true);
#endif
            SetStatus("Rapor kaydedilemedi: " + ex.Message);
            return false;
        }
    }

    private void Finish(bool completed)
    {
        _report.completed = completed;
        bool saved = SaveReport();
        _finished = true;
        if (saved) SetStatus(completed ? "Tamamlandı" : "Durduruldu — kısmi rapor kaydedildi");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(completed ? 0 : 1);
#endif
    }

    private void OnDestroy()
    {
        if (_config == null) return;
        Application.logMessageReceived -= OnLog;
#if UNITY_EDITOR
        if (_recorder != null) Destroy(_recorder.gameObject);
#endif
        try
        {
            if (!_finished)
            {
                if (_activeResult != null) CaptureResult(_activeResult);
                _report.completed = false;
                if (SaveReport()) SetStatus("Durduruldu — kısmi rapor kaydedildi");
            }
        }
        finally
        {
            Time.timeScale = _oldTimeScale;
            UnityEngine.Random.state = _oldRandomState;
            Application.runInBackground = _oldRunInBackground;
            CurrentLevel = null;
            CurrentLevelNumber = 0;
        }
    }
#endif
}
