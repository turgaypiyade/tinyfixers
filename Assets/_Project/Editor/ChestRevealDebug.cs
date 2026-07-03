using UnityEditor;
using UnityEngine;

/// <summary>
/// Sandık açılış törenini (RewardChestRevealOverlay) Play Mode'da sahte ödüllerle test eder.
/// Menü: TinyFixers > Debug > Test Chest Reveal (Play Mode)
/// </summary>
public static class ChestRevealDebug
{
    [MenuItem("TinyFixers/Debug/Test Chest Reveal (Play Mode)")]
    public static void TestReveal()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Chest Reveal", "Önce Play Mode'a gir.", "Tamam");
            return;
        }

        var coin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/GoldMoney.png");
        var star = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Star.png");

        var rewards = new System.Collections.Generic.List<DailySlotReward>
        {
            new DailySlotReward { type = DailySlotRewardType.Joker_SystemOverride, amount = 2, fallbackName = "Override" },
            new DailySlotReward { type = DailySlotRewardType.Joker_PulseCore,      amount = 1, fallbackName = "PulseCore" },
            new DailySlotReward { type = DailySlotRewardType.Coins, amount = 500, icon = coin, fallbackName = "Altın" },
            new DailySlotReward { type = DailySlotRewardType.Stars, amount = 3,   icon = star, fallbackName = "Yıldız" },
        };

        RewardChestRevealOverlay.Show(rewards, () => Debug.Log("[ChestRevealDebug] Tören bitti."));
    }
}
