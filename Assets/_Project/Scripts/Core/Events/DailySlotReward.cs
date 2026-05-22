using System.Collections.Generic;
using UnityEngine;

public enum DailySlotRewardType
{
    Coins                = 0,
    Lives                = 1,
    Stars                = 2,

    Joker_LineH          = 10,
    Joker_PulseCore      = 11,
    Joker_SystemOverride = 12,

    Booster_Hammer       = 20,
    Booster_Row          = 21,
    Booster_Column       = 22,
    Booster_Shuffle      = 23,
}

[System.Serializable]
public class DailySlotReward
{
    [Tooltip("Ödülün tipi.")]
    public DailySlotRewardType type;

    [Tooltip("Miktar — coin için altın sayısı, joker/booster için adet, life için kalp sayısı.")]
    [Min(1)] public int amount = 1;

    [Tooltip("Slot reel'de ve kazanma popup'ında gösterilecek ikon.")]
    public Sprite icon;

    [Tooltip("Ödül adı için localization key (örn \"reward_coins\", \"reward_hammer\"). " +
             "Boş olursa fallback name kullanılır.")]
    public string nameLocalizationKey;

    [Tooltip("Localization yoksa kullanılacak isim (örn \"100 Altın\").")]
    public string fallbackName;

    [Tooltip("Spin'de çıkma olasılığı ağırlığı. Yüksek = daha sık çıkar. " +
             "Normalize edilir, mutlak değer önemli değil sadece oran.")]
    [Min(0)] public int weight = 10;
}

[CreateAssetMenu(fileName = "DailySlotRewardConfig", menuName = "CoreCollapse/Events/Daily Slot Reward Config", order = 1)]
public class DailySlotRewardConfig : ScriptableObject
{
    [Tooltip("Slot machine'de spawn olabilecek ödüller. Weight'lere göre rastgele seçilir.")]
    public List<DailySlotReward> rewards = new();

    /// Toplam weight (normalize için).
    public int TotalWeight
    {
        get
        {
            int total = 0;
            for (int i = 0; i < rewards.Count; i++)
                if (rewards[i] != null) total += Mathf.Max(0, rewards[i].weight);
            return total;
        }
    }

    /// Weight'lere göre rastgele bir ödül seç.
    public DailySlotReward PickRandom()
    {
        if (rewards == null || rewards.Count == 0) return null;
        int total = TotalWeight;
        if (total <= 0) return rewards[0];

        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i] == null) continue;
            acc += Mathf.Max(0, rewards[i].weight);
            if (roll < acc) return rewards[i];
        }
        return rewards[rewards.Count - 1];
    }
}
