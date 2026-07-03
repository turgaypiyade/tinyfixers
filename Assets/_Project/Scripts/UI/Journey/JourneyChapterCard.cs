using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yolculuk bölüm kartı: büyük resim + başlık + "Bölüm X" + "İzle" butonu.
/// Önizleme modunda (sonraki bölüm) karartılır ve buton gizlenir.
/// </summary>
public sealed class JourneyChapterCard : MonoBehaviour
{
    [SerializeField] private Image frame;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text chapterText;
    [SerializeField] private Button watchButton;
    [SerializeField] private TMP_Text watchButtonText;
    [SerializeField] private Image dimOverlay;   // önizleme/kilit karartması

    private JourneyChapter chapter;
    private Action<JourneyChapter> onWatch;

    public void Bind(JourneyChapter c, UITheme theme, bool isPreview, Action<JourneyChapter> watchHandler)
    {
        chapter = c;
        onWatch = watchHandler;
        if (c == null) return;

        if (titleText != null)   titleText.text   = c.title;
        if (chapterText != null) chapterText.text = "Bölüm " + c.chapterNumber;
        if (image != null)
        {
            image.sprite  = c.image;
            image.enabled = c.image != null;
            image.color   = new Color(1, 1, 1, c.revealProgress); // tamir oranı = görünürlük
        }

        if (watchButton != null)
        {
            watchButton.gameObject.SetActive(!isPreview);
            watchButton.onClick.RemoveAllListeners();
            watchButton.onClick.AddListener(() => onWatch?.Invoke(chapter));
        }
        if (watchButtonText != null) watchButtonText.text = "İzle";

        if (dimOverlay != null)
        {
            dimOverlay.enabled = isPreview;
            if (theme != null) dimOverlay.color = new Color(0, 0, 0, isPreview ? 0.45f : 0f);
        }

        if (theme == null) return;
        UITheme.ApplySurface(frame, theme.cardBackground, theme.panelSurface);
        theme.ApplyText(titleText, theme.textLight, heading: true);
        theme.ApplyText(chapterText, theme.textLight, heading: true);
        theme.ApplyText(watchButtonText, theme.textLight, heading: true);
    }
}
