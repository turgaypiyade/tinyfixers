using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TopHudController : MonoBehaviour
{
    public readonly struct ActiveGoal
    {
        public readonly LevelGoalTargetType targetType;
        public readonly TileType tileType;
        public readonly ObstacleId obstacleId;
        public readonly CollectibleId collectibleId;
        public readonly int remaining;

        public ActiveGoal(LevelGoalTargetType targetType, TileType tileType, ObstacleId obstacleId, CollectibleId collectibleId, int remaining)
        {
            this.targetType = targetType;
            this.tileType = tileType;
            this.obstacleId = obstacleId;
            this.collectibleId = collectibleId;
            this.remaining = remaining;
        }
    }

    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private Transform goalsRoot;
    [SerializeField] private TopHudGoalSlot goalSlotPrefab;

    [Header("Display")]
    [SerializeField] private string movesPrefix = "MOVES";
    [SerializeField] private Sprite fallbackGoalIcon;

    public RectTransform MovesTextRect => movesText != null ? movesText.rectTransform : null;

    private readonly List<RuntimeGoal> runtimeGoals = new();
    private bool initialized;

    public bool AreAllGoalsCompleted { get; private set; }
    public event Action<bool> OnGoalsCompletionChanged;

    private class RuntimeGoal
    {
        public LevelGoalDefinition definition;
        public int remaining;
        public TopHudGoalSlot slot;
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        if (board == null)
            return;

        board.OnMovesChanged -= HandleMovesChanged;
        board.OnTilesCleared -= HandleTilesCleared;
        board.OnObstacleDestroyed -= HandleObstacleDestroyed;
        initialized = false;
    }

    private IEnumerator InitializeWhenReady()
    {
        if (initialized)
            yield break;

        if (board == null)
            board = FindFirstObjectByType<BoardController>();

        while (board == null || board.ActiveLevelData == null)
            yield return null;

        board.OnMovesChanged -= HandleMovesChanged;
        board.OnTilesCleared -= HandleTilesCleared;
        board.OnObstacleDestroyed -= HandleObstacleDestroyed;

        board.OnMovesChanged += HandleMovesChanged;
        board.OnTilesCleared += HandleTilesCleared;
        board.OnObstacleDestroyed += HandleObstacleDestroyed;

        BuildGoals(board.ActiveLevelData);
        RefreshMoves(board.RemainingMoves);
        initialized = true;
    }

    private void BuildGoals(LevelData levelData)
    {
        runtimeGoals.Clear();

        if (goalsRoot != null)
        {
            for (int i = goalsRoot.childCount - 1; i >= 0; i--)
                Destroy(goalsRoot.GetChild(i).gameObject);
        }

        if (levelData == null || levelData.goals == null)
        {
            UpdateGoalsCompletionState();
            return;
        }

        for (int i = 0; i < levelData.goals.Length; i++)
        {
            var goal = levelData.goals[i];
            if (goal == null || goal.amount <= 0)
                continue;

            var runtime = new RuntimeGoal
            {
                definition = goal,
                remaining = goal.amount,
                slot = CreateSlot(goal, i)
            };

            runtime.slot?.SetRemaining(runtime.remaining);
            runtimeGoals.Add(runtime);
        }

        UpdateGoalsCompletionState();
    }

    private TopHudGoalSlot CreateSlot(LevelGoalDefinition goal, int goalIndex)
    {
        if (goalSlotPrefab == null || goalsRoot == null)
            return null;

        var slot = Instantiate(goalSlotPrefab, goalsRoot);
        slot.Setup(ResolveGoalIcon(goal, goalIndex), goal.amount, ShouldUseLargeGoalIcon(goal));
        return slot;
    }

    private static bool ShouldUseLargeGoalIcon(LevelGoalDefinition goal)
    {
        return goal != null
               && goal.targetType == LevelGoalTargetType.Collectible
               && goal.collectibleId == CollectibleId.EnergyOrb;
    }

    private Sprite ResolveGoalIcon(LevelGoalDefinition goal, int goalIndex)
    {
        if (goal == null)
            return fallbackGoalIcon;

        if (goal.iconOverride != null)
            return goal.iconOverride;

        Sprite sourceOverride = ResolveSourceLevelIconOverride(goalIndex);
        if (sourceOverride != null)
            return sourceOverride;

        if (goal.targetType == LevelGoalTargetType.Tile)
        {
            var sprite = board != null ? board.GetIcon(goal.tileType) : null;
            return sprite != null ? sprite : fallbackGoalIcon;
        }

        if (goal.targetType == LevelGoalTargetType.Collectible)
            return fallbackGoalIcon;

        var levelData = board != null ? board.ActiveLevelData : null;
        var obstacleDef = levelData != null && levelData.obstacleLibrary != null
            ? levelData.obstacleLibrary.Get(goal.obstacleId)
            : null;

        var preview = obstacleDef != null ? obstacleDef.GetPreviewSprite() : null;
        return preview != null ? preview : fallbackGoalIcon;
    }

    private Sprite ResolveSourceLevelIconOverride(int goalIndex)
    {
        if (goalIndex < 0)
            return null;

        var spawner = FindFirstObjectByType<GridSpawner>();
        var sourceLevel = spawner != null ? spawner.level : null;
        if (sourceLevel == null || sourceLevel.goals == null || goalIndex >= sourceLevel.goals.Length)
            return null;

        var sourceGoal = sourceLevel.goals[goalIndex];
        return sourceGoal != null ? sourceGoal.iconOverride : null;
    }

    private void HandleMovesChanged(int remainingMoves)
    {
        RefreshMoves(remainingMoves);
    }

    private void RefreshMoves(int remainingMoves)
    {
        if (movesText == null)
            return;

        movesText.text = string.IsNullOrWhiteSpace(movesPrefix)
            ? remainingMoves.ToString()
            : $"{movesPrefix}\n{remainingMoves}";
    }

    private void HandleTilesCleared(TileType tileType, int amount)
    {
        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Tile || goal.definition.tileType != tileType)
                continue;

            int previous = goal.remaining;
            goal.remaining = Mathf.Max(0, goal.remaining - amount);
            goal.slot?.SetRemaining(goal.remaining);
            anyGoalUpdated |= goal.remaining != previous;
        }

        if (anyGoalUpdated)
            UpdateGoalsCompletionState();
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Obstacle || goal.definition.obstacleId != obstacleId)
                continue;

            int previous = goal.remaining;
            goal.remaining = Mathf.Max(0, goal.remaining - 1);
            goal.slot?.SetRemaining(goal.remaining);
            anyGoalUpdated |= goal.remaining != previous;
        }

        if (anyGoalUpdated)
            UpdateGoalsCompletionState();
    }

    public bool NotifyCollectibleCollected(CollectibleId collectibleId, int amount)
    {
        if (amount <= 0)
            return false;

        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Collectible || goal.definition.collectibleId != collectibleId)
                continue;

            int previous = goal.remaining;
            goal.remaining = Mathf.Max(0, goal.remaining - amount);
            goal.slot?.SetRemaining(goal.remaining);
            anyGoalUpdated |= goal.remaining != previous;
        }

        if (anyGoalUpdated)
            UpdateGoalsCompletionState();

        return anyGoalUpdated;
    }

    private void UpdateGoalsCompletionState()
    {
        bool allCompleted = runtimeGoals.Count > 0;
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            if (runtimeGoals[i].remaining > 0)
            {
                allCompleted = false;
                break;
            }
        }

        if (AreAllGoalsCompleted == allCompleted)
            return;

        AreAllGoalsCompleted = allCompleted;
        OnGoalsCompletionChanged?.Invoke(AreAllGoalsCompleted);
    }

    public bool HasGoalForTile(TileType tileType)
    {
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g.definition == null) continue;
            if (g.definition.targetType != LevelGoalTargetType.Tile) continue;
            if (g.definition.tileType != tileType) continue;
            if (g.remaining <= 0) continue;
            return true;
        }
        return false;
    }

    public bool HasGoalForCollectible(CollectibleId collectibleId)
    {
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g.definition == null) continue;
            if (g.definition.targetType != LevelGoalTargetType.Collectible) continue;
            if (g.definition.collectibleId != collectibleId) continue;
            if (g.remaining <= 0) continue;
            return true;
        }
        return false;
    }

    public bool TryGetGoalTargetRectForTile(TileType tileType, out RectTransform rect)
    {
        rect = null;
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g.definition == null) continue;
            if (g.definition.targetType != LevelGoalTargetType.Tile) continue;
            if (g.definition.tileType != tileType) continue;
            if (g.slot == null) continue;

            rect = g.slot.IconRectTransform != null ? g.slot.IconRectTransform : g.slot.transform as RectTransform;
            return rect != null;
        }
        return false;
    }

    public bool TryGetGoalTargetRectForObstacle(ObstacleId obstacleId, out RectTransform rect)
    {
        rect = null;
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g.definition == null) continue;
            if (g.definition.targetType != LevelGoalTargetType.Obstacle) continue;
            if (g.definition.obstacleId != obstacleId) continue;
            if (g.slot == null) continue;

            rect = g.slot.IconRectTransform != null ? g.slot.IconRectTransform : g.slot.transform as RectTransform;
            return rect != null;
        }
        return false;
    }

    public bool TryGetGoalTargetRectForCollectible(CollectibleId collectibleId, out RectTransform rect)
    {
        rect = null;
        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g.definition == null) continue;
            if (g.definition.targetType != LevelGoalTargetType.Collectible) continue;
            if (g.definition.collectibleId != collectibleId) continue;
            if (g.slot == null) continue;

            rect = g.slot.IconRectTransform != null ? g.slot.IconRectTransform : g.slot.transform as RectTransform;
            return rect != null;
        }
        return false;
    }

    public float GetGoalProgressRatio()
    {
        if (runtimeGoals == null || runtimeGoals.Count == 0) return 0f;

        float total = 0f;
        int validCount = 0;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i];
            if (g == null || g.definition == null || g.definition.amount <= 0) continue;
            int cleared = g.definition.amount - g.remaining;
            total += Mathf.Clamp01((float)cleared / g.definition.amount);
            validCount++;
        }

        return validCount > 0 ? total / validCount : 0f;
    }

    public void GetActiveGoals(List<ActiveGoal> result)
    {
        if (result == null)
            return;

        result.Clear();

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal == null || goal.definition == null) continue;
            if (goal.remaining <= 0) continue;

            result.Add(new ActiveGoal(
                goal.definition.targetType,
                goal.definition.tileType,
                goal.definition.obstacleId,
                goal.definition.collectibleId,
                goal.remaining));
        }
    }
}
