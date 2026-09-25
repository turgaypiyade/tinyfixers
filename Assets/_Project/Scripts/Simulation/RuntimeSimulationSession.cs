using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only automation of the REAL gameplay scene. No obstacle, cascade, refill,
/// combo, or goal rules are implemented here. Player builds always see IsActive=false.
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
#else
            return false;
#endif
        }
    }

    public static LevelData CurrentLevel { get; private set; }

    [Serializable]
    public sealed class Config
    {
        public string[] levelPaths;
        public string scenePath = "Assets/_Project/Scenes/01_Game.unity";
        public int gamesPerLevel = 20;
        public int seed = 42;
        public RuntimeBotPolicy policy = RuntimeBotPolicy.GoalAware;
        public float timeScale = 1f;
        public float stepTimeoutSeconds = 120f;
        public string outputPath;
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

#if UNITY_EDITOR
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        CurrentLevel = null;
        if (!IsActive) return;
        var config = JsonUtility.FromJson<Config>(UnityEditor.SessionState.GetString(ConfigKey, ""));
        if (config == null || config.levelPaths == null || config.levelPaths.Length == 0) return;
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
        CurrentLevel = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(config.levelPaths[0]);
        UnityEngine.Random.InitState(config.seed);
        Application.logMessageReceived += session.OnLog;
    }

    private IEnumerator Start()
    {
        if (_config == null) yield break;
        bool first = true;
        foreach (var levelPath in _config.levelPaths)
        {
            for (int sample = 0; sample < _config.gamesPerLevel; sample++)
            {
                if (Cancelled) { Finish(false); yield break; }
                int seed = unchecked(_config.seed + sample * 7919);
                CurrentLevel = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(levelPath);
                var result = new GameResult { levelPath = levelPath, level = CurrentLevel != null ? CurrentLevel.name : levelPath,
                    sample = sample, seed = seed };
                // Preserve errors emitted by Awake/Start in the first gameplay scene.
                if (!first) _runtimeError = null;
                _forcedFail = false;
                _activeResult = result;
                _activeBoard = null;
                _gameStarted = Time.realtimeSinceStartup;
                _report.games.Add(result);
                if (CurrentLevel == null)
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
                    if (string.IsNullOrEmpty(result.outcome))
                        yield return GuardGame(PlayGame(result), result);
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
        foreach (var goal in goals) result.remainingGoalUnits += goal.remaining;
    }

    private void OnForcedFail() => _forcedFail = true;
    private bool Cancelled => UnityEditor.SessionState.GetBool(CancelKey, false);
    private static void SetStatus(string text) => UnityEditor.SessionState.SetString(StatusKey, text);
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
            UnityEditor.SessionState.SetString(ReportKey, _config.outputPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[RuntimeSim] Report could not be written: " + ex.Message);
            UnityEditor.SessionState.SetBool(CancelKey, true);
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
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void OnDestroy()
    {
        if (_config == null) return;
        Application.logMessageReceived -= OnLog;
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
        }
    }
#endif
}
