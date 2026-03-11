using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event hub for SystemOverride (Ion) visual lifecycle.
/// </summary>
public static class SystemOverrideBehaviorEvents
{
    public static event Action<float> OverrideComboVfxPlayed;
    public static event Action<Vector2Int, TileSpecial> OverrideFanoutStarted;

    public static void EmitOverrideComboVfxPlayed(float duration)
    {
        OverrideComboVfxPlayed?.Invoke(duration);
    }

    public static void EmitOverrideFanoutStarted(Vector2Int originCell, TileSpecial targetSpecial)
    {
        OverrideFanoutStarted?.Invoke(originCell, targetSpecial);
    }
}

/// <summary>
/// SystemOverride special: selects all tiles on the board matching a specific type.
/// The base type is stored on the tile via GetOverrideBaseType.
/// </summary>
public class SystemOverrideBehavior : ISpecialBehavior
{
    public TileSpecial SpecialType => TileSpecial.SystemOverride;

    public HashSet<Vector2Int> CalculateAffectedCells(BoardController board, int originX, int originY)
    {
        var cells = new HashSet<Vector2Int>();

        var originTile = board.Tiles[originX, originY];
        if (originTile == null) return cells;

        TileType baseType = originTile.GetOverrideBaseType(out var storedType)
            ? storedType
            : originTile.GetTileType();

        for (int x = 0; x < board.Width; x++)
        for (int y = 0; y < board.Height; y++)
        {
            if (!SpecialUtils.CanAffectCell(board, x, y)) continue;
            var tile = board.Tiles[x, y];
            if (tile == null) continue;
            if (!tile.GetTileType().Equals(baseType)) continue;
            if (tile.GetSpecial() != TileSpecial.None) continue; // Don't select other specials

            cells.Add(new Vector2Int(x, y));
        }

        return cells;
    }
}
