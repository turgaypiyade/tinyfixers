using UnityEngine;

/// <summary>
/// Avatar kataloğu: id → sprite. 5-6 avatar sprite'ını buraya dizersin.
/// PlayerProfile.AvatarId bu listeye index'ler. Tek asset, her yerde paylaşılır.
/// </summary>
[CreateAssetMenu(fileName = "AvatarLibrary", menuName = "TinyFixers/Avatar Library")]
public sealed class AvatarLibrary : ScriptableObject
{
    [SerializeField] private Sprite[] avatars;

    public int Count => avatars != null ? avatars.Length : 0;

    public Sprite Get(int id)
    {
        if (avatars == null || avatars.Length == 0) return null;
        return avatars[Mathf.Clamp(id, 0, avatars.Length - 1)];
    }
}
