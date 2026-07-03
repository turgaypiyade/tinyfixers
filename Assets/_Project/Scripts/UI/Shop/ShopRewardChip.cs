using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teklif kartı içindeki tek ödül göstergesi: ikon + miktar/rozet etiketi ("x5" / "∞" / "1s").
/// ShopOfferCard tarafından chipContainer'a basılır.
/// </summary>
public sealed class ShopRewardChip : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    public void Setup(ShopReward reward, UITheme theme)
    {
        if (reward == null) return;

        if (icon != null)
        {
            icon.sprite  = reward.icon;
            icon.enabled = reward.icon != null;
        }

        if (label != null)
        {
            label.text = reward.ChipLabel();
            if (theme != null) theme.ApplyText(label, theme.textLight, heading: true);
        }
    }
}
