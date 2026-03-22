using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialCreationFormationEffectDescriptor : IClearEffectDescriptor
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
        get { return "special_creation_formation"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public TileView CreatedTile { get; private set; }

    // interface ile uyumlu olmalı
    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

    public float Duration { get; private set; }

    public Vector2Int? MergeTargetCell { get; private set; }
    public float ClearAtNormalizedTime { get; private set; }
    public float TailHoldSeconds { get; private set; }

    // mevcut creation merge kullanımı
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
        TailHoldSeconds = 0.02f;
    }

    // yeni pulse implode kullanımı
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