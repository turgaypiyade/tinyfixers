using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana ekran Tiny Safari event ikonu (diğer event ikonları gibi). Görünürlüğü controller yönetir
/// (level kapısı + aktif gün). Tık → controller.OnIconClicked (katılmadıysa popup, katıldıysa harita).
///
/// İdle animasyon için aynı objeye <see cref="EventIconAnimator"/> eklenebilir.
/// </summary>
public sealed class SafariEventButton : MonoBehaviour
{
    [SerializeField] private SafariEventController controller;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [Tooltip("Görünürlük için açılıp kapanacak kök. Boşsa bu GameObject kullanılır.")]
    [SerializeField] private GameObject visibilityRoot;

    private string defaultLabel = "SAFARI";
    private int lastShownSeconds = int.MinValue;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClicked);
        if (visibilityRoot == null) visibilityRoot = gameObject;
        if (labelText != null && !string.IsNullOrEmpty(labelText.text))
            defaultLabel = labelText.text;
        RefreshLabel();
    }

    private void OnEnable()
    {
        SafariState.OnChanged += RefreshLabel;
        RefreshLabel();
    }

    private void OnDisable()
    {
        SafariState.OnChanged -= RefreshLabel;
    }

    private void Update()
    {
        var remaining = SafariState.FallCooldownRemaining(DateTime.UtcNow);
        if (remaining <= TimeSpan.Zero)
        {
            if (lastShownSeconds != int.MinValue)
                RefreshLabel();
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        if (seconds == lastShownSeconds) return;

        RefreshLabel();
    }

    private void OnClicked()
    {
        if (SafariState.FallCooldownRemaining(DateTime.UtcNow) > TimeSpan.Zero)
        {
            RefreshLabel();
            return;
        }

        if (controller != null) controller.OnIconClicked();
    }

    public void SetVisible(bool visible)
    {
        if (visibilityRoot != null && visibilityRoot.activeSelf != visible)
            visibilityRoot.SetActive(visible);
        if (visible) RefreshLabel();
    }

    private void RefreshLabel()
    {
        TimeSpan remaining = SafariState.FallCooldownRemaining(DateTime.UtcNow);
        bool canContinue = remaining <= TimeSpan.Zero;
        lastShownSeconds = canContinue ? int.MinValue : Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));

        if (button != null)
            button.interactable = canContinue;

        if (labelText == null) return;
        if (!canContinue)
            labelText.text = FormatRemaining(remaining);
        else
            labelText.text = defaultLabel;
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return $"{minutes:00}:{secs:00}";
    }
}
