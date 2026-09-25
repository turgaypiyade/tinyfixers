using System.Collections.Generic;

/// <summary>
/// Sahte takım verisi. Sohbet + can istekleri sabit havuzdan; "Yardım" mock olarak ilerletir.
/// Backend gelince bu sınıf değişir, TeamScreenController değil.
/// </summary>
public sealed class MockTeamService : ITeamService
{
    public event System.Action OnChanged;
    public TeamLifeInbox LifeInbox { get; }

    public MockTeamService()
    {
        LifeInbox = new TeamLifeInbox("mock:" + info.teamName, () => OnChanged?.Invoke());
        LifeInbox.SetBots(3, () => "Alex");
    }

    private readonly TeamInfo info = new()
    {
        teamName = "curvealanoglari",
        giftCurrent = 35,
        giftTarget = 100,
        timerLabel = "2g 20s",
        missionText = "kazanmak için bir göreve BAŞLA",
    };

    private readonly List<TeamChatMessage> chat = new()
    {
        new TeamChatMessage { senderName = "SinanOzcan", text = "selamun aleykum gencler", timeLabel = "3g 21s" },
        new TeamChatMessage { senderName = "Yusuf",      text = "slm",                      timeLabel = "3g 21s" },
        new TeamChatMessage { senderName = "Alex",       text = "bugün etkinlik var mı?",    timeLabel = "2g 10s" },
    };

    private readonly List<TeamLifeRequest> requests = new()
    {
        new TeamLifeRequest { requesterName = "jxjdjdjdjdj", current = 2, needed = 5 },
        new TeamLifeRequest { requesterName = "Alex",        current = 0, needed = 5 },
    };

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

        // Sohbette de görünsün (kendi tarafımda, sağda).
        chat.Add(new TeamChatMessage
        {
            senderName = PlayerProfile.PlayerName,
            text = "❤️ Can istedi!",
            timeLabel = "şimdi",
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
            timeLabel = "şimdi",
            sentTicks = System.DateTime.UtcNow.Ticks,
            isMine = true,
        });
    }
}
