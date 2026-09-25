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

    private static readonly string[] BotChatPool =
    {
        "selam gençler", "günaydın", "bugün etkinlik var mı?", "yardım lazım arkadaşlar",
        "teşekkürler!", "harika oynadınız", "kim aktif?", "bu level çok zor ya",
        "can atabilecek var mı?", "iyi oyunlar herkese", "az kaldı, devam!", "süpersiniz 💪"
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
                ? botChat[UnityEngine.Random.Range(0, botChat.Count)].senderName : "Takım arkadaşların");
            OnChanged?.Invoke();
        });

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
                    var msg = new TeamChatMessage
                    {
                        senderName = doc.ContainsField("senderName") ? doc.GetValue<string>("senderName") : "Oyuncu",
                        text = doc.ContainsField("text") ? doc.GetValue<string>("text") : "",
                        timeLabel = TimeLabel(doc),
                        sentTicks = SentTicks(doc),
                        isMine = senderId == FirebaseAuthService.UserId,
                    };
                    realChat.Insert(0, msg);   // desc sorgu → ekranda eskiden yeniye
                }
                OnChanged?.Invoke();
            });
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
                text = BotChatPool[rng.Next(BotChatPool.Length)],
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
        if (!doc.ContainsField("sentAt")) return "şimdi";
        try
        {
            var ts = doc.GetValue<Timestamp>("sentAt");
            var span = DateTime.UtcNow - ts.ToDateTime();
            if (span.TotalMinutes < 1) return "şimdi";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + "d";
            if (span.TotalDays < 1) return (int)span.TotalHours + "s";
            return (int)span.TotalDays + "g";
        }
        catch { return "şimdi"; }   // pending server timestamp (henüz senkronlanmadı)
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

    public List<TeamLifeRequest> GetLifeRequests() => new();
    public bool Help(TeamLifeRequest request) => false;

    public void RequestLife()
    {
        if (!disposed && LifeInbox.Request(DateTime.UtcNow)) SendMessage("❤️ Can istedi!");
    }

    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        FirebaseTeamCloud.SendChat(teamId, text);
        // Yankı gerekmez: Listen local-cache yazımını aynı frame'lerde teslim eder.
    }

    public void Dispose()
    {
        disposed = true;
        chatListener?.Stop();
        chatListener = null;
        FirebaseAuthService.OnReady -= Connect;
    }
}
