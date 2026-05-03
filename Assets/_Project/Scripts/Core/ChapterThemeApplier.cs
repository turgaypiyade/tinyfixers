using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place this MonoBehaviour once per scene.
/// Assign only the Image slots that exist in the current scene — leave others null.
/// Applies the correct ChapterTheme on Start and whenever Apply() is called.
///
/// Game scene:   fill Background, TopHud, BottomArea, BoardBorderApplier.
/// Menu scene:   fill Background, TopHud, BottomArea, LevelSelectorBtn.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ChapterThemeApplier : MonoBehaviour
{
    [Header("Library (shared ScriptableObject)")]
    [SerializeField] private ChapterThemeLibrary themeLibrary;

    [Header("Background")]
    [SerializeField] private Image backgroundImage;

    [Header("Top HUD")]
    [SerializeField] private Image topHudImage;

    [Header("Bottom Area")]
    [SerializeField] private Image bottomAreaImage;

    [Header("Main Menu extras")]
    [SerializeField] private Image levelSelectorBtnImage;

    [Header("Board Border (game scene only)")]
    [SerializeField] private BoardBorderThemeApplier boardBorderApplier;

    [Header("Success Animation (game scene only)")]
    [SerializeField] private SuccessAnimationPlayer successAnimationPlayer;

    // ─────────────────────────────────────────────────────────────────

    private void Start()  => Apply();
    private void OnEnable() => Apply();

    // ─────────────────────────────────────────────────────────────────

    public void Apply()
    {
        if (themeLibrary == null) return;

        ChapterTheme theme = themeLibrary.GetCurrentTheme();
        if (theme == null) return;

        // Determine whether we're in the game scene or menu scene by which
        // Image slots are assigned, then apply the matching sprites.
        bool isGameScene = backgroundImage != null &&
                           (topHudImage != null || bottomAreaImage != null || boardBorderApplier != null) &&
                           levelSelectorBtnImage == null;

        if (isGameScene)
        {
            SetSprite(backgroundImage,    theme.gameBackground);
            SetSprite(topHudImage,        theme.gameTopHud);
            SetSprite(bottomAreaImage,    theme.gameBottomArea);
        }
        else
        {
            SetSprite(backgroundImage,    theme.menuBackground);
            SetSprite(topHudImage,        theme.menuTopHud);
            SetSprite(bottomAreaImage,    theme.menuBottomArea);
            SetSprite(levelSelectorBtnImage, theme.menuLevelSelectorBtn);
        }

        ApplyBoardBorder(theme);
        ApplySuccessAnimation(theme);
    }

    // ─────────────────────────────────────────────────────────────────

    private void ApplyBoardBorder(ChapterTheme theme)
    {
        if (boardBorderApplier == null) return;

        if (theme.borderSpriteLibrary != null)
            boardBorderApplier.BorderSpriteLibrary = theme.borderSpriteLibrary;

        boardBorderApplier.BorderColor = theme.borderColorId;
    }

    private void ApplySuccessAnimation(ChapterTheme theme)
    {
        if (successAnimationPlayer == null) return;
        successAnimationPlayer.SetBackground(theme.successAnimationBackground);
    }

    private static void SetSprite(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
    }
}
