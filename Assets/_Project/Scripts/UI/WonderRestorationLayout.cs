using UnityEngine;

/// <summary>
/// Allocation-free, deterministic curved restoration patches. The same boundary equations
/// live in UI_WonderReveal.shader, so the torch follows the edge actually being cut.
/// </summary>
public static class WonderRestorationLayout
{
    public static int Stride(int count)
    {
        count = Mathf.Max(1, count);
        for (int candidate = 3; candidate < count; candidate += 2)
        {
            int a = count, b = candidate;
            while (b != 0) { int remainder = a % b; a = b; b = remainder; }
            if (a == 1) return candidate;
        }
        return 1;
    }

    public static int CellForStage(int stage, int count)
    {
        count = Mathf.Max(1, count);
        int stride = Stride(count);
        stage = Mathf.Clamp(stage, 0, count - 1);
        for (int cell = 0; cell < count; cell++)
            if (cell * stride % count == stage) return cell;
        return 0;
    }

    public static float Boundary(int row, int rows, float x)
    {
        if (row <= 0) return 0f;
        if (row >= rows) return 1f;
        float f = (float)row / rows;
        float amplitude = Mathf.Min(1f, 6f / rows);
        return f + Mathf.Sin(Mathf.PI * f) * amplitude *
            (0.024f * Mathf.Sin(x * 10f + f * 8f) + 0.008f * Mathf.Sin(x * 25f - f * 11f));
    }

    public static float Split(float y) => 0.49f + 0.1f * Mathf.Sin(y * 8f + 0.4f)
        + 0.035f * Mathf.Sin(y * 21f + 1f);

    public static Vector2 Point(int cell, int count, float progress)
    {
        float phase = Mathf.Clamp01(progress) * 4f;
        float u, v;
        if (phase < 1f) { u = phase; v = 0f; }
        else if (phase < 2f) { u = 1f; v = phase - 1f; }
        else if (phase < 3f) { u = 3f - phase; v = 1f; }
        else { u = 0f; v = 4f - phase; }
        return SurfacePoint(cell, count, u, v);
    }

    public static Vector2 SurfacePoint(int cell, int count, float u, float v)
    {
        count = Mathf.Max(1, count);
        int rows = (count + 1) / 2;
        int row = cell / 2;
        bool right = cell % 2 != 0;
        bool fullWidth = count % 2 != 0 && cell == count - 1;

        // Boundary/split are mutually dependent; fixed iterations keep the torch on their intersection.
        float x = fullWidth ? u : (right ? 0.5f : 0f) + u * 0.5f;
        float y = (row + v) / rows;
        for (int i = 0; i < 8; i++)
        {
            float split = Split(y);
            x = fullWidth ? u : Mathf.Lerp(right ? split : 0f, right ? 1f : split, u);
            y = Mathf.Lerp(Boundary(row, rows, x), Boundary(row + 1, rows, x), v);
        }
        return new Vector2(x, y);
    }
}
