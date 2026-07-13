using System;
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
    public readonly List<Vector2Int> ChainExecutionOrder = new();

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
    public Dictionary<TileView, float> OverrideRadialClearDistances;
    public bool UseEventDrivenRadialClear;
    public float OverrideVfxDuration;
    public readonly HashSet<TileView> OverrideImplantedTiles = new();
    public readonly List<Vector2Int> OverrideDeferredPulseExplosions = new();
    public readonly List<Vector2Int> OverrideDeferredPatchBotDashes = new();
    public bool DeferOverrideImplantVisualRefresh;

    public readonly List<Vector2Int> DeferredLineHitOverrideCells = new();

    // Filled before BuildClearAction: sweep animation fires these callbacks when
    // it reaches the keyed cell, so deferred specials start concurrently.
    public Dictionary<Vector2Int, Action> ArrivalTriggers;
    public bool SuppressImmediateOverrideQueueProcessing;
    public const float OverrideRadialClearDuration = 0.20f;
    public readonly List<Vector2Int> OverrideDeferredPulseActivations = new();
    public readonly List<Vector2Int> DeferredPulseComboOverrideCells = new();
    public readonly List<Vector2Int> ImpactCells = new();
    public readonly List<SpecialActivation> OverrideDeferredLineVActivations = new();
    public bool IsPulsePulseComboActive;
    public bool IsPulseCoreActive;
    public bool SuppressOverridePulseSelectionVfx;

    // Override+PulseCore presentation scope:
    // When true, PulseCoreSpecial may still collect affected cells and trigger other specials,
    // but PulseCore -> PulseCore chain reactions are skipped so the explicit Override sequence
    // owns the pulse order.
    public bool SuppressPulseCoreToPulseCoreChain;

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
        IsPulsePulseComboActive = false;
        IsPulseCoreActive = false;
        SuppressOverridePulseSelectionVfx = false;
        SuppressPulseCoreToPulseCoreChain = false;
        SuppressImmediateOverrideQueueProcessing = false;
        AffectedCells = new HashSet<Vector2Int>();
        Affected.Clear();
        Processed.Clear();
        Queued.Clear();
        Queue.Clear();
        ChainExecutionOrder.Clear();
        ImpactCells.Clear();

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
        OverrideRadialClearDistances = null;
        UseEventDrivenRadialClear = false;
        OverrideVfxDuration = 0f;
        OverrideImplantedTiles.Clear();
        DeferOverrideImplantVisualRefresh = false;
        OverrideDeferredPulseExplosions.Clear();
        OverrideDeferredPatchBotDashes.Clear();
        OverrideDeferredLineVActivations.Clear();
        OverrideDeferredPulseActivations.Clear();
        DeferredLineHitOverrideCells.Clear();
        DeferredPulseComboOverrideCells.Clear();
        ArrivalTriggers = null;
    }
}