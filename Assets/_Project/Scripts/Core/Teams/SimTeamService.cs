using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Takım ekranı için yerel simülasyon servisi (ITeamService). Oyuncunun KENDİ takımını
/// üretir: ~40 bot üye (BotNameGenerator + NamePool ile zengin isimli), simüle sohbet ve
/// can istekleri. Tam bot havuzunu (10K) açmaz — sadece bir takım kadar üye üretir, hafiftir.
///
/// Gerçek Firebase takımı gelince yerini FirebaseTeamService alır (BackendServices tek satır).
/// 100-takım/10K-bot TeamManager, takım-tarayıcı/takım-liderlik için ayrı durur.
/// </summary>
public sealed class SimTeamService : ITeamService
{
    public event System.Action OnChanged;
    public TeamLifeInbox LifeInbox { get; }

    private readonly TeamInfo info;
    private readonly List<BotPlayer> members = new();
    private readonly List<TeamChatMessage> chat = new();
    private readonly List<TeamLifeRequest> requests = new();

    // Lokalizasyon anahtarları (bot mesajı oyuncunun dilinde).
    private static readonly string[] ChatPool =
    {
        "team_bot_chat_1", "team_bot_chat_2", "team_bot_chat_3", "team_bot_chat_4",
        "team_bot_chat_5", "team_bot_chat_6", "team_bot_chat_7", "team_bot_chat_8",
        "team_bot_chat_9", "team_bot_chat_10", "team_bot_chat_11", "team_bot_chat_12"
    };

    public SimTeamService()
    {
        // Takımı OYUNCU KURDUYSA: tek üye (kendisi), hoş geldin mesajı — bot doldurma yok.
        // Katıldığı (hazır) takımlar bot üyelerle simüle edilir.
        bool created = PlayerTeamState.HasTeam && PlayerTeamState.IsCreator;

        if (!created)
        {
            var config = ScriptableObject.CreateInstance<BotConfig>();   // default değerler (teamMemberCount=40, dil oto)
            var lang = BotNameGenerator.DetectLanguage(config);

            int memberCount = Mathf.Max(4, config.teamMemberCount);
            for (int i = 0; i < memberCount; i++)
            {
                members.Add(new BotPlayer
                {
                    botId = $"member_{i}",
                    displayName = BotNameGenerator.Generate(lang),
                    level = Random.Range(1, 30),
                });
            }
        }

        info = new TeamInfo
        {
            teamName = PlayerTeamState.TeamName,   // liderlik panosuyla aynı takım adı
            memberCount = created ? 1 : members.Count,
            memberCapacity = 50,
            giftCurrent = created ? 0 : Random.Range(20, 90),
            giftTarget = 100,
            timerLabel = "2g 20s",
            missionText = GameLocalization.Get("team_mission_start"),
        };

        LifeInbox = new TeamLifeInbox("local:" + PlayerTeamState.TeamName, () => OnChanged?.Invoke());
        LifeInbox.SetBots(members.Count, () => members[Random.Range(0, members.Count)].displayName);

        if (created)
        {
            chat.Add(new TeamChatMessage
            {
                senderName = "Wonder Fixers",
                text = GameLocalization.Get("team_created_welcome"),
                timeLabel = GameLocalization.Get("team_time_now"),
            sentTicks = System.DateTime.UtcNow.Ticks,
            });
        }
        else
        {
            BuildChat();
            BuildRequests();
        }
    }

    private void BuildChat()
    {
        int count = Mathf.Min(4, members.Count);
        for (int i = 0; i < count; i++)
        {
            var m = members[Random.Range(0, members.Count)];
            chat.Add(new TeamChatMessage
            {
                senderName = m.displayName,
                text = GameLocalization.Get(ChatPool[Random.Range(0, ChatPool.Length)]),
                timeLabel = GameLocalization.GetFormat("progress_timer_days", Random.Range(1, 9)),
            });
        }
    }

    private void BuildRequests()
    {
        int count = Mathf.Min(Random.Range(1, 4), members.Count);
        for (int i = 0; i < count; i++)
        {
            var m = members[Random.Range(0, members.Count)];
            requests.Add(new TeamLifeRequest
            {
                requesterName = m.displayName,
                current = Random.Range(0, 4),
                needed = 5,
            });
        }
    }

    public TeamInfo GetTeamInfo() => info;
    public List<TeamChatMessage> GetChat() => chat;
    public List<TeamLifeRequest> GetLifeRequests() => requests;

    public bool Help(TeamLifeRequest request)
    {
        if (request == null) return false;
        request.current++;
        if (request.current >= request.needed)
        {
            requests.Remove(request);
            return true;
        }
        return false;
    }

    public void RequestLife()
    {
        if (!LifeInbox.Request(System.DateTime.UtcNow)) return;

        // Sohbette görünür geri bildirim (kendi tarafımda, sağda).
        chat.Add(new TeamChatMessage
        {
            senderName = PlayerProfile.PlayerName,
            text = "❤️ Can istedi!",
            timeLabel = GameLocalization.Get("team_time_now"),
            sentTicks = System.DateTime.UtcNow.Ticks,
            isMine = true,
        });
        OnChanged?.Invoke();
    }

    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        chat.Add(new TeamChatMessage
        {
            senderName = PlayerProfile.PlayerName,
            text = text.Trim(),
            timeLabel = GameLocalization.Get("team_time_now"),
            sentTicks = System.DateTime.UtcNow.Ticks,
            isMine = true,      // benim mesajım → sağda + avatarım sağda
        });
    }
}
