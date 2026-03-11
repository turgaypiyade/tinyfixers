using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event hub for PulseCore visual lifecycle.
/// Keeps pulse animation triggers out of BoardController public API.
/// </summary>
public static class PulseBehaviorEvents
{
    public static event Action<Vector2Int> PulseExplosionPlayed;
    public static event Action<Vector2Int> PulseEmitterComboTriggered;

    public static void EmitPulseExplosionPlayed(Vector2Int cell)
    {
        PulseExplosionPlayed?.Invoke(cell);
    }

    public static void EmitPulseEmitterComboTriggered(Vector2Int centerCell)
    {
        PulseEmitterComboTriggered?.Invoke(centerCell);
    }
}

/// <summary>
/// PulseCore special: explodes a square area around the origin (default radius=1 → 3×3).
/// </summary>
public class PulseCoreBehavior : ISpecialBehavior
{
    public TileSpecial SpecialType => TileSpecial.PulseCore;

    private readonly int affectedCellCount;

    public PulseCoreBehavior(int affectedCellCount = 9)
    {
        this.affectedCellCount = Mathf.Max(1, affectedCellCount);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        // Build candidates in a centered square window based on desired cell count.
        // Use deterministic ordering by distance to center, then row/column to cap exactly.
        int side = Mathf.CeilToInt(Mathf.Sqrt(affectedCellCount));
        if (side % 2 == 0) side += 1; // keep origin centered
        int half = side / 2;

        var candidates = new List<Vector2Int>(side * side);

        for (int x = originX - half; x <= originX + half; x++)
        for (int y = originY - half; y <= originY + half; y++)
        {
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
            if (!SpecialUtils.CanAffectCell(board, x, y)) continue;
            candidates.Add(new Vector2Int(x, y));
        }

        return cells;
    }
}
