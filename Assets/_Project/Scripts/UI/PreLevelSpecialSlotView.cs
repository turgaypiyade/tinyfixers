using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreLevelSpecialSlotView : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private TileSpecial special = TileSpecial.LineH;

    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image selectionTintImage;
    [SerializeField] private Image checkMarkImage;
    [SerializeField] private Image numberBG;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text nameText;

    [Header("Slot Background Sprites")]
    [SerializeField] private Sprite normalBackgroundSprite;
    [SerializeField] private Sprite selectedBackgroundSprite;
    [SerializeField] private bool hideCountWhenSelected = true;

    [Header("Lock State")]
    [Tooltip("İlk 10 level boyunca kilitli olan durum için gösterilecek görsel Obje")]
    [SerializeField] private GameObject lockOverlay;

    [Header("Timed Reward Overlay")]
    [Tooltip("Süreli ödül aktifken gösterilecek overlay container.")]
    [SerializeField] private GameObject timedOverlay;
    [Tooltip("Süreli ödülün wave/reward ikonunu gösterir. Sprite runtime'da atanır.")]
    [SerializeField] private Image timedWaveImage;
    [Tooltip("Süreli ödülün geri sayım metni.")]
    [SerializeField] private TMP_Text timedCountdownText;

    [Header("Localization")]
    [SerializeField] private string nameLocalizationKey;

    [Header("Motion")]
    [SerializeField] private float checkPopDuration = 0.16f;
    [SerializeField] private float iconPopScale = 1.06f;

    private ChapterTheme theme;
    private bool isSelected;
    private bool isUnlocked = true;
    private Coroutine selectionRoutine;

    public TileSpecial Special => special;
    public bool IsSelected => isSelected;
    public bool IsUnlocked => isUnlocked;
    public bool IsTimedActive => TimedRewardService.IsSpecialFree(special);

    public event Action<PreLevelSpecialSlotView> Clicked;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (numberBG == null)
        {
            Transform numberBgTransform = transform.Find("NumberBG");
            if (numberBgTransform != null)
                numberBG = numberBgTransform.GetComponent<Image>();
        }

        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += RefreshLocalizedTexts;
        RefreshLocalizedTexts();
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= RefreshLocalizedTexts;
    }

    private void Update()
    {
        if (timedCountdownText == null) return;
        if (!TimedRewardService.IsSpecialFree(special)) return;

        timedCountdownText.text = FormatTimeSpan(GetTimedRemaining());
    }

    public void Configure(TileSpecial slotSpecial, Sprite icon, ChapterTheme chapterTheme, bool unlocked = true)
    {
        special = slotSpecial;
        theme = chapterTheme;
        isUnlocked = unlocked;

        if (iconImage != null && iconImage.sprite == null && icon != null)
            iconImage.sprite = icon;

        ApplyTheme(chapterTheme);
        RefreshCount();
        RefreshLocalizedTexts();
        SetSelected(false, false);
        ApplyLockState();
        ApplyTimedState();
    }

    private void ApplyLockState()
    {
        if (lockOverlay != null)
            lockOverlay.SetActive(!isUnlocked);
            
        if (button != null)
        {
            // Eğer kilitliyse buton tıklanamaz
            if (!isUnlocked)
                button.interactable = false;
        }
        
        if (!isUnlocked)
        {
            if (countText != null) countText.gameObject.SetActive(false);
            if (numberBG != null) numberBG.gameObject.SetActive(false);
        }
    }

    public void ApplyTheme(ChapterTheme chapterTheme)
    {
        theme = chapterTheme;

        if (checkMarkImage != null && theme != null && theme.preLevelCheckMark != null)
            checkMarkImage.sprite = theme.preLevelCheckMark;

        if (countText != null && theme != null)
            countText.color = theme.preLevelCountTextColor;

        ApplySelectionVisual(false);
    }

    public void RefreshCount()
    {
        if (countText == null)
            return;

        int count = PreLevelSpecialInventory.GetCount(special);
        countText.text = GameLocalization.GetFormat("prelevel_special_count_format", count);
    }

    public void SetSelected(bool selected, bool animate = true)
    {
        if (isSelected == selected && animate)
            return;

        isSelected = selected;
        ApplySelectionVisual(animate);
    }

    public void ToggleSelected()
    {
        SetSelected(!isSelected, true);
    }

    // ── Timed Reward ──────────────────────────────────────────────────────────

    private void ApplyTimedState()
    {
        bool isFree = TimedRewardService.IsSpecialFree(special);

        if (timedOverlay != null)
            timedOverlay.SetActive(isFree);

        if (button != null)
            button.interactable = !isFree;

        if (numberBG != null)
            numberBG.gameObject.SetActive(!isFree);

        if (countText != null)
            countText.gameObject.SetActive(!isFree);

        if (timedWaveImage != null)
        {
            Sprite waveSprite = isFree ? FindTimedWaveSprite() : null;
            timedWaveImage.sprite = waveSprite;
            timedWaveImage.enabled = waveSprite != null;
        }

        if (isFree && timedCountdownText != null)
            timedCountdownText.text = FormatTimeSpan(GetTimedRemaining());
    }

    private Sprite FindTimedWaveSprite()
    {
        var service = ProgressEventService.Instance;
        if (service == null) return null;

        foreach (var goal in service.Goals)
        {
            var def = goal.Definition;
            if (def.reward == null || def.rewardDurationMinutes <= 0 || def.rewardWaveSprite == null) continue;
            if (IsMatchingRewardType(def.reward.type)) return def.rewardWaveSprite;
        }
        return null;
    }

    private bool IsMatchingRewardType(DailySlotRewardType type) => special switch
    {
        TileSpecial.LineH          => type == DailySlotRewardType.Joker_Line || type == DailySlotRewardType.Joker_LineH,
        TileSpecial.LineV          => type == DailySlotRewardType.Joker_Line,
        TileSpecial.PulseCore      => type == DailySlotRewardType.Joker_PulseCore,
        TileSpecial.SystemOverride => type == DailySlotRewardType.Joker_SystemOverride,
        _                          => false
    };

    private System.TimeSpan GetTimedRemaining()
    {
        switch (special)
        {
            case TileSpecial.LineH:
                if (TimedRewardService.IsActive(DailySlotRewardType.Joker_LineH))
                {
                    var a = TimedRewardService.GetRemaining(DailySlotRewardType.Joker_LineH);
                    var b = TimedRewardService.GetRemaining(DailySlotRewardType.Joker_Line);
                    return a > b ? a : b;
                }
                return TimedRewardService.GetRemaining(DailySlotRewardType.Joker_Line);
            case TileSpecial.LineV:
                return TimedRewardService.GetRemaining(DailySlotRewardType.Joker_Line);
            case TileSpecial.PulseCore:
                return TimedRewardService.GetRemaining(DailySlotRewardType.Joker_PulseCore);
            case TileSpecial.SystemOverride:
                return TimedRewardService.GetRemaining(DailySlotRewardType.Joker_SystemOverride);
            default:
                return System.TimeSpan.Zero;
        }
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

    // ─────────────────────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (!isUnlocked) return;
        
        // Kilitli değil ama elimizde hiç joker kalmadıysa seçime izin verme
        if (PreLevelSpecialInventory.GetCount(special) <= 0 && !IsTimedActive && !isSelected)
            return; // Sınırsız kullanımı durdur!
            
        Clicked?.Invoke(this);
    }

    private void RefreshLocalizedTexts()
    {
        if (nameText == null || string.IsNullOrEmpty(nameLocalizationKey))
            return;

        nameText.text = GameLocalization.Get(nameLocalizationKey);
    }

    private void ApplySelectionVisual(bool animate)
    {
        ApplyBackgroundVisual();
        ApplyCountVisual();
        ApplyCheckVisual(animate);

        if (!animate)
            return;

        if (selectionRoutine != null)
            StopCoroutine(selectionRoutine);

        selectionRoutine = StartCoroutine(CoSelectionPulse());
    }

    private void ApplyBackgroundVisual()
    {
        if (selectionTintImage == null)
            return;

        Sprite targetSprite = isSelected ? selectedBackgroundSprite : normalBackgroundSprite;
        if (targetSprite != null)
        {
            selectionTintImage.sprite = targetSprite;
            selectionTintImage.color = Color.white;
            selectionTintImage.enabled = true;
            return;
        }

        Color color = Color.white;
        if (theme != null)
            color = isSelected ? theme.preLevelSlotSelectedTint : theme.preLevelSlotNormalTint;

        selectionTintImage.color = color;
        selectionTintImage.enabled = color.a > 0f;
    }

    private void ApplyCountVisual()
    {
        bool visible = !isSelected || !hideCountWhenSelected;

        if (countText != null)
            countText.gameObject.SetActive(visible);

        if (numberBG != null)
            numberBG.gameObject.SetActive(visible);
    }

    private void ApplyCheckVisual(bool animate)
    {
        if (checkMarkImage == null)
            return;

        checkMarkImage.gameObject.SetActive(isSelected);
        if (!animate || !isSelected)
            checkMarkImage.rectTransform.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator CoSelectionPulse()
    {
        RectTransform checkRt = checkMarkImage != null ? checkMarkImage.rectTransform : null;
        RectTransform iconRt = iconImage != null ? iconImage.rectTransform : null;

        Vector3 checkBase = Vector3.one;
        Vector3 iconBase = iconRt != null ? iconRt.localScale : Vector3.one;

        float duration = Mathf.Max(0.01f, checkPopDuration);
        float elapsed = 0f;

        if (checkRt != null && isSelected)
            checkRt.localScale = Vector3.one * 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float checkScale;
            if (t < 0.65f)
            {
                float k = t / 0.65f;
                checkScale = Mathf.LerpUnclamped(0.2f, 1.15f, EaseOutBackLight(k));
            }
            else
            {
                float k = (t - 0.65f) / 0.35f;
                checkScale = Mathf.LerpUnclamped(1.15f, 1f, EaseOut(k));
            }

            float iconScale = Mathf.LerpUnclamped(iconPopScale, 1f, EaseOut(t));

            if (checkRt != null && isSelected)
                checkRt.localScale = checkBase * checkScale;

            if (iconRt != null)
                iconRt.localScale = iconBase * iconScale;

            yield return null;
        }

        if (checkRt != null)
            checkRt.localScale = Vector3.one;

        if (iconRt != null)
            iconRt.localScale = iconBase;

        selectionRoutine = null;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseOutBackLight(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.25f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
