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
    [SerializeField] private TMP_Text amount;

    public void Set(Sprite iconSprite, int value)
    {
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (amount != null)
            amount.text = value.ToString();
    }
}
