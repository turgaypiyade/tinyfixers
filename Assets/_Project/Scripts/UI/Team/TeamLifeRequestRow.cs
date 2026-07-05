using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Can isteği satırı: avatar + isim + "Can İsteği!" + kalp ilerleme (current/needed) + Yardım butonu.
/// Yardım'a basınca controller'a haber verir; karşılanınca satır kaldırılır.
/// </summary>
public sealed class TeamLifeRequestRow : MonoBehaviour
{
    [SerializeField] private Image panel;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tagText;        // "Can İsteği!"
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;   // "2/5"
    [SerializeField] private Button helpButton;
    [SerializeField] private TMP_Text helpButtonText;

    private TeamLifeRequest request;
    private Action<TeamLifeRequest> onHelp;

    public void Bind(TeamLifeRequest r, UITheme theme, Action<TeamLifeRequest> helpHandler)
    {
        request = r;
        onHelp = helpHandler;
        if (r == null) return;

        if (nameText != null) nameText.text = r.requesterName;
        if (tagText != null)  tagText.text  = "Can İsteği!";
        if (progressText != null) progressText.text = r.current + "/" + r.needed;
        if (progressFill != null) progressFill.fillAmount = r.Progress01;
        if (avatar != null)
        {
            avatar.sprite  = r.avatar;
            avatar.enabled = r.avatar != null;
        }
        if (helpButtonText != null) helpButtonText.text = "Yardım";

        if (helpButton != null)
        {
            helpButton.onClick.RemoveAllListeners();
            helpButton.onClick.AddListener(() => onHelp?.Invoke(request));
        }

        if (theme == null) return;
        UITheme.ApplySurface(panel, theme.cardBackground, theme.creamSurface);
        theme.ApplyText(nameText, Color.black, heading: true);   // isim siyah (bold/boyut prefab'tan)
        theme.ApplyText(tagText, theme.lifeRed, heading: true);
        theme.ApplyText(progressText, theme.textOnCream, heading: true);
        UITheme.ApplySurface(progressFill, theme.progressFill, theme.ctaGreen);
        theme.ApplyText(helpButtonText, theme.textLight, heading: true);
    }
}
