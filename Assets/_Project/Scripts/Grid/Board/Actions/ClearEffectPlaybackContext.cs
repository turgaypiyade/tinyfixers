using System;
using UnityEngine;

public sealed class ClearEffectPlaybackContext
{
    public delegate bool TileLifetimeLookup(TileView tile, out int lifetime);
    public TileLifetimeLookup CurrentTileLifetime { get; set; }

    public bool TryGetCurrentTileLifetime(TileView tile, out int lifetime)
    {
        lifetime = -1;
        return CurrentTileLifetime != null && CurrentTileLifetime(tile, out lifetime);
    }

    public bool IsCurrentTile(TileView tile) => TryGetCurrentTileLifetime(tile, out _);

    public Action<TileView> ClearTileNow { get; set; }
    public Action<Vector2Int> NotifyCellImpactNow { get; set; }
}
