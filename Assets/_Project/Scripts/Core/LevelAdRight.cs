using UnityEngine;

/// <summary>
/// Fail'de reklamla bedava devam hakkı: LEVEL başına 2 (kullanıcı 2026-11-13; önceden 1). 1. reklam +2, 2. reklam +1
/// hamle — altın teklifleri (+5/+10/+15) ayrı kalır; reklam küçük nefes, altın gerçek kurtarış (reklam +5 verince
/// altın neredeyse hiç gerekmiyordu). Can reklamı ayrı ve günlük sınırlı (RewardedAdCaps.Life); bu sınıfa yalnız
/// RewardedAdCaps.Continue erişir. Çıkıp girince, ana menüye dönünce de geri gelmez; level geçilince (CurrentLevel
/// değişir) yeni level'da haklar yeniden açılır. Kullanılan level numarası + sayı kalıcı tutulur.
/// </summary>
public static class LevelAdRight
{
    // Eski adıyla korunuyor (mevcut kayıtlar geçerli kalsın): hangi level'da kullanıldı.
    public const string UsedLevelKey = "ad_continue_used_level";
    // O level'da kaç reklam izlendi. Eski kayıtta yoksa (yalnız level anahtarı) 1 sayılır.
    public const string UsedCountKey = "ad_continue_used_count";
    public const int MaxPerLevel = 2;
    private static readonly int[] MovesPerAd = { 2, 1 };

    public static int UsedCount => PlayerPrefs.GetInt(UsedLevelKey, 0) == CurrentLevel.Played
        ? Mathf.Clamp(PlayerPrefs.GetInt(UsedCountKey, 1), 0, MaxPerLevel) : 0;

    public static bool IsUsed => UsedCount >= MaxPerLevel;

    /// Sıradaki reklamın verdiği hamle (+2, sonra +1).
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
