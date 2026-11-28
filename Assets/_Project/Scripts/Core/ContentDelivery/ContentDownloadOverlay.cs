using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only shown when prefetch has not finished. No inventory/life is spent behind this gate.</summary>
public sealed class ContentDownloadOverlay : MonoBehaviour
{
    private ProgressiveContent source;
    private TMP_Text status;
    private Button retryButton;
    private Button cancelButton;
    private readonly List<Action> cancelHandlers = new();
    private int mandatoryUsers;
    private bool failed;
    private float nextRefresh;
    private static ContentDownloadOverlay instance;
    public static bool IsVisible => instance != null;
    private int users;
    public int RetryVersion { get; private set; }

    public static ContentDownloadOverlay Open(ProgressiveContent source, Action cancel = null)
    {
        if (instance != null) { instance.AddUser(cancel); return instance; }
        var go = new GameObject("ContentDownload", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(go.transform, false);
        var rect = (RectTransform)background.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.10f, 0.97f);
        var self = go.AddComponent<ContentDownloadOverlay>();
        instance = self;
        self.source = source;
        self.status = Label(go.transform, "", new Vector2(850, 240), new Vector2(0, 40));
        var buttonGo = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(go.transform, false);
        var br = (RectTransform)buttonGo.transform;
        br.sizeDelta = new Vector2(460, 100); br.anchoredPosition = new Vector2(0, -150);
        buttonGo.GetComponent<Image>().color = new Color(0.1f, 0.45f, 0.85f);
        self.retryButton = buttonGo.GetComponent<Button>();
        Label(buttonGo.transform, GameLocalization.GetOr("content_retry", "Retry"), new Vector2(440, 90), Vector2.zero);
        self.retryButton.onClick.AddListener(() => self.RetryVersion++);
        var cancelGo = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelGo.transform.SetParent(go.transform, false);
        var cr = (RectTransform)cancelGo.transform;
        cr.sizeDelta = new Vector2(460, 100); cr.anchoredPosition = new Vector2(0, -280);
        cancelGo.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f);
        self.cancelButton = cancelGo.GetComponent<Button>();
        Label(cancelGo.transform, GameLocalization.GetOr("common_cancel", "Cancel"), new Vector2(440, 90), Vector2.zero);
        self.cancelButton.onClick.AddListener(() => {
            foreach (var handler in self.cancelHandlers.ToArray()) handler();
        });
        self.AddUser(cancel);
        self.ShowProgress();
        return self;
    }
    private static TMP_Text Label(Transform parent, string text, Vector2 size, Vector2 position)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 34; label.color = Color.white; label.raycastTarget = false;
        label.text = text;
        return label;
    }
    public void ShowFailure()
    {
        failed = true;
        status.text = GameLocalization.GetOr("content_download_failed", "Download could not finish. Check your connection and free storage, then try again.");
        retryButton.gameObject.SetActive(true);
    }
    public void ShowProgress()
    {
        failed = false; nextRefresh = 0;
        retryButton.gameObject.SetActive(false);
        Refresh();
    }
    private void AddUser(Action cancel)
    {
        users++;
        if (cancel == null) mandatoryUsers++; else cancelHandlers.Add(cancel);
        cancelButton.gameObject.SetActive(mandatoryUsers == 0 && cancelHandlers.Count > 0);
    }
    public void Release(Action cancel = null)
    {
        if (cancel == null) mandatoryUsers--; else cancelHandlers.Remove(cancel);
        cancelButton.gameObject.SetActive(mandatoryUsers == 0 && cancelHandlers.Count > 0);
        if (--users > 0) return;
        if (instance == this) instance = null;
        Destroy(gameObject);
    }
    private void OnDestroy() { if (instance == this) instance = null; }
    private void Update()
    {
        if (failed || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh();
    }
    private void Refresh()
    {
        if (source == null || status == null) return;
        if (!source.DownloadingForView)
        {
            status.text = GameLocalization.GetOr("content_preparing_local", "Preparing content…");
            return;
        }
        status.text = string.Format(GameLocalization.GetOr("content_downloading", "Preparing content…\n{0}% · {1:0.0} MB"),
            Mathf.RoundToInt(source.Progress * 100), source.DownloadBytes / (1024f * 1024f));
    }
}
