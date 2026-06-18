using System;
using UnityEngine;

/// <summary>
/// Oyuncu profili: seçili avatar id'si + isim. PlayerPrefs'te kalıcı.
/// Oyun başında default (avatar 0, "Oyuncu"). Avatar/isim seçimi 10. level sonrası açılır.
/// </summary>
public static class PlayerProfile
{
    private const string AvatarKey = "player_avatar_id";
    private const string NameKey   = "player_name";
    private const string LevelKey  = "current_level";

    public const int    DefaultAvatarId = 0;
    public const string DefaultName     = "Oyuncu";
    public const int    UnlockLevel     = 10;

    public static event Action OnChanged;

    public static int AvatarId
    {
        get => PlayerPrefs.GetInt(AvatarKey, DefaultAvatarId);
        set
        {
            PlayerPrefs.SetInt(AvatarKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }

    public static string PlayerName
    {
        get => PlayerPrefs.GetString(NameKey, DefaultName);
        set
        {
            string n = string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();
            PlayerPrefs.SetString(NameKey, n);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }

    /// <summary>Avatar/isim seçimi 10. level tamamlandıktan sonra açılır.</summary>
    public static bool CustomizationUnlocked => PlayerPrefs.GetInt(LevelKey, 1) >= UnlockLevel;

    public static void SetProfile(int avatarId, string name)
    {
        PlayerPrefs.SetInt(AvatarKey, Mathf.Max(0, avatarId));
        PlayerPrefs.SetString(NameKey, string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
