using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Günlük slot machine. Günde 1 kez oynanabilir, ödül havuzundan rastgele bir ödül verir.
///
/// Akış:
///   1. HasAvailableSpin() → bugün spin hakkı kaldı mı?
///   2. Open() → UI açılır
///   3. SPIN butonu → reel'de hızlı icon cycle, sonra yavaşlar, kazanan ikon kalır
///   4. Claim butonu → ödül teslim edilir, popup kapanır
///   5. PlayerPrefs.daily_slot_last_spin_yyyymmdd kaydedilir
/// </summary>
public class DailySlotMachineController : MonoBehaviour
{
    private const string KeyLastSpinDate = "daily_slot_last_spin_date";
    private const string DateFormat      = "yyyy-MM-dd";

    [Header("Config")]
    [SerializeField] private DailySlotRewardConfig rewardConfig;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;

    [Header("Reel (Slot Görselleri)")]
    [Tooltip("Spin sırasında değişen ana ikon. Final kazanan ikon burada kalır.")]
    [SerializeField] private Image reelIconImage;
    [Tooltip("Reel altındaki kazanan ödülün adı. Spin esnasında boş, sonunda dolar.")]
    [SerializeField] private TMP_Text rewardNameText;

    [Header("Buttons")]
    [SerializeField] private Button spinButton;
    [SerializeField] private Button claimButton;
    [Tooltip("Bugün spin hakkı yoksa gösterilecek metin (örn \"Yarın tekrar gel!\").")]
    [SerializeField] private GameObject noSpinAvailableLabel;

    [Header("Spin Animation")]
    [SerializeField, Min(0.5f)] private float spinDuration   = 2.5f;
    [Tooltip("Spin başlangıcında icon değişim hızı (sn/icon). Yavaşlama bu hızdan başlar.")]
    [SerializeField, Min(0.01f)] private float startCycleSpeed = 0.05f;
    [SerializeField, Min(0.05f)] private float endCycleSpeed   = 0.35f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Win Feedback")]
    [SerializeField, Range(1f, 2f)] private float winPunchScale = 1.25f;
    [SerializeField, Min(0.05f)]    private float winPunchDuration = 0.3f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip spinSfx;
    [SerializeField] private AudioClip winSfx;

    [Header("Fade")]
    [SerializeField, Min(0.05f)] private float panelFadeDuration = 0.2f;

    private DailySlotReward selectedReward;
    private bool isSpinning;
    private bool rewardClaimed;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (spinButton  != null) spinButton.onClick.AddListener(OnSpinClicked);
        if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// Bugün spin hakkı var mı?
    public static bool HasAvailableSpin()
    {
        string lastSpinDate = PlayerPrefs.GetString(KeyLastSpinDate, "");
        string today = DateTime.Now.ToString(DateFormat);
        return lastSpinDate != today;
    }

    public void Open()
    {
        if (panelRoot == null) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        panelRoot.SetActive(true);

        bool canSpin = HasAvailableSpin();
        if (noSpinAvailableLabel != null) noSpinAvailableLabel.SetActive(!canSpin);

        if (spinButton  != null) spinButton.interactable  = canSpin;
        if (claimButton != null) claimButton.gameObject.SetActive(false);

        // Reel'i defaulta al
        if (reelIconImage != null && rewardConfig != null && rewardConfig.rewards.Count > 0)
            reelIconImage.sprite = rewardConfig.rewards[0].icon;
        if (rewardNameText != null) rewardNameText.text = "";

        selectedReward = null;
        rewardClaimed = false;
        isSpinning = false;

        if (panelGroup != null) panelGroup.alpha = 0f;
        StartCoroutine(FadePanel(0f, 1f));
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;
        if (isSpinning) return; // spin sırasında kapama yok
        StartCoroutine(CloseRoutine());
    }

    // ── Spin ─────────────────────────────────────────────────────────────────

    private void OnSpinClicked()
    {
        if (isSpinning) return;
        if (!HasAvailableSpin())
        {
            Debug.LogWarning("[DailySlot] Spin hakkı yok.");
            return;
        }
        if (rewardConfig == null || rewardConfig.rewards.Count == 0)
        {
            Debug.LogWarning("[DailySlot] rewardConfig boş.");
            return;
        }

        // Kazananı önceden seç (ağırlıklı rastgele)
        selectedReward = rewardConfig.PickRandom();
        if (selectedReward == null)
        {
            Debug.LogError("[DailySlot] PickRandom null döndü.");
            return;
        }

        // Tarihi kaydet — duplicate spin engellensin
        PlayerPrefs.SetString(KeyLastSpinDate, DateTime.Now.ToString(DateFormat));
        PlayerPrefs.Save();

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        if (spinButton != null) spinButton.interactable = false;
        if (rewardNameText != null) rewardNameText.text = "";

        if (sfxSource != null && spinSfx != null) sfxSource.PlayOneShot(spinSfx);

        float elapsed = 0f;
        int iconCount = rewardConfig.rewards.Count;
        int idx = 0;
        float timer = 0f;

        while (elapsed < spinDuration)
        {
            float t = Mathf.Clamp01(elapsed / spinDuration);
            float curve = speedCurve.Evaluate(t);
            float cycleSpeed = Mathf.Lerp(startCycleSpeed, endCycleSpeed, curve);

            timer += Time.unscaledDeltaTime;
            if (timer >= cycleSpeed)
            {
                timer = 0f;
                idx = (idx + 1) % iconCount;
                var sprite = rewardConfig.rewards[idx].icon;
                if (reelIconImage != null && sprite != null)
                    reelIconImage.sprite = sprite;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Reel'i kazanan ödülün ikonuyla sonlandır
        if (reelIconImage != null && selectedReward.icon != null)
            reelIconImage.sprite = selectedReward.icon;

        // Win SFX
        if (sfxSource != null && winSfx != null) sfxSource.PlayOneShot(winSfx);

        // Ödül adı
        if (rewardNameText != null)
        {
            string name = !string.IsNullOrEmpty(selectedReward.nameLocalizationKey)
                ? GameLocalization.Get(selectedReward.nameLocalizationKey)
                : null;
            if (string.IsNullOrEmpty(name) || name == selectedReward.nameLocalizationKey)
                name = selectedReward.fallbackName ?? selectedReward.type.ToString();
            rewardNameText.text = name;
        }

        // Punch animation
        yield return PlayWinPunch();

        // Claim butonunu aç
        if (claimButton != null)
        {
            claimButton.gameObject.SetActive(true);
            claimButton.interactable = true;
        }
        if (spinButton != null) spinButton.gameObject.SetActive(false);

        isSpinning = false;
    }

    private IEnumerator PlayWinPunch()
    {
        if (reelIconImage == null) yield break;
        var rt = reelIconImage.rectTransform;
        Vector3 start = rt.localScale;
        Vector3 peak  = start * winPunchScale;

        float t = 0f;
        float half = winPunchDuration * 0.5f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / half);
            rt.localScale = Vector3.Lerp(start, peak, k);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / half);
            rt.localScale = Vector3.Lerp(peak, start, k);
            yield return null;
        }
        rt.localScale = start;
    }

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
        yield return FadePanel(panelGroup != null ? panelGroup.alpha : 1f, 0f);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private IEnumerator FadePanel(float from, float to)
    {
        if (panelGroup == null) yield break;
        panelGroup.alpha = from;
        float t = 0f;
        while (t < panelFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / panelFadeDuration));
            yield return null;
        }
        panelGroup.alpha = to;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Daily Spin")]
    private void ResetDailySpin()
    {
        PlayerPrefs.DeleteKey(KeyLastSpinDate);
        PlayerPrefs.Save();
        Debug.Log("[DailySlot] Daily spin reset.");
    }
#endif
}
