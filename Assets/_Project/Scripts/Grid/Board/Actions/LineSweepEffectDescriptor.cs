using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LineSweepEffectDescriptor : ClearEffectDescriptorBase
{
    private static readonly EffectTimingSemantics _semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.OnHit,
        TargetingMode = EffectTargetingMode.Ordered,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public override string EffectKey => "line_sweep";
    public override EffectTimingSemantics Timing => _semantics;

    public IList<LightningLineStrike> LineStrikes { get; private set; }
    public TileView OriginTile { get; private set; }
    public Vector2Int? OriginCell { get; private set; }

    // Cell → callback fired when sweep animation reaches that cell.
    // Used for deferred specials (e.g. Override hit by a line sweep).
    public Dictionary<Vector2Int, Action> ArrivalTriggers { get; private set; }

    public LineSweepEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        IList<LightningLineStrike> lineStrikes,
        TileView originTile,
        Vector2Int? originCell,
        Dictionary<Vector2Int, Action> arrivalTriggers = null)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        LineStrikes = lineStrikes;
        OriginTile = originTile;
        OriginCell = originCell;
        ArrivalTriggers = arrivalTriggers;
    }
}
