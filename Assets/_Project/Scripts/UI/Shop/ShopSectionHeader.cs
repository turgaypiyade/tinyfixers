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
            title.text = section.LocalizedTitle;
            if (theme != null) theme.ApplyText(title, theme.textLight, heading: true);
        }

        if (section.bandSprite != null)
        {
            // Hazır görselli band (ör. altın bant): renk boyaması yok, köşeler 9-slice.
            UITheme.ApplySurface(band, section.bandSprite, Color.white);
            if (band != null) band.pixelsPerUnitMultiplier = 1f;   // görseller 900x92 birebir çizildi
            SetHeight(GraphicBandHeight);
            if (title != null)
            {
                title.fontSize = 44f;
                title.outlineColor = new Color32(92, 40, 12, 255);
                title.outlineWidth = 0.22f;
            }
        }
        else if (theme != null)
        {
            Color c = section.bandStyle == ShopSection.BandStyle.Special
                ? theme.specialBand
                : theme.headerBand;
            UITheme.ApplySurface(band, theme.sectionHeaderBackground, c);
        }
    }

    // Başlık görselleri (MarketUI/PkgTitle, CoinsTitle) 900x92 çizildi; 64px'lik varsayılan başlıkta ezilir.
    private const float GraphicBandHeight = 92f;

    private void SetHeight(float height)
    {
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        if (TryGetComponent(out LayoutElement layout))
        {
            layout.preferredHeight = height;
            layout.minHeight = height;
        }
    }
}
