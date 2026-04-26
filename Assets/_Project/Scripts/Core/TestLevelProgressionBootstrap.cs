using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    private const string PrefsLevelKey = "current_level";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetCurrentLevelForTestSession()
    {
        PlayerPrefs.SetInt(PrefsLevelKey, 1);
        PlayerPrefs.Save();

        Debug.Log("[TestLevelProgressionBootstrap] current_level reset to 1.");
    }
}