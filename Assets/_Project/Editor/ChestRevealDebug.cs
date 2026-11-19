using UnityEditor;
using UnityEngine;

/// <summary>
/// Sandık açılış törenini (RewardChestRevealOverlay) Play Mode'da sahte ödüllerle test eder.
/// Menü: TinyFixers > Debug > Test Chest Reveal (Play Mode)
/// </summary>
public static class ChestRevealDebug
{
    [MenuItem("TinyFixers/Debug/Test Chest Reveal (Play Mode)")]
    [MenuItem("TinyFixers/Preview/Reward Titles/Chest (Play Mode)")]
    public static void TestReveal()
    {
        if (!CanPreview()) return;

        var coin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/GoldMoney.png");
        var star = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Star.png");

        var rewards = new System.Collections.Generic.List<DailySlotReward>
        {
            new DailySlotReward { type = DailySlotRewardType.Joker_SystemOverride, amount = 2, fallbackName = "Override" },
            new DailySlotReward { type = DailySlotRewardType.Joker_PulseCore,      amount = 1, fallbackName = "PulseCore" },
            new DailySlotReward { type = DailySlotRewardType.Coins, amount = 500, icon = coin, fallbackName = "Altın" },
            new DailySlotReward { type = DailySlotRewardType.Stars, amount = 3,   icon = star, fallbackName = "Yıldız" },
        };

        RewardChestRevealOverlay.Show(rewards, () => {
    if (GameDiagnostics.EditorTools)
        Debug.Log("[ChestRevealDebug] Tören bitti.");
}, titleKey: "progress_chest_title");
    }

    [MenuItem("TinyFixers/Preview/Reward Titles/Gold Gift (Play Mode)")]
    public static void TestGoldGift()
    {
        if (!CanPreview()) return;
        var coin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/GoldMoney.png");
        // Display only: no claim callback, wallet changes or progress writes.
        ComebackGiftOverlay.Show(new[]
        {
            new DailySlotReward { type = DailySlotRewardType.Coins, amount = 500, icon = coin, fallbackName = "Altın" }
        }, onClaim: null);
    }

    private static bool CanPreview()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Ödül Başlığı Önizleme",
                "MainMenu sahnesinde Play Mode'a gir, ardından bu menüyü tekrar seç. Önizleme ödül veya ilerleme eklemez.", "Tamam");
            return false;
        }
        if (RewardChestRevealOverlay.IsShowing || ComebackGiftOverlay.IsShowing)
        {
            EditorUtility.DisplayDialog("Ödül Başlığı Önizleme",
                "Önce açık ödül ekranını dokunarak kapat, ardından tekrar seç.", "Tamam");
            return false;
        }
        return true;
    }
}
