using UnityEngine;

public static class TestLevelProgressionBootstrap
{
    private const string KeyCurrentLevel = "current_level";
    private const string KeyCoins = "player_coins";
    private const string KeyTotalStars = "player_total_stars";
    private const string KeyLevelStarsPrefix = "level_stars_";

    private const string KeyPendingStarReward = "pending_star_reward";
    private const string KeyPendingStarBefore = "pending_star_before";
    private const string KeyPendingStarAfter = "pending_star_after";

    private const int MaxLevelStarsReset = 500;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetProgressOnFreshAppLaunch()
    {
        // Fresh app launch: start as a new player.
        PlayerPrefs.SetInt(KeyCurrentLevel, 1);
        PlayerPrefs.SetInt(KeyCoins, 0);

        PlayerPrefs.DeleteKey(KeyTotalStars);
        PlayerPrefs.DeleteKey(KeyPendingStarReward);
        PlayerPrefs.DeleteKey(KeyPendingStarBefore);
        PlayerPrefs.DeleteKey(KeyPendingStarAfter);

        for (int i = 1; i <= MaxLevelStarsReset; i++)
            PlayerPrefs.DeleteKey(KeyLevelStarsPrefix + i);

        PlayerPrefs.Save();

        Debug.Log("[TestLevelProgressionBootstrap] Fresh app launch. Progress, wallet and stars reset.");
    }
}