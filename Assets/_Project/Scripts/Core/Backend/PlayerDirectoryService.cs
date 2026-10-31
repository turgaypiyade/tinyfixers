using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>Dizinde bulunan GERÇEK bir oyuncunun özeti (ID araması sonucu).</summary>
public sealed class RemotePlayer
{
    public string uid;
    public string name;
    public int chapter;
    public string region;
    public int avatarId;
}

/// <summary>
/// Gerçek oyuncu dizini (Docs/ProductionPlan.md P3): players/{uid} dokümanı.
/// — Auth + restore tamamlandıktan sonra profil/skor değişiminde tazelenir;
///   mevcut cloud-save tikinde başarısız bağımsız yazımlar yeniden denenir.
/// — Arkadaş kodu araması BURADAN gerçek oyuncu bulur (bot değil).
/// Alanlar: name, nameLower, friendCode, chapter, region, avatarId, updatedAt.
/// </summary>
public static class PlayerDirectoryService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;
    private static string lastUpsertSignature;
    private static bool upsertInFlight, friendsRefreshInFlight;
    private static float nextFriendsRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        FirebaseAuthService.OnReady -= UpsertIfChanged;
        FirebaseAuthService.OnReady += UpsertIfChanged;
        FirebaseCloudSaveService.OnRestored -= UpsertIfChanged;
        FirebaseCloudSaveService.OnRestored += UpsertIfChanged;
        PlayerProfile.OnChanged -= UpsertIfChanged;
        PlayerProfile.OnChanged += UpsertIfChanged;
        PlayerWallet.OnTotalScoreChanged -= OnScoreChanged;
        PlayerWallet.OnTotalScoreChanged += OnScoreChanged;
    }

    private static void OnScoreChanged(int _) => UpsertIfChanged();

    /// <summary>Profil alanları son yazımdan beri değiştiyse players/{uid} dokümanını tazeler.</summary>
    public static void UpsertIfChanged()
    {
        if (RuntimeSimulationSession.IsActive || !FirebaseAuthService.IsReady
            || !FirebaseCloudSaveService.RestoreResolved || FirebaseCloudSaveService.SuspendForDeletion) return;

        // Independent projections must also retry when the directory itself is unchanged.
        FirebaseLeaderboardService.PublishOwnScoreIfChanged();
        FirebaseTeamCloud.SyncSelfProfileIfChanged();
        if (upsertInFlight) return;
        string uid = FirebaseAuthService.UserId;
        string name = PlayerProfile.PlayerName;
        int chapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1));
        int avatarId = PlayerPrefs.GetInt("player_avatar_id", 0);
        string region = DetectRegion();
        string code = FriendState.MyCode;

        string signature = $"{uid}|{name}|{chapter}|{avatarId}|{region}|{code}";
        if (signature == lastUpsertSignature) return;
        upsertInFlight = true;

        var doc = new Dictionary<string, object>
        {
            { "name", name },
            { "nameLower", name.ToLowerInvariant() },
            { "friendCode", code },
            { "chapter", chapter },
            { "region", region },
            { "avatarId", avatarId },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        Db.Collection("players").Document(uid)
          .SetAsync(doc, SetOptions.MergeAll)
          .ContinueWithOnMainThread(task =>
          {
              upsertInFlight = false;
              if (task.IsFaulted || task.IsCanceled)
              {
                  if (GameDiagnostics.Backend)
                      Debug.LogWarning($"[PlayerDirectory] upsert hatası: {task.Exception?.GetBaseException().Message}");
                  return;
              }
              lastUpsertSignature = signature; // only acknowledged writes suppress retries
              UpsertIfChanged(); // coalesce profile edits made while this write was pending
          });
    }

    /// <summary>Refresh existing friends by stable UID; never adds strangers or removes on read failure.</summary>
    public static void RefreshFriends()
    {
        if (RuntimeSimulationSession.IsActive || !FirebaseAuthService.IsReady || friendsRefreshInFlight
            || Time.realtimeSinceStartup < nextFriendsRefresh) return;
        var friends = FriendState.RealFriends;
        if (friends.Count == 0) return;
        friendsRefreshInFlight = true;
        nextFriendsRefresh = Time.realtimeSinceStartup + 15f;
        string owner = FirebaseAuthService.UserId;
        var reads = new Task<DocumentSnapshot>[friends.Count];
        for (int i = 0; i < friends.Count; i++)
            reads[i] = Db.Collection("players").Document(friends[i].uid).GetSnapshotAsync();
        // One failed profile must not discard successful refreshes for other friends.
        Task.WhenAll(reads).ContinueWithOnMainThread(_ =>
        {
            if (FirebaseAuthService.UserId == owner)
            {
                var updates = new List<RemotePlayer>(reads.Length);
                foreach (var read in reads)
                    if (read.Status == TaskStatus.RanToCompletion && read.Result.Exists)
                        updates.Add(Parse(read.Result));
                FriendState.RefreshRealFriends(updates);
            }
            friendsRefreshInFlight = false;
        });
    }

    private static RemotePlayer Parse(DocumentSnapshot snap) => new RemotePlayer
    {
        uid = snap.Id,
        name = snap.ContainsField("name") ? snap.GetValue<string>("name") : "Oyuncu",
        chapter = snap.ContainsField("chapter") ? (int)snap.GetValue<long>("chapter") : 1,
        region = snap.ContainsField("region") ? snap.GetValue<string>("region") : "",
        avatarId = snap.ContainsField("avatarId") ? (int)snap.GetValue<long>("avatarId") : 0,
    };

    /// <summary>
    /// Arkadaş koduyla GERÇEK oyuncu arar. callback(null) = bulunamadı/hata.
    /// Kendi kodun da null döner (kendini ekleyemezsin).
    /// </summary>
    public static void FindByFriendCode(string code, Action<RemotePlayer> callback)
    {
        if (!FirebaseAuthService.IsReady || string.IsNullOrWhiteSpace(code))
        {
            callback?.Invoke(null);
            return;
        }

        code = code.Trim().ToUpperInvariant();
        if (code == FriendState.MyCode)
        {
            callback?.Invoke(null);
            return;
        }

        Db.Collection("players").WhereEqualTo("friendCode", code).Limit(1)
          .GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (GameDiagnostics.Backend)
                    Debug.LogWarning($"[PlayerDirectory] arama hatası: {task.Exception?.GetBaseException().Message}");
                callback?.Invoke(null);
                return;
            }

            foreach (var snap in task.Result.Documents)
            {
                callback?.Invoke(Parse(snap));
                return;
            }
            callback?.Invoke(null);
        });
    }

    /// <summary>Cihaz bölgesi ("TR" vb.) — liderlik Türkiye filtresi bunu kullanacak.</summary>
    public static string DetectRegion() => DeviceCountry.Get();
}
