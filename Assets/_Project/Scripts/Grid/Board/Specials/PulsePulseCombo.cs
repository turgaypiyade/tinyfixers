using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PulseCore+PulseCore combo: clears a 5×5 area (radius 2) instead of 3×3.
/// Implements IComboExecutor to also fire the explosion VFX.
/// </summary>
public class PulsePulseCombo : IComboBehavior, IComboExecutor
{
    public int Priority => 300;

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return a == TileSpecial.PulseCore && b == TileSpecial.PulseCore;
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        for (int x = originX - 2; x <= originX + 2; x++)
        for (int y = originY - 2; y <= originY + 2; y++)
        {
            if (SpecialUtils.CanAffectCell(board, x, y))
                cells.Add(new Vector2Int(x, y));
        }
        return cells;
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var a = ctx.TileA;

        ComboBehaviorEvents.EmitComboTriggered(ctx.SpecialA, ctx.SpecialB, new Vector2Int(a.X, a.Y));
        ctx.Board.PlayPulsePulseExplosionVfxAtCell(a.X, a.Y);
        SpecialCellUtils.AddSquare(res.Affected, res, ctx.Board, a.X, a.Y, 2);
    }
}