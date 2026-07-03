using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liderlik panosu satırı: sıra no + avatar + isim/alt başlık + puan.
/// Top-3 madalya rengi, kendi satırın yeşil vurgu alır.
/// </summary>
public sealed class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private Image rowBackground;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image avatar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text scoreText;

    public void Bind(LeaderboardEntry e, UITheme theme)
    {
        if (e == null) return;

        if (rankText != null) rankText.text = e.rank.ToString();
        if (nameText != null) nameText.text = e.playerName;
        if (subtitleText != null) subtitleText.text = e.subtitle;
        if (scoreText != null) scoreText.text = e.score.ToString();

        if (avatar != null)
        {
            avatar.sprite  = e.avatar;
            avatar.enabled = e.avatar != null;
        }

        if (theme == null || rowBackground == null) return;

        // Renk önceliği: kendi satır > top-3 madalya > normal.
        Color bg;
        if (e.isSelf)            bg = theme.ctaGreen;
        else if (e.rank == 1)    bg = theme.goldTrim;
        else if (e.rank == 2)    bg = new Color(0.75f, 0.78f, 0.85f);  // gümüş
        else if (e.rank == 3)    bg = new Color(0.80f, 0.52f, 0.30f);  // bronz
        else                     bg = theme.panelSurface;
        UITheme.ApplySurface(rowBackground, theme.cardBackground, bg);

        Color txt = e.isSelf ? theme.textLight : theme.textLight;
        theme.ApplyText(nameText, txt, heading: true);
        theme.ApplyText(rankText, txt, heading: true);
        theme.ApplyText(scoreText, theme.accentAmber, heading: true);
        theme.ApplyText(subtitleText, theme.textSub);
    }
}
