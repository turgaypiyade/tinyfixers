using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Takım sohbeti satırı: avatar + gönderen + mesaj + zaman.</summary>
public sealed class TeamChatRow : MonoBehaviour
{
    [SerializeField] private Image bubble;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text senderText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;

    public void Bind(TeamChatMessage m, UITheme theme)
    {
        if (m == null) return;

        if (senderText != null)  senderText.text  = m.senderName;
        if (messageText != null) messageText.text = m.text;
        if (timeText != null)    timeText.text    = m.timeLabel;
        if (avatar != null)
        {
            avatar.sprite  = m.avatar;
            avatar.enabled = m.avatar != null;
        }

        if (theme == null) return;
        UITheme.ApplySurface(bubble, theme.cardBackground, theme.creamSurface);
        theme.ApplyText(senderText, theme.headerBand, heading: true);
        theme.ApplyText(messageText, theme.textOnCream);
        theme.ApplyText(timeText, theme.textSub);
    }
}
