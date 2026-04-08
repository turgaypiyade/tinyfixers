using System.Collections.Generic;
using UnityEngine;

public sealed class PulseWaveEffectDescriptor : ClearEffectDescriptorBase
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

    public override string EffectKey => "pulse_wave";
    public override EffectTimingSemantics Timing => _semantics;

    public Dictionary<TileView, float> DelayMap { get; private set; }
    public float ImpactAnimTime { get; private set; }
    public Vector2Int CenterCell { get; private set; }
    public int CenterRadiusCells { get; private set; }
    public bool ClearOnImpact { get; private set; }
    public float TailHoldSecondsOverride { get; private set; }

    public PulseWaveEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        Dictionary<TileView, float> delayMap,
        float impactAnimTime,
        Vector2Int centerCell,
        int centerRadiusCells = 2,
        bool clearOnImpact = false,
        float tailHoldSecondsOverride = 0.02f)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        DelayMap = delayMap;
        ImpactAnimTime = impactAnimTime;
        CenterCell = centerCell;
        CenterRadiusCells = Mathf.Max(1, centerRadiusCells);
        ClearOnImpact = clearOnImpact;
        TailHoldSecondsOverride = Mathf.Max(0f, tailHoldSecondsOverride);
    }
}
