using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "CoreCollapse/Level Catalog", order = 2)]
public class LevelCatalog : ScriptableObject
{
    [Serializable]
    public class LevelEntry
    {
        [Min(1)] public int chapter = 1;
        [Min(1)] public int level = 1;
        public string levelKey;
        public LevelData levelData;

        public bool Matches(int chapterValue, int levelValue)
        {
            return chapter == chapterValue && level == levelValue;
        }

        public bool Matches(string key)
        {
            return !string.IsNullOrWhiteSpace(levelKey)
                   && string.Equals(levelKey, key, StringComparison.OrdinalIgnoreCase);
        }
    }

    public List<LevelEntry> entries = new();

    public bool TryGetLevel(int chapter, int level, out LevelData levelData)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.levelData == null) continue;
            if (entry.Matches(chapter, level))
            {
                levelData = entry.levelData;
                return true;
            }
        }

        levelData = null;
        return false;
    }

    public bool TryGetLevel(string levelKey, out LevelData levelData)
    {
        if (string.IsNullOrWhiteSpace(levelKey))
        {
            levelData = null;
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.levelData == null) continue;
            if (entry.Matches(levelKey))
            {
                levelData = entry.levelData;
                return true;
            }
        }

        levelData = null;
        return false;
    }
}
