using System.Collections.Generic;
using TMPro;
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

    public ChapterThemeLibrary ThemeLibrary => themeLibrary;
    public ChapterTheme CurrentTheme => themeLibrary != null ? themeLibrary.GetCurrentTheme() : null;

    [Header("Background")]
    [SerializeField] private Image backgroundImage;

    [Header("Top HUD")]
    [SerializeField] private Image topHudImage;

    [Header("Bottom Area")]
    [SerializeField] private Image bottomAreaImage;

    [Header("Main Menu extras")]
    [SerializeField] private Image levelSelectorBtnImage;
    [SerializeField] private Image repairBtnImage;

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
            MatchAspectFitter(backgroundImage);
            SetSprite(topHudImage,        theme.gameTopHud);
            SetSprite(bottomAreaImage,    theme.gameBottomArea);
        }
        else
        {
            SetSprite(backgroundImage,    theme.menuBackground);
            MatchAspectFitter(backgroundImage);
            SetSprite(topHudImage,        theme.menuTopHud);
            SetSprite(bottomAreaImage,    theme.menuBottomArea);
            SetSprite(levelSelectorBtnImage, theme.menuLevelSelectorBtn);
            SetSprite(repairBtnImage,        theme.menuRepairBtn);
        }

        ApplyBoardBorder(theme);
        ApplySuccessAnimation(theme);
        ApplySceneSwaps(theme);
    }

    // Özel tema (Usta Ligi): sahnede sabit atanmış görsel/yazı materyallerini tek seferde değiştirir
    // (alt bar butonları, buton çerçeveleri, sahnedeki popup zeminleri, başlık konturu). Bölüm temalarında liste boş → iş yok.
    // Sahne başına bir kez; tema sahne içinde değişmez (yeni seviye paketi ana menüye dönüşte sahneyle birlikte okunur).
    private bool sceneSwapsApplied;
    private static readonly List<Image> imageBuffer = new();
    private static readonly List<TMP_Text> textBuffer = new();
    private static readonly List<GameObject> rootBuffer = new();

    private void ApplySceneSwaps(ChapterTheme theme)
    {
        if (sceneSwapsApplied || !theme.HasSwaps) return;
        sceneSwapsApplied = true;

        rootBuffer.Clear();
        gameObject.scene.GetRootGameObjects(rootBuffer);
        foreach (var root in rootBuffer)
        {
            if (theme.spriteSwaps.Count > 0)
            {
                root.GetComponentsInChildren(true, imageBuffer);
                foreach (var img in imageBuffer)
                {
                    var swapped = theme.Swap(img.sprite);
                    if (swapped != img.sprite) img.sprite = swapped;
                }
            }
            if (theme.materialSwaps.Count > 0)
            {
                root.GetComponentsInChildren(true, textBuffer);
                foreach (var text in textBuffer)
                {
                    var swapped = theme.Swap(text.fontSharedMaterial);
                    if (swapped != text.fontSharedMaterial) text.fontSharedMaterial = swapped;
                }
            }
        }
        imageBuffer.Clear();
        textBuffer.Clear();
        rootBuffer.Clear();
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

    // Arka planın AspectRatioFitter'ı sahnede sabit (0.5625) yazılıydı: 9:16 olmayan yeni bir bölüm görseli
    // sessizce esnerdi. Oran her tema uygulanışında görselin KENDİSİNDEN alınır.
    private static void MatchAspectFitter(Image img)
    {
        if (img == null || img.sprite == null) return;
        if (!img.TryGetComponent<AspectRatioFitter>(out var fitter)) return;
        var r = img.sprite.rect;
        if (r.height > 0.5f) fitter.aspectRatio = r.width / r.height;
    }

    private static void SetSprite(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
    }
}
