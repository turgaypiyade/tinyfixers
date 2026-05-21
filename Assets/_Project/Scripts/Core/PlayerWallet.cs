using System;
using UnityEngine;

/// <summary>
/// Merkezi para/yıldız yönetimi. Tüm sistemler buradan okur/yazar.
/// PlayerPrefs tabanlı, uygulama genelinde static erişim.
/// </summary>
public static class PlayerWallet
{
    private const string KeyCoins      = "player_coins";
    private const string KeyTotalStars = "player_total_stars";
    private const string KeyLevelStars = "level_stars_";   // + level numarası

    // -----------------------------------------------------------------------
    // Events — UI'lar abone olup anlık güncellenebilir

    public static event Action<int> OnCoinsChanged;
    public static event Action<int> OnTotalStarsChanged;

    // -----------------------------------------------------------------------
    // Coins

    public static int Coins => PlayerPrefs.GetInt(KeyCoins, 0);

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        int newVal = Coins + amount;
        PlayerPrefs.SetInt(KeyCoins, newVal);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newVal);
    }

    /// <summary>
    /// Harcama. Yeterli coin yoksa false döner, para düşülmez.
    /// </summary>
    public static bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;
        int current = Coins;
        if (current < amount) return false;

        int newVal = current - amount;
        PlayerPrefs.SetInt(KeyCoins, newVal);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newVal);
        return true;
    }

    public static bool HasEnoughCoins(int amount) => Coins >= amount;

    // -----------------------------------------------------------------------
    // Stars

    public static int TotalStars => PlayerPrefs.GetInt(KeyTotalStars, 0);

    /// <summary>
    /// Seviye bazında yıldız kaydeder.
    /// Sadece önceki değerden yüksekse günceller ve farkı toplam yıldıza ekler.
    /// </summary>
    public static void SetLevelStars(int level, int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        string key = KeyLevelStars + level;

        int prev = PlayerPrefs.GetInt(key, 0);
        if (stars <= prev) return;   // daha iyi değilse kaydetme

        int gained = stars - prev;
        PlayerPrefs.SetInt(key, stars);

        int newTotal = TotalStars + gained;
        PlayerPrefs.SetInt(KeyTotalStars, newTotal);
        PlayerPrefs.Save();

        OnTotalStarsChanged?.Invoke(newTotal);
    }

    public static int GetLevelStars(int level)
        => PlayerPrefs.GetInt(KeyLevelStars + level, 0);

    /// <summary>
    /// Toplam yıldızdan harcar (workshop/atölye tamiri için).
    /// Yeterli yıldız yoksa false döner, düşülmez.
    /// </summary>
    public static bool SpendStars(int amount)
    {
        if (amount <= 0) return true;
        int current = TotalStars;
        if (current < amount) return false;

        int newVal = current - amount;
        PlayerPrefs.SetInt(KeyTotalStars, newVal);
        PlayerPrefs.Save();
        OnTotalStarsChanged?.Invoke(newVal);
        return true;
    }

    public static bool HasEnoughStars(int amount) => TotalStars >= amount;
}
