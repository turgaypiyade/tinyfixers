using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PulseCore+Line combo: clears 3 parallel rows (if LineH) or 3 parallel columns (if LineV).
/// </summary>
public class PulseLineCombo : IComboBehavior, IComboExecutor, ILightningComboBehavior
{
    public int Priority => 100;
    public bool HasLineActivation => true;

    public IEnumerable<LightningLineStrike> GetLineStrikes(int originX, int originY, TileSpecial a, TileSpecial b)
    {
        TileSpecial line = IsLine(a) ? a : b;
        int[] offsets = { -1, 0, 1 };

        if (line == TileSpecial.LineH)
        {
            foreach (var dy in offsets)
                yield return new LightningLineStrike(new Vector2Int(originX, originY + dy), true);
        }
        else
        {
            foreach (var dx in offsets)
                yield return new LightningLineStrike(new Vector2Int(originX + dx, originY), false);
        }
    }

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return (IsLine(a) && IsPulse(b)) || (IsPulse(a) && IsLine(b));
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
                                                       TileSpecial specialA, TileSpecial specialB)
    {
        var cells = new HashSet<Vector2Int>();
        TileSpecial line = IsLine(specialA) ? specialA : specialB;
        int[] offsets = { -1, 0, 1 };

        if (line == TileSpecial.LineH)
        {
            foreach (int dy in offsets)
                cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineH, board, originX, originY + dy));
        }
        else
        {
            foreach (int dx in offsets)
                cells.UnionWith(board.SpecialBehaviors.CalculateEffect(TileSpecial.LineV, board, originX + dx, originY));
        }

        return cells;
    }


    public static PulseLineComboAction CreatePulseEmitterComboAction(BoardController board, int cx, int cy)
    {
        var targets = board.BuildPulseEmitterTargets(cx, cy);

        RectTransform space = null;
        if (board.lineTravelPlayer != null)
            space = board.lineTravelPlayer.afterImageParent != null
                ? board.lineTravelPlayer.afterImageParent
                : (board.LineTravelSpawnParent as RectTransform);

        var hOrigins = new List<(Vector2Int cell, Vector2 anch)>();
        var vOrigins = new List<(Vector2Int cell, Vector2 anch)>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= board.Height) continue;
            var originTile = board.Tiles[cx, yy];
            if (originTile == null) continue;

            var rt = originTile.GetComponent<RectTransform>();
            var wc = rt.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
            hOrigins.Add((new Vector2Int(cx, yy), board.WorldToAnchoredIn(space, wc)));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= board.Width) continue;
            var originTile = board.Tiles[xx, cy];
            if (originTile == null) continue;

            var rt = originTile.GetComponent<RectTransform>();
            var wc = rt.TransformPoint(new Vector3(board.TileSize * 0.5f, -board.TileSize * 0.5f, 0f));
            vOrigins.Add((new Vector2Int(xx, cy), board.WorldToAnchoredIn(space, wc)));
        }

        var targetVisuals = new Dictionary<Vector2Int, (TileType type, TileView view)>();
        foreach (var c in targets)
        {
            var tile = board.Tiles[c.x, c.y];
            if (tile != null)
                targetVisuals[c] = (tile.GetTileType(), tile);
        }

        foreach (var c in targets)
            board.ClearCellDataOnly(c);

        return new PulseLineComboAction(board, cx, cy, targets, hOrigins, vOrigins, targetVisuals);
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var board = ctx.Board;
        var origin = new Vector2Int(ctx.TileA.X, ctx.TileA.Y);

        ctx.Effects.EmitComboTriggered(ctx.SpecialA, ctx.SpecialB, origin);

        var cells = CalculateAffectedCells(board, origin.x, origin.y, ctx.SpecialA, ctx.SpecialB);
        foreach (var c in cells)
        {
            SpecialCellUtils.MarkAffectedCell(res, c.x, c.y, board);

            var tile = board.Tiles[c.x, c.y];
            if (tile == null) continue;

            res.Affected.Add(tile);
            res.LightningVisualTargets.Add(tile);
        }

        var strikes = GetLineStrikes(origin.x, origin.y, ctx.SpecialA, ctx.SpecialB);
        if (strikes != null)
            res.LightningLineStrikes.AddRange(strikes);

        res.HasLineActivation = true;
    }

    static bool IsLine(TileSpecial s) => s == TileSpecial.LineH || s == TileSpecial.LineV;
    static bool IsPulse(TileSpecial s) => s == TileSpecial.PulseCore;
}
