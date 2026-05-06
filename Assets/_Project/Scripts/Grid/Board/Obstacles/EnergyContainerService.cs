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
/// Energy capacity is level-driven through LevelData.energyPerContainer. The
/// serialized fallbackEnergyPerContainer value is only used when there is no active
/// level/source level available.
/// </summary>
public sealed class EnergyContainerService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private EnergyContainerFx energyFx;

    [Header("Rules")]
    [SerializeField, Min(1)] private int fallbackEnergyPerContainer = 10;
    [SerializeField] private CollectibleId collectibleId = CollectibleId.EnergyOrb;

    [Header("Debug")]
    [SerializeField] private bool logHits;

    private readonly Dictionary<int, int> releasedByOrigin = new();
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
        // Prefer the source LevelData on GridSpawner because GridSpawner currently
        // clones LevelData at runtime. This keeps the value level-authored even if an
        // older runtime clone omitted newer serialized fields.
        var spawner = FindFirstObjectByType<GridSpawner>();
        if (spawner != null && spawner.level != null)
            return Mathf.Max(1, spawner.level.energyPerContainer);

        if (board != null && board.ActiveLevelData != null)
            return Mathf.Max(1, board.ActiveLevelData.energyPerContainer);

        return Mathf.Max(1, fallbackEnergyPerContainer);
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null)
        {
            ResolveReferences();
            yield return null;
        }

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

        if (change.cleared)
            return;

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
        int released = releasedByOrigin.TryGetValue(origin, out int current) ? current : 0;
        if (released >= capacity)
        {
            energyFx?.SetExhausted(origin, sprite);
            return;
        }

        released++;
        releasedByOrigin[origin] = released;

        int remainingEnergy = Mathf.Max(0, capacity - released);

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        bool goalAccepted = topHud != null && topHud.NotifyCollectibleCollected(collectibleId, 1);

        if (logHits)
        {
            Debug.Log(
                $"[EnergyContainer] source={source} origin={origin} " +
                $"remainingHits={remainingHits} released={released}/{capacity} " +
                $"remainingEnergy={remainingEnergy} goalAccepted={goalAccepted} " +
                $"hasFx={energyFx != null} hasHud={topHud != null}");
        }

        energyFx?.PlayHit(origin, collectibleId, remainingEnergy, goalAccepted);

        if (remainingEnergy <= 0)
            energyFx?.SetExhausted(origin, sprite);
    }

    public bool IsExhausted(int originIndex)
    {
        return releasedByOrigin.TryGetValue(originIndex, out int released) && released >= EnergyPerContainer;
    }
}
