using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level eşiğine göre farklı ödül havuzu döndüren ScriptableObject.
/// defaultConfig her zaman fallback olarak kullanılır.
/// levelOverrides listesinde oyuncunun level'ı bir eşiği geçince o config aktif olur.
/// </summary>
[CreateAssetMenu(fileName = "LeveledWheelRewardConfig",
    menuName = "CoreCollapse/Events/Leveled Wheel Reward Config")]
public class LeveledWheelRewardConfig : ScriptableObject
{
    [Tooltip("Hiçbir level eşiği eşleşmezse kullanılacak varsayılan ödül havuzu.")]
    public DailySlotRewardConfig defaultConfig;

    [Serializable]
    public class LevelEntry
    {
        [Tooltip("Bu level'a ulaşıldığında devreye girecek ödül havuzu.")]
        public int fromLevel = 1;
        [Tooltip("Bu level aralığı için ödül havuzu. Boş bırakılırsa defaultConfig kullanılır.")]
        public DailySlotRewardConfig config;
    }

    [Tooltip("Level bazlı ödül havuzu override'ları. fromLevel'a göre küçükten büyüğe sırala.")]
    public List<LevelEntry> levelOverrides = new();

    private const string LevelKey = "current_level";

    public DailySlotRewardConfig GetForCurrentLevel()
    {
        int level = PlayerPrefs.GetInt(LevelKey, 1);
        return GetForLevel(level);
    }

    public DailySlotRewardConfig GetForLevel(int level)
    {
        DailySlotRewardConfig result = defaultConfig;
        foreach (var entry in levelOverrides)
        {
            if (entry.config != null && level >= entry.fromLevel)
                result = entry.config;
        }
        return result != null ? result : defaultConfig;
    }
}
