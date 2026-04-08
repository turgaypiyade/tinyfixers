using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideRadialEffectDescriptor : ClearEffectDescriptorBase
{
    private static readonly EffectTimingSemantics _semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.AfterEffect,
        TargetingMode = EffectTargetingMode.Wave,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public override string EffectKey => "override_radial";
    public override EffectTimingSemantics Timing => _semantics;

    public Dictionary<TileView, float> DelayMap { get; private set; }
    public TileView OriginTile { get; private set; }
    public Vector2Int? OriginCell { get; private set; }

    public OverrideRadialEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        Dictionary<TileView, float> delayMap,
        TileView originTile,
        Vector2Int? originCell)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        DelayMap = delayMap;
        OriginTile = originTile;
        OriginCell = originCell;
    }
}
