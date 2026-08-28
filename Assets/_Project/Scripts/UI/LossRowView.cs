using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fail popup'taki "vazgeçersen kaybedeceklerin" listesinin tek satırı: ikon + miktar.
/// LevelEndSimplePopupController her kayıp öğesi için bu prefab'ı instantiate edip Set çağırır.
/// </summary>
public sealed class LossRowView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("Kaybedilen şeyin ADI (ör. 'Safari ilerlemesi'). Oyuncu ne kaybettiğini bundan anlar.")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text amount;
    [Tooltip("Sağ-alt köşe rozeti: gerçekleşmişse checkmark, gerçekleşmemişse cancel ikonu.")]
    [SerializeField] private Image statusIcon;

    public void Set(Sprite iconSprite, int value) => Set(iconSprite, null, value, null);

    public void Set(Sprite iconSprite, int value, Sprite statusSprite) => Set(iconSprite, null, value, statusSprite);

    public void Set(Sprite iconSprite, string labelText, int value, Sprite statusSprite)
    {
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (label != null)
        {
            label.text = labelText ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(labelText));
        }
        if (amount != null)
        {
            // Sayı yalnız anlamlıysa gösterilir. 0/negatif → gizle (ör. progress-event tamamlanan
            // hedef satırı yalnız ikon gösterir; coin/safari sayıyı gösterir).
            bool showAmount = value > 0;
            amount.text = showAmount ? value.ToString() : string.Empty;
            amount.gameObject.SetActive(showAmount);
        }
        if (statusIcon != null)
        {
            statusIcon.sprite = statusSprite;
            statusIcon.enabled = statusSprite != null;
        }
    }
}
