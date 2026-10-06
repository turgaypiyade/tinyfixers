using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge Repair katılım popup'ı — Safari popup'ının AYNISI (kurulum sahnedeki SafariJoinPopup'ı kopyalar):
/// EvenSelectorPopup paneli + başlık + ortada BridgeRepairPopupImg + yeşil "Katıl" + sağ-üst X.
/// "Katıl" → katıl (controller: Nasıl oynanır → yarış ekranı); X → kapat, ikon aktif kalır.
/// </summary>
public sealed class BridgeRepairJoinPopup : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image overlayImage;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private Button cancelButton;
    [Tooltip("Opsiyonel: kalan süre yazısı.")]
    [SerializeField] private TMP_Text timerText;

    [Header("Resim üstü bilgi (koddan kurulur)")]
    [Tooltip("Başlık resmin gökyüzü kısmına (DynaPuff, kullanıcı isteği 2026-10-05), bilgi + süre alttaki krem alana yazılır.")]
    [SerializeField, Range(0f, 1f)] private float headlineY = 0.88f;
    [SerializeField, Range(0f, 1f)] private float infoY = 0.13f;
    [SerializeField, Range(0f, 1f)] private float timerY = 0.05f;

    private TMP_Text headlineText, infoText;
    private Action onContinue, onLater;
    private DateTime windowEnd;
    private bool showRequested, wired;
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleCaptured;

    private void Awake()
    {
        Wire();
        if (root != null && root != gameObject) root.SetActive(false);
        else if (!showRequested) gameObject.SetActive(false);
    }

    private void Wire()
    {
        if (wired) return;
        wired = true;
        if (continueButton != null) continueButton.onClick.AddListener(() => { EventSfx.Play(x => x.uiTap); Close(onContinue); });
        if (cancelButton != null)   cancelButton.onClick.AddListener(() => Close(onLater));
    }

    public void Show(DateTime end, Action continueAction, Action laterAction)
    {
        onContinue = continueAction;
        onLater = laterAction;
        windowEnd = end;
        showRequested = true;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        Wire();

        if (titleText != null) titleText.text = BridgeRepairUI.L("bridge_title", "BRIDGE REPAIR");
        if (continueLabel != null) continueLabel.text = BridgeRepairUI.L("event_join_button", "Katıl");   // ortak event "Katıl" (büyük harf değil)
        if (overlayImage != null) overlayImage.gameObject.SetActive(overlayImage.sprite != null);
        BuildImageTexts();
        RefreshTimer();
        EventSfx.Play(x => x.popupOpen);

        if (popupRoot == null) return;
        if (!baseScaleCaptured) { baseScale = popupRoot.localScale; baseScaleCaptured = true; }
        Canvas.ForceUpdateCanvases();
        var viewport = popupRoot.parent as RectTransform;
        float fit = 1f;
        if (viewport != null)
        {
            float w = popupRoot.rect.width, h = popupRoot.rect.height;
            if (w > viewport.rect.width || h > viewport.rect.height)
                fit = Mathf.Min(viewport.rect.width / Mathf.Max(1f, w), viewport.rect.height / Mathf.Max(1f, h));
        }
        popupRoot.localScale = baseScale * fit;
        PopupEntranceAnimator.Enter(popupRoot);
    }

    // Resmin üstüne: gökyüzünde başlık (DynaPuff, oyun anı stili), alttaki krem alanda kural özeti + kalan süre.
    // Süre yazısı sahnede bağlı değilse burada kurulur.
    private void BuildImageTexts()
    {
        if (overlayImage == null || !overlayImage.gameObject.activeSelf) return;
        var host = overlayImage.rectTransform;
        var skin = CommonPopupSkin.Shared;
        var config = BridgeRepairConfig.Shared;

        if (headlineText == null)
        {
            headlineText = MakeText("Headline", host, headlineY, 0.86f, 0.13f);
            if (skin != null && skin.celebrationFont != null)
            {
                headlineText.font = skin.celebrationFont;
                if (skin.celebrationMaterial != null) headlineText.fontSharedMaterial = skin.celebrationMaterial;
            }
        }
        headlineText.text = BridgeRepairUI.L("bridge_join_headline", "Köprüyü ilk sen tamamla!");

        if (infoText == null)
        {
            infoText = MakeText("Info", host, infoY, 0.9f, 0.08f);
            ApplyBodyStyle(infoText, skin);
        }
        int levels = config != null ? config.levelsToFinish : 15;
        int players = config != null ? config.contestantCount : 5;
        int ranks = config != null ? config.prizeRanks : 3;
        infoText.text = BridgeRepairUI.LFormat("bridge_join_info", "{0} level · {1} kişilik yarış · ilk {2} ödül kazanır",
            levels, players, ranks);

        if (timerText == null)
        {
            timerText = MakeText("Timer", host, timerY, 0.7f, 0.07f);
            ApplyBodyStyle(timerText, skin);
        }
    }

    private static TMP_Text MakeText(string name, RectTransform host, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = host.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(host, false);
        rt.anchorMin = new Vector2(0.5f - width * 0.5f, y - height * 0.5f);
        rt.anchorMax = new Vector2(0.5f + width * 0.5f, y + height * 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 90f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    // Krem zemin üstünde koyu bordo, kalın (BakbakOne skin fontu).
    private static void ApplyBodyStyle(TMP_Text text, CommonPopupSkin skin)
    {
        if (skin != null && skin.font != null) text.font = skin.font;
        text.color = new Color(0.45f, 0.08f, 0.10f, 1f);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    private void Update() => RefreshTimer();

    private void RefreshTimer()
    {
        if (timerText == null) return;
        var remaining = windowEnd - DateTime.UtcNow;
        timerText.text = windowEnd == DateTime.MinValue || remaining <= TimeSpan.Zero
            ? ""
            : BridgeRepairUI.LFormat("bridge_time_left", "Kalan süre: {0}", TimeFormat.Countdown(remaining));
    }

    private void Close(Action then)
    {
        Hide();
        var cb = then;
        onContinue = onLater = null;
        cb?.Invoke();
    }
}
