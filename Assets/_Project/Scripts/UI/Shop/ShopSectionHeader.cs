using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mağaza bölüm başlığı bandı ("Özel Teklifler" / "Mega Fırsatlar"). Renk bölüm stiline göre.
/// ShopScreenController içerik akışına basar; ardından o bölümün kartları gelir.
/// </summary>
public sealed class ShopSectionHeader : MonoBehaviour
{
    [SerializeField] private Image band;
    [SerializeField] private TMP_Text title;

    public void Setup(ShopSection section, UITheme theme)
    {
        if (section == null) return;

        if (title != null)
        {
            title.text = section.title;
            if (theme != null) theme.ApplyText(title, theme.textLight, heading: true);
        }

        if (theme != null)
        {
            Color c = section.bandStyle == ShopSection.BandStyle.Special
                ? theme.specialBand
                : theme.headerBand;
            UITheme.ApplySurface(band, theme.sectionHeaderBackground, c);
        }
    }
}
