using System;
using System.Collections.Generic;

/// <summary>
/// Sahte liderlik verisi. Kendi satırın gerçek (PlayerProfile adı + yıldız puanı);
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

    public List<LeaderboardEntry> GetEntries(LeaderboardTab tab)
    {
        // Sekmeye göre tohum kaydır → her sekme farklı sıralama görünür.
        int seed = (int)tab * 7;
        int count = tab == LeaderboardTab.Friends ? 6 : 12;

        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < count; i++)
        {
            int score = 140 - i * 9 - (seed % 5) - (i * seed) % 7;
            list.Add(new LeaderboardEntry
            {
                rank = i + 1,
                playerName = Names[(i + seed) % Names.Length],
                subtitle = "Bölüm " + (200 - i * 3),
                score = UnityEngine.Mathf.Max(1, score),
            });
        }

        // Kendi satırını araya gerçek veriyle yerleştir (örn 5. sıra).
        int selfRank = UnityEngine.Mathf.Clamp(5, 1, list.Count);
        list[selfRank - 1] = new LeaderboardEntry
        {
            rank = selfRank,
            playerName = PlayerProfile.PlayerName,
            subtitle = "Sen",
            score = UnityEngine.Mathf.Max(1, PlayerWallet.TotalStars),
            isSelf = true,
        };
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
