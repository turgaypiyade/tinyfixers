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

    // Legacy workshop keys (single-stage system)
    private const string KeyLegacyWorkshopCurrentStage = "workshop_current_stage";
    private const string KeyLegacyWorkshopFinalRewardClaimed = "workshop_final_reward_claimed";

    // Per-chapter workshop keys
    private const string KeyWorkshopChapterStagePrefix       = "workshop_chapter_";
    private const string KeyWorkshopChapterStageSuffix       = "_stage";
    private const string KeyWorkshopChapterRewardSuffix      = "_reward_claimed";

    private const int MaxLevelStarsReset = 500;
    private const int MaxWorkshopChaptersReset = 200;

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

        // Legacy keys (eski single-stage workshop)
        PlayerPrefs.DeleteKey(KeyLegacyWorkshopCurrentStage);
        PlayerPrefs.DeleteKey(KeyLegacyWorkshopFinalRewardClaimed);

        // Initial stars flag — sil ki tekrar 10 yıldız verilsin
        PlayerPrefs.DeleteKey("initial_stars_granted");

        // Tutorial seen flag
        PlayerPrefs.DeleteKey("tutorial_seen_workshop_repair");

        // Daily slot machine — her testte yeni spin hakkı
        PlayerPrefs.DeleteKey("daily_slot_last_spin_date");

        // Per-chapter workshop keys
        for (int chapter = 1; chapter <= MaxWorkshopChaptersReset; chapter++)
        {
            PlayerPrefs.DeleteKey($"{KeyWorkshopChapterStagePrefix}{chapter}{KeyWorkshopChapterStageSuffix}");
            PlayerPrefs.DeleteKey($"{KeyWorkshopChapterStagePrefix}{chapter}{KeyWorkshopChapterRewardSuffix}");
        }

        TutorialManager.ResetAll();
        ComboTutorialManager.ResetAll();

        PlayerPrefs.Save();

        Debug.Log("[TestLevelProgressionBootstrap] Fresh app launch. Progress, wallet, stars and tutorials reset.");
    }
}