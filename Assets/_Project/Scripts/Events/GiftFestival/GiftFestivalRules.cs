using System;

/// <summary>Pure timing, schedule and targeting rules shared by gameplay and UI.</summary>
public static class GiftFestivalRules
{
    public enum Hit { Miss, Down, Perfect, Up }

    public readonly struct Window
    {
        public readonly int Cycle;
        public readonly DateTime Start, End, NextStart;
        public readonly bool Active;

        public Window(int cycle, DateTime start, DateTime end, DateTime nextStart, bool active)
        {
            Cycle = cycle; Start = start; End = end; NextStart = nextStart; Active = active;
        }
    }

    public static Window GetWindow(DateTime anchorUtc, DateTime nowUtc, int activeHours, int restHours)
    {
        long active = Math.Max(1, Math.Min(8760, activeHours)) * TimeSpan.TicksPerHour;
        long period = active + Math.Max(1, Math.Min(8760, restHours)) * TimeSpan.TicksPerHour;
        if (nowUtc < anchorUtc)
            return new Window(-1, anchorUtc, anchorUtc, anchorUtc, false);
        long cycle = (nowUtc.Ticks - anchorUtc.Ticks) / period;
        var start = new DateTime(anchorUtc.Ticks + cycle * period, DateTimeKind.Utc);
        var end = start.AddTicks(active);
        return new Window((int)Math.Min(int.MaxValue, cycle), start, end, start.AddTicks(period), nowUtc < end);
    }

    public static float RedWidth(float value) => float.IsNaN(value) ? 0.15f : Math.Max(0.01f, Math.Min(0.45f, value));
    public static float GreenWidth(float red, float green) => float.IsNaN(green)
        ? Math.Min(0.2f, 1f - 2f * RedWidth(red))
        : Math.Max(0.02f, Math.Min(1f - 2f * RedWidth(red), green));

    // Boundary values belong to the better zone: red/yellow -> yellow, yellow/green -> green.
    public static Hit Evaluate(float position, float red, float green)
    {
        if (float.IsNaN(position) || float.IsInfinity(position) || position < 0f || position > 1f) return Hit.Miss;
        red = RedWidth(red);
        green = GreenWidth(red, green);
        float greenStart = (1f - green) * 0.5f;
        float greenEnd = 1f - greenStart;
        if (position >= greenStart && position <= greenEnd) return Hit.Perfect;
        if (position < red || position > 1f - red) return Hit.Miss;
        return position < greenStart ? Hit.Down : Hit.Up;
    }

    public static int TargetCell(int selected, int size, Hit hit)
    {
        if (size < 2 || size > 4 || selected < 0 || selected >= size * size || hit == Hit.Miss) return -1;
        if (hit == Hit.Perfect) return selected;
        int row = selected / size;
        int step = hit == Hit.Down ? 1 : -1;
        // Reflect at a board edge, always keeping a yellow shot off the selected cell.
        if (row + step < 0 || row + step >= size) step = -step;
        return selected + step * size;
    }

    public static bool IsOpen(int mask, int cell) => cell >= 0 && cell < 16 && (mask & (1 << cell)) != 0;
    public static bool IsComplete(int mask, int size)
    {
        if (size < 2 || size > 4) return false;
        int all = (1 << (size * size)) - 1;
        return (mask & all) == all;
    }
}
