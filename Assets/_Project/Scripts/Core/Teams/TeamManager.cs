using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Takım sistemini yönetir.
/// — İlk açılışta otomatik takımlar oluşturur ve botlarla doldurur
/// — Gerçek kullanıcılar geldikçe takımlara eklenir
/// — Diğer event türlerinde (takım puanı vb.) ortak kullanılır
/// </summary>
public class TeamManager : MonoBehaviour
{
    [SerializeField] private BotConfig botConfig;

    private BotService botService;
    private readonly List<Team> teams = new();
    private const string KeyTeamsInitialized = "teams_initialized";

    public IReadOnlyList<Team> Teams => teams;

    // -----------------------------------------------------------------------

    private void Awake()
    {
        botService = new BotService(botConfig);
        botService.Initialize();
    }

    private void Start()
    {
        if (!PlayerPrefs.HasKey(KeyTeamsInitialized))
            InitializeTeams();
        else
            LoadTeams();
    }

    // -----------------------------------------------------------------------
    // Başlatma

    private void InitializeTeams()
    {
        teams.Clear();

        for (int i = 0; i < botConfig.initialTeamCount; i++)
        {
            var team = new Team
            {
                teamId   = $"team_{i:D3}",
                teamName = GenerateTeamName(i),
                score    = 0
            };

            var bots = botService.GetRandomBots(botConfig.teamMemberCount);
            foreach (var bot in bots)
            {
                bot.teamId = team.teamId;
                team.memberIds.Add(bot.botId);
            }

            teams.Add(team);
        }

        PlayerPrefs.SetInt(KeyTeamsInitialized, 1);
        PlayerPrefs.Save();

        Debug.Log($"[TeamManager] {teams.Count} takım oluşturuldu, " +
                  $"her birinde {botConfig.teamMemberCount} üye.");
    }

    private void LoadTeams()
    {
        // Şimdilik her açılışta yeniden üretiyoruz.
        // Sunucu entegrasyonunda burası RemoteTeamService'ten çekilecek.
        InitializeTeams();
    }

    // -----------------------------------------------------------------------
    // Takım işlemleri

    /// <summary>Gerçek kullanıcıyı en uygun takıma atar.</summary>
    public Team AssignPlayerToTeam(string playerId, string displayName)
    {
        // En az üyeli takımı bul
        Team target = null;
        int  min    = int.MaxValue;

        foreach (var t in teams)
        {
            if (t.memberIds.Count < min)
            {
                min    = t.memberIds.Count;
                target = t;
            }
        }

        if (target == null) return null;

        target.memberIds.Add(playerId);
        Debug.Log($"[TeamManager] {displayName} → {target.teamName} takımına eklendi.");
        return target;
    }

    public Team GetTeam(string teamId)
    {
        foreach (var t in teams)
            if (t.teamId == teamId) return t;
        return null;
    }

    public void AddScore(string teamId, int points)
    {
        var team = GetTeam(teamId);
        if (team == null) return;
        team.score += points;
    }

    // -----------------------------------------------------------------------
    // İsim üretimi

    private static readonly string[] TeamAdjectives =
        { "Hızlı", "Güçlü", "Cesur", "Usta", "Çelik", "Parlak", "Demir", "Altın" };

    private static readonly string[] TeamNouns =
        { "Tamirciler", "Ustalar", "Kahraman", "Ekip", "Takım", "Birlik", "Klan" };

    private static string GenerateTeamName(int index)
    {
        string adj  = TeamAdjectives[index % TeamAdjectives.Length];
        string noun = TeamNouns[(index / TeamAdjectives.Length) % TeamNouns.Length];
        return $"{adj} {noun} #{index + 1}";
    }
}

// -----------------------------------------------------------------------

[Serializable]
public class Team
{
    public string      teamId;
    public string      teamName;
    public int         score;
    public List<string> memberIds = new();
}
