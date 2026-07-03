using System.Collections.Generic;
using UnityEngine;

/// <summary>Takım üst bilgisi: amblem, isim, hediye ilerlemesi, sayaç, görev metni.</summary>
public sealed class TeamInfo
{
    public string teamName = "Takım";
    public Sprite emblem;
    public int memberCount;
    public int memberCapacity = 50;
    public int giftCurrent;
    public int giftTarget = 100;
    public string timerLabel = "2g 20s";
    public string missionText = "kazanmak için bir göreve BAŞLA";

    /// <summary>Üye doluluk etiketi, örn "40/50".</summary>
    public string MemberLabel => memberCount + "/" + memberCapacity;

    public float GiftProgress01 => giftTarget <= 0 ? 0f : Mathf.Clamp01((float)giftCurrent / giftTarget);
}

/// <summary>Takım sohbetinde tek mesaj.</summary>
public sealed class TeamChatMessage
{
    public string senderName;
    public Sprite avatar;
    public string text;
    public string timeLabel;
}

/// <summary>Bir takım üyesinin can isteği (current/needed).</summary>
public sealed class TeamLifeRequest
{
    public string requesterName;
    public Sprite avatar;
    public int current;
    public int needed = 5;

    public float Progress01 => needed <= 0 ? 0f : Mathf.Clamp01((float)current / needed);
}

/// <summary>
/// Takım verisi kaynağı. v1'de MockTeamService; backend gelince aynı arayüze gerçek
/// implementasyon (UGS/Firebase realtime) takılır — controller değişmez.
/// </summary>
public interface ITeamService
{
    TeamInfo GetTeamInfo();
    List<TeamChatMessage> GetChat();
    List<TeamLifeRequest> GetLifeRequests();

    /// <summary>Bir can isteğine yardım et (mock: current++). true = isteği karşıladı/sildi.</summary>
    bool Help(TeamLifeRequest request);
    void RequestLife();
    void SendMessage(string text);
}
