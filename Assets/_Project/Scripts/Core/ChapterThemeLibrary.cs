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

    [Header("Ortak arayüz (tüm bölümlerde aynı — YEREL)")]
    [Tooltip("Bölüm teması bu alanı boş bırakırsa bu kullanılır. 2026-11-28: bölüm temalarında aynı görseller 51+ " +
             "paketlerine kopyalanıyordu (fazla boyut + GÖREV butonu kimlik karşılaştırması bozuldu). Sabit görseller " +
             "burada yerelde durur; tema yalnız gerçekten farklı olanı taşır (ör. Usta Ligi).")]
    public Sprite menuTopHud;
    public Sprite menuBottomArea, menuLevelSelectorBtn, menuRepairBtn, gameTopHud, gameBottomArea;

    [Header("Usta Ligi")]
    [Tooltip("Ana yol bitince (current_level > katalogdaki son seviye) bölüm teması yerine bu tema kullanılır. " +
             "Yeni seviyeler gelince kendiliğinden bölüm temasına döner.")]
    [SerializeField] private ChapterTheme masterLeagueTheme;
    [SerializeField] private LevelCatalog levelCatalog;

    [System.Serializable]
    public struct RemoteTheme { public int chapter; public string guid; }
    [SerializeField, HideInInspector] private List<RemoteTheme> remoteThemes = new();

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
        foreach (var entry in remoteThemes)
            if (entry.chapter == chapter) return ProgressiveContent.Resolve<ChapterTheme>(entry.guid);
        if (themes == null || themes.Count == 0) return null;

        for (int i = 0; i < themes.Count; i++)
        {
            if (themes[i] != null && themes[i].chapterIndex == chapter)
                return themes[i];
        }

        // Keep the original final-chapter fallback after its local reference is moved to a bundle.
        ChapterTheme fallback = null;
        for (int i = themes.Count - 1; i >= 0; i--)
            if (themes[i] != null) { fallback = themes[i]; break; }
        int lastRemoteChapter = fallback != null ? fallback.chapterIndex : 0;
        string lastRemoteGuid = null;
        foreach (var entry in remoteThemes)
            if (entry.chapter > lastRemoteChapter) { lastRemoteChapter = entry.chapter; lastRemoteGuid = entry.guid; }
        return lastRemoteGuid != null ? ProgressiveContent.Resolve<ChapterTheme>(lastRemoteGuid) : fallback;
    }

    /// <summary>Reads current_level from PlayerPrefs and returns the matching theme.</summary>
    public ChapterTheme GetCurrentTheme()
    {
        int level   = PlayerPrefs.GetInt(prefsLevelKey, 1);
        ChapterTheme theme = IsMasterLeagueLevel(level)
            ? masterLeagueTheme
            : GetThemeForChapter(GetChapterForLevel(level));
        ChapterTheme.Active = theme;
        return theme;
    }

    // MasterLeague.IsUnlocked ile aynı kural; havuz listesi kurmadan (tema her sahne açılışında okunur).
    private bool IsMasterLeagueLevel(int level)
        => masterLeagueTheme != null && levelCatalog != null && level > levelCatalog.MaximumGlobalLevel;

    /// <summary>Returns the current 1-based chapter number derived from PlayerPrefs.</summary>
    public int GetCurrentChapter()
    {
        return GetChapterForLevel(PlayerPrefs.GetInt(prefsLevelKey, 1));
    }

    /// <summary>
    /// Mevcut chapter'ın localized loading hint listesinden rastgele bir entry döner.
    /// Liste boşsa null döner. Eski loadingScreenImages listesini değiştirmez.
    /// </summary>
    public LoadingHintEntry GetRandomLoadingHint()
    {
        var theme = GetCurrentTheme();
        if (theme == null || theme.loadingHints == null || theme.loadingHints.Count == 0)
            return null;

        var list = theme.loadingHints;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>
    /// Mevcut chapter'ın loading screen görselleri listesinden rastgele bir Sprite döner.
    /// Liste boşsa null döner.
    /// </summary>
    public Sprite GetRandomLoadingImage()
    {
        var theme = GetCurrentTheme();
        if (theme == null || theme.loadingScreenImages == null || theme.loadingScreenImages.Count == 0)
            return null;

        var list = theme.loadingScreenImages;
        int start = Random.Range(0, list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            var sprite = list[(start + i) % list.Count];
            if (sprite != null) return sprite;
        }
        return null;
    }
}
