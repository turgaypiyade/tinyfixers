using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    private const int MinEditorStartingCoins = 500;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnLaunch()
    {
        // KRİTİK: yalnız EDITOR'de çalışır. Cihaz build'inde her açılışta DeleteAll
        // yapmak tüm oyuncu ilerlemesini siler (2026-07-19'da yakalanan launch bug'ı).
#if UNITY_EDITOR
        var settings = Resources.Load<EditorTestSettings>("EditorTestSettings");
        int level = settings != null ? settings.testLevel : 1;
        bool hasEditorCoinOverride = PlayerPrefs.HasKey("editor_test_coins");
        int editorCoinOverride = hasEditorCoinOverride
            ? Mathf.Max(MinEditorStartingCoins, PlayerPrefs.GetInt("editor_test_coins", MinEditorStartingCoins))
            : MinEditorStartingCoins;
        int coins = hasEditorCoinOverride
            ? editorCoinOverride
            : PlayerPrefs.HasKey("player_coins")
            ? Mathf.Max(MinEditorStartingCoins, PlayerPrefs.GetInt("player_coins", MinEditorStartingCoins))
            : MinEditorStartingCoins;
        // Progress event ve timed reward verilerini koru, geri kalanı sıfırla.
        string savedStartTime = PlayerPrefs.GetString("progress_event_v1_start_time", "");
        string savedGoals     = PlayerPrefs.GetString("progress_event_v1_goals", "");

        // Yıldız + wonder ilerlemesini KORU (yazdığın değerle başla; yoksa 100 başlangıç).
        int savedStars      = PlayerPrefs.HasKey("player_total_stars") ? PlayerPrefs.GetInt("player_total_stars") : 100;
        int savedWonderDone = PlayerPrefs.GetInt("wonder_completed_count", 0);
        int savedWonderStg  = PlayerPrefs.GetInt("wonder_current_stage", 0);

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
        if (hasEditorCoinOverride)
            PlayerPrefs.SetInt("editor_test_coins", editorCoinOverride);
        PlayerPrefs.SetInt("current_level", level);
        PlayerPrefs.SetInt("player_coins", coins);
        PlayerPrefs.SetInt("player_total_stars", savedStars);          // korunan/girilen değer
        PlayerPrefs.SetInt("wonder_completed_count", savedWonderDone); // wonder ilerlemesi korunur
        PlayerPrefs.SetInt("wonder_current_stage", savedWonderStg);
        PlayerPrefs.SetInt("initial_stars_granted", 1);
        PlayerPrefs.SetInt("first_launch_done", 1);
        PlayerPrefs.Save();

        Debug.Log($"[TestLevelProgressionBootstrap] Starting at level {level} with {coins} coins, {savedStars} stars, wonder {savedWonderDone}/{savedWonderStg}.");
#endif
    }
}
