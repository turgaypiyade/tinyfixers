using System;

/// <summary>Pure shared season, scoring and deterministic empty-slot opponents.</summary>
public static class GearCupRules
{
    public static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTime SeasonStart(DateTime now, int hours)
    {
        long ticks = TimeSpan.FromHours(Math.Max(1, hours)).Ticks;
        return Epoch.AddTicks(Math.Max(0, (now.ToUniversalTime() - Epoch).Ticks / ticks) * ticks);
    }
    public static string SeasonKey(DateTime start) => "gc_" + ((start.Ticks - Epoch.Ticks) / TimeSpan.TicksPerHour);
    public static bool Counts(TileType type, bool yellowOnly) => type == TileType.Gear ||
        (!yellowOnly && (type == TileType.Core || type == TileType.Bolt || type == TileType.Plate));
    public static int BotFinalScore(int group, int slot) => 4000 + (int)(((long)Math.Max(0, group) * 1103 + slot * 1801 + 7619) % 10001);
    public static int BotScore(int group, int slot, DateTime start, DateTime end, DateTime now)
    {
        double t = Math.Max(0, Math.Min(1, (now - start).TotalSeconds / Math.Max(1, (end - start).TotalSeconds)));
        // Deterministic short sessions; no per-frame simulation or moving target based on player score.
        double sessions = Math.Floor(t * 144) / 144;
        return (int)Math.Floor(BotFinalScore(group, slot) * sessions);
    }
    public static bool RanksBefore(int score, int slot, int otherScore, int otherSlot) =>
        score > otherScore || (score == otherScore && slot < otherSlot);
}
