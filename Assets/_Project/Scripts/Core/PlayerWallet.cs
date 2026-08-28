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
        CurrencyLedger.RecordCoins(+amount);   // anti-hack: meşru kazanç delta'ya
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
        CurrencyLedger.RecordCoins(-amount);   // harcama da delta'ya (net değişim)
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

        CurrencyLedger.RecordStars(+gained);
        OnTotalStarsChanged?.Invoke(newTotal);
    }

    public static int GetLevelStars(int level)
        => PlayerPrefs.GetInt(KeyLevelStars + level, 0);

    /// <summary>
    /// Toplam yıldıza ekler (başlangıç bonusu, etkinlik ödülü gibi durumlar için).
    /// OnTotalStarsChanged event'i tetiklenir → wallet UI güncellenir.
    /// </summary>
    public static void AddStars(int amount)
    {
        if (amount <= 0) return;
        int newTotal = TotalStars + amount;
        PlayerPrefs.SetInt(KeyTotalStars, newTotal);
        PlayerPrefs.Save();
        CurrencyLedger.RecordStars(+amount);
        OnTotalStarsChanged?.Invoke(newTotal);
    }

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
        CurrencyLedger.RecordStars(-amount);
        OnTotalStarsChanged?.Invoke(newVal);
        return true;
    }

    public static bool HasEnoughStars(int amount) => TotalStars >= amount;

    // -----------------------------------------------------------------------
    // Score (puan) — leaderboard/team sıralama metriği.
    // Yıldız kalıbının aynısı: level başına EN İYİ skor saklanır, sadece
    // iyileştirme farkı toplam puana eklenir → level tekrar oynamak grind istismarı
    // yaratmaz (yalnız kendi rekorunu geçince artar).

    private const string KeyTotalScore = "player_total_score";
    private const string KeyLevelScore = "level_score_";   // + level numarası

    public static event Action<int> OnTotalScoreChanged;

    public static int TotalScore => PlayerPrefs.GetInt(KeyTotalScore, 0);

    public static int GetLevelScore(int level)
        => PlayerPrefs.GetInt(KeyLevelScore + level, 0);

    /// <summary>
    /// Seviye bazında en iyi skoru kaydeder; önceki en iyiden yüksekse farkı
    /// toplam puana ekler. (SetLevelStars ile birebir aynı mantık.)
    /// </summary>
    public static void SetLevelScore(int level, int score)
    {
        if (score < 0) score = 0;
        string key = KeyLevelScore + level;

        int prev = PlayerPrefs.GetInt(key, 0);
        if (score <= prev) return;   // rekoru geçmediyse kaydetme

        int gained = score - prev;
        PlayerPrefs.SetInt(key, score);

        int newTotal = TotalScore + gained;
        PlayerPrefs.SetInt(KeyTotalScore, newTotal);
        PlayerPrefs.Save();

        OnTotalScoreChanged?.Invoke(newTotal);
    }
}
