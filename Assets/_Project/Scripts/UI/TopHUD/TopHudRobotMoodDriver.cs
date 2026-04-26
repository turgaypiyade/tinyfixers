using System.Collections;
using UnityEngine;

public class TopHudRobotMoodDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TopHudRobotMood mood;
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;

    [Header("Durations")]
    [SerializeField] private float happyDuration = 0.75f;
    [SerializeField] private float excitedDuration = 0.85f;

    private Coroutine bindRoutine;
    private Coroutine terminalCheckRoutine;
    private bool terminalMoodLocked;

    private void OnEnable()
    {
        SubscribeGlobalSpecialEvents();

        if (bindRoutine != null)
            StopCoroutine(bindRoutine);

        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        UnsubscribeBoardEvents();
        UnsubscribeGlobalSpecialEvents();

        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (terminalCheckRoutine != null)
        {
            StopCoroutine(terminalCheckRoutine);
            terminalCheckRoutine = null;
        }
    }

    private IEnumerator BindWhenReady()
    {
        if (mood == null)
            mood = GetComponentInChildren<TopHudRobotMood>(true);

        if (mood == null)
            mood = FindFirstObjectByType<TopHudRobotMood>(FindObjectsInactive.Include);

        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>(FindObjectsInactive.Include);

        while (mood == null || board == null || topHud == null)
        {
            if (mood == null)
                mood = FindFirstObjectByType<TopHudRobotMood>(FindObjectsInactive.Include);

            if (board == null)
                board = FindFirstObjectByType<BoardController>();

            if (topHud == null)
                topHud = FindFirstObjectByType<TopHudController>(FindObjectsInactive.Include);

            yield return null;
        }

        SubscribeBoardEvents();

        terminalMoodLocked = false;
        mood.SetMood(TopHudRobotMood.Mood.Idle, animate: false);

        bindRoutine = null;
    }

    private void SubscribeBoardEvents()
    {
        UnsubscribeBoardEvents();

        board.OnTilesCleared += HandleTilesCleared;
        board.OnObstacleDestroyed += HandleObstacleDestroyed;
        board.OnMovesChanged += HandleMovesChanged;
        board.OnBecameIdle += HandleBoardBecameIdle;
        board.OnLineSweepStarted += HandleLineSweepStarted;

        topHud.OnGoalsCompletionChanged += HandleGoalsCompletionChanged;
    }

    private void UnsubscribeBoardEvents()
    {
        if (board != null)
        {
            board.OnTilesCleared -= HandleTilesCleared;
            board.OnObstacleDestroyed -= HandleObstacleDestroyed;
            board.OnMovesChanged -= HandleMovesChanged;
            board.OnBecameIdle -= HandleBoardBecameIdle;
            board.OnLineSweepStarted -= HandleLineSweepStarted;
        }

        if (topHud != null)
            topHud.OnGoalsCompletionChanged -= HandleGoalsCompletionChanged;
    }

    private void SubscribeGlobalSpecialEvents()
    {
        ComboBehaviorEvents.ComboTriggered -= HandleComboTriggered;
        ComboBehaviorEvents.ComboTriggered += HandleComboTriggered;

        PulseBehaviorEvents.PulseExplosionPlayed -= HandlePulseExplosionPlayed;
        PulseBehaviorEvents.PulseExplosionPlayed += HandlePulseExplosionPlayed;

        PulseBehaviorEvents.PulseEmitterComboTriggered -= HandlePulseEmitterComboTriggered;
        PulseBehaviorEvents.PulseEmitterComboTriggered += HandlePulseEmitterComboTriggered;

        SystemOverrideBehaviorEvents.OverrideFanoutStarted -= HandleOverrideFanoutStarted;
        SystemOverrideBehaviorEvents.OverrideFanoutStarted += HandleOverrideFanoutStarted;
    }

    private void UnsubscribeGlobalSpecialEvents()
    {
        ComboBehaviorEvents.ComboTriggered -= HandleComboTriggered;
        PulseBehaviorEvents.PulseExplosionPlayed -= HandlePulseExplosionPlayed;
        PulseBehaviorEvents.PulseEmitterComboTriggered -= HandlePulseEmitterComboTriggered;
        SystemOverrideBehaviorEvents.OverrideFanoutStarted -= HandleOverrideFanoutStarted;
    }

    // ─────────────────────────
    // Happy: taş / obstacle kırılınca
    // ─────────────────────────

    private void HandleTilesCleared(TileType tileType, int amount)
    {
        if (amount <= 0)
            return;

        PlayTemporaryMood(TopHudRobotMood.Mood.Happy, happyDuration);
        StartTerminalCheck();
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Happy, happyDuration);
        StartTerminalCheck();
    }

    // ─────────────────────────
    // Excited: special / combo çalışınca
    // ─────────────────────────

    private void HandleComboTriggered(TileSpecial a, TileSpecial b, Vector2Int originCell)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Excited, excitedDuration);
    }

    private void HandlePulseExplosionPlayed(Vector2Int cell)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Excited, excitedDuration);
    }

    private void HandlePulseEmitterComboTriggered(Vector2Int centerCell)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Excited, excitedDuration);
    }

    private void HandleOverrideFanoutStarted(Vector2Int originCell, TileSpecial targetSpecial)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Excited, excitedDuration);
    }

    private void HandleLineSweepStarted(LightningLineStrike strike, float delay)
    {
        PlayTemporaryMood(TopHudRobotMood.Mood.Excited, excitedDuration);
    }

    // ─────────────────────────
    // Win / Fail
    // ─────────────────────────

    private void HandleGoalsCompletionChanged(bool completed)
    {
        if (!completed)
            return;

        terminalMoodLocked = true;
        mood.SetHappy();
    }

    private void HandleMovesChanged(int remainingMoves)
    {
        if (remainingMoves <= 0)
            StartTerminalCheck();
    }

    private void HandleBoardBecameIdle()
    {
        StartTerminalCheck();
    }

    private void StartTerminalCheck()
    {
        if (terminalCheckRoutine != null)
            StopCoroutine(terminalCheckRoutine);

        terminalCheckRoutine = StartCoroutine(CoTerminalCheckAfterBoardSettles());
    }

    private IEnumerator CoTerminalCheckAfterBoardSettles()
    {
        yield return null;

        while (board != null && board.IsBusy)
            yield return null;

        EvaluateTerminalMood();
        terminalCheckRoutine = null;
    }

    private void EvaluateTerminalMood()
    {
        if (mood == null || board == null || topHud == null)
            return;

        if (topHud.AreAllGoalsCompleted)
        {
            terminalMoodLocked = true;
            mood.SetHappy();
            return;
        }

        if (board.RemainingMoves <= 0)
        {
            terminalMoodLocked = true;
            mood.SetSad();
        }
    }

    private void PlayTemporaryMood(TopHudRobotMood.Mood targetMood, float duration)
    {
        if (terminalMoodLocked || mood == null)
            return;

        mood.PlayTemporaryMood(targetMood, duration);
    }
}