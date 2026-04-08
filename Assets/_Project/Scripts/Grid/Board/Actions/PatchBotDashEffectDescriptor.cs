using System.Collections.Generic;
using UnityEngine;

public sealed class PatchBotDashEffectDescriptor : ClearEffectDescriptorBase
{
    private static readonly EffectTimingSemantics _semantics = new EffectTimingSemantics
    {
        IsBlocking = true,
        CanRunInParallel = false,
        TileClearMoment = TileClearMoment.AfterEffect,
        TargetingMode = EffectTargetingMode.Single,
        LeadInSeconds = 0f,
        TailHoldSeconds = 0.02f,
        StepDelaySeconds = 0f
    };

    public override string EffectKey => "patchbot_dash";
    public override EffectTimingSemantics Timing => _semantics;

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
