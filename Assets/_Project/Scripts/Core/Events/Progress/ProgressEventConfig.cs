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

    [Tooltip("0 = adet bazlı (reward.amount kadar), >0 = N dakika ücretsiz kullanım.")]
    [Min(0)] public int rewardDurationMinutes = 0;

    [Tooltip("Reward görseli arkasında dalga deseni. Null bırakırsan gösterilmez.")]
    public Sprite rewardWaveSprite;

    [Tooltip("Progress bar'da gösterilecek açıklama. Örn: '200 taş kır'")]
    public string fallbackDescription;

    [Tooltip("Progress bar sol tarafındaki ikon. Null bırakırsan reward.icon kullanılır.")]
    public Sprite goalIcon;
}
