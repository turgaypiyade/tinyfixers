using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prefab-backed loading hint view.
/// Assign references in the prefab, or keep the default child names:
/// HintImage, TitleText, DescText, LoadingText.
/// Visual styling is owned by the prefab. This script only swaps content.
/// </summary>
public class LoadingHintView : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private Image hintImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text loadingText;

    [Header("Optional Panels")]
    [SerializeField] private Image titlePanelImage;
    [SerializeField] private Image descriptionPanelImage;
    [SerializeField] private Image bottomPanelImage;

    public Image HintImage => hintImage;
    public TMP_Text TitleText => titleText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text LoadingText => loadingText;

    private void Awake()
    {
        ResolveReferencesIfMissing();
    }

    private void OnValidate()
    {
        ResolveReferencesIfMissing();
    }

    public void Apply(Sprite sprite)
    {
        ResolveReferencesIfMissing();

        if (hintImage != null)
        {
            hintImage.sprite = sprite;
            hintImage.enabled = true;
            hintImage.color = Color.white;
            // Tam ekran chapter görseli: esneme YOK, aspect koruyarak ekrana sığdır (kırpma yok).
            LoadingScreenManager.ApplyFitAspect(hintImage, sprite);
        }

        SetText(titleText, string.Empty);
        SetText(descriptionText, string.Empty);
        SetText(loadingText, string.Empty);

        SetPanelVisible(titlePanelImage, false);
        SetPanelVisible(descriptionPanelImage, false);
        SetPanelVisible(bottomPanelImage, false);
    }

    public void Apply(LoadingHintEntry hint)
    {
        ResolveReferencesIfMissing();

        if (hintImage != null)
        {
            hintImage.sprite = hint != null ? hint.image : null;
            hintImage.enabled = hintImage.sprite != null;
            hintImage.color = Color.white;
            // ChapterTheme.loadingHints görselleri: esneme YOK, aspect koruyarak ekrana sığdır.
            if (hintImage.sprite != null)
                LoadingScreenManager.ApplyFitAspect(hintImage, hintImage.sprite);
        }

        string title = hint != null ? GameLocalization.Get(hint.titleLocalizationKey) : string.Empty;
        string description = hint != null ? GameLocalization.Get(hint.descriptionLocalizationKey) : string.Empty;
        string loadingKey = hint != null && !string.IsNullOrWhiteSpace(hint.loadingLocalizationKey)
            ? hint.loadingLocalizationKey
            : "loading_hint_loading";
        string loading = GameLocalization.Get(loadingKey);

        SetText(titleText, title);
        SetText(descriptionText, description);
        SetText(loadingText, loading);

        SetPanelVisible(titlePanelImage, !string.IsNullOrWhiteSpace(title));
        SetPanelVisible(descriptionPanelImage, !string.IsNullOrWhiteSpace(description));
        SetPanelVisible(bottomPanelImage, !string.IsNullOrWhiteSpace(loading));
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.text = value ?? string.Empty;
        target.gameObject.SetActive(!string.IsNullOrWhiteSpace(target.text));
    }

    private static void SetPanelVisible(Image panel, bool visible)
    {
        if (panel != null)
            panel.gameObject.SetActive(visible);
    }

    private void ResolveReferencesIfMissing()
    {
        if (hintImage == null)
            hintImage = FindImage("HintImage");
        if (titlePanelImage == null)
            titlePanelImage = FindImage("TitlePanelImage");
        if (descriptionPanelImage == null)
            descriptionPanelImage = FindImage("DescPanelImage");
        if (bottomPanelImage == null)
            bottomPanelImage = FindImage("BottomPanelImage");

        if (titleText == null)
            titleText = FindText("TitleText");
        if (descriptionText == null)
            descriptionText = FindText("DescText");
        if (loadingText == null)
            loadingText = FindText("LoadingText");
    }

    private Image FindImage(string childName)
    {
        Transform child = FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
