using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SystemOverride + SystemOverride combo: clears ALL tiles on the board
/// with a radial wave effect emanating from center.
/// </summary>
public class OverrideOverrideCombo : IComboBehavior, IComboExecutor
{
    public int Priority => 500;

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return a == TileSpecial.SystemOverride && b == TileSpecial.SystemOverride;
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (SpecialUtils.CanAffectCell(board, x, y))
                    cells.Add(new Vector2Int(x, y));
        return cells;
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var a = ctx.TileA;
        var b = ctx.TileB;
        var sa = ctx.SpecialA;
        var sb = ctx.SpecialB;

        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(a.X, a.Y));

        bool aHasBase = a != null && a.GetOverrideBaseType(out _);
        bool bHasBase = b != null && b.GetOverrideBaseType(out _);
        if (aHasBase && bHasBase)
        {
            SpecialVisualService.HideTileVisualForCombo(a);
            SpecialVisualService.HideTileVisualForCombo(b);

            res.OverrideVfxDuration = ctx.Board.PlaySystemOverrideComboVfxAndGetDuration();
            ComboBehaviorEvents.EmitComboVisualQueued(sa, sb, new Vector2Int(a.X, a.Y), res.OverrideVfxDuration);
        }

        SpecialCellUtils.AddAllTiles(res.Affected, res, ctx.Board);

        float clearDuration = Mathf.Max(
            ResolutionContext.OverrideRadialClearDuration,
            res.OverrideVfxDuration * 0.85f);
        res.OverrideRadialClearDelays = ctx.VisualService.BuildCenterOutClearDelays(res.Affected, clearDuration);
    }
}