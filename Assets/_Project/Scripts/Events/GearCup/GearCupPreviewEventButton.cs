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

    private void OnDisable() => GameLocalization.OnLanguageChanged -= Refresh;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    private void Refresh()
    {
        var config = GearCupPreviewConfig.Shared;
        bool unlocked = config != null && config.eventEnabled && GearCupState.IsUnlocked;
        visibility.alpha = unlocked ? 1 : 0;
        visibility.blocksRaycasts = visibility.interactable = unlocked;
        if (iconAnimator != null && iconAnimator.enabled != unlocked) iconAnimator.enabled = unlocked;
        if (screen != null && countdown != null)
            countdown.text = TimeFormat.Countdown(screen.PreviewTimeRemaining);
    }
}
