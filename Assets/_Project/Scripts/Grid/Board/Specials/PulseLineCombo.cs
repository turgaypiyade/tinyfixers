using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PulseCore+Line combo: clears 3 parallel rows (if LineH) or 3 parallel columns (if LineV).
/// </summary>
public class PulseLineCombo : IComboBehavior
{
    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return (IsLine(a) && IsPulse(b)) || (IsPulse(a) && IsLine(b));
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                       TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        TileSpecial line = IsLine(specialA) ? specialA : specialB;
        int[] offsets = { -1, 0, 1 };

        if (line == TileSpecial.LineH)
        {
            foreach (int dy in offsets)
                cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineH, board, originX, originY + dy));
        }
        else
        {
            foreach (int dx in offsets)
                cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineV, board, originX + dx, originY));
        }

        return cells;
    }

    static bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
    static bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
}
