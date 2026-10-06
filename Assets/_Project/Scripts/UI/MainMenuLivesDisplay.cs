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

    /// Can ödüllerinin (normal / süreli sonsuz can) menüde uçacağı hedef: TopHUD kalp ikonu.
    public RectTransform HeartTarget => heartImage != null ? heartImage.rectTransform : transform as RectTransform;

    [Tooltip("Sonsuz can aktifken gösterilecek ikon.")]
    [SerializeField] private Sprite infiniteHeartSprite;

    [Tooltip("Kalp değişim animasyonu süresi (saniye).")]
    [SerializeField, Min(0.05f)] private float heartSwapDuration = 0.3f;

    [Header("Yerleşim: kalpte sayı · sağında durum · köşede fazla can")]
    [Tooltip("Açıkken LivesText kalbin içine, TimerText kalbin sağına taşınır (pozisyonlar aşağıdan, kalbe göre).")]
    [SerializeField] private bool layoutAroundHeart = true;
    [Tooltip("Kalp içindeki sayı (en fazla RegenCap = 5; tek hane).")]
    [SerializeField, Min(8f)] private float heartNumberFontSize = 42f;
    [SerializeField] private Vector2 heartNumberOffset = new Vector2(0f, 3f);
    [SerializeField] private Color heartNumberOutline = new Color(0.42f, 0.04f, 0.07f, 1f);
    [Tooltip("Durum yazısı (Dolu / geri sayım / sonsuz can süresi) merkezinin kalp merkezine göre konumu.")]
    [SerializeField] private Vector2 statusOffset = new Vector2(100f, 0f);
    [SerializeField] private Vector2 statusSize = new Vector2(120f, 56f);
    [SerializeField, Min(8f)] private float statusFontSize = 30f;
    [Tooltip("RegenCap üstündeki (bedava/hediye/satın alınan) can rozeti merkezinin kalp merkezine göre konumu.")]
    [SerializeField] private Vector2 extraBadgeOffset = new Vector2(150f, 36f);
    [SerializeField, Min(8f)] private float extraBadgeSize = 46f;

    [Header("Reklam")]
    [SerializeField] private bool simulateAdInEditor = true;
    [SerializeField, Min(0f)] private float simulatedAdDuration = 2f;

    [Header("Can 0 iken satın alma")]
    [Tooltip("Can 0 iken kalbe tıklayınca teklif edilen can paketi adedi.")]
    [SerializeField, Min(1)] private int buyLivesPackAmount = 5;
    [Tooltip("Can paketinin coin fiyatı.")]
    [SerializeField, Min(0)] private int buyLivesPackCost = 900;

    // ─────────────────────────────────────────────────────────────────────────

    private bool _adInProgress;
    private Sprite _normalHeartSprite;
    private bool _infiniteActive;
    private Coroutine _heartSwapRoutine;
    private TMP_Text _extraBadgeText;

    private void Awake()
    {
        LivesTimerService.EnsureExists();
        if (heartImage != null)
            _normalHeartSprite = heartImage.sprite;
        if (layoutAroundHeart)
            LayoutAroundHeart();
    }

    // Kalp kutusu (Royal Match düzeni): kalbin içinde dolu can sayısı (en fazla 5 → hep tek hane),
    // sağında okunur durum yazısı, kutunun sağ üst köşesinde 5'in üstündeki canlar için rozet.
    private void LayoutAroundHeart()
    {
        if (heartImage == null) return;
        var heart = heartImage.rectTransform;
        var bar = heart.parent as RectTransform;
        if (bar == null) return;

        if (livesText != null)
        {
            var rt = livesText.rectTransform;
            rt.SetParent(heart, false);   // kalp animasyonuyla birlikte ölçeklenir
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = heart.sizeDelta;
            rt.anchoredPosition = heartNumberOffset;
            livesText.fontSize = heartNumberFontSize;
            livesText.enableAutoSizing = false;
            livesText.alignment = TextAlignmentOptions.Center;
            livesText.textWrappingMode = TextWrappingModes.NoWrap;
            livesText.raycastTarget = false;
            // Awake'te yazının TMP kurulumu henüz bitmemiş olabilir → güvenli kontur.
            TmpOutline.Apply(livesText, 0.22f, heartNumberOutline);
        }

        if (timerText != null)
        {
            var rt = timerText.rectTransform;
            rt.SetParent(bar, false);
            rt.anchorMin = heart.anchorMin;
            rt.anchorMax = heart.anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = statusSize;
            rt.anchoredPosition = heart.anchoredPosition + statusOffset;
            timerText.enableAutoSizing = true;   // "Dolu" büyük; uzun sonsuz-can süresi sığana dek küçülür
            timerText.fontSizeMax = statusFontSize;
            timerText.fontSizeMin = statusFontSize * 0.6f;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.textWrappingMode = TextWrappingModes.NoWrap;
            timerText.raycastTarget = false;
        }

        _extraBadgeText = UiBadge.CreateCount(bar, "ExtraLivesBadge", extraBadgeSize,
            livesText != null ? livesText.font : null);
        var badge = (RectTransform)_extraBadgeText.transform.parent.parent;
        badge.anchorMin = heart.anchorMin;
        badge.anchorMax = heart.anchorMax;
        badge.anchoredPosition = heart.anchoredPosition + extraBadgeOffset;
        badge.SetAsLastSibling();
        badge.gameObject.SetActive(false);
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

        // Sonsuz can aktifken (timed reward) satın almaya gerek yok.
        if (TimedRewardService.IsLivesFree()) return;

        // Can 0 → paket satın alma onayı (coin ile). Coin yetmezse market.
        if (LivesManager.Current <= 0)
        {
            ShowBuyLivesConfirm();
            return;
        }

        if (LivesManager.Current >= LivesManager.MaxLives) return;
        // Level başına tek reklam hakkı (LevelAdRight) burada da geçerli.
        if (LevelAdRight.IsUsed) return;
        StartAd();
    }

    // Can 0: ortak "Canın Bitti" teklifi (paket / reklam / market) — level öncesi popup'la aynı yol.
    // Coin yetmiyorsa can paketi butonu pasif görünür; alttaki "Devam" kapatır.
    private void ShowBuyLivesConfirm()
    {
        LivesRefillOffer.Show(this, RefreshDisplay, buyLivesPackAmount, buyLivesPackCost, GameLocalization.Get("prelevel_popup_continue"));
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void StartAd()
    {
#if UNITY_EDITOR
        if (simulateAdInEditor)
        {
            if (!LevelAdRight.TryConsume()) return;
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
        int lives = LivesManager.Current;
        int cap = LivesManager.RegenCapLives;

        if (livesText != null)
        {
            livesText.gameObject.SetActive(!livesFree);
            // Yeni düzende kalp en fazla RegenCap'i gösterir; fazlası köşedeki rozette.
            if (!livesFree)
                livesText.text = (layoutAroundHeart ? Mathf.Min(lives, cap) : lives).ToString();
        }

        if (_extraBadgeText != null)
        {
            int extra = lives - cap;
            _extraBadgeText.transform.parent.parent.gameObject.SetActive(!livesFree && extra > 0);
            if (extra > 0) _extraBadgeText.text = extra.ToString();
        }

        RefreshTimer();
        SyncInfiniteHeartState();
    }

    // Her karede çağrılır: metin yalnız değişince yazılır (TMP her atamada yeniden mesh kurar).
    private void RefreshTimer()
    {
        if (timerText == null) return;

        string status;
        if (TimedRewardService.IsLivesFree())
            status = FormatTimeSpan(TimedRewardService.GetRemaining(DailySlotRewardType.Lives));
        else if (!LivesManager.IsRegenFull)
            status = FormatTimeSpan(LivesManager.TimeUntilNextLife);
        else
            status = layoutAroundHeart ? GameLocalization.Get("lives_full") : null;   // eski düzen: gizli

        bool show = status != null;
        if (timerText.gameObject.activeSelf != show) timerText.gameObject.SetActive(show);
        if (show && timerText.text != status) timerText.text = status;
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
        return TimeFormat.Countdown(span);
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
