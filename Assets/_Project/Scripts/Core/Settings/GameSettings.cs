using System;
using UnityEngine;

/// <summary>
/// Oyun ayarları için merkezi static API. PlayerPrefs tabanlı.
/// Tüm sistemler (audio, vibration, hint, notification) buradan okur/dinler.
/// </summary>
public static class GameSettings
{
    // ── PlayerPrefs keys ─────────────────────────────────────────────────────
    private const string KeyMusic        = "settings_music_on";
    private const string KeySound        = "settings_sound_on";
    private const string KeyVibration    = "settings_vibration_on";
    private const string KeyHint         = "settings_hint_on";
    private const string KeyNotification = "settings_notification_on";

    private const string KeyInstagramFollowed = "settings_instagram_followed";

    // ── Events ───────────────────────────────────────────────────────────────
    public static event Action<bool> OnMusicChanged;
    public static event Action<bool> OnSoundChanged;
    public static event Action<bool> OnVibrationChanged;
    public static event Action<bool> OnHintChanged;
    public static event Action<bool> OnNotificationChanged;

    // ── Defaults ─────────────────────────────────────────────────────────────
    private const int DefaultOn = 1;

    // ── Properties ───────────────────────────────────────────────────────────
    public static bool MusicEnabled        { get => GetBool(KeyMusic);        set => SetBool(KeyMusic,        value, OnMusicChanged); }
    public static bool SoundEnabled        { get => GetBool(KeySound);        set => SetBool(KeySound,        value, OnSoundChanged); }
    public static bool VibrationEnabled    { get => GetBool(KeyVibration);    set => SetBool(KeyVibration,    value, OnVibrationChanged); }
    public static bool HintEnabled         { get => GetBool(KeyHint);         set => SetBool(KeyHint,         value, OnHintChanged); }
    public static bool NotificationEnabled { get => GetBool(KeyNotification); set => SetBool(KeyNotification, value, OnNotificationChanged); }

    public static bool InstagramFollowed
    {
        get => PlayerPrefs.GetInt(KeyInstagramFollowed, 0) == 1;
        set { PlayerPrefs.SetInt(KeyInstagramFollowed, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static bool GetBool(string key) => PlayerPrefs.GetInt(key, DefaultOn) == 1;

    private static void SetBool(string key, bool value, Action<bool> evt)
    {
        int current = PlayerPrefs.GetInt(key, DefaultOn);
        int next = value ? 1 : 0;
        if (current == next) return;

        PlayerPrefs.SetInt(key, next);
        PlayerPrefs.Save();
        evt?.Invoke(value);
    }

    /// <summary>
    /// Vibration kısa titreşim — VibrationEnabled true ise tetikler.
    /// Mobile platformda Handheld.Vibrate çağırır, editörde no-op.
    /// </summary>
    public static void TryVibrate()
    {
        if (!VibrationEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
