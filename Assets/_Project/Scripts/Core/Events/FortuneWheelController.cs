using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FortuneWheelController : MonoBehaviour
{
    private const string KeyLastSpinTime = "fortune_wheel_last_spin_time";
    private static int s_cooldownHours = 24;

    [Header("Config")]
    [Tooltip("Spin hakları arasındaki süre (saat).")]
    [SerializeField, Min(1)] private int spinCooldownHours = 24;

    [Header("Free Spin Rewards")]
    [Tooltip("Ücretsiz spin için level bazlı ödül havuzu.")]
    [SerializeField] private LeveledWheelRewardConfig freeSpinConfig;
    [Tooltip("freeSpinConfig resolve edilemezse kullanılacak direkt ödül havuzu.")]
    [SerializeField] private DailySlotRewardConfig freeSpinDirectConfig;

    [Header("Paid Spin Rewards")]
    [Tooltip("Ücretli spin için level bazlı ödül havuzu. Boş bırakılırsa freeSpinConfig kullanılır.")]
    [SerializeField] private LeveledWheelRewardConfig paidSpinConfig;
    [Tooltip("paidSpinConfig resolve edilemezse kullanılacak direkt ödül havuzu.")]
    [SerializeField] private DailySlotRewardConfig paidSpinDirectConfig;
    [Tooltip("Ücretli spin maliyeti (altın).")]
    [SerializeField, Min(1)] private int paidSpinCost = 100;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;

    [Header("Wheel")]
    [SerializeField] private RectTransform innerWheel;
    [SerializeField] private float iconRadius = 190f;
    [SerializeField] private float iconSize = 60f;
    [Tooltip("İkonun container içindeki Y pozisyonu.")]
    [SerializeField] private float iconOffsetY = 29f;
    [SerializeField] private float amountFontSize = 18f;
    [SerializeField] private TMP_FontAsset amountFont;
    [SerializeField] private TMP_Text amountTextTemplate;

    [Header("UI")]
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private Button spinButton;
    [Tooltip("Spin butonundaki yazı.")]
    [SerializeField] private TMP_Text spinButtonText;
    [Tooltip("Free spin butonu yazısı.")]
    [SerializeField] private string freeSpinLabel = "SPIN";
    [Tooltip("Ücretli spin butonu sol yazısı (ikon ve miktar ayrıca gösterilir).")]
    [SerializeField] private string paidSpinLabel = "Spin";
    [Tooltip("Ücretli spin butonunda gösterilecek ikon (altın vb.).")]
    [SerializeField] private Sprite paidSpinButtonIcon;
    [Tooltip("Spin butonu üzerindeki ikon Image bileşeni.")]
    [SerializeField] private Image spinButtonIconImage;
    [Tooltip("İkonun sağındaki miktar texti (paid spin için paidSpinCost gösterir, free spin'de gizlenir).")]
    [SerializeField] private TMP_Text spinButtonAmountText;
    [SerializeField] private Button claimButton;
    [Tooltip("Ücretsiz spin geri sayımı için label container.")]
    [SerializeField] private GameObject noSpinAvailableLabel;
    [Tooltip("noSpinAvailableLabel içindeki geri sayım texti.")]
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("Yeterli altın yoksa gösterilecek label (opsiyonel).")]
    [SerializeField] private GameObject insufficientCoinsLabel;

    [Header("Win Feedback")]
    [SerializeField, Range(1f, 2f)] private float winPunchScale = 1.2f;
    [SerializeField] private float winPunchDuration = 0.3f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip spinSfx;
    [SerializeField] private AudioClip winSfx;

    [Header("Spin")]
    [SerializeField] private int fullRotations = 4;
    [SerializeField] private float spinDuration = 3.5f;
    [Tooltip("Wheel görselinin segment 0'ı 12 o'clock'tan ne kadar offset'li. Yanlış segment kazanıyorsa ayarla.")]
    [SerializeField] private float wheelSegmentOffset = 0f;
    [SerializeField, Range(0.5f, 2f)] private float spinPitchStart = 1.4f;
    [SerializeField, Range(0.1f, 1f)] private float spinPitchEnd = 0.6f;

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
    private DailySlotRewardConfig activeSpinConfig;
    private int selectedIndex;
    private bool isSpinning;
    private bool rewardClaimed;
    private bool isFreeSpin;
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

        Debug.Log($"[FortuneWheel] Awake — " +
                  $"freeSpinConfig={SafeName(freeSpinConfig)} " +
                  $"freeDefaultConfig={SafeName(freeSpinConfig != null ? freeSpinConfig.defaultConfig : null)} " +
                  $"freeDirectConfig={SafeName(freeSpinDirectConfig)}", this);
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

        isSpinning     = false;
        rewardClaimed  = false;
        selectedReward = null;

        if (innerWheel != null) innerWheel.localEulerAngles = Vector3.zero;

        if (claimButton  != null) claimButton.gameObject.SetActive(false);
        if (rewardNameText != null) rewardNameText.text = "";
        if (winnerDisplay  != null) winnerDisplay.SetActive(false);
        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(false);
            var c = dimOverlay.color; c.a = 0f; dimOverlay.color = c;
        }

        RefreshSpinButtonState();

        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        bool canFree = HasAvailableSpin();
        if (!canFree && countdownText != null)
            countdownRoutine = StartCoroutine(CountdownRoutine());
        else if (countdownText != null)
            countdownText.text = "";

        PlaceIcons();

        if (glowRoutine != null) StopCoroutine(glowRoutine);
        if (pointerGlow != null) glowRoutine = StartCoroutine(PulseGlow());

        if (spinButtonPulse != null) StopCoroutine(spinButtonPulse);
        if (spinButton != null && spinButton.interactable)
            spinButtonPulse = StartCoroutine(PulseSpinButton());

        if (panelGroup != null) panelGroup.alpha = 0f;
        StartCoroutine(FadePanel(0f, 1f));
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        if (isSpinning) return;
        StartCoroutine(CloseRoutine());
    }

    // ── Spin Button State ─────────────────────────────────────────────────────

    private void RefreshSpinButtonState()
    {
        bool canFree   = HasAvailableSpin();
        bool canAfford = PlayerWallet.Coins >= paidSpinCost;

        if (spinButton != null)
        {
            spinButton.gameObject.SetActive(true);
            spinButton.interactable = true; // always clickable; paid spin is always an option
        }

        if (spinButtonText != null)
            spinButtonText.text = canFree ? freeSpinLabel : paidSpinLabel;

        bool showPaidExtras = !canFree;

        if (spinButtonIconImage != null)
        {
            spinButtonIconImage.gameObject.SetActive(showPaidExtras && paidSpinButtonIcon != null);
            if (showPaidExtras && paidSpinButtonIcon != null)
                spinButtonIconImage.sprite = paidSpinButtonIcon;
        }

        if (spinButtonAmountText != null)
        {
            spinButtonAmountText.gameObject.SetActive(showPaidExtras);
            if (showPaidExtras)
                spinButtonAmountText.text = paidSpinCost.ToString();
        }

        if (noSpinAvailableLabel != null) noSpinAvailableLabel.SetActive(!canFree);
        if (insufficientCoinsLabel != null) insufficientCoinsLabel.SetActive(!canFree && !canAfford);
    }

    // ── Icon Placement ───────────────────────────────────────────────────────

    private void PlaceIcons()
    {
        if (innerWheel == null) return;
        var config = GetDisplayConfig();
        Debug.Log($"[Wheel] PlaceIcons config={SafeName(config)} " +
                  $"free={SafeName(freeSpinConfig)}(def={SafeName(freeSpinConfig != null ? freeSpinConfig.defaultConfig : null)})(direct={SafeName(freeSpinDirectConfig)}) " +
                  $"paid={SafeName(paidSpinConfig)}(def={SafeName(paidSpinConfig != null ? paidSpinConfig.defaultConfig : null)})(direct={SafeName(paidSpinDirectConfig)})");
        if (config == null)
        {
            Debug.LogWarning("[Wheel] PlaceIcons: config NULL — LeveledWheelRewardConfig içindeki defaultConfig atanmamış " +
                             "veya DailySlotRewardConfig build'a dahil edilmemiş. " +
                             "FortuneWheelController inspector'ında freeSpinDirectConfig / paidSpinDirectConfig'i ata.");
            return;
        }

        for (int i = innerWheel.childCount - 1; i >= 0; i--)
            Destroy(innerWheel.GetChild(i).gameObject);

        int count = Mathf.Min(config.rewards.Count, 8);
        if (count <= 0) return;

        float segmentAngle = 360f / count;

        for (int i = 0; i < count; i++)
        {
            var reward = config.rewards[i];
            if (reward == null) continue;

            float angleCW = i * segmentAngle + segmentAngle * 0.5f;
            float rad = angleCW * Mathf.Deg2Rad;

            var container = new GameObject($"Segment_{i}", typeof(RectTransform));
            container.transform.SetParent(innerWheel, false);

            var crt = container.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(iconSize * 2.2f, iconSize * 2.4f);
            crt.pivot     = new Vector2(0.5f, 0.5f);
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(iconRadius * Mathf.Sin(rad), iconRadius * Mathf.Cos(rad));
            crt.localEulerAngles = new Vector3(0f, 0f, -angleCW);

            if (reward.icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(container.transform, false);

                var irt = iconGo.GetComponent<RectTransform>();
                irt.sizeDelta = new Vector2(iconSize, iconSize);
                irt.pivot     = new Vector2(0.5f, 0.5f);
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(0f, iconOffsetY);

                var img = iconGo.GetComponent<Image>();
                img.sprite = reward.icon;
                img.preserveAspect = true;
            }

            TMP_Text tmp;
            if (amountTextTemplate != null)
            {
                var textGo = Instantiate(amountTextTemplate.gameObject, container.transform, false);
                textGo.name = "Amount";
                textGo.SetActive(true);

                var textRt = textGo.GetComponent<RectTransform>();
                textRt.localEulerAngles = new Vector3(0f, 0f, 90f);

                tmp = textGo.GetComponent<TMP_Text>();
            }
            else
            {
                var textGo = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(container.transform, false);

                var textRt = textGo.GetComponent<RectTransform>();
                textRt.sizeDelta = new Vector2(130f, 42f);
                textRt.pivot     = new Vector2(0.5f, 0.5f);
                textRt.anchorMin = new Vector2(0.5f, 0.5f);
                textRt.anchorMax = new Vector2(0.5f, 0.5f);
                textRt.anchoredPosition  = new Vector2(0f, -49f);
                textRt.localEulerAngles  = new Vector3(0f, 0f, 90f);

                tmp = textGo.GetComponent<TextMeshProUGUI>();
                tmp.fontSize  = amountFontSize;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color     = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = Color.black;
            }

            tmp.text = "+" + reward.amount.ToString();
            if (amountFont != null) tmp.font = amountFont;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }

    // ── Spin ─────────────────────────────────────────────────────────────────

    private static string SafeName(UnityEngine.Object obj) => obj != null ? obj.name : "NULL";

    private void OnSpinClicked()
    {
        if (isSpinning) return;

        isFreeSpin = HasAvailableSpin();

        if (isFreeSpin)
        {
            activeSpinConfig = ResolveConfig(freeSpinConfig, freeSpinDirectConfig)
                            ?? ResolveConfig(paidSpinConfig, paidSpinDirectConfig);
            PlayerPrefs.SetString(KeyLastSpinTime, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }
        else
        {
            if (!PlayerWallet.SpendCoins(paidSpinCost))
            {
                RefreshSpinButtonState();
                return;
            }
            activeSpinConfig = ResolveConfig(paidSpinConfig, paidSpinDirectConfig)
                            ?? ResolveConfig(freeSpinConfig, freeSpinDirectConfig);
        }

        if (activeSpinConfig == null || activeSpinConfig.rewards.Count == 0) return;

        int count = Mathf.Min(activeSpinConfig.rewards.Count, 8);
        selectedIndex = PickRandomIndex(count, activeSpinConfig);
        if (selectedIndex < 0) return;
        selectedReward = activeSpinConfig.rewards[selectedIndex];

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        if (spinButtonPulse != null) { StopCoroutine(spinButtonPulse); spinButtonPulse = null; }
        if (spinButton    != null) spinButton.interactable = false;
        if (rewardNameText != null) rewardNameText.text   = "";

        int   count        = Mathf.Min(activeSpinConfig.rewards.Count, 8);
        float segmentAngle = 360f / count;
        Debug.Log($"[FortuneWheel] {(isFreeSpin ? "FREE" : "PAID")} selectedIndex={selectedIndex} reward={selectedReward?.fallbackName}");

        float landingOffset = UnityEngine.Random.Range(-segmentAngle * 0.25f, segmentAngle * 0.25f);
        float targetCW = (fullRotations + 1) * 360f
                       - selectedIndex * segmentAngle
                       - segmentAngle * 0.5f
                       + wheelSegmentOffset
                       + landingOffset;

        if (GameSettings.SoundEnabled && sfxSource != null && spinSfx != null)
        {
            sfxSource.clip  = spinSfx;
            sfxSource.loop  = true;
            sfxSource.pitch = spinPitchStart;
            sfxSource.Play();
        }

        int   lastTickSeg = -1;
        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / spinDuration);
            float eased  = 1f - Mathf.Pow(1f - t, 3f);
            float current = Mathf.Lerp(0f, targetCW, eased);
            innerWheel.localEulerAngles = new Vector3(0f, 0f, -current);

            if (sfxSource != null) sfxSource.pitch = Mathf.Lerp(spinPitchStart, spinPitchEnd, eased);

            int seg = (int)(current / segmentAngle) % count;
            if (seg != lastTickSeg)
            {
                lastTickSeg = seg;
                if (GameSettings.SoundEnabled && sfxSource != null && tickSfx != null) sfxSource.PlayOneShot(tickSfx);
            }
            yield return null;
        }

        if (sfxSource != null) { sfxSource.Stop(); sfxSource.loop = false; sfxSource.pitch = 1f; }
        innerWheel.localEulerAngles = new Vector3(0f, 0f, -targetCW);

        if (GameSettings.SoundEnabled && sfxSource != null && winSfx != null) sfxSource.PlayOneShot(winSfx);

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

    // ── Config Helpers ───────────────────────────────────────────────────────

    private DailySlotRewardConfig GetDisplayConfig()
    {
        bool canFree = HasAvailableSpin();
        return canFree
            ? (ResolveConfig(freeSpinConfig, freeSpinDirectConfig) ?? ResolveConfig(paidSpinConfig, paidSpinDirectConfig))
            : (ResolveConfig(paidSpinConfig, paidSpinDirectConfig) ?? ResolveConfig(freeSpinConfig, freeSpinDirectConfig));
    }

    private DailySlotRewardConfig ResolveConfig(LeveledWheelRewardConfig leveled, DailySlotRewardConfig direct = null)
    {
        if (leveled != null)
        {
            var resolved = leveled.GetForCurrentLevel() ?? leveled.defaultConfig;
            if (resolved != null) return resolved;
        }
        return direct;
    }

    // ── Weighted Random ──────────────────────────────────────────────────────

    private int PickRandomIndex(int count, DailySlotRewardConfig config)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            if (config.rewards[i] != null) total += Mathf.Max(0, config.rewards[i].weight);
        if (total <= 0) return 0;
        int roll = UnityEngine.Random.Range(0, total);
        int acc  = 0;
        for (int i = 0; i < count; i++)
        {
            if (config.rewards[i] == null) continue;
            acc += Mathf.Max(0, config.rewards[i].weight);
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
                RefreshSpinButtonState();
                if (spinButtonPulse != null) StopCoroutine(spinButtonPulse);
                if (spinButton != null && spinButton.interactable)
                    spinButtonPulse = StartCoroutine(PulseSpinButton());
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

        var winner    = innerWheel.GetChild(selectedIndex);
        Vector3 base_ = winner.localScale;
        Vector3 peak  = base_ * winnerHighlightScale;
        float   dur   = 0.25f;

        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            winner.localScale = Vector3.Lerp(base_, peak, Mathf.SmoothStep(0f, 1f, t / dur));
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
        if (countdownRoutine  != null) { StopCoroutine(countdownRoutine);  countdownRoutine  = null; }
        if (glowRoutine       != null) { StopCoroutine(glowRoutine);       glowRoutine       = null; }
        if (spinButtonPulse   != null) { StopCoroutine(spinButtonPulse);   spinButtonPulse   = null; }
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
    private void OnValidate()
    {
        ValidateSpinConfig("freeSpinConfig",  freeSpinConfig,  freeSpinDirectConfig);
        ValidateSpinConfig("paidSpinConfig",  paidSpinConfig,  paidSpinDirectConfig);
    }

    private void ValidateSpinConfig(string label, LeveledWheelRewardConfig leveled, DailySlotRewardConfig direct)
    {
        bool leveledOk = leveled != null && leveled.defaultConfig != null;
        bool directOk  = direct != null;
        if (!leveledOk && !directOk)
            Debug.LogWarning(
                $"[FortuneWheel] {label} çözülemiyor! " +
                $"LeveledConfig='{SafeName(leveled)}' defaultConfig={SafeName(leveled != null ? leveled.defaultConfig : null)}, " +
                $"DirectConfig={SafeName(direct)}. " +
                $"En az birini ata.", this);
    }

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
