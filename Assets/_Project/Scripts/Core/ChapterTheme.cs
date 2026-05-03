using UnityEngine;

/// <summary>
/// Holds all visual assets for one chapter.
/// Create one asset per chapter via Assets → Create → TinyFixers → Chapter Theme.
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
}
