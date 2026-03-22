using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Each special tile type implements this interface to define its
/// self-contained gameplay behavior: which cells it affects.
/// </summary>
public interface ISpecialBehavior
{
    TileSpecial SpecialType { get; }

    /// <summary>
    /// Returns the set of cells this special affects when activated at (originX, originY).
    /// Does NOT include the origin cell itself — the caller adds the origin.
    /// </summary>
    HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY);
}
