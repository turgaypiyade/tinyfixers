using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuLivesDisplay : MonoBehaviour
{
    [Header("Metinler")]
    [Tooltip("Sadece sayıyı gösterir: '10', '0'")]
    [SerializeField] private TMP_Text livesText;

    [Tooltip("Geri sayım metni. Can doluysa gizlenir. Opsiyonel.")]
    [SerializeField] private TMP_Text timerText;

    [Header("Kalp İkonu")]
    [Tooltip("TopHUD'daki kalp Image bileşeni.")]
    [SerializeField] private Image heartImage;

    [Tooltip("Sonsuz can aktifken gösterilecek ikon.")]
    [SerializeField] private Sprite infiniteHeartSprite;

    [Tooltip("Kalp değişim animasyonu süresi (saniye).")]
    [SerializeField, Min(0.05f)] private float heartSwapDuration = 0.3f;

    [Header("Reklam")]
    [SerializeField] private bool simulateAdInEditor = true;
    [SerializeField, Min(0f)] private float simulatedAdDuration = 2f;

    // ─────────────────────────────────────────────────────────────────────────

    private bool _adInProgress;
    private Sprite _normalHeartSprite;
    private bool _infiniteActive;
    private Coroutine _heartSwapRoutine;

    private void Awake()
    {
        LivesTimerService.EnsureExists();
        if (heartImage != null)
            _normalHeartSprite = heartImage.sprite;
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
        SyncInfiniteHeartState();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void OnAreaClicked()
    {
        if (_adInProgress) return;
        if (LivesManager.Current >= LivesManager.MaxLives) return;
        StartAd();
    }

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
        Debug.LogWarning("[LivesDisplay] Reklam SDK'sı bağlanmadı. simulateAdInEditor=true yapın veya SDK entegre edin.");
    }

    public void OnAdRewarded()
    {
        _adInProgress = false;
        if (LivesManager.Current < LivesManager.MaxLives)
            LivesManager.AddLives(1);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshDisplay()
    {
        bool livesFree = TimedRewardService.IsLivesFree();

        if (livesText != null)
        {
            livesText.gameObject.SetActive(!livesFree);
            if (!livesFree)
                livesText.text = LivesManager.Current.ToString();
        }

        RefreshTimer();
        SyncInfiniteHeartState();
    }

    private void RefreshTimer()
    {
        if (timerText == null) return;

        if (TimedRewardService.IsLivesFree())
        {
            timerText.gameObject.SetActive(true);
            timerText.text = FormatTimeSpan(TimedRewardService.GetRemaining(DailySlotRewardType.Lives));
            return;
        }

        bool showTimer = !LivesManager.IsRegenFull;
        timerText.gameObject.SetActive(showTimer);
        if (showTimer)
            timerText.text = FormatTimeSpan(LivesManager.TimeUntilNextLife);
    }

    // ── Infinite Heart ────────────────────────────────────────────────────────

    private void SyncInfiniteHeartState()
    {
        bool shouldBeInfinite = TimedRewardService.IsLivesFree();
        if (shouldBeInfinite == _infiniteActive) return;

        _infiniteActive = shouldBeInfinite;

        if (_heartSwapRoutine != null)
            StopCoroutine(_heartSwapRoutine);

        _heartSwapRoutine = StartCoroutine(CoSwapHeart(shouldBeInfinite));
    }

    private IEnumerator CoSwapHeart(bool toInfinite)
    {
        if (heartImage == null) yield break;

        var rt = heartImage.rectTransform;
        Vector3 baseScale = rt.localScale;
        float half = Mathf.Max(0.01f, heartSwapDuration * 0.5f);

        // Scale down
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rt.localScale = baseScale * (1f - 0.65f * EaseIn(t));
            yield return null;
        }

        // Swap sprite
        Sprite target = toInfinite ? infiniteHeartSprite : _normalHeartSprite;
        if (target != null)
            heartImage.sprite = target;

        // Scale up with soft bounce
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rt.localScale = baseScale * (0.35f + 0.65f * EaseOutBack(t));
            yield return null;
        }

        rt.localScale = baseScale;
        _heartSwapRoutine = null;
    }

    private static float EaseIn(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ─────────────────────────────────────────────────────────────────────────

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
