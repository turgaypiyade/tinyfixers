using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TopHUD / Home dairesindeki avatar görselini PlayerProfile'a göre gösterir.
/// Daireye bir Image koy, bu component'i ekle, avatarImage + library ata.
/// Profil değişince (avatar seçilince) otomatik güncellenir. İsim göstermek istersen
/// opsiyonel nameText alanına bağlarsın.
/// </summary>
public sealed class AvatarView : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private AvatarLibrary library;
    [Tooltip("Opsiyonel: oyuncu adını gösteren TMP/Text. Boşsa kullanılmaz.")]
    [SerializeField] private TMPro.TMP_Text nameText;

    private void OnEnable()
    {
        PlayerProfile.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        if (avatarImage != null && library != null)
        {
            var sprite = library.Get(PlayerProfile.AvatarId);
            if (sprite != null)
            {
                avatarImage.sprite = sprite;
                avatarImage.enabled = true;
            }
        }

        if (nameText != null)
            nameText.text = PlayerProfile.PlayerName;
    }
}
