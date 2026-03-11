using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PulseCore+PulseCore combo: clears a 5×5 area (radius 2) instead of 3×3.
/// </summary>
public class PulsePulseCombo : IComboBehavior
{
    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return a == TileSpecial.PulseCore && b == TileSpecial.PulseCore;
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                       TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        // 5×5 = radius 2
        for (int x = originX - 2; x <= originX + 2; x++)
        for (int y = originY - 2; y <= originY + 2; y++)
        {
            if (SpecialUtils.CanAffectCell(board, x, y))
                cells.Add(new Vector2Int(x, y));
        }
        return cells;
    }
}
