using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ortak buton ikonları (Resources/UIIcons). Fontlarda olmayan ✕ ▶ gibi karakterler "□" çıkardığı
/// için ikon gereken butonlar metin yerine bunları kullanır.
/// </summary>
public static class UiIcons
{
    public static Sprite Close     => Load("Icon_Close");
    public static Sprite AddFriend => Load("Icon_AddFriend");
    public static Sprite Search    => Load("Icon_Search");
    public static Sprite Chat      => Load("Icon_Chat");
    public static Sprite Info      => Load("Icon_Info");
    public static Sprite Invite    => Load("Icon_Invite");
    public static Sprite Heart     => Load("Icon_Heart");

    /// <summary>
    /// Butonun SOLUNA ikon koyar, yazıyı ikondan sonrasına kaydırır (ikon + yazı butonlar: "Can İste", "Mesaj").
    /// size = ikon kenarı (px). Tekrar çağrılması güvenli.
    /// </summary>
    public static void SetLeadingIcon(Button button, Sprite icon, float size = 84f, float leftPad = 28f)
    {
        if (button == null || icon == null) return;

        var existing = button.transform.Find("LeadIcon");
        Image img;
        if (existing != null) img = existing.GetComponent<Image>();
        else
        {
            var go = new GameObject("LeadIcon", typeof(RectTransform));
            go.layer = button.gameObject.layer;
            go.transform.SetParent(button.transform, false);
            img = go.AddComponent<Image>();
        }
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(leftPad, 0f);
        rt.sizeDelta = new Vector2(size, size);

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(leftPad + size + 8f, 6f);
        lrt.offsetMax = new Vector2(-18f, -6f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMax = 40f;
        label.fontSizeMin = 22f;
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>("UIIcons/" + name);

    /// <summary>
    /// Butonun yazısını gizleyip ortasına ikon koyar (yalnız-ikon butonlar: X, +).
    /// fill = ikonun butonun kısa kenarına oranı. Tekrar çağrılması güvenli.
    /// </summary>
    public static void SetIconOnly(Button button, Sprite icon, float fill = 0.62f)
    {
        if (button == null || icon == null) return;

        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            label.gameObject.SetActive(false);

        var existing = button.transform.Find("Icon");
        Image img;
        if (existing != null) img = existing.GetComponent<Image>();
        else
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.layer = button.gameObject.layer;
            go.transform.SetParent(button.transform, false);
            img = go.AddComponent<Image>();
        }

        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        float inset = (1f - fill) * 0.5f;
        rt.anchorMin = new Vector2(inset, inset);
        rt.anchorMax = new Vector2(1f - inset, 1f - inset);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
