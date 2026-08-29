using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bundle kartı (MegaAwards1 çerçevesi): SOL tarafta doğrudan altın ikonu + miktar (kutu YOK),
/// sağında ELLE yerleştirilmiş ödül kutuları (MATGrup1/3/5) + mor bantta isim + BuyButton.
///
/// Veri eşlemesi: <see cref="ShopOffer.groups"/>[0] = hero (altın, doğrudan ikon+miktar),
/// groups[1..] = kutular (soldan sağa <see cref="boxes"/> slotlarına). Fazla kutu gizlenir.
/// </summary>
public sealed class ShopOfferCard : ShopOfferCardBase
{
    [Header("Hero (altın, sol — kutu değil)")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TMP_Text heroAmountText;

    [Header("Ödül kutuları (altının sağı)")]
    [Tooltip("Prefab'ta elle yerleştirilmiş kutu slotları; groups[1..] soldan sağa doldurulur.")]
    [SerializeField] private ShopRewardGroupBox[] boxes;

    [Header("Diğer")]
    [SerializeField] private GameObject bestBadge;   // "En İyi Fırsat" kurdelesi
    [SerializeField] private TMP_Text nameText;

    protected override void BuildBody()
    {
        if (bestBadge != null) bestBadge.SetActive(offer.showBestBadge);

        var groups = offer.groups;
        int count = groups?.Count ?? 0;

        // groups[0] = hero (altın): doğrudan sol ikon + miktar, MATGrup kutusu yok.
        ShopRewardGroup hero = count > 0 ? groups[0] : null;
        if (heroIcon != null)
        {
            Sprite s = (hero?.icons != null && hero.icons.Count > 0) ? hero.icons[0] : null;
            heroIcon.sprite = s;
            heroIcon.enabled = s != null;
            heroIcon.preserveAspect = true;
        }
        if (heroAmountText != null)
        {
            heroAmountText.text = hero != null ? hero.GroupLabel() : "";
            if (theme != null) theme.ApplyText(heroAmountText, theme.textOnCream, heading: true);
        }

        // groups[1..] = kutular.
        if (boxes != null)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null) continue;

                int g = i + 1;   // hero'yu atla
                if (g < count)
                {
                    box.gameObject.SetActive(true);
                    box.Setup(groups[g], theme);
                }
                else
                {
                    box.gameObject.SetActive(false);
                }
            }
        }

        if (nameText != null)
        {
            nameText.text = offer.displayName;
            if (theme != null) theme.ApplyText(nameText, theme.textLight, heading: true);
        }
    }
}
