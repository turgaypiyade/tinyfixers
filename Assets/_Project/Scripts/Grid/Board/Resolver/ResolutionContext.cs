using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds all mutable state for a single special resolution pass.
/// Created by SpecialResolver at the start of each Resolve call,
/// passed by reference to all sub-services, and discarded when the pass ends.
///
/// This replaces the 11+ instance fields that previously lived on SpecialResolver.
/// </summary>
public class ResolutionContext
{
    // ── Core resolution state ──
    public HashSet<Vector2Int> AffectedCells;
    public readonly HashSet<TileView> Affected = new();
    public readonly HashSet<Vector2Int> Processed = new();
    public readonly HashSet<Vector2Int> Queued = new();
    public readonly Queue<SpecialActivation> Queue = new();

    // Lightning / line tracking
    public bool HasLineActivation;
    public readonly HashSet<TileView> LightningVisualTargets = new();
    public readonly List<LightningLineStrike> LightningLineStrikes = new();

    // ── SystemOverride fan-out state ──
    public TileView OverrideFanoutOrigin;
    public readonly List<TileView> OverrideFanoutTargets = new();
    public bool OverrideForceDefaultClearAnim;
    public bool OverrideSuppressPerTileClearVfx;
    public bool OverrideFanoutNormalSelectionPulse;
    public int OverrideFanoutPulseHitCount;
    public readonly List<PendingOverrideImplant> PendingOverrideImplants = new();
    public Dictionary<TileView, float> OverrideRadialClearDelays;
    public float OverrideVfxDuration;
    public readonly HashSet<TileView> OverrideImplantedTiles = new();
    public readonly List<Vector2Int> OverrideDeferredPulseExplosions = new();
    public bool DeferOverrideImplantVisualRefresh;

    public const float OverrideRadialClearDuration = 0.45f;
    public readonly List<Vector2Int> OverrideDeferredPulseActivations = new();
    /// <summary>
    /// DTO for decoupling logic from visuals — pending override implant data.
    /// </summary>
    public readonly struct PendingOverrideImplant
    {
        public readonly Vector2Int targetCell;
        public readonly TileSpecial special;
        public readonly Vector2Int? partnerCell;
        public readonly Vector2Int overrideCell;

        public PendingOverrideImplant(Vector2Int targetCell, TileSpecial special, Vector2Int? partnerCell, Vector2Int overrideCell)
        {
            this.targetCell = targetCell;
            this.special = special;
            this.partnerCell = partnerCell;
            this.overrideCell = overrideCell;
        }
    }

    /// <summary>
    /// Represents a queued special tile activation.
    /// </summary>
    public readonly struct SpecialActivation
    {
        public readonly Vector2Int cell;
        public readonly Vector2Int? partnerCell;

        public SpecialActivation(Vector2Int cell, Vector2Int? partnerCell)
        {
            this.cell = cell;
            this.partnerCell = partnerCell;
        }
    }

    public ResolutionContext()
    {
        Reset();
    }

    public void Reset()
    {
        AffectedCells = new HashSet<Vector2Int>();
        Affected.Clear();
        Processed.Clear();
        Queued.Clear();
        Queue.Clear();

        HasLineActivation = false;
        LightningVisualTargets.Clear();
        LightningLineStrikes.Clear();

        OverrideFanoutOrigin = null;
        OverrideFanoutTargets.Clear();
        OverrideForceDefaultClearAnim = false;
        OverrideSuppressPerTileClearVfx = false;
        OverrideFanoutNormalSelectionPulse = false;
        OverrideFanoutPulseHitCount = 0;
        PendingOverrideImplants.Clear();
        OverrideRadialClearDelays = null;
        OverrideVfxDuration = 0f;
        OverrideImplantedTiles.Clear();
        DeferOverrideImplantVisualRefresh = false;
        OverrideDeferredPulseExplosions.Clear();
        OverrideDeferredPulseActivations.Clear();
    }
}