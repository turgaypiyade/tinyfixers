using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Telefona bırakılacak tek bir yerel bildirim.</summary>
public readonly struct PlannedNotification
{
    public readonly string Kind;        // log/teşhis için ("lives_full", "event_start_rising"...)
    public readonly int Priority;       // çakışmada yüksek olan kalır
    public readonly DateTime FireUtc;
    public readonly string Title;
    public readonly string Body;

    public PlannedNotification(string kind, int priority, DateTime fireUtc, string title, string body)
    {
        Kind = kind;
        Priority = priority;
        FireUtc = fireUtc;
        Title = title;
        Body = body;
    }
}

/// <summary>
/// Uygulama arka plana giderken "hangi bildirim, ne zaman" planını çıkarır — saf hesap, platform bilmez.
/// Hepsi önceden hesaplanabilir olaylar (can dolumu, event takvimi, çark bekleme süresi) olduğu için
/// sunucu gerekmez; <see cref="LocalNotificationService"/> planı telefona bırakır, açılışta hepsini siler.
///
/// Rahatsız etmeme kuralları:
///  - Sessiz saat (yerel 22:00–09:00): ileri kayan bildirim sabah 10:00'a, "bitiyor" hatırlatması
///    akşam 21:30'a (geriye) alınır — bitişten sonraya kaymasın.
///  - Yerel gün başına en fazla <see cref="MaxPerLocalDay"/>, iki bildirim arası en az <see cref="MinGapMinutes"/> dk.
///    Çakışmada öncelik: event başladı > can doldu > event bitiyor > çark > geri dönüş.
/// </summary>
public static class NotificationPlanner
{
    public const int MaxPerLocalDay = 3;
    public const int MinGapMinutes = 60;
    private const int HorizonDays = 7;
    private const int QuietStartHour = 22;
    private const int QuietEndHour = 9;
    private const int MorningFireHour = 10;
    private static readonly TimeSpan EveningFallback = new TimeSpan(21, 30, 0);
    private static readonly TimeSpan EndingLeadTime = TimeSpan.FromHours(3);
    private static readonly TimeSpan MinLeadFromNow = TimeSpan.FromMinutes(10);

    private const int PriorityEventStart = 50;
    private const int PriorityLivesFull = 40;
    private const int PriorityEventEnding = 30;
    private const int PriorityWheel = 20;
    private const int PriorityComeback = 10;

    public static List<PlannedNotification> Build(DateTime utcNow)
    {
        var candidates = new List<PlannedNotification>();
        AddLivesFull(candidates, utcNow);
        AddRising(candidates, utcNow);
        AddBridgeRepair(candidates, utcNow);
        AddWheel(candidates, utcNow);
        AddComeback(candidates, utcNow, 2, "notif_comeback_title", "notif_comeback_body");
        AddComeback(candidates, utcNow, 5, "notif_comeback2_title", "notif_comeback2_body");
        return ApplyLimits(candidates, utcNow);
    }

    // ── Kaynaklar ───────────────────────────────────────────────────

    private static void AddLivesFull(List<PlannedNotification> list, DateTime utcNow)
    {
        if (LivesManager.IsRegenFull) return;
        int missing = LivesManager.RegenCapLives - LivesManager.Current;
        DateTime full = LivesManager.NextLifeTime.AddMinutes((missing - 1) * LivesManager.RegenIntervalMinutes);
        AddForward(list, "lives_full", PriorityLivesFull, full, utcNow,
            "notif_lives_full_title", "notif_lives_full_body");
    }

    // Safari takvimi ana menüde "Yükseliş" görünümüyle oynanıyor (RisingMapScreen) → metinler Yükseliş adını kullanır.
    private static void AddRising(List<PlannedNotification> list, DateTime utcNow)
    {
        var config = SafariState.Config;
        if (config == null || CurrentLevel.Global < config.minLevelGate) return;

        int windowHours = Math.Max(1, config.windowHours);
        bool everyDay = config.activeDays == null || config.activeDays.Count == 0;
        for (int d = 1; d <= HorizonDays; d++)
        {
            DateTime start = utcNow.Date.AddDays(d);
            if (!everyDay && !config.activeDays.Contains(start.DayOfWeek)) continue;
            // Bir önceki gün de aktifse pencere zaten açık sayılır; yalnız yeni açılışları duyur.
            if (SafariSchedule.IsActiveNow(config, start.AddMinutes(-1))) continue;
            AddForward(list, "event_start_rising", PriorityEventStart, start, utcNow,
                "notif_event_rising_title", "notif_event_rising_body", start.AddHours(windowHours));
        }

        if (SafariSchedule.IsActiveNow(config, utcNow)
            && SafariState.HasJoined
            && SafariState.CycleKey == SafariSchedule.GetCycleKey(config, utcNow)
            && SafariState.RunStatus != SafariRunStatus.Completed)
        {
            AddEnding(list, "event_ending_rising", SafariSchedule.GetWindowEnd(config, utcNow), utcNow,
                GameLocalization.Get("notif_event_name_rising"));
        }
    }

    private static void AddBridgeRepair(List<PlannedNotification> list, DateTime utcNow)
    {
        var config = BridgeRepairConfig.Shared;
        if (config == null || CurrentLevel.Global < config.minLevelGate) return;

        int windowHours = Math.Max(1, config.windowHours);
        var days = BridgeRepairSchedule.GetWeekDays(config, utcNow);
        days.AddRange(BridgeRepairSchedule.GetWeekDays(config, utcNow.AddDays(7)));
        foreach (DateTime start in days)
        {
            if (start <= utcNow || start > utcNow.AddDays(HorizonDays)) continue;
            if (BridgeRepairSchedule.IsActiveNow(config, start.AddMinutes(-1))) continue;
            AddForward(list, "event_start_bridge", PriorityEventStart, start, utcNow,
                "notif_event_bridge_title", "notif_event_bridge_body", start.AddHours(windowHours));
        }

        if (BridgeRepairSchedule.IsActiveNow(config, utcNow)
            && BridgeRepairState.HasJoined
            && BridgeRepairState.CycleKey == BridgeRepairSchedule.GetCycleKey(config, utcNow)
            && !BridgeRepairState.IsFinished)
        {
            AddEnding(list, "event_ending_bridge", BridgeRepairSchedule.GetWindowEnd(config, utcNow), utcNow,
                GameLocalization.Get("notif_event_name_bridge"));
        }
    }

    private static void AddWheel(List<PlannedNotification> list, DateTime utcNow)
    {
        TimeSpan wait = FortuneWheelController.GetTimeUntilNextSpin();
        if (wait <= TimeSpan.Zero) return;   // hak zaten hazır; geri dönüş hatırlatması kapsar
        AddForward(list, "wheel_ready", PriorityWheel, utcNow + wait, utcNow,
            "notif_wheel_title", "notif_wheel_body");
    }

    private static void AddComeback(List<PlannedNotification> list, DateTime utcNow, int days, string titleKey, string bodyKey)
    {
        AddForward(list, $"comeback_{days}d", PriorityComeback, utcNow.AddDays(days), utcNow, titleKey, bodyKey);
    }

    // ── Zaman ayarı ─────────────────────────────────────────────────

    /// Sessiz saate düşerse sabaha ertelenir; ertelenmiş hali <paramref name="mustBeBeforeUtc"/>'i geçerse düşer.
    private static void AddForward(List<PlannedNotification> list, string kind, int priority, DateTime fireUtc,
        DateTime utcNow, string titleKey, string bodyKey, DateTime? mustBeBeforeUtc = null)
    {
        DateTime adjusted = ShiftOutOfQuietForward(fireUtc);
        if (adjusted < utcNow + MinLeadFromNow) return;
        if (mustBeBeforeUtc.HasValue && adjusted >= mustBeBeforeUtc.Value) return;
        list.Add(new PlannedNotification(kind, priority, adjusted,
            GameLocalization.Get(titleKey), GameLocalization.Get(bodyKey)));
    }

    private static void AddEnding(List<PlannedNotification> list, string kind, DateTime windowEndUtc, DateTime utcNow, string eventName)
    {
        DateTime fire = ShiftOutOfQuietBackward(windowEndUtc - EndingLeadTime);
        if (fire < utcNow + MinLeadFromNow || fire >= windowEndUtc) return;
        list.Add(new PlannedNotification(kind, PriorityEventEnding, fire,
            GameLocalization.Get("notif_event_ending_title"),
            GameLocalization.GetFormat("notif_event_ending_body", eventName)));
    }

    private static bool IsQuiet(DateTime local) => local.Hour >= QuietStartHour || local.Hour < QuietEndHour;

    private static DateTime ShiftOutOfQuietForward(DateTime utc)
    {
        DateTime local = utc.ToLocalTime();
        if (!IsQuiet(local)) return utc;
        DateTime morning = local.Date.AddHours(MorningFireHour);
        if (local.Hour >= QuietStartHour) morning = morning.AddDays(1);
        return DateTime.SpecifyKind(morning, DateTimeKind.Local).ToUniversalTime();
    }

    private static DateTime ShiftOutOfQuietBackward(DateTime utc)
    {
        DateTime local = utc.ToLocalTime();
        if (!IsQuiet(local)) return utc;
        DateTime evening = local.Date + EveningFallback;
        if (local.Hour < QuietEndHour) evening = evening.AddDays(-1);
        return DateTime.SpecifyKind(evening, DateTimeKind.Local).ToUniversalTime();
    }

    // ── Günlük sınır + aralık ───────────────────────────────────────

    private static List<PlannedNotification> ApplyLimits(List<PlannedNotification> candidates, DateTime utcNow)
    {
        candidates.Sort((a, b) => a.Priority != b.Priority
            ? b.Priority.CompareTo(a.Priority)
            : a.FireUtc.CompareTo(b.FireUtc));

        var accepted = new List<PlannedNotification>();
        var perDay = new Dictionary<DateTime, int>();
        foreach (var n in candidates)
        {
            DateTime day = n.FireUtc.ToLocalTime().Date;
            perDay.TryGetValue(day, out int count);
            if (count >= MaxPerLocalDay) continue;

            bool tooClose = false;
            foreach (var a in accepted)
                tooClose |= Math.Abs((a.FireUtc - n.FireUtc).TotalMinutes) < MinGapMinutes;
            if (tooClose) continue;

            accepted.Add(n);
            perDay[day] = count + 1;
        }

        accepted.Sort((a, b) => a.FireUtc.CompareTo(b.FireUtc));
        return accepted;
    }
}
