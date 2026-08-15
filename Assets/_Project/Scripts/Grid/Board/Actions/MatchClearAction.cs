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
    private Dictionary<TileView, float> perTileClearDistances;
    private Dictionary<TileView, float> staggerDelays;
    private float staggerAnimTime;
    private bool isSpecialActivationPhase;
    private IReadOnlyList<Vector2Int> impactCells;
    private bool isBlocking;
    private bool enqueueCascadeOnComplete;
    private Vector2Int? implodeTargetCell;
    private Dictionary<Vector2Int, System.Action> arrivalTriggers;
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
        bool enqueueCascadeOnComplete = false,
        Vector2Int? implodeTargetCell = null,
        Dictionary<Vector2Int, System.Action> arrivalTriggers = null,
        Dictionary<TileView, float> perTileClearDistances = null)
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
        this.implodeTargetCell = implodeTargetCell;
        this.arrivalTriggers = arrivalTriggers;
        this.perTileClearDistances = perTileClearDistances;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        // Non-blocking match clear registers a Resolve job so the resolve loop waits for it.
        // The handle's Dispose in finally guarantees no leak even if the clear anim throws/bails.
        System.IDisposable clearJob = isBlocking
            ? null
            : sequencer.Board.BeginJob(BoardController.BoardJobKind.Resolve);
        try
        {
            var inner = RunClear(sequencer);
            while (inner.MoveNext())
                yield return inner.Current;
        }
        finally
        {
            clearJob?.Dispose();
        }
    }

    private IEnumerator RunClear(ActionSequencer sequencer)
    {
        float _mcStart = UnityEngine.Time.realtimeSinceStartup;
        bool trace = sequencer != null
                     && sequencer.Board != null
                     && sequencer.Board.BoardFlowTraceEnabled;

        if (trace)
        {
            Debug.Log(
                $"[PulseClearDebug][MCA] ENTER " +
                $"isSpecialPhase={isSpecialActivationPhase} " +
                $"matches={(matches != null ? matches.Count : -1)} " +
                $"stagger={(staggerDelays != null ? staggerDelays.Count : 0)} " +
                $"perTile={(perTileClearDelays != null ? perTileClearDelays.Count : 0)} " +
                $"plan={(PresentationPlan != null)}");
        }

        if (!isSpecialActivationPhase)
            PruneDeadReferences(sequencer != null ? sequencer.Board : null);

        bool hasMatches = matches != null && matches.Count > 0;
        bool hasImpacts = impactCells != null && impactCells.Count > 0;
        bool hasAffected = affectedCells != null && affectedCells.Count > 0;
        bool hasStrikes = lightningLineStrikes != null && lightningLineStrikes.Count > 0;

        if (!hasMatches && !hasImpacts && !hasAffected && !hasStrikes && PresentationPlan == null)
        {
            yield break;
        }

        bool prevSpecial = sequencer.Board.IsSpecialActivationPhase;
        if (isSpecialActivationPhase)
            sequencer.Board.IsSpecialActivationPhase = true;

        bool hasPlan = PresentationPlan != null;
        if (trace)
            UnityEngine.Debug.Log($"[MatchClear] START matches={matches.Count} plan={hasPlan} blocking={isBlocking} shake={doShake}");

        if (PresentationPlan != null)
        {
            yield return sequencer.Animator.PlayClearPresentation(PresentationPlan);
            if (trace)
                UnityEngine.Debug.Log($"[MatchClear] presentation_done +{(UnityEngine.Time.realtimeSinceStartup - _mcStart):0.000}s");

            if (isSpecialActivationPhase)
                sequencer.Board.IsSpecialActivationPhase = prevSpecial;

            EnqueueCascadeIfNeeded(sequencer);
            yield break;
        }

        yield return sequencer.Animator.ClearMatchesAnimated(
            matches, doShake, staggerDelays, staggerAnimTime,
            animationMode, affectedCells, impactCells, obstacleHitContext,
            includeAdjacentOverTileBlockerDamage, lightningOriginTile,
            lightningOriginCell, lightningVisualTargets, lightningLineStrikes,
            suppressPerTileClearVfx, perTileClearDelays, implodeTargetCell,
            arrivalTriggers, perTileClearDistances);

        if (trace)
            UnityEngine.Debug.Log($"[MatchClear] clear_anim_done +{(UnityEngine.Time.realtimeSinceStartup - _mcStart):0.000}s");

        if (isSpecialActivationPhase)
            sequencer.Board.IsSpecialActivationPhase = prevSpecial;

        float _cascStart = UnityEngine.Time.realtimeSinceStartup;
        EnqueueCascadeIfNeeded(sequencer);
        if (trace)
            UnityEngine.Debug.Log($"[MatchClear] cascade_enqueue +{(UnityEngine.Time.realtimeSinceStartup - _cascStart):0.000}s total={UnityEngine.Time.realtimeSinceStartup - _mcStart:0.000}s");
    }

    public void RemoveFromMatches(TileView tile)
    {
        matches.Remove(tile);
    }

    public void AddArrivalTrigger(Vector2Int cell, System.Action trigger)
    {
        arrivalTriggers ??= new Dictionary<Vector2Int, System.Action>();
        if (!arrivalTriggers.ContainsKey(cell))
            arrivalTriggers[cell] = trigger;
        else
            arrivalTriggers[cell] += trigger;
    }

    private void EnqueueCascadeIfNeeded(ActionSequencer sequencer)
    {
        if (!enqueueCascadeOnComplete) return;
        var cascades = sequencer.Board.CascadeLogic.CalculateCascades();
        if (cascades.Count > 0)
            sequencer.Enqueue(cascades);
    }

    private void PruneDeadReferences(BoardController board)
    {
        if (board == null)
            return;

        if (matches != null)
        {
            var liveMatches = new HashSet<TileView>();
            foreach (var tile in matches)
            {
                if (IsLiveTile(board, tile))
                    liveMatches.Add(tile);
            }
            matches = liveMatches;
        }

        if (lightningVisualTargets != null)
        {
            var liveTargets = new List<TileView>();
            foreach (var tile in lightningVisualTargets)
            {
                if (IsLiveTile(board, tile))
                    liveTargets.Add(tile);
            }
            lightningVisualTargets = liveTargets;
        }

        if (perTileClearDelays != null)
        {
            var liveDelays = new Dictionary<TileView, float>();
            foreach (var pair in perTileClearDelays)
            {
                if (IsLiveTile(board, pair.Key))
                    liveDelays[pair.Key] = pair.Value;
            }
            perTileClearDelays = liveDelays;
        }

        if (perTileClearDistances != null)
        {
            var liveDist = new Dictionary<TileView, float>();
            foreach (var pair in perTileClearDistances)
            {
                if (IsLiveTile(board, pair.Key))
                    liveDist[pair.Key] = pair.Value;
            }
            perTileClearDistances = liveDist;
        }

        if (staggerDelays != null)
        {
            var liveStagger = new Dictionary<TileView, float>();
            foreach (var pair in staggerDelays)
            {
                if (IsLiveTile(board, pair.Key))
                    liveStagger[pair.Key] = pair.Value;
            }
            staggerDelays = liveStagger;
        }

        if (!IsLiveTile(board, lightningOriginTile))
            lightningOriginTile = null;

        if (PresentationPlan != null && PresentationPlan.FinalClearTiles != null)
        {
            var liveFinalTiles = new List<TileView>();
            foreach (var tile in PresentationPlan.FinalClearTiles)
            {
                if (IsLiveTile(board, tile))
                    liveFinalTiles.Add(tile);
            }

            PresentationPlan.FinalClearTiles.Clear();
            foreach (var tile in liveFinalTiles)
                PresentationPlan.FinalClearTiles.Add(tile);
        }
    }

    private static bool IsLiveTile(BoardController board, TileView tile)
    {
        if (board == null || tile == null)
            return false;

        int x = tile.X;
        int y = tile.Y;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        return board.Tiles[x, y] == tile;
    }
}
