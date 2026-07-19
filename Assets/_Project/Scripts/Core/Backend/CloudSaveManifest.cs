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
        "player_team_joined",
        "player_team_emblem",
        "player_team_min_chapter",
        "player_team_is_creator",
        "initial_stars_granted",
        "prelevel_specials_rewarded",
        "first_launch_done",
        "boss_tip_weakness_seen",
        "tutorial_seen_workshop_repair",
        "real_users_seen_max",   // bot evreni azalma eğrisi cihazlar arası tutarlı kalsın
        "music_selected",        // seçili müzik parçası
    };

    // PlayerPrefs'te STRING yazılan anahtarlar (SetString/GetString).
    private static readonly string[] StringKeys =
    {
        "player_name",
        "player_id",
        "friend_code",
        "friends_list",
        "friends_real",
        "friends_dismissed",
        "player_team_name",
        "player_team_id",
        "player_team_desc",
        "lives_next_ticks",
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
    };
    private const int MaxEnumScan = 64;

    // timed_reward_{DailySlotRewardType} → STRING (expiry ticks).
    private const string TimedRewardPrefix = "timed_reward_";

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

        // Bölüm başına yıldız/puan: 1..current_level (+pay, restore sonrası ileride kalmış olabilir).
        int maxLevel = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1)) + 2;
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

        PlayerPrefs.Save();
    }
}
