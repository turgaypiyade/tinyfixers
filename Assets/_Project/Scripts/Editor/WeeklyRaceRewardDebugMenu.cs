using UnityEditor;
using UnityEngine;

// Haftalık Yarış ödül törenini hafta bitmeden görmek için (yalnız Play modunda). ÖNİZLEME: ödül VERİLMEZ.
public static class WeeklyRaceRewardDebugMenu
{
    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (1.)")]
    private static void Preview1() => Preview(1);

    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (2.)")]
    private static void Preview2() => Preview(2);

    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (3.)")]
    private static void Preview3() => Preview(3);

    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (1.)", true)]
    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (2.)", true)]
    [MenuItem("TinyFixers/Debug/Haftalık Yarış Ödülü Önizle (3.)", true)]
    private static bool Validate() => Application.isPlaying;

    // UÇTAN UCA TEST: kayıtlı haftalık puanı GEÇEN haftaya taşır (yüksek puan → büyük ihtimalle 1.), alınmış işaretini
    // siler. Ana menüye dönünce (sahneyi yeniden yükle) gerçek akış çalışır: sıra Firestore'dan hesaplanır, tören açılır,
    // "Devam" ödülleri GERÇEKTEN verir. Yalnız test hesabında kullan.
    [MenuItem("TinyFixers/Debug/Haftalık Yarış: Geçen Haftayı Simüle Et (gerçek ödül)")]
    private static void SimulateLastWeek()
    {
        var last = System.DateTime.UtcNow.AddDays(-7);
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(last, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
        string weekId = $"{last.Year}-W{week:00}";
        PlayerPrefs.SetString("player_weekly_score_week", weekId);
        PlayerPrefs.SetInt("player_weekly_score", 99999);
        PlayerPrefs.DeleteKey(WeeklyRaceRewards.KeyClaimedWeek);
        PlayerPrefs.DeleteKey(WeeklyRaceRewards.KeyClaimStep);
        WeeklyRaceRewards.ClearPending();
        PlayerPrefs.Save();
        WeeklyRaceRewardFlow.DebugResetSession();
        Debug.Log($"[WeeklyRace] Geçen hafta ({weekId}) 99999 puanla simüle edildi. Ana menüyü yeniden yükle → tören açılmalı.");
    }

    [MenuItem("TinyFixers/Debug/Haftalık Yarış: Geçen Haftayı Simüle Et (gerçek ödül)", true)]
    private static bool ValidateSimulate() => Application.isPlaying;

    private static void Preview(int rank)
    {
        string title = rank == 1
            ? GameLocalization.GetOr("weekly_race_reward_title_1", "Şampiyon!")
            : GameLocalization.GetOr("weekly_race_reward_title", "Tebrikler!");
        string subtitle = string.Format(
            GameLocalization.GetOr("weekly_race_reward_subtitle", "Haftalık Yarış'ı {0}. sırada bitirdin!"), rank);
        ComebackGiftOverlay.Show(WeeklyRaceRewards.RewardsFor(rank), () => Debug.Log("[WeeklyRace] önizleme kapandı (ödül verilmedi)"),
            title, subtitle, celebrate: true);
    }
}
