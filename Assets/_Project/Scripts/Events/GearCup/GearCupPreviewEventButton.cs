using TMPro;
using UnityEngine;

/// <summary>Countdown in the menu badge's empty ribbon and the shared event icon animation.</summary>
public sealed class GearCupPreviewEventButton : MonoBehaviour
{
    [SerializeField] private GearCupPreviewScreen screen;
    [SerializeField] private TMP_Text countdown;
    private float nextRefresh;
    private CanvasGroup visibility;
    private EventIconAnimator iconAnimator;
    private float menuReadyAt = -1f;

    public void Configure(GearCupPreviewScreen target, TMP_Text label)
    {
        screen = target;
        countdown = label;
    }

    private void OnEnable()
    {
        if (!TryGetComponent(out visibility)) visibility = gameObject.AddComponent<CanvasGroup>();
        TryGetComponent(out iconAnimator);
        GameLocalization.OnLanguageChanged += Refresh;
        Refresh();
        nextRefresh = Time.unscaledTime + 1f;
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= Refresh;
        menuReadyAt = -1f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        Refresh();
        TryShowReminder();
    }

    private void Refresh()
    {
        var config = GearCupPreviewConfig.Shared;
        bool unlocked = config != null && GearCupState.IsMenuAvailable;
        visibility.alpha = unlocked ? 1 : 0;
        visibility.blocksRaycasts = visibility.interactable = unlocked;
        if (iconAnimator != null && iconAnimator.enabled != unlocked) iconAnimator.enabled = unlocked;
        if (screen != null && countdown != null)
            countdown.text = unlocked ? TimeFormat.Countdown(screen.PreviewTimeRemaining) : "";
    }

    private void TryShowReminder()
    {
        if (RuntimeSimulationSession.IsActive || screen == null) return;
        if (!FirebaseCloudSaveService.RestoreResolved || LoadingScreenManager.IsVisible || CustomIntroLoadingManager.IsVisible)
        { menuReadyAt = -1f; return; }
        if (menuReadyAt < 0f) menuReadyAt = Time.unscaledTime;
        // Existing Safari/Bridge intros begin two seconds after loading; join their modal flow after them.
        if (Time.unscaledTime - menuReadyAt < 3f) return;
        var config = GearCupPreviewConfig.Shared;
        if (config == null || !config.eventEnabled || !GearCupState.IsUnlocked) return;
        bool resultPending = GearCupState.HasPendingResult;
        if (!resultPending && (!config.autoShowJoinPopup || !GearCupState.CanShowInvitation || !PlayerTeamState.HasTeam)) return;
        string cycle = GearCupRules.SeasonKey(GearCupRules.SeasonStart(System.DateTime.UtcNow, config.seasonHours));
        if (!resultPending && PlayerPrefs.GetString(GearCupState.InvitationAskedKey, "") == cycle) return;
        if (screen.gameObject.activeInHierarchy || screen.IsInvitationVisible || RuntimeChoicePopup.IsShowing
            || RewardChestRevealOverlay.IsShowing || ShopPurchaseCelebration.IsShowing || MarketNavigator.IsMenuMarketOpen()) return;
        if (FindFirstObjectByType<TutorialOverlayController>() != null) return;
        if (!WorkshopRepairButtonTutorial.IsSeen() && FindFirstObjectByType<WorkshopRepairButtonTutorial>() != null) return;
        var popups = FindObjectsByType<PopupEntranceAnimator>(FindObjectsSortMode.None);
        for (int i = 0; i < popups.Length; i++) if (popups[i].IsVisible) return;
        if (resultPending) { screen.TryShowPendingResult(); return; }
        screen.Open();
    }
}
