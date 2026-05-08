using UnityEngine;

/// <summary>
/// Holds all visual assets for one chapter.
/// Create one asset per chapter via Assets  Create  TinyFixers  Chapter Theme.
/// </summary>
[CreateAssetMenu(fileName = "ChapterTheme_1",
                 menuName  = "TinyFixers/Chapter Theme",
                 order     = 10)]
public class ChapterTheme : ScriptableObject
{
    [Header("Chapter Index")]
    [Tooltip("1-based chapter number. Must match the LevelCatalog entries.")]
    [Min(1)] public int chapterIndex = 1;

    [Header("Game Screen (01_game)")]
    public Sprite gameBackground;
    public Sprite gameTopHud;
    public Sprite gameBottomArea;

    [Header("Main Menu Screen")]
    public Sprite menuBackground;
    public Sprite menuTopHud;
    public Sprite menuBottomArea;
    public Sprite menuLevelSelectorBtn;

    [Header("Board Border / Grid")]
    public BorderColorId    borderColorId      = BorderColorId.Orange;
    [Tooltip("Leave null to keep the border applier's existing library.")]
    public BorderSpriteLibrary borderSpriteLibrary;

    [Header("Success Animation")]
    [Tooltip("Background shown behind the success frame animation. Leave null to hide the background.")]
    public Sprite successAnimationBackground;

    [Header("Pre-Level Special Popup")]
    [Tooltip("Chapter-specific popup panel/background image shown before entering a level.")]
    public Sprite preLevelPopupBackground;
    [Tooltip("Optional chapter-specific Continue button sprite.")]
    public Sprite preLevelContinueButton;
    [Tooltip("Optional chapter-specific Cancel icon/button sprite. This button may be icon-only.")]
    public Sprite preLevelCancelButton;
    [Tooltip("Check mark sprite shown on selected pre-level special slots.")]
    public Sprite preLevelCheckMark;

    [Header("Pre-Level Popup Colors")]
    public Color preLevelDimColor = new Color(0f, 0f, 0f, 0.55f);
    public Color preLevelSlotNormalTint = new Color(1f, 1f, 1f, 0.18f);
    public Color preLevelSlotSelectedTint = new Color(1f, 0.86f, 0.25f, 0.72f);
    public Color preLevelCountTextColor = Color.white;
    public Color preLevelButtonTextColor = Color.white;

    [Header("Level End Popup")]
    [Tooltip("Chapter-specific shared popup panel/background image for fail and success end popups.")]
    public Sprite levelEndPopupBackground;
    [Tooltip("Optional chapter-specific Continue button sprite for level end popups.")]
    public Sprite levelEndContinueButton;
    [Tooltip("Optional chapter-specific Close button sprite for level end popups.")]
    public Sprite levelEndCloseButton;
    [Tooltip("Filled star sprite used on the success popup.")]
    public Sprite levelEndStarFilled;
    [Tooltip("Empty star sprite used on the success popup.")]
    public Sprite levelEndStarEmpty;
    [Tooltip("Check mark sprite shown near completed goals on the success popup.")]
    public Sprite levelEndGoalCheckMark;
    [Tooltip("Extra moves offer icon shown on the fail popup for +5 moves.")]
    public Sprite levelEndExtraMoves5;
    [Tooltip("Extra moves offer icon shown on the fail popup for +10 moves.")]
    public Sprite levelEndExtraMoves10;
    [Tooltip("Extra moves offer icon shown on the fail popup for +15 moves.")]
    public Sprite levelEndExtraMoves15;
}
