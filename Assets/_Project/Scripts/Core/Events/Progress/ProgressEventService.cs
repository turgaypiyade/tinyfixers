using System;
using System.Collections.Generic;
using UnityEngine;

public class ProgressEventService : MonoBehaviour, IProgressEventService
{
    public static ProgressEventService Instance { get; private set; }

    [SerializeField] private ProgressEventConfig config;

    private static ProgressEventConfig  s_pendingConfig;
    private static ProgressEventScheduler s_pendingScheduler;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        // Önce scheduler'ı dene; yoksa direkt config'e bak.
        s_pendingScheduler = Resources.Load<ProgressEventScheduler>("Events/ProgressEventScheduler");
        if (s_pendingScheduler == null)
        {
            s_pendingConfig = Resources.Load<ProgressEventConfig>("Events/ProgressEventConfig");
            if (s_pendingConfig == null)
            {
                Debug.LogWarning("[ProgressEvent] Resources/Events/ altında ne ProgressEventScheduler ne de ProgressEventConfig bulunamadı.");
                return;
            }
        }

        var go = new GameObject("ProgressEventService [Auto]");
        go.AddComponent<ProgressEventService>();
    }

    // ── Persistence keys ────────────────────────────────────────

    private const string KeyStartTime = "progress_event_v1_start_time";
    private const string KeyGoals     = "progress_event_v1_goals";
    private const string KeyCycleKey  = "progress_event_v1_cycle_key";

    // ── Runtime state ────────────────────────────────────────────

    private ProgressEventScheduler    scheduler;
    private ScheduledProgressEvent    activeEvent;
    private ISaveStore                saveStore;
    private long                      startTimeTicks;

    private readonly List<ProgressGoalRuntime> goals        = new();
    private readonly List<SessionGainRecord>   sessionGains = new();

    public static event Action<int> OnProgressGained;

    public ProgressEventState                  State         { get; private set; }
    public TimeSpan                            TimeRemaining => ComputeTimeRemaining();
    public IReadOnlyList<ProgressGoalRuntime>  Goals         => goals;
    public string                              EventName     => config != null ? config.eventName : "";

    // ── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        // Scheduler veya direkt config al.
        scheduler = s_pendingScheduler;
        s_pendingScheduler = null;

        if (config == null && s_pendingConfig != null) config = s_pendingConfig;
        s_pendingConfig = null;

        // Scheduler varsa aktif event'ten config'i çöz.
        if (scheduler != null)
        {
            activeEvent = scheduler.GetActiveEvent();
            config      = activeEvent?.config;
        }

        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
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

    public IReadOnlyList<SessionGainRecord> ConsumeSessionGains()
    {
        var copy = new List<SessionGainRecord>(sessionGains);
        sessionGains.Clear();
        return copy;
    }

    // ── Tile counting ────────────────────────────────────────────

    private void HandleTileCleared(TileType type, int count)
    {
        if (State != ProgressEventState.Active) return;

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

    // ── State & time ────────────────────────────────────────────

    private void RefreshState()
    {
        if (config == null) { State = ProgressEventState.Scheduled; return; }

        // Scheduler varsa schedule'a göre kontrol et.
        if (scheduler != null && activeEvent != null)
        {
            if (!activeEvent.schedule.IsActiveNow())
            {
                // Pencere kapandı; bir sonraki Awake'de yeni aktif event seçilecek.
                State = ProgressEventState.Ended;
                return;
            }

            // Always dışındaki tiplerde pencere bitişini kontrol et.
            if (activeEvent.schedule.scheduleType != ProgressEventScheduleType.Always)
            {
                State = ProgressEventState.Active;
                return;
            }
        }

        // Always tipi veya scheduler yok → durationHours kullan.
        double elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTimeTicks).TotalHours;
        State = elapsed >= config.durationHours ? ProgressEventState.Ended : ProgressEventState.Active;
    }

    private TimeSpan ComputeTimeRemaining()
    {
        if (State != ProgressEventState.Active || config == null) return TimeSpan.Zero;

        DateTime end;

        if (scheduler != null && activeEvent != null &&
            activeEvent.schedule.scheduleType != ProgressEventScheduleType.Always)
        {
            end = scheduler.GetWindowEnd(activeEvent);
        }
        else
        {
            end = new DateTime(startTimeTicks, DateTimeKind.Utc).AddHours(config.durationHours);
        }

        var remaining = end - DateTime.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
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
        // Cycle key kontrolü: değiştiyse goal'leri sıfırla.
        string currentCycleKey = scheduler != null && activeEvent != null
            ? scheduler.GetCycleKey(activeEvent)
            : "always";

        string savedCycleKey = saveStore.Load(KeyCycleKey, "");
        if (savedCycleKey != currentCycleKey)
        {
            Debug.Log($"[ProgressEvent] Yeni döngü: '{savedCycleKey}' → '{currentCycleKey}'. Goal'ler sıfırlanıyor.");
            saveStore.Save(KeyCycleKey, currentCycleKey);
            // Goal verisini temizle (start time'ı yenile).
            startTimeTicks = DateTime.UtcNow.Ticks;
            saveStore.Save(KeyStartTime, startTimeTicks.ToString());
            return; // goals sıfır kalır
        }

        // Start time yükle (Always tipi için).
        string startStr = saveStore.Load(KeyStartTime, "");
        if (!long.TryParse(startStr, out startTimeTicks))
        {
            startTimeTicks = DateTime.UtcNow.Ticks;
            saveStore.Save(KeyStartTime, startTimeTicks.ToString());
        }

        // Goal durumlarını yükle.
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
