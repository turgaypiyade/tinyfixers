using System;
using UnityEngine;

public sealed class ClearEffectPlaybackContext
{
    public Action<TileView> ClearTileNow { get; set; }
    public Action<Vector2Int> NotifyCellImpactNow { get; set; }
}