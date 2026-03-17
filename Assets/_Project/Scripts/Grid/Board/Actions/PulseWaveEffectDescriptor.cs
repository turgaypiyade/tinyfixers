using System.Collections.Generic;
using UnityEngine;

public sealed class PulseWaveEffectDescriptor : IClearEffectDescriptor
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
        get { return "pulse_wave"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

    public Dictionary<TileView, float> DelayMap { get; private set; }
    public float ImpactAnimTime { get; private set; }
    public Vector2Int CenterCell { get; private set; }

    public PulseWaveEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        Dictionary<TileView, float> delayMap,
        float impactAnimTime,
        Vector2Int centerCell)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        DelayMap = delayMap;
        ImpactAnimTime = impactAnimTime;
        CenterCell = centerCell;
    }
}