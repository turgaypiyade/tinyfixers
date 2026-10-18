using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgressEventConfig",
                 menuName = "TinyFixers/Events/Progress Event Config")]
public class ProgressEventConfig : ScriptableObject
{
    [Tooltip("Event adı — progress bar başlığında gösterilir.")]
    public string eventName = "Haftalık Görevler";

    [Tooltip("Event süresi (saat). 48 = 2 gün.")]
    [Min(1)] public float durationHours = 48f;

    public List<ProgressGoalDefinition> goals = new();
}

[Serializable]
public class ProgressGoalDefinition
{
    [Tooltip("Hangi olay sayılacak.")]
    public ProgressGoalType goalType;

    [Tooltip("Hedefe ulaşmak için gereken toplam sayı.")]
    [Min(1)] public int targetCount = 100;

    [Tooltip("Hedef tamamlanınca verilecek ödül.")]
    public DailySlotReward reward;

    [Tooltip("0 = adet bazlı. Dakika yalnız can ve level başı jokerlerde desteklenir.")]
    [Min(0)] public int rewardDurationMinutes = 0;

    [Tooltip("Reward görseli arkasında dalga deseni. Null bırakırsan gösterilmez.")]
    public Sprite rewardWaveSprite;

    [Tooltip("Progress bar'da gösterilecek açıklama. Örn: '200 taş kır'")]
    public string fallbackDescription;

    [Tooltip("Hedef türünün ortak ikonu bulunamazsa kullanılacak hedef ikonu. Ödül ikonu değildir.")]
    public Sprite goalIcon;

    public int EffectiveRewardDurationMinutes => reward != null && TimedRewardService.SupportsTimed(reward.type)
        ? Mathf.Max(0, rewardDurationMinutes) : 0;

    public string GetDescription() => !string.IsNullOrWhiteSpace(fallbackDescription)
        ? fallbackDescription
        : GameLocalization.GetFormat("progress_goal_" + goalType, targetCount);

    public Sprite ResolveGoalIcon() => LibraryGoalIcon(goalType) ?? goalIcon;

    internal static Sprite LibraryGoalIcon(ProgressGoalType type)
    {
        var lib = TileIconLibrary.Shared;
        if (lib == null) return null;
        return type switch
        {
            ProgressGoalType.TileCleared_Gear => lib.gear,
            ProgressGoalType.TileCleared_Core => lib.core,
            ProgressGoalType.TileCleared_Bolt => lib.bolt,
            ProgressGoalType.TileCleared_Plate => lib.plate,
            ProgressGoalType.TileCleared_Any => lib.gear,
            ProgressGoalType.LineH => lib.lineH,
            ProgressGoalType.LineV => lib.lineV,
            ProgressGoalType.LineAny => lib.lineH,
            ProgressGoalType.ObstacleCleared => lib.boosterHammer,
            ProgressGoalType.SpecialCreated => lib.pulseCore,
            ProgressGoalType.LevelWon => lib.star,
            ProgressGoalType.FirstTryWin => lib.star,
            _ => null,
        };
    }
}
