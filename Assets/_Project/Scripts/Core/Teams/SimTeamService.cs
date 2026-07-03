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
    private readonly TeamInfo info;
    private readonly List<BotPlayer> members = new();
    private readonly List<TeamChatMessage> chat = new();
    private readonly List<TeamLifeRequest> requests = new();

    private static readonly string[] ChatPool =
    {
        "selam gençler", "günaydın", "bugün etkinlik var mı?", "yardım lazım arkadaşlar",
        "teşekkürler!", "harika oynadınız", "kim aktif?", "bu level çok zor ya",
        "can atabilecek var mı?", "iyi oyunlar herkese", "az kaldı, devam!", "süpersiniz 💪"
    };

    public SimTeamService()
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

        info = new TeamInfo
        {
            teamName = PlayerTeamState.TeamName,   // liderlik panosuyla aynı takım adı
            memberCount = members.Count,
            memberCapacity = 50,
            giftCurrent = Random.Range(20, 90),
            giftTarget = 100,
            timerLabel = "2g 20s",
            missionText = "kazanmak için bir göreve BAŞLA",
        };

        BuildChat();
        BuildRequests();
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
                text = ChatPool[Random.Range(0, ChatPool.Length)],
                timeLabel = Random.Range(1, 9) + "g",
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
        if (!requests.Exists(r => r.requesterName == PlayerProfile.PlayerName))
            requests.Add(new TeamLifeRequest { requesterName = PlayerProfile.PlayerName, current = 0, needed = 5 });
    }

    public void SendMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        chat.Add(new TeamChatMessage { senderName = PlayerProfile.PlayerName, text = text.Trim(), timeLabel = "şimdi" });
    }
}
