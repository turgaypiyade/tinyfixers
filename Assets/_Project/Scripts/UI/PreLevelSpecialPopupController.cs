using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PreLevelSpecialPopupController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string gameSceneName = "01_Game";
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField] private ChapterThemeApplier chapterThemeApplier;
    [SerializeField] private ChapterThemeLibrary fallbackThemeLibrary;

    [Header("Main Screen Dim")]
    [SerializeField] private CanvasGroup mainScreenCanvasGroup;
    [SerializeField] private Image dimImage;
    [SerializeField, Range(0f, 1f)] private float dimmedMainAlpha = 0.45f;

    [Header("Popup Root")]
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private Image popupBackgroundImage;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text continueText;
    [SerializeField] private TMP_Text cancelText;
    [SerializeField] private string titleLocalizationKey = "prelevel_popup_title_level";
    [SerializeField] private string continueLocalizationKey = "prelevel_popup_continue";
    [SerializeField] private string cancelLocalizationKey = "prelevel_popup_cancel";

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Image continueButtonImage;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Image cancelButtonImage;

    [Header("Goal Preview")]
    [SerializeField] private LevelCatalog levelCatalog;
    [Tooltip("Used only if no ChapterThemeLibrary is assigned. Normally chapter is calculated automatically from current_level.")]
    [SerializeField, Min(1)] private int fallbackChapter = 1;
    [SerializeField] private Transform goalsPreviewRoot;
    [SerializeField] private TopHudGoalSlot goalSlotPrefab;
    [SerializeField, Min(1)] private int maxPreviewGoals = 4;
    [SerializeField, Min(0.1f)] private float goalPreviewScale = 2f;
    [SerializeField] private Sprite fallbackGoalIcon;

    [Header("Special Slots")]
    [SerializeField] private TileIconLibrary tileIconLibrary;
    [SerializeField] private PreLevelSpecialSlotView lineHSlot;
    [SerializeField] private PreLevelSpecialSlotView pulseCoreSlot;
    [SerializeField] private PreLevelSpecialSlotView overrideSlot;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip selectSfx;
    [SerializeField] private AudioClip continueSfx;
    [SerializeField] private AudioClip cancelSfx;

    [Header("Motion")]
    [SerializeField] private float openDuration = 0.22f;
    [SerializeField] private float closeDuration = 0.16f;
    [SerializeField] private Vector3 openStartScale = new Vector3(0.92f, 0.92f, 1f);

    private readonly List<PreLevelSpecialSlotView> slots = new();
    private Coroutine transitionRoutine;
    private ChapterTheme currentTheme;

    private void Awake()
    {
        if (popupCanvasGroup == null)
            popupCanvasGroup = GetComponent<CanvasGroup>();

        if (popupRoot == null)
            popupRoot = transform as RectTransform;

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelClicked);

        RegisterSlot(lineHSlot);
        RegisterSlot(pulseCoreSlot);
        RegisterSlot(overrideSlot);

        SetPopupVisible(false, true);
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

    public void Open()
    {
        currentTheme = ResolveTheme();
        ApplyTheme(currentTheme);
        ConfigureSlots();
        RefreshLocalizedTexts();
        RefreshCounts();
        RefreshGoalsPreview();
        PreLevelSpecialSelectionState.Clear();

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(CoOpen());
    }

    public void ApplyTheme(ChapterTheme theme)
    {
        currentTheme = theme;

        if (popupBackgroundImage != null && theme != null && theme.preLevelPopupBackground != null)
            popupBackgroundImage.sprite = theme.preLevelPopupBackground;

        if (continueButtonImage != null && theme != null && theme.preLevelContinueButton != null)
            continueButtonImage.sprite = theme.preLevelContinueButton;

        if (cancelButtonImage != null && theme != null && theme.preLevelCancelButton != null)
            cancelButtonImage.sprite = theme.preLevelCancelButton;

        if (continueText != null && theme != null)
            continueText.color = theme.preLevelButtonTextColor;

        if (cancelText != null && theme != null)
            cancelText.color = theme.preLevelButtonTextColor;

        for (int i = 0; i < slots.Count; i++)
            slots[i].ApplyTheme(theme);

        if (dimImage != null && theme != null)
            dimImage.color = theme.preLevelDimColor;
    }

    private void RegisterSlot(PreLevelSpecialSlotView slot)
    {
        if (slot == null || slots.Contains(slot))
            return;

        slots.Add(slot);
        slot.Clicked += HandleSlotClicked;
    }

    private void ConfigureSlots()
    {
        if (tileIconLibrary == null)
            return;

        if (lineHSlot != null)
            lineHSlot.Configure(TileSpecial.LineH, tileIconLibrary.GetSpecialIcon(TileSpecial.LineH), currentTheme);

        if (pulseCoreSlot != null)
            pulseCoreSlot.Configure(TileSpecial.PulseCore, tileIconLibrary.GetSpecialIcon(TileSpecial.PulseCore), currentTheme);

        if (overrideSlot != null)
            overrideSlot.Configure(TileSpecial.SystemOverride, tileIconLibrary.GetSpecialIcon(TileSpecial.SystemOverride), currentTheme);
    }

    private void RefreshGoalsPreview()
    {
        ClearGoalsPreview();

        if (goalsPreviewRoot == null || goalSlotPrefab == null)
            return;

        LevelData levelData = ResolvePreviewLevelData();
        if (levelData == null || levelData.goals == null)
            return;

        int spawned = 0;
        int limit = Mathf.Max(1, maxPreviewGoals);

        for (int i = 0; i < levelData.goals.Length && spawned < limit; i++)
        {
            LevelGoalDefinition goal = levelData.goals[i];
            if (goal == null || goal.amount <= 0)
                continue;

            Sprite goalIcon = ResolvePreviewGoalIcon(levelData, goal);

            // Icon yoksa slot basma.
            // Böylece ikon görünmeyip sadece sayı görünmesi engellenir.
            if (goalIcon == null)
            {
                Debug.LogWarning(
                    $"[PreLevelSpecialPopup] Goal preview skipped because icon is missing. index={i}, targetType={goal.targetType}");
                continue;
            }

            TopHudGoalSlot slot = Instantiate(goalSlotPrefab, goalsPreviewRoot);
            slot.Setup(goalIcon, goal.amount, ShouldUseLargeGoalIcon(goal));
            ApplyGoalPreviewScale(slot);

            spawned++;
        }

        if (goalsPreviewRoot is RectTransform rootRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
    }
    private void ApplyGoalPreviewScale(TopHudGoalSlot slot)
    {
        if (slot == null)
            return;

        float scale = Mathf.Max(0.1f, goalPreviewScale);

        if (slot.transform is RectTransform rt)
        {
            LayoutElement slotLayoutElement = slot.GetComponent<LayoutElement>();
            float baseWidth = ResolveLayoutSize(slotLayoutElement != null ? slotLayoutElement.preferredWidth : -1f, rt.rect.width, rt.sizeDelta.x);
            float baseHeight = ResolveLayoutSize(slotLayoutElement != null ? slotLayoutElement.preferredHeight : -1f, rt.rect.height, rt.sizeDelta.y);
            WrapGoalPreviewSlotForLayout(slot, rt, baseWidth, baseHeight, scale);
            rt.localScale = Vector3.one * scale;
        }
        else
        {
            slot.transform.localScale = Vector3.one * scale;
        }
    }

    private void WrapGoalPreviewSlotForLayout(TopHudGoalSlot slot, RectTransform slotRt, float baseWidth, float baseHeight, float scale)
    {
        if (slot == null || slotRt == null || goalsPreviewRoot == null)
            return;

        int siblingIndex = slotRt.GetSiblingIndex();
        var wrapper = new GameObject("GoalPreviewSlotLayout", typeof(RectTransform), typeof(LayoutElement));
        var wrapperRt = wrapper.GetComponent<RectTransform>();
        wrapperRt.SetParent(goalsPreviewRoot, false);
        wrapperRt.SetSiblingIndex(siblingIndex);
        wrapperRt.anchorMin = new Vector2(0.5f, 0.5f);
        wrapperRt.anchorMax = new Vector2(0.5f, 0.5f);
        wrapperRt.pivot = new Vector2(0.5f, 0.5f);
        wrapperRt.anchoredPosition = Vector2.zero;
        wrapperRt.localScale = Vector3.one;

        var wrapperLayout = wrapper.GetComponent<LayoutElement>();
        wrapperLayout.preferredWidth = baseWidth > 0f ? baseWidth * scale : -1f;
        wrapperLayout.preferredHeight = baseHeight > 0f ? baseHeight * scale : -1f;

        slotRt.SetParent(wrapperRt, false);
        slotRt.anchorMin = new Vector2(0.5f, 0.5f);
        slotRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.anchoredPosition = Vector2.zero;
        if (baseWidth > 0f)
            slotRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, baseWidth);

        if (baseHeight > 0f)
            slotRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, baseHeight);

        LayoutElement slotLayout = slot.GetComponent<LayoutElement>();
        if (slotLayout != null)
            slotLayout.ignoreLayout = true;
    }

    private static float ResolveLayoutSize(float preferred, float rectSize, float sizeDelta)
    {
        if (preferred > 0f)
            return preferred;

        if (rectSize > 0f)
            return rectSize;

        if (sizeDelta > 0f)
            return sizeDelta;

        return -1f;
    }

    private void ClearGoalsPreview()
    {
        if (goalsPreviewRoot == null)
            return;

        for (int i = goalsPreviewRoot.childCount - 1; i >= 0; i--)
            Destroy(goalsPreviewRoot.GetChild(i).gameObject);
    }

    private LevelData ResolvePreviewLevelData()
    {
        if (levelCatalog == null)
            return null;

        int level = Mathf.Max(1, PlayerPrefs.GetInt(prefsLevelKey, 1));
        int previewChapter = ResolvePreviewChapter(level);
        return levelCatalog.TryGetLevel(previewChapter, level, out LevelData levelData) ? levelData : null;
    }

    private int ResolvePreviewChapter(int globalLevel)
    {
        ChapterThemeLibrary library = ResolveThemeLibrary();
        return library != null ? library.GetChapterForLevel(globalLevel) : Mathf.Max(1, fallbackChapter);
    }

    private ChapterThemeLibrary ResolveThemeLibrary()
    {
        if (chapterThemeApplier != null && chapterThemeApplier.ThemeLibrary != null)
            return chapterThemeApplier.ThemeLibrary;

        return fallbackThemeLibrary;
    }

    private Sprite ResolvePreviewGoalIcon(LevelData levelData, LevelGoalDefinition goal)
    {
        if (goal == null)
            return fallbackGoalIcon;

        if (goal.iconOverride != null)
            return goal.iconOverride;

        if (goal.targetType == LevelGoalTargetType.Tile)
        {
            Sprite sprite = tileIconLibrary != null ? tileIconLibrary.Get(goal.tileType) : null;
            return sprite != null ? sprite : fallbackGoalIcon;
        }

        if (goal.targetType == LevelGoalTargetType.Obstacle)
        {
            var obstacleDef = levelData != null && levelData.obstacleLibrary != null
                ? levelData.obstacleLibrary.Get(goal.obstacleId)
                : null;

            Sprite preview = obstacleDef != null ? obstacleDef.GetPreviewSprite() : null;
            return preview != null ? preview : fallbackGoalIcon;
        }

        return fallbackGoalIcon;
    }

    private static bool ShouldUseLargeGoalIcon(LevelGoalDefinition goal)
    {
        return goal != null
               && goal.targetType == LevelGoalTargetType.Collectible
               && goal.collectibleId == CollectibleId.EnergyOrb;
    }

    private ChapterTheme ResolveTheme()
    {
        if (chapterThemeApplier != null && chapterThemeApplier.CurrentTheme != null)
            return chapterThemeApplier.CurrentTheme;

        if (fallbackThemeLibrary != null)
            return fallbackThemeLibrary.GetCurrentTheme();

        return null;
    }

    private void HandleSlotClicked(PreLevelSpecialSlotView slot)
    {
        if (slot == null)
            return;

        slot.ToggleSelected();
        PlayOneShot(selectSfx);
    }

    private void HandleContinueClicked()
    {
        var selected = new List<TileSpecial>();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.IsSelected)
                continue;

            selected.Add(slot.Special);
            PreLevelSpecialInventory.Spend(slot.Special, 1);
        }

        Debug.Log($"[PreLevelSpecialPopup] Continue selected={selected.Count}");
        PreLevelSpecialSelectionState.SetSelection(selected);
        PreLevelSpecialRuntimeInjector.EnsureForSelection(selected);
        RefreshCounts();
        PlayOneShot(continueSfx);

        var library = ResolveThemeLibrary();
        LoadingScreenManager.Show(library != null ? library.GetRandomLoadingImage() : null);
        SceneManager.LoadScene(gameSceneName);
    }

    private void HandleCancelClicked()
    {
        PreLevelSpecialSelectionState.Clear();
        PlayOneShot(cancelSfx);

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(CoClose());
    }

    private void RefreshLocalizedTexts()
    {
        if (titleText != null)
        {
            int level = PlayerPrefs.GetInt(prefsLevelKey, 1);
            titleText.text = GameLocalization.GetFormat(titleLocalizationKey, level);
        }

        if (continueText != null)
            continueText.text = GameLocalization.Get(continueLocalizationKey);

        if (cancelText != null)
            cancelText.text = GameLocalization.Get(cancelLocalizationKey);
    }

    private void RefreshCounts()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].RefreshCount();
        }
    }

    private IEnumerator CoOpen()
    {
        SetPopupVisible(true, false);
        PlayOneShot(openSfx);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOut(t);

            if (mainScreenCanvasGroup != null)
                mainScreenCanvasGroup.alpha = Mathf.Lerp(1f, dimmedMainAlpha, eased);

            if (popupCanvasGroup != null)
                popupCanvasGroup.alpha = eased;

            if (popupRoot != null)
                popupRoot.localScale = Vector3.LerpUnclamped(openStartScale, Vector3.one, EaseOutBackLight(t));

            yield return null;
        }

        if (mainScreenCanvasGroup != null)
            mainScreenCanvasGroup.alpha = dimmedMainAlpha;

        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 1f;
            popupCanvasGroup.interactable = true;
            popupCanvasGroup.blocksRaycasts = true;
        }

        if (popupRoot != null)
            popupRoot.localScale = Vector3.one;

        transitionRoutine = null;
    }

    private IEnumerator CoClose()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, closeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOut(t);

            if (mainScreenCanvasGroup != null)
                mainScreenCanvasGroup.alpha = Mathf.Lerp(dimmedMainAlpha, 1f, eased);

            if (popupCanvasGroup != null)
                popupCanvasGroup.alpha = 1f - eased;

            if (popupRoot != null)
                popupRoot.localScale = Vector3.LerpUnclamped(Vector3.one, openStartScale, eased);

            yield return null;
        }

        if (mainScreenCanvasGroup != null)
            mainScreenCanvasGroup.alpha = 1f;

        SetPopupVisible(false, true);
        transitionRoutine = null;
    }

    private void SetPopupVisible(bool visible, bool instant)
    {
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = visible ? 1f : 0f;
            popupCanvasGroup.interactable = visible;
            popupCanvasGroup.blocksRaycasts = visible;
        }

        if (popupRoot != null)
        {
            popupRoot.gameObject.SetActive(visible);
            popupRoot.localScale = instant ? Vector3.one : popupRoot.localScale;
        }

        if (!visible && mainScreenCanvasGroup != null)
            mainScreenCanvasGroup.alpha = 1f;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float EaseOutBackLight(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.12f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
