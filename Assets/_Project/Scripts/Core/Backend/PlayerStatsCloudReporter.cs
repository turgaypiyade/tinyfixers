using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Oyuncu başına oyun istatistiği → Firestore (Firebase Console'dan oyuncu oyuncu görülür).
/// Yalnız oyuncunun kendi alanına yazar (users/{uid}/..., rules: sahibi-only; "players" herkese açık olduğu için
/// oraya oynama süresi YAZILMAZ).
///
///   users/{uid}/stats/summary   — currentLevel, totalPlaySeconds, sessions, attemptsStarted, levelsWon,
///                                 levelsGivenUp, coins, firstLaunchAt, lastSeenAt, platform, appVersion
///   users/{uid}/levels/L00100   — level, attempts, wins, giveUps, playSeconds, wonOnAttempt, lastMovesLeft,
///                                 lastMovesTotal, maxAssistTier, lastPlayedAt
///
/// Sayaçlar FieldValue.Increment ile yazılır (offline'da Firestore SDK kuyruklar, bağlanınca gönderir).
/// Auth hazır olmadan gelen kayıtlar bellekte bekler, OnReady'de gönderilir.
/// Tüm oyuncuların TOPLU level istatistiği (zorluk ayarı) için asıl kaynak Firebase Analytics'tir (GameAnalytics).
/// </summary>
public static class PlayerStatsCloudReporter
{
    private const float NewSessionAfterBackgroundSeconds = 30f * 60f;

    private static readonly List<Action> pending = new();
    private static float s_unflushedPlaySeconds;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;
    private static DocumentReference UserDoc => Db.Collection("users").Document(FirebaseAuthService.UserId);
    private static DocumentReference SummaryDoc => UserDoc.Collection("stats").Document("summary");
    private static DocumentReference LevelDoc(int level) => UserDoc.Collection("levels").Document($"L{level:D5}");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;

        var go = new GameObject("[PlayerStatsCloudReporter]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Hook>();

        LevelAttemptStats.OnAttemptStarted += HandleAttemptStarted;
        LevelAttemptStats.OnAttemptEnded += HandleAttemptEnded;
        FirebaseAuthService.OnReady += FlushPending;

        CountSession();
    }

    // ── Olaylar ─────────────────────────────────────────────────────

    private static void CountSession()
    {
        Enqueue(() => SummaryDoc.SetAsync(new Dictionary<string, object>
        {
            { "sessions", FieldValue.Increment(1) },
            { "firstLaunchAt", Timestamp.FromDateTime(PlayerStats.FirstLaunchDate.ToUniversalTime()) },
            { "platform", Application.platform.ToString() },
            { "appVersion", Application.version },
            { "currentLevel", CurrentLevel.Global },
            { "lastSeenAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll));
    }

    private static void HandleAttemptStarted(int level, int attempt)
    {
        Enqueue(() => LevelDoc(level).SetAsync(new Dictionary<string, object>
        {
            { "level", level },
            { "attempts", FieldValue.Increment(1) },
            { "lastPlayedAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll));

        Enqueue(() => SummaryDoc.SetAsync(new Dictionary<string, object>
        {
            { "attemptsStarted", FieldValue.Increment(1) },
            { "currentLevel", level },
            { "lastSeenAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll));
    }

    private static void HandleAttemptEnded(LevelAttemptResult r)
    {
        var levelData = new Dictionary<string, object>
        {
            { "level", r.Level },
            { r.Won ? "wins" : "giveUps", FieldValue.Increment(1) },
            { "playSeconds", FieldValue.Increment(r.Seconds) },
            { "lastPlayedAt", FieldValue.ServerTimestamp },
        };
        if (r.Won)
        {
            levelData["wonOnAttempt"] = r.Attempt;
            levelData["lastMovesLeft"] = r.MovesLeft;
        }
        if (r.AssistTier > 0)
            levelData["maxAssistTier"] = r.AssistTier;
        if (r.MovesTotal > 0)
            levelData["lastMovesTotal"] = r.MovesTotal;   // uzaktan hamle ayarı değişince hangi ayarla oynandığı
        Enqueue(() => LevelDoc(r.Level).SetAsync(levelData, SetOptions.MergeAll));

        Enqueue(() => SummaryDoc.SetAsync(new Dictionary<string, object>
        {
            { r.Won ? "levelsWon" : "levelsGivenUp", FieldValue.Increment(1) },
            { "currentLevel", r.Won ? r.Level + 1 : r.Level },
            { "coins", PlayerWallet.Coins },
            { "lastSeenAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll));

        FlushPlayTime();
    }

    // Toplam oynama süresi: uygulama ön plandayken geçen süre; arka plana geçişte ve level sonunda yazılır.
    private static void FlushPlayTime()
    {
        int seconds = Mathf.FloorToInt(s_unflushedPlaySeconds);
        if (seconds <= 0) return;
        s_unflushedPlaySeconds -= seconds;
        Enqueue(() => SummaryDoc.SetAsync(new Dictionary<string, object>
        {
            { "totalPlaySeconds", FieldValue.Increment(seconds) },
            { "lastSeenAt", FieldValue.ServerTimestamp },
        }, SetOptions.MergeAll));
    }

    // ── Gönderim ────────────────────────────────────────────────────

    private static void Enqueue(Action write)
    {
        pending.Add(write);
        FlushPending();
    }

    private static void FlushPending()
    {
        if (!FirebaseAuthService.IsReady || string.IsNullOrEmpty(FirebaseAuthService.UserId)) return;
        var batch = pending.ToArray();
        pending.Clear();
        foreach (var write in batch)
        {
            try { write(); }
            catch (Exception e) { Debug.LogWarning($"[StatsReporter] yazım hatası: {e.Message}"); }
        }
    }

    private sealed class Hook : MonoBehaviour
    {
        private DateTime backgroundedAtUtc = DateTime.MinValue;

        private void Update() => s_unflushedPlaySeconds += Mathf.Min(Time.unscaledDeltaTime, 0.5f);

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                backgroundedAtUtc = DateTime.UtcNow;
                FlushPlayTime();
                return;
            }
            if (backgroundedAtUtc != DateTime.MinValue
                && (DateTime.UtcNow - backgroundedAtUtc).TotalSeconds >= NewSessionAfterBackgroundSeconds)
                CountSession();
            backgroundedAtUtc = DateTime.MinValue;
        }

        private void OnApplicationQuit() => FlushPlayTime();
    }
}
