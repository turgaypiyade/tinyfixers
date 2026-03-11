using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a combo between two special tiles.
/// Each combo knows which special pair it handles and how to calculate the effect.
/// </summary>
public interface IComboBehavior
{
    /// <summary>
    /// Returns true if this combo handles the given pair of specials (order-independent).
    /// </summary>
    bool Matches(TileSpecial a, TileSpecial b);

    /// <summary>
    /// Calculates the cells affected by this combo at the given origin.
    /// Returns the affected cells. The caller handles chain reactions.
    /// </summary>
    HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                TileSpecial specialA, TileSpecial specialB);
}
