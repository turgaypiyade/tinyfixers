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
        if (RuntimeSimulationSession.IsActive) return;
        if (Instance != null) return;

        // Önce scheduler'ı dene; yoksa direkt config'e bak.
        s_pendingScheduler = Resources.Load<ProgressEventScheduler>("Events/ProgressEventScheduler");
        if (s_pendingScheduler == null)
        {
            s_pendingConfig = Resources.Load<ProgressEventConfig>("Events/ProgressEventConfig");
            if (s_pendingConfig == null)
            {
                if (GameDiagnostics.CriticalErrors)
                    Debug.LogWarning("[ProgressEvent] Resources/Events/ altında ne ProgressEventScheduler ne de ProgressEventConfig bulunamadı.");
                return;
            }
        }

        var go = new GameObject("ProgressEventService [Auto]");
        go.AddComponent<ProgressEventService>();
    }

    // Loss paneline "vazgeçersen staged etkinlik puanını kaybedersin" öğesini kaydeder.
    // Provider CANLI Instance'ı okur (kayıt uygulama başında bir kez yapılır → çift kayıt yok).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterLossProvider()
    {
        LevelLossRegistry.Register("progress_event", () =>
        {
            var inst = Instance;
            if (inst == null)
                return null;

            // Bu level'da hedefe yazılan HER ilerleme kayıptır (kullanıcı kararı, 2026-10-10): oyuncu
            // ör. 12 LineH/V topladıysa hedef dolmasa da vazgeçince o 12 silinir. Hedef başına ikon + sayı.
            List<LevelLossItem> result = null;
            foreach (var (goal, gained) in inst.GetStagedGoalGains())
                (result ??= new List<LevelLossItem>())
                    .Add(new LevelLossItem(goal.DisplayIcon, inst.EventName, gained, achieved: true));
            return result;
        });
    }

    // ── Persistence keys ────────────────────────────────────────

    private const string KeyGoals     = "progress_event_v1_goals";
    private const string KeyCycleKey  = "progress_event_v1_cycle_key";
    private const string KeyRound     = "progress_event_v1_round";   // basamak turu (sunucu sezonu)

    // Asset'teki "Always" event'i sabit bir başlangıçtan durationHours'luk dilimlerle döner → bitip
    // kaybolmaz (eski davranış: 48 saat sonra "always" anahtarı değişmediği için sonsuza dek Ended kalıyordu).
    private static readonly DateTime LocalAlwaysEpochUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    // ── Runtime state ────────────────────────────────────────────

    private ProgressEventScheduler    scheduler;
    private ScheduledProgressEvent    activeEvent;
    private ISaveStore                saveStore;

    // Kaynak önceliği: sunucu sezonu (RemoteProgressEvent) → asset scheduler → asset config.
    private ProgressEventConfig                localDirectConfig;   // scheduler yoksa doğrudan asset config
    private RemoteProgressEvent.ActiveSeason   remoteSeason;        // null = asset kaynağı
    private int                                ladderRound;         // sunucu sezonunda basamak turu (0 = taban)
    private bool                               pendingReload;       // level sürerken gelen değişiklik → level sonunda
    private float                              nextGateCheckTime;
    private float                              nextEndedReloadTime;
    private bool                               levelGateOpen = true;

    private readonly List<ProgressGoalRuntime> goals        = new();
    private readonly List<SessionGainRecord>   sessionGains = new();

    public static event Action<int> OnProgressGained;

    /// OnProgressGained'in taş tipi taşıyan hali — FX driver "+1"leri kırılan
    /// taşların pozisyonuyla eşleştirmek için kullanır.
    public static event Action<TileType, int> OnProgressGainedTyped;

    public ProgressEventState                  State         { get; private set; }
    public TimeSpan                            TimeRemaining => ComputeTimeRemaining();
    public IReadOnlyList<ProgressGoalRuntime>  Goals         => goals;
    // Sunucu sezonu oyuncunun dilinde ad verdiyse o; yoksa çeviri (asset'teki eventName Türkçe sabit, yalnız son çare).
    public string                              EventName     => config == null ? ""
        : remoteSeason != null && !string.IsNullOrEmpty(config.eventName) ? config.eventName
        : GameLocalization.GetOr("progress_event_name", config.eventName);
    public Sprite EventIcon
    {
        get
        {
            if (config == null || config.goals == null || config.goals.Count == 0) return null;
            var g = config.goals[0];
            if (g == null) return null;
            return g.ResolveGoalIcon();
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        if (RuntimeSimulationSession.IsActive) { enabled = false; return; }
        // Scheduler veya direkt config al.
        scheduler = s_pendingScheduler;
        s_pendingScheduler = null;

        if (config == null && s_pendingConfig != null) config = s_pendingConfig;
        s_pendingConfig = null;
        localDirectConfig = config;

        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        saveStore = new PlayerPrefsSaveStore();
        RebuildFromSource();

        GameEventBus.OnTileCleared     += HandleTileCleared;
        GameEventBus.OnObstacleCleared += HandleObstacleCleared;
        GameEventBus.OnSpecialCreated  += HandleSpecialCreated;
        RemoteProgressEvent.OnChanged  += RequestReload;
    }

    private void OnDestroy()
    {
        GameEventBus.OnTileCleared     -= HandleTileCleared;
        GameEventBus.OnObstacleCleared -= HandleObstacleCleared;
        GameEventBus.OnSpecialCreated  -= HandleSpecialCreated;
        RemoteProgressEvent.OnChanged  -= RequestReload;
        if (Instance == this) Instance = null;
    }

    private void Update() => RefreshState();

    /// The service survives menu scene reloads; discard cached goals/rewards after a save reset/restore.
    public void ReloadFromPrefs()
    {
        DiscardStagedGains();
        stagedSpacePerGoal = null;
        sessionGains.Clear();
        pendingRewardReveals.Clear();
        pendingMenuRewardFx.Clear();
        RebuildFromSource();
    }

    // Sunucu dokümanı değişti / sezon bitti: level sürerken staging'i bozmamak için level sonuna ertelenir.
    // Kayıt geri yüklemesinden (ReloadFromPrefs) farkı: bekleyen ödül töreni / menü uçuşu korunur.
    private void RequestReload()
    {
        if (stagingActive) { pendingReload = true; return; }
        RebuildFromSource();
    }

    // Kaynağı çöz → döngü/tur → hedefler → kayıtlı sayımlar. Hedef listesi değiştiyse menü bar şovunun
    // indeksleri geçersizdir (sessionGains silinir); ilerleme kayıtta olduğu için kaybolmaz.
    private void RebuildFromSource()
    {
        pendingReload = false;
        string previousKey = config != null ? CurrentCycleKey() : null;
        int previousCount = goals.Count;

        ResolveSource();
        if (config == null)
        {
            goals.Clear();
            sessionGains.Clear();
            RefreshState();
            return;
        }

        LoadState();
        BuildGoals();
        LoadGoalCounts();
        TryAdvanceLadder();
        if (previousKey != CurrentCycleKey() || previousCount != goals.Count) sessionGains.Clear();
        RefreshState();
    }

    // Kaynak: sunucu sezonu (varsa) → yoksa oyundaki asset. Sunucu "kapalı" diyorsa event yok.
    private void ResolveSource()
    {
        remoteSeason = null;
        activeEvent = null;

        var status = RemoteProgressEvent.TryResolve(DateTime.UtcNow, LocalIconSource(), out var season);
        if (status == RemoteProgressEvent.Status.Active)
        {
            remoteSeason = season;
            config = season.Config;
        }
        else if (status == RemoteProgressEvent.Status.Inactive)
        {
            config = null;
        }
        else if (scheduler != null)
        {
            activeEvent = scheduler.GetActiveEvent();
            config = activeEvent?.config;
        }
        else
        {
            config = localDirectConfig;
        }

        UpdateLevelGate();
    }

    // Sunucu sezonunun ikon/dalga görselleri oyundaki asset'ten eşlenir.
    private ProgressEventConfig LocalIconSource()
    {
        if (localDirectConfig != null) return localDirectConfig;
        if (scheduler?.events == null) return null;
        foreach (var e in scheduler.events)
            if (e?.config != null) return e.config;
        return null;
    }

    private void UpdateLevelGate()
    {
        int minLevel = remoteSeason != null ? remoteSeason.MinLevel : 1;
        levelGateOpen = PlayerPrefs.GetInt("current_level", 1) >= minLevel;
    }

    // ── IProgressEventService ────────────────────────────────────

    public IReadOnlyList<SessionGainRecord> ConsumeSessionGains()
    {
        var copy = new List<SessionGainRecord>(sessionGains);
        sessionGains.Clear();
        return copy;
    }

    // ── Level staging ────────────────────────────────────────────
    //
    // Level sırasında toplanan event item'ları kalıcı goal'lere HEMEN yazılmaz;
    // staging havuzunda bekletilir. Level kazanılırsa CommitStagedGains kalıcılaştırır,
    // oyuncu kaybetmeyi kabul ederse DiscardStagedGains havuzu atar (item'lar kaybolur).
    // Oyun içi "+N" feedback'i staging sırasında da canlı akar (sanal kapasite
    // simülasyonu commit'le birebir aynı dağıtımı kullanır; commit sessiz uygular).

    // Sayılan olay: taş (tip taşır), engel, special, level kazanma. Hedef türüyle Matches eşler.
    private enum SignalKind : byte { Tile, Obstacle, Special, LevelWon, FirstTryWin }
    private struct Signal { public SignalKind kind; public TileType tile; public int count; }

    private bool stagingActive;
    private readonly List<Signal> stagedEvents = new();
    private int[] stagedSpacePerGoal;
    private int[] stagedStartSpacePerGoal;   // level başındaki boşluk → (başlangıç − şimdiki) = bu level'ın kazancı
    private int stagedGainTotal;

    /// Fail popup'ının "X event puanı kaybolacak" uyarısı için.
    public int StagedGainTotal => stagingActive ? stagedGainTotal : 0;

    /// <summary>
    /// Bu level'da (staged) ilerleme yazılan hedefler ve kazanılan miktar. Vazgeçilirse hepsi silinir.
    /// Tamamlanan ve yarım kalan hedefler birlikte döner; ödülü alınmış / önceden dolmuş hedefler hariç.
    /// </summary>
    public IEnumerable<(ProgressGoalRuntime goal, int gained)> GetStagedGoalGains()
    {
        if (!stagingActive || stagedSpacePerGoal == null || stagedStartSpacePerGoal == null)
            yield break;

        int n = Mathf.Min(goals.Count, Mathf.Min(stagedSpacePerGoal.Length, stagedStartSpacePerGoal.Length));
        for (int i = 0; i < n; i++)
        {
            var g = goals[i];
            if (g == null || g.IsRewardClaimed || g.IsCompleted) continue;
            int gained = stagedStartSpacePerGoal[i] - stagedSpacePerGoal[i];
            if (gained > 0)
                yield return (g, gained);
        }
    }

    // Verilen ama henüz törenle (sandık açılışı) gösterilmemiş ödüller.
    private readonly List<DailySlotReward> pendingRewardReveals = new();

    /// <summary>
    /// Son grant'lerden bekleyen sandık-töreni ödüllerini alır ve listeyi temizler.
    /// Boşsa boş liste döner (null dönmez). UI: RewardChestRevealOverlay.Show(list).
    /// </summary>
    public List<DailySlotReward> ConsumePendingRewardReveals()
    {
        var copy = new List<DailySlotReward>(pendingRewardReveals);
        pendingRewardReveals.Clear();
        return copy;
    }

    // Ana menü "torbaya uçuş" animasyonu bekleyen ödüller (her grant, final şartı yok).
    private readonly List<DailySlotReward> pendingMenuRewardFx = new();

    /// <summary>Menü dönüşünde LevelSelector'a uçurulacak ödülleri alır ve temizler.</summary>
    public List<DailySlotReward> ConsumePendingMenuRewardFx()
    {
        var copy = new List<DailySlotReward>(pendingMenuRewardFx);
        pendingMenuRewardFx.Clear();
        return copy;
    }

    // Event'in tüm hedefleri tamamlanıp ödülleri alındı mı? (= sandık anı)
    private bool AllGoalsClaimed()
    {
        for (int i = 0; i < goals.Count; i++)
        {
            if (!goals[i].IsRewardClaimed)
                return false;
        }
        return goals.Count > 0;
    }

    /// Level başında çağrılır (GridSpawner). Önceki kalıntıyı atar.
    public void BeginLevelStaging()
    {
        if (pendingReload) RebuildFromSource();
        UpdateLevelGate();
        TryAdvanceLadder();
        stagingActive = true;
        stagedEvents.Clear();
        stagedGainTotal = 0;

        stagedSpacePerGoal = new int[goals.Count];
        for (int i = 0; i < goals.Count; i++)
        {
            stagedSpacePerGoal[i] = goals[i].IsRewardClaimed
                ? 0
                : Mathf.Max(0, goals[i].Definition.targetCount - goals[i].CurrentCount);
        }
        stagedStartSpacePerGoal = (int[])stagedSpacePerGoal.Clone();
    }

    /// Level kazanılınca çağrılır: bekleyen kazanımları kalıcılaştırır (ödüller dahil).
    /// "+N" feedback'i staging sırasında zaten aktığı için burada tekrar yayınlanmaz.
    public void CommitStagedGains()
    {
        if (!stagingActive) return;
        stagingActive = false;

        for (int i = 0; i < stagedEvents.Count; i++)
            ApplySignal(stagedEvents[i], fireGainEvent: false);

        if (stagedGainTotal > 0)
            {}

        stagedEvents.Clear();
        stagedGainTotal = 0;

        // Commit = level kazanıldı. Kazanma hedefleri burada (staging'siz) sayılır.
        if (State == ProgressEventState.Active)
        {
            ApplySignal(new Signal { kind = SignalKind.LevelWon, count = 1 }, fireGainEvent: true);
            if (LevelAttemptStats.AttemptsOn(CurrentLevel.Global) <= 1)
                ApplySignal(new Signal { kind = SignalKind.FirstTryWin, count = 1 }, fireGainEvent: true);
        }

        if (pendingReload) RebuildFromSource();
    }

    /// Oyuncu kaybetmeyi kabul edince çağrılır: bekleyen kazanımlar atılır.
    public void DiscardStagedGains()
    {
        if (stagedGainTotal > 0)
            {}

        stagingActive = false;
        stagedEvents.Clear();
        stagedGainTotal = 0;
        if (pendingReload) RebuildFromSource();
    }

    // Staging sırasında "+N" feedback'i için: commit'in yapacağı dağıtımın birebir
    // simülasyonu (sanal kalan kapasiteler üzerinden), kalıcı state'e dokunmaz.
    // KADEMELİ sayım: yalnızca AKTİF hedef (ilk dolmamış) sayar; o dolmadan
    // sonraki hedefe hiçbir taş yazılmaz. Aktif hedef bu level'da (sanal) dolarsa
    // sıradaki hedef O ANDAN itibaren saymaya başlar.
    private int SimulateStagedGain(in Signal signal)
    {
        if (stagedSpacePerGoal == null) return 0;

        int remaining = signal.count;
        int gained = 0;

        for (int i = 0; i < goals.Count && remaining > 0; i++)
        {
            if (stagedSpacePerGoal[i] <= 0) continue;   // bitmiş (ya da bu level'da sanal bitti) → sıradaki

            // İlk dolmamış hedef = aktif hedef. Tip uymuyorsa sayım yok — sonrakiler de bloklu.
            if (!Matches(goals[i].Definition.goalType, signal)) break;

            int take = Mathf.Min(stagedSpacePerGoal[i], remaining);
            stagedSpacePerGoal[i] -= take;
            remaining -= take;
            gained += take;

            // Aktif hedef hâlâ dolmadıysa sonraki hedefler bloklu kalır.
            if (stagedSpacePerGoal[i] > 0) break;
        }

        return gained;
    }

    // ── Tile counting ────────────────────────────────────────────

    private void HandleTileCleared(TileType type, int count)
        => HandleSignal(new Signal { kind = SignalKind.Tile, tile = type, count = count });

    private void HandleObstacleCleared(ObstacleId id)
        => HandleSignal(new Signal { kind = SignalKind.Obstacle, count = 1 });

    private void HandleSpecialCreated(TileSpecial special)
        => HandleSignal(new Signal { kind = SignalKind.Special, count = 1 });

    private void HandleSignal(in Signal signal)
    {
        if (State != ProgressEventState.Active || signal.count <= 0) return;

        if (stagingActive)
        {
            stagedEvents.Add(signal);

            int gained = SimulateStagedGain(signal);
            if (gained > 0)
            {
                stagedGainTotal += gained;
                OnProgressGained?.Invoke(gained);
                if (signal.kind == SignalKind.Tile)
                    OnProgressGainedTyped?.Invoke(signal.tile, gained);
            }
            return;
        }

        ApplySignal(signal, fireGainEvent: true);
    }

    private void ApplySignal(in Signal signal, bool fireGainEvent)
    {
        EnsureSessionGainSlots();

        int remaining   = signal.count;
        int totalGained = 0;
        bool dirty      = false;

        // KADEMELİ sayım: yalnızca AKTİF hedef (ilk claim edilmemiş) taş sayar.
        // Aktif hedef tamamlanınca (claim edilir) sıradaki hedef aynı batch'in
        // artan taşlarıyla saymaya devam edebilir; tamamlanmadıysa sonrakiler bloklu.
        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            if (goal.IsRewardClaimed) continue;

            // İlk claim edilmemiş hedef = aktif hedef. Tip uymuyorsa bu batch'ten
            // hiçbir hedef sayamaz (sonrakiler bloklu) — döngüyü kes.
            if (!Matches(goal.Definition.goalType, signal)) break;

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

                // Sandık açılış töreni SADECE event'in tamamı bittiğinde (son adım
                // = gerçek sandık anı) oynar. Ara adımların küçük ödülleri (altın vb.)
                // sessizce verilir — her level sonunda popup çıkmasın.
                if (goal.Definition.reward != null && AllGoalsClaimed())
                    pendingRewardReveals.Add(goal.Definition.reward);

                // Ana menü "ödülü torbaya at" uçuşu için HER grant biriktirilir
                // (MainMenuRewardCollectFx, menüye dönünce LevelSelector'a uçurur).
                if (goal.Definition.reward != null)
                    pendingMenuRewardFx.Add(goal.Definition.reward);
            }

            // Aktif hedef tamamlanmadıysa sonraki hedefler saymaz — batch burada biter.
            if (!goal.IsRewardClaimed) break;

            remaining = overflow;
            if (remaining <= 0) break;
        }

        if (dirty) SaveGoals();
        if (totalGained > 0 && fireGainEvent)
        {
            OnProgressGained?.Invoke(totalGained);
            if (signal.kind == SignalKind.Tile)
                OnProgressGainedTyped?.Invoke(signal.tile, totalGained);
        }
    }

    private void EnsureSessionGainSlots()
    {
        while (sessionGains.Count < goals.Count)
            sessionGains.Add(new SessionGainRecord { GoalIndex = sessionGains.Count });
    }

    // ── State & time ────────────────────────────────────────────

    private void RefreshState()
    {
        if (config == null)
        {
            State = ProgressEventState.Scheduled;
            // Sunucu kapalıyken / hafta içi boş günlerde ara ara yeniden bak (sezon başlamış olabilir).
            ReloadWhenEnded();
            return;
        }

        if (Time.unscaledTime >= nextGateCheckTime)
        {
            nextGateCheckTime = Time.unscaledTime + 1f;
            UpdateLevelGate();
        }

        if (DateTime.UtcNow >= CurrentWindowEndUtc())
        {
            // Pencere kapandı → sıradaki sezon/döngü (level sürüyorsa level sonunda).
            State = ProgressEventState.Ended;
            ReloadWhenEnded();
            return;
        }

        State = levelGateOpen ? ProgressEventState.Active : ProgressEventState.Scheduled;
    }

    private void ReloadWhenEnded()
    {
        if (Time.unscaledTime < nextEndedReloadTime) return;
        nextEndedReloadTime = Time.unscaledTime + 30f;
        RequestReload();
    }

    private TimeSpan ComputeTimeRemaining()
    {
        if (State != ProgressEventState.Active || config == null) return TimeSpan.Zero;
        var remaining = CurrentWindowEndUtc() - DateTime.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    // Aktif pencerenin bitişi: sunucu sezonu → sezon dilimi; scheduler Weekly/FixedDate → kendi penceresi;
    // Always → sabit başlangıçtan durationHours'luk dilim (sürekli döner).
    private DateTime CurrentWindowEndUtc()
    {
        if (remoteSeason != null) return remoteSeason.WindowEndUtc;
        if (activeEvent != null && activeEvent.schedule.scheduleType != ProgressEventScheduleType.Always)
            return activeEvent.schedule.IsActiveNow() ? scheduler.GetWindowEnd(activeEvent) : DateTime.MinValue;
        long period = LocalAlwaysPeriod();
        return LocalAlwaysEpochUtc.AddHours(LocalAlwaysHours() * (period + 1));
    }

    private double LocalAlwaysHours() => Math.Max(1.0, config != null ? config.durationHours : 48f);

    private long LocalAlwaysPeriod()
        => (long)Math.Floor((DateTime.UtcNow - LocalAlwaysEpochUtc).TotalHours / LocalAlwaysHours());

    private string CurrentCycleKey()
    {
        if (remoteSeason != null) return remoteSeason.CycleKey;
        if (activeEvent != null && activeEvent.schedule.scheduleType != ProgressEventScheduleType.Always)
            return scheduler.GetCycleKey(activeEvent);
        return $"always_{LocalAlwaysPeriod()}";
    }

    // ── Basamak (ladder) ─────────────────────────────────────────
    // Sunucu sezonunda tüm hedefler alınınca aynı hedefler growthPercent büyüyerek yeniden açılır.
    // Tur ilerletme level BAŞINDA / yüklemede yapılır: menüdeki bar şovu (sessionGains, hedef indeksleri)
    // bitmiş turu oynatırken liste değişmesin.
    private void TryAdvanceLadder()
    {
        if (remoteSeason == null || !remoteSeason.LadderEnabled || stagingActive) return;
        if (!AllGoalsClaimed() || ladderRound + 1 >= remoteSeason.MaxRounds) return;

        ladderRound++;
        saveStore.Save(KeyRound, ladderRound.ToString());
        saveStore.Save(KeyGoals, "");
        sessionGains.Clear();
        BuildGoals();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static void GrantProgressReward(ProgressGoalDefinition def)
    {
        if (def.reward == null) return;
        if (def.EffectiveRewardDurationMinutes > 0)
            TimedRewardService.Grant(def.reward.type, def.EffectiveRewardDurationMinutes);
        else
            DailySlotRewardService.Grant(def.reward);
    }

    private static bool Matches(ProgressGoalType goalType, in Signal s)
    {
        switch (s.kind)
        {
            case SignalKind.Obstacle:    return goalType == ProgressGoalType.ObstacleCleared;
            case SignalKind.Special:     return goalType == ProgressGoalType.SpecialCreated;
            case SignalKind.LevelWon:    return goalType == ProgressGoalType.LevelWon;
            case SignalKind.FirstTryWin: return goalType == ProgressGoalType.FirstTryWin;
        }

        var tile = s.tile;
        return goalType switch
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
    }

    // ── Init & Persistence ───────────────────────────────────────

    private void BuildGoals()
    {
        goals.Clear();
        if (config?.goals == null) return;

        // Basamak turunda aynı hedefler büyütülmüş hedefle (ödül aynı) yeniden kurulur.
        float scale = remoteSeason != null && ladderRound > 0
            ? Mathf.Pow(1f + remoteSeason.GrowthPercent / 100f, ladderRound)
            : 1f;

        foreach (var def in config.goals)
        {
            if (def == null) continue;
            goals.Add(new ProgressGoalRuntime(Mathf.Approximately(scale, 1f) ? def : Scaled(def, scale)));
        }
    }

    private static ProgressGoalDefinition Scaled(ProgressGoalDefinition def, float scale) => new()
    {
        goalType = def.goalType,
        targetCount = Mathf.Max(def.targetCount, Mathf.CeilToInt(def.targetCount * scale)),
        reward = def.reward,
        rewardDurationMinutes = def.rewardDurationMinutes,
        rewardWaveSprite = def.rewardWaveSprite,
        fallbackDescription = def.fallbackDescription,
        goalIcon = def.goalIcon,
    };

    // Döngü anahtarı + basamak turu. Döngü değiştiyse eski hedef kaydı SİLİNİR (önceden yalnız anahtar
    // yazılıyordu → ilk kazanım yazılmadan uygulama kapanırsa eski döngünün ilerlemesi geri yükleniyordu).
    private void LoadState()
    {
        string currentCycleKey = CurrentCycleKey();
        string savedCycleKey = saveStore.Load(KeyCycleKey, "");
        if (savedCycleKey != currentCycleKey)
        {
            saveStore.Save(KeyCycleKey, currentCycleKey);
            saveStore.Save(KeyGoals, "");
            saveStore.Save(KeyRound, "0");
            ladderRound = 0;
            return;
        }

        ladderRound = int.TryParse(saveStore.Load(KeyRound, "0"), out int r) ? Mathf.Max(0, r) : 0;
        if (remoteSeason == null || !remoteSeason.LadderEnabled) ladderRound = 0;
        else ladderRound = Mathf.Min(ladderRound, remoteSeason.MaxRounds - 1);
    }

    private void LoadGoalCounts()
    {
        string json = saveStore.Load(KeyGoals, "");
        if (string.IsNullOrEmpty(json)) return;
        var wrapper = JsonUtility.FromJson<GoalSaveWrapper>(json);
        if (wrapper?.items == null) return;
        int count = Mathf.Min(goals.Count, wrapper.items.Count);
        for (int i = 0; i < count; i++)
            goals[i].SetFromSave(wrapper.items[i].count, wrapper.items[i].claimed);
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
