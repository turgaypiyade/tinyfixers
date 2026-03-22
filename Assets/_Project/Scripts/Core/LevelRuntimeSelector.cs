using UnityEngine;

public class LevelRuntimeSelector : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private LevelCatalog levelCatalog;

    [Header("Input")]
    [SerializeField] private bool useLevelKey;
    [SerializeField] private string levelKey;
    [SerializeField, Min(1)] private int chapter = 1;
    [SerializeField, Min(1)] private int level = 1;

    public LevelData ResolveLevelData()
    {
        if (levelCatalog == null)
            return null;

        if (useLevelKey)
        {
            if (levelCatalog.TryGetLevel(levelKey, out var byKey))
                return byKey;

            return null;
        }

        if (levelCatalog.TryGetLevel(chapter, level, out var byChapterAndLevel))
            return byChapterAndLevel;

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
