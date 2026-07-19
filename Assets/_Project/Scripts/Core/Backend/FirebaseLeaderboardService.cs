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
/// Skor kaynağı: PlayerWallet.TotalScore. Botlar Firestore'a YAZILMAZ (sadece görünüm).
/// </summary>
public sealed class FirebaseLeaderboardService : ILeaderboardService
{
    private const int TopCount = 100;
    private const int TeamCapacity = 50;

    public event Action OnChanged;

    // Cache anahtarı (sekme, alt-filtre): Dünya/Türkiye ve Liste/Ekle ayrı listeler.
    private readonly Dictionary<(LeaderboardTab tab, int sub), List<LeaderboardEntry>> cache = new();

    // ── Ülke sekmesi: sabit "Türkiye" DEĞİL, oyuncunun KENDİ ülkesi (players.region ile
    // aynı kaynak: PlayerDirectoryService.DetectRegion). Etiket ülkenin yerel adı
    // (TR→"Türkiye", DE→"Deutschland"); bot havuzu ülke koduna göre deterministik ayrışır —
    // her ülke kendi bot evrenini görür, tüm cihazlarda aynı.
    private static string regionCode;
    private static string regionLabel;

    private static string RegionCode => regionCode ??= PlayerDirectoryService.DetectRegion();

    private static string RegionLabel
    {
        get
        {
            if (regionLabel != null) return regionLabel;
            try { regionLabel = new System.Globalization.RegionInfo(RegionCode).NativeName; }
            catch { regionLabel = RegionCode; }
            if (string.IsNullOrWhiteSpace(regionLabel)) regionLabel = RegionCode;
            return regionLabel;
        }
    }

    // Ülke koduna deterministik bot-havuzu tabanı (string.GetHashCode process'e göre
    // değişebilir — cihazlar arası tutarlılık için basit char hash).
    private static int RegionSeed
    {
        get
        {
            int h = 0;
            foreach (char c in RegionCode) h = h * 31 + c;
            return ((h % 40) + 40) % 40;
        }
    }

    private static int RegionPlayerBase => 3000 + RegionSeed * 500;
    private static int RegionTeamBase => 300 + RegionSeed * 120;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    public List<LeaderboardEntry> GetEntries(LeaderboardTab tab, int subFilter)
        => cache.TryGetValue((tab, subFilter), out var list) ? list : new List<LeaderboardEntry>();

    public string[] GetSubFilters(LeaderboardTab tab) => tab switch
    {
        LeaderboardTab.Friends => new[] { "Arkadaş Listesi", "Arkadaş Ekle" },
        LeaderboardTab.Players => new[] { "Dünya", RegionLabel },
        LeaderboardTab.Team    => new[] { "Dünya", RegionLabel },
        _                      => System.Array.Empty<string>(),
    };

    public string GetTimeLabel(LeaderboardTab tab)
        => tab == LeaderboardTab.Weekly ? WeeklyRemainingLabel() : "";

    public void Fetch(LeaderboardTab tab)
    {
        // Takım sekmesi = takımların liderlik tablosu (yerel sim, Dünya + Türkiye ayrı).
        if (tab == LeaderboardTab.Team)
        {
            cache[(tab, 0)] = TeamBoard(region: 0);
            cache[(tab, 1)] = TeamBoard(region: 1);
            OnChanged?.Invoke();
            return;
        }
        // Arkadaşlar = GERÇEK arkadaş listen (FriendState). "Ekle" alt-görünümü liste kullanmaz.
        if (tab == LeaderboardTab.Friends)
        {
            cache[(tab, 0)] = FriendsBoard();
            cache[(tab, 1)] = new List<LeaderboardEntry>();
            OnChanged?.Invoke();
            return;
        }

        // 1) Anında yerel botlar + kendi satırın → boş/geç görünmesin.
        //    Dünya (0) ve Türkiye (1) ayrı bot havuzlarından.
        cache[(tab, 0)] = Rank(Merge(new List<LeaderboardEntry>(), region: 0));
        cache[(tab, 1)] = Rank(Merge(new List<LeaderboardEntry>(), region: 1));
        OnChanged?.Invoke();

        // 2) Gerçek Firestore verisi yalnız Dünya havuzuna karışır (bölgesel board sonra).
        //    Auth hazır değilse hazır olunca çek.
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
                cache[(tab, 0)] = Rank(Merge(ParseOthers(task.Result), region: 0));
                OnChanged?.Invoke();
            });
        });
    }

    // Gerçek(diğerleri) + botlar + kendi satırın → tek liste (henüz sırasız).
    private List<LeaderboardEntry> Merge(List<LeaderboardEntry> realOthers, int region)
    {
        var list = new List<LeaderboardEntry>();
        list.AddRange(realOthers);
        list.AddRange(BuildBots(region));
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = "Sen",
            score = PlayerWallet.TotalScore,
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

        // Görülen gerçek oyuncu sayısı bot evrenini otomatik küçültür (launch kuralı).
        BotPopulation.ReportRealUsers(list.Count);
        return list;
    }

    // Dolgu botları — sabit isim (index), gerçek zamanla ilerleyen skor (BotProgression).
    // Evren boyutu BotPopulation'dan (launch ~15k, gerçek kullanıcı geldikçe azalır) —
    // sıra numaraları bu evrene göre gerçekçi çıkar (örn. #6543). Liste top-100'e kesilir.
    // region: 0=Dünya (0..N), 1=Türkiye (ayrı index aralığı → farklı isimler/skorlar).
    private List<LeaderboardEntry> BuildBots(int region)
    {
        int baseIndex = region == 1 ? RegionPlayerBase : 0;
        int count = BotPopulation.ActiveCount;
        var list = new List<LeaderboardEntry>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.PlayerAt(baseIndex + i),
                subtitle = "",
                score = BotProgression.WeeklyScore(baseIndex + i),
            });
        }
        return list;
    }

    // Takım liderlik tablosu: takımlar (sabit isim, ilerleyen skor) + senin takımın (vurgulu).
    // region: 0=Dünya, 1=Türkiye (ayrı takım havuzu). Takımsızken self satırı basılmaz.
    private List<LeaderboardEntry> TeamBoard(int region)
    {
        int baseIndex = region == 1 ? RegionTeamBase : 0;
        // Takım evreni bot nüfusuyla ölçeklenir (~40 üye/takım varsayımı).
        int teamCount = Mathf.Max(150, BotPopulation.ActiveCount / 40);
        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < teamCount; i++)
        {
            int members = Mathf.Min(TeamCapacity, BotProgression.TeamMembers(baseIndex + i));
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.TeamAt(baseIndex + i),
                subtitle = members + "/" + TeamCapacity,
                score = BotProgression.TeamWeeklyScore(baseIndex + i),
                capacityCurrent = members,
                capacityMax = TeamCapacity,
            });
        }

        if (PlayerTeamState.HasTeam)
        {
            list.Add(new LeaderboardEntry
            {
                playerName = PlayerTeamState.TeamName,
                subtitle = "Senin takımın",
                // Sim: takım puanı = senin toplam puanın (gerçek üye toplamı backend'le gelecek).
                // Böylece oynadıkça takım skorun büyür ve puanın teams sekmesinde de görünür.
                score = PlayerWallet.TotalScore,
                isSelf = true,
                capacityCurrent = PlayerTeamState.IsCreator ? 1 : 6,
                capacityMax = TeamCapacity,
            });
        }
        return Rank(list);
    }

    // Arkadaşlar (Liste): GERÇEK arkadaşların (FriendState) + sen; Bölüm'e göre sıralı
    // (referans RM ekranı — puan yerine bölüm yarışı). Arkadaş yoksa yalnız sen kalırsın;
    // controller o durumda öneri görünümünü açar.
    private List<LeaderboardEntry> FriendsBoard()
    {
        var list = new List<LeaderboardEntry>();

        // GERÇEK arkadaşlar (ID aramasıyla eklenen oyuncular) — bölümleri dizinden geldi.
        foreach (var rf in FriendState.RealFriends)
        {
            list.Add(new LeaderboardEntry
            {
                playerName = rf.name,
                subtitle = "",
                chapter = Mathf.Max(1, rf.chapter),
                score = 0,
            });
        }

        // Bot arkadaşlar (öneri kartlarından eklenenler).
        foreach (var name in FriendState.Friends)
        {
            int hash = Mathf.Abs(name.GetHashCode());
            list.Add(new LeaderboardEntry
            {
                playerName = name,
                subtitle = NamePool.TeamAt(hash % 500),   // arkadaşın takımı (alt-isim)
                chapter = FriendDirectory.ChapterOf(name),
                score = 0,
            });
        }
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = PlayerTeamState.HasTeam ? PlayerTeamState.TeamName : "Sen",
            chapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1)),
            score = 0,
            isSelf = true,
        });

        // Bölüm'e göre sırala (yüksek → düşük); rank buradan.
        list.Sort((a, b) => b.chapter.CompareTo(a.chapter));
        for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
        return list;
    }

    private static Task WriteOwnScore(CollectionReference col)
    {
        var data = new Dictionary<string, object>
        {
            { "name",      PlayerProfile.PlayerName },
            { "score",     PlayerWallet.TotalScore },
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
