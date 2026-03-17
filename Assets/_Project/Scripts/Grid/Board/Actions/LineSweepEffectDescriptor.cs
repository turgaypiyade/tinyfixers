using System.Collections.Generic;
using UnityEngine;

public sealed class LineSweepEffectDescriptor : IClearEffectDescriptor
{
    private static readonly EffectTimingSemantics semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.OnHit,
        TargetingMode = EffectTargetingMode.Ordered,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public string EffectKey
    {
        get { return "line_sweep"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

    public IList<LightningLineStrike> LineStrikes { get; private set; }
    public TileView OriginTile { get; private set; }
    public Vector2Int? OriginCell { get; private set; }

    public LineSweepEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        IList<LightningLineStrike> lineStrikes,
        TileView originTile,
        Vector2Int? originCell)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        LineStrikes = lineStrikes;
        OriginTile = originTile;
        OriginCell = originCell;
    }
}