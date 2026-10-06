using System;
using UnityEngine;

/// <summary>Bir level denemesinin sonucu (analytics / bulut istatistiği / yardım sistemi için).</summary>
public readonly struct LevelAttemptResult
{
    public readonly int Level;
    public readonly bool Won;
    public readonly int Attempt;        // bu level'daki kaçıncı deneme (1'den başlar)
    public readonly int FailsBefore;    // bu denemeden ÖNCE üst üste kaç kez vazgeçildi
    public readonly int Seconds;        // denemenin süresi (uygulama arka plandayken geçen süre hariç)
    public readonly int MovesLeft;      // kazanınca kalan hamle; kaybedince 0
    public readonly int AssistTier;     // deneme başladığında aktif gizli yardım kademesi
    public readonly int MovesTotal;     // level'ın bu denemedeki hamle sayısı (uzaktan ayar dahil)

    public LevelAttemptResult(int level, bool won, int attempt, int failsBefore, int seconds, int movesLeft, int assistTier, int movesTotal)
    {
        Level = level;
        Won = won;
        Attempt = attempt;
        FailsBefore = failsBefore;
        Seconds = seconds;
        MovesLeft = movesLeft;
        AssistTier = assistTier;
        MovesTotal = movesTotal;
    }
}

/// <summary>
/// Oynanan level'ın deneme sayacı — PlayerStats'ın global "ilk deneme" bayrağından bağımsız, LEVEL'a bağlı.
///  - Deneme = level tahtası kurulduğunda başlar (GridSpawner), kazanınca / vazgeçince biter
///    (PlayerStats.RecordLevelCleared / MarkCurrentLevelFailed tek kapıdan çağırır).
///  - "Devam" (hamle satın alma) aynı denemenin parçasıdır; yalnız vazgeçmek fail sayılır.
///  - Level geçilince sayaçlar sıfırlanır. Cloud save'e dahildir (cihaz değişince yardım kademesi korunur).
/// Tüketiciler: <see cref="LevelAssist"/> (gizli yardım), PlayerStatsCloudReporter + GameAnalytics (raporlama).
/// </summary>
public static class LevelAttemptStats
{
    private const string KeyLevel = "level_attempt_level";
    private const string KeyAttempts = "level_attempt_count";
    private const string KeyFails = "level_attempt_fails";

    /// Tahta kuruldu, deneme başladı: (level, attempt).
    public static event Action<int, int> OnAttemptStarted;
    /// Deneme bitti (kazanma ya da vazgeçme).
    public static event Action<LevelAttemptResult> OnAttemptEnded;

    private static int s_activeLevel = -1;
    private static float s_activeSeconds;
    private static int s_activeAssistTier;
    private static int s_movesLeftAtWin;
    private static int s_movesTotal;

    public static bool HasActiveAttempt => s_activeLevel > 0;

    /// Süren denemenin başında sabitlenen yardım kademesi.
    public static int ActiveAssistTier => s_activeAssistTier;

    /// Bu level'da üst üste kaç kez vazgeçildi (başka level için 0).
    public static int ConsecutiveFails(int level) =>
        PlayerPrefs.GetInt(KeyLevel, 0) == level ? PlayerPrefs.GetInt(KeyFails, 0) : 0;

    public static int AttemptsOn(int level) =>
        PlayerPrefs.GetInt(KeyLevel, 0) == level ? PlayerPrefs.GetInt(KeyAttempts, 0) : 0;

    public static void BeginAttempt(int level, int movesTotal)
    {
        if (RuntimeSimulationSession.IsActive || level <= 0) return;

        if (PlayerPrefs.GetInt(KeyLevel, 0) != level)
        {
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.SetInt(KeyAttempts, 0);
            PlayerPrefs.SetInt(KeyFails, 0);
        }
        int attempt = PlayerPrefs.GetInt(KeyAttempts, 0) + 1;
        PlayerPrefs.SetInt(KeyAttempts, attempt);
        PlayerPrefs.Save();

        s_activeLevel = level;
        s_activeSeconds = 0f;
        s_movesLeftAtWin = 0;
        s_movesTotal = movesTotal;
        s_activeAssistTier = LevelAssist.TierFor(level);
        EnsureTimer();
        OnAttemptStarted?.Invoke(level, attempt);
    }

    /// Kazanma anındaki kalan hamle (LevelEnd popup'ı ölçer).
    public static void SetMovesLeftAtWin(int movesLeft) => s_movesLeftAtWin = Mathf.Max(0, movesLeft);

    internal static void RecordWin(int level) => End(level, won: true);

    internal static void RecordGiveUp(int level) => End(level, won: false);

    private static void End(int level, bool won)
    {
        if (RuntimeSimulationSession.IsActive || level <= 0) return;

        int failsBefore = ConsecutiveFails(level);
        int attempt = Mathf.Max(1, AttemptsOn(level));
        bool tracked = s_activeLevel == level;
        var result = new LevelAttemptResult(level, won, attempt, failsBefore,
            tracked ? Mathf.RoundToInt(s_activeSeconds) : 0,
            won ? s_movesLeftAtWin : 0,
            tracked ? s_activeAssistTier : LevelAssist.TierFor(level),
            tracked ? s_movesTotal : 0);

        if (won)
        {
            PlayerPrefs.DeleteKey(KeyLevel);
            PlayerPrefs.DeleteKey(KeyAttempts);
            PlayerPrefs.DeleteKey(KeyFails);
        }
        else
        {
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.SetInt(KeyFails, failsBefore + 1);
        }
        PlayerPrefs.Save();

        s_activeLevel = -1;
        OnAttemptEnded?.Invoke(result);
    }

    // Deneme süresi: yalnız uygulama ön plandayken sayılır.
    private static void EnsureTimer()
    {
        if (s_timer != null) return;
        var go = new GameObject("[LevelAttemptTimer]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_timer = go.AddComponent<Timer>();
    }

    private static Timer s_timer;

    private sealed class Timer : MonoBehaviour
    {
        private void Update()
        {
            // Arka plandan dönüşteki ilk karenin dev delta'sı süreye eklenmesin.
            if (s_activeLevel > 0) s_activeSeconds += Mathf.Min(Time.unscaledDeltaTime, 0.5f);
        }
    }
}
