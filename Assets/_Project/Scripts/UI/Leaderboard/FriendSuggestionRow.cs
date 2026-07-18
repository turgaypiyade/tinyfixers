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
        if (mutualText != null) mutualText.text = profile != null ? $"{profile.mutualCount} ortak arkadaş" : "";
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
        wired = true;
    }
}
