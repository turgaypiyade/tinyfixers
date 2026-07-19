using System;
using System.Collections.Generic;
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
/// — Boot'ta (auth sonrası) kendi profilini upsert eder; cloud-save push döngüsü
///   değişiklikte tazeler (UpsertIfChanged).
/// — Arkadaş kodu araması BURADAN gerçek oyuncu bulur (bot değil).
/// Alanlar: name, nameLower, friendCode, chapter, region, avatarId, updatedAt.
/// </summary>
public static class PlayerDirectoryService
{
    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;
    private static string lastUpsertSignature;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
        => FirebaseAuthService.OnReady += () => UpsertIfChanged();

    /// <summary>Profil alanları son yazımdan beri değiştiyse players/{uid} dokümanını tazeler.</summary>
    public static void UpsertIfChanged()
    {
        if (!FirebaseAuthService.IsReady) return;

        string name = PlayerProfile.PlayerName;
        int chapter = Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1));
        int avatarId = PlayerPrefs.GetInt("player_avatar_id", 0);
        string region = DetectRegion();
        string code = FriendState.MyCode;

        string signature = $"{name}|{chapter}|{avatarId}|{region}|{code}";
        if (signature == lastUpsertSignature) return;
        lastUpsertSignature = signature;

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

        Db.Collection("players").Document(FirebaseAuthService.UserId)
          .SetAsync(doc, SetOptions.MergeAll)
          .ContinueWithOnMainThread(task =>
          {
              if (task.IsFaulted || task.IsCanceled)
              {
                  lastUpsertSignature = null;   // sonraki döngüde tekrar dene
                  Debug.LogWarning($"[PlayerDirectory] upsert hatası: {task.Exception?.GetBaseException().Message}");
              }
          });
    }

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
                Debug.LogWarning($"[PlayerDirectory] arama hatası: {task.Exception?.GetBaseException().Message}");
                callback?.Invoke(null);
                return;
            }

            foreach (var snap in task.Result.Documents)
            {
                callback?.Invoke(new RemotePlayer
                {
                    uid = snap.Id,
                    name = snap.ContainsField("name") ? snap.GetValue<string>("name") : "Oyuncu",
                    chapter = snap.ContainsField("chapter") ? (int)snap.GetValue<long>("chapter") : 1,
                    region = snap.ContainsField("region") ? snap.GetValue<string>("region") : "",
                    avatarId = snap.ContainsField("avatarId") ? (int)snap.GetValue<long>("avatarId") : 0,
                });
                return;
            }
            callback?.Invoke(null);
        });
    }

    /// <summary>Cihaz bölgesi ("TR" vb.) — liderlik Türkiye filtresi bunu kullanacak.</summary>
    public static string DetectRegion()
    {
        try
        {
            var region = System.Globalization.RegionInfo.CurrentRegion;
            if (region != null && !string.IsNullOrEmpty(region.TwoLetterISORegionName))
                return region.TwoLetterISORegionName.ToUpperInvariant();
        }
        catch { /* bazı platformlarda InvariantCulture → aşağıdaki dil fallback'i */ }

        return Application.systemLanguage == SystemLanguage.Turkish ? "TR" : "XX";
    }
}
