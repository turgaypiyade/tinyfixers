using System;
using UnityEngine;

public enum SafariRunStatus
{
    Idle,            // yarışa hazır/bekliyor (harita açılabilir, devam edilebilir)
    AwaitingResult,  // level oynanıyor; MainMenu'ye dönünce sonuç değerlendirilecek
    Fell,            // son turda düştü (ilk-hakta geçemedi) → tekrar dene
    Completed        // 7. pitstopa ulaştı, ödül alındı
}

/// <summary>
/// Tiny Safari kalıcı durumu — PlayerPrefs tabanlı (ileride cloud-save'e taşınabilir).
///
/// Pencere (cycle) değişince tüm run state sıfırlanır: her yeni Safari penceresi temiz başlar.
/// Level dönüş tespiti için <see cref="FirstTryClearsSnapshot"/> kullanılır (PlayerStats.FirstTryClears
/// ile karşılaştırma → arttıysa ilerle, aynıysa düş). Bu, Safari'yi global streak'e bağlamadan izole tutar.
/// </summary>
public static class SafariState
{
    private const string KeyCycle       = "safari_cycle";
    private const string KeyJoined      = "safari_joined";
    private const string KeyPitstop     = "safari_pitstop";
    private const string KeyJoinTime    = "safari_join_ticks";
    private const string KeyLastAsk     = "safari_lastask_ticks";
    private const string KeyRunStatus   = "safari_runstatus";
    private const string KeyFtcSnapshot = "safari_ftc_snapshot";
    private const string KeyFallUntil   = "safari_fall_until_ticks";

    public static event Action OnChanged;

    // İkon event'in KENDİ config'inde (SafariConfig, Resources) — provider oradan okur.
    private static SafariConfig s_config;
    private static SafariConfig Config =>
        s_config != null ? s_config : (s_config = Resources.Load<SafariConfig>("Events/SafariConfig"));

    // Loss paneline "vazgeçersen safari ilk-hakkını (pitstop ilerlemesi) yakarsın" öğesini kaydeder.
    // Provider CANLI state okur; kayıt uygulama başında bir kez.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterLossProvider()
    {
        LevelLossRegistry.Register("safari", () =>
        {
            // Level bir safari turuysa (AwaitingResult) ve pitstop ilerlemesi varsa, vazgeçmek
            // ilk-hakkı yakar → CurrentPitstop sıfırlanır. Elde var (gerçekleşti → checkmark).
            if (RunStatus != SafariRunStatus.AwaitingResult || CurrentPitstop <= 0)
                return null;

            string label = GameLocalization.Get("level_end_loss_safari");
            if (string.IsNullOrEmpty(label) || label == "level_end_loss_safari")
                label = "Safari ilerlemesi";

            return new[] { new LevelLossItem(Config != null ? Config.lossIcon : null, label, CurrentPitstop, true) };
        });
    }

    // ── Cycle ────────────────────────────────────────────────────

    public static string CycleKey => PlayerPrefs.GetString(KeyCycle, "");

    /// <summary>
    /// Aktif pencerenin cycle anahtarıyla senkronla. Anahtar değiştiyse (yeni pencere veya kapandı)
    /// run state'i sıfırlar. Her giriş noktasında (controller OnEnable) çağrılmalı.
    /// </summary>
    public static void SyncCycle(SafariConfig config, DateTime utcNow)
    {
        string current = SafariSchedule.GetCycleKey(config, utcNow);
        if (current == CycleKey) return;

        PlayerPrefs.SetString(KeyCycle, current);
        // Yeni pencere → temiz başla.
        PlayerPrefs.SetInt(KeyJoined, 0);
        PlayerPrefs.SetInt(KeyPitstop, 0);
        PlayerPrefs.SetString(KeyJoinTime, "0");
        PlayerPrefs.SetString(KeyLastAsk, "0");
        PlayerPrefs.SetInt(KeyRunStatus, (int)SafariRunStatus.Idle);
        PlayerPrefs.SetInt(KeyFtcSnapshot, 0);
        PlayerPrefs.SetString(KeyFallUntil, "0");
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── Katılım ──────────────────────────────────────────────────

    public static bool HasJoined => PlayerPrefs.GetInt(KeyJoined, 0) == 1;

    public static void MarkJoined(DateTime utcNow)
    {
        PlayerPrefs.SetInt(KeyJoined, 1);
        PlayerPrefs.SetString(KeyJoinTime, utcNow.Ticks.ToString());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static DateTime JoinTimeUtc => ReadUtc(KeyJoinTime);

    // ── Popup re-ask ─────────────────────────────────────────────

    public static DateTime LastAskUtc => ReadUtc(KeyLastAsk);

    public static void MarkAsked(DateTime utcNow)
    {
        PlayerPrefs.SetString(KeyLastAsk, utcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    // ── Pitstop ilerleme ─────────────────────────────────────────

    public static int CurrentPitstop => PlayerPrefs.GetInt(KeyPitstop, 0);

    public static void SetPitstop(int value)
    {
        PlayerPrefs.SetInt(KeyPitstop, Mathf.Max(0, value));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── Run status ───────────────────────────────────────────────

    public static SafariRunStatus RunStatus =>
        (SafariRunStatus)PlayerPrefs.GetInt(KeyRunStatus, (int)SafariRunStatus.Idle);

    public static void SetRunStatus(SafariRunStatus status)
    {
        PlayerPrefs.SetInt(KeyRunStatus, (int)status);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── İlk-hak snapshot (level dönüş tespiti) ───────────────────

    public static int FirstTryClearsSnapshot => PlayerPrefs.GetInt(KeyFtcSnapshot, 0);

    public static void SnapshotFirstTryClears(int value)
    {
        PlayerPrefs.SetInt(KeyFtcSnapshot, value);
        PlayerPrefs.Save();
    }

    // ── Düşme cooldown'ı ─────────────────────────────────────────

    public static DateTime FallCooldownUntilUtc => ReadUtc(KeyFallUntil);

    public static void StartFallCooldown(DateTime utcNow, int minutes)
    {
        DateTime until = utcNow.AddMinutes(Mathf.Max(0, minutes));
        PlayerPrefs.SetString(KeyFallUntil, until.Ticks.ToString());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static TimeSpan FallCooldownRemaining(DateTime utcNow)
    {
        var remaining = FallCooldownUntilUtc - utcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    // ── Yardımcı ─────────────────────────────────────────────────

    private static DateTime ReadUtc(string key)
    {
        long ticks = 0;
        long.TryParse(PlayerPrefs.GetString(key, "0"), out ticks);
        return ticks <= 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
    }

#if UNITY_EDITOR
    /// <summary>Editor testleri için tüm Safari anahtarlarını temizler.</summary>
    public static void DebugClearAll()
    {
        foreach (var k in new[] { KeyCycle, KeyJoined, KeyPitstop, KeyJoinTime,
                                  KeyLastAsk, KeyRunStatus, KeyFtcSnapshot, KeyFallUntil })
            PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
#endif
}
