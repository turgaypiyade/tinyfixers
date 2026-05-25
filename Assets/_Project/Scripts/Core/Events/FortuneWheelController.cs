using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FortuneWheelController : MonoBehaviour
{
    private const string KeyLastSpinTime = "fortune_wheel_last_spin_time"; // UTC ticks as string

    private static int s_cooldownHours = 24;

    [Header("Config")]
    [SerializeField] private DailySlotRewardConfig rewardConfig;
    [Tooltip("Spin hakları arasındaki süre (saat). Örn: 12 veya 24.")]
    [SerializeField, Min(1)] private int spinCooldownHours = 24;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;

    [Header("Wheel")]
    [SerializeField] private RectTransform innerWheel;
    [SerializeField] private float iconRadius = 190f;
    [SerializeField] private float iconSize = 60f;
    [Tooltip("İkonun container içindeki Y pozisyonu (dışa/merkeze doğru kayma).")]
    [SerializeField] private float iconOffsetY = 29f;
    [SerializeField] private float amountFontSize = 18f;
    [SerializeField] private TMP_FontAsset amountFont;
    [SerializeField] private TMP_Text amountTextTemplate;

    [Header("UI")]
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private GameObject noSpinAvailableLabel;
    [Tooltip("noSpinAvailableLabel içindeki geri sayım texti (opsiyonel).")]
    [SerializeField] private TMP_Text countdownText;

    [Header("Win Feedback")]
    [SerializeField, Range(1f, 2f)] private float winPunchScale = 1.2f;
    [SerializeField] private float winPunchDuration = 0.3f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip spinSfx;
    [SerializeField] private AudioClip winSfx;

    [Header("Spin")]
    [SerializeField] private int fullRotations = 4;
    [SerializeField] private float spinDuration = 3.5f;
    [Tooltip("Wheel görselinin segment 0'ı 12 o'clock'tan ne kadar offset'li (derece). Yanlış segment kazanıyorsa ayarla.")]
    [SerializeField] private float wheelSegmentOffset = 0f;
    [SerializeField, Range(0.5f, 2f)] private float spinPitchStart = 1.4f;
    [SerializeField, Range(0.1f, 1f)] private float spinPitchEnd   = 0.6f;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Winner Display")]
    [SerializeField] private GameObject winnerDisplay;
    [SerializeField] private Image winnerIcon;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private Graphic dimOverlay;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;

    [Header("Effects")]
    [SerializeField] private AudioClip tickSfx;
    [SerializeField] private Graphic pointerGlow;
    [SerializeField, Min(0.5f)] private float glowSpeed = 3f;
    [SerializeField, Range(1f, 1.5f)] private float winnerHighlightScale = 1.25f;

    private DailySlotReward selectedReward;
    private int selectedIndex;
    private bool isSpinning;
    private bool rewardClaimed;
    private Coroutine glowRoutine;
    private Coroutine spinButtonPulse;
    private Coroutine countdownRoutine;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        s_cooldownHours = spinCooldownHours;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (spinButton  != null) spinButton.onClick.AddListener(OnSpinClicked);
        if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
        if (panelRoot   != null) panelRoot.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public static bool HasAvailableSpin()
    {
        string stored = PlayerPrefs.GetString(KeyLastSpinTime, "");
        if (string.IsNullOrEmpty(stored)) return true;
        if (!long.TryParse(stored, out long ticks)) return true;
        return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours >= s_cooldownHours;
    }

    public static TimeSpan GetTimeUntilNextSpin()
    {
        string stored = PlayerPrefs.GetString(KeyLastSpinTime, "");
        if (string.IsNullOrEmpty(stored)) return TimeSpan.Zero;
        if (!long.TryParse(stored, out long ticks)) return TimeSpan.Zero;
        var elapsed  = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
        var cooldown = TimeSpan.FromHours(s_cooldownHours);
        return elapsed >= cooldown ? TimeSpan.Zero : cooldown - elapsed;
    }

    public void Open()
    {
        if (panelRoot == null) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelRoot.SetActive(true);

        isSpinning    = false;
        rewardClaimed = false;
        selectedReward = null;

        if (innerWheel != null) innerWheel.localEulerAngles = Vector3.zero;

        bool canSpin = HasAvailableSpin();
        if (noSpinAvailableLabel != null) noSpinAvailableLabel.SetActive(!canSpin);
        if (spinButton  != null) { spinButton.gameObject.SetActive(true); spinButton.interactable = canSpin; }
        if (claimButton != null)   claimButton.gameObject.SetActive(false);
        if (rewardNameText != null) rewardNameText.text = "";
        if (winnerDisplay  != null) winnerDisplay.SetActive(false);
        if (dimOverlay != null) { dimOverlay.gameObject.SetActive(false); var c = dimOverlay.color; c.a = 0f; dimOverlay.color = c; }

        // Countdown
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        if (!canSpin && countdownText != null)
            countdownRoutine = StartCoroutine(CountdownRoutine());
        else if (countdownText != null)
            countdownText.text = "";

        PlaceIcons();

        if (glowRoutine != null) StopCoroutine(glowRoutine);
        if (pointerGlow != null) glowRoutine = StartCoroutine(PulseGlow());

        if (spinButtonPulse != null) StopCoroutine(spinButtonPulse);
        if (spinButton != null && canSpin) spinButtonPulse = StartCoroutine(PulseSpinButton());

        if (panelGroup != null) panelGroup.alpha = 0f;
        StartCoroutine(FadePanel(0f, 1f));
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        if (isSpinning) return;
        StartCoroutine(CloseRoutine());
    }

    // ── Icon Placement ───────────────────────────────────────────────────────

    private void PlaceIcons()
    {
        if (innerWheel == null || rewardConfig == null) return;

        for (int i = innerWheel.childCount - 1; i >= 0; i--)
            Destroy(innerWheel.GetChild(i).gameObject);

        int count = Mathf.Min(rewardConfig.rewards.Count, 8);
        float segmentAngle = 360f / count;

        for (int i = 0; i < count; i++)
        {
            var reward = rewardConfig.rewards[i];
            if (reward == null) continue;

            float angleCW = i * segmentAngle + segmentAngle * 0.5f;
            float rad     = angleCW * Mathf.Deg2Rad;

            var container = new GameObject($"Segment_{i}", typeof(RectTransform));
            container.transform.SetParent(innerWheel, false);
            var crt = container.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(iconSize * 1.5f, iconSize * 2f);
            crt.anchoredPosition = new Vector2(iconRadius * Mathf.Sin(rad), iconRadius * Mathf.Cos(rad));
            crt.localEulerAngles = new Vector3(0f, 0f, -angleCW);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(container.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            irt.anchoredPosition = new Vector2(0f, iconOffsetY);

            var img = iconGo.GetComponent<Image>();
            img.sprite = reward.icon;
            img.preserveAspect = true;

            TMP_Text tmp;
            if (amountTextTemplate != null)
            {
                var textGo = Instantiate(amountTextTemplate.gameObject, container.transform, false);
                textGo.name = "Amount";
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.pivot            = new Vector2(0.5f, 1f);
                textRt.anchoredPosition = new Vector2(0f, -(iconSize * 0.5f) - 10f);
                textGo.SetActive(true);
                tmp = textGo.GetComponent<TMP_Text>();
            }
            else
            {
                var textGo = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(container.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.sizeDelta        = new Vector2(iconSize * 1.4f, 80f);
                textRt.pivot            = new Vector2(0.5f, 1f);
                textRt.anchoredPosition = new Vector2(0f, -(iconSize * 0.5f));
                tmp = textGo.GetComponent<TextMeshProUGUI>();
                tmp.fontSize  = amountFontSize;
                tmp.alignment = TextAlignmentOptions.Top;
                tmp.color     = Color.white;
                if (amountFont != null) tmp.font = amountFont;
            }

            tmp.text = "+" + reward.amount.ToString();
        }
    }

    // ── Spin ─────────────────────────────────────────────────────────────────

    private void OnSpinClicked()
    {
        if (isSpinning) return;
        if (!HasAvailableSpin()) return;
        if (rewardConfig == null || rewardConfig.rewards.Count == 0) return;

        int count = Mathf.Min(rewardConfig.rewards.Count, 8);
        selectedIndex = PickRandomIndex(count);
        if (selectedIndex < 0) return;
        selectedReward = rewardConfig.rewards[selectedIndex];

        PlayerPrefs.SetString(KeyLastSpinTime, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        if (spinButtonPulse != null) { StopCoroutine(spinButtonPulse); spinButtonPulse = null; }
        if (spinButton   != null) spinButton.interactable = false;
        if (rewardNameText != null) rewardNameText.text  = "";

        int   count        = Mathf.Min(rewardConfig.rewards.Count, 8);
        float segmentAngle = 360f / count;
        Debug.Log($"[FortuneWheel] selectedIndex={selectedIndex} reward={selectedReward?.fallbackName}");
        float landingOffset = UnityEngine.Random.Range(-segmentAngle * 0.25f, segmentAngle * 0.25f);
        float targetCW = (fullRotations + 1) * 360f
                       - selectedIndex * segmentAngle
                       - segmentAngle * 0.5f
                       + wheelSegmentOffset
                       + landingOffset;

        if (sfxSource != null && spinSfx != null)
        {
            sfxSource.clip  = spinSfx;
            sfxSource.loop  = true;
            sfxSource.pitch = spinPitchStart;
            sfxSource.Play();
        }

        int lastTickSeg = -1;
        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / spinDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float currentCW = Mathf.Lerp(0f, targetCW, eased);
            innerWheel.localEulerAngles = new Vector3(0f, 0f, -currentCW);

            if (sfxSource != null) sfxSource.pitch = Mathf.Lerp(spinPitchStart, spinPitchEnd, eased);

            int currentSeg = (int)(currentCW / segmentAngle) % count;
            if (currentSeg != lastTickSeg)
            {
                lastTickSeg = currentSeg;
                if (sfxSource != null && tickSfx != null) sfxSource.PlayOneShot(tickSfx);
            }
            yield return null;
        }

        if (sfxSource != null) { sfxSource.Stop(); sfxSource.loop = false; sfxSource.pitch = 1f; }
        innerWheel.localEulerAngles = new Vector3(0f, 0f, -targetCW);

        if (sfxSource != null && winSfx != null) sfxSource.PlayOneShot(winSfx);

        yield return HighlightWinner();
        yield return PlayWinPunch();
        yield return ShowWinnerDisplay();

        if (spinButton  != null) spinButton.gameObject.SetActive(false);
        if (claimButton != null) { claimButton.gameObject.SetActive(true); claimButton.interactable = true; }

        isSpinning = false;
    }

    private IEnumerator PlayWinPunch()
    {
        if (innerWheel == null) yield break;
        Vector3 start = innerWheel.localScale;
        Vector3 peak  = start * winPunchScale;
        float   half  = winPunchDuration * 0.5f;

        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            innerWheel.localScale = Vector3.Lerp(start, peak, Mathf.SmoothStep(0f, 1f, t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            innerWheel.localScale = Vector3.Lerp(peak, start, Mathf.SmoothStep(0f, 1f, t / half));
            yield return null;
        }
        innerWheel.localScale = start;
    }

    // ── Weighted Random ──────────────────────────────────────────────────────

    private int PickRandomIndex(int count)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            if (rewardConfig.rewards[i] != null) total += Mathf.Max(0, rewardConfig.rewards[i].weight);
        if (total <= 0) return 0;
        int roll = UnityEngine.Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < count; i++)
        {
            if (rewardConfig.rewards[i] == null) continue;
            acc += Mathf.Max(0, rewardConfig.rewards[i].weight);
            if (roll < acc) return i;
        }
        return count - 1;
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    private IEnumerator CountdownRoutine()
    {
        while (true)
        {
            var remaining = GetTimeUntilNextSpin();
            if (remaining <= TimeSpan.Zero)
            {
                if (countdownText != null) countdownText.text = "";
                // Spin hakkı geldi — UI'ı güncelle
                if (noSpinAvailableLabel != null) noSpinAvailableLabel.SetActive(false);
                if (spinButton != null) { spinButton.gameObject.SetActive(true); spinButton.interactable = true; }
                if (spinButtonPulse != null) StopCoroutine(spinButtonPulse);
                if (spinButton != null) spinButtonPulse = StartCoroutine(PulseSpinButton());
                yield break;
            }
            if (countdownText != null)
                countdownText.text = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private IEnumerator PulseGlow()
    {
        while (true)
        {
            float a = (Mathf.Sin(Time.unscaledTime * glowSpeed) + 1f) * 0.5f;
            var c = pointerGlow.color;
            c.a = Mathf.Lerp(0.3f, 1f, a);
            pointerGlow.color = c;
            yield return null;
        }
    }

    private IEnumerator PulseSpinButton()
    {
        var rt = spinButton.GetComponent<RectTransform>();
        Vector3 baseScale = rt.localScale;
        while (true)
        {
            float s = (Mathf.Sin(Time.unscaledTime * 2f) + 1f) * 0.5f;
            rt.localScale = baseScale * Mathf.Lerp(1f, 1.08f, s);
            yield return null;
        }
    }

    private IEnumerator ShowWinnerDisplay()
    {
        if (winnerDisplay == null) yield break;

        if (winnerIcon != null && selectedReward?.icon != null)
            winnerIcon.sprite = selectedReward.icon;

        if (winnerText != null)
        {
            string name = !string.IsNullOrEmpty(selectedReward.nameLocalizationKey)
                ? GameLocalization.Get(selectedReward.nameLocalizationKey) : null;
            if (string.IsNullOrEmpty(name) || name == selectedReward.nameLocalizationKey)
                name = selectedReward.fallbackName ?? selectedReward.type.ToString();
            winnerText.text = $"+{selectedReward.amount} {name}";
        }

        if (rewardNameText != null) rewardNameText.text = "";

        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(true);
            float dimDur = 0.2f;
            for (float t = 0f; t < dimDur; t += Time.unscaledDeltaTime)
            {
                var c = dimOverlay.color;
                c.a = Mathf.Lerp(0f, dimAlpha, t / dimDur);
                dimOverlay.color = c;
                yield return null;
            }
            var fc = dimOverlay.color; fc.a = dimAlpha; dimOverlay.color = fc;
        }

        winnerDisplay.SetActive(true);
        var rt = winnerDisplay.GetComponent<RectTransform>();
        float dur = 0.3f;

        for (float t = 0f; t < dur * 0.7f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.15f,
                Mathf.SmoothStep(0f, 1f, t / (dur * 0.7f)));
            yield return null;
        }
        for (float t = 0f; t < dur * 0.3f; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one,
                Mathf.SmoothStep(0f, 1f, t / (dur * 0.3f)));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private IEnumerator HighlightWinner()
    {
        if (innerWheel == null || selectedIndex < 0 || selectedIndex >= innerWheel.childCount)
            yield break;

        var winner = innerWheel.GetChild(selectedIndex);
        Vector3 baseScale = winner.localScale;
        Vector3 peak = baseScale * winnerHighlightScale;
        float dur = 0.25f;

        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            winner.localScale = Vector3.Lerp(baseScale, peak, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        winner.localScale = peak;
    }

    // ── Claim ────────────────────────────────────────────────────────────────

    private void OnClaimClicked()
    {
        if (selectedReward == null || rewardClaimed) return;
        DailySlotRewardService.Grant(selectedReward);
        rewardClaimed = true;
        if (claimButton != null) claimButton.interactable = false;
        StartCoroutine(CloseRoutine());
    }

    // ── Fade ─────────────────────────────────────────────────────────────────

    private IEnumerator CloseRoutine()
    {
        if (countdownRoutine != null) { StopCoroutine(countdownRoutine); countdownRoutine = null; }
        if (glowRoutine != null) { StopCoroutine(glowRoutine); glowRoutine = null; }
        if (spinButtonPulse != null) { StopCoroutine(spinButtonPulse); spinButtonPulse = null; }
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator FadePanel(float from, float to)
    {
        if (panelGroup == null) yield break;
        panelGroup.alpha = from;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        panelGroup.alpha = to;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Open")]
    private void TestOpen() => Open();

    [ContextMenu("Reset Daily Spin")]
    private void ResetDailySpin()
    {
        PlayerPrefs.DeleteKey(KeyLastSpinTime);
        PlayerPrefs.Save();
        Debug.Log("[FortuneWheel] Daily spin reset.");
    }
#endif
}
