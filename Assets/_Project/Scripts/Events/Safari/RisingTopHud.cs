using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yükseliş (Rising) eventinin üst HUD'u — salt görüntü. Sol kutu "Seviye" (kat N/7), sağ kutu
/// "Oyuncu" (kalan sayı). Başlıklar lokalizasyondan (rising_title/rising_level/rising_players);
/// değerleri <c>RisingMapScreen</c> koreografi sırasında <see cref="SetLevel"/>/<see cref="SetPlayers"/>
/// ile besler (tek kaynak = harita ekranı; kalabalık boyutu orada hesaplanır).
/// Orta kutu SafariSchedule üzerinden etkinliğin bitişine kalan süreyi gösterir.
///
/// Kutu başlıkları sarı, değerler beyaz (renkler Inspector'dan; kod ezmez).
/// </summary>
public sealed class RisingTopHud : MonoBehaviour
{
    [Header("Başlık (mor bant)")]
    [SerializeField] private TMP_Text titleText;

    [Header("Orta kutu — Etkinlik süresi")]
    [SerializeField] private TMP_Text timerValueText;

    [Header("Sol kutu — Seviye")]
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private Image    levelIcon;
    [SerializeField] private TMP_Text levelValueText;

    [Header("Sağ kutu — Oyuncu")]
    [SerializeField] private TMP_Text playersTitleText;
    [SerializeField] private Image    playersIcon;
    [SerializeField] private TMP_Text playersValueText;

    private SafariEventController controller;
    private long lastUpdatedUtcSecond = -1;

    private void Awake() => RefreshTitles();

    private void OnEnable()
    {
        RefreshTitles();
        lastUpdatedUtcSecond = -1;
        RefreshCountdown();
    }

    private void Update() => RefreshCountdown();

    public void Bind(SafariEventController owner)
    {
        controller = owner;
        lastUpdatedUtcSecond = -1;
        RefreshCountdown();
    }

    private void RefreshCountdown()
    {
        if (timerValueText == null) return;

        // UTC keeps the countdown accurate after suspension and when timeScale is zero.
        DateTime now = DateTime.UtcNow;
        long utcSecond = now.Ticks / TimeSpan.TicksPerSecond;
        if (utcSecond == lastUpdatedUtcSecond) return;
        lastUpdatedUtcSecond = utcSecond;

        DateTime end = SafariSchedule.GetWindowEnd(controller != null ? controller.Config : null, now);
        long seconds = end > now ? (long)Math.Ceiling((end - now).TotalSeconds) : 0;
        timerValueText.text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
    }

    /// <summary>Statik başlıkları (mor bant + kutu etiketleri) mevcut dile göre günceller.</summary>
    public void RefreshTitles()
    {
        if (titleText != null)        titleText.text        = Loc("rising_title",   "Yükseliş");
        if (levelTitleText != null)   levelTitleText.text   = Loc("rising_level",   "Seviye");
        if (playersTitleText != null) playersTitleText.text = Loc("rising_players", "Oyuncu");
    }

    /// <summary>Sol kutu değeri: "current/total" (ör. 3/7).</summary>
    public void SetLevel(int current, int total)
    {
        total = Mathf.Max(1, total);
        if (levelValueText != null)
            levelValueText.text = $"{Mathf.Clamp(current, 0, total)}/{total}";
    }

    /// <summary>Sağ kutu değeri: kalan oyuncu sayısı.</summary>
    public void SetPlayers(int remaining)
    {
        if (playersValueText != null)
            playersValueText.text = Mathf.Max(0, remaining).ToString();
    }

    private static string Loc(string key, string fallback)
    {
        string v = GameLocalization.Get(key);
        return string.IsNullOrEmpty(v) || v == key ? fallback : v;
    }
}
