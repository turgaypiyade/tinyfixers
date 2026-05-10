using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Can göstergesi — kalp ikonunun yanındaki metin ve opsiyonel geri sayım.
///
/// Kurulum (LifeCounter GameObject):
///   1. Bu scripti ekle.
///   2. livesText  → mevcut TMP_Text (sayıyı gösterir: "10", "0")
///   3. timerText  → opsiyonel ikinci TMP_Text (geri sayım: "44:23")
///                   Can doluysa otomatik gizlenir.
///   4. LifeCounter'a Button ekle, onClick → OnAreaClicked()
///
/// Reklam entegrasyonu:
///   - Editor'da simulateAdInEditor=true ise 2 sn sahte reklam oynar.
///   - Gerçek SDK'da: SDK'nın rewarded callback'inden OnAdRewarded() çağır.
/// </summary>
public class MainMenuLivesDisplay : MonoBehaviour
{
    [Header("Metinler")]
    [Tooltip("Sadece sayıyı gösterir: '10', '0'")]
    [SerializeField] private TMP_Text livesText;

    [Tooltip("Geri sayım metni. Can doluysa gizlenir. Opsiyonel.")]
    [SerializeField] private TMP_Text timerText;

    [Header("Reklam")]
    [SerializeField] private bool simulateAdInEditor = true;
    [SerializeField, Min(0f)] private float simulatedAdDuration = 2f;

    // ─────────────────────────────────────────────────────────────────────────

    private bool _adInProgress;

    private void Awake()
    {
        LivesTimerService.EnsureExists();
    }

    private void OnEnable()
    {
        LivesManager.OnLivesChanged += RefreshDisplay;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        LivesManager.OnLivesChanged -= RefreshDisplay;
    }

    private void Update()
    {
        RefreshTimer();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tıklama — Button onClick'e bağla
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LifeCounter alanına tıklanınca çağrılır.
    /// Can = 0 ise reklam başlatır.
    /// Can > 0 ise ileride satın alma ekranına yönlendirilebilir.
    /// </summary>
    public void OnAreaClicked()
    {
        if (_adInProgress) return;
        if (LivesManager.Current >= LivesManager.MaxLives) return;
        StartAd();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reklam
    // ─────────────────────────────────────────────────────────────────────────

    private void StartAd()
    {
#if UNITY_EDITOR
        if (simulateAdInEditor)
        {
            StartCoroutine(SimulateAd());
            return;
        }
#endif
        // Gerçek SDK entegrasyonu:
        // MyAdSDK.ShowRewardedAd(rewarded => { if (rewarded) OnAdRewarded(); });
        Debug.LogWarning("[LivesDisplay] Reklam SDK'sı bağlanmadı. simulateAdInEditor=true yapın veya SDK entegre edin.");
    }

    /// <summary>
    /// Reklam SDK'sı ödüllü tamamlandığında çağır.
    /// </summary>
    public void OnAdRewarded()
    {
        _adInProgress = false;
        if (LivesManager.Current < LivesManager.MaxLives)
            LivesManager.AddLives(1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Güncelleme
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshDisplay()
    {
        if (livesText != null)
            livesText.text = LivesManager.Current.ToString();

        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (timerText == null) return;

        bool showTimer = !LivesManager.IsRegenFull;
        timerText.gameObject.SetActive(showTimer);

        if (showTimer)
            timerText.text = FormatTimeSpan(LivesManager.TimeUntilNextLife);
    }

    private static string FormatTimeSpan(System.TimeSpan span)
    {
        if (span.TotalSeconds <= 0) return "00:00";

        int totalMinutes = (int)span.TotalMinutes;
        int seconds      = span.Seconds;

        if (totalMinutes >= 60)
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{seconds:D2}";

        return $"{totalMinutes:D2}:{seconds:D2}";
    }

#if UNITY_EDITOR
    private IEnumerator SimulateAd()
    {
        _adInProgress = true;
        Debug.Log($"[LivesDisplay] Reklam simülasyonu ({simulatedAdDuration}s)...");
        yield return new WaitForSecondsRealtime(simulatedAdDuration);
        Debug.Log("[LivesDisplay] Simülasyon tamamlandı — 1 can eklendi.");
        OnAdRewarded();
    }
#endif
}
