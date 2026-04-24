using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialCreationFormationEffectDescriptor : ClearEffectDescriptorBase
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

    public override string EffectKey => "special_creation_formation";
    public override EffectTimingSemantics Timing => _semantics;

    public TileView CreatedTile { get; private set; }
    public float Duration { get; private set; }
    public Vector2Int? MergeTargetCell { get; private set; }
    public float ClearAtNormalizedTime { get; private set; }
    public float TailHoldSeconds { get; private set; }

    // creation merge
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
        MergeTargetCell = null;
        ClearAtNormalizedTime = 1f;
        TailHoldSeconds = 0.001f;
    }

    // pulse implode
    public SpecialCreationFormationEffectDescriptor(
        IList<TileView> targetTiles,
        Vector2Int mergeTargetCell,
        float duration,
        float clearAtNormalizedTime = 0.72f,
        float tailHoldSeconds = 0.04f)
    {
        CreatedTile = null;
        TargetTiles = targetTiles;
        TargetCells = null;
        Duration = duration;
        MergeTargetCell = mergeTargetCell;
        ClearAtNormalizedTime = Mathf.Clamp01(clearAtNormalizedTime);
        TailHoldSeconds = Mathf.Max(0f, tailHoldSeconds);
    }
}
