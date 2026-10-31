using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GiftFestivalUI
{
    public static readonly Color Cream = new(1f, 0.97f, 0.88f, 1f);
    public static readonly Color Ink = new(0.22f, 0.10f, 0.03f, 1f);

    public static string L(string key) => GameLocalization.GetOr("giftfestival_" + key, key);

    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Vector2 FitSize(Sprite sprite, Vector2 bounds)
    {
        if (sprite == null || sprite.rect.width <= 0 || sprite.rect.height <= 0) return bounds;
        return sprite.rect.size * Mathf.Min(bounds.x / sprite.rect.width, bounds.y / sprite.rect.height);
    }

    public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos)
    {
        var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true;
        image.raycastTarget = false; image.enabled = sprite != null;
        return image;
    }

    public static TMP_Text Label(string name, Transform parent, GiftFestivalConfig cfg, string text, float size, Vector2 box, Vector2 pos, Color? tint = null)
    {
        var label = Rect(name, parent, box, pos).gameObject.AddComponent<TextMeshProUGUI>();
        if (cfg.Art.font != null) label.font = cfg.Art.font;
        if (cfg.Art.fontMaterial != null) label.fontSharedMaterial = cfg.Art.fontMaterial;
        label.text = text; label.color = tint ?? Cream;
        label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * 0.65f;
        label.enableAutoSizing = true; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false; label.extraPadding = true;
        return label;
    }

    public static Button Button(Image image, UnityEngine.Events.UnityAction callback)
    {
        image.enabled = true; image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(callback);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }
}
