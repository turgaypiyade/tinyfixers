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
        if (continueLabel != null) continueLabel.text = BridgeRepairUI.L("bridge_join_continue", "KATIL");
        if (overlayImage != null) overlayImage.gameObject.SetActive(overlayImage.sprite != null);
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
