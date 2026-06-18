using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Profil edit ekranındaki avatar seçenek butonu (grid'de bir hücre).
/// ProfilePageController her avatar için bu prefab'ı instantiate edip Setup çağırır.
/// </summary>
public sealed class AvatarOptionButton : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("Seçili olduğunda gösterilecek çerçeve/işaret.")]
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private Button button;

    private int avatarId;

    public void Setup(int id, Sprite sprite, Action<int> onSelect)
    {
        avatarId = id;

        if (icon != null && sprite != null)
        {
            icon.sprite = sprite;
            icon.enabled = true;
        }

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(avatarId));
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);
    }
}
