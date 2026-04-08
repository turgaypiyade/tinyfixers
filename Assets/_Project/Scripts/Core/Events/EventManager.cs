using System;
using UnityEngine;

/// <summary>
/// Event sisteminin MonoBehaviour koordinatörü.
/// — LocalEventService'i ayağa kaldırır
/// — Level sonuçlarını (win/loss) event'e bildirir
/// — UI için durum ve cooldown sorgularını expose eder
/// — Cooldown biten oyuncuyu otomatik Active'e alır
/// </summary>
public class EventManager : MonoBehaviour
{
    [SerializeField] private EventConfig eventConfig;
    [SerializeField] private BotConfig   botConfig;

    // Dışarıdan IEventService olarak erişilir — ileride Remote ile swap edilir
    public IEventService EventService { get; private set; }

    // Aktif oyuncu kimliği (oturum boyunca sabit)
    private string playerId;

    // Cooldown kontrol aralığı (saniye)
    private const float CooldownCheckInterval = 5f;
    private float cooldownTimer;

    // -----------------------------------------------------------------------
    // Static events — UI bileşenleri bunlara abone olur

    public static event Action<EventParticipantStatus> OnStatusChanged;
    public static event Action<int>                    OnStreakChanged;
    public static event Action<int>                    OnRewardClaimed;   // coin miktarı

    // -----------------------------------------------------------------------

    private void Awake()
    {
        playerId = PlayerPrefs.GetString("player_id", SystemInfo.deviceUniqueIdentifier);
        PlayerPrefs.SetString("player_id", playerId);

        var botService = new BotService(botConfig);
        botService.Initialize();

        EventService = new LocalEventService(eventConfig, botService);
    }

    private void Update()
    {
        // Cooldown'daki oyuncu için periyodik kontrol
        if (EventService.GetStatus(playerId) != EventParticipantStatus.Cooldown) return;

        cooldownTimer += Time.deltaTime;
        if (cooldownTimer < CooldownCheckInterval) return;

        cooldownTimer = 0f;
        var remaining = EventService.GetCooldownRemaining(playerId);

        if (remaining <= TimeSpan.Zero)
        {
            // GetCooldownRemaining zaten status'ü Active'e çekti
            OnStatusChanged?.Invoke(EventParticipantStatus.Active);
            Debug.Log("[EventManager] Cooldown bitti, oyuncu Active.");
        }
    }

    // -----------------------------------------------------------------------
    // Level sonucu bildirimi — LevelEndSimplePopupController'dan çağrılır

    /// <summary>Level kazanıldığında çağır.</summary>
    public void NotifyLevelWon()
    {
        var status = EventService.GetStatus(playerId);

        if (status == EventParticipantStatus.NotJoined)
        {
            EventService.JoinEvent(playerId);
        }

        if (EventService.GetStatus(playerId) != EventParticipantStatus.Active) return;

        EventService.ReportWin(playerId);

        var newStatus = EventService.GetStatus(playerId);
        OnStatusChanged?.Invoke(newStatus);
        OnStreakChanged?.Invoke(EventService.GetCurrentStreak(playerId));
    }

    /// <summary>Level kaybedildiğinde çağır.</summary>
    public void NotifyLevelLost()
    {
        if (EventService.GetStatus(playerId) != EventParticipantStatus.Active) return;

        EventService.ReportLoss(playerId);
        OnStatusChanged?.Invoke(EventParticipantStatus.Cooldown);
        OnStreakChanged?.Invoke(0);
    }

    // -----------------------------------------------------------------------
    // Ödül

    /// <summary>Winner olan oyuncu ödülünü talep eder.</summary>
    public void ClaimReward()
    {
        if (EventService.GetStatus(playerId) != EventParticipantStatus.Winner) return;

        int share = EventService.CalculateRewardShare();
        EventService.ClaimReward(playerId);

        OnStatusChanged?.Invoke(EventParticipantStatus.Claimed);
        OnRewardClaimed?.Invoke(share);
    }

    // -----------------------------------------------------------------------
    // UI yardımcı sorguları

    public EventParticipantStatus CurrentStatus  => EventService.GetStatus(playerId);
    public int                    CurrentStreak  => EventService.GetCurrentStreak(playerId);
    public int                    WinsRequired   => eventConfig.consecutiveWinsRequired;
    public bool                   IsEventActive  => EventService.IsEventActive();
    public DateTime               EventEndTime   => EventService.GetEventEndTime();

    public TimeSpan CooldownRemaining => EventService.GetCooldownRemaining(playerId);

    /// <summary>Streak ilerleme oranı (0–1), progress bar için.</summary>
    public float StreakRatio =>
        WinsRequired > 0 ? Mathf.Clamp01((float)CurrentStreak / WinsRequired) : 0f;
}
