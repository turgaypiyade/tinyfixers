using UnityEngine;
#if TF_FIREBASE_ANALYTICS
using Firebase.Analytics;
#endif

/// <summary>
/// Tüm oyuncuların TOPLU oyun istatistiği → Firebase Analytics (Console > Analytics > Events).
/// "Level 100'ü kaç kişi denedi, kaçta kaçı geçti, ortalama kaçıncı denemede, ne kadar sürdü" buradan okunur.
///
/// Firebase Analytics SDK projede yokken derlenir ama hiçbir şey göndermez. Etkinleştirmek için:
///   1) Firebase Unity SDK zip'inden FirebaseAnalytics.unitypackage'ı içe aktar,
///   2) Player Settings > Scripting Define Symbols (iOS + Android): TF_FIREBASE_ANALYTICS ekle,
///   3) Console > Analytics > Custom definitions: level, attempt, success, seconds, moves_left,
///      assist_tier, fails_before, moves_total parametrelerini "custom dimension/metric" olarak tanımla
///      (tanımlanmayan parametreler raporlarda görünmez; BigQuery export'ta hep vardır).
///
/// Event'ler: level_start (level, attempt) · level_end (level, attempt, success, seconds, moves_left,
/// assist_tier, fails_before, moves_total). Kullanıcı özelliği: current_level.
/// </summary>
public static class GameAnalytics
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (RuntimeSimulationSession.IsActive) return;
        LevelAttemptStats.OnAttemptStarted += (level, attempt) =>
        {
            SetCurrentLevel(level);
            Log("level_start", ("level", level), ("attempt", attempt));
        };
        LevelAttemptStats.OnAttemptEnded += r =>
        {
            Log("level_end",
                ("level", r.Level), ("attempt", r.Attempt), ("success", r.Won ? 1 : 0),
                ("seconds", r.Seconds), ("moves_left", r.MovesLeft),
                ("assist_tier", r.AssistTier), ("fails_before", r.FailsBefore), ("moves_total", r.MovesTotal));
            if (r.Won) SetCurrentLevel(r.Level + 1);
        };
#if TF_FIREBASE_ANALYTICS
        FirebaseAuthService.OnReady += () => FirebaseAnalytics.SetUserId(FirebaseAuthService.UserId);
#endif
    }

    private static void SetCurrentLevel(int level)
    {
#if TF_FIREBASE_ANALYTICS
        FirebaseAnalytics.SetUserProperty("current_level", level.ToString());
#endif
    }

    private static void Log(string eventName, params (string name, long value)[] parameters)
    {
#if TF_FIREBASE_ANALYTICS
        var p = new Parameter[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            p[i] = new Parameter(parameters[i].name, parameters[i].value);
        FirebaseAnalytics.LogEvent(eventName, p);
#endif
    }
}
