using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressEventService : MonoBehaviour, IProgressEventService
{
    public static ProgressEventService Instance { get; private set; }

    [SerializeField] private ProgressEventConfig config;

    // Resources/Events/ProgressEventConfig.asset yolundan otomatik yükler.
    // Sahneye manuel eklenmeden her sahnede çalışır.
    private static ProgressEventConfig s_pendingConfig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;
        s_pendingConfig = Resources.Load<ProgressEventConfig>("Events/ProgressEventConfig");
        if (s_pendingConfig == null)
        {
            Debug.LogWarning("[ProgressEvent] Resources/Events/ProgressEventConfig bulunamadı — ProgressEventConfig.asset dosyasını Resources/Events/ klasörüne taşı.");
            return;
        }
        var go = new GameObject("ProgressEventService [Auto]");
        go.AddComponent<ProgressEventService>();
    }

    private const string KeyStartTime = "progress_event_v1_start_time";
    private const string KeyGoals     = "progress_event_v1_goals";

    private ISaveStore saveStore;
    private long       startTimeTicks;

    private readonly List<ProgressGoalRuntime>  goals        = new();
    private readonly List<SessionGainRecord>    sessionGains = new();

    /// Oyun sahnesi FX driver'ı bu event'i dinler → +N animasyonu başlatır.
    public static event Action<int> OnProgressGained;

    public ProgressEventState             State         { get; private set; }
    public TimeSpan                       TimeRemaining => ComputeTimeRemaining();
    public IReadOnlyList<ProgressGoalRuntime> Goals     => goals;
    public string                         EventName     => config != null ? config.eventName : "";

    // ── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (config == null && s_pendingConfig != null) { config = s_pendingConfig; }
        s_pendingConfig = null;

        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null); // DontDestroyOnLoad sadece root objelerde çalışır
        DontDestroyOnLoad(gameObject);

        saveStore = new PlayerPrefsSaveStore();
        BuildGoals();
        LoadState();
        RefreshState();

        GameEventBus.OnTileCleared += HandleTileCleared;
    }

    private void OnDestroy()
    {
        GameEventBus.OnTileCleared -= HandleTileCleared;
        if (Instance == this) Instance = null;
    }

    private void Update() => RefreshState();

    // ── IProgressEventService ────────────────────────────────────

    /// Ana menü bu metodu çağırır; session kazanımlarını alır ve listeyi temizler.
    public IReadOnlyList<SessionGainRecord> ConsumeSessionGains()
    {
        var copy = new List<SessionGainRecord>(sessionGains);
        sessionGains.Clear();
        return copy;
    }

    // ── Sayma ve otomatik ödül ───────────────────────────────────

    private void HandleTileCleared(TileType type, int count)
    {
        if (State != ProgressEventState.Active) return;

        // SessionGain kayıtlarını başlat (index başına bir kayıt).
        EnsureSessionGainSlots();

        int remaining   = count;
        int totalGained = 0;
        bool dirty      = false;

        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            if (goal.IsRewardClaimed) continue;
            if (!Matches(goal.Definition.goalType, type)) continue;

            int before   = goal.CurrentCount;
            int overflow = goal.AddWithOverflow(remaining);
            int added    = goal.CurrentCount - before;

            if (added > 0)
            {
                sessionGains[i].GainedCount += added;
                totalGained += added;
                dirty = true;
            }

            if (goal.IsCompleted && !goal.IsRewardClaimed)
            {
                GrantProgressReward(goal.Definition);
                goal.MarkClaimed();
                sessionGains[i].RewardGranted = true;
                sessionGains[i].Reward        = goal.Definition.reward;
            }

            remaining = overflow;
            if (remaining <= 0) break;
        }

        if (dirty) SaveGoals();
        if (totalGained > 0) OnProgressGained?.Invoke(totalGained);
    }

    private void EnsureSessionGainSlots()
    {
        while (sessionGains.Count < goals.Count)
            sessionGains.Add(new SessionGainRecord { GoalIndex = sessionGains.Count });
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static void GrantProgressReward(ProgressGoalDefinition def)
    {
        if (def.reward == null) return;

        if (def.rewardDurationMinutes > 0)
            TimedRewardService.Grant(def.reward.type, def.rewardDurationMinutes);
        else
            DailySlotRewardService.Grant(def.reward);
    }

    private static bool Matches(ProgressGoalType goalType, TileType tile) =>
        goalType switch
        {
            ProgressGoalType.LineH             => tile == TileType.LineEmitter_H,
            ProgressGoalType.LineV             => tile == TileType.LineEmitter_V,
            ProgressGoalType.LineAny           => tile == TileType.LineEmitter_H || tile == TileType.LineEmitter_V,
            ProgressGoalType.TileCleared_Gear  => tile == TileType.Gear,
            ProgressGoalType.TileCleared_Core  => tile == TileType.Core,
            ProgressGoalType.TileCleared_Bolt  => tile == TileType.Bolt,
            ProgressGoalType.TileCleared_Plate => tile == TileType.Plate,
            ProgressGoalType.TileCleared_Any   => tile == TileType.Gear  || tile == TileType.Core ||
                                                   tile == TileType.Bolt  || tile == TileType.Plate,
            _ => false
        };

    // ── Init & Persistence ───────────────────────────────────────

    private void BuildGoals()
    {
        goals.Clear();
        if (config?.goals == null) return;
        foreach (var def in config.goals)
            goals.Add(new ProgressGoalRuntime(def));
    }

    private void LoadState()
    {
        string startStr = saveStore.Load(KeyStartTime, "");
        if (!long.TryParse(startStr, out startTimeTicks))
        {
            startTimeTicks = DateTime.UtcNow.Ticks;
            saveStore.Save(KeyStartTime, startTimeTicks.ToString());
        }

        string json = saveStore.Load(KeyGoals, "");
        if (!string.IsNullOrEmpty(json))
        {
            var wrapper = JsonUtility.FromJson<GoalSaveWrapper>(json);
            if (wrapper?.items != null)
            {
                int count = Mathf.Min(goals.Count, wrapper.items.Count);
                for (int i = 0; i < count; i++)
                    goals[i].SetFromSave(wrapper.items[i].count, wrapper.items[i].claimed);
            }
        }
    }

    private void RefreshState()
    {
        if (config == null) { State = ProgressEventState.Scheduled; return; }
        double elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTimeTicks).TotalHours;
        State = elapsed >= config.durationHours ? ProgressEventState.Ended : ProgressEventState.Active;
    }

    private TimeSpan ComputeTimeRemaining()
    {
        if (State != ProgressEventState.Active || config == null) return TimeSpan.Zero;
        var end       = new DateTime(startTimeTicks, DateTimeKind.Utc).AddHours(config.durationHours);
        var remaining = end - DateTime.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void SaveGoals()
    {
        var items = new List<GoalSaveItem>(goals.Count);
        foreach (var g in goals)
            items.Add(new GoalSaveItem { count = g.CurrentCount, claimed = g.IsRewardClaimed });
        saveStore.Save(KeyGoals, JsonUtility.ToJson(new GoalSaveWrapper { items = items }));
    }

    [Serializable] private class GoalSaveWrapper { public List<GoalSaveItem> items; }
    [Serializable] private class GoalSaveItem   { public int count; public bool claimed; }
}
