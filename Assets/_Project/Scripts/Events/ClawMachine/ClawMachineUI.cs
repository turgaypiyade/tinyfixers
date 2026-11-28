using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pençe Makinesi için küçük UI kurucu yardımcıları (yazı kuralı: TextSurfaceStyle).</summary>
public static class ClawMachineUI
{
    public static string L(string key, string fallback) => GameLocalization.GetOr("claw_" + key, fallback);

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

    public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, bool preserve = true)
    {
        var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = preserve;
        image.raycastTarget = false; image.enabled = sprite != null;
        return image;
    }

    /// Sprite'ı verilen genişliğe sığdıran boy (en-boy oranı korunur).
    public static Vector2 SizeForWidth(Sprite sprite, float width)
        => sprite == null || sprite.rect.width <= 0 ? new Vector2(width, width)
            : new Vector2(width, width * sprite.rect.height / sprite.rect.width);

    /// Ekran fontları (ClawMachineScreen.Build config'ten atar): başlık/buton/sayı ve gövde metni — Inter.
    public static TMP_FontAsset Font, BodyFont;

    /// Gövde metni (açıklama, ipucu): okunaklı font, aynı kontur kuralı.
    public static TMP_Text BodyLabel(string name, Transform parent, string text, float size, Vector2 box, Vector2 pos, Color surface)
    {
        var label = Label(name, parent, text, size, box, pos, surface);
        if (BodyFont != null) TextSurfaceStyle.Apply(label, surface, BodyFont);
        return label;
    }

    public static TMP_Text Label(string name, Transform parent, string text, float size, Vector2 box, Vector2 pos, Color surface)
    {
        var label = Rect(name, parent, box, pos).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * 0.6f;
        label.enableAutoSizing = true; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        TextSurfaceStyle.Apply(label, surface, Font);
        return label;
    }

    public static Button TextButton(string name, Transform parent, string text, Vector2 size, Vector2 pos, UiButtons.Kind kind, System.Action onClick)
    {
        var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var surface = kind == UiButtons.Kind.Green ? TextSurfaceStyle.Base(TextSurface.Green) : TextSurfaceStyle.Base(TextSurface.Blue);
        var label = Label("Label", image.transform, text, size.y * 0.42f, size * new Vector2(0.84f, 0.8f), new Vector2(0, 4), surface);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UiButtons.Apply(button, kind);
        if (Font != null) TextSurfaceStyle.Apply(label, surface, Font);   // UiButtons Pop'a çevirir; ekran fontu Inter
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }
}
