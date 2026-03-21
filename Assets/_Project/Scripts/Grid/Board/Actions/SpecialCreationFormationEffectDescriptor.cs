using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialCreationFormationEffectDescriptor : IClearEffectDescriptor
{
    private static readonly EffectTimingSemantics semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.AfterEffect,
        TargetingMode = EffectTargetingMode.Batch,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public string EffectKey
    {
        get { return "special_creation_formation"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

    public TileView CreatedTile { get; private set; }
    public float Duration { get; private set; }

    public SpecialCreationFormationEffectDescriptor(
        TileView createdTile,
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        float duration)
    {
        CreatedTile = createdTile;
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        Duration = duration;
    }
}
