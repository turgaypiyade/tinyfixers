using UnityEngine;

public readonly struct ObstacleDamageRequest
{
    public readonly Vector2Int cell;
    public readonly ObstacleHitContext context;
    public readonly TileType? normalMatchTileType;

    public ObstacleDamageRequest(
        Vector2Int cell,
        ObstacleHitContext context,
        TileType? normalMatchTileType = null)
    {
        this.cell = cell;
        this.context = context;
        this.normalMatchTileType = context == ObstacleHitContext.NormalMatch
            ? normalMatchTileType
            : null;
    }

    public ObstacleDamageRequest(
        int x,
        int y,
        ObstacleHitContext context,
        TileType? normalMatchTileType = null)
        : this(new Vector2Int(x, y), context, normalMatchTileType)
    {
    }

    public ObstacleDamageRequest WithContext(ObstacleHitContext newContext)
    {
        return new ObstacleDamageRequest(cell, newContext, normalMatchTileType);
    }

    public static ObstacleDamageRequest NormalMatch(Vector2Int cell, TileType tileType)
    {
        return new ObstacleDamageRequest(cell, ObstacleHitContext.NormalMatch, tileType);
    }

    public static ObstacleDamageRequest Special(Vector2Int cell)
    {
        return new ObstacleDamageRequest(cell, ObstacleHitContext.SpecialActivation, null);
    }

    public static ObstacleDamageRequest Booster(Vector2Int cell)
    {
        return new ObstacleDamageRequest(cell, ObstacleHitContext.Booster, null);
    }

    public static ObstacleDamageRequest Scripted(Vector2Int cell)
    {
        return new ObstacleDamageRequest(cell, ObstacleHitContext.Scripted, null);
    }
}
