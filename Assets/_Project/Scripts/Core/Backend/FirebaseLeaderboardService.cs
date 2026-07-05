using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Hibrit liderlik panosu: yerel botlarla ANINDA dolar (gecikmesiz), arkasından gerçek
/// Firestore verisi (gerçek oyuncular) karışır. Weekly + Players destekli; Friends/Team boş.
/// Kendi skorun her zaman gösterilir (yerel), gerçek veri gelince güncellenir.
/// Skor kaynağı: PlayerWallet.TotalStars. Botlar Firestore'a YAZILMAZ (sadece görünüm).
/// </summary>
public sealed class FirebaseLeaderboardService : ILeaderboardService
{
    private const int TopCount = 100;
    private const int BotFillCount = 120;
    private const int TeamCapacity = 50;

    public event Action OnChanged;

    private readonly Dictionary<LeaderboardTab, List<LeaderboardEntry>> cache = new();

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    // subFilter (Dünya/Türkiye) v1'de yok sayılır — tek havuz. Bölgesel board sonra.
    public List<LeaderboardEntry> GetEntries(LeaderboardTab tab, int subFilter)
        => cache.TryGetValue(tab, out var list) ? list : new List<LeaderboardEntry>();

    public string[] GetSubFilters(LeaderboardTab tab) => tab switch
    {
        LeaderboardTab.Friends => new[] { "Arkadaş Listesi", "Arkadaş Ekle" },
        LeaderboardTab.Players => new[] { "Dünya", "Türkiye" },
        LeaderboardTab.Team    => new[] { "Dünya", "Türkiye" },
        _                      => System.Array.Empty<string>(),
    };

    public string GetTimeLabel(LeaderboardTab tab)
        => tab == LeaderboardTab.Weekly ? WeeklyRemainingLabel() : "";

    public void Fetch(LeaderboardTab tab)
    {
        // Takım sekmesi = takımların liderlik tablosu (yerel sim).
        if (tab == LeaderboardTab.Team)
        {
            cache[tab] = TeamBoard();
            OnChanged?.Invoke();
            return;
        }
        // Arkadaşlar = küçük bot listesi + sen (arkadaş sistemi gelene kadar).
        if (tab == LeaderboardTab.Friends)
        {
            cache[tab] = FriendsBoard();
            OnChanged?.Invoke();
            return;
        }

        // 1) Anında yerel botlar + kendi satırın → boş/geç görünmesin.
        cache[tab] = Rank(Merge(new List<LeaderboardEntry>()));
        OnChanged?.Invoke();

        // 2) Auth hazır değilse hazır olunca gerçek veriyi çek.
        if (!FirebaseAuthService.IsReady)
        {
            FirebaseAuthService.OnReady += () => FetchReal(tab);
            return;
        }
        FetchReal(tab);
    }

    // Gerçek Firestore verisini çek, kendi skorunu yaz, botlarla birleştir.
    private void FetchReal(LeaderboardTab tab)
    {
        string path = tab == LeaderboardTab.Weekly
            ? $"leaderboards/{WeekId()}/scores"
            : "leaderboards/global/scores";
        var col = Db.Collection(path);

        WriteOwnScore(col).ContinueWithOnMainThread(_ =>
        {
            col.OrderByDescending("score").Limit(TopCount).GetSnapshotAsync()
               .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[Leaderboard] okuma hatası ({tab}): {task.Exception}");
                    return;
                }
                cache[tab] = Rank(Merge(ParseOthers(task.Result)));
                OnChanged?.Invoke();
            });
        });
    }

    // Gerçek(diğerleri) + botlar + kendi satırın → tek liste (henüz sırasız).
    private List<LeaderboardEntry> Merge(List<LeaderboardEntry> realOthers)
    {
        var list = new List<LeaderboardEntry>();
        list.AddRange(realOthers);
        list.AddRange(BuildBots());
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = "Sen",
            score = PlayerWallet.TotalStars,
            isSelf = true,
            chapter = PlayerPrefs.GetInt("current_level", 1),
        });
        return list;
    }

    // Skora göre sırala, rank ata, top-N al (kendi satırın top dışındaysa sona ekle).
    private static List<LeaderboardEntry> Rank(List<LeaderboardEntry> list)
    {
        list.Sort((a, b) => b.score.CompareTo(a.score));
        for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;

        if (list.Count <= TopCount) return list;

        var top = list.GetRange(0, TopCount);
        var self = list.Find(e => e.isSelf);
        if (self != null && !top.Contains(self)) top.Add(self);   // kendi satırın hep görünür
        return top;
    }

    private static List<LeaderboardEntry> ParseOthers(QuerySnapshot snapshot)
    {
        var list = new List<LeaderboardEntry>();
        foreach (var doc in snapshot.Documents)
        {
            if (doc.Id == FirebaseAuthService.UserId) continue;   // kendi satırını yerelden ekliyoruz
            string name = doc.ContainsField("name") ? doc.GetValue<string>("name") : "Oyuncu";
            long score  = doc.ContainsField("score") ? doc.GetValue<long>("score") : 0;
            list.Add(new LeaderboardEntry { playerName = name, subtitle = "", score = (int)score });
        }
        return list;
    }

    // Dolgu botları — sabit isim (index), gerçek zamanla ilerleyen skor (BotProgression).
    private List<LeaderboardEntry> BuildBots()
    {
        var list = new List<LeaderboardEntry>(BotFillCount);
        for (int i = 0; i < BotFillCount; i++)
        {
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.PlayerAt(i),
                subtitle = "",
                score = BotProgression.WeeklyScore(i),
            });
        }
        return list;
    }

    // Takım liderlik tablosu: 50 takım (sabit isim, ilerleyen skor) + senin takımın (vurgulu).
    private List<LeaderboardEntry> TeamBoard()
    {
        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < 100; i++)
        {
            int members = Mathf.Min(TeamCapacity, BotProgression.TeamMembers(i));
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.TeamAt(i),
                subtitle = members + "/" + TeamCapacity,
                score = BotProgression.TeamWeeklyScore(i),
                capacityCurrent = members,
                capacityMax = TeamCapacity,
            });
        }
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerTeamState.TeamName,
            subtitle = "Senin takımın",
            score = BotProgression.TeamWeeklyScore(9999),
            isSelf = true,
            capacityCurrent = 6,
            capacityMax = TeamCapacity,
        });
        return Rank(list);
    }

    // Arkadaşlar: küçük bot listesi (ayrı index aralığı) + sen.
    private List<LeaderboardEntry> FriendsBoard()
    {
        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < 15; i++)
        {
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.PlayerAt(1000 + i),
                subtitle = "",
                score = BotProgression.WeeklyScore(1000 + i),
            });
        }
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = "Sen",
            score = PlayerWallet.TotalStars,
            isSelf = true,
        });
        return Rank(list);
    }

    private static Task WriteOwnScore(CollectionReference col)
    {
        var data = new Dictionary<string, object>
        {
            { "name",      PlayerProfile.PlayerName },
            { "score",     PlayerWallet.TotalStars },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
        return col.Document(FirebaseAuthService.UserId).SetAsync(data, SetOptions.MergeAll);
    }

    private static string WeekId()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{now.Year}-W{week:00}";
    }

    private static string WeeklyRemainingLabel()
    {
        var now = DateTime.UtcNow;
        int daysToMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (daysToMonday == 0) daysToMonday = 7;
        var end = now.Date.AddDays(daysToMonday);
        var rem = end - now;
        return $"{(int)rem.TotalDays}g {rem.Hours}s";
    }
}
