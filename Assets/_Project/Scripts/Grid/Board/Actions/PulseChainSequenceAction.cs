using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a PulseCore-involved special chain SEQUENTIALLY: each special clears its
/// own tiles immediately, gravity starts, and the next special "catches" the
/// still-falling tiles.
///
/// Per step:
///   - PulseCore  → explosion VFX + clear its 5x5 area immediately.
///   - Other special (Line, ...) found inside a pulse area → activated inline via the
///     resolveOtherSpecial callback (its own sweep/clear), as its own step.
///   After each step: CalculateCascades (board.Tiles updated synchronously) runs the
///   fall as a background job; we wait catchOverlap × fallDuration before the next
///   step, so the next clear destroys the falling tiles mid-air (MoveToGridCell
///   null-checks per frame, so this is safe).
///
/// A pulse-area PulseCore is queued (not cleared) so it explodes on its own turn.
/// A non-pulse special is queued (not cleared) so it activates on its own turn.
/// </summary>
public sealed class PulseChainSequenceAction : BoardAction
{
    private readonly BoardController board;
    private readonly List<TileView> initialSpecials;
    private readonly int areaHalf;
    private readonly float catchOverlap;
    private readonly Func<TileView, List<BoardAction>> resolveOtherSpecial;

    public override bool Blocking => true;

    public PulseChainSequenceAction(
        BoardController board,
        List<TileView> initialSpecials,
        int areaHalf,
        float catchOverlapFraction,
        Func<TileView, List<BoardAction>> resolveOtherSpecial = null)
    {
        this.board = board;
        this.initialSpecials = initialSpecials ?? new List<TileView>();
        this.areaHalf = Mathf.Max(1, areaHalf);
        this.catchOverlap = Mathf.Clamp01(catchOverlapFraction);
        this.resolveOtherSpecial = resolveOtherSpecial;
    }

    public override IEnumerator ExecuteVisuals(ActionSequencer sequencer)
    {
        if (board == null || initialSpecials.Count == 0)
            yield break;

        var queue = new Queue<TileView>(initialSpecials);
        var processed = new HashSet<TileView>();

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (t == null || !t) continue;
            if (processed.Contains(t)) continue;

            // The tile may have fallen during a previous step's gravity; read its live
            // cell. If it was consumed/cleared meanwhile, skip it.
            int cx = t.X, cy = t.Y;
            if (cx < 0 || cx >= board.Width || cy < 0 || cy >= board.Height) continue;
            if (board.Tiles[cx, cy] != t) continue;

            var special = t.GetSpecial();
            if (special == TileSpecial.None) continue;

            processed.Add(t);

            if (special == TileSpecial.PulseCore)
            {
                yield return ExplodePulse(sequencer, t, cx, cy, queue, processed);
            }
            else if (resolveOtherSpecial != null)
            {
                // Non-pulse special inside the chain — activate its own effect inline.
                var acts = resolveOtherSpecial(t);
                if (acts != null)
                {
                    for (int i = 0; i < acts.Count; i++)
                        if (acts[i] != null)
                            yield return acts[i].ExecuteVisuals(sequencer);
                }
                yield return RunGravityWithOverlap(queue.Count > 0);
            }

            // After this step, any newly-arrived specials are discovered on the next
            // pulse area scan (live board). Non-pulse specials reach the queue only
            // through pulse area scans below.
        }

        // Wait for any still-running background falls to settle before finishing.
        float safety = 0f;
        while (board.ActiveBackgroundJobs > 0 && safety < 5f)
        {
            safety += Time.deltaTime;
            yield return null;
        }

        board.RefreshAllSortingOrders();
    }

    private IEnumerator ExplodePulse(
        ActionSequencer sequencer, TileView pulse, int cx, int cy,
        Queue<TileView> queue, HashSet<TileView> processed)
    {
        // 1) Explosion VFX.
        board.PulseCoreImpactService?.PlayPulseCoreExplosionVfxAtCell(cx, cy, radiusCells: areaHalf);

        // 2) Collect area: normal tiles clear, chained specials queue.
        var clearTiles = new HashSet<TileView> { pulse };
        var affectedCells = new HashSet<Vector2Int>();
        var impactCells = new List<Vector2Int>();
        var perTileDelays = new Dictionary<TileView, float>();

        for (int x = cx - areaHalf; x <= cx + areaHalf; x++)
        {
            for (int y = cy - areaHalf; y <= cy + areaHalf; y++)
            {
                if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) continue;

                if (!SpecialUtils.CanAffectCell(board, x, y))
                {
                    if (board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y))
                        impactCells.Add(new Vector2Int(x, y));
                    continue;
                }

                affectedCells.Add(new Vector2Int(x, y));

                var tile = board.Tiles[x, y];
                if (tile == null) continue;

                var sp = tile.GetSpecial();

                // Another PulseCore → chains (explodes on its turn).
                if (tile != pulse && sp == TileSpecial.PulseCore && !processed.Contains(tile))
                {
                    if (!queue.Contains(tile)) queue.Enqueue(tile);
                    continue;
                }

                // A non-pulse special, and we can activate it inline → chains too.
                if (tile != pulse && sp != TileSpecial.None && sp != TileSpecial.PulseCore
                    && resolveOtherSpecial != null && !processed.Contains(tile))
                {
                    if (!queue.Contains(tile)) queue.Enqueue(tile);
                    continue;
                }

                clearTiles.Add(tile);
            }
        }

        // Radial clear delay (wave feel).
        foreach (var ct in clearTiles)
        {
            int dist = Mathf.Max(Mathf.Abs(ct.X - cx), Mathf.Abs(ct.Y - cy));
            perTileDelays[ct] = dist * board.PulseImpactDelayStep;
        }

        // 3) Immediate clear of this explosion's tiles.
        var stepClear = new MatchClearAction(
            clearTiles,
            doShake: true,
            staggerDelays: null,
            staggerAnimTime: board.ApplySpecialChainTempo(board.PulseImpactAnimTime),
            animationMode: ClearAnimationMode.Default,
            affectedCells: affectedCells,
            impactCells: impactCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: null,
            lightningLineStrikes: null,
            suppressPerTileClearVfx: false,
            perTileClearDelays: perTileDelays,
            isSpecialPhase: true,
            presentationPlan: null);

        yield return stepClear.ExecuteVisuals(sequencer);

        // 4) Gravity + overlap.
        yield return RunGravityWithOverlap(queue.Count > 0);
    }

    // Runs CalculateCascades (board.Tiles updated synchronously) as background fall
    // job(s), then waits catchOverlap × fallDuration so the next step can catch the
    // still-falling tiles.
    private IEnumerator RunGravityWithOverlap(bool hasNext)
    {
        float fallDuration = 0f;
        var cascades = board.CascadeLogic.CalculateCascades();
        if (cascades != null && cascades.Count > 0)
        {
            for (int i = 0; i < cascades.Count; i++)
            {
                if (cascades[i] is FallAction fa)
                    fallDuration = Mathf.Max(fallDuration, fa.GetEstimatedVisualDuration(board));
                board.StartImmediateAction(cascades[i]);
            }
        }
        board.RefreshAllSortingOrders();

        if (hasNext && fallDuration > 0f && catchOverlap > 0f)
            yield return new WaitForSeconds(fallDuration * catchOverlap);
    }
}
