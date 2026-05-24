using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FortuneWheelController : MonoBehaviour
{
    private const string KeyLastSpinDate = "fortune_wheel_last_spin_date";
    private const string DateFormat = "yyyy-MM-dd";

    [Header("Config")]
    [SerializeField] private DailySlotRewardConfig rewardConfig;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Button closeButton;

    [Header("Wheel")]
    [SerializeField] private RectTransform innerWheel;
    [SerializeField] private float iconRadius = 190f;
    [SerializeField] private float iconSize = 80f;

    [Header("UI")]
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button claimButton;
    [SerializeField] private GameObject noSpinAvailableLabel;

    [Header("Win Feedback")]
    [SerializeField, Range(1f, 2f)] private float winPunchScale = 1.2f;
    [SerializeField] private float winPunchDuration = 0.3f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip spinSfx;
    [SerializeField] private AudioClip winSfx;

    [Header("Spin")]
    [SerializeField] private int fullRotations = 4;
    [SerializeField] private float spinDuration = 3.5f;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.2f;

    private DailySlotReward selectedReward;
    private int selectedIndex;
    private bool isSpinning;
    private bool rewardClaimed;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (spinButton  != null) spinButton.onClick.AddListener(OnSpinClicked);
        if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
        if (panelRoot   != null) panelRoot.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public static bool HasAvailableSpin()
    {
        return PlayerPrefs.GetString(KeyLastSpinDate, "") != DateTime.Now.ToString(DateFormat);
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

        PlaceIcons();

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

            var go  = new GameObject($"Icon_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(innerWheel, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            float angleCW  = i * segmentAngle + segmentAngle * 0.5f;
            float rad      = angleCW * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(
                iconRadius * Mathf.Sin(rad),
                iconRadius * Mathf.Cos(rad)
            );

            var img = go.GetComponent<Image>();
            img.sprite = reward.icon;
            img.preserveAspect = true;
        }
    }

    // ── Spin ─────────────────────────────────────────────────────────────────

    private void OnSpinClicked()
    {
        if (isSpinning) return;
        if (!HasAvailableSpin()) return;
        if (rewardConfig == null || rewardConfig.rewards.Count == 0) return;

        selectedReward = rewardConfig.PickRandom();
        if (selectedReward == null) return;
        selectedIndex = rewardConfig.rewards.IndexOf(selectedReward);

        PlayerPrefs.SetString(KeyLastSpinDate, DateTime.Now.ToString(DateFormat));
        PlayerPrefs.Save();

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        if (spinButton   != null) spinButton.interactable = false;
        if (rewardNameText != null) rewardNameText.text  = "";
        if (sfxSource != null && spinSfx != null) sfxSource.PlayOneShot(spinSfx);

        int   count        = Mathf.Min(rewardConfig.rewards.Count, 8);
        float segmentAngle = 360f / count;
        float landingOffset = UnityEngine.Random.Range(-segmentAngle * 0.25f, segmentAngle * 0.25f);
        float targetCW = fullRotations * 360f
                       + selectedIndex * segmentAngle
                       + segmentAngle * 0.5f
                       + landingOffset;

        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / spinDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // cubic ease-out
            innerWheel.localEulerAngles = new Vector3(0f, 0f, -Mathf.Lerp(0f, targetCW, eased));
            yield return null;
        }

        innerWheel.localEulerAngles = new Vector3(0f, 0f, -targetCW);

        if (sfxSource != null && winSfx != null) sfxSource.PlayOneShot(winSfx);

        if (rewardNameText != null)
        {
            string name = !string.IsNullOrEmpty(selectedReward.nameLocalizationKey)
                ? GameLocalization.Get(selectedReward.nameLocalizationKey) : null;
            if (string.IsNullOrEmpty(name) || name == selectedReward.nameLocalizationKey)
                name = selectedReward.fallbackName ?? selectedReward.type.ToString();
            rewardNameText.text = name;
        }

        yield return PlayWinPunch();

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
        PlayerPrefs.DeleteKey(KeyLastSpinDate);
        PlayerPrefs.Save();
        Debug.Log("[FortuneWheel] Daily spin reset.");
    }
#endif
}
