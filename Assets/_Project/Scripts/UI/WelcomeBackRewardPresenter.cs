using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Uses the existing one-second lives tick, without another persistent object/update loop.</summary>
public static class WelcomeBackRewardPresenter
{
    public static void Tick()
    {
        if (RuntimeSimulationSession.IsActive || SceneManager.GetActiveScene().name != "MainMenu"
            || !WelcomeBackReward.HasPending
            || !FirebaseCloudSaveService.RestoreResolved || LoadingScreenManager.IsVisible
            || CustomIntroLoadingManager.IsVisible || RuntimeChoicePopup.IsShowing
            || ShopPurchaseCelebration.IsShowing || RewardChestRevealOverlay.IsShowing
            || MarketNavigator.IsMenuMarketOpen()) return;

        // Only inspect UI while a gift is pending, at most once a second. Never in board hot paths.
        var popups = Object.FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++)
            if (popups[i].IsVisible) return;
        if (!WelcomeBackReward.GrantPending()) return;
        FirebaseCloudSaveService.MarkDirty();
        RewardChestRevealOverlay.Show(WelcomeBackReward.CreatePresentation(), titleKey: "welcome_back_reward_title");
    }
}
