using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnLaunch()
    {
#if UNITY_EDITOR
        var settings = Resources.Load<EditorTestSettings>("EditorTestSettings");
        int level = settings != null ? settings.testLevel : 1;
#else
        int level = 1;
#endif
        // Progress event verilerini koru, geri kalanı sıfırla.
        string savedStartTime = PlayerPrefs.GetString("progress_event_v1_start_time", "");
        string savedGoals     = PlayerPrefs.GetString("progress_event_v1_goals", "");
        PlayerPrefs.DeleteAll();
        if (!string.IsNullOrEmpty(savedStartTime)) PlayerPrefs.SetString("progress_event_v1_start_time", savedStartTime);
        if (!string.IsNullOrEmpty(savedGoals))     PlayerPrefs.SetString("progress_event_v1_goals", savedGoals);
        PlayerPrefs.SetInt("current_level", level);
        PlayerPrefs.SetInt("player_coins", 100);
        PlayerPrefs.SetInt("player_total_stars", 10);
        PlayerPrefs.SetInt("initial_stars_granted", 1);
        PlayerPrefs.SetInt("first_launch_done", 1);
        PlayerPrefs.Save();

        Debug.Log($"[TestLevelProgressionBootstrap] Starting at level {level} with 100 coins, 10 stars.");
    }
}
