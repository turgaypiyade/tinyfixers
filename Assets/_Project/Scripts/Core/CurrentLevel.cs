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

    /// <summary>Şu an OYNANAN seviyenin katalog numarası: Usta Ligi oturumunda lig seviyesi, değilse Global.
    /// Yalnız "hangi seviye verisi yüklenecek" soranlar kullanır (seçici, uzak hamle ayarı, PreLevel hedefleri);
    /// oyuncunun ana yol ilerlemesi her zaman Global'dir.
    /// Deneme kaydı (sayaç, can/vazgeçme, seviye reklam hakkı) ve analitik de bunu kullanır: deneme sayaçları tek seviyelik
    /// (seviye değişince sıfırlanır) ve ligdeyken ana yolda aktif deneme olmaz → çakışma yok. Analitikte lig seviyesi
    /// "L57" diye yazılır (GameAnalytics.LevelText), ana yol hunisine ("000057") karışmaz.</summary>
    public static int Played => MasterLeague.IsPlaying ? MasterLeague.ActiveLevel : Global;
}
