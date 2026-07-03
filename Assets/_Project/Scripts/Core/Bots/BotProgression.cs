using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Botların "arka planda oynayıp kazanıyormuş gibi" ilerleyen skorunu hesaplar — deterministik
/// ve gerçek zamana bağlı. Aynı bot (seed) + aynı hafta → aynı ilerleme; hafta ilerledikçe puan
/// artar, Pazartesi sıfırlanır. Sunucu/maliyet yok, tamamen yerel.
///
/// Mantık: her bot günde birkaç oyun oynar, botWinRate oranında kazanır, her galibiyet birkaç puan.
/// Skor = oynananGün × günlükOyun × kazanmaOranı × puan. Hafta başından bugüne kadar birikmiş hâli.
/// </summary>
public static class BotProgression
{
    public static int WeeklyScore(int seed, float baseWinRate = 0.65f)
    {
        var rnd = Rng(seed, salt: 73856093);
        float winRate = Mathf.Clamp01(baseWinRate + (float)(rnd.NextDouble() * 0.30 - 0.12)); // ~0.53–0.83
        int gamesPerDay  = rnd.Next(3, 26);
        int pointsPerWin = rnd.Next(1, 4);

        double wins = gamesPerDay * ElapsedDaysThisWeek() * winRate;
        return Mathf.RoundToInt((float)(wins * pointsPerWin));
    }

    public static int TeamWeeklyScore(int seed)
    {
        var rnd = Rng(seed, salt: 19349663);
        int members        = TeamMembers(seed);
        int perMemberDaily = rnd.Next(2, 9);
        double score = members * perMemberDaily * ElapsedDaysThisWeek() * 0.65;
        return Mathf.RoundToInt((float)score);
    }

    public static int TeamMembers(int seed) => Rng(seed, salt: 83492791).Next(12, 45);

    // ---- yardımcılar --------------------------------------------------

    private static System.Random Rng(int seed, int salt)
        => new System.Random(unchecked(seed * salt) ^ WeekSeed());

    // Haftaya özgü tohum → her yeni hafta botlar yeni hedeflerle "sıfırlanır".
    private static int WeekSeed()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return now.Year * 100 + week;
    }

    // Hafta başından (Pazartesi 00:00 UTC) bugüne geçen gün (0–7 arası, kesirli).
    private static float ElapsedDaysThisWeek()
    {
        var now = DateTime.UtcNow;
        int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;   // Pazartesi = 0
        var weekStart = now.Date.AddDays(-daysSinceMonday);
        return Mathf.Clamp((float)(now - weekStart).TotalDays, 0.05f, 7f);
    }
}
