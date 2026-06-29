using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime companion for ObstacleId.EnergyContainer.
///
/// Each container produces one EnergyOrb per hit, indefinitely.
/// Exhaustion is global: when total released orbs reach the goal's amount,
/// ALL containers simultaneously become visually exhausted (remaining=1 / FullyDisabled)
/// so they keep blocking cells. A booster can still clear an exhausted container.
///
/// Hit counting is driven by ObstacleStateService.EnergyContainerHitInterceptor,
/// which fires once per hit without decrementing the container's own remaining counter.
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

    public int TotalCapacity => ResolveTotalCapacity();

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
        if (board?.ObstacleStateService != null)
            board.ObstacleStateService.EnergyContainerHitInterceptor = null;
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

    private int ResolveTotalCapacity()
    {
        LevelData level = ResolveActiveLevelData();
        if (level?.goals != null)
        {
            for (int i = 0; i < level.goals.Length; i++)
            {
                var goal = level.goals[i];
                if (goal == null) continue;
                if (goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId != CollectibleId.None)
                    return Mathf.Max(1, goal.amount);
            }
        }
        return Mathf.Max(1, level != null ? level.energyPerContainer : 1);
    }

    private CollectibleId ResolveCollectibleId()
    {
        LevelData level = ResolveActiveLevelData();
        if (level?.goals != null)
        {
            for (int i = 0; i < level.goals.Length; i++)
            {
                var goal = level.goals[i];
                if (goal == null) continue;
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
        while (board == null || board.ObstacleStateService == null)
        {
            ResolveReferences();
            yield return null;
        }

        totalGroupReleased = 0;
        board.ObstacleStateService.EnergyContainerHitInterceptor = HandleEnergyContainerHit;

        Debug.Log($"[EnergyContainer] Bound. totalCapacity={TotalCapacity}");
    }

    private void HandleEnergyContainerHit(int originIndex)
    {
        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        int capacity = TotalCapacity;

        if (totalGroupReleased >= capacity)
        {
            energyFx?.SetExhausted(originIndex, null);
            return;
        }

        totalGroupReleased++;
        int remainingEnergy = Mathf.Max(0, capacity - totalGroupReleased);
        bool isGroupExhausted = totalGroupReleased >= capacity;

        CollectibleId collectible = ResolveCollectibleId();
        bool goalExists = collectible != CollectibleId.None
                          && topHud != null
                          && topHud.HasGoalForCollectible(collectible);

        // Orb VARINCA goal ilerlesin. Goal kredisi her koşulda varışta verilir.
        //
        // Orb uçuşu "goal-orb flight" olarak işaretlenir (BeginGoalOrbFlight): end-of-level
        // değerlendirmesi (fail) orb hedefe varmadan sonucu göstermez — son hamlede orb
        // gelmeden FAIL çıkmaz. AMA bu sayaç ResolveBoard'un cascade/düşüş döngüsünü BEKLETMEZ;
        // böylece orb hedefe uçarken board akmaya devam eder (level 26: çok container →
        // sürekli staggered orb → eskiden board donuyordu).
        System.Action onOrbArrived = null;
        if (goalExists)
        {
            board?.BeginGoalOrbFlight();
            onOrbArrived = () =>
            {
                topHud.NotifyCollectibleCollected(collectible, 1);
                board?.EndGoalOrbFlight();
            };
        }

        if (logHits)
        {
            Debug.Log(
                $"[EnergyContainer] origin={originIndex} " +
                $"totalReleased={totalGroupReleased}/{capacity} " +
                $"remainingEnergy={remainingEnergy} collectible={collectible} " +
                $"goalExists={goalExists} exhausted={isGroupExhausted}");
        }

        if (energyFx != null)
            energyFx.PlayHit(originIndex, collectible, remainingEnergy, goalExists, onOrbArrived);
        else
            onOrbArrived?.Invoke();

        if (isGroupExhausted)
            ExhaustAllContainers();
    }

    private void ExhaustAllContainers()
    {
        if (board?.ObstacleStateService == null)
            return;

        // Null out the interceptor first — subsequent hits are now blocked unconditionally.
        board.ObstacleStateService.EnergyContainerHitInterceptor = null;

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
    }

    public bool IsExhausted(int originIndex) => totalGroupReleased >= TotalCapacity;
}
