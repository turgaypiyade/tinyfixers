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
        UiButtons.Apply(addButton, UiButtons.Kind.SquareGreen, styleLabel: false);
        UiButtons.Apply(dismissButton, UiButtons.Kind.SquareRed, styleLabel: false);
        UiIcons.SetIconOnly(addButton, UiIcons.AddFriend);
        UiIcons.SetIconOnly(dismissButton, UiIcons.Close);
        wired = true;
    }
}
