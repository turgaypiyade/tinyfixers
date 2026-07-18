using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun arkadaş listesi — kalıcı (PlayerPrefs), tek kaynak. Liderlik panosunun
/// "Arkadaşlar" sekmesi ve öneri/arama akışı buradan beslenir.
/// v1: arkadaşlar İSİMLE tutulur (bot evreni); gerçek backend gelince id'ye taşınır,
/// bu sınıfın API'si değişmez.
/// </summary>
public static class FriendState
{
    private const string KeyFriends   = "friends_list";
    private const string KeyDismissed = "friends_dismissed";
    private const string KeyMyCode    = "friend_code";
    private const char Sep = '\n';

    /// <summary>Liste değişince (ekle/çıkar) tetiklenir → ekranlar yeniden basar.</summary>
    public static event Action OnChanged;

    private static List<string> friends;
    private static HashSet<string> dismissed;

    public static IReadOnlyList<string> Friends { get { EnsureLoaded(); return friends; } }
    public static bool HasFriends { get { EnsureLoaded(); return friends.Count > 0; } }

    public static bool IsFriend(string name)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(name) && friends.Contains(name.Trim());
    }

    public static bool IsDismissed(string name)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(name) && dismissed.Contains(name.Trim());
    }

    public static void AddFriend(string name)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        if (friends.Contains(name)) return;

        friends.Add(name);
        dismissed.Remove(name);   // öneriyi reddetmişti ama sonradan ekledi → temizle
        Save();
        OnChanged?.Invoke();
    }

    public static void RemoveFriend(string name)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!friends.Remove(name.Trim())) return;
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>Öneriyi kalıcı olarak reddet (X) — bir daha önerilmez.</summary>
    public static void DismissSuggestion(string name)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!dismissed.Add(name.Trim())) return;
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Oyuncunun paylaşılabilir arkadaş kodu (örn "YX7115676"). İlk erişimde üretilir,
    /// kalıcıdır. Gerçek backend gelince sunucudan gelen kodla değiştirilebilir.
    /// </summary>
    public static string MyCode
    {
        get
        {
            var code = PlayerPrefs.GetString(KeyMyCode, "");
            if (!string.IsNullOrEmpty(code)) return code;

            code = GenerateCode();
            PlayerPrefs.SetString(KeyMyCode, code);
            PlayerPrefs.Save();
            return code;
        }
    }

    private static string GenerateCode()
    {
        const string letters = "ABCDEFGHJKLMNPRSTUVYXZ";
        var rng = new System.Random(Environment.TickCount ^ SystemInfo.deviceUniqueIdentifier.GetHashCode());
        return $"{letters[rng.Next(letters.Length)]}{letters[rng.Next(letters.Length)]}{rng.Next(1000000, 9999999)}";
    }

    private static void EnsureLoaded()
    {
        if (friends != null) return;
        friends = new List<string>(SplitNonEmpty(PlayerPrefs.GetString(KeyFriends, "")));
        dismissed = new HashSet<string>(SplitNonEmpty(PlayerPrefs.GetString(KeyDismissed, "")));
    }

    private static void Save()
    {
        PlayerPrefs.SetString(KeyFriends, string.Join(Sep.ToString(), friends));
        PlayerPrefs.SetString(KeyDismissed, string.Join(Sep.ToString(), dismissed));
        PlayerPrefs.Save();
    }

    private static IEnumerable<string> SplitNonEmpty(string joined)
    {
        if (string.IsNullOrEmpty(joined)) yield break;
        foreach (var part in joined.Split(Sep))
        {
            var t = part.Trim();
            if (t.Length > 0) yield return t;
        }
    }
}
