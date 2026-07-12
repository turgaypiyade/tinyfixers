using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Takım sohbeti satırı: avatar + baloncuk (gönderen + mesaj + zaman).
/// Gelen mesaj SOLDA (avatar solda), benim mesajım SAĞDA (avatarım sağda).
/// </summary>
public sealed class TeamChatRow : MonoBehaviour
{
    [Tooltip("Kök yatay dizilim — sol/sağ hizalama için reverse edilir.")]
    [SerializeField] private HorizontalLayoutGroup layout;
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

        // Sol (gelen) / sağ (benim): dizilimi ters çevir + hizala.
        if (layout != null)
        {
            layout.reverseArrangement = m.isMine;
            layout.childAlignment = m.isMine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        if (theme == null) return;
        // Baloncuk rengi: benimki farklı (mavi tonu), gelen krem.
        UITheme.ApplySurface(bubble, theme.cardBackground, m.isMine ? theme.infoBlue : theme.creamSurface);
        theme.ApplyText(senderText, m.isMine ? theme.textLight : theme.headerBand, heading: true);
        theme.ApplyText(messageText, m.isMine ? theme.textLight : theme.textOnCream);
        theme.ApplyText(timeText, m.isMine ? new Color(1f, 1f, 1f, 0.7f) : theme.textSub);
    }
}
