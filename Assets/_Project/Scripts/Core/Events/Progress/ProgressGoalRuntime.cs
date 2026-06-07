using UnityEngine;

/// Bir progress hedefinin çalışma zamanı durumu.
/// UI bunu okuyarak progress bar, ikon ve ödül göstergesini doldurur.
public class ProgressGoalRuntime
{
    public ProgressGoalDefinition Definition { get; }

    public int  CurrentCount    { get; private set; }
    public bool IsRewardClaimed { get; private set; }

    public bool  IsCompleted       => CurrentCount >= Definition.targetCount;
    public float NormalizedProgress =>
        Definition.targetCount > 0
            ? Mathf.Clamp01((float)CurrentCount / Definition.targetCount)
            : 0f;

    // goalIcon null ise reward.icon'a düşer — UI bunu kullanır.
    public Sprite DisplayIcon =>
        Definition.goalIcon != null ? Definition.goalIcon : Definition.reward?.icon;

    public ProgressGoalRuntime(ProgressGoalDefinition definition)
    {
        Definition = definition;
    }

    // Miktarı ekler; hedefi aşarsa fazlayı döner (bir sonraki hedefe taşınır).
    internal int AddWithOverflow(int amount)
    {
        if (IsCompleted) return amount;
        int space = Definition.targetCount - CurrentCount;
        if (amount >= space)
        {
            CurrentCount = Definition.targetCount;
            return amount - space;
        }
        CurrentCount += amount;
        return 0;
    }

    internal void SetFromSave(int count, bool claimed)
    {
        CurrentCount    = Mathf.Clamp(count, 0, Definition.targetCount);
        IsRewardClaimed = claimed;
    }

    internal void MarkClaimed() => IsRewardClaimed = true;
}
