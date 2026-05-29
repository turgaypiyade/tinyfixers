using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime companion for ObstacleId.EnergyContainer.
///
/// The object is still placed through ObstacleLibrary / LevelEditor so it keeps the
/// existing obstacle placement, blocking, size and hit-rule pipeline. This service
/// only handles the special gameplay layer: every accepted hit releases one
/// EnergyOrb collectible and the container becomes visually exhausted after its
/// configured capacity is depleted.
///
/// Energy capacity is level-driven through LevelData.energyPerContainer.
/// The collectible target is resolved from the active LevelData goals.
/// </summary>
public sealed class EnergyContainerService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private EnergyContainerFx energyFx;

    [Header("Debug")]
    [SerializeField] private bool logHits;

    private int totalGroupReleased;
    private readonly Dictionary<int, int> lastProcessedRemainingHitsByOrigin = new();

    public int EnergyPerContainer => ResolveEnergyPerContainer();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (board == null)
            return;

        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.OnObstacleStageChanged -= HandleObstacleStageChanged;
    }

    private void ResolveReferences()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        if (energyFx == null)
            energyFx = GetComponent<EnergyContainerFx>()
                       ?? GetComponentInChildren<EnergyContainerFx>(true)
                       ?? FindFirstObjectByType<EnergyContainerFx>();
    }

    private int ResolveEnergyPerContainer()
    {
        LevelData level = ResolveActiveLevelData();
        return Mathf.Max(1, level != null ? level.energyPerContainer : 1);
    }

    private CollectibleId ResolveCollectibleId()
    {
        LevelData level = ResolveActiveLevelData();
        if (level != null && level.goals != null)
        {
            for (int i = 0; i < level.goals.Length; i++)
            {
                var goal = level.goals[i];
                if (goal == null)
                    continue;

                if (goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId != CollectibleId.None)
                    return goal.collectibleId;
            }
        }

        return CollectibleId.None;
    }

    private LevelData ResolveActiveLevelData()
    {
        if (board != null && board.ActiveLevelData != null)
            return board.ActiveLevelData;

        var spawner = FindFirstObjectByType<GridSpawner>();
        return spawner != null ? spawner.level : null;
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null)
        {
            ResolveReferences();
            yield return null;
        }

        totalGroupReleased = 0;
        lastProcessedRemainingHitsByOrigin.Clear();

        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;

        board.OnObstacleStageChanged -= HandleObstacleStageChanged;
        board.OnObstacleStageChanged += HandleObstacleStageChanged;

        if (logHits)
            Debug.Log($"[EnergyContainer] Bound to board obstacle events. energyPerContainer={EnergyPerContainer}");
    }

    private void HandleObstacleStageChanged(int originIndex, ObstacleStageSnapshot stage)
    {
        if (board == null || board.ObstacleStateService == null)
            return;

        if (originIndex < 0 || board.Width <= 0)
            return;

        int x = originIndex % board.Width;
        int y = originIndex / board.Width;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.EnergyContainer)
            return;

        int remainingHits = board.ObstacleStateService.GetRemainingHitsAt(x, y);
        ProcessEnergyContainerHit(originIndex, remainingHits, stage.sprite, source: "stage");
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.EnergyContainer)
            return;

        // Process cleared events too: cleared means the final hit arrived and the
        // obstacle was removed from level data. We still need to release the last
        // ball and show the exhausted state instead of letting the image disappear.
        ProcessEnergyContainerHit(change.originIndex, change.remainingHits, change.sprite, source: "visual");
    }

    private void ProcessEnergyContainerHit(int origin, int remainingHits, Sprite sprite, string source)
    {
        if (origin < 0)
            return;

        if (lastProcessedRemainingHitsByOrigin.TryGetValue(origin, out int lastRemaining) && lastRemaining == remainingHits)
            return;

        lastProcessedRemainingHitsByOrigin[origin] = remainingHits;

        ResolveReferences();

        int capacity = EnergyPerContainer;

        if (totalGroupReleased >= capacity)
        {
            energyFx?.SetExhausted(origin, sprite);
            return;
        }

        totalGroupReleased++;
        int remainingEnergy = Mathf.Max(0, capacity - totalGroupReleased);
        bool isGroupExhausted = totalGroupReleased >= capacity;

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        CollectibleId collectible = ResolveCollectibleId();

        // Orb animasyonu hedefe VARDIĞINDA sayacı güncelle — anlık güncelleme görsel tutarsızlığa yol açıyor.
        bool goalExists = collectible != CollectibleId.None
                          && topHud != null
                          && topHud.HasGoalForCollectible(collectible);

        System.Action onOrbArrived = goalExists
            ? () => topHud.NotifyCollectibleCollected(collectible, 1)
            : null;

        if (logHits)
        {
            Debug.Log(
                $"[EnergyContainer] source={source} origin={origin} " +
                $"remainingHits={remainingHits} totalReleased={totalGroupReleased}/{capacity} " +
                $"remainingEnergy={remainingEnergy} goalExists={goalExists} " +
                $"collectible={collectible} groupExhausted={isGroupExhausted} " +
                $"hasFx={energyFx != null} hasHud={topHud != null}");
        }

        if (energyFx != null)
            energyFx.PlayHit(origin, collectible, remainingEnergy, goalExists, onOrbArrived);
        else
            onOrbArrived?.Invoke();

        if (isGroupExhausted)
            ExhaustAllContainers();
    }

    private void ExhaustAllContainers()
    {
        if (board == null || board.ObstacleStateService == null)
            return;

        LevelData level = ResolveActiveLevelData();
        if (level?.obstacles != null && level.obstacleOrigins != null)
        {
            int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
            for (int i = 0; i < size; i++)
            {
                if ((ObstacleId)level.obstacles[i] != ObstacleId.EnergyContainer) continue;
                if (level.obstacleOrigins[i] != i) continue;
                energyFx?.SetExhausted(i, null);
            }
        }

        board.ObstacleStateService.ForceRemoveAllEnergyContainers();
    }

    public bool IsExhausted(int originIndex) => totalGroupReleased >= EnergyPerContainer;
}
