using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IEventService'in PlayerPrefs tabanlı yerel implementasyonu.
/// Sunucu entegrasyonunda RemoteEventService ile değiştirilebilir.
/// </summary>
public class LocalEventService : IEventService
{
    private readonly EventConfig config;
    private readonly BotService  botService;

    // PlayerPrefs anahtarları
    private const string KeyEventStartTime  = "event_start_time";   // UTC ticks
    private const string KeyParticipantData = "event_participants";  // JSON array

    // Bellek içi katılımcı listesi (oyun boyunca)
    private readonly List<EventParticipant> participants = new();
    private DateTime eventStartTime;

    // -----------------------------------------------------------------------

    public LocalEventService(EventConfig config, BotService botService)
    {
        this.config     = config;
        this.botService = botService;
        LoadOrStartEvent();
    }

    // -----------------------------------------------------------------------
    // Başlatma / yükleme

    private void LoadOrStartEvent()
    {
        if (PlayerPrefs.HasKey(KeyEventStartTime))
        {
            long ticks = Convert.ToInt64(PlayerPrefs.GetString(KeyEventStartTime, "0"));
            eventStartTime = new DateTime(ticks, DateTimeKind.Utc);

            if (!IsEventActive())
            {
                // Event süresi dolmuş → yeni event başlat
                StartNewEvent();
                return;
            }

            LoadParticipants();
        }
        else
        {
            StartNewEvent();
        }
    }

    private void StartNewEvent()
    {
        eventStartTime = DateTime.UtcNow;
        PlayerPrefs.SetString(KeyEventStartTime, eventStartTime.Ticks.ToString());
        participants.Clear();
        SaveParticipants();
        PlayerPrefs.Save();
        PopulateWithBots();
        Debug.Log($"[LocalEventService] Yeni event başladı. Bitiş: {GetEventEndTime():g}");
    }

    private void PopulateWithBots()
    {
        int count = Mathf.Min(botService.Pool.Count, 200); // leaderboard doluluk simülasyonu
        var bots  = botService.GetRandomBots(count);

        foreach (var bot in bots)
        {
            var p = new EventParticipant
            {
                playerId    = bot.botId,
                displayName = bot.displayName,
                isBot       = true,
                streak      = UnityEngine.Random.Range(0, config.consecutiveWinsRequired),
                status      = EventParticipantStatus.Active,
                teamId      = bot.teamId
            };

            // Bazı botlar zaten kazanmış olsun
            if (UnityEngine.Random.value < 0.05f)
                p.status = EventParticipantStatus.Winner;

            participants.Add(p);
        }

        SaveParticipants();
    }

    // -----------------------------------------------------------------------
    // IEventService — Katılım

    public bool CanParticipate(string playerId)
    {
        if (!IsEventActive()) return false;
        if (config.maxParticipants > 0 && participants.Count >= config.maxParticipants) return false;

        var p = Find(playerId);
        if (p == null) return true;

        return p.status == EventParticipantStatus.Active ||
               p.status == EventParticipantStatus.NotJoined ||
               (p.status == EventParticipantStatus.Cooldown && GetCooldownRemaining(playerId) <= TimeSpan.Zero);
    }

    public void JoinEvent(string playerId)
    {
        if (!CanParticipate(playerId)) return;

        var p = Find(playerId);
        if (p == null)
        {
            p = new EventParticipant
            {
                playerId    = playerId,
                displayName = playerId,
                isBot       = false,
                streak      = 0,
                status      = EventParticipantStatus.Active
            };
            participants.Add(p);
        }
        else
        {
            p.status = EventParticipantStatus.Active;
            p.streak = 0;
        }

        SaveParticipants();
        Debug.Log($"[LocalEventService] {playerId} event'e katıldı.");
    }

    // -----------------------------------------------------------------------
    // IEventService — Oyun sonucu

    public void ReportWin(string playerId)
    {
        var p = Find(playerId);
        if (p == null || p.status != EventParticipantStatus.Active) return;

        p.streak++;
        Debug.Log($"[LocalEventService] {playerId} kazandı. Streak: {p.streak}/{config.consecutiveWinsRequired}");

        if (p.streak >= config.consecutiveWinsRequired)
        {
            p.status = EventParticipantStatus.Winner;
            Debug.Log($"[LocalEventService] {playerId} EVENT KAZANDI!");
        }

        SaveParticipants();
    }

    public void ReportLoss(string playerId)
    {
        var p = Find(playerId);
        if (p == null || p.status != EventParticipantStatus.Active) return;

        p.streak            = 0;
        p.status            = EventParticipantStatus.Cooldown;
        p.lastFailTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Debug.Log($"[LocalEventService] {playerId} kaybetti. Cooldown: {config.failCooldownMinutes} dk.");

        SaveParticipants();
    }

    // -----------------------------------------------------------------------
    // IEventService — Durum sorguları

    public EventParticipantStatus GetStatus(string playerId)
    {
        var p = Find(playerId);
        return p?.status ?? EventParticipantStatus.NotJoined;
    }

    public int GetCurrentStreak(string playerId)
    {
        return Find(playerId)?.streak ?? 0;
    }

    public TimeSpan GetCooldownRemaining(string playerId)
    {
        var p = Find(playerId);
        if (p == null || p.status != EventParticipantStatus.Cooldown) return TimeSpan.Zero;

        var failTime  = DateTimeOffset.FromUnixTimeMilliseconds(p.lastFailTimestamp).UtcDateTime;
        var cooldownEnd = failTime.AddMinutes(config.failCooldownMinutes);
        var remaining   = cooldownEnd - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            p.status = EventParticipantStatus.Active;
            SaveParticipants();
            return TimeSpan.Zero;
        }

        return remaining;
    }

    public bool IsEventActive()
    {
        return DateTime.UtcNow < GetEventEndTime();
    }

    public DateTime GetEventEndTime()
    {
        return eventStartTime.AddHours(config.DurationHours);
    }

    // -----------------------------------------------------------------------
    // IEventService — Ödül

    public void ClaimReward(string playerId)
    {
        var p = Find(playerId);
        if (p == null || p.status != EventParticipantStatus.Winner) return;

        int share = CalculateRewardShare();
        PlayerWallet.AddCoins(share);

        p.status = EventParticipantStatus.Claimed;
        SaveParticipants();

        Debug.Log($"[LocalEventService] {playerId} {share} coin ödül aldı.");
    }

    public int CalculateRewardShare()
    {
        int winnerCount = 0;
        foreach (var p in participants)
            if (p.status == EventParticipantStatus.Winner || p.status == EventParticipantStatus.Claimed)
                winnerCount++;

        if (winnerCount == 0) return config.prizePool;
        return Mathf.Max(1, config.prizePool / winnerCount);
    }

    // -----------------------------------------------------------------------
    // IEventService — Leaderboard

    public List<EventParticipant> GetTopParticipants(int count)
    {
        var sorted = new List<EventParticipant>(participants);
        sorted.Sort((a, b) =>
        {
            // Önce kazananlar, sonra streake göre
            int statusA = StatusRank(a.status);
            int statusB = StatusRank(b.status);
            if (statusA != statusB) return statusB.CompareTo(statusA);
            return b.streak.CompareTo(a.streak);
        });

        int take = Mathf.Min(count, sorted.Count);
        return sorted.GetRange(0, take);
    }

    // -----------------------------------------------------------------------
    // Yardımcı

    private EventParticipant Find(string playerId)
    {
        foreach (var p in participants)
            if (p.playerId == playerId) return p;
        return null;
    }

    private static int StatusRank(EventParticipantStatus s) => s switch
    {
        EventParticipantStatus.Winner  => 3,
        EventParticipantStatus.Claimed => 2,
        EventParticipantStatus.Active  => 1,
        _                              => 0
    };

    // -----------------------------------------------------------------------
    // Persistence (basit JSON wrapper)

    private void SaveParticipants()
    {
        var wrapper = new ParticipantListWrapper { items = participants };
        PlayerPrefs.SetString(KeyParticipantData, JsonUtility.ToJson(wrapper));
    }

    private void LoadParticipants()
    {
        participants.Clear();
        string json = PlayerPrefs.GetString(KeyParticipantData, "");
        if (string.IsNullOrEmpty(json)) return;

        var wrapper = JsonUtility.FromJson<ParticipantListWrapper>(json);
        if (wrapper?.items != null)
            participants.AddRange(wrapper.items);
    }

    [Serializable]
    private class ParticipantListWrapper
    {
        public List<EventParticipant> items;
    }
}
