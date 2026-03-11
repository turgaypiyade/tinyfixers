using System.Collections.Generic;
using UnityEngine;

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

    public BoardAction CreateVisualAction(BoardController board, int originX, int originY,
                                           HashSet<Vector2Int> affectedCells)
    {
        // PulseCore VFX is handled by default clear animation with stagger.
        return null;
    }
}
