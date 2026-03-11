using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PulseCore+Line combo that applies 3 rows + 3 columns (full cross with width 3).
/// Also known as "PulseEmitter" effect.
/// </summary>
public class PulseLineCrossCombo : IComboBehavior
{
    public bool Matches(TileSpecial a, TileSpecial b)
    {
        // This matches when both Line and PulseCore are present
        // and the combo should produce a full 3-wide cross
        return false; // Reserved for future use — currently PulseLineCombo handles this
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                       TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        int[] offsets = { -1, 0, 1 };

        // 3 rows + 3 columns
        foreach (int dy in offsets)
            cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineH, board, originX, originY + dy));
        foreach (int dx in offsets)
            cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineV, board, originX + dx, originY));

        return cells;
    }
}
