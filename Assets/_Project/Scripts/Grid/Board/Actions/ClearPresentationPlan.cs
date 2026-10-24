using System.Collections.Generic;
using UnityEngine;

public sealed class ClearPresentationPlan
{
    public List<IClearEffectDescriptor> Effects { get; } = new();
    public HashSet<TileView> FinalClearTiles { get; } = new();
    public HashSet<Vector2Int> ImpactOnlyCells { get; } = new();

    // Normal match presentation path'lerinde hangi hücrenin hangi TileType match'ten
    // geldiğini taşır. Special / booster path'lerinde boş kalabilir.
    public Dictionary<Vector2Int, TileType> NormalMatchTileTypeByCell { get; } = new();

    public bool DoBoardShake { get; set; }
    public bool IncludeAdjacentOverTileBlockerDamage { get; set; } = true;
    public ObstacleHitContext ObstacleHitContext { get; set; } = ObstacleHitContext.NormalMatch;
    public bool CommitFinalClearsBeforeEffects { get; set; }
    public bool BackgroundEffectsBlockResolve { get; set; } = true;

    // Capture when the action is queued, never when a delayed effect starts.
    // The same TileView can already belong to a new stone by then.
    private readonly Dictionary<TileView, int> tileLifetimes = new();
    private bool lifetimesCaptured;

    public void CaptureTileLifetimes()
    {
        if (lifetimesCaptured) return;
        lifetimesCaptured = true;
        foreach (var tile in FinalClearTiles) CaptureTile(tile);
        foreach (var effect in Effects)
        {
            if (effect?.TargetTiles != null)
                foreach (var tile in effect.TargetTiles) CaptureTile(tile);
            if (effect is SpecialCreationFormationEffectDescriptor creation)
                CaptureTile(creation.CreatedTile);
        }
    }

    private void CaptureTile(TileView tile)
    {
        if (tile != null) tileLifetimes[tile] = tile.LifetimeVersion;
    }

    public bool TryGetCurrentTileLifetime(TileView tile, out int lifetime)
    {
        lifetime = -1;
        return tile != null && tileLifetimes.TryGetValue(tile, out lifetime)
            && tile.IsCurrentLifetime(lifetime);
    }

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

    public void RegisterImpactOnlyCell(Vector2Int cell)
    {
        ImpactOnlyCells.Add(cell);
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
