using UnityEngine;

/// <summary>
/// Oynanan global level numarasının TEK merkezi kaynağı. Oyun genelinde "current_level" PlayerPrefs
/// anahtarı kullanılıyor (LevelEnd win'de +1 yazar; HomeScreen/PreLevel/Profile/Booster hepsi okur).
/// Yeni kodda LevelCatalog referansı bağlamak yerine BUNU kullan — sadece level numarası lazımsa.
/// </summary>
public static class CurrentLevel
{
    public const string PrefsKey = "current_level";

    /// <summary>Şu an oynanan/açık global level (1..N).</summary>
    public static int Global => Mathf.Max(1, PlayerPrefs.GetInt(PrefsKey, 1));
}
