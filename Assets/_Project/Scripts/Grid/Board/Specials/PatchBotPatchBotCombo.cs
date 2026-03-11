using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot + PatchBot combo: both PatchBots independently teleport to
/// different random targets and hit them.
/// </summary>
public class PatchBotPatchBotCombo : IComboBehavior, IComboExecutor
{
    public int Priority => 100;

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return a == TileSpecial.PatchBot && b == TileSpecial.PatchBot;
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        // Targets are random — can't pre-calculate meaningfully
        return new HashSet<Vector2Int> { new Vector2Int(originX, originY) };
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var board = ctx.Board;
        var a = ctx.TileA;
        var b = ctx.TileB;

        ComboBehaviorEvents.EmitComboTriggered(ctx.SpecialA, ctx.SpecialB, new Vector2Int(a.X, a.Y));

        var usedTargets = new HashSet<TileView>();
        var dataMatches = new HashSet<TileData>();

        var firstTarget = ctx.PatchbotService.FindTarget(a, b, usedTargets);
        if (firstTarget.hasCell)
        {
            if (firstTarget.tile != null) usedTargets.Add(firstTarget.tile);
            ctx.PatchbotService.EnqueueDash(a, firstTarget.x, firstTarget.y);
            ctx.VisualService.PlayTeleportMarkers(a, firstTarget.x, firstTarget.y);
            ctx.PatchbotService.HitCellOnce(dataMatches, firstTarget.x, firstTarget.y, firstTarget.tile,
                (x, y) => SpecialCellUtils.MarkAffectedCell(res, x, y, board),
                (tile) => SpecialCellUtils.MarkAffectedCell(res, tile, board));
        }

        var secondTarget = ctx.PatchbotService.FindTarget(b, a, usedTargets);
        if (secondTarget.hasCell)
        {
            if (secondTarget.tile != null) usedTargets.Add(secondTarget.tile);
            ctx.PatchbotService.EnqueueDash(b, secondTarget.x, secondTarget.y);
            ctx.VisualService.PlayTeleportMarkers(b, secondTarget.x, secondTarget.y);
            ctx.PatchbotService.HitCellOnce(dataMatches, secondTarget.x, secondTarget.y, secondTarget.tile,
                (x, y) => SpecialCellUtils.MarkAffectedCell(res, x, y, board),
                (tile) => SpecialCellUtils.MarkAffectedCell(res, tile, board));
        }

        foreach (var data in dataMatches)
        {
            if (board.Tiles[data.X, data.Y] != null)
                res.Affected.Add(board.Tiles[data.X, data.Y]);
        }
    }
}