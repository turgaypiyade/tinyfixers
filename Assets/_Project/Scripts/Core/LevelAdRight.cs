using UnityEngine;

/// <summary>
/// Fail'de reklamla bedava devam hakkı LEVEL başına TEK (kullanıcı kuralı). Can reklamı 2026-11-08'den beri
/// AYRI ve günlük sınırlı (RewardedAdCaps.Life); bu sınıfa yalnız RewardedAdCaps.Continue erişir. Çıkıp girince, ana menüye dönünce
/// de geri gelmez; level geçilince (CurrentLevel değişir) yeni level'da hak yeniden açılır.
/// Kullanılan level numarası kalıcı tutulur.
/// </summary>
public static class LevelAdRight
{
    // Eski adıyla korunuyor (mevcut kayıtlar geçerli kalsın).
    public const string UsedLevelKey = "ad_continue_used_level";

    public static bool IsUsed => PlayerPrefs.GetInt(UsedLevelKey, 0) == CurrentLevel.Played;

    /// <summary>Hakkı harcar. Zaten harcanmışsa false (reklam gösterilmemeli).</summary>
    public static bool TryConsume()
    {
        if (IsUsed) return false;
        PlayerPrefs.SetInt(UsedLevelKey, CurrentLevel.Played);
        PlayerPrefs.Save();
        return true;
    }
}
