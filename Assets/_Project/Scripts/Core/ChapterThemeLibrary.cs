using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps chapter indices to ChapterTheme assets and computes the current chapter
/// from the global level stored in PlayerPrefs.
///
/// Create one shared asset via Assets → Create → TinyFixers → Chapter Theme Library.
/// </summary>
[CreateAssetMenu(fileName = "ChapterThemeLibrary",
                 menuName  = "TinyFixers/Chapter Theme Library",
                 order     = 11)]
public class ChapterThemeLibrary : ScriptableObject
{
    [Header("Progression")]
    [Tooltip("How many levels belong to each chapter. E.g. 10 → levels 1-10 = chapter 1, 11-20 = chapter 2.")]
    [Min(1)] public int levelsPerChapter = 10;

    [SerializeField] private string prefsLevelKey = "current_level";

    [Header("Themes (one per chapter, any order)")]
    [SerializeField] private List<ChapterTheme> themes = new();

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>Converts a 1-based global level number to a 1-based chapter number.</summary>
    public int GetChapterForLevel(int globalLevel)
    {
        int lv = Mathf.Max(1, globalLevel);
        int lpc = Mathf.Max(1, levelsPerChapter);
        return (lv - 1) / lpc + 1;
    }

    /// <summary>Returns the theme for a given 1-based chapter index.
    /// If no exact match, falls back to the last entry in the list.</summary>
    public ChapterTheme GetThemeForChapter(int chapter)
    {
        if (themes == null || themes.Count == 0) return null;

        for (int i = 0; i < themes.Count; i++)
        {
            if (themes[i] != null && themes[i].chapterIndex == chapter)
                return themes[i];
        }

        // Fallback: chapter wraps to the last available theme
        for (int i = themes.Count - 1; i >= 0; i--)
            if (themes[i] != null) return themes[i];

        return null;
    }

    /// <summary>Reads current_level from PlayerPrefs and returns the matching theme.</summary>
    public ChapterTheme GetCurrentTheme()
    {
        int level   = PlayerPrefs.GetInt(prefsLevelKey, 1);
        int chapter = GetChapterForLevel(level);
        return GetThemeForChapter(chapter);
    }

    /// <summary>Returns the current 1-based chapter number derived from PlayerPrefs.</summary>
    public int GetCurrentChapter()
    {
        return GetChapterForLevel(PlayerPrefs.GetInt(prefsLevelKey, 1));
    }
}
