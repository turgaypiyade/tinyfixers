using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Koddan kurulan basit modal onay/seçim penceresi. Sahneye prefab gömmeye gerek yok;
/// LoadingScreenManager gibi kendi canvas'ını runtime'da inşa eder → her sahnede çalışır.
///
/// Kullanım:
///   RuntimeChoicePopup.Show("Başlık", "Mesaj",
///       new RuntimeChoicePopup.Choice("Evet", () => ...),
///       new RuntimeChoicePopup.Choice("Hayır", null));
///
/// Aynı anda tek pop-up gösterilir (yeni Show öncekini kapatır). Buton callback'i
/// çağrılmadan ÖNCE pop-up kapatılır (callback sahne yükleyebilir).
/// </summary>
public sealed class RuntimeChoicePopup : MonoBehaviour
{
    public readonly struct Choice
    {
        public readonly string Label;
        public readonly Action OnClick;
        public readonly bool Primary;

        public Choice(string label, Action onClick, bool primary = false)
        {
            Label = label;
            OnClick = onClick;
            Primary = primary;
        }
    }

    /// SaveProgressPopup'taki gibi gövdede büyük buton: başlık + alt yazı, kapalıysa soluk görünür.
    public readonly struct OfferButton
    {
        public readonly string Label;
        public readonly string Subtitle;
        public readonly Action OnClick;
        public readonly bool Interactable;

        public OfferButton(string label, string subtitle, Action onClick, bool interactable = true)
        {
            Label = label;
            Subtitle = subtitle;
            OnClick = onClick;
            Interactable = interactable;
        }
    }

    private static RuntimeChoicePopup _instance;

    /// SaveProgressPopup düzeni: üstte durum yazısı, gövdede alt alta büyük seçenek butonları,
    /// altta tek aksiyon butonu. Başlıktaki X yalnız popup'ı kapatır (onClose çağrılmaz).
    /// defaultFrame=true: çerçeve ortak popup'ın orijinal renginde kalır (buton stilleri aynı).
    public static void ShowOffer(string title, string status, OfferButton[] options, string actionLabel, Action onAction,
        bool defaultFrame = false)
    {
        Dismiss();

        var root = BuildCanvas();
        var popup = root.AddComponent<RuntimeChoicePopup>();
        popup.BuildOffer(root.transform, title, status, options, actionLabel, onAction, defaultFrame);
        _instance = popup;
        DontDestroyOnLoad(root);
    }

    public static void Show(string title, string message, params Choice[] choices)
    {
        Dismiss();

        var root = BuildCanvas();
        var popup = root.AddComponent<RuntimeChoicePopup>();
        popup.Build(root.transform, title, message, choices);
        _instance = popup;
        DontDestroyOnLoad(root);
    }

    public static void Dismiss()
    {
        if (_instance != null)
        {
            _instance.gameObject.SetActive(false);
            Destroy(_instance.gameObject);
        }
        _instance = null;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private bool closing;
    private CommonPopupView view;

    // Çıkış animasyonu oynarken popup artık "kapalı" sayılır (yeni Show engellenmez, çift tık yok);
    // callback'ler Close'dan hemen sonra çağrılmaya devam eder.
    private void Close()
    {
        if (closing || !gameObject.activeSelf) return;
        closing = true;
        if (_instance == this) _instance = null;

        var anim = view != null ? view.Entrance : null;
        if (anim != null)
            anim.PlayExit(() => { if (this != null) Destroy(gameObject); });
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────

    private static GameObject BuildCanvas()
    {
        var go = new GameObject("RuntimeChoicePopup");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // fail popup / loading üstünde

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private void Build(Transform parent, string title, string message, IReadOnlyList<Choice> choices)
    {
        // Karartma + raycast bloklayıcı (arka plana tık geçmesin).
        var dim = BuildStretch(parent, "Dim");
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);
        dimImg.raycastTarget = true;

        view = CommonPopupView.Create(parent, title, Close);
        var body = CommonPopupView.Text(view.Body, "Message", message, 38,
            CommonPopupSkin.Shared.bodyTextColor);
        CommonPopupView.Region(body.rectTransform, new Rect(0, 0, 1, 1));

        int count = choices != null ? choices.Count : 0;
        if (count == 0)
        {
            var done = CommonPopupView.Button(view.Actions, "BtnContinue", "Tamam", Close);
            CommonPopupView.Region((RectTransform)done.transform, CommonPopupView.ActionRegion(0, 1));
        }
        for (int i = 0; i < count; i++)
        {
            Choice choice = choices[i];
            var button = CommonPopupView.Button(view.Actions,
                choice.Primary || i == 0 ? "BtnContinue" : "BtnChoice_" + i,
                choice.Label, () =>
                {
                    if (closing || !gameObject.activeSelf) return;
                    Close();
                    choice.OnClick?.Invoke();
                });
            CommonPopupView.Region((RectTransform)button.transform, CommonPopupView.ActionRegion(i, count));
        }
    }

    private void BuildOffer(Transform parent, string title, string status, IReadOnlyList<OfferButton> options,
        string actionLabel, Action onAction, bool defaultFrame)
    {
        var dim = BuildStretch(parent, "Dim");
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);
        dimImg.raycastTarget = true;

        view = defaultFrame
            ? CommonPopupView.Create(parent, title, Close)
            : CommonPopupView.Create(parent, title, Close, CommonPopupSkin.Shared.saveProgressBackgroundMaterial);

        var statusText = CommonPopupView.Text(view.Body, "Status", status, 34, CommonPopupSkin.Shared.bodyTextColor);
        CommonPopupView.Region(statusText.rectTransform, new Rect(0, 0.80f, 1, 0.20f));

        int count = options != null ? options.Count : 0;
        var list = CommonPopupView.NewRect(view.Body, "Options", Vector2.zero);
        CommonPopupView.Region(list, new Rect(0, 0, 1, 0.76f));
        for (int i = 0; i < count; i++)
        {
            // Buton yüksekliği %30 (SaveProgressPopup ölçüsü); alan içinde EŞİT aralıkla dikey ortalanır.
            const float height = 0.30f;
            float gap = Mathf.Max(0f, (1f - count * height) / (count + 1));
            float y = 1f - (i + 1) * (gap + height);
            MakeOfferButton(list, options[i], y);
        }

        var action = CommonPopupView.Button(view.Actions, "BtnContinue", actionLabel, () =>
        {
            if (closing || !gameObject.activeSelf) return;
            Close();
            onAction?.Invoke();
        }, CommonPopupSkin.Shared.saveProgressContinueButton);
        CommonPopupView.Region((RectTransform)action.transform, CommonPopupView.ActionRegion(0, 1));
    }

    private void MakeOfferButton(Transform parent, OfferButton option, float y)
    {
        var button = CommonPopupView.Button(parent, "Btn_" + option.Label, option.Label, () =>
        {
            if (closing || !gameObject.activeSelf) return;
            Close();
            option.OnClick?.Invoke();
        }, CommonPopupSkin.Shared.accountButton);
        CommonPopupView.Region((RectTransform)button.transform, new Rect(0, y, 1, 0.30f));

        var label = button.GetComponentInChildren<TMPro.TMP_Text>();
        CommonPopupView.StyleText(label, 48, Color.white);
        label.fontSizeMin = 32;
        label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        CommonPopupView.Region(label.rectTransform, new Rect(0.08f, 0.39f, 0.84f, 0.46f));

        var subtitle = CommonPopupView.Text(label.transform.parent, "OfferSubtitle", option.Subtitle ?? "", 26,
            new Color(0.88f, 0.95f, 1f));
        subtitle.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        CommonPopupView.Region(subtitle.rectTransform, new Rect(0.08f, 0.14f, 0.84f, 0.25f));

        var colors = button.colors;
        colors.disabledColor = new Color(0.70f, 0.74f, 0.80f, 0.85f);
        button.colors = colors;
        button.interactable = option.Interactable;
    }

    private static RectTransform BuildStretch(Transform parent, string name)
    {
        var rt = CommonPopupView.NewRect(parent, name, Vector2.zero);
        CommonPopupView.Region(rt, new Rect(0, 0, 1, 1));
        return rt;
    }
}
