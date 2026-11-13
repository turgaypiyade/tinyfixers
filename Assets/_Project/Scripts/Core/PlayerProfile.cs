using System;
using UnityEngine;

/// <summary>
/// Oyuncu profili: seçili avatar id'si + isim. PlayerPrefs'te kalıcı.
/// Oyun başında default avatar 0 + otomatik isim "Oyuncu 48213" / "Player 48213" (kullanıcı 2026-11-13: herkes
/// "Oyuncu" görünmesin). Numara rastgele üretilir; cihaz/reklam kimliğinden TÜRETİLMEZ (KVKK/Play politikası).
/// Kendi ismini yazmayan oyuncunun ismi kaydedilmez → önek dil değişince çevrilir. Avatar/isim seçimi level 9'da açılır.
/// </summary>
public static class PlayerProfile
{
    private const string AvatarKey = "player_avatar_id";
    private const string NameKey   = "player_name";
    private const string NameCodeKey = "player_name_code";
    private const string LevelKey  = "current_level";

    public const int    DefaultAvatarId = 0;
    private const string LegacyDefaultName = "Oyuncu";   // eski sürümlerde herkese kaydedilen sabit isim

    /// Otomatik isim: dile göre önek + oyuncuya özel 5 haneli numara (10 bin+ oyuncuda isim çakışması nadir;
    /// kimlik zaten Firebase uid, isim benzersiz olmak zorunda değil).
    public static string DefaultName =>
        GameLocalization.GetOr("profile_default_name", LegacyDefaultName) + " " + NameCode;   // 12 karakter = isim sınırı

    private static int NameCode
    {
        get
        {
            int code = PlayerPrefs.GetInt(NameCodeKey, 0);
            if (code < 10000)
            {
                code = UnityEngine.Random.Range(10000, 100000);
                PlayerPrefs.SetInt(NameCodeKey, code);
                PlayerPrefs.Save();
            }
            return code;
        }
    }

    /// Oyuncu kendi ismini henüz yazmadı mı (otomatik isim gösteriliyor)?
    public static bool HasDefaultName => IsUnsetName(PlayerPrefs.GetString(NameKey, string.Empty));

    private static bool IsUnsetName(string stored) =>
        string.IsNullOrWhiteSpace(stored) || stored.Trim() == LegacyDefaultName || stored.Trim() == DefaultName;
    public const int    UnlockLevel     = 9;    // level 8 bitince (kullanıcı 2026-11-13: 12'de joker+görev+profil üst üste biniyordu; görevler 10 bitince, çekiç 12)

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
        get
        {
            string stored = PlayerPrefs.GetString(NameKey, string.Empty);
            return IsUnsetName(stored) ? DefaultName : stored.Trim();
        }
        set
        {
            StoreName(value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }

    /// <summary>Avatar/isim seçimi 10. level tamamlandıktan sonra açılır.</summary>
    public static bool CustomizationUnlocked => PlayerPrefs.GetInt(LevelKey, 1) >= UnlockLevel;

    public static void SetProfile(int avatarId, string name)
    {
        PlayerPrefs.SetInt(AvatarKey, Mathf.Max(0, avatarId));
        StoreName(name);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // Otomatik isim kaydedilmez (önek dile göre çevrilsin); yalnız oyuncunun yazdığı isim saklanır.
    private static void StoreName(string name)
    {
        if (IsUnsetName(name)) PlayerPrefs.DeleteKey(NameKey);
        else PlayerPrefs.SetString(NameKey, name.Trim());
    }
}
