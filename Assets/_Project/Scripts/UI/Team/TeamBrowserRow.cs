using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım tarayıcı (Ara) satırı: amblem + isim + Kapasite çipi + "Takım Bilgisi" butonu.
/// Buton takım bilgi popup'ını açar (oradan Katıl).
/// </summary>
public sealed class TeamBrowserRow : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image emblem;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text capacityText;   // "41/50"
    [SerializeField] private Button infoButton;

    private Action onInfo;
    private bool wired;

    public void Bind(TeamDirectoryEntry entry, Sprite emblemSprite, Action onInfo)
    {
        this.onInfo = onInfo;
        if (!wired)
        {
            if (infoButton != null) infoButton.onClick.AddListener(() => this.onInfo?.Invoke());
            wired = true;
        }

        if (nameText != null) nameText.text = entry != null ? entry.name : "";
        if (capacityText != null) capacityText.text = entry != null ? $"{entry.members}/{entry.capacity}" : "";
        if (emblem != null)
        {
            emblem.sprite = emblemSprite;
            emblem.enabled = emblemSprite != null;
            emblem.preserveAspect = true;
        }
    }
}
