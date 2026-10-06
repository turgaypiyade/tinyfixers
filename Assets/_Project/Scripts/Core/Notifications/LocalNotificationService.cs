using System;
using UnityEngine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using Unity.Notifications;
#endif

/// <summary>
/// Yerel bildirimler (com.unity.mobile.notifications, sunucusuz).
///
/// Tek kural: oyun AÇIKKEN telefonda bekleyen bildirim yoktur. Arka plana giderken
/// <see cref="NotificationPlanner"/> planı bırakılır; geri gelince hepsi (bekleyen + bildirim
/// merkezinde duran) silinir. Böylece eski plan hiç bayatlamaz.
///
/// İzin: işletim sistemi izni tek seferde istenir, tüm türler birlikte açılır. Önce oyun içi
/// <see cref="NotificationPermissionPrompt"/> sorar; "Evet" → OS izni. Ayarlar'daki Bildirimler
/// anahtarı ana şalterdir: kapalıyken hiçbir şey bırakılmaz, açılınca (gerekirse) izin istenir.
///
/// iOS NOT: Project Settings > Mobile Notifications > iOS > "Request Authorization on App Launch"
/// KAPALI olmalı; yoksa izin açılışta sorulur ve ön-popup anlamsızlaşır.
/// </summary>
public static class LocalNotificationService
{
    public enum ConsentState { NotAsked = 0, Accepted = 1, Declined = 2 }

    private const string KeyConsent = "notif_consent";
    private const string AndroidChannelId = "game";

    private static bool s_initialized;
    private static bool s_permissionRequestedThisSession;

    public static ConsentState Consent
    {
        get => (ConsentState)PlayerPrefs.GetInt(KeyConsent, (int)ConsentState.NotAsked);
        private set { PlayerPrefs.SetInt(KeyConsent, (int)value); PlayerPrefs.Save(); }
    }

    private static bool CanSchedule => Consent == ConsentState.Accepted && GameSettings.NotificationEnabled;

    // ── Açılış ──────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[LocalNotificationService]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<LifecycleHook>();

        PlatformInitialize();
        ClearAll();
        GameSettings.OnNotificationChanged += HandleSettingChanged;
    }

    // ── İzin ────────────────────────────────────────────────────────

    /// Ön-popup "Evet" / Ayarlar'da açma: onayı kaydet, OS iznini iste.
    public static void Accept()
    {
        Consent = ConsentState.Accepted;
        GameSettings.NotificationEnabled = true;
        RequestOsPermission();
    }

    /// Ön-popup "Hayır": tekrar sorulmaz; Ayarlar anahtarı kapalı görünür (sonradan açılabilir).
    public static void Decline()
    {
        Consent = ConsentState.Declined;
        GameSettings.NotificationEnabled = false;
    }

    private static void HandleSettingChanged(bool enabled)
    {
        if (!enabled)
        {
            ClearAll();
            return;
        }
        if (Consent != ConsentState.Accepted)
            Accept();
    }

    private static void RequestOsPermission()
    {
        if (s_permissionRequestedThisSession) return;
        s_permissionRequestedThisSession = true;
        PlatformRequestPermission();
    }

    // ── Yaşam döngüsü ───────────────────────────────────────────────

    private static void OnSuspend()
    {
        ClearAll();
        if (!CanSchedule) return;

        var plan = NotificationPlanner.Build(DateTime.UtcNow);
        foreach (var n in plan)
            PlatformSchedule(n);
#if !UNITY_EDITOR
        Debug.Log($"[Notifications] {plan.Count} bildirim bırakıldı.");
#endif
    }

    private static void OnResume() => ClearAll();

    private static void ClearAll() => PlatformCancelAll();

    /// Editörde plan önizleme (TinyFixers/Debug menüsü).
    public static string DescribePlan()
    {
        var plan = NotificationPlanner.Build(DateTime.UtcNow);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Notifications] consent={Consent} setting={GameSettings.NotificationEnabled} → {plan.Count} bildirim:");
        foreach (var n in plan)
            sb.AppendLine($"  {n.FireUtc.ToLocalTime():ddd dd.MM HH:mm}  [{n.Kind}]  {n.Title} — {n.Body}");
        return sb.ToString();
    }

    private sealed class LifecycleHook : MonoBehaviour
    {
        private bool suspended;

        private void OnApplicationPause(bool paused) => SetSuspended(paused);
        private void OnApplicationFocus(bool focused) => SetSuspended(!focused);
        private void OnApplicationQuit() => SetSuspended(true);

        // Pause ve Focus aynı geçişte ikisi birden gelir; planı bir kez bırak / bir kez sil.
        private void SetSuspended(bool value)
        {
            if (suspended == value) return;
            suspended = value;
            if (value) OnSuspend();
            else OnResume();
        }
    }

    // ── Platform katmanı ────────────────────────────────────────────

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private static void PlatformInitialize()
    {
        if (s_initialized) return;
        s_initialized = true;
        var args = NotificationCenterArgs.Default;
        args.AndroidChannelId = AndroidChannelId;
        args.AndroidChannelName = GameLocalization.Get("notif_channel_name");
        args.AndroidChannelDescription = GameLocalization.Get("notif_channel_description");
        NotificationCenter.Initialize(args);
    }

    private static void PlatformRequestPermission() => NotificationCenter.RequestPermission();

    private static void PlatformSchedule(PlannedNotification n)
    {
        var notification = new Notification
        {
            Title = n.Title,
            Text = n.Body,
        };
        NotificationCenter.ScheduleNotification(notification,
            new NotificationDateTimeSchedule(n.FireUtc.ToLocalTime()));
    }

    private static void PlatformCancelAll()
    {
        if (!s_initialized) return;
        NotificationCenter.CancelAllScheduledNotifications();
        NotificationCenter.CancelAllDeliveredNotifications();
    }
#else
    private static void PlatformInitialize() => s_initialized = true;
    private static void PlatformRequestPermission() => Debug.Log("[Notifications] (editör) OS izni istendi.");
    private static void PlatformSchedule(PlannedNotification n) { }
    private static void PlatformCancelAll() { }
#endif
}
