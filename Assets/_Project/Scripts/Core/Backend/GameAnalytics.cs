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
///   3) Console > Analytics > Custom definitions: level/attempt/success/assist_tier/fails_before
///      metinlerini event-scoped custom dimension; seconds/moves_left/moves_total sayılarını
///      custom metric olarak tanımla
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
                ("assist_tier", r.AssistTier), ("fails_before", r.FailsBefore), ("moves_total", r.MovesTotal),
                ("struggles_before", r.StrugglesBefore), ("struggled", r.Struggled ? 1 : 0),
                ("continues", r.Continues), ("extra_moves", r.ExtraMoves),
                ("continue_coins", r.ContinueCoinsSpent), ("ad_continues", r.AdContinues),
                ("measurement_version", 2), ("is_development", Debug.isDebugBuild ? 1 : 0));
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

    /// Yeni oyuncunun başladığı harika + ülkesi (yerel başlangıcın tutunmaya etkisi ölçülsün).
    public static void SetStartWonder(string wonderId, string country)
    {
        if (RuntimeSimulationSession.IsActive) return;
#if TF_FIREBASE_ANALYTICS
        FirebaseAnalytics.SetUserProperty("start_wonder", wonderId);
        FirebaseAnalytics.SetUserProperty("device_country", country);
#endif
    }

    /// Event'lerin (Bostan Hasadı vb.) kendi analytics olayları için ortak giriş.
    public static void LogEvent(string eventName, params (string name, long value)[] parameters)
    {
        if (RuntimeSimulationSession.IsActive) return;
        Log(eventName, parameters);
    }

    private static void Log(string eventName, params (string name, long value)[] parameters)
    {
        if (RuntimeSimulationSession.IsActive) return;
#if TF_FIREBASE_ANALYTICS
        parameters ??= System.Array.Empty<(string name, long value)>();
        bool isLevelEvent = eventName == "level_start" || eventName == "level_end";
        long level = isLevelEvent ? CurrentLevel.Global : 0;
        int count = isLevelEvent ? 1 : 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (isLevelEvent && parameters[i].name == "level")
            {
                // End events carry the played level, even if progress has already advanced.
                if (parameters[i].value > 0) level = parameters[i].value;
            }
            else count++;
        }

        var p = new Parameter[count];
        int next = 0;
        string levelText = isLevelEvent
            ? level.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        if (isLevelEvent) p[next++] = new Parameter("level", levelText);
        for (int i = 0; i < parameters.Length; i++)
        {
            if (isLevelEvent && parameters[i].name == "level") continue;
            var item = parameters[i];
            p[next++] = isLevelEvent && IsLevelDimension(item.name)
                ? new Parameter(item.name, item.value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : new Parameter(item.name, item.value);
        }
        FirebaseAnalytics.LogEvent(eventName, p);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isLevelEvent) LogLevelPayload(eventName, levelText, parameters);
#endif
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameDiagnostics.AnalyticsEvents)
        {
            if (eventName == "level_start" || eventName == "level_end")
                Debug.LogWarning($"[Analytics] event={eventName} gönderilmedi: TF_FIREBASE_ANALYTICS sembolü bu platformda kapalı.");
        }
#endif
    }

    // Mobile app custom dimensions require text; measured quantities stay numeric metrics.
    private static bool IsLevelDimension(string name) =>
        name == "attempt" || name == "success" || name == "assist_tier" ||
        name == "fails_before" || name == "struggles_before" || name == "struggled" ||
        name == "measurement_version" || name == "is_development";

#if TF_FIREBASE_ANALYTICS && (UNITY_EDITOR || DEVELOPMENT_BUILD)
    private static void LogLevelPayload(string eventName, string level,
        (string name, long value)[] parameters)
    {
        if (GameDiagnostics.AnalyticsEvents)
        {
            var message = new System.Text.StringBuilder(512);
            message.Append("[Analytics] event=").Append(eventName)
                .Append(" | level=\"").Append(level).Append("\" (String)");
            for (int i = 0; i < parameters.Length; i++)
            {
                var item = parameters[i];
                if (item.name == "level") continue;
                bool text = IsLevelDimension(item.name);
                message.Append(" | ").Append(item.name).Append('=');
                if (text) message.Append('"');
                message.Append(item.value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                message.Append(text ? "\" (String)" : " (Int64)");
            }
            message.Append(" | SDK çağrısı tamamlandı.");
#if UNITY_EDITOR
            message.Append(" Editor stub: sunucuya gönderim yapılmaz.");
#else
            message.Append(" Gerçek alımı cihaz DebugView'inde doğrulayın.");
#endif
            Debug.Log(message.ToString());
        }
    }
#endif
}
