using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class RuntimeSimulationReport
{
    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static string Number(double value) => value.ToString("F1", CultureInfo.InvariantCulture);

    public static string ToCsv(RuntimeSimulationSession.Report report)
    {
        var text = new StringBuilder("Level,Policy,Speed,Attempts,Completed,Wins,Win%,Low95%,High95%,AvgMovesOnWin,Unresolved\n");
        foreach (var group in report.games.GroupBy(g => g.levelPath))
        {
            var valid = group.Where(g => g.outcome == "Won" || g.outcome == "Lost").ToList();
            var wins = valid.Where(g => g.outcome == "Won").ToList();
            int n = valid.Count;
            var interval = Wilson(wins.Count, n);
            text.AppendLine(string.Join(",", new[] { Csv(group.First().level), report.config.policy.ToString(),
                Number(report.config.timeScale), group.Count().ToString(), n.ToString(), wins.Count.ToString(),
                n > 0 ? Number(100.0 * wins.Count / n) : "", n > 0 ? Number(100 * interval.low) : "",
                n > 0 ? Number(100 * interval.high) : "",
                wins.Count > 0 ? Number(wins.Average(g => g.movesUsed)) : "",
                (group.Count() - n).ToString() }));
        }
        return text.ToString();
    }

    public static string Summary(RuntimeSimulationSession.Report report)
    {
        var text = new StringBuilder();
        text.AppendLine(report.completed ? "Koşu tamamlandı." : "Kısmi rapor — koşu henüz tamamlanmadı veya durduruldu.");
        long planned = (long)(report.config.levelPaths?.Length ?? 0) * report.config.gamesPerLevel;
        text.AppendLine($"Kaydedilen oyun: {report.games.Count}/{planned}");
        text.AppendLine($"Motor: {report.engine}; bot: {report.config.policy}; hız: {report.config.timeScale:F1}×");
        text.AppendLine("Oranlar insan zorluğu için kalibre edilmemiştir. Hata/timeout/hamlesizlik kayıp sayılmaz.");
        foreach (var group in report.games.GroupBy(g => g.levelPath))
        {
            int n = group.Count(g => g.outcome == "Won" || g.outcome == "Lost");
            int wins = group.Count(g => g.outcome == "Won");
            var interval = Wilson(wins, n);
            text.AppendLine(n == 0 ? $"{group.First().level}: ölçüm yok" :
                $"{group.First().level}: {100.0 * wins / n:F1}% [{100 * interval.low:F0}–{100 * interval.high:F0}%], {wins}/{n}");
            foreach (var failures in group.Where(g => g.outcome != "Won" && g.outcome != "Lost").GroupBy(g => g.outcome))
                text.AppendLine($"  {failures.Key ?? "Incomplete"}: {failures.Count()} — {failures.First().detail}");
        }
        return text.ToString();
    }

    private static (double low, double high) Wilson(int wins, int n)
    {
        if (n == 0) return (0, 0);
        const double z = 1.96;
        double p = (double)wins / n, d = 1 + z * z / n;
        double center = p + z * z / (2 * n);
        double margin = z * System.Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
        return ((center - margin) / d, (center + margin) / d);
    }
}
