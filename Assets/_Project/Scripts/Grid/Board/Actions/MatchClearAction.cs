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
        IReadOnlyList<Vector2Int> impactCells = null)
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
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (matches == null || matches.Count == 0)
            yield break;

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
    }
}