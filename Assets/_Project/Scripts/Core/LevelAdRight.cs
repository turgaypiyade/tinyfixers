using UnityEngine;

/// <summary>
/// Seviye başına iki reklamlı devam hakkı. İlk ödül beş hamle, ikincisi üç hamle ve bir güçlendirici.
/// </summary>
public static class LevelAdRight
{
    // Eski adıyla korunuyor (mevcut kayıtlar geçerli kalsın): hangi level'da kullanıldı.
    public const string UsedLevelKey = "ad_continue_used_level";
    // O level'da kaç reklam izlendi. Eski kayıtta yoksa (yalnız level anahtarı) 1 sayılır.
    public const string UsedCountKey = "ad_continue_used_count";
    public const int MaxPerLevel = 2;
    private static readonly int[] MovesPerAd = { 5, 3 };

    public static int UsedCount => PlayerPrefs.GetInt(UsedLevelKey, 0) == CurrentLevel.Played
        ? Mathf.Clamp(PlayerPrefs.GetInt(UsedCountKey, 1), 0, MaxPerLevel) : 0;

    public static bool IsUsed => UsedCount >= MaxPerLevel;

    /// Sıradaki reklamın verdiği hamle (+5, sonra +3).
    public static int NextAdMoves => MovesPerAd[Mathf.Clamp(UsedCount, 0, MovesPerAd.Length - 1)];

    /// <summary>Bir hak harcar. Haklar bitmişse false (reklam gösterilmemeli).</summary>
    public static bool TryConsume()
    {
        int used = UsedCount;
        if (used >= MaxPerLevel) return false;
        PlayerPrefs.SetInt(UsedLevelKey, CurrentLevel.Played);
        PlayerPrefs.SetInt(UsedCountKey, used + 1);
        PlayerPrefs.Save();
        return true;
    }
}
