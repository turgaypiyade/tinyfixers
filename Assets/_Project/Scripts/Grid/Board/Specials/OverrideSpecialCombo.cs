using System.Collections.Generic;
using UnityEngine;
using static ResolutionContext;

/// <summary>
/// SystemOverride + AnyOtherSpecial combo: finds all tiles matching the partner's type
/// and either clears them (if partner is normal) or implants the partner's special onto each.
/// Handles fan-out state, pending implants, and chain enqueuing.
/// </summary>
public class OverrideSpecialCombo : IComboBehavior, IComboExecutor
{
    public int Priority => 400; // Below Override+Override

    public bool Matches(TileSpecial a, TileSpecial b)
    {
        return (a == TileSpecial.SystemOverride && b != TileSpecial.SystemOverride)
            || (b == TileSpecial.SystemOverride && a != TileSpecial.SystemOverride);
    }

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY,
        TileSpecial specialA, TileSpecial specialB)
    {
        // Preview: all cells of matching type
        var overrideTile = board.Tiles[originX, originY];
        if (overrideTile == null) return new HashSet<Vector2Int>();

        TileType baseType = overrideTile.GetOverrideBaseType(out var storedType)
            ? storedType : overrideTile.GetTileType();

        var cells = new HashSet<Vector2Int>();
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!SpecialUtils.CanAffectCell(board, x, y)) continue;
                var tile = board.Tiles[x, y];
                if (tile == null || !tile.GetTileType().Equals(baseType)) continue;
                cells.Add(new Vector2Int(x, y));
            }
        return cells;
    }

    public void Execute(ComboExecutionContext ctx)
    {
        var res = ctx.Resolution;
        var board = ctx.Board;
        var a = ctx.TileA;
        var b = ctx.TileB;
        var sa = ctx.SpecialA;
        var sb = ctx.SpecialB;

        bool IsOverride(TileSpecial s) => s == TileSpecial.SystemOverride;

        ComboBehaviorEvents.EmitComboTriggered(sa, sb, new Vector2Int(a.X, a.Y));

        var overrideTile = IsOverride(sa) ? a : b;
        var otherTile = IsOverride(sa) ? b : a;

        if (otherTile == null || overrideTile == null) return;

        res.Affected.Add(overrideTile);
        res.Affected.Add(otherTile);
        SpecialCellUtils.MarkAffectedCell(res, overrideTile, board);
        SpecialCellUtils.MarkAffectedCell(res, otherTile, board);

        TileSpecial targetSpecial = otherTile.GetSpecial();
        bool targetIsLine = targetSpecial == TileSpecial.LineH || targetSpecial == TileSpecial.LineV;
        bool targetIsNormal = targetSpecial == TileSpecial.None;
        TileType baseType = otherTile.GetTileType();

        res.OverrideFanoutOrigin = overrideTile;
        SystemOverrideBehaviorEvents.EmitOverrideFanoutStarted(
            new Vector2Int(overrideTile.X, overrideTile.Y), targetSpecial);
        res.OverrideForceDefaultClearAnim = !targetIsLine;
        res.OverrideSuppressPerTileClearVfx = false;
        res.OverrideFanoutNormalSelectionPulse = targetIsNormal;

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (!SpecialCellUtils.CanAffectCell(board, x, y)) continue;
                var tile = board.Tiles[x, y];
                if (tile == null) continue;
                if (!tile.GetTileType().Equals(baseType)) continue;

                // Mevcut special taşlar zincire girebilmeli
                if (tile.GetSpecial() != TileSpecial.None)
                {
                    res.Affected.Add(tile);
                    SpecialCellUtils.MarkAffectedCell(res, tile, board);
                    ctx.QueueProcessor.EnqueueActivation(res, tile, otherTile);
                    continue;
                }

                res.OverrideFanoutTargets.Add(tile);

                if (targetSpecial == TileSpecial.None)
                {
                    res.Affected.Add(tile);
                    SpecialCellUtils.MarkAffectedCell(res, tile, board);
                    continue;
                }

                Vector2Int targetPos = new Vector2Int(tile.X, tile.Y);
                Vector2Int? partnerPos = new Vector2Int(otherTile.X, otherTile.Y);
                Vector2Int overridePos = new Vector2Int(overrideTile.X, overrideTile.Y);

                res.PendingOverrideImplants.Add(
                    new PendingOverrideImplant(targetPos, targetSpecial, partnerPos, overridePos));
            }
    }
}