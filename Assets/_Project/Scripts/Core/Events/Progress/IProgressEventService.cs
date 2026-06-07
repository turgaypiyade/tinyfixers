using System;
using System.Collections.Generic;

/// Ana menü animasyonu için bir session'da ne kazanıldığını taşır.
public class SessionGainRecord
{
    public int              GoalIndex;
    public int              GainedCount;   // bu sessionda bu hedefe eklenen miktar
    public bool             RewardGranted; // bu sessionda tamamlanıp ödül verildi mi
    public DailySlotReward  Reward;        // verilen ödül (animasyon için)
}

public interface IProgressEventService
{
    ProgressEventState             State         { get; }
    TimeSpan                       TimeRemaining { get; }
    IReadOnlyList<ProgressGoalRuntime> Goals     { get; }
    string                         EventName     { get; }

    /// Ana menü animasyonunu başlatmak için session kazanımlarını alır ve temizler.
    IReadOnlyList<SessionGainRecord> ConsumeSessionGains();
}
