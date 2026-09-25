using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One LevelEndPopup shell for runtime dialogs and existing scene forms.
/// Gameplay/auth callbacks remain with their original controllers.
/// </summary>
public sealed class CommonPopupView : MonoBehaviour
{
    public RectTransform Body { get; private set; }
    public RectTransform Actions { get; private set; }
    public TMP_Text Title { get; private set; }
    private RectTransform panel;
    private RectTransform host;

    public static CommonPopupView Create(Transform parent, string title, Action close, Material backgroundMaterial = null)
    {
        var skin = CommonPopupSkin.Shared;
        var rect = NewRect(parent, "CommonPopup", skin.panelSize);
        var view = rect.gameObject.AddComponent<CommonPopupView>();
        view.panel = rect;
        view.host = parent as RectTransform;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = skin.background;
        image.material = backgroundMaterial;
        image.color = skin.background != null ? Color.white : new Color(0.48f, 0.06f, 0.17f);
        view.Title = Text(rect, "Title", title, skin.titleFontSize, Color.white);
        Region(view.Title.rectTransform, skin.titleRegion);
        StyleTitle(view.Title);
        view.Body = NewRect(rect, "Body", Vector2.zero);
        Region(view.Body, skin.bodyRegion);
        view.Actions = NewRect(rect, "Actions", Vector2.zero);
        Region(view.Actions, skin.actionsRegion);
        if (close != null)
        {
            var button = Button(rect, "BtnClose", "×", close);
            Region((RectTransform)button.transform, skin.closeRegion);
            var bg = (Image)button.targetGraphic;
            bg.sprite = skin.closeButton;
            bg.preserveAspect = true;
            if (skin.closeButton != null)
                button.GetComponentInChildren<TMP_Text>().text = "";
        }
        view.Fit();
        return view;
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (host == null || panel == null || host.rect.width <= 0 || host.rect.height <= 0) return;
        float scale = Mathf.Min(1, (host.rect.width - 32) / panel.sizeDelta.x,
            (host.rect.height - 48) / panel.sizeDelta.y);
        panel.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    public static RectTransform NewRect(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    public static void Region(RectTransform rt, Rect region)
    {
        rt.anchorMin = region.min;
        rt.anchorMax = region.max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public static void Place(Component item, Transform parent, Rect region)
    {
        if (item == null) return;
        var rt = item.transform as RectTransform;
        if (rt == null) return;
        rt.SetParent(parent, false);
        Region(rt, region);
    }

    public static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
    {
        var rt = NewRect(parent, name, Vector2.zero);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(text, size, color);
        text.text = value ?? string.Empty;
        return text;
    }

    public static void StyleText(TMP_Text text, float size, Color color)
    {
        if (text == null) return;
        if (CommonPopupSkin.Shared.font != null) text.font = CommonPopupSkin.Shared.font;
        text.color = color;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(20, size);
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    public static void StyleTitle(TMP_Text text)
    {
        if (text == null) return;
        var skin = CommonPopupSkin.Shared;
        StyleText(text, skin.titleFontSize, Color.white);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var arc = text.GetComponent<TextArcEffect>();
        if (arc == null) arc = text.gameObject.AddComponent<TextArcEffect>();
        arc.SetArcAngle(skin.titleArcDegrees);
    }

    public static Button Button(Transform parent, string name, string label, Action onClick, Sprite sprite = null)
    {
        var rt = NewRect(parent, name, Vector2.zero);
        rt.gameObject.AddComponent<Image>();
        var button = rt.gameObject.AddComponent<Button>();
        var text = Text(rt, "BtnContinueText", label, 36, Color.white);
        Region(text.rectTransform, new Rect(0.08f, 0.15f, 0.84f, 0.70f));
        StyleButton(button, sprite);
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    public static void StyleButton(Button button, Sprite sprite = null)
    {
        if (button == null) return;
        var view = button.GetComponentInParent<CommonPopupView>(true);
        bool isFooterAction = view != null && button.transform.parent == view.Actions;
        float fontSize = isFooterAction ? CommonPopupSkin.Shared.actionFontSize : 36f;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite != null ? sprite : CommonPopupSkin.Shared.continueButton;
            image.type = Image.Type.Simple;
            image.color = image.sprite != null ? Color.white : new Color(0.2f, 0.6f, 0.3f);
            image.preserveAspect = true;
            button.targetGraphic = image;
        }

        // Keep the label inside the same aspect-fitted rectangle as the artwork.
        // The outer button remains the layout/touch slot; neither art nor text
        // stretches to fill a slot with a different width/height ratio.
        var labelBounds = button.transform.Find("LabelBounds") as RectTransform;
        if (labelBounds == null)
            labelBounds = NewRect(button.transform, "LabelBounds", Vector2.zero);
        var aspect = labelBounds.GetComponent<AspectRatioFitter>();
        if (aspect == null) aspect = labelBounds.gameObject.AddComponent<AspectRatioFitter>();
        if (image != null && image.sprite != null && image.sprite.rect.height > 0)
        {
            aspect.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }
        else
        {
            aspect.aspectMode = AspectRatioFitter.AspectMode.None;
            Region(labelBounds, new Rect(0, 0, 1, 1));
        }
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.transform.SetParent(labelBounds, false);
            StyleText(text, fontSize, Color.white);
            // Alt aksiyon butonları küçük; yazıya görselin daha büyük kısmını ver.
            Region(text.rectTransform, isFooterAction
                ? new Rect(0.05f, 0.10f, 0.90f, 0.80f)
                : new Rect(0.08f, 0.15f, 0.84f, 0.70f));
        }
    }

    // One action is centered; multiple choices keep the same usable touch size.
    public static Rect ActionRegion(int index, int count)
    {
        // Tek aksiyon: eski 0.84x0.88 alanın %66'sı, aynı merkezde.
        if (count <= 1) return new Rect(0.2228f, 0.2096f, 0.5544f, 0.5808f);
        if (count == 2) return new Rect(index * 0.52f, 0.10f, 0.48f, 0.80f);
        int rows = (count + 1) / 2;
        float height = 1f / rows;
        bool centered = index == count - 1 && count % 2 != 0;
        return new Rect(centered ? 0.26f : (index % 2) * 0.52f,
            1 - (index / 2 + 1) * height, 0.48f, height - 0.035f);
    }
}
