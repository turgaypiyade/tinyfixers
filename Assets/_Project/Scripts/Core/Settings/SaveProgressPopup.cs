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

        AuthLinkService.OnLinked -= HandleLinked;
        AuthLinkService.OnLinkFailed -= HandleFailed;
        AuthLinkService.OnLinkConflict -= HandleConflict;
        AuthLinkService.OnLinked += HandleLinked;
        AuthLinkService.OnLinkFailed += HandleFailed;
        AuthLinkService.OnLinkConflict += HandleConflict;
    }

    private void OnDisable()
    {
        AuthLinkService.OnLinked -= HandleLinked;
        AuthLinkService.OnLinkFailed -= HandleFailed;
        AuthLinkService.OnLinkConflict -= HandleConflict;
    }

    private void Close() => gameObject.SetActive(false);

    // ── AuthLink olayları ───────────────────────────────────────────

    private void HandleLinked(AuthLinkService.Provider p)
    {
        SetStatus($"Hesabın bağlandı ✓ ({p})", positive: true);
        ShowConfirmation(false);
        FirebaseCloudSaveService.Push();   // kalıcı kimlikle hemen yedekle
    }

    private void HandleFailed(AuthLinkService.Provider p, string message)
        => SetStatus(message, positive: false);

    private void HandleConflict(AuthLinkService.Provider p, Firebase.Auth.Credential credential)
    {
        pendingProvider = p;
        pendingCredential = credential;
        SetStatus("Bu hesap başka bir kayda bağlı. O kayda geçilsin mi?", positive: false);
        ShowConfirmation(true);
    }

    private void ShowConfirmation(bool show)
    {
        if (providerChoices != null) providerChoices.SetActive(!show);
        if (confirmRow != null) confirmRow.SetActive(show);
    }

    private void SetStatus(string msg, bool positive)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.color = positive ? new Color(0.12f, 0.36f, 0.18f) : new Color(0.65f, 0.16f, 0.10f);
    }

    private void RefreshStatus()
    {
        if (AuthLinkService.IsLinked)
            SetStatus("Hesabın bağlı ✓\nİlerlemen güvende.", positive: true);
        else
            SetStatus("İlerlemeni korumak için\nhesabını bağla.", positive: true);
    }

    // ── Runtime UI kurulumu ─────────────────────────────────────────

    private void Build()
    {
        CommonPopupView.Region((RectTransform)transform, new Rect(0, 0, 1, 1));
        gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        var view = CommonPopupView.Create(transform, "İlerlemeyi Kaydet", Close,
            CommonPopupSkin.Shared.saveProgressBackgroundMaterial);
        statusText = CommonPopupView.Text(view.Body, "Status", "", 34, CommonPopupSkin.Shared.bodyTextColor);
        CommonPopupView.Region(statusText.rectTransform, new Rect(0, 0.80f, 1, 0.20f));

        providerChoices = CommonPopupView.NewRect(view.Body, "Providers", Vector2.zero).gameObject;
        CommonPopupView.Region((RectTransform)providerChoices.transform, new Rect(0, 0, 1, 0.76f));
        MakeProviderButton(providerChoices.transform, "Facebook", AuthLinkService.Provider.Facebook, 0.68f);
        MakeProviderButton(providerChoices.transform, "Google", AuthLinkService.Provider.Google, 0.34f);
        MakeProviderButton(providerChoices.transform, "Apple", AuthLinkService.Provider.Apple, 0);

        confirmRow = CommonPopupView.NewRect(view.Body, "ConfirmRow", Vector2.zero).gameObject;
        CommonPopupView.Region((RectTransform)confirmRow.transform, new Rect(0, 0, 1, 0.76f));
        var yes = CommonPopupView.Button(confirmRow.transform, "ConfirmSwitch", "Bu hesaba geç",
            () => AuthLinkService.SwitchToExisting(pendingProvider, pendingCredential), CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)yes.transform, new Rect(0, 0.52f, 1, 0.30f));
        var no = CommonPopupView.Button(confirmRow.transform, "CancelSwitch", "Vazgeç",
            () => { ShowConfirmation(false); RefreshStatus(); }, CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)no.transform, new Rect(0, 0.14f, 1, 0.30f));
        CommonPopupView.StyleText(yes.GetComponentInChildren<TMP_Text>(), 42, Color.white);
        CommonPopupView.StyleText(no.GetComponentInChildren<TMP_Text>(), 42, Color.white);
        confirmRow.SetActive(false);
        var done = CommonPopupView.Button(view.Actions, "BtnContinue", "Tamam", Close,
            CommonPopupSkin.Shared.saveProgressContinueButton);
        CommonPopupView.Region((RectTransform)done.transform, CommonPopupView.ActionRegion(0, 1));
    }

    private void MakeProviderButton(Transform parent, string label, AuthLinkService.Provider provider, float y)
    {
        bool available = AuthLinkService.IsAvailable(provider);
        var button = CommonPopupView.Button(parent, "Btn_" + provider, label, () =>
            {
                SetStatus("Bağlanıyor...", positive: true);
                AuthLinkService.Link(provider);
            }, CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)button.transform, new Rect(0, y, 1, 0.30f));
        var title = button.GetComponentInChildren<TMP_Text>();
        CommonPopupView.StyleText(title, 48, Color.white);
        title.fontSizeMin = 32;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        CommonPopupView.Region(title.rectTransform, new Rect(0.08f, 0.39f, 0.84f, 0.46f));
        var subtitle = CommonPopupView.Text(title.transform.parent, "ProviderStatus",
            available ? "ile devam et" : "Yakında", 26, new Color(0.88f, 0.95f, 1f));
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        CommonPopupView.Region(subtitle.rectTransform, new Rect(0.08f, 0.14f, 0.84f, 0.25f));
        var colors = button.colors;
        colors.disabledColor = new Color(0.70f, 0.74f, 0.80f, 0.85f);
        button.colors = colors;
        button.interactable = available;
    }
}
