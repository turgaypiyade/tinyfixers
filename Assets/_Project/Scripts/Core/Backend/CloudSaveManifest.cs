using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cloud save'e giren PlayerPrefs anahtarlarının TEK gerçek listesi (Docs/ProductionPlan.md P1).
/// Yeni kalıcı oyuncu verisi ekleyen HERKES buraya da eklemeli — aksi halde cihaz
/// değişiminde o veri kaybolur.
///
/// Bilerek DIŞARIDA tutulanlar: settings_* (cihaza özgü tercih), bot_pool_count /
/// teams_initialized / real_user_count (yerel sim altyapısı), gen. geçici debug anahtarları.
/// </summary>
public static class CloudSaveManifest
{
    public const string ProgressResetRevisionKey = "progress_reset_revision";
    public const string PendingProgressResetKey = "progress_reset_pending";
    public const string LevelHistoryMaxKey = "progress_level_history_max";

    public static long ProgressResetRevision => ReadRevision(PlayerPrefs.GetString(ProgressResetRevisionKey, "0"));

    // PlayerPrefs'te INT yazılan anahtarlar (SetInt/GetInt).
    private static readonly string[] IntKeys =
    {
        "current_level",
        "player_coins",
        "player_total_stars",
        "player_total_score",
        "player_avatar_id",
        "booster_hammer_count",
        "booster_row_count",
        "booster_column_count",
        "booster_shuffle_count",
        "lives_current",
        "purchase_thanks_pending_lives",
        "safari_joined",
        "safari_pitstop",
        "safari_runstatus",
        "safari_level_snapshot",
        "safari_fail_snapshot",
        "safari_reward_claimed",
        "bridge_joined",
        "harvest_trowels",
        "harvest_floor",
        "harvest_dug",
        "harvest_found",
        "bridge_run_seed",
        "bridge_wins",
        "bridge_final_rank",
        "bridge_reward_claimed",
        "bridge_presented_wins",
        "bridge_end_presented",
        "player_team_joined",
        "player_team_emblem",
        "player_team_min_chapter",
        "player_team_is_creator",
        "initial_stars_granted",
        "prelevel_specials_rewarded",
        "first_launch_done",
        "boss_tip_weakness_seen",
        "boss_tip_goals_left_seen",   // boss yenildi ama hedef kaldı ipucu (bir kez)
        "boss_duel_howto_seen_v1",
        "tutorial_seen_workshop_repair",
        "real_users_seen_max",   // bot evreni azalma eğrisi cihazlar arası tutarlı kalsın
        "music_selected",        // seçili müzik parçası
        // Level deneme sayacı (LevelAttemptStats) — gizli yardım kademesi cihaz değişince korunsun.
        "level_attempt_level",
        "level_attempt_count",
        "level_attempt_fails",
        "level_attempt_struggles",
        // Harika (wonder/event) ilerlemesi: en son tamamlanan event + model sürümü.
        // Event başına görev sırası "wonder_stage_" aile öneki ile taranır (aşağıda).
        // Eski tek-harika anahtarları migration kaynağı olarak taşınmaya devam eder.
        "wonder_last_completed",
        "wonder_selected_background",   // Journey'den seçilen ana menü arka planı (-1 = varsayılan)
        "wonder_model_v2",
        "wonder_completed_count",
        "wonder_current_stage",
    };

    // PlayerPrefs'te STRING yazılan anahtarlar (SetString/GetString).
    private static readonly string[] StringKeys =
    {
        ProgressResetRevisionKey,
        "player_name",
        "player_id",
        "friend_code",
        "friends_list",
        "friends_real",
        "friends_dismissed",
        "player_team_name",
        "player_team_id",
        "player_team_desc",
        "player_team_joined_ticks",   // takıma katılma anı: 24 saat can isteği kilidi
        "lives_next_ticks",
        "purchase_thanks_receipts_v1",
        "team_life_inbox_v1",
        "safari_cycle",
        "safari_join_ticks",
        "safari_lastask_ticks",
        "safari_fall_until_ticks",
        "bridge_cycle",
        "harvest_cycle",
        "bridge_join_ticks",
        "bridge_lastask_ticks",
        "bridge_finish_ticks",
        "bridge_presented_ticks",
        "bridge_bot_names",
        "bridge_howto_cycle",
        "progress_event_v1_goals",
        "progress_event_v1_cycle_key",
        "progress_event_v1_start_time",
        "event_start_time",
        "event_participants",
        "daily_slot_last_spin_date",
        "fortune_wheel_last_spin_time",
        "music_owned",           // 100 altınla açılan müzik parçaları (satın alma korunmalı)
    };

    // Sayı son-ekli INT bayrak aileleri (id 0..MaxEnumScan taranır, HasKey olanlar alınır).
    private static readonly string[] IntFlagPrefixes =
    {
        "tutorial_seen_",
        "combo_tutorial_seen_",
        "obstacle_hint_seen_",
        "wonder_stage_",         // event başına yapılan görev sayısı
    };
    private const int MaxEnumScan = 64;

    // timed_reward_{DailySlotRewardType} → STRING (expiry ticks).
    private const string TimedRewardPrefix = "timed_reward_";

    // These progress fields were historically local-only. Keep them in reset/restore too.
    private static readonly string[] ProgressIntKeys =
    {
        "stats_first_try_clears", "stats_current_streak", "stats_longest_streak",
        "stats_current_level_failed", "stats_level_fail_count", "safari_run_seed",
        "ad_continue_used_level",
    };

    private static bool IsResettableProgress(string key) =>
        key == "current_level" || key == "player_total_score" || key == "boss_duel_howto_seen_v1"
        || key == "tutorial_seen_workshop_repair" || key == "event_start_time" || key == "event_participants"
        || key.StartsWith("boss_tip_", System.StringComparison.Ordinal)
        || key.StartsWith("level_attempt_", System.StringComparison.Ordinal)
        || key.StartsWith("wonder_", System.StringComparison.Ordinal)
        || key.StartsWith("safari_", System.StringComparison.Ordinal)
        || key.StartsWith("bridge_", System.StringComparison.Ordinal)
        || key.StartsWith("progress_event_", System.StringComparison.Ordinal);

    private static long ReadRevision(object value) =>
        long.TryParse(value?.ToString(), out long revision) ? System.Math.Max(0L, revision) : 0L;

    public static long ReadProgressResetRevision(IDictionary<string, object> data) =>
        data != null && data.TryGetValue(ProgressResetRevisionKey, out var value) ? ReadRevision(value) : 0L;

    /// A deliberate reset takes priority over an older save's higher level.
    public static bool ShouldRestoreProgress(IDictionary<string, object> cloudData, long cloudLevel,
        int localLevel, bool force, bool forceLocalWins)
    {
        long cloudRevision = ReadProgressResetRevision(cloudData);
        if (cloudRevision != ProgressResetRevision) return cloudRevision > ProgressResetRevision;
        return force || (!forceLocalWins && cloudLevel > localLevel);
    }

    public static void BeginProgressReset(int maxLevel)
    {
        long revision = System.Math.Max(System.DateTime.UtcNow.Ticks, ProgressResetRevision + 1);
        ClearProgress(maxLevel);
        PlayerPrefs.SetString(ProgressResetRevisionKey, revision.ToString());
        // Keep this until the replacement snapshot is acknowledged by Firestore.
        PlayerPrefs.SetInt(PendingProgressResetKey, 1);
        PlayerPrefs.Save();
    }

    private static void ClearProgress(int maxLevel)
    {
        maxLevel = Mathf.Max(maxLevel, Mathf.Max(PlayerPrefs.GetInt("current_level", 1),
            PlayerPrefs.GetInt(LevelHistoryMaxKey, 1)));
        foreach (string key in IntKeys)
            if (IsResettableProgress(key)) PlayerPrefs.DeleteKey(key);
        foreach (string key in StringKeys)
            if (IsResettableProgress(key)) PlayerPrefs.DeleteKey(key);
        foreach (string key in ProgressIntKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey("stats_weekly_clear_ticks");

        for (int i = 0; i <= maxLevel + 2; i++)
        {
            PlayerPrefs.DeleteKey("level_stars_" + i);
            PlayerPrefs.DeleteKey("level_score_" + i);
            // Old workshop progress is chapter-based; chapter count cannot exceed level count.
            PlayerPrefs.DeleteKey("workshop_chapter_" + i + "_stage");
            PlayerPrefs.DeleteKey("workshop_chapter_" + i + "_reward_claimed");
        }
        foreach (string prefix in IntFlagPrefixes)
            for (int i = 0; i < MaxEnumScan; i++) PlayerPrefs.DeleteKey(prefix + i);
        // Obstacle IDs may grow beyond the legacy cloud scan limit.
        foreach (ObstacleId id in System.Enum.GetValues(typeof(ObstacleId)))
            PlayerPrefs.DeleteKey("obstacle_hint_seen_" + (int)id);

        // Pending menu animations must not replay pre-reset rewards.
        foreach (string key in new[] { "pending_star_reward", "pending_star_before", "pending_star_after",
                     "pending_coin_reward", "pending_coin_before", "pending_coin_after" })
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.SetInt("current_level", 1);
        PlayerPrefs.SetInt("player_total_score", 0);
        PlayerPrefs.SetInt("wonder_model_v2", 2);
        PlayerPrefs.SetInt("wonder_last_completed", -1);
        PlayerPrefs.SetInt("wonder_selected_background", -1);
        PlayerPrefs.SetInt(LevelHistoryMaxKey, maxLevel);
        // Wallet, ledger, initial grants, inventory, identity and settings are deliberately preserved.
    }

    /// <summary>
    /// Yereldeki tüm manifest verisini tek düz map olarak toplar
    /// (int → long, string → string; olmayan anahtar atlanır).
    /// </summary>
    public static Dictionary<string, object> Collect()
    {
        var data = new Dictionary<string, object>();

        foreach (var key in IntKeys)
            if (PlayerPrefs.HasKey(key)) data[key] = (long)PlayerPrefs.GetInt(key);

        foreach (var key in StringKeys)
            if (PlayerPrefs.HasKey(key)) data[key] = PlayerPrefs.GetString(key);

        foreach (var key in ProgressIntKeys)
            if (PlayerPrefs.HasKey(key)) data[key] = (long)PlayerPrefs.GetInt(key);
        if (PlayerPrefs.HasKey("stats_weekly_clear_ticks"))
            data["stats_weekly_clear_ticks"] = PlayerPrefs.GetString("stats_weekly_clear_ticks");

        // Bölüm başına yıldız/puan: 1..current_level (+pay, restore sonrası ileride kalmış olabilir).
        int maxLevel = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1));
        int historyMax = Mathf.Max(maxLevel, PlayerPrefs.GetInt(LevelHistoryMaxKey, 1));
        if (historyMax != PlayerPrefs.GetInt(LevelHistoryMaxKey, 0)) PlayerPrefs.SetInt(LevelHistoryMaxKey, historyMax);
        data[LevelHistoryMaxKey] = (long)historyMax;
        maxLevel = historyMax + 2;
        for (int i = 1; i <= maxLevel; i++)
        {
            string stars = "level_stars_" + i;
            string score = "level_score_" + i;
            if (PlayerPrefs.HasKey(stars)) data[stars] = (long)PlayerPrefs.GetInt(stars);
            if (PlayerPrefs.HasKey(score)) data[score] = (long)PlayerPrefs.GetInt(score);
        }

        foreach (var prefix in IntFlagPrefixes)
            for (int id = 0; id < MaxEnumScan; id++)
            {
                string key = prefix + id;
                if (PlayerPrefs.HasKey(key)) data[key] = (long)PlayerPrefs.GetInt(key);
            }

        foreach (DailySlotRewardType type in System.Enum.GetValues(typeof(DailySlotRewardType)))
        {
            string key = TimedRewardPrefix + (int)type;
            if (PlayerPrefs.HasKey(key)) data[key] = PlayerPrefs.GetString(key);
        }

        return data;
    }

    /// <summary>
    /// Buluttan gelen map'i PlayerPrefs'e yazar. Tip, Collect'in yazdığıyla aynı okunur
    /// (long/int → SetInt, string → SetString). Yerelde olup map'te olmayan anahtar SİLİNMEZ.
    /// </summary>
    public static void Apply(IDictionary<string, object> data)
    {
        if (data == null) return;

        // Apply normally merges keys; after a remote reset, remove older local progress first.
        if (ReadProgressResetRevision(data) > ProgressResetRevision)
        {
            int maxLevel = data.TryGetValue(LevelHistoryMaxKey, out var value)
                ? (int)System.Math.Min(int.MaxValue - 2L, ReadRevision(value)) : 1;
            ClearProgress(maxLevel);
            PlayerPrefs.DeleteKey(PendingProgressResetKey);
        }

        foreach (var kvp in data)
        {
            switch (kvp.Value)
            {
                case long l:   PlayerPrefs.SetInt(kvp.Key, (int)l); break;
                case int i:    PlayerPrefs.SetInt(kvp.Key, i); break;
                case string s: PlayerPrefs.SetString(kvp.Key, s); break;
                // double/bool beklenmiyor (Collect üretmez); bilinmeyen tip sessizce atlanır.
            }
        }

#if UNITY_EDITOR
        const int minEditorCoins = 500;
        int coins = PlayerPrefs.GetInt("player_coins", 0);
        if (coins < minEditorCoins)
        {
            PlayerPrefs.SetInt("player_coins", minEditorCoins);
            Debug.Log($"[CloudSaveManifest] Editor coin minimum applied after restore: {coins} -> {minEditorCoins}");
        }
#endif

        PlayerPrefs.Save();
    }
}
