using System.Collections.Generic;
using UnityEngine;

public sealed class OverrideRadialEffectDescriptor : IClearEffectDescriptor
{
    private static readonly EffectTimingSemantics semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.AfterEffect,
        TargetingMode = EffectTargetingMode.Wave,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public string EffectKey
    {
        get { return "override_radial"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

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