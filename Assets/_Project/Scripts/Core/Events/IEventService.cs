using System;
using System.Collections.Generic;

/// <summary>
/// Event servis sözleşmesi.
/// LocalEventService şimdi, RemoteEventService ileride implement eder.
/// Oyunun geri kalanı sadece bu interface'i görür.
/// </summary>
public interface IEventService
{
    // --- Katılım ---
    bool CanParticipate(string playerId);
    void JoinEvent(string playerId);

    // --- Oyun sonucu bildirimi ---
    void ReportWin(string playerId);
    void ReportLoss(string playerId);

    // --- Durum sorguları ---
    EventParticipantStatus GetStatus(string playerId);
    int GetCurrentStreak(string playerId);
    TimeSpan GetCooldownRemaining(string playerId);
    bool IsEventActive();
    DateTime GetEventEndTime();

    // --- Ödül ---
    void ClaimReward(string playerId);
    int CalculateRewardShare();

    // --- Leaderboard ---
    List<EventParticipant> GetTopParticipants(int count);
}

public enum EventParticipantStatus
{
    NotJoined,
    Active,       // oyunda, streak devam ediyor
    Cooldown,     // kaybetti, bekleme süresi dolmadı
    Winner,       // 5 üst üste kazandı, ödül bekliyor
    Claimed       // ödül alındı
}

[System.Serializable]
public class EventParticipant
{
    public string playerId;
    public string displayName;
    public bool isBot;
    public int streak;
    public EventParticipantStatus status;
    public long lastFailTimestamp;   // Unix ms
    public string teamId;
}
