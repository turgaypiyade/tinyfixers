using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    private const string PrefsLevelKey = "current_level";
    private const string ResetOnBootKey = "debug_reset_current_level_on_boot";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetCurrentLevelForTestSession()
    {
        if (PlayerPrefs.GetInt(ResetOnBootKey, 0) == 0)
            return;

        PlayerPrefs.SetInt(PrefsLevelKey, 1);
        PlayerPrefs.DeleteKey(ResetOnBootKey);
        PlayerPrefs.Save();

        Debug.Log("[TestLevelProgressionBootstrap] current_level reset to 1.");
    }
}
