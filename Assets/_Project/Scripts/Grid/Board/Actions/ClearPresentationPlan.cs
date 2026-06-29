using System.Collections.Generic;
using UnityEngine;

public sealed class ClearPresentationPlan
{
    public List<IClearEffectDescriptor> Effects { get; } = new();
    public HashSet<TileView> FinalClearTiles { get; } = new();

    // Normal match presentation path'lerinde hangi hücrenin hangi TileType match'ten
    // geldiğini taşır. Special / booster path'lerinde boş kalabilir.
    public Dictionary<Vector2Int, TileType> NormalMatchTileTypeByCell { get; } = new();

    public bool DoBoardShake { get; set; }
    public bool IncludeAdjacentOverTileBlockerDamage { get; set; } = true;
    public ObstacleHitContext ObstacleHitContext { get; set; } = ObstacleHitContext.NormalMatch;
    public bool CommitFinalClearsBeforeEffects { get; set; }

    public void RegisterNormalMatchSource(TileView tile)
    {
        if (tile == null)
            return;

        RegisterNormalMatchSource(new Vector2Int(tile.X, tile.Y), tile.GetTileType());
    }

    public void RegisterNormalMatchSource(Vector2Int cell, TileType tileType)
    {
        NormalMatchTileTypeByCell[cell] = tileType;
    }

    public TileType? GetNormalMatchSourceTileType(Vector2Int cell)
    {
        if (ObstacleHitContext != ObstacleHitContext.NormalMatch)
            return null;

        return NormalMatchTileTypeByCell.TryGetValue(cell, out var tileType)
            ? tileType
            : null;
    }
}
