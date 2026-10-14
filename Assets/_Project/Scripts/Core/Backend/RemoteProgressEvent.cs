using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Progress event'in (Haftalık Görevler) hedef + ödüllerini YENİ SÜRÜM YAYINLAMADAN merkezden yönetme.
/// Doküman şeması ve örnek: Docs/ProgressEvent_Remote.md.
///
/// Firestore: config/progressEvent dokümanı, "json" METİN alanı (Console'a JSON yapıştırılır). Kural: config/*
/// herkes okur, kimse yazamaz (LevelRemoteTuning ile aynı). Değer cihazda önbelleklenir → çevrimdışıyken ve ilk
/// okuma bitmeden son bilinen sezon geçerlidir; hiç okunmadıysa oyundaki asset (ProgressEventScheduler) yedektir.
///
/// SONSUZ AKIŞ: sezonlar anchorUtc'den itibaren seasonHours'luk dilimlerle SIRAYLA döner (liste bitince başa
/// sarar) → Console'a hiç dokunulmasa da event durmaz. Sezon içinde son hedef bitince "basamak" (ladder) aynı
/// hedefleri growthPercent büyüterek yeniden açar (maxRounds'a dek). Hedefler herkes için aynıdır (oyuncuya göre
/// ölçeklenmez — kullanıcı kararı 2026-10-14).
///
/// İleriye uyum: uygulamanın tanımadığı hedef/ödül adı gelen hedef atlanır (eski sürümler kırılmaz).
/// </summary>
public static class RemoteProgressEvent
{
    private const string CacheKey = "remote_progress_event_json";
    private const float RefreshAfterSeconds = 10f * 60f;
    private const int MaxGoals = 12;

    // ── Doküman şeması (JsonUtility) ─────────────────────────────
    [Serializable]
    public class Data
    {
        public bool enabled = true;
        public int minLevel = 1;
        public string anchorUtc = "";        // ilk sezonun başlangıcı (UTC, "2026-10-20" ya da ISO-8601)
        public float seasonHours = 72f;
        public Ladder ladder = new();
        public List<Season> seasons = new();
    }

    [Serializable]
    public class Ladder
    {
        public bool enabled = true;
        public int growthPercent = 15;       // her turda hedef büyümesi (%)
        public int maxRounds = 10;           // sezon içinde en fazla tur (1 = basamak yok)
    }

    [Serializable]
    public class Season
    {
        public string id = "";
        public string nameTr = "";
        public string nameEn = "";
        public List<Goal> goals = new();
    }

    [Serializable]
    public class Goal
    {
        public string type = "";             // ProgressGoalType adı (ör. "TileCleared_Gear")
        public int target = 1;
        public string reward = "";           // DailySlotRewardType adı (ör. "Coins")
        public int amount = 1;
        public int minutes;                  // >0 = süreli ödül (ör. 15 dk sınırsız can)
        public string descTr = "";
        public string descEn = "";
    }

    public enum Status { NoData, Inactive, Active }

    /// Çözülmüş aktif sezon. Config bir kez kurulur ve sezon değişene dek yeniden kullanılır.
    public sealed class ActiveSeason
    {
        public ProgressEventConfig Config;
        public string CycleKey;
        public DateTime WindowEndUtc;
        public int MinLevel;
        public bool LadderEnabled;
        public int GrowthPercent;
        public int MaxRounds;
    }

    /// Yeni doküman okundu (içerik değişti). Servis level dışında yeniden kurar.
    public static event Action OnChanged;

    private static Data s_data;
    private static string s_json;
    private static bool s_loaded;
    private static float s_lastFetchTime = -999f;
    private static bool s_fetchInFlight;

    private static ActiveSeason s_active;
    private static string s_activeBuildKey;   // cycleKey + json + dil → değişince config yeniden kurulur

    // ── Çözümleme ────────────────────────────────────────────────

    /// iconSource: oyundaki asset config'i — hedef/ödül görselleri (ikon, dalga) oradan eşlenir.
    public static Status TryResolve(DateTime nowUtc, ProgressEventConfig iconSource, out ActiveSeason season)
    {
        season = null;
        if (RuntimeSimulationSession.IsActive) return Status.NoData;
        EnsureLoaded();
        if (s_data == null) return Status.NoData;
        if (!s_data.enabled || s_data.seasons == null || s_data.seasons.Count == 0) return Status.Inactive;
        if (!TryParseUtc(s_data.anchorUtc, out var anchor) || nowUtc < anchor) return Status.Inactive;

        double hours = Mathf.Clamp(s_data.seasonHours, 1f, 24f * 60f);
        long period = (long)Math.Floor((nowUtc - anchor).TotalHours / hours);
        var src = s_data.seasons[(int)(period % s_data.seasons.Count)];
        if (src == null) return Status.Inactive;

        string cycleKey = $"remote_{period}_{src.id}";
        string buildKey = cycleKey + "|" + s_json + "|" + GameLocalization.CurrentLanguage;
        if (s_active == null || s_activeBuildKey != buildKey)
        {
            var config = Build(src, (float)hours, iconSource);
            if (config == null) return Status.Inactive;
            if (s_active?.Config != null) UnityEngine.Object.Destroy(s_active.Config);

            var ladder = s_data.ladder ?? new Ladder();
            s_active = new ActiveSeason
            {
                Config = config,
                CycleKey = cycleKey,
                WindowEndUtc = anchor.AddHours(hours * (period + 1)),
                MinLevel = Mathf.Max(1, s_data.minLevel),
                LadderEnabled = ladder.enabled && ladder.maxRounds > 1,
                GrowthPercent = Mathf.Clamp(ladder.growthPercent, 0, 200),
                MaxRounds = Mathf.Clamp(ladder.maxRounds, 1, 100),
            };
            s_activeBuildKey = buildKey;
        }

        season = s_active;
        return Status.Active;
    }

    private static ProgressEventConfig Build(Season src, float hours, ProgressEventConfig iconSource)
    {
        if (src.goals == null) return null;
        bool tr = GameLocalization.CurrentLanguage == "tr";

        var config = ScriptableObject.CreateInstance<ProgressEventConfig>();
        config.hideFlags = HideFlags.DontSave;
        config.eventName = Pick(tr, src.nameTr, src.nameEn);
        config.durationHours = hours;
        config.goals = new List<ProgressGoalDefinition>(src.goals.Count);

        foreach (var g in src.goals)
        {
            if (config.goals.Count >= MaxGoals) break;
            if (g == null || !TryParseEnum(g.type, out ProgressGoalType goalType)) continue;
            if (!TryParseEnum(g.reward, out DailySlotRewardType rewardType) || rewardType == DailySlotRewardType.Empty)
                continue;

            int minutes = Mathf.Clamp(g.minutes, 0, 24 * 60);
            config.goals.Add(new ProgressGoalDefinition
            {
                goalType = goalType,
                targetCount = Mathf.Clamp(g.target, 1, 100000),
                reward = new DailySlotReward
                {
                    type = rewardType,
                    amount = Mathf.Clamp(g.amount, 1, 100000),
                    icon = RewardIconFrom(iconSource, rewardType, minutes > 0),
                },
                rewardDurationMinutes = minutes,
                rewardWaveSprite = minutes > 0 ? WaveFrom(iconSource, rewardType) : null,
                fallbackDescription = Pick(tr, g.descTr, g.descEn),
                goalIcon = GoalIcon(iconSource, goalType),
            });
        }

        if (config.goals.Count > 0) return config;
        UnityEngine.Object.Destroy(config);
        return null;
    }

    private static string Pick(bool tr, string trText, string enText)
    {
        string first = tr ? trText : enText, second = tr ? enText : trText;
        return !string.IsNullOrEmpty(first) ? first : (second ?? "");
    }

    // Hedef ikonu: güncel ikon kütüphanesi (taş/special sanatı tek kaynak); kütüphanede yoksa oyundaki
    // asset'te o türe atanmış görsel. (Asset'teki eski atamalar tutarsızdı: mavi taş hedefinde yıldız vb.)
    private static Sprite GoalIcon(ProgressEventConfig source, ProgressGoalType type)
    {
        var icon = LibraryGoalIcon(type);
        if (icon != null || source?.goals == null) return icon;
        foreach (var d in source.goals)
            if (d != null && d.goalType == type && d.goalIcon != null) return d.goalIcon;
        return null;
    }

    private static Sprite LibraryGoalIcon(ProgressGoalType type)
    {
        var lib = TileIconLibrary.Shared;
        if (lib == null) return null;
        return type switch
        {
            ProgressGoalType.TileCleared_Gear => lib.gear,
            ProgressGoalType.TileCleared_Core => lib.core,
            ProgressGoalType.TileCleared_Bolt => lib.bolt,
            ProgressGoalType.TileCleared_Plate => lib.plate,
            ProgressGoalType.TileCleared_Any => lib.gear,
            ProgressGoalType.LineH => lib.lineH,
            ProgressGoalType.LineV => lib.lineV,
            ProgressGoalType.LineAny => lib.lineH,
            ProgressGoalType.ObstacleCleared => lib.boosterHammer,
            ProgressGoalType.SpecialCreated => lib.pulseCore,
            ProgressGoalType.LevelWon => lib.star,
            ProgressGoalType.FirstTryWin => lib.star,
            _ => null,
        };
    }

    // Ödül ikonu: asset'te aynı tür VE aynı süreli/adetli biçimde atanmış görsel (ör. süreli can = sonsuz kalp);
    // yoksa null → DailySlotReward.ResolveIcon kütüphaneden çözer.
    private static Sprite RewardIconFrom(ProgressEventConfig source, DailySlotRewardType type, bool timed)
    {
        if (source?.goals != null)
            foreach (var d in source.goals)
                if (d?.reward != null && d.reward.type == type && (d.rewardDurationMinutes > 0) == timed
                    && d.reward.icon != null)
                    return d.reward.icon;
        return null;
    }

    private static Sprite WaveFrom(ProgressEventConfig source, DailySlotRewardType type)
    {
        if (source?.goals == null) return null;
        Sprite any = null;
        foreach (var d in source.goals)
        {
            if (d?.rewardWaveSprite == null) continue;
            if (d.reward != null && d.reward.type == type) return d.rewardWaveSprite;
            any ??= d.rewardWaveSprite;
        }
        return any;
    }

    private static bool TryParseEnum<T>(string name, out T value) where T : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Yalnız AD kabul edilir: sayısal metin ("3") enum sırası değişirse yanlış türe düşerdi.
        if (char.IsDigit(name[0]) || name[0] == '-') return false;
        return Enum.TryParse(name.Trim(), true, out value) && Enum.IsDefined(typeof(T), value);
    }

    private static bool TryParseUtc(string s, out DateTime utc)
    {
        return DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);
    }

    // ── Okuma + önbellek ─────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        EnsureLoaded();
        var go = new GameObject("[RemoteProgressEvent]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Hook>();
        if (FirebaseAuthService.IsReady) Fetch();
        else FirebaseAuthService.OnReady += Fetch;
    }

    private static void Fetch()
    {
        if (s_fetchInFlight || !FirebaseAuthService.IsReady) return;
        s_fetchInFlight = true;
        s_lastFetchTime = Time.realtimeSinceStartup;

        FirebaseFirestore.DefaultInstance.Collection("config").Document("progressEvent").GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                s_fetchInFlight = false;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[RemoteProgressEvent] okunamadı: {task.Exception?.GetBaseException().Message}");
                    return;
                }

                var snap = task.Result;
                string json = snap.Exists && snap.TryGetValue("json", out string j) ? j : "";
                if (json == s_json) return;   // değişmedi

                if (!string.IsNullOrEmpty(json) && Parse(json) == null)
                {
                    // Bozuk JSON: son geçerli sezon (önbellek) korunur.
                    Debug.LogError("[RemoteProgressEvent] config/progressEvent.json okunamadı (JSON hatası) — önbellek korunuyor.");
                    return;
                }

                Apply(json);
                PlayerPrefs.SetString(CacheKey, json);
                PlayerPrefs.Save();
                Debug.Log($"[RemoteProgressEvent] Yüklendi: {(s_data == null ? "doküman yok → oyundaki asset" : $"{s_data.seasons?.Count ?? 0} sezon")}.");
                OnChanged?.Invoke();
            });
    }

    private static void EnsureLoaded()
    {
        if (s_loaded) return;
        s_loaded = true;
        Apply(PlayerPrefs.GetString(CacheKey, ""));
    }

    private static void Apply(string json)
    {
        s_json = json ?? "";
        s_data = string.IsNullOrEmpty(s_json) ? null : Parse(s_json);
    }

    private static Data Parse(string json)
    {
        try { return JsonUtility.FromJson<Data>(json); }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteProgressEvent] JSON hatası: {e.Message}");
            return null;
        }
    }

    private sealed class Hook : MonoBehaviour
    {
        private void OnApplicationPause(bool paused)
        {
            if (!paused && Time.realtimeSinceStartup - s_lastFetchTime >= RefreshAfterSeconds)
                Fetch();
        }
    }
}
