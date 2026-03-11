using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Line special: clears an entire row (LineH) or column (LineV).
/// Handles both orientations via a single implementation.
/// </summary>
public class LineBehavior : ISpecialBehavior, ILightningBehavior
{
    private readonly TileSpecial lineType;

    public TileSpecial SpecialType => lineType;
    public bool HasLineActivation => true;

    public LineBehavior(TileSpecial type)
    {
        Debug.Assert(type == TileSpecial.LineH || type == TileSpecial.LineV,
            $"LineBehavior created with invalid type: {type}");
        lineType = type;
    }

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY)
    {
        yield return new LightningLineStrike(new Vector2Int(originX, originY), lineType == TileSpecial.LineH);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        if (lineType == TileSpecial.LineH)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (SpecialUtils.CanAffectCell(board, x, originY))
                    cells.Add(new Vector2Int(x, originY));
            }
        }
        else // LineV
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (SpecialUtils.CanAffectCell(board, originX, y))
                    cells.Add(new Vector2Int(originX, y));
            }
        }

        return cells;
    }

    public BoardAction CreateVisualAction(BoardController board, int originX, int originY,
                                           HashSet<Vector2Int> affectedCells)
    {
        // Line VFX is handled by MatchClearAction with LightningStrike mode.
        // Return null — the caller configures ClearAnimationMode.
        return null;
    }
}
