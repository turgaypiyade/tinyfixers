using System;
using System.Collections.Generic;

// Pure projection used inside a Firestore transaction; no mutable client state or side effects.
public static class LevelTelemetryProjection
{
    public static long Number(IDictionary<string, object> data, string key)
        => data.TryGetValue(key, out var value) && value != null ? Convert.ToInt64(value) : 0;

    private static bool Flag(IDictionary<string, object> data, string key)
        => data.TryGetValue(key, out var value) && value is bool b && b;

    private static void Add(Dictionary<string, object> result, IDictionary<string, object> old, string key, long delta)
        => result[key] = Number(old, key) + delta;

    public static Dictionary<string, object> Level(LevelTelemetryBatch b, IDictionary<string, object> old)
    {
        bool previousWin = Flag(old, "hasWon") || Number(old, "wins") > 0 || Number(old, "wonOnAttempt") > 0;
        bool tracked = Number(old, "measurementVersion") == 2;
        var data = new Dictionary<string, object>
        {
            ["measurementVersion"] = 2,
            ["level"] = b.level,
            ["isDevelopment"] = b.isDevelopment,
            ["appVersion"] = b.appVersion,
            ["platform"] = b.platform,
            ["highestAttempt"] = Math.Max(Number(old, "highestAttempt"), b.highestAttempt),
            ["maxAssistTier"] = Math.Max(Number(old, "maxAssistTier"), b.maxAssistTier),
            ["hasWon"] = previousWin || b.wins > 0,
            // Legacy / mid-level installs must not enter first-attempt denominators.
            ["trackingFromAttemptOne"] = tracked ? Flag(old, "trackingFromAttemptOne")
                : b.firstObservedAttempt == 1 && Number(old, "attempts") == 0
                  && Number(old, "wins") == 0 && Number(old, "giveUps") == 0,
        };
        if (!tracked)
        {
            data["firstObservedAttempt"] = b.firstObservedAttempt;
            data["firstObservedAtUnix"] = b.firstObservedAt;
            data["firstAppVersion"] = b.appVersion;
        }
        if (b.lastMovesTotal > 0) data["lastMovesTotal"] = b.lastMovesTotal;
        Add(data, old, "attempts", b.attempts);
        Add(data, old, "wins", b.wins);
        Add(data, old, "giveUps", b.giveUps);
        Add(data, old, "struggles", b.struggles);
        Add(data, old, "interruptedAttempts", b.interruptedAttempts);
        Add(data, old, "assistedAttempts", b.assistedAttempts);
        Add(data, old, "playSeconds", b.playSeconds);
        Add(data, old, "continues", b.continues);
        Add(data, old, "extraMoves", b.extraMoves);
        Add(data, old, "continueCoinsSpent", b.coinsSpent);
        Add(data, old, "adContinues", b.adContinues);
        data["lastObservedAtUnix"] = Math.Max(Number(old, "lastObservedAtUnix"), b.lastObservedAt);

        // Freeze the FIRST completion. Replaying a completed level must never move its win bucket.
        if (!previousWin && b.firstWinAttempt > 0)
        {
            data["firstWinAttempt"] = b.firstWinAttempt;
            data["wonOnAttempt"] = b.firstWinAttempt; // compatibility with existing player views
            data["firstWinBucket"] = WinBucket(b.firstWinAttempt);
            data["firstWonAtUnix"] = b.firstWonAt;
            data["firstWinAppVersion"] = b.appVersion;
            data["firstWinGiveUpsBefore"] = b.winGiveUpsBefore;
            data["firstWinStrugglesBefore"] = b.winStrugglesBefore;
            data["firstWinStruggled"] = b.winStruggled;
            data["firstWinContinues"] = b.winContinues;
            data["firstWinExtraMoves"] = b.winExtraMoves;
            data["firstWinCoinsSpent"] = b.winCoinsSpent;
            data["firstWinAdContinues"] = b.winAdContinues;
            data["firstWinAssistTier"] = b.winAssistTier;
            data["firstWinSeconds"] = b.winSeconds;
            data["firstWinMovesLeft"] = b.winMovesLeft;
            data["firstWinMovesTotal"] = b.winMovesTotal;
            data["firstWinWithoutContinueOrMercy"] = b.winContinues == 0 && b.winAssistTier == 0;
            data["lastMovesLeft"] = b.winMovesLeft;
        }
        return data;
    }

    public static string WinBucket(int attempt) => attempt <= 5 ? attempt.ToString()
        : attempt <= 10 ? "6_10" : "11_plus";

    public static Dictionary<string, object> Summary(LevelTelemetryBatch b, IDictionary<string, object> old)
    {
        var data = new Dictionary<string, object>
        {
            ["measurementVersion"] = 2,
            ["currentLevel"] = Math.Max(Number(old, "currentLevel"), b.firstWinAttempt > 0 ? b.level + 1 : b.currentLevel),
            ["platform"] = b.platform,
            ["appVersion"] = b.appVersion,
            ["isDevelopment"] = b.isDevelopment,
        };
        Add(data, old, "attemptsStarted", b.attempts);
        Add(data, old, "levelsWon", b.wins);
        Add(data, old, "levelsGivenUp", b.giveUps);
        Add(data, old, "struggles", b.struggles);
        Add(data, old, "interruptedAttempts", b.interruptedAttempts);
        Add(data, old, "continues", b.continues);
        Add(data, old, "continueCoinsSpent", b.coinsSpent);
        Add(data, old, "sessions", b.sessions);
        Add(data, old, "totalPlaySeconds", b.foregroundSeconds);
        return data;
    }
}
