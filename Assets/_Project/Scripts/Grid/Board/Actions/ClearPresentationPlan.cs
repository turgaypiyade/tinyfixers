using System.Collections.Generic;

public sealed class ClearPresentationPlan
{
    public List<IClearEffectDescriptor> Effects { get; } = new();
    public HashSet<TileView> FinalClearTiles { get; } = new();

    public bool DoBoardShake { get; set; }
    public bool IncludeAdjacentOverTileBlockerDamage { get; set; } = true;
    public ObstacleHitContext ObstacleHitContext { get; set; } = ObstacleHitContext.NormalMatch;
}