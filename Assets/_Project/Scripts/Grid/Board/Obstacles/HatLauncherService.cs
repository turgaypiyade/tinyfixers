using System.Collections;
using UnityEngine;

/// <summary>
/// Runtime companion for ObstacleId.HatLauncher.
///
/// Her hit'te bir enerji topu fırlatır. Toplam kapasite level'daki collectible
/// goal miktarından alınır. Kapasite dolunca tüm hat'lar exhausted olur
/// (görsel solar, booster dışında kırılamaz — EnergyContainer ile aynı model).
///
/// Görsel animasyon EnergyContainerFx üzerinden çalışır:
///   closedSprite   = boş şapka
///   halfOpenSprite = top yarı çıkmış şapka
///   squashX/squashY = şişerek fırlatma efekti
/// </summary>
public sealed class HatLauncherService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [SerializeField] private EnergyContainerFx fx;

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
            board.ObstacleStateService.HatLauncherHitInterceptor = null;
    }

    private void ResolveReferences()
    {
        if (board == null)
            board = GetComponent<BoardController>()
                    ?? GetComponentInParent<BoardController>(true)
                    ?? FindFirstObjectByType<BoardController>();

        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        if (fx == null)
            fx = GetComponent<EnergyContainerFx>()
                 ?? GetComponentInChildren<EnergyContainerFx>(true)
                 ?? FindFirstObjectByType<EnergyContainerFx>();
    }

    private IEnumerator BindWhenReady()
    {
        while (board == null || board.ObstacleStateService == null)
        {
            ResolveReferences();
            yield return null;
        }

        totalGroupReleased = 0;
        board.ObstacleStateService.HatLauncherHitInterceptor = HandleHit;

        Debug.Log($"[HatLauncher] Bound. totalCapacity={TotalCapacity}");
    }

    private void HandleHit(int originIndex)
    {
        if (topHud == null)
            topHud = FindFirstObjectByType<TopHudController>();

        int capacity = TotalCapacity;

        if (totalGroupReleased >= capacity)
        {
            fx?.SetExhausted(originIndex, null);
            return;
        }

        totalGroupReleased++;
        int remaining = Mathf.Max(0, capacity - totalGroupReleased);
        bool exhausted = totalGroupReleased >= capacity;

        CollectibleId collectible = ResolveCollectibleId();
        bool goalExists = collectible != CollectibleId.None
                          && topHud != null
                          && topHud.HasGoalForCollectible(collectible);

        // Goal collectible uçarken board'u "busy" tut → son hamlede orb varmadan FAIL gösterilmesin.
        System.Action onArrived = null;
        if (goalExists)
        {
            board?.BeginBackgroundJob();
            onArrived = () =>
            {
                topHud.NotifyCollectibleCollected(collectible, 1);
                board?.EndBackgroundJob();
            };
        }

        if (logHits)
        {
            Debug.Log($"[HatLauncher] HIT origin={originIndex} released={totalGroupReleased}/{capacity} " +
                      $"collectible={collectible} goalExists={goalExists} fx={(fx != null ? fx.name : "NULL")} topHud={(topHud != null ? "ok" : "NULL")}");
        }

        if (fx != null)
            fx.PlayHit(originIndex, collectible, remaining, goalExists, onArrived);
        else
            onArrived?.Invoke();

        if (exhausted)
            ExhaustAll();
    }

    private void ExhaustAll()
    {
        if (board?.ObstacleStateService == null)
            return;

        board.ObstacleStateService.HatLauncherHitInterceptor = null;

        LevelData level = ResolveActiveLevelData();
        if (level?.obstacles == null || level.obstacleOrigins == null)
            return;

        int size = Mathf.Min(level.obstacles.Length, level.obstacleOrigins.Length);
        for (int i = 0; i < size; i++)
        {
            if ((ObstacleId)level.obstacles[i] != ObstacleId.HatLauncher) continue;
            if (level.obstacleOrigins[i] != i) continue;
            fx?.SetExhausted(i, null);
        }
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
}
