using UnityEngine;

/// <summary>
/// Reklam hakkı LEVEL başına TEK (kullanıcı kuralı): oynanan level için bir reklam izlenebilir —
/// fail'de bedava devam ya da can bitince +1 can, hangisi önce. Çıkıp girince, ana menüye dönünce
/// de geri gelmez; level geçilince (CurrentLevel değişir) yeni level'da hak yeniden açılır.
/// Kullanılan level numarası kalıcı tutulur.
/// </summary>
public static class LevelAdRight
{
    // Eski adıyla korunuyor (mevcut kayıtlar geçerli kalsın).
    public const string UsedLevelKey = "ad_continue_used_level";

    public static bool IsUsed => PlayerPrefs.GetInt(UsedLevelKey, 0) == CurrentLevel.Global;

    /// <summary>Hakkı harcar. Zaten harcanmışsa false (reklam gösterilmemeli).</summary>
    public static bool TryConsume()
    {
        if (IsUsed) return false;
        PlayerPrefs.SetInt(UsedLevelKey, CurrentLevel.Global);
        PlayerPrefs.Save();
        return true;
    }
}
