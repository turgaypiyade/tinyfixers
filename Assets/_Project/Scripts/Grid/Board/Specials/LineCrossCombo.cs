using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Line+Line combo: clears a full cross (1 row + 1 column) from the origin.
/// </summary>
public class LineCrossCombo : IComboBehavior, ILightningComboBehavior
{
    public int Priority => 200;
    public bool HasLineActivation => true;

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY, TileSpecial a, TileSpecial b)
    {
        yield return new LightningLineStrike(new Vector2Int(originX, originY), true);
        yield return new LightningLineStrike(new Vector2Int(originX, originY), false);
    }

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return IsLine(a) && IsLine(b);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                       TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        // Cross = full row + full column from origin
        var rowCells = board.SpecialBehaviors.CalculateEffect(TileSpecial.LineH, board, originX, originY);
        var colCells = board.SpecialBehaviors.CalculateEffect(TileSpecial.LineV, board, originX, originY);
        cells.UnionWith(rowCells);
        cells.UnionWith(colCells);
        return cells;
    }

    static bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
}
