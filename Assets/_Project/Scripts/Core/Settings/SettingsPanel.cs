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
    [SerializeField] private Toggle hdrToggle;
    [SerializeField] private TMP_Text hdrLabel;

    private HdrDisplayTrial hdrTrial;
    private SlideToggleAnimator hdrAnimator;

    [Header("Buttons")]
    [SerializeField] private Button saveProgressButton;
    [SerializeField] private Button helpSupportButton;
    [SerializeField] private Button followInstagramButton;
    [Tooltip("\"Bizi Değerlendir\" — mağazanın yorum sayfasını açar (StoreReviewService).")]
    [SerializeField] private Button rateUsButton;
    [Tooltip("\"Terms&Privacy\" — boşsa panelde 'TermsButton' adlı buton bulunur. Kullanım Şartları / Gizlilik seçimi açar.")]
    [SerializeField] private Button termsButton;

    [Header("Save Progress Feedback")]
    [Tooltip("Save sonrası kısa süreliğine görünür olacak \"Kaydedildi\" etiketi (opsiyonel).")]
    [SerializeField] private GameObject savedConfirmationLabel;
    [SerializeField, Min(0.2f)] private float savedConfirmationDuration = 1.5f;

    [Header("Instagram")]
    [Tooltip("Instagram URL'i. Bir kez takip edip altın aldıktan sonra buton disable olur.")]
    [SerializeField] private string instagramUrl = "https://www.instagram.com/wondigames/";
    [SerializeField, Min(1)] private int instagramFollowRewardCoins = 100;
    [Tooltip("Instagram takip edildikten sonra butonun \"Takip ediliyor\" görsel state'i (opsiyonel).")]
    [SerializeField] private GameObject instagramFollowedBadge;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.18f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (hdrToggle != null)
        {
            hdrTrial = HdrDisplayTrial.GetOrCreate();
            hdrAnimator = hdrToggle.GetComponent<SlideToggleAnimator>();
            hdrToggle.onValueChanged.AddListener(OnHdrToggleChanged);
        }
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
        }
        if (followInstagramButton != null)   followInstagramButton.onClick.AddListener(OnFollowInstagramClicked);
        if (rateUsButton != null)            rateUsButton.onClick.AddListener(StoreReviewService.OpenStorePage);
        if (termsButton == null)             termsButton = FindButton("TermsButton");
        if (termsButton != null)             termsButton.onClick.AddListener(OnTermsClicked);

        if (savedConfirmationLabel != null) savedConfirmationLabel.SetActive(false);
    }

    private void OnEnable()
    {
        GameSettings.OnMusicChanged        += OnSettingChanged;
        GameSettings.OnSoundChanged        += OnSettingChanged;
        GameSettings.OnVibrationChanged    += OnSettingChanged;
        GameSettings.OnHintChanged         += OnSettingChanged;
        GameSettings.OnNotificationChanged += OnSettingChanged;
        if (hdrTrial != null) hdrTrial.Changed += SyncHdrToggle;
        SyncTogglesFromSettings();
        SyncHdrToggle();
    }

    private void OnDisable()
    {
        GameSettings.OnMusicChanged        -= OnSettingChanged;
        GameSettings.OnSoundChanged        -= OnSettingChanged;
        GameSettings.OnVibrationChanged    -= OnSettingChanged;
        GameSettings.OnHintChanged         -= OnSettingChanged;
        GameSettings.OnNotificationChanged -= OnSettingChanged;
        if (hdrTrial != null) hdrTrial.Changed -= SyncHdrToggle;
    }

    private void OnDestroy()
    {
        if (hdrTrial != null) hdrTrial.Changed -= SyncHdrToggle;
        if (hdrToggle != null) hdrToggle.onValueChanged.RemoveListener(OnHdrToggleChanged);
    }

    private void OnHdrToggleChanged(bool requested)
    {
        if (hdrTrial != null && hdrTrial.CanToggle && requested != hdrTrial.IsHdrActive)
            hdrTrial.Toggle();
        // Native switching is asynchronous; always show the actual output state.
        SyncHdrToggle();
    }

    private void SyncHdrToggle()
    {
        if (hdrTrial == null || hdrToggle == null) return;
        hdrToggle.SetIsOnWithoutNotify(hdrTrial.IsHdrActive);
        if (hdrAnimator != null) hdrAnimator.SyncWithoutAnimation();
        hdrToggle.interactable = hdrTrial.CanToggle;
        if (hdrLabel == null) return;
        switch (hdrTrial.State)
        {
            case HdrDisplayTrial.DisplayState.Unavailable: hdrLabel.text = "HDR (N/A)"; break;
            case HdrDisplayTrial.DisplayState.Switching: hdrLabel.text = "HDR (...)"; break;
            case HdrDisplayTrial.DisplayState.Failed: hdrLabel.text = "HDR (!)"; break;
            default: hdrLabel.text = "HDR"; break;
        }
    }

    private void OnSettingChanged(bool _) => SyncTogglesFromSettings();

    // ── Public API ───────────────────────────────────────────────────────────

    public void Open()
    {
        if (panelRoot == null) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelRoot.SetActive(true);

        SyncTogglesFromSettings();
        SyncInstagramButtonState();
        SyncHdrToggle();

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

        // Gerçek kayıt = bulut: manifest verisini hemen Firestore'a it (P1 cloud save).
        FirebaseCloudSaveService.Push();

        // P4 (referans RM akışı): giriş popup'ı — Facebook/Google/Apple ile hesabı bağla.
        // Bağlıysa popup durumu gösterir; SDK'sı gelmeyen provider "Yakında" görünür.
        SaveProgressPopup.Show(transform);

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

    // Canlı destek: Tawk.to sohbet sayfası (çevrimdışıyken mesaj bileti olur). Oyuncunun arkadaş kodu ile ilk mesaj
    // yazılabilsin diye kod panoya kopyalanmaz — oyuncu profil ekranında görür.
    private const string SupportChatUrl = "https://tawk.to/chat/6ad344724cf72734cbfe452f/1k54k6oid";

    private void OnHelpSupportClicked()
    {
        Application.OpenURL(SupportChatUrl);
    }

    // ── Terms & Privacy ──────────────────────────────────────────────────────

    // "Can bitti" penceresiyle aynı kart düzeni: belgeler açılıp kapanınca bu pencereye dönülür (KeepOpen);
    // "Hesabımı Sil" uyarı alt yazılı ayrı bir kart, ardından onay penceresi (Vazgeç → buraya geri döner).
    private void OnTermsClicked()
    {
        RuntimeChoicePopup.ShowOffer(GameLocalization.Get("settings_terms_title"), GameLocalization.Get("settings_terms_body"),
            new[]
            {
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("settings_terms_open_terms"),
                    GameLocalization.Get("legal_card_terms_sub"), () => LegalDocumentView.Show("terms"), keepOpen: true),
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("settings_terms_open_privacy"),
                    GameLocalization.Get("legal_card_privacy_sub"), () => LegalDocumentView.Show("privacy"), keepOpen: true),
                new RuntimeChoicePopup.OfferButton(GameLocalization.Get("settings_delete_account"),
                    GameLocalization.Get("settings_delete_account_sub"), ConfirmDeleteAccount),
            },
            GameLocalization.Get("common_close"), null);
    }

    // ── Hesabı sil (App Store 5.1.1(v)) ──────────────────────────────────────

    private void ConfirmDeleteAccount()
    {
        RuntimeChoicePopup.Show(GameLocalization.Get("account_delete_title"), GameLocalization.Get("account_delete_body"),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("account_delete_confirm"), StartDeleteAccount),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("common_cancel"), () => { if (this != null) OnTermsClicked(); },
                primary: true));
    }

    private static void StartDeleteAccount()
    {
        // Butonsuz bekleme penceresi yok: tek "Tamam" kapatır, silme arka planda sürer; başarıda oyun baştan açılır.
        RuntimeChoicePopup.Show(GameLocalization.Get("account_delete_title"), GameLocalization.Get("account_delete_working"));
        AccountDeletionService.DeleteAccount((ok, error) =>
        {
            if (!ok)
                RuntimeChoicePopup.Show(GameLocalization.Get("account_delete_title"), error);
        });
    }

    private Button FindButton(string objectName)
    {
        // Panel kökü (SettingsPanel) altında ara: TermsFrame/TermsButton panelRoot'un kardeşi olabilir.
        foreach (var b in GetComponentsInChildren<Button>(true))
            if (b.name == objectName) return b;
        return null;
    }

    // ── Instagram Follow Reward ──────────────────────────────────────────────

    private void OnFollowInstagramClicked()
    {
        RuntimeChoicePopup.Show("Instagram", GameLocalization.Get("settings_instagram_body"),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("settings_instagram_open"), () =>
            {
                if (this != null) OpenInstagram();
            }, primary: true),
            new RuntimeChoicePopup.Choice(GameLocalization.Get("common_cancel"), null));
    }

    private void OpenInstagram()
    {
        // Instagram URL'i aç
        if (!string.IsNullOrEmpty(instagramUrl))
            Application.OpenURL(instagramUrl);

        // İlk kez takip ediyorsa ödülü ver, butonu disable et
        if (!GameSettings.InstagramFollowed)
        {
            PlayerWallet.AddCoins(instagramFollowRewardCoins);
            GameSettings.InstagramFollowed = true;
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
