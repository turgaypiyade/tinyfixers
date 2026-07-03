using System;
using System.Collections.Generic;
using TMPro;
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

    private static RuntimeChoicePopup _instance;

    public static void Show(string title, string message, params Choice[] choices)
    {
        if (_instance != null)
            Destroy(_instance.gameObject);

        var root = BuildCanvas();
        var popup = root.AddComponent<RuntimeChoicePopup>();
        popup.Build(root.transform, title, message, choices);
        _instance = popup;
        DontDestroyOnLoad(root);
    }

    public static void Dismiss()
    {
        if (_instance != null)
            Destroy(_instance.gameObject);
        _instance = null;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Close()
    {
        if (_instance == this) _instance = null;
        Destroy(gameObject);
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

        // Panel.
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var panel = (RectTransform)panelGo.transform;
        panel.SetParent(parent, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(820f, 0f);

        var panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0.11f, 0.15f, 0.26f, 1f);

        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(48, 48, 48, 48);
        vlg.spacing = 28f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (!string.IsNullOrEmpty(title))
            BuildText(panel, "Title", title, 58, FontStyles.Bold, new Color(1f, 0.96f, 0.78f, 1f), 120f);

        if (!string.IsNullOrEmpty(message))
            BuildText(panel, "Message", message, 38, FontStyles.Normal, Color.white, 200f);

        if (choices != null)
        {
            for (int i = 0; i < choices.Count; i++)
                BuildButton(panel, choices[i]);
        }
    }

    private static RectTransform BuildStretch(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void BuildText(Transform parent, string name, string value, float size,
        FontStyles style, Color color, float minHeight)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value ?? string.Empty;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = minHeight;
        le.flexibleWidth = 1f;
    }

    private void BuildButton(Transform parent, Choice choice)
    {
        var go = new GameObject("Btn_" + (choice.Label ?? "?"),
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = choice.Primary ? new Color(0.20f, 0.60f, 0.34f, 1f)
                                   : new Color(0.24f, 0.30f, 0.44f, 1f);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 118f;
        le.flexibleWidth = 1f;

        // Label.
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)labelGo.transform;
        lrt.SetParent(go.transform, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = choice.Label ?? string.Empty;
        label.fontSize = 42;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        Action cb = choice.OnClick;
        btn.onClick.AddListener(() =>
        {
            Close();          // callback sahne yükleyebilir → önce kapat
            cb?.Invoke();
        });
    }
}
