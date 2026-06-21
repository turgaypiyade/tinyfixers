using TMPro;
using UnityEngine;

/// <summary>
/// Profil sayfasındaki istatistik kutusunu doldurur. PlayerStats'tan okur ve PlayerStats.OnChanged
/// ile otomatik yenilenir. Her alan opsiyonel — sadece bağladıkların gösterilir.
///
/// Format string'lerde {0} sayı/değerle değişir. Boş bırakırsan sadece değer yazılır.
/// </summary>
public sealed class ProfileStatsView : MonoBehaviour
{
    // Başlıkları UI'da ayrı koyuyorsan format'ları BOŞ bırak → sadece değer yazılır.
    [Header("İstatistikler (format boş = sadece değer)")]
    [SerializeField] private TMP_Text firstTryClearsText;
    [SerializeField] private string   firstTryClearsFormat = "";

    [SerializeField] private TMP_Text longestStreakText;
    [SerializeField] private string   longestStreakFormat = "";

    [SerializeField] private TMP_Text weeklyClearsText;
    [SerializeField] private string   weeklyClearsFormat = "";

    [Header("Mevcut Level")]
    [SerializeField] private TMP_Text currentLevelText;
    [SerializeField] private string   currentLevelFormat = "";
    [Tooltip("PlayerPrefs anahtarı — oyunun geri kalanıyla aynı: current_level.")]
    [SerializeField] private string   levelPrefsKey = "current_level";

    [Header("İlk Giriş")]
    [SerializeField] private TMP_Text firstLaunchText;
    [SerializeField] private string   firstLaunchFormat = "";
    [SerializeField] private string   dateFormat = "dd.MM.yyyy";

    [Header("Takım (şimdilik placeholder)")]
    [SerializeField] private TMP_Text teamNameText;
    [Tooltip("Takım sistemi yokken gösterilecek metin.")]
    [SerializeField] private string   teamPlaceholder = "Takım yok";

    private void OnEnable()
    {
        PlayerStats.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerStats.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        Set(firstTryClearsText, firstTryClearsFormat, PlayerStats.FirstTryClears);
        Set(longestStreakText,  longestStreakFormat,  PlayerStats.LongestStreak);
        Set(weeklyClearsText,   weeklyClearsFormat,   PlayerStats.WeeklyClears);
        Set(currentLevelText,   currentLevelFormat,   UnityEngine.PlayerPrefs.GetInt(levelPrefsKey, 1));

        if (firstLaunchText != null)
        {
            string date = PlayerStats.FirstLaunchDate.ToString(dateFormat);
            firstLaunchText.text = string.IsNullOrEmpty(firstLaunchFormat) ? date : string.Format(firstLaunchFormat, date);
        }

        if (teamNameText != null)
            teamNameText.text = teamPlaceholder;
    }

    private static void Set(TMP_Text text, string format, int value)
    {
        if (text == null) return;
        text.text = string.IsNullOrEmpty(format) ? value.ToString() : string.Format(format, value);
    }
}
