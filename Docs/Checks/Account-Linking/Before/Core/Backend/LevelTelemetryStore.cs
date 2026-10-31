using System;
using System.Collections.Generic;
using UnityEngine;

// Durable, device-local outbox. Intentionally NOT in CloudSaveManifest: copying the stream
// and its acknowledgements between devices would duplicate attribution. A level's pending
// changes are compacted into one summary, not a growing list of individual attempts.
public static class LevelTelemetryStore
{
    private const string Key = "level_telemetry_outbox_v2";
    private static LevelTelemetryState state;
    private static LevelTelemetryState State => state ??= Load();
    public static string StreamId => State.streamId;
    public static string OwnerId => State.ownerId;
    internal static LevelTelemetryAttempt CurrentAttempt => State.active;

    private static LevelTelemetryState Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        var loaded = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<LevelTelemetryState>(json);
        if (loaded == null) return new LevelTelemetryState { streamId = Guid.NewGuid().ToString("N") };
        // Unity serialization may materialize empty nested objects for null fields.
        if (loaded.active != null && loaded.active.level <= 0) loaded.active = null;
        if (loaded.inFlight != null && loaded.inFlight.sequence <= 0) loaded.inFlight = null;
        return loaded;
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(State));
        PlayerPrefs.Save();
    }

    public static bool BindOwner(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return false;
        if (!string.IsNullOrEmpty(State.ownerId)) return State.ownerId == uid;
        State.ownerId = uid;
        Save();
        return true;
    }

    private static LevelTelemetryBatch Pending(int level, LevelTelemetryAttempt attempt = null)
    {
        bool development = attempt != null ? attempt.isDevelopment : Debug.isDebugBuild;
        string version = attempt != null ? attempt.appVersion : Application.version;
        var batch = State.pending.Find(p => p.level == level && p.isDevelopment == development && p.appVersion == version);
        if (batch == null)
        {
            batch = new LevelTelemetryBatch { level = level, isDevelopment = development,
                appVersion = version, platform = Application.platform.ToString(),
                firstObservedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            State.pending.Add(batch);
        }
        batch.lastObservedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        batch.currentLevel = CurrentLevel.Global;
        return batch;
    }

    public static void Begin(int level, int attempt, int giveUpsBefore, int strugglesBefore, int tier, int moves)
    {
        if (RuntimeSimulationSession.IsActive) return;
        RecoverInterrupted();
        State.active = new LevelTelemetryAttempt { level = level, attempt = attempt,
            giveUpsBefore = giveUpsBefore, strugglesBefore = strugglesBefore, assistTier = tier, movesTotal = moves,
            isDevelopment = Debug.isDebugBuild, appVersion = Application.version };
        var b = Pending(level);
        b.attempts++;
        if (b.firstObservedAttempt == 0) b.firstObservedAttempt = attempt;
        b.highestAttempt = Math.Max(b.highestAttempt, attempt);
        b.lastMovesTotal = moves;
        b.maxAssistTier = Math.Max(b.maxAssistTier, tier);
        if (tier > 0) b.assistedAttempts++;
        Save();
    }

    // A pause is not abandonment. Only an old attempt replaced by a fresh board / process
    // is classified as interrupted; it never changes mercy or the explicit give-up counter.
    public static void RecoverInterrupted()
    {
        if (RuntimeSimulationSession.IsActive || State.active == null) return;
        Pending(State.active.level, State.active).interruptedAttempts++;
        State.active = null;
        Save();
    }

    public static void Struggle()
    {
        if (RuntimeSimulationSession.IsActive || State.active == null || State.active.struggled) return;
        State.active.struggled = true;
        Pending(State.active.level, State.active).struggles++;
        Save();
    }

    public static void Continue(int moves, int coins, bool rewardedAd)
    {
        if (RuntimeSimulationSession.IsActive || State.active == null || moves <= 0) return;
        var a = State.active;
        a.continues++;
        a.extraMoves += moves;
        a.coinsSpent += Math.Max(0, coins);
        if (rewardedAd) a.adContinues++;
        var b = Pending(a.level, a);
        b.continues++;
        b.extraMoves += moves;
        b.coinsSpent += Math.Max(0, coins);
        if (rewardedAd) b.adContinues++;
        Save();
    }

    public static void Checkpoint(int seconds)
    {
        if (RuntimeSimulationSession.IsActive || State.active == null) return;
        int delta = Math.Max(0, seconds - State.active.seconds);
        if (delta == 0) return;
        State.active.seconds += delta;
        Pending(State.active.level, State.active).playSeconds += delta;
        Save();
    }

    public static void End(bool won, int movesLeft, int seconds)
    {
        if (RuntimeSimulationSession.IsActive || State.active == null) return;
        Checkpoint(seconds);
        var a = State.active;
        var b = Pending(a.level, a);
        if (won)
        {
            b.wins++;
            if (b.firstWinAttempt == 0)
            {
                b.firstWinAttempt = a.attempt;
                b.winGiveUpsBefore = a.giveUpsBefore;
                b.winStrugglesBefore = a.strugglesBefore;
                b.winStruggled = a.struggled;
                b.winContinues = a.continues;
                b.winExtraMoves = a.extraMoves;
                b.winCoinsSpent = a.coinsSpent;
                b.winAdContinues = a.adContinues;
                b.winAssistTier = a.assistTier;
                b.winSeconds = a.seconds;
                b.winMovesLeft = Math.Max(0, movesLeft);
                b.winMovesTotal = a.movesTotal;
                b.firstWonAt = b.lastObservedAt;
            }
        }
        else b.giveUps++;
        State.active = null;
        Save();
    }

    public static void Session()
    {
        if (RuntimeSimulationSession.IsActive) return;
        Pending(0).sessions++;
        Save();
    }

    public static void ForegroundTime(int seconds)
    {
        if (RuntimeSimulationSession.IsActive || seconds <= 0) return;
        Pending(0).foregroundSeconds += seconds;
        Save();
    }

    // The frozen in-flight payload survives both crashes and ambiguous network acknowledgements.
    // New gameplay writes to pending summaries; it can never mutate a payload being retried.
    public static LevelTelemetryBatch Next()
    {
        if (State.inFlight != null) return State.inFlight;
        if (State.pending.Count == 0) return null;
        State.inFlight = State.pending[0];
        State.pending.RemoveAt(0);
        State.inFlight.sequence = State.nextSequence++;
        Save();
        return State.inFlight;
    }

    public static void Acknowledge(long sequence)
    {
        if (State.inFlight == null || State.inFlight.sequence != sequence) return;
        State.inFlight = null;
        Save();
    }
}

[Serializable]
public sealed class LevelTelemetryState
{
    public string streamId, ownerId;
    public long nextSequence = 1;
    public LevelTelemetryAttempt active;
    public LevelTelemetryBatch inFlight;
    public List<LevelTelemetryBatch> pending = new List<LevelTelemetryBatch>();
}

[Serializable]
public sealed class LevelTelemetryAttempt
{
    public int level, attempt, giveUpsBefore, strugglesBefore, assistTier, movesTotal, seconds;
    public int continues, extraMoves, coinsSpent, adContinues;
    public bool struggled, isDevelopment;
    public string appVersion;
}

[Serializable]
public sealed class LevelTelemetryBatch
{
    public long sequence, firstObservedAt, lastObservedAt, firstWonAt;
    public int level, currentLevel, firstObservedAttempt, highestAttempt, lastMovesTotal, maxAssistTier;
    public long attempts, wins, giveUps, struggles, interruptedAttempts, assistedAttempts;
    public long playSeconds, continues, extraMoves, coinsSpent, adContinues, sessions, foregroundSeconds;
    public int firstWinAttempt, winGiveUpsBefore, winStrugglesBefore, winContinues, winExtraMoves;
    public int winCoinsSpent, winAdContinues, winAssistTier, winSeconds, winMovesLeft, winMovesTotal;
    public bool winStruggled, isDevelopment;
    public string appVersion, platform;
}
