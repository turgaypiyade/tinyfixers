using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "İlerlemeyi Kaydet" popup'ı (referans RM ayar akışı, Docs/ProductionPlan.md P4):
/// Facebook / Google / Apple ile giriş → AuthLinkService.Link (anonim hesaba bağlama,
/// uid korunur). SDK'sı henüz ekli olmayan provider "Yakında" görünür; bağlanmışsa
/// popup "Hesabın bağlı ✓" durumunu gösterir. Çakışmada seçenekler yerine onay alanı açılır.
///
/// UI runtime'da ortak LevelEndPopup görünümüyle SettingsPanel'in canvas'ına eklenir.
/// </summary>
public sealed class SaveProgressPopup : MonoBehaviour
{
    private TMP_Text statusText;
    private readonly Dictionary<AuthLinkService.Provider, Button> providerButtons = new();
    private readonly Dictionary<AuthLinkService.Provider, TMP_Text> providerLabels = new();
    private Button doneButton, switchButton, cancelButton;
    private GameObject providerChoices;
    private GameObject confirmRow;
    private AuthLinkService.Provider pendingProvider;
    private Firebase.Auth.Credential pendingCredential;

    /// <summary>Popup'ı gösterir (yoksa kurar). parentCanvas = SettingsPanel'in kökü.</summary>
    public static void Show(Transform parentCanvas)
    {
        var existing = parentCanvas.GetComponentInChildren<SaveProgressPopup>(true);
        if (existing == null)
        {
            var go = new GameObject("SaveProgressPopup", typeof(RectTransform));
            go.transform.SetParent(parentCanvas, false);
            go.layer = parentCanvas.gameObject.layer;   // Screen Space Camera culling tuzağı
            existing = go.AddComponent<SaveProgressPopup>();
            existing.Build();
        }
        existing.Open();
    }

    private void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowConfirmation(false);
        RefreshStatus();
        RefreshButtons();

        AuthLinkService.OnLinked -= HandleLinked;
        AuthLinkService.OnLinkFailed -= HandleFailed;
        AuthLinkService.OnLinkConflict -= HandleConflict;
        AuthLinkService.OnStateChanged -= HandleState;
        AuthLinkService.OnCanceled -= HandleCanceled;
        FirebaseAuthService.OnReady -= HandleReady;
        FirebaseCloudSaveService.OnRestored -= HandleReady;
        AuthLinkService.OnLinked += HandleLinked;
        AuthLinkService.OnLinkFailed += HandleFailed;
        AuthLinkService.OnLinkConflict += HandleConflict;
        AuthLinkService.OnStateChanged += HandleState;
        AuthLinkService.OnCanceled += HandleCanceled;
        FirebaseAuthService.OnReady += HandleReady;
        FirebaseCloudSaveService.OnRestored += HandleReady;
    }

    private void OnDisable()
    {
        AuthLinkService.OnLinked -= HandleLinked;
        AuthLinkService.OnLinkFailed -= HandleFailed;
        AuthLinkService.OnLinkConflict -= HandleConflict;
        AuthLinkService.OnStateChanged -= HandleState;
        AuthLinkService.OnCanceled -= HandleCanceled;
        FirebaseAuthService.OnReady -= HandleReady;
        FirebaseCloudSaveService.OnRestored -= HandleReady;
        if (!AuthLinkService.IsBusy) AuthLinkService.CancelConflict();
        pendingCredential = null;
    }

    private void Close()
    {
        if (AuthLinkService.IsBusy) return;
        AuthLinkService.CancelConflict();
        gameObject.SetActive(false);
    }

    private void HandleState()
    {
        RefreshButtons();
        if (AuthLinkService.IsBusy) SetStatus(GameLocalization.Get("save_progress_connecting"), true);
    }
    private void HandleReady() { RefreshButtons(); if (!AuthLinkService.IsBusy) RefreshStatus(); }
    private void HandleCanceled() { RefreshButtons(); RefreshStatus(); }

    private void RefreshButtons()
    {
        foreach (var entry in providerButtons)
        {
            bool linked = AuthLinkService.HasProvider(entry.Key);
            bool available = AuthLinkService.IsAvailable(entry.Key);
            entry.Value.interactable = available && AuthLinkService.CanStart && !linked;
            providerLabels[entry.Key].text = GameLocalization.Get(linked ? "save_progress_provider_linked"
                : !available ? "save_progress_provider_unavailable" : "save_progress_provider_continue");
        }
        if (doneButton != null) doneButton.interactable = !AuthLinkService.IsBusy;
        if (switchButton != null) switchButton.interactable = !AuthLinkService.IsBusy;
        if (cancelButton != null) cancelButton.interactable = !AuthLinkService.IsBusy;
    }

    // ── AuthLink olayları ───────────────────────────────────────────

    private void HandleLinked(AuthLinkService.Provider p)
    {
        SetStatus(GameLocalization.GetFormat("save_progress_linked_provider", p), positive: true);
        ShowConfirmation(false);
        RefreshButtons();
    }

    private void HandleFailed(AuthLinkService.Provider p, string message)
    {
        SetStatus(message, positive: false);
        RefreshButtons();
    }

    private void HandleConflict(AuthLinkService.Provider p, Firebase.Auth.Credential credential)
    {
        pendingProvider = p;
        pendingCredential = credential;
        SetStatus(GameLocalization.Get("save_progress_conflict"), positive: false);
        ShowConfirmation(true);
    }

    private void ShowConfirmation(bool show)
    {
        if (providerChoices != null) providerChoices.SetActive(!show);
        if (confirmRow != null) confirmRow.SetActive(show);
        if (!show) { pendingCredential = null; AuthLinkService.CancelConflict(); }
    }

    private void SetStatus(string msg, bool positive)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.color = positive ? new Color(0.12f, 0.36f, 0.18f) : new Color(0.65f, 0.16f, 0.10f);
    }

    private void RefreshStatus()
    {
        if (!FirebaseAuthService.IsReady || !FirebaseCloudSaveService.RestoreResolved)
            SetStatus(GameLocalization.Get("auth_session_not_ready"), positive: true);
        else if (AuthLinkService.IsLinked)
            SetStatus(GameLocalization.Get("save_progress_linked"), positive: true);
        else
            SetStatus(GameLocalization.Get("save_progress_not_linked"), positive: true);
    }

    // ── Runtime UI kurulumu ─────────────────────────────────────────

    private void Build()
    {
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        var view = CommonPopupView.Create(transform, GameLocalization.Get("save_progress_title"), Close,
            CommonPopupSkin.Shared.saveProgressBackgroundMaterial);
        statusText = CommonPopupView.Text(view.Body, "Status", "", 34, CommonPopupSkin.Shared.bodyTextColor);
        CommonPopupView.Region(statusText.rectTransform, new Rect(0, 0.80f, 1, 0.20f));

        providerChoices = CommonPopupView.NewRect(view.Body, "Providers", Vector2.zero).gameObject;
        CommonPopupView.Region((RectTransform)providerChoices.transform, new Rect(0, 0, 1, 0.76f));
#if UNITY_ANDROID
        // Android'de Apple ile giriş yok (eklenti iOS'a özel) → iki buton, aralıklı ortalanır.
        MakeProviderButton(providerChoices.transform, "Facebook", AuthLinkService.Provider.Facebook, 0.52f);
        MakeProviderButton(providerChoices.transform, "Google", AuthLinkService.Provider.Google, 0.16f);
#else
        MakeProviderButton(providerChoices.transform, "Facebook", AuthLinkService.Provider.Facebook, 0.68f);
        MakeProviderButton(providerChoices.transform, "Google", AuthLinkService.Provider.Google, 0.34f);
        MakeProviderButton(providerChoices.transform, "Apple", AuthLinkService.Provider.Apple, 0);
#endif

        confirmRow = CommonPopupView.NewRect(view.Body, "ConfirmRow", Vector2.zero).gameObject;
        CommonPopupView.Region((RectTransform)confirmRow.transform, new Rect(0, 0, 1, 0.76f));
        var yes = switchButton = CommonPopupView.Button(confirmRow.transform, "ConfirmSwitch", GameLocalization.Get("save_progress_switch"),
            () => AuthLinkService.SwitchToExisting(pendingProvider, pendingCredential), CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)yes.transform, new Rect(0, 0.52f, 1, 0.30f));
        var no = cancelButton = CommonPopupView.Button(confirmRow.transform, "CancelSwitch", GameLocalization.Get("common_cancel"),
            () => { ShowConfirmation(false); RefreshStatus(); }, CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)no.transform, new Rect(0, 0.14f, 1, 0.30f));
        CommonPopupView.StyleText(yes.GetComponentInChildren<TMP_Text>(), 42, Color.white);
        CommonPopupView.StyleText(no.GetComponentInChildren<TMP_Text>(), 42, Color.white);
        confirmRow.SetActive(false);
        var done = doneButton = CommonPopupView.Button(view.Actions, "BtnContinue", GameLocalization.Get("common_ok"), Close,
            CommonPopupSkin.Shared.saveProgressContinueButton);
        CommonPopupView.Region((RectTransform)done.transform, CommonPopupView.ActionRegion(0, 1));
    }

    private void MakeProviderButton(Transform parent, string label, AuthLinkService.Provider provider, float y)
    {
        bool available = AuthLinkService.IsAvailable(provider);
        var button = CommonPopupView.Button(parent, "Btn_" + provider, label, () =>
            {
                SetStatus(GameLocalization.Get("save_progress_connecting"), positive: true);
                AuthLinkService.Link(provider);
            }, CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)button.transform, new Rect(0, y, 1, 0.30f));
        var title = button.GetComponentInChildren<TMP_Text>();
        CommonPopupView.StyleText(title, 48, Color.white);
        title.fontSizeMin = 32;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        CommonPopupView.Region(title.rectTransform, new Rect(0.08f, 0.39f, 0.84f, 0.46f));
        var subtitle = CommonPopupView.Text(title.transform.parent, "ProviderStatus",
            GameLocalization.Get(available ? "save_progress_provider_continue" : "common_coming_soon"), 26, new Color(0.88f, 0.95f, 1f));
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        CommonPopupView.Region(subtitle.rectTransform, new Rect(0.08f, 0.14f, 0.84f, 0.25f));
        var colors = button.colors;
        colors.disabledColor = new Color(0.70f, 0.74f, 0.80f, 0.85f);
        button.colors = colors;
        providerButtons[provider] = button;
        providerLabels[provider] = subtitle;
        button.interactable = available && AuthLinkService.CanStart;
    }
}
