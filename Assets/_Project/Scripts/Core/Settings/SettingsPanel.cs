using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings popup UI controller.
/// Inspector'dan toggle/button referansları bağlanır.
/// Music/Sound/Vibration/Hint/Notification toggle'ları + Save Progress + Help & Support + Follow Instagram butonları.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundDismissButton;

    [Header("Toggles")]
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Toggle hintToggle;
    [SerializeField] private Toggle notificationToggle;

    [Header("Buttons")]
    [SerializeField] private Button saveProgressButton;
    [SerializeField] private Button helpSupportButton;
    [SerializeField] private Button followInstagramButton;

    [Header("Save Progress Feedback")]
    [Tooltip("Save sonrası kısa süreliğine görünür olacak \"Kaydedildi\" etiketi (opsiyonel).")]
    [SerializeField] private GameObject savedConfirmationLabel;
    [SerializeField, Min(0.2f)] private float savedConfirmationDuration = 1.5f;

    [Header("Instagram")]
    [Tooltip("Instagram URL'i. Bir kez takip edip altın aldıktan sonra buton disable olur.")]
    [SerializeField] private string instagramUrl = "https://www.instagram.com/tinyfixers";
    [SerializeField, Min(1)] private int instagramFollowRewardCoins = 100;
    [Tooltip("Instagram takip edildikten sonra butonun \"Takip ediliyor\" görsel state'i (opsiyonel).")]
    [SerializeField] private GameObject instagramFollowedBadge;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.18f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backgroundDismissButton != null) backgroundDismissButton.onClick.AddListener(Close);

        if (musicToggle != null)        musicToggle.onValueChanged.AddListener(v => GameSettings.MusicEnabled = v);
        if (soundToggle != null)        soundToggle.onValueChanged.AddListener(v => GameSettings.SoundEnabled = v);
        if (vibrationToggle != null)    vibrationToggle.onValueChanged.AddListener(v => GameSettings.VibrationEnabled = v);
        if (hintToggle != null)         hintToggle.onValueChanged.AddListener(v => GameSettings.HintEnabled = v);
        if (notificationToggle != null) notificationToggle.onValueChanged.AddListener(v => GameSettings.NotificationEnabled = v);

        if (saveProgressButton != null)      saveProgressButton.onClick.AddListener(OnSaveProgressClicked);
        if (helpSupportButton != null)
        {
            helpSupportButton.onClick.AddListener(OnHelpSupportClicked);
            helpSupportButton.interactable = false; // şimdilik devre dışı
        }
        if (followInstagramButton != null)   followInstagramButton.onClick.AddListener(OnFollowInstagramClicked);

        if (savedConfirmationLabel != null) savedConfirmationLabel.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Open()
    {
        if (panelRoot == null) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelRoot.SetActive(true);

        SyncTogglesFromSettings();
        SyncInstagramButtonState();

        if (panelGroup != null) panelGroup.alpha = 0f;
        StartCoroutine(FadePanel(0f, 1f));
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        StartCoroutine(CloseRoutine());
    }

    // ── Toggle Sync ──────────────────────────────────────────────────────────

    private void SyncTogglesFromSettings()
    {
        SetToggleSilent(musicToggle,        GameSettings.MusicEnabled);
        SetToggleSilent(soundToggle,        GameSettings.SoundEnabled);
        SetToggleSilent(vibrationToggle,    GameSettings.VibrationEnabled);
        SetToggleSilent(hintToggle,         GameSettings.HintEnabled);
        SetToggleSilent(notificationToggle, GameSettings.NotificationEnabled);
    }

    private void SetToggleSilent(Toggle toggle, bool value)
    {
        if (toggle == null) return;
        // SetIsOnWithoutNotify event tetiklemez — initial sync için doğru yöntem.
        toggle.SetIsOnWithoutNotify(value);
    }

    // ── Save Progress ────────────────────────────────────────────────────────

    private void OnSaveProgressClicked()
    {
        PlayerPrefs.Save();
        Debug.Log("[Settings] Progress saved (PlayerPrefs.Save).");

        if (savedConfirmationLabel != null)
            StartCoroutine(ShowSavedConfirmation());
    }

    private IEnumerator ShowSavedConfirmation()
    {
        savedConfirmationLabel.SetActive(true);
        yield return new WaitForSecondsRealtime(savedConfirmationDuration);
        if (savedConfirmationLabel != null) savedConfirmationLabel.SetActive(false);
    }

    // ── Help & Support ───────────────────────────────────────────────────────

    private void OnHelpSupportClicked()
    {
        // Şimdilik disable; ileride mailto/in-game ticket sistemi açılacak.
        Debug.Log("[Settings] Help & Support clicked (no-op).");
    }

    // ── Instagram Follow Reward ──────────────────────────────────────────────

    private void OnFollowInstagramClicked()
    {
        // Instagram URL'i aç
        if (!string.IsNullOrEmpty(instagramUrl))
            Application.OpenURL(instagramUrl);

        // İlk kez takip ediyorsa ödülü ver, butonu disable et
        if (!GameSettings.InstagramFollowed)
        {
            PlayerWallet.AddCoins(instagramFollowRewardCoins);
            GameSettings.InstagramFollowed = true;
            Debug.Log($"[Settings] Instagram follow reward granted: {instagramFollowRewardCoins} coins.");
        }

        SyncInstagramButtonState();
    }

    private void SyncInstagramButtonState()
    {
        if (followInstagramButton == null) return;
        bool alreadyFollowed = GameSettings.InstagramFollowed;

        // Buton hala tıklanabilir (URL açmak için), ama ödül bir kez verilir
        if (instagramFollowedBadge != null)
            instagramFollowedBadge.SetActive(alreadyFollowed);
    }

    // ── Fade ─────────────────────────────────────────────────────────────────

    private IEnumerator CloseRoutine()
    {
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator FadePanel(float from, float to)
    {
        if (panelGroup == null) yield break;
        panelGroup.alpha = from;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        panelGroup.alpha = to;
    }
}
