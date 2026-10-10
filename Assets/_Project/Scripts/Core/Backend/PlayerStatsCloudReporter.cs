using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Durable, compact per-player/per-level statistics. Production: users/{uid}/levels/L00126;
/// development: users/{uid}/levels/D_L00126 (explicitly excluded from production reports).
/// A device-local sequence checkpoint and the level/summary updates commit in ONE transaction.
/// Replaying an unacknowledged payload after app restart is therefore safe.
/// </summary>
public static class PlayerStatsCloudReporter
{
    private const float NewSessionAfterBackgroundSeconds = 30f * 60f;
    private static float unflushedSeconds, retryAfter;
    private static bool sending, ownerMismatchLogged;
    private static int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        LevelTelemetryStore.RecoverInterrupted();
        LevelTelemetryStore.Session();
        var go = new GameObject("[PlayerStatsCloudReporter]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Hook>();
        FirebaseAuthService.OnReady -= FlushPending;
        FirebaseAuthService.OnReady += FlushPending;
    }

    private static void FlushPlayTime()
    {
        int seconds = Mathf.FloorToInt(unflushedSeconds);
        if (seconds <= 0) return;
        unflushedSeconds -= seconds;
        LevelTelemetryStore.ForegroundTime(seconds);
    }

    private static void FlushPending()
    {
        if (RuntimeSimulationSession.IsActive || sending || !FirebaseAuthService.IsReady
            || Time.realtimeSinceStartup < retryAfter) return;
        string uid = FirebaseAuthService.UserId;
        if (!LevelTelemetryStore.BindOwner(uid))
        {
            if (!ownerMismatchLogged)
                Debug.LogWarning("[StatsReporter] Pending stats belong to a different account; retaining them locally.");
            ownerMismatchLogged = true;
            return;
        }
        var payload = LevelTelemetryStore.Next();
        if (payload == null) return;
        sending = true;
        string stream = LevelTelemetryStore.StreamId;
        int coins = PlayerWallet.Coins;
        var firstLaunch = Timestamp.FromDateTime(PlayerStats.FirstLaunchDate.ToUniversalTime());
        var db = FirebaseFirestore.DefaultInstance;
        var user = db.Collection("users").Document(uid);
        var cursor = user.Collection("statsStreams").Document(stream);
        var summary = user.Collection("stats").Document(payload.isDevelopment ? "summary_dev" : "summary");
        var level = payload.level > 0 ? user.Collection("levels").Document(
            (payload.isDevelopment ? "D_" : "") + $"L{payload.level:D5}") : null;
        try
        {
            db.RunTransactionAsync(async transaction =>
            {
                var checkpoint = await transaction.GetSnapshotAsync(cursor);
                if (checkpoint.Exists && checkpoint.TryGetValue("sequence", out long committed)
                    && committed >= payload.sequence) return;
                var summarySnapshot = await transaction.GetSnapshotAsync(summary);
                var summaryData = LevelTelemetryProjection.Summary(payload, Data(summarySnapshot));
                Dictionary<string, object> levelData = null;
                if (level != null)
                {
                    var levelSnapshot = await transaction.GetSnapshotAsync(level);
                    levelData = LevelTelemetryProjection.Level(payload, Data(levelSnapshot));
                    levelData["lastPlayedAt"] = FieldValue.ServerTimestamp;
                }
                // All reads precede all writes; callbacks may be retried by Firestore.
                summaryData["lastSeenAt"] = FieldValue.ServerTimestamp;
                summaryData["coins"] = coins;
                if (!summarySnapshot.Exists || !summarySnapshot.ContainsField("firstLaunchAt"))
                    summaryData["firstLaunchAt"] = firstLaunch;
                if (levelData != null) transaction.Set(level, levelData, SetOptions.MergeAll);
                transaction.Set(summary, summaryData, SetOptions.MergeAll);
                transaction.Set(cursor, new Dictionary<string, object> { ["sequence"] = payload.sequence });
            }).ContinueWithOnMainThread(task =>
            {
                sending = false;
                if (task.IsCanceled || task.IsFaulted)
                {
                    Failed(task.Exception?.GetBaseException().Message ?? "cancelled");
                    return;
                }
                LevelTelemetryStore.Acknowledge(payload.sequence);
                failures = 0;
                retryAfter = 0f;
                FlushPending();
            });
        }
        catch (Exception e)
        {
            sending = false;
            Failed(e.Message);
        }
    }

    private static Dictionary<string, object> Data(DocumentSnapshot snapshot)
        => snapshot.Exists ? snapshot.ToDictionary() : new Dictionary<string, object>();

    private static void Failed(string message)
    {
        failures = Math.Min(failures + 1, 6);
        retryAfter = Time.realtimeSinceStartup + Math.Min(300, 5 * (1 << failures));
        Debug.LogWarning($"[StatsReporter] Upload deferred; saved data will retry: {message}");
    }

    private sealed class Hook : MonoBehaviour
    {
        private DateTime backgroundedAtUtc = DateTime.MinValue;
        private float elapsed;

        private void Update()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.5f);
            unflushedSeconds += delta;
            elapsed += delta;
            if (elapsed < 15f) return;
            elapsed = 0f;
            FlushPlayTime();
            FlushPending();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                backgroundedAtUtc = DateTime.UtcNow;
                FlushPlayTime();
                FlushPending();
                return;
            }
            if (backgroundedAtUtc != DateTime.MinValue
                && (DateTime.UtcNow - backgroundedAtUtc).TotalSeconds >= NewSessionAfterBackgroundSeconds)
                LevelTelemetryStore.Session();
            backgroundedAtUtc = DateTime.MinValue;
            FlushPending();
        }

        private void OnApplicationQuit() => FlushPlayTime();
    }
}
