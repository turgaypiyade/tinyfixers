using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
/// - stages 0..energyPerContainer-1: normal active visuals, damageRule=Any
/// - final stage: exhausted visual, blocksCells as desired, damageRule set to a
///   rule that will not be reached in your level, until a Disabled rule is added.
///
/// This service never clears/destroys the obstacle. It stops emitting after the
/// configured capacity so it remains a passive shell on the board.
/// </summary>
public sealed class EnergyContainerService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
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
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (energyFx == null)
            energyFx = GetComponent<EnergyContainerFx>()
                       ?? GetComponentInChildren<EnergyContainerFx>(true)
                       ?? FindFirstObjectByType<EnergyContainerFx>();
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

    private IEnumerator BindWhenReady()
    {
        while (board == null)
        {
            board = FindFirstObjectByType<BoardController>();
            yield return null;
        }

        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId != ObstacleId.EnergyContainer)
            return;

        // If the current obstacle pipeline reports it as cleared, do not emit a bonus
        // from that terminal clear. EnergyContainer is intended to remain visible; this
        // guard keeps accidental destroy-stage hits from over-counting goals.
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

        bool goalAccepted = false;
        var hud = board != null ? board.TopHud : null;
        if (hud != null)
            goalAccepted = hud.NotifyCollectibleCollected(collectibleId, 1);

        energyFx?.PlayHit(origin, collectibleId, remainingEnergy, goalAccepted);

        if (remainingEnergy <= 0)
            energyFx?.SetExhausted(origin, change.sprite);
    }

    public bool IsExhausted(int originIndex)
    {
        return releasedByOrigin.TryGetValue(originIndex, out int released) && released >= EnergyPerContainer;
    }
}
