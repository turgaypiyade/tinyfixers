using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LineH special: clears a full row from the activation origin.
/// LineV artık LineVSpecial içinde sahipleniliyor.
/// </summary>
public sealed class LineHorizontalBehavior : ISpecialBehavior, ILightningBehavior
{
    public TileSpecial SpecialType => TileSpecial.LineH;
    public bool HasLineActivation => true;

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY)
    {
        yield return new LightningLineStrike(new Vector2Int(originX, originY), true);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        for (int x = 0; x < board.Width; x++)
        {
            if (SpecialUtils.CanAffectCell(board, x, originY))
                cells.Add(new Vector2Int(x, originY));
        }

        return cells;
    }
}
