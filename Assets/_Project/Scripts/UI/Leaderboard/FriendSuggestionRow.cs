using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Önerilen Arkadaşlar" satırı (referans RM): avatar + isim + "N ortak arkadaş"
/// + sağda X (reddet) ve kişi-ekle butonları. Liderlik panosunun Arkadaşlar/Ekle
/// alt-görünümünde listelenir.
/// </summary>
public sealed class FriendSuggestionRow : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text mutualText;   // "1 ortak arkadaş"
    [SerializeField] private Button addButton;
    [SerializeField] private Button dismissButton;

    private Action onAdd;
    private Action onDismiss;
    private bool wired;

    public void Bind(FriendProfile profile, Sprite avatarSprite, UITheme theme, Action onAdd, Action onDismiss)
    {
        this.onAdd = onAdd;
        this.onDismiss = onDismiss;
        Wire();

        if (nameText != null) nameText.text = profile != null ? profile.name : "";
        SingleLineText.Fit(nameText);
        if (mutualText != null) mutualText.text = profile != null ? GameLocalization.GetFormat("friend_mutual_count", profile.mutualCount) : "";
        if (avatar != null)
        {
            avatar.sprite = avatarSprite;
            avatar.enabled = avatarSprite != null;
            avatar.preserveAspect = true;
        }
    }

    private void Wire()
    {
        if (wired) return;
        if (addButton != null) addButton.onClick.AddListener(() => onAdd?.Invoke());
        if (dismissButton != null) dismissButton.onClick.AddListener(() => onDismiss?.Invoke());
        // RM gibi yazılı butonlar (kullanıcı 2026-11-25): oyunun GreenButonwoStroke "Ekle" + kırmızı eşi "Yok Say".
        LayoutTextButton(addButton, UiButtons.Kind.GreenPlain, "friend_add", "Ekle", 172f, -20f);
        LayoutTextButton(dismissButton, UiButtons.Kind.RedPlain, "friend_dismiss", "Yok Say", 196f, -20f - 172f - 14f);
        // Yazı butonları geniş: isim/ortak satırı butonlara değmesin.
        foreach (var t in new[] { nameText, mutualText })
            if (t != null) t.rectTransform.sizeDelta = new Vector2(310f, t.rectTransform.sizeDelta.y);
        wired = true;
    }

    private static void LayoutTextButton(Button button, UiButtons.Kind kind, string key, string fallback, float width, float x)
    {
        if (button == null) return;
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(width, 88f);
        rt.anchoredPosition = new Vector2(x, 0f);
        var icon = button.transform.Find("Icon");
        if (icon != null) icon.gameObject.SetActive(false);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = GameLocalization.GetOr(key, fallback);
            label.enableAutoSizing = true;
            label.fontSizeMax = 40f;
            label.fontSizeMin = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Center;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(18f, 6f); lrt.offsetMax = new Vector2(-18f, -10f);
        }
        UiButtons.Apply(button, kind);   // görsel + yazı kuralı (beyaz yüz, buton renginin koyusu kontur)
    }
}
