using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// GERÇEK takım servisi (ITeamService, Docs/ProductionPlan.md P3b): oyuncunun
/// Firestore takımına (PlayerTeamState.TeamId) bağlanır.
/// — Sohbet: teams/{id}/chat canlı dinleyici (son 30) — aynı takımdaki gerçek
///   oyuncular birbirini ANINDA görür; kendi mesajın local-cache'ten anında düşer.
/// — Bot takımlarda (botSeed >= 0) sohbetin başına deterministik bot mesajları
///   harmanlanır → takım ilk günden canlı hisseder.
/// — Üye sayısı: botMembers + realMembers (takım dokümanından).
/// BackendServices.ResetTeam() dispose eder (dinleyici sızmaz).
/// </summary>
public sealed class FirebaseTeamService : ITeamService, IDisposable
{
    private const int ChatLimit = 30;

    public event Action OnChanged;
    public TeamLifeInbox LifeInbox { get; }
    private bool disposed;

    private readonly string teamId;
    private readonly TeamInfo info;
    private readonly List<TeamChatMessage> botChat = new();
    private readonly List<TeamChatMessage> realChat = new();
    private ListenerRegistration chatListener;
    private ListenerRegistration lifeListener;
    // Takım arkadaşlarının son 24 saatteki can istekleri (Firestore; kendi isteğim hariç — o yerel kartta).
    private readonly List<TeamLifeRequest> realLifeRequests = new();

    // Lokalizasyon anahtarları (bot mesajı oyuncunun dilinde).
    private static readonly string[] BotChatPool =
    {
        "team_bot_chat_1", "team_bot_chat_2", "team_bot_chat_3", "team_bot_chat_4",
        "team_bot_chat_5", "team_bot_chat_6", "team_bot_chat_7", "team_bot_chat_8",
        "team_bot_chat_9", "team_bot_chat_10", "team_bot_chat_11", "team_bot_chat_12"
    };

    public FirebaseTeamService()
    {
        teamId = PlayerTeamState.TeamId;
        LifeInbox = new TeamLifeInbox(teamId, () => OnChanged?.Invoke());

        info = new TeamInfo
        {
            teamName = PlayerTeamState.TeamName,
            memberCount = PlayerTeamState.IsCreator ? 1 : 0,   // doküman gelene dek yer tutucu
            memberCapacity = FirebaseTeamCloud.Capacity,
            giftCurrent = 0,
            giftTarget = 100,
            timerLabel = "",
            missionText = "",
        };

        ChatModeration.OnBlockedChanged += HandleBlockedChanged;
        if (FirebaseAuthService.IsReady) Connect();
        else FirebaseAuthService.OnReady += Connect;
    }

    private void Connect()
    {
        if (disposed || string.IsNullOrEmpty(teamId)) return;

        // Takım dokümanı: üye sayısı + bot harman tohumu.
        FirebaseTeamCloud.TeamDoc(teamId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (disposed || task.IsFaulted || task.IsCanceled || !task.Result.Exists) return;

            var snap = task.Result;
            long botMembers = snap.ContainsField("botMembers") ? snap.GetValue<long>("botMembers") : 0;
            long realMembers = snap.ContainsField("realMembers") ? snap.GetValue<long>("realMembers") : 0;
            long botSeed = snap.ContainsField("botSeed") ? snap.GetValue<long>("botSeed") : -1;

            info.memberCount = Mathf.Max(1, (int)(botMembers + realMembers));
            if (botSeed >= 0 && botChat.Count == 0)
                BuildBotChat((int)botSeed, (int)botMembers);

            LifeInbox.SetBots((int)botMembers, () => botChat.Count > 0
                ? botChat[UnityEngine.Random.Range(0, botChat.Count)].senderName : GameLocalization.Get("team_teammates"));
            OnChanged?.Invoke();
        });

        // Can istekleri: takım arkadaşlarınınki kart olarak; kendi dokümanımdaki yardımcılar → "Kabul Et" cevapları.
        lifeListener = FirebaseTeamCloud.LifeRequests(teamId)
            .WhereGreaterThan("createdAt", Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-24)))
            .Listen(OnLifeRequests);

        // Canlı sohbet dinleyicisi (local-cache yazımları da anında düşer).
        chatListener = FirebaseTeamCloud.TeamDoc(teamId).Collection("chat")
            .OrderByDescending("sentAt").Limit(ChatLimit)
            .Listen(snapshot =>
            {
                if (disposed) return;
                realChat.Clear();
                foreach (var doc in snapshot.Documents)
                {
                    string senderId = doc.ContainsField("senderId") ? doc.GetValue<string>("senderId") : "";
                    if (ChatModeration.IsBlocked(senderId)) continue;   // engellenen oyuncunun mesajları görünmez
                    var msg = new TeamChatMessage
                    {
                        senderName = ChatModeration.Filter(doc.ContainsField("senderName") ? doc.GetValue<string>("senderName") : GameLocalization.Get("team_player")),
                        senderId = senderId,
                        messageId = doc.Id,
                        teamId = teamId,
                        text = ChatModeration.Filter(doc.ContainsField("text") ? doc.GetValue<string>("text") : ""),
                        timeLabel = TimeLabel(doc),
                        sentTicks = SentTicks(doc),
                        isMine = senderId == FirebaseAuthService.UserId,
                    };
                    realChat.Insert(0, msg);   // desc sorgu → ekranda eskiden yeniye
                }
                OnChanged?.Invoke();
            });
    }

    private void OnLifeRequests(QuerySnapshot snapshot)
    {
        if (disposed) return;
        string me = FirebaseAuthService.UserId;
        realLifeRequests.Clear();
        foreach (var doc in snapshot.Documents)
        {
            var helpers = doc.ContainsField("helpers") ? doc.GetValue<Dictionary<string, object>>("helpers") : null;
            long created = doc.ContainsField("createdAt") ? SafeTicks(doc, "createdAt") : DateTime.UtcNow.Ticks;
            if (doc.Id == me)
            {
                if (helpers != null && helpers.Count > 0) LifeInbox.ReceiveRealHelpers(created, HelperNames(helpers));
                continue;
            }
            if (ChatModeration.IsBlocked(doc.Id)) continue;
            int needed = doc.ContainsField("needed") ? (int)doc.GetValue<long>("needed") : 5;
            realLifeRequests.Add(new TeamLifeRequest
            {
                requesterId = doc.Id,
                requesterName = ChatModeration.Filter(doc.ContainsField("requesterName")
                    ? doc.GetValue<string>("requesterName") : GameLocalization.Get("team_player")),
                createdTicks = created,
                current = helpers?.Count ?? 0,
                needed = Mathf.Max(1, needed),
                helpedByMe = helpers != null && me != null && helpers.ContainsKey(me),
            });
        }
        OnChanged?.Invoke();
    }

    private static IEnumerable<KeyValuePair<string, string>> HelperNames(Dictionary<string, object> helpers)
    {
        foreach (var kv in helpers)
            yield return new KeyValuePair<string, string>(kv.Key, ChatModeration.Filter(kv.Value as string ?? ""));
    }

    private static long SafeTicks(DocumentSnapshot doc, string field)
    {
        try { return doc.GetValue<Timestamp>(field).ToDateTime().Ticks; }
        catch { return DateTime.UtcNow.Ticks; }   // sunucu zamanı henüz yazılmadı (yerel önbellek)
    }

    // Bot takımın "yaşayan" sohbeti: takım seed'inden deterministik birkaç mesaj.
    private void BuildBotChat(int seed, int botMembers)
    {
        var rng = new System.Random(seed * 7919);
        int count = Mathf.Clamp(botMembers / 8, 2, 5);
        for (int i = 0; i < count; i++)
        {
            botChat.Add(new TeamChatMessage
            {
                senderName = NamePool.PlayerAt(seed * 40 + rng.Next(0, 40)),
                text = GameLocalization.Get(BotChatPool[rng.Next(BotChatPool.Length)]),
                timeLabel = rng.Next(1, 9) + "s",
            });
        }
    }

    private static long SentTicks(DocumentSnapshot doc)
    {
        try
        {
            if (doc.ContainsField("sentAt")) return doc.GetValue<Timestamp>("sentAt").ToDateTime().Ticks;
        }
        catch { } // A local message may still be waiting for its server timestamp.
        return DateTime.UtcNow.Ticks;
    }

    private static string TimeLabel(DocumentSnapshot doc)
    {
        if (!doc.ContainsField("sentAt")) return GameLocalization.Get("team_time_now");
        try
        {
            var ts = doc.GetValue<Timestamp>("sentAt");
            var span = DateTime.UtcNow - ts.ToDateTime();
            if (span.TotalMinutes < 1) return GameLocalization.Get("team_time_now");
            if (span.TotalHours < 1) return GameLocalization.GetFormat("progress_timer_mins", (int)span.TotalMinutes);
            if (span.TotalDays < 1) return GameLocalization.GetFormat("progress_timer_hours", (int)span.TotalHours);
            return GameLocalization.GetFormat("progress_timer_days", (int)span.TotalDays);
        }
        catch { return GameLocalization.Get("team_time_now"); }   // pending server timestamp (henüz senkronlanmadı)
    }

    // ── ITeamService ────────────────────────────────────────────────

    public TeamInfo GetTeamInfo() => info;

    public List<TeamChatMessage> GetChat()
    {
        // Bot mesajları geçmiş olarak başta, gerçek mesajlar kronolojik sonda.
        var merged = new List<TeamChatMessage>(botChat.Count + realChat.Count);
        merged.AddRange(botChat);
        merged.AddRange(realChat);
        return merged;
    }

    public List<TeamLifeRequest> GetLifeRequests() => realLifeRequests;

    /// Takım arkadaşının isteğine 1 can (ücretsiz yardım kuralı; kendi canımdan düşmez). İyimser: kart hemen "Gönderildi".
    public bool Help(TeamLifeRequest request)
    {
        if (disposed || request == null || request.helpedByMe || string.IsNullOrEmpty(request.requesterId)
            || request.current >= request.needed) return false;
        FirebaseTeamCloud.HelpLife(teamId, request.requesterId);
        request.helpedByMe = true;
        request.current++;
        OnChanged?.Invoke();
        return true;
    }

    // İstek Firestore'a yazılır (takım arkadaşları kartı görür); botlar yerel kutudan cevap vermeye devam eder.
    public void RequestLife()
    {
        if (!disposed && LifeInbox.Request(DateTime.UtcNow))
            FirebaseTeamCloud.RequestLife(teamId, LifeInbox.RequestTotal);
    }

    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        FirebaseTeamCloud.SendChat(teamId, text);
        // Yankı gerekmez: Listen local-cache yazımını aynı frame'lerde teslim eder.
    }

    // Engellenen oyuncunun ekrandaki mesajları hemen kalksın (sonraki anlık görüntüyü beklemeden).
    private void HandleBlockedChanged()
    {
        if (disposed) return;
        realChat.RemoveAll(m => ChatModeration.IsBlocked(m.senderId));
        OnChanged?.Invoke();
    }

    public void Dispose()
    {
        disposed = true;
        ChatModeration.OnBlockedChanged -= HandleBlockedChanged;
        chatListener?.Stop();
        chatListener = null;
        lifeListener?.Stop();
        lifeListener = null;
        FirebaseAuthService.OnReady -= Connect;
    }
}
