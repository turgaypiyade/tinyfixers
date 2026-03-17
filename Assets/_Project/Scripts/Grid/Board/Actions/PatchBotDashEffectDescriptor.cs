using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotDashEffectDescriptor : IClearEffectDescriptor
{
    private static readonly EffectTimingSemantics semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.AfterEffect,
        TargetingMode = EffectTargetingMode.Single,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public string EffectKey
    {
        get { return "patchbot_dash"; }
    }

    public EffectTimingSemantics Timing
    {
        get { return semantics; }
    }

    public IList<TileView> TargetTiles { get; private set; }
    public IList<Vector2Int> TargetCells { get; private set; }

    public TileView OriginTile { get; private set; }
    public Vector2Int? OriginCell { get; private set; }
    public TileView TargetTile { get; private set; }
    public Vector2Int? TargetCell { get; private set; }

    public PatchBotDashEffectDescriptor(
        IList<TileView> targetTiles,
        IList<Vector2Int> targetCells,
        TileView originTile,
        Vector2Int? originCell,
        TileView targetTile,
        Vector2Int? targetCell)
    {
        TargetTiles = targetTiles;
        TargetCells = targetCells;
        OriginTile = originTile;
        OriginCell = originCell;
        TargetTile = targetTile;
        TargetCell = targetCell;
    }
}