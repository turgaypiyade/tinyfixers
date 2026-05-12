using UnityEngine;

public class LevelRuntimeSelector : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private LevelCatalog levelCatalog;

    [Header("Test Progression")]
    [SerializeField] private bool usePlayerPrefsLevel = true;
    [SerializeField] private string prefsLevelKey = "current_level";
    [SerializeField, Min(1)] private int prefsDefaultLevel = 1;

    [Header("Input")]
    [SerializeField] private bool useLevelKey;
    [SerializeField] private string levelKey;
    [SerializeField, Min(1)] private int chapter = 1;
    [SerializeField, Min(1)] private int level = 1;

    public LevelData ResolveLevelData()
    {
        if (levelCatalog == null)
        {
            Debug.LogError("[LevelRuntimeSelector] LevelCatalog is null.");
            return null;
        }

        if (usePlayerPrefsLevel)
        {
            int selectedLevel = Mathf.Max(1, PlayerPrefs.GetInt(prefsLevelKey, prefsDefaultLevel));

            Debug.Log($"[LevelRuntimeSelector] Loading from PlayerPrefs. Chapter={chapter}, Level={selectedLevel}");

            if (levelCatalog.TryGetGlobalLevel(selectedLevel, out var byPrefsLevel))
                return byPrefsLevel;

            Debug.LogWarning($"[LevelRuntimeSelector] PlayerPrefs level not found in catalog. Chapter={chapter}, Level={selectedLevel}. Falling back to inspector selection.");
        }

        if (useLevelKey)
        {
            Debug.Log($"[LevelRuntimeSelector] Loading by key: {levelKey}");

            if (levelCatalog.TryGetLevel(levelKey, out var byKey))
                return byKey;

            Debug.LogWarning($"[LevelRuntimeSelector] Level key not found: {levelKey}");
            return null;
        }

        Debug.Log($"[LevelRuntimeSelector] Loading from inspector. Chapter={chapter}, Level={level}");

        if (levelCatalog.TryGetLevel(chapter, level, out var byChapterAndLevel))
            return byChapterAndLevel;

        Debug.LogWarning($"[LevelRuntimeSelector] Inspector level not found. Chapter={chapter}, Level={level}");
        return null;
    }

    public void SetSelection(int chapterValue, int levelValue)
    {
        useLevelKey = false;
        chapter = Mathf.Max(1, chapterValue);
        level = Mathf.Max(1, levelValue);
    }

    public void SetSelection(string key)
    {
        useLevelKey = true;
        levelKey = key;
    }
}