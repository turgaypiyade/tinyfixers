using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Takımların Firestore katmanı (Docs/ProductionPlan.md P3b): oluştur / katıl /
/// gerçek takım araması. Veri modeli:
///   teams/{id}: name, nameLower, emblemIndex, desc, minChapter, capacity,
///               botSeed (bot takımsa dizin index'i; oyuncu takımı -1),
///               botMembers (statik sim üye sayısı), realMembers (Increment),
///               createdBy, updatedAt
///   teams/{id}/members/{uid}: name, joinedAt
///   teams/{id}/chat/{msgId}: senderId, senderName, text, sentAt
///
/// Bot takımlar LAZY MATERIALIZE edilir: dizindeki bot takıma ilk gerçek oyuncu
/// katıldığında deterministik id'yle ("bot_{seed}") gerçek doküman oluşur — böylece
/// aynı bot takımı seçen iki gerçek oyuncu AYNI dokümanda buluşur.
/// Görünen üye sayısı = botMembers + realMembers.
///
/// Yazımlar OPTİMİSTİK uygulanır: yerel durum hemen değişir, Firestore offline
/// cache'i yazımı bağlantı gelince senkronlar (UX çevrimdışı da takılmaz).
/// </summary>
public static class FirebaseTeamCloud
{
    public const int Capacity = 50;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    public static DocumentReference TeamDoc(string teamId)
        => Db.Collection("teams").Document(teamId);

    /// <summary>Bot takımın deterministik doküman id'si (dizin seed'inden).</summary>
    public static string BotTeamId(int directorySeed) => "bot_" + directorySeed;

    // ── Oluştur ─────────────────────────────────────────────────────

    /// <summary>
    /// Yeni oyuncu takımı oluşturur (auto-id). Optimistik: id hemen döner, yazım
    /// arkaplanda senkronlanır.
    /// </summary>
    public static string CreateTeam(string name, int emblemIndex, string desc, int minChapter)
    {
        var doc = Db.Collection("teams").Document();   // auto-id

        var data = new Dictionary<string, object>
        {
            { "name", name },
            { "nameLower", name.ToLowerInvariant() },
            { "emblemIndex", emblemIndex },
            { "desc", desc ?? "" },
            { "minChapter", minChapter },
            { "capacity", Capacity },
            { "botSeed", -1L },
            { "botMembers", 0L },
            { "realMembers", FieldValue.Increment(1) },
            { "createdBy", FirebaseAuthService.UserId ?? "" },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        doc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(LogIfFailed("create"));
        AddSelfMember(doc);
        return doc.Id;
    }

    // ── Katıl ───────────────────────────────────────────────────────

    /// <summary>Var olan GERÇEK takıma katıl (dizin sorgusundan gelen id).</summary>
    public static void JoinRealTeam(string teamId)
    {
        var doc = TeamDoc(teamId);
        doc.SetAsync(new Dictionary<string, object>
        {
            { "realMembers", FieldValue.Increment(1) },
            { "updatedAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll).ContinueWithOnMainThread(LogIfFailed("join"));
        AddSelfMember(doc);
    }

    /// <summary>
    /// Bot takıma katıl → takımı gerçekleştir (lazy materialize). Statik alanlar
    /// deterministik olduğundan merge ile tekrar yazmak güvenlidir; realMembers
    /// yalnız Increment ile değişir.
    /// </summary>
    public static string JoinBotTeam(TeamDirectoryEntry entry, int directorySeed)
    {
        string teamId = BotTeamId(directorySeed);
        var doc = TeamDoc(teamId);

        var data = new Dictionary<string, object>
        {
            { "name", entry.name },
            { "nameLower", entry.name.ToLowerInvariant() },
            { "emblemIndex", entry.emblemSeed },
            { "desc", entry.description ?? "" },
            { "minChapter", entry.minChapter },
            { "capacity", entry.capacity },
            { "botSeed", (long)directorySeed },
            { "botMembers", (long)entry.members },
            { "realMembers", FieldValue.Increment(1) },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        doc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(LogIfFailed("join-bot"));
        AddSelfMember(doc);
        return teamId;
    }

    // ── Gerçek takım araması (tarayıcı harmanı için) ────────────────

    /// <summary>
    /// Firestore'daki gerçek takımları getirir. query boş → en güncel takımlar;
    /// dolu → nameLower prefix araması. Hata/auth-yok → boş liste.
    /// </summary>
    public static void QueryRealTeams(string query, int count, Action<List<TeamDirectoryEntry>> callback)
    {
        if (!FirebaseAuthService.IsReady)
        {
            callback?.Invoke(new List<TeamDirectoryEntry>());
            return;
        }

        Query q;
        if (!string.IsNullOrWhiteSpace(query))
        {
            string ql = query.Trim().ToLowerInvariant();
            q = Db.Collection("teams").OrderBy("nameLower").StartAt(ql).EndAt(ql + "\uf8ff");
        }
        else
        {
            q = Db.Collection("teams").OrderByDescending("updatedAt");
        }

        q.Limit(count).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            var list = new List<TeamDirectoryEntry>();
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning($"[TeamCloud] arama hatası: {task.Exception?.GetBaseException().Message}");
                callback?.Invoke(list);
                return;
            }

            foreach (var snap in task.Result.Documents)
            {
                long botMembers = snap.ContainsField("botMembers") ? snap.GetValue<long>("botMembers") : 0;
                long realMembers = snap.ContainsField("realMembers") ? snap.GetValue<long>("realMembers") : 0;
                list.Add(new TeamDirectoryEntry
                {
                    teamId = snap.Id,
                    name = snap.ContainsField("name") ? snap.GetValue<string>("name") : "Takım",
                    members = (int)(botMembers + realMembers),
                    capacity = snap.ContainsField("capacity") ? (int)snap.GetValue<long>("capacity") : Capacity,
                    emblemSeed = snap.ContainsField("emblemIndex") ? (int)snap.GetValue<long>("emblemIndex") : 0,
                    minChapter = snap.ContainsField("minChapter") ? (int)snap.GetValue<long>("minChapter") : 0,
                    description = snap.ContainsField("desc") ? snap.GetValue<string>("desc") : "",
                });
            }
            callback?.Invoke(list);
        });
    }

    // ── Sohbet ──────────────────────────────────────────────────────

    public static void SendChat(string teamId, string text)
    {
        if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(text)) return;

        TeamDoc(teamId).Collection("chat").AddAsync(new Dictionary<string, object>
        {
            { "senderId", FirebaseAuthService.UserId ?? "" },
            { "senderName", PlayerProfile.PlayerName },
            { "text", text.Trim() },
            { "sentAt", FieldValue.ServerTimestamp },
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Debug.LogWarning($"[TeamCloud] chat yazım hatası (offline olabilir, senkronlanacak): {task.Exception?.GetBaseException().Message}");
        });
    }

    // ── yardımcılar ─────────────────────────────────────────────────

    private static void AddSelfMember(DocumentReference teamDoc)
    {
        if (!FirebaseAuthService.IsReady) return;
        teamDoc.Collection("members").Document(FirebaseAuthService.UserId)
            .SetAsync(new Dictionary<string, object>
            {
                { "name", PlayerProfile.PlayerName },
                { "joinedAt", FieldValue.ServerTimestamp },
            }, SetOptions.MergeAll)
            .ContinueWithOnMainThread(LogIfFailed("member"));
    }

    private static Action<System.Threading.Tasks.Task> LogIfFailed(string op) => task =>
    {
        if (task.IsFaulted || task.IsCanceled)
            Debug.LogWarning($"[TeamCloud] {op} yazım hatası (offline olabilir, senkronlanacak): {task.Exception?.GetBaseException().Message}");
    };
}
