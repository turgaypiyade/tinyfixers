using System;

/// <summary>
/// Read-only, local refill preference. Scores normal colors only; never creates a special or
/// edits a tile. The live match resolver remains responsible for matching and special creation.
/// The accessor returns null for empty, blocked, locked, movable-obstacle or special cells.
/// </summary>
internal static class RefillAssistScoring
{
    internal static int Score(int color, int x, int y, Func<int, int, int?> colorAt)
    {
        Func<int, int, int?> placed = (cx, cy) => cx == x && cy == y ? color : colorAt(cx, cy);
        int immediate = ConnectedMatchScore(x, y, color, placed);
        if (immediate > 0) return immediate; // This stone will clear; it cannot be swapped first.

        int bestSwap = 0;
        int adjacent = 0;
        for (int dir = 0; dir < 4; dir++)
        {
            int nx = x + (dir == 0 ? -1 : dir == 1 ? 1 : 0);
            int ny = y + (dir == 2 ? -1 : dir == 3 ? 1 : 0);
            int? neighbor = colorAt(nx, ny);
            if (!neighbor.HasValue) continue;
            if (neighbor.Value == color) { adjacent++; continue; }

            int displacedColor = neighbor.Value;
            Func<int, int, int?> swapped = (cx, cy) =>
                cx == x && cy == y ? displacedColor : cx == nx && cy == ny ? color : colorAt(cx, cy);
            int score = Math.Max(ConnectedMatchScore(nx, ny, color, swapped),
                ConnectedMatchScore(x, y, displacedColor, swapped));
            bestSwap = Math.Max(bestSwap, score);
        }
        // Prefer an available special-producing move over a plain immediate 3-match,
        // while a special formed immediately still wins over its one-move counterpart.
        return Math.Max(bestSwap * 3 / 4, adjacent * 10);
    }

    private static int ConnectedMatchScore(int x, int y, int color, Func<int, int, int?> at)
    {
        int best = PatternScore(x, y, color, at);
        int left = Run(x, y, -1, 0, color, at), right = Run(x, y, 1, 0, color, at);
        int up = Run(x, y, 0, -1, color, at), down = Run(x, y, 0, 1, color, at);
        // Completing the arm of a T/L also creates a special at an off-center intersection.
        if (left + right + 1 >= 3)
            for (int dx = -left; dx <= right; dx++)
                best = Math.Max(best, PatternScore(x + dx, y, color, at));
        if (up + down + 1 >= 3)
            for (int dy = -up; dy <= down; dy++)
                best = Math.Max(best, PatternScore(x, y + dy, color, at));
        return best;
    }

    private static int PatternScore(int x, int y, int color, Func<int, int, int?> at)
    {
        int horizontal = 1 + Run(x, y, -1, 0, color, at) + Run(x, y, 1, 0, color, at);
        int vertical = 1 + Run(x, y, 0, -1, color, at) + Run(x, y, 0, 1, color, at);
        if (horizontal >= 5 || vertical >= 5) return 1000; // Five-in-a-row.
        if (horizontal >= 3 && vertical >= 3) return 900; // T / L.
        if (horizontal >= 4 || vertical >= 4) return 800;
        for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
                if (at(x + dx, y) == color && at(x, y + dy) == color && at(x + dx, y + dy) == color)
                    return 700; // 2x2 square.
        return horizontal >= 3 || vertical >= 3 ? 400 : 0;
    }

    private static int Run(int x, int y, int dx, int dy, int color, Func<int, int, int?> at)
    {
        int count = 0;
        for (x += dx, y += dy; at(x, y) == color; x += dx, y += dy) count++;
        return count;
    }
}
