using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "İlerlemeyi Kaydet" popup'ı (referans RM ayar akışı, Docs/ProductionPlan.md P4):
/// Facebook / Google / Apple ile giriş → AuthLinkService.Link (anonim hesaba bağlama,
/// uid korunur). SDK'sı henüz ekli olmayan provider "Yakında" görünür; bağlanmışsa
/// popup "Hesabın bağlı ✓" durumunu gösterir. Çakışmada basit onay satırı çıkar.
///
/// UI tamamen RUNTIME kurulur (sahne/prefab bağımlılığı yok) — SettingsPanel'in
/// canvas'ına scrim + kart olarak eklenir. Görsel cila istenirse sprite'lar sonra basılır.
/// </summary>
public sealed class SaveProgressPopup : MonoBehaviour
{
    private GameObject root;
    private TMP_Text statusText;
    private Button confirmSwitchButton;
    private GameObject confirmRow;
    private AuthLinkService.Provider pendingProvider;
    private Firebase.Auth.Credential pendingCredential;

    private static readonly Color FacebookBlue = new Color(0.23f, 0.35f, 0.60f);
    private static readonly Color GoogleAmber  = new Color(0.95f, 0.62f, 0.15f);
    private static readonly Color AppleBlack   = new Color(0.13f, 0.13f, 0.15f);

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
        if (confirmRow != null) confirmRow.SetActive(false);
        RefreshStatus();

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
        if (confirmRow != null) confirmRow.SetActive(false);
        FirebaseCloudSaveService.Push();   // kalıcı kimlikle hemen yedekle
    }

    private void HandleFailed(AuthLinkService.Provider p, string message)
        => SetStatus(message, positive: false);

    private void HandleConflict(AuthLinkService.Provider p, Firebase.Auth.Credential credential)
    {
        pendingProvider = p;
        pendingCredential = credential;
        SetStatus("Bu hesap başka bir kayda bağlı. O kayda geçilsin mi?", positive: false);
        if (confirmRow != null) confirmRow.SetActive(true);
    }

    private void SetStatus(string msg, bool positive)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusText.color = positive ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.85f, 0.4f);
    }

    private void RefreshStatus()
    {
        if (AuthLinkService.IsLinked)
            SetStatus("Hesabın bağlı ✓ — ilerlemen her cihazda güvende.", positive: true);
        else
            SetStatus("İlerlemeni kaydetmek için giriş yap!", positive: true);
    }

    // ── Runtime UI kurulumu ─────────────────────────────────────────

    private void Build()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Scrim (arkayı karart + tıklama yut)
        var scrim = gameObject.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.72f);

        // Kart
        var card = NewRect("Card", transform, new Vector2(720, 760));
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = new Color(0.16f, 0.22f, 0.42f, 0.98f);

        var title = NewText("Title", card, "İlerlemeyi Kaydet", 44, FontStyles.Bold);
        Top(title.rectTransform, 84, 24);

        var closeBtn = NewButton("Close", card, "✕", new Color(0.75f, 0.2f, 0.2f), new Vector2(84, 84));
        var crt = ((RectTransform)closeBtn.transform);
        crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1);
        crt.anchoredPosition = new Vector2(-10, -10);
        closeBtn.onClick.AddListener(Close);

        statusText = NewText("Status", card, "", 28, FontStyles.Normal);
        Top(statusText.rectTransform, 70, 118);

        // Provider butonları (referans sıra: Facebook, Google, Apple)
        MakeProviderButton(card, "Facebook ile giriş yap", FacebookBlue, AuthLinkService.Provider.Facebook, 210);
        MakeProviderButton(card, "Google ile giriş yap", GoogleAmber, AuthLinkService.Provider.Google, 330);
        MakeProviderButton(card, "Apple ile giriş yap", AppleBlack, AuthLinkService.Provider.Apple, 450);

        // Çakışma onay satırı (başta kapalı)
        confirmRow = NewRect("ConfirmRow", card, new Vector2(620, 90)).gameObject;
        Top((RectTransform)confirmRow.transform, 90, 580);
        var yes = NewButton("Yes", confirmRow.transform, "Evet, o hesaba geç", new Color(0.2f, 0.6f, 0.3f), new Vector2(360, 84));
        var yrt = (RectTransform)yes.transform;
        yrt.anchorMin = yrt.anchorMax = new Vector2(0, 0.5f); yrt.pivot = new Vector2(0, 0.5f);
        yrt.anchoredPosition = Vector2.zero;
        yes.onClick.AddListener(() => AuthLinkService.SwitchToExisting(pendingProvider, pendingCredential));
        var no = NewButton("No", confirmRow.transform, "Vazgeç", new Color(0.45f, 0.45f, 0.5f), new Vector2(220, 84));
        var nrt = (RectTransform)no.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(1, 0.5f); nrt.pivot = new Vector2(1, 0.5f);
        nrt.anchoredPosition = Vector2.zero;
        no.onClick.AddListener(() => { confirmRow.SetActive(false); RefreshStatus(); });
        confirmRow.SetActive(false);
    }

    private void MakeProviderButton(RectTransform card, string label, Color color,
                                    AuthLinkService.Provider provider, float y)
    {
        bool available = AuthLinkService.IsAvailable(provider);
        var btn = NewButton("Btn_" + provider, card, available ? label : label + "  (Yakında)",
            available ? color : Color.Lerp(color, Color.gray, 0.6f), new Vector2(620, 100));
        Top((RectTransform)btn.transform, 100, y);
        btn.interactable = available;
        btn.onClick.AddListener(() =>
        {
            SetStatus("Bağlanıyor...", positive: true);
            AuthLinkService.Link(provider);
        });
    }

    // ── küçük UGUI yardımcıları ─────────────────────────────────────

    private RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        return rt;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
    {
        var rt = NewRect(name, parent, new Vector2(620, 60));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private Button NewButton(string name, Transform parent, string label, Color color, Vector2 size)
    {
        var rt = NewRect(name, parent, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = NewText("Label", rt, label, 30, FontStyles.Bold);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        return btn;
    }

    // Üstten y px aşağıda, yatay ortalı, sabit yükseklik.
    private static void Top(RectTransform rt, float height, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 1); rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
    }
}
