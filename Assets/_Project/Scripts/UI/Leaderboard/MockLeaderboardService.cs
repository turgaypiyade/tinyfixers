using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahte liderlik verisi. Kendi satırın gerçek (PlayerProfile adı + toplam puan);
/// gerisi sabit isim havuzundan üretilir. Backend gelince bu sınıf değişir, controller değil.
/// </summary>
public sealed class MockLeaderboardService : ILeaderboardService
{
    public event Action OnChanged;

    // Mock veri zaten senkron (GetEntries anında döner); Fetch sadece render tetikler.
    public void Fetch(LeaderboardTab tab) => OnChanged?.Invoke();

    private static readonly string[] Names =
    {
        "BulgariA", "RavenSKull", "govind uikey", "booodats", "Yigefep", "carole",
        "SinanOzcan", "Yusuf", "Alex", "tuurr", "mehmet42", "ZeynepX"
    };

    private static readonly string[] TeamNames =
    {
        "BESIKTAS", "Badboyz", "TURKIYEe", "ASiLZADE", "FENERBAHCE", "GALATASARAYaile",
        "East Coast", "all for you", "Together again", "Minions", "ALPHA TEAM", "Indiana"
    };

    public string[] GetSubFilters(LeaderboardTab tab) => tab switch
    {
        LeaderboardTab.Friends => new[] { "Arkadaş Listesi", "Arkadaş Ekle" },
        LeaderboardTab.Players => new[] { "Dünya", "Türkiye" },
        LeaderboardTab.Team    => new[] { "Dünya", "Türkiye" },
        _                      => Array.Empty<string>(),
    };

    public List<LeaderboardEntry> GetEntries(LeaderboardTab tab, int subFilter)
    {
        if (tab == LeaderboardTab.Team)
            return TeamEntries(subFilter);

        // Sekme + alt-filtre tohumu → her görünüm farklı liste.
        int seed = (int)tab * 7 + subFilter * 13;
        int count = tab == LeaderboardTab.Friends ? 6 : 12;

        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < count; i++)
        {
            // Gerçekçi puan aralığı (yıldız değil, oyun-içi puan). Oyuncu ilerledikçe
            // TotalScore büyüyüp bu rakiplerin arasından yukarı tırmanır.
            int score = 18000 - i * 1500 - (seed * 137) % 1000;
            list.Add(new LeaderboardEntry
            {
                rank = i + 1,
                playerName = Names[(i + seed) % Names.Length],
                subtitle = subFilter == 1 ? "Türkiye" : "Dünya",
                score = Mathf.Max(500, score),
                chapter = 4400 - (i * 37 + seed) % 900,
            });
        }

        // Kendi satırını gerçek toplam puanla EKLE (sabit sıraya sokma!).
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = "Sen",
            score = Mathf.Max(1, PlayerWallet.TotalScore),
            isSelf = true,
            chapter = PlayerPrefs.GetInt("current_level", 1),
        });

        // Puana göre sırala (yüksek → düşük) ve sıra numaralarını yeniden ver.
        // Böylece self, gerçek puanının hak ettiği yerde görünür (backend de böyle döner).
        list.Sort((a, b) => b.score.CompareTo(a.score));
        for (int i = 0; i < list.Count; i++)
            list[i].rank = i + 1;

        return list;
    }

    private List<LeaderboardEntry> TeamEntries(int subFilter)
    {
        int seed = 3 + subFilter * 11;
        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < 10; i++)
        {
            int members = 50 - (i + seed) % 4;
            list.Add(new LeaderboardEntry
            {
                rank = i + 1,
                playerName = TeamNames[(i + seed) % TeamNames.Length],
                subtitle = "",
                score = 241103 - i * 17022 - (seed * 913) % 4000,
                capacityCurrent = members,
                capacityMax = 50,
            });
        }

        // Kendi takımın: alt sıralarda, yeşil vurgulu (pinned satır controller'da).
        list.Add(new LeaderboardEntry
        {
            rank = 667,
            playerName = PlayerTeamState.TeamName,
            subtitle = "Senin takımın",
            score = 2458,
            isSelf = true,
            capacityCurrent = 6,
            capacityMax = 50,
        });
        return list;
    }

    public string GetTimeLabel(LeaderboardTab tab)
    {
        return tab switch
        {
            LeaderboardTab.Weekly => "2g 20s",
            LeaderboardTab.Team   => "5g 4s",
            _ => ""
        };
    }
}
