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
        public int dynamicTotal; // grows when spreading obstacles (e.g. Oil) add new cells
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
        board.OnBatteryHit -= HandleBatteryHit;
        board.OnObstacleCreatedDynamic -= HandleObstacleCreatedDynamic;
        board.OnBarrelResolved -= HandleBarrelResolved;
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
        board.OnBatteryHit -= HandleBatteryHit;
        board.OnObstacleCreatedDynamic -= HandleObstacleCreatedDynamic;
        board.OnBarrelResolved -= HandleBarrelResolved;

        board.OnMovesChanged += HandleMovesChanged;
        board.OnTilesCleared += HandleTilesCleared;
        board.OnObstacleDestroyed += HandleObstacleDestroyed;
        board.OnBatteryHit += HandleBatteryHit;
        board.OnObstacleCreatedDynamic += HandleObstacleCreatedDynamic;
        board.OnBarrelResolved += HandleBarrelResolved;

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

            int initialRemaining = goal.amount;

            // Mud goal'ü dinamiktir: barrel'lar kırıldıkça mud saçılıp sayaç artar. Başlangıçta
            // authored mud + board'daki barrel sayısı kadar placeholder ile başlar ("kaç tane
            // varsa" otomatiği). Her kırılmamış barrel = 1 placeholder → mud oluşmadan goal
            // erken tamamlanmaz. Barrel çözülünce (mud stamp'inden SONRA) placeholder düşürülür.
            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId == ObstacleId.Mud)
            {
                int computed = CountObstacleCells(levelData, ObstacleId.Mud)
                             + CountStampedBeneathCells(ObstacleId.Mud)
                             + CountObstacleCells(levelData, ObstacleId.Barrel)
                             + CountObstacleCells(levelData, ObstacleId.Barrell_v2);
                if (computed > 0)
                    initialRemaining = computed;
            }

            var runtime = new RuntimeGoal
            {
                definition = goal,
                remaining = initialRemaining,
                dynamicTotal = initialRemaining,
                slot = CreateSlot(goal, i)
            };

            runtime.slot?.SetRemaining(runtime.remaining);
            runtimeGoals.Add(runtime);
        }

        TryBirthMudGoalFromBarrels(levelData);

        UpdateGoalsCompletionState();
    }

    // Barrel/Barrell_v2 içeren levelde Mud goal'ü AUTHOR edilmemişse otomatik doğur:
    // barrel kırıldıkça saçılacak mud kendiliğinden hedefe dönüşür, tasarımcının ayrıca
    // mud goal eklemesi gerekmez. Placeholder sayaç (kırılmamış barrel başına 1) mevcut
    // dinamik akışla aynı — mud oluşmadan goal erken tamamlanamaz (erken-WIN koruması).
    // Yalnız barrel varlığı tetikler; barrel'sız dekoratif mud, goal'e zorlanmaz.
    private void TryBirthMudGoalFromBarrels(LevelData levelData)
    {
        if (levelData == null)
            return;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var g = runtimeGoals[i].definition;
            if (g != null && g.targetType == LevelGoalTargetType.Obstacle && g.obstacleId == ObstacleId.Mud)
                return;   // authored mud goal zaten var — mevcut dinamik akış işliyor
        }

        int barrelCells = CountObstacleCells(levelData, ObstacleId.Barrel)
                        + CountObstacleCells(levelData, ObstacleId.Barrell_v2);
        if (barrelCells <= 0)
            return;

        int initialRemaining = CountObstacleCells(levelData, ObstacleId.Mud)
                             + CountStampedBeneathCells(ObstacleId.Mud)
                             + barrelCells;

        var mudGoal = new LevelGoalDefinition
        {
            targetType = LevelGoalTargetType.Obstacle,
            obstacleId = ObstacleId.Mud,
            amount = initialRemaining
        };

        var runtime = new RuntimeGoal
        {
            definition = mudGoal,
            remaining = initialRemaining,
            dynamicTotal = initialRemaining,
            slot = CreateSlot(mudGoal, runtimeGoals.Count)
        };

        runtime.slot?.SetRemaining(runtime.remaining);
        runtimeGoals.Add(runtime);
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
        // BatteryBox goal'u per-hit (HandleBatteryHit) takip edilir, yıkılınca çift sayma olmasın.
        if (obstacleId == ObstacleId.BatteryBox)
            return;

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

    private void HandleBatteryHit(int originIndex, ChestColorMask color, int remaining)
    {
        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Obstacle || goal.definition.obstacleId != ObstacleId.BatteryBox)
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

    private void HandleObstacleCreatedDynamic(int x, int y)
    {
        var svc = board?.ObstacleStateService;
        if (svc == null) return;

        // Yayılan (Oil) ya da barrel'dan saçılan (Mud) yeni hücre → eşleşen dinamik goal'ü büyüt.
        ObstacleId createdId;
        if (svc.IsOilAt(x, y)) createdId = ObstacleId.Oil;
        else if (svc.IsMudAt(x, y)) createdId = ObstacleId.Mud;
        else return;

        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Obstacle || goal.definition.obstacleId != createdId)
                continue;

            goal.remaining++;
            goal.dynamicTotal++;
            goal.slot?.SetRemaining(goal.remaining);
            anyGoalUpdated = true;
        }

        if (anyGoalUpdated)
            UpdateGoalsCompletionState();
    }

    // Bir barrel'ın mud yayılımı bittiğinde: o barrel'a ait placeholder'ı Mud goal'ünden düş.
    // Gerçek mud hücreleri HandleObstacleCreatedDynamic ile zaten eklendiği için net etki doğru
    // kalır; decrement mud stamp'inden SONRA geldiğinden sayaç asla erken 0'a inmez.
    private void HandleBarrelResolved()
    {
        bool anyGoalUpdated = false;

        for (int i = 0; i < runtimeGoals.Count; i++)
        {
            var goal = runtimeGoals[i];
            if (goal.definition.targetType != LevelGoalTargetType.Obstacle || goal.definition.obstacleId != ObstacleId.Mud)
                continue;
            if (goal.remaining <= 0)
                continue;

            goal.remaining--;
            if (goal.dynamicTotal > 0)
                goal.dynamicTotal--;
            goal.slot?.SetRemaining(goal.remaining);
            anyGoalUpdated = true;
        }

        if (anyGoalUpdated)
            UpdateGoalsCompletionState();
    }

    private static int CountObstacleCells(LevelData levelData, ObstacleId id)
    {
        if (levelData == null || levelData.obstacles == null)
            return 0;

        int count = 0;
        for (int i = 0; i < levelData.obstacles.Length; i++)
        {
            if ((ObstacleId)levelData.obstacles[i] != id)
                continue;
            // Multi-cell obstacle'larda yalnızca origin hücresini say (Barrel/Mud zaten 1x1).
            if (levelData.obstacleOrigins != null
                && i < levelData.obstacleOrigins.Length
                && levelData.obstacleOrigins[i] != i)
                continue;
            count++;
        }
        return count;
    }

    private int CountStampedBeneathCells(ObstacleId id)
    {
        return board != null && board.ObstacleStateService != null
            ? board.ObstacleStateService.CountStampedBeneath(id)
            : 0;
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
            if (g == null || g.definition == null || g.dynamicTotal <= 0) continue;
            int cleared = g.dynamicTotal - g.remaining;
            total += Mathf.Clamp01((float)cleared / g.dynamicTotal);
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
