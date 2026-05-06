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
/// Recommended ObstacleLibrary setup:
/// - id: EnergyContainer
/// - hits: energyPerContainer + 1
/// - stages 0..energyPerContainer-1: active visuals, damageRule=Any
/// - final stage: exhausted/passive visual, damageRule=Disabled
///
/// This service does not clear/destroy the obstacle. With the final Disabled stage,
/// the container remains visible but no longer consumes hits after its energy is depleted.
/// </summary>
public sealed class EnergyContainerService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private EnergyContainerFx energyFx;

    [Header("Rules")]
    [SerializeField, Min(1)] private int energyPerContainer = 10;
    [SerializeField] private CollectibleId collectibleId = CollectibleId.EnergyOrb;

    [Header("Debug")]
    [SerializeField] private bool logHits;

    private readonly Dictionary<int, int> releasedByOrigin = new();

    public int EnergyPerContainer => Mathf.Max(1, energyPerContainer);

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
        if (board != null)
            board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
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

    private IEnumerator BindWhenReady()
    {
        while (board == null)
        {
            ResolveReferences();
            yield return null;
        }

        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.EnergyContainer)
            return;

        if (change.cleared)
            return;

        int origin = change.originIndex;
        int released = releasedByOrigin.TryGetValue(origin, out int current) ? current : 0;
        if (released >= EnergyPerContainer)
        {
            energyFx?.SetExhausted(origin, change.sprite);
            return;
        }

        released++;
        releasedByOrigin[origin] = released;

        int remainingEnergy = Mathf.Max(0, EnergyPerContainer - released);

        if (logHits)
        {
            Debug.Log($"[EnergyContainer] origin={origin} released={released}/{EnergyPerContainer} remainingEnergy={remainingEnergy}");
        }

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        bool goalAccepted = topHud != null && topHud.NotifyCollectibleCollected(collectibleId, 1);

        energyFx?.PlayHit(origin, collectibleId, remainingEnergy, goalAccepted);

        if (remainingEnergy <= 0)
            energyFx?.SetExhausted(origin, change.sprite);
    }

    public bool IsExhausted(int originIndex)
    {
        return releasedByOrigin.TryGetValue(originIndex, out int released) && released >= EnergyPerContainer;
    }
}
