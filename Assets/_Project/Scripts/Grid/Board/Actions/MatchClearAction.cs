using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchClearAction : BoardAction
{
    private HashSet<TileView> matches;
    private bool doShake;
    private ClearAnimationMode animationMode;
    private HashSet<Vector2Int> affectedCells;
    private ObstacleHitContext? obstacleHitContext;
    private bool includeAdjacentOverTileBlockerDamage;
    private TileView lightningOriginTile;
    private Vector2Int? lightningOriginCell;
    private IReadOnlyCollection<TileView> lightningVisualTargets;
    private IReadOnlyList<LightningLineStrike> lightningLineStrikes;
    private bool suppressPerTileClearVfx;
    private Dictionary<TileView, float> perTileClearDelays;
    private Dictionary<TileView, float> staggerDelays;
    private float staggerAnimTime;
    private bool isSpecialActivationPhase;
    private IReadOnlyList<Vector2Int> impactCells;
    private bool isBlocking;
    private bool enqueueCascadeOnComplete;
    public override bool Blocking => isBlocking;
    // NEW: future-facing generic presentation payload
    public ClearPresentationPlan PresentationPlan { get; }

    public MatchClearAction(
        HashSet<TileView> matches,
        bool doShake = false,
        ClearAnimationMode animationMode = ClearAnimationMode.Default,
        HashSet<Vector2Int> affectedCells = null,
        ObstacleHitContext? obstacleHitContext = null,
        bool includeAdjacentOverTileBlockerDamage = true,
        TileView lightningOriginTile = null,
        Vector2Int? lightningOriginCell = null,
        IReadOnlyCollection<TileView> lightningVisualTargets = null,
        IReadOnlyList<LightningLineStrike> lightningLineStrikes = null,
        bool suppressPerTileClearVfx = false,
        Dictionary<TileView, float> perTileClearDelays = null,
        Dictionary<TileView, float> staggerDelays = null,
        float staggerAnimTime = 0.16f,
        bool isSpecialPhase = false,
        ClearPresentationPlan presentationPlan = null,
        IReadOnlyList<Vector2Int> impactCells = null,
        bool isBlocking = true,
        bool enqueueCascadeOnComplete = false)
    {
        this.matches = matches != null ? new HashSet<TileView>(matches) : new HashSet<TileView>();
        this.doShake = doShake;
        this.animationMode = animationMode;
        this.affectedCells = affectedCells;
        this.obstacleHitContext = obstacleHitContext;
        this.includeAdjacentOverTileBlockerDamage = includeAdjacentOverTileBlockerDamage;
        this.lightningOriginTile = lightningOriginTile;
        this.lightningOriginCell = lightningOriginCell;
        this.lightningVisualTargets = lightningVisualTargets;
        this.lightningLineStrikes = lightningLineStrikes;
        this.suppressPerTileClearVfx = suppressPerTileClearVfx;
        this.perTileClearDelays = perTileClearDelays;
        this.staggerDelays = staggerDelays;
        this.staggerAnimTime = staggerAnimTime;
        this.isSpecialActivationPhase = isSpecialPhase;
        this.PresentationPlan = presentationPlan;
        this.impactCells = impactCells;
        this.isBlocking = isBlocking;
        this.enqueueCascadeOnComplete = enqueueCascadeOnComplete;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (!isBlocking)
            sequencer.Board.ActiveBackgroundJobs++;

        bool hasMatches = matches != null && matches.Count > 0;
        bool hasImpacts = impactCells != null && impactCells.Count > 0;
        bool hasAffected = affectedCells != null && affectedCells.Count > 0;
        bool hasStrikes = lightningLineStrikes != null && lightningLineStrikes.Count > 0;

        if (!hasMatches && !hasImpacts && !hasAffected && !hasStrikes && PresentationPlan == null)
        {
            if (!isBlocking)
                sequencer.Board.ActiveBackgroundJobs--;
            yield break;
        }

        bool prevSpecial = sequencer.Board.IsSpecialActivationPhase;
        if (isSpecialActivationPhase)
            sequencer.Board.IsSpecialActivationPhase = true;

        // Pulse pilot path:
        // If PresentationPlan exists, let the new presentation pipeline own visuals.
        // Otherwise fall back to the legacy clear animation path.
        if (PresentationPlan != null)
        {
            yield return sequencer.Animator.PlayClearPresentation(PresentationPlan);

            if (isSpecialActivationPhase)
                sequencer.Board.IsSpecialActivationPhase = prevSpecial;

            EnqueueCascadeIfNeeded(sequencer);

            if (!isBlocking)
                sequencer.Board.ActiveBackgroundJobs--;

            yield break;
        }

        yield return sequencer.Animator.ClearMatchesAnimated(
            matches, doShake, staggerDelays, staggerAnimTime,
            animationMode, affectedCells, impactCells, obstacleHitContext,
            includeAdjacentOverTileBlockerDamage, lightningOriginTile,
            lightningOriginCell, lightningVisualTargets, lightningLineStrikes,
            suppressPerTileClearVfx, perTileClearDelays);

        if (isSpecialActivationPhase)
            sequencer.Board.IsSpecialActivationPhase = prevSpecial;

        EnqueueCascadeIfNeeded(sequencer);

        if (!isBlocking)
            sequencer.Board.ActiveBackgroundJobs--;
    }

    private void EnqueueCascadeIfNeeded(ActionSequencer sequencer)
    {
        if (!enqueueCascadeOnComplete) return;
        var cascades = sequencer.Board.CascadeLogic.CalculateCascades();
        if (cascades.Count > 0)
            sequencer.Enqueue(cascades);
    }
}