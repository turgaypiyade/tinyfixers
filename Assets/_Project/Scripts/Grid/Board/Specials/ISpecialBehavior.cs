using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Each special tile type implements this interface to define its
/// self-contained behavior: which cells it affects and what VFX it shows.
/// </summary>
public interface ISpecialBehavior
{
    TileSpecial SpecialType { get; }

    /// <summary>
    /// Returns the set of cells this special affects when activated at (originX, originY).
    /// Does NOT include the origin cell itself — the caller adds the origin.
    /// </summary>
    HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY);

    /// <summary>
    /// Creates the visual BoardAction(s) for this activation.
    /// Returns null if no custom VFX is needed (default clear anim will run).
    /// </summary>
    BoardAction CreateVisualAction(BoardController board, int originX, int originY,
                                    HashSet<Vector2Int> affectedCells);
}
