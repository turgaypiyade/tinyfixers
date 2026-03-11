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

    private readonly int radius;

    public PulseCoreBehavior(int radius = 1)
    {
        this.radius = radius;
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        for (int x = originX - radius; x <= originX + radius; x++)
        for (int y = originY - radius; y <= originY + radius; y++)
        {
            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;
            if (!SpecialUtils.CanAffectCell(board, x, y)) continue;
            cells.Add(new Vector2Int(x, y));
        }

        return cells;
    }
}
