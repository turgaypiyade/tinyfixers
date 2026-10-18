using UnityEngine;

/// <summary>
/// Avatar kataloğu: id → sprite. 5-6 avatar sprite'ını buraya dizersin.
/// PlayerProfile.AvatarId bu listeye index'ler. Tek asset, her yerde paylaşılır.
/// </summary>
[CreateAssetMenu(fileName = "AvatarLibrary", menuName = "TinyFixers/Avatar Library")]
public sealed class AvatarLibrary : ScriptableObject
{
    [SerializeField] private Sprite[] avatars;
    [Tooltip("Avatar ile aynı indekste hayvan kimliği (ram, bear, pig, rabbit, monkey). Kostüm çeşitleri aynı kimliği kullanır.")]
    [SerializeField] private string[] characterIds;

    public int Count => avatars != null ? avatars.Length : 0;

    public Sprite Get(int id)
    {
        if (avatars == null || avatars.Length == 0) return null;
        return avatars[Mathf.Clamp(id, 0, avatars.Length - 1)];
    }

    public string GetCharacterId(int id)
    {
        if (Count == 0 || characterIds == null) return string.Empty;
        int index = Mathf.Clamp(id, 0, Count - 1); // Sprite seçimi ile aynı fallback.
        return index < characterIds.Length ? characterIds[index] ?? string.Empty : string.Empty;
    }
}
