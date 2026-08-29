using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Basit altın satırı: tek coin görseli + miktar + fiyat butonu ("Altınlar" bölümü).
/// Görsel/miktar ilk gruptan (<see cref="ShopOffer.groups"/>[0]) okunur: ilk ikon coin görseli,
/// labelValue miktar. İsim/kutu/kurdele yok — sadeleştirilmiş kart.
/// </summary>
public sealed class ShopCoinRowCard : ShopOfferCardBase
{
    [Header("Coin satırı")]
    [SerializeField] private Image coinIcon;
    [SerializeField] private TMP_Text amountText;

    protected override void BuildBody()
    {
        ShopRewardGroup first = (offer.groups != null && offer.groups.Count > 0) ? offer.groups[0] : null;

        if (coinIcon != null)
        {
            Sprite sprite = (first != null && first.icons != null && first.icons.Count > 0) ? first.icons[0] : null;
            coinIcon.sprite = sprite;
            coinIcon.enabled = sprite != null;
            coinIcon.preserveAspect = true;
        }

        if (amountText != null)
        {
            amountText.text = first != null ? first.labelValue.ToString("N0") : "0";
            if (theme != null) theme.ApplyText(amountText, theme.textOnCream, heading: true);
        }
    }
}
