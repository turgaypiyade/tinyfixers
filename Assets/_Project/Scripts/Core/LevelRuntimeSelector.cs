using UnityEngine;

public class LevelRuntimeSelector : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private LevelCatalog levelCatalog;

    [Header("Test Progression")]
    [Tooltip("Açık: oyuncunun ilerlemesindeki level (CurrentLevel.Global — ana menüyle AYNI kaynak, yeni " +
             "kurulumda 1). Kapalı: aşağıdaki Input seçimi (yalnız test).")]
    [SerializeField] private bool usePlayerPrefsLevel = true;

    [Header("Input")]
    [SerializeField] private bool useLevelKey;
    [SerializeField] private string levelKey;
    [SerializeField, Min(1)] private int chapter = 1;
    [SerializeField, Min(1)] private int level = 1;

    public LevelCatalog Catalog => levelCatalog;

    public LevelData ResolveLevelData()
    {
        if (RuntimeSimulationSession.IsActive) return RuntimeSimulationSession.CurrentLevel;
        if (levelCatalog == null)
        {
            Debug.LogError("[LevelRuntimeSelector] LevelCatalog is null.");
            return null;
        }

        if (usePlayerPrefsLevel)
        {
            // Tek kaynak: ana menü / pre-level popup / hedef gösterimi de CurrentLevel.Global okur.
            // Eskiden burada ayrı bir Inspector varsayılanı vardı (sahnede 22 kalmıştı) → yeni kurulumda
            // ana menü "Seviye 1" gösterirken oyun 22. level'ı (LevelP_00240) açıyordu.
            int selectedLevel = CurrentLevel.Global;

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
