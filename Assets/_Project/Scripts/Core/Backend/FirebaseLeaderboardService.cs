using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Hibrit liderlik panosu: yerel botlarla ANINDA dolar (gecikmesiz), arkasından gerçek
/// Firestore verisi (gerçek oyuncular) karışır. Weekly/Players gerçek skorlar; Friends UID profil yenilemesi, Team yerel dolgu.
/// Kendi skorun her zaman gösterilir (yerel), gerçek veri gelince güncellenir.
/// Skor kaynağı: PlayerWallet.TotalScore. Botlar Firestore'a YAZILMAZ (sadece görünüm).
/// </summary>
public sealed class FirebaseLeaderboardService : ILeaderboardService
{
    private const int TopCount = 100;
    private const int TeamCapacity = 50;
    private readonly HashSet<LeaderboardTab> pendingAuthTabs = new();
    private readonly Dictionary<(LeaderboardTab tab, int sub), int> fetchVersions = new();
    private static string lastPublishedSignature;
    private static bool publishing;


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
    private const int MasterBotBase = 60000;   // lig botları: ayrı isim aralığı (haftalık/oyuncu botlarıyla çakışmaz)
    private const int MasterSub = 1;           // Haftalık sekmesinin 2. alt seçeneği = Usta Ligi
    private static int RegionTeamBase => 300 + RegionSeed * 120;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    public List<LeaderboardEntry> GetEntries(LeaderboardTab tab, int subFilter)
        => cache.TryGetValue((tab, subFilter), out var list) ? list : new List<LeaderboardEntry>();

    public string[] GetSubFilters(LeaderboardTab tab) => tab switch
    {
        LeaderboardTab.Friends => new[] { GameLocalization.Get("leaderboard_friend_list"), GameLocalization.Get("leaderboard_add_friend") },
        LeaderboardTab.Players => new[] { GameLocalization.Get("leaderboard_world"), RegionLabel },
        LeaderboardTab.Team    => new[] { GameLocalization.Get("leaderboard_world"), RegionLabel },
        // Usta Ligi'ne ulaşan oyuncu Haftalık sekmesinde iki tablo görür: genel haftalık + lig haftalığı.
        LeaderboardTab.Weekly when MasterLeague.Reached
            => new[] { GameLocalization.Get("leaderboard_sub_weekly"), GameLocalization.Get("leaderboard_sub_master") },
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
            PlayerDirectoryService.RefreshFriends();
            cache[(tab, 0)] = FriendsBoard();
            cache[(tab, 1)] = new List<LeaderboardEntry>();
            OnChanged?.Invoke();
            return;
        }

        // 1) Anında yerel botlar + kendi satırın → boş/geç görünmesin.
        //    Dünya (0) ve Türkiye (1) ayrı bot havuzlarından.
        cache[(tab, 0)] = Rank(Merge(new List<LeaderboardEntry>(), region: 0, tab));
        cache[(tab, 1)] = tab == LeaderboardTab.Weekly
            ? RankMaster(MergeMaster(new List<LeaderboardEntry>()))
            : Rank(Merge(new List<LeaderboardEntry>(), region: 1, tab));
        OnChanged?.Invoke();

        // 2) Dünya ve ülke ayrı gerçek skor kayıtlarını okur.
        //    Auth hazır değilse hazır olunca çek.
        if (!FirebaseAuthService.IsReady)
        {
            if (pendingAuthTabs.Add(tab) && pendingAuthTabs.Count == 1)
                FirebaseAuthService.OnReady += OnAuthReady;
            return;
        }
        FetchReal(tab);
    }

    private void OnAuthReady()
    {
        FirebaseAuthService.OnReady -= OnAuthReady;
        var tabs = new List<LeaderboardTab>(pendingAuthTabs);
        pendingAuthTabs.Clear();
        foreach (var tab in tabs) FetchReal(tab);
    }

    // Reads must not wait for an acknowledged write: offline/pending writes can take minutes.
    private void FetchReal(LeaderboardTab tab)
    {
        PublishOwnScoreIfChanged();
        string path = tab == LeaderboardTab.Weekly
            ? $"leaderboards/{WeekId()}/scores" : "leaderboards/global/scores";
        FetchBoard(tab, 0, path);
        if (tab == LeaderboardTab.Players)
            FetchBoard(tab, 1, $"leaderboards/region_{RegionCode}/scores");
        if (tab == LeaderboardTab.Weekly && MasterLeague.Reached)
            FetchBoard(tab, MasterSub, $"leaderboards/{MasterBoardId()}/scores");
    }

    private void FetchBoard(LeaderboardTab tab, int sub, string path)
    {
        var key = (tab, sub);
        int version = fetchVersions.TryGetValue(key, out int previous) ? previous + 1 : 1;
        fetchVersions[key] = version;
        string owner = FirebaseAuthService.UserId;
        Db.Collection(path).OrderByDescending("score").Limit(TopCount).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (FirebaseAuthService.UserId != owner || fetchVersions[key] != version) return;
                if (task.IsFaulted || task.IsCanceled)
                {
                    if (GameDiagnostics.Backend)
                        Debug.LogWarning($"[Leaderboard] read deferred ({tab}/{sub}): {task.Exception?.GetBaseException().Message}");
                    return;
                }
                var others = ParseOthers(task.Result);
                cache[key] = tab == LeaderboardTab.Weekly && sub == MasterSub
                    ? RankMaster(MergeMaster(others)) : Rank(Merge(others, region: sub, tab));
                OnChanged?.Invoke();
            });
    }

    // Gerçek(diğerleri) + botlar + kendi satırın → tek liste (henüz sırasız).
    private List<LeaderboardEntry> Merge(List<LeaderboardEntry> realOthers, int region, LeaderboardTab tab)
    {
        var list = new List<LeaderboardEntry>();
        list.AddRange(realOthers);
        list.AddRange(BuildBots(region));
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = GameLocalization.Get("leaderboard_you"),
            // Haftalık sekme yalnız bu haftanın puanını yarıştırır (botlar da haftalık puanla gelir).
            score = tab == LeaderboardTab.Weekly ? PlayerWallet.WeeklyScore : PlayerWallet.TotalScore,
            isSelf = true,
            chapter = PlayerPrefs.GetInt("current_level", 1),
        });
        return list;
    }

    // Usta Ligi haftalık tablosu: gerçek lig oyuncuları + lig botları + sen (lig puanınla). Ligde oynamayan (puanı 0)
    // oyuncu da kendini görür (0 puanla en altta) — tabloya katılmak için lig seviyesi kazanmak yeter.
    private List<LeaderboardEntry> MergeMaster(List<LeaderboardEntry> realOthers)
    {
        int botCount = Mathf.Clamp(BotPopulation.ActiveCount / 30, 120, 500);   // lig nüfusu ana nüfusun küçük bir kesri
        var list = new List<LeaderboardEntry>(realOthers.Count + botCount + 1);
        list.AddRange(realOthers);
        for (int i = 0; i < botCount; i++)
        {
            int score = BotProgression.MasterWeeklyScore(MasterBotBase + i);
            if (score <= 0) continue;   // haftaya henüz katılmamış bot görünmez
            list.Add(new LeaderboardEntry
            {
                playerName = NamePool.PlayerAt(MasterBotBase + i),
                subtitle = "",
                score = score,
            });
        }
        list.Add(new LeaderboardEntry
        {
            playerName = PlayerProfile.PlayerName,
            subtitle = GameLocalization.Get("leaderboard_you"),
            score = MasterLeague.WeeklyPoints,
            isSelf = true,
        });
        return list;
    }

    // Lig tablosu sıralanınca kendi sıran lig dönemi özetine bildirilir (puanın varken).
    private static List<LeaderboardEntry> RankMaster(List<LeaderboardEntry> list)
    {
        var ranked = Rank(list);
        var self = ranked.Find(e => e.isSelf);
        if (self != null && self.score > 0) MasterLeague.ReportRank(self.rank);
        return ranked;
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
                subtitle = GameLocalization.Get("leaderboard_your_team"),
                // Sim: takım puanı = senin BU HAFTAKİ puanın (gerçek üye toplamı backend'le gelecek). Rakip takımlar
                // da haftalık puanla (TeamWeeklyScore) yarıştığı için toplam puan kullanılmaz (2026-11-02).
                score = PlayerWallet.WeeklyScore,
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
            subtitle = PlayerTeamState.HasTeam ? PlayerTeamState.TeamName : GameLocalization.Get("leaderboard_you"),
            chapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1)),
            score = 0,
            isSelf = true,
        });

        // Bölüm'e göre sırala (yüksek → düşük); rank buradan.
        list.Sort((a, b) => b.chapter.CompareTo(a.chapter));
        for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
        return list;
    }

    /// <summary>Publish on profile/score changes and retry from the shared save tick, even if ranks is never opened.</summary>
    public static void PublishOwnScoreIfChanged()
    {
        if (RuntimeSimulationSession.IsActive || !FirebaseAuthService.IsReady
            || !FirebaseCloudSaveService.RestoreResolved || FirebaseCloudSaveService.SuspendForDeletion || publishing) return;
        string uid = FirebaseAuthService.UserId, name = PlayerProfile.PlayerName;
        string week = WeekId(), region = PlayerDirectoryService.DetectRegion();
        int score = PlayerWallet.TotalScore, weekly = PlayerWallet.WeeklyScore, master = MasterLeague.WeeklyPoints;
        string signature = $"{uid}|{week}|{region}|{name}|{score}|{weekly}|{master}";
        if (lastPublishedSignature == signature) return;
        publishing = true;
        var data = new Dictionary<string, object>
        {
            { "name", name },
            { "score", score },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
        // Same permitted score schema on all boards; no rules/index deployment required.
        var batch = Db.StartBatch();
        batch.Set(Db.Collection("leaderboards/global/scores").Document(uid), data, SetOptions.MergeAll);
        // Haftalık tabloya bu haftanın puanı (toplam değil).
        var weeklyData = new Dictionary<string, object>
        {
            { "name", name },
            { "score", weekly },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
        batch.Set(Db.Collection($"leaderboards/{week}/scores").Document(uid), weeklyData, SetOptions.MergeAll);
        batch.Set(Db.Collection($"leaderboards/region_{region}/scores").Document(uid), data, SetOptions.MergeAll);
        // Usta Ligi haftalık tablosu: yalnız bu hafta lig puanı alan oyuncular yazılır (aynı izinli şema, kural değişmez).
        if (master > 0)
        {
            var masterData = new Dictionary<string, object>
            {
                { "name", name },
                { "score", master },
                { "updatedAt", FieldValue.ServerTimestamp },
            };
            batch.Set(Db.Collection($"leaderboards/{MasterBoardId()}/scores").Document(uid), masterData, SetOptions.MergeAll);
        }
        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            publishing = false;
            if (task.IsFaulted || task.IsCanceled)
            {
                if (GameDiagnostics.Backend)
                    Debug.LogWarning($"[Leaderboard] publish deferred: {task.Exception?.GetBaseException().Message}");
                return;
            }
            lastPublishedSignature = signature;
            PublishOwnScoreIfChanged();
        });
    }

    /// <summary>
    /// Geçmiş bir haftanın Usta Ligi kesin sırası: 1 + (hafta sonu puanı daha yüksek lig botu) + (Firestore'da puanı daha
    /// yüksek gerçek oyuncu). Gerçek sayım ödül sınırının biraz üstünde kesilir (sıra yalnız ödül kademesi için lazım).
    /// Ağ/kimlik hazır değilse done(-1) → ödül sonraki açılışta yeniden denenir (botlarla tahmin VERİLMEZ).
    /// </summary>
    public static void FetchMasterFinalRank(string weekId, int points, Action<int> done)
    {
        if (!FirebaseAuthService.IsReady || string.IsNullOrEmpty(weekId)) { done(-1); return; }
        int botsAbove = 0;
        int botCount = Mathf.Clamp(BotPopulation.ActiveCount / 30, 120, 500);
        for (int i = 0; i < botCount; i++)
            if (BotProgression.MasterFinalScore(MasterBotBase + i, weekId) > points) botsAbove++;

        string owner = FirebaseAuthService.UserId;
        Db.Collection($"leaderboards/master_{weekId}/scores").WhereGreaterThan("score", points)
            .Limit(MasterLeagueRewards.RewardedRankLimit + 10).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (FirebaseAuthService.UserId != owner || task.IsFaulted || task.IsCanceled)
                {
                    if (GameDiagnostics.Backend)
                        Debug.LogWarning($"[Leaderboard] master final rank deferred: {task.Exception?.GetBaseException().Message}");
                    done(-1);
                    return;
                }
                int realAbove = 0;
                foreach (var doc in task.Result.Documents)
                    if (doc.Id != owner) realAbove++;
                done(1 + botsAbove + realAbove);
            });
    }

    internal static string CurrentWeekId => WeekId();

    private static string MasterBoardId() => "master_" + WeekId();

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
