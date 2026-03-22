using System.Collections.Generic;
using UnityEngine;

public enum TileClearMoment
{
    Immediate,
    OnHit,
    AfterEffect
}

public enum EffectTargetingMode
{
    Single,
    Batch,
    Ordered,
    Wave
}

public sealed class EffectTimingSemantics
{
    public bool IsBlocking { get; set; }
    public bool CanRunInParallel { get; set; }
    public TileClearMoment TileClearMoment { get; set; }
    public EffectTargetingMode TargetingMode { get; set; }
    public float LeadInSeconds { get; set; }
    public float TailHoldSeconds { get; set; }
    public float StepDelaySeconds { get; set; }
}

public interface IClearEffectDescriptor
{
    string EffectKey { get; }
    EffectTimingSemantics Timing { get; }
    IList<TileView> TargetTiles { get; }
    IList<Vector2Int> TargetCells { get; }
}