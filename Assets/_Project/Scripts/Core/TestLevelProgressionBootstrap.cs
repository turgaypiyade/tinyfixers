using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnLaunch()
    {
        // KRİTİK: yalnız EDITOR'de çalışır. Cihaz build'inde her açılışta DeleteAll
        // yapmak tüm oyuncu ilerlemesini siler (2026-07-19'da yakalanan launch bug'ı).
#if UNITY_EDITOR
        var settings = Resources.Load<EditorTestSettings>("EditorTestSettings");
        int level = settings != null ? settings.testLevel : 1;
        // Progress event ve timed reward verilerini koru, geri kalanı sıfırla.
        string savedStartTime = PlayerPrefs.GetString("progress_event_v1_start_time", "");
        string savedGoals     = PlayerPrefs.GetString("progress_event_v1_goals", "");

        var timedRewardTypes = new[] { 1, 10, 11, 12, 13 }; // Lives, Joker_LineH, PulseCore, Override, Joker_Line
        var savedTimedRewards = new string[timedRewardTypes.Length];
        for (int t = 0; t < timedRewardTypes.Length; t++)
            savedTimedRewards[t] = PlayerPrefs.GetString($"timed_reward_{timedRewardTypes[t]}", "");

        PlayerPrefs.DeleteAll();

        if (!string.IsNullOrEmpty(savedStartTime)) PlayerPrefs.SetString("progress_event_v1_start_time", savedStartTime);
        if (!string.IsNullOrEmpty(savedGoals))     PlayerPrefs.SetString("progress_event_v1_goals", savedGoals);
        for (int t = 0; t < timedRewardTypes.Length; t++)
            if (!string.IsNullOrEmpty(savedTimedRewards[t]))
                PlayerPrefs.SetString($"timed_reward_{timedRewardTypes[t]}", savedTimedRewards[t]);
        PlayerPrefs.SetInt("current_level", level);
        PlayerPrefs.SetInt("player_coins", 100);
        PlayerPrefs.SetInt("player_total_stars", 10);
        PlayerPrefs.SetInt("initial_stars_granted", 1);
        PlayerPrefs.SetInt("first_launch_done", 1);
        PlayerPrefs.Save();

        Debug.Log($"[TestLevelProgressionBootstrap] Starting at level {level} with 100 coins, 10 stars.");
#endif
    }
}
