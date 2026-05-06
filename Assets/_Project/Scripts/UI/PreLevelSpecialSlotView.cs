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
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text nameText;

    [Header("Localization")]
    [SerializeField] private string nameLocalizationKey;

    [Header("Motion")]
    [SerializeField] private float checkPopDuration = 0.16f;
    [SerializeField] private float iconPopScale = 1.06f;

    private ChapterTheme theme;
    private bool isSelected;
    private Coroutine selectionRoutine;

    public TileSpecial Special => special;
    public bool IsSelected => isSelected;

    public event Action<PreLevelSpecialSlotView> Clicked;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

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

    public void Configure(TileSpecial slotSpecial, Sprite icon, ChapterTheme chapterTheme)
    {
        special = slotSpecial;
        theme = chapterTheme;

        if (iconImage != null && icon != null)
            iconImage.sprite = icon;

        ApplyTheme(chapterTheme);
        RefreshCount();
        RefreshLocalizedTexts();
        SetSelected(false, false);
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

    private void HandleClick()
    {
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
        if (selectionTintImage != null)
        {
            Color color = Color.white;
            if (theme != null)
                color = isSelected ? theme.preLevelSlotSelectedTint : theme.preLevelSlotNormalTint;
            selectionTintImage.color = color;
        }

        if (checkMarkImage != null)
        {
            checkMarkImage.gameObject.SetActive(isSelected);
            if (!animate || !isSelected)
                checkMarkImage.rectTransform.localScale = Vector3.one;
        }

        if (!animate)
            return;

        if (selectionRoutine != null)
            StopCoroutine(selectionRoutine);

        selectionRoutine = StartCoroutine(CoSelectionPulse());
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
