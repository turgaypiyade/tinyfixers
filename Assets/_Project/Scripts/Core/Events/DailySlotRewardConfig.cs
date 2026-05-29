using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DailySlotRewardConfig", menuName = "CoreCollapse/Events/Daily Slot Reward Config", order = 1)]
public class DailySlotRewardConfig : ScriptableObject
{
    [Tooltip("Slot machine'de spawn olabilecek ödüller. Weight'lere göre rastgele seçilir.")]
    public List<DailySlotReward> rewards = new();

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
