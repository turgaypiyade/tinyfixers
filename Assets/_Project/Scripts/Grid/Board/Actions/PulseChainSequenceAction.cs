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
///   - LineV/LineH → native step: clears its column/row immediately (lightning strike
///     visuals); a special on the path fires THE MOMENT the beam reaches its cell,
///     as a concurrent sub-chain (arrival trigger) — never on a delayed queue turn.
///   - Other special (PatchBot, Override) → activated inline via the
///     resolveOtherSpecial callback (its own dash/fanout), as its own step.
///   After each step: CalculateCascades (board.Tiles updated synchronously) runs the
///   fall as a background job; we wait catchOverlap × fallDuration before the next
///   step, so the next clear destroys the falling tiles mid-air (MoveToGridCell
///   null-checks per frame, so this is safe).
///
/// A special found by a step is queued (not cleared) so it activates on its own
/// turn, and its cell is anchored (pending-triggered → gravity-blocked) until then,
/// so it fires where the player saw it instead of falling with the cascade.
/// </summary>
public sealed class PulseChainSequenceAction : BoardAction
{
    private readonly BoardController board;
    private readonly List<TileView> initialSpecials;
    private readonly int areaHalf;
    private readonly float catchOverlap;
    private readonly Func<TileView, List<BoardAction>> resolveOtherSpecial;

    // Queued specials are anchored via pending-triggered cells (gravity-blocked in
    // CalculateCascades) so they activate where the player saw them instead of
    // falling between steps. Released when the special's own turn comes. The registry
    // lives on the ROOT chain: arrival sub-chains share it, and only the root runs
    // the final wait/release (a sub-chain runs as a background job itself, so waiting
    // on ActiveBackgroundJobs from inside one would deadlock until the safety cap).
    private readonly PulseChainSequenceAction root;
    private readonly Dictionary<TileView, Vector2Int> anchoredCells = new();
    private readonly Vector2Int[] anchorCellBuffer = new Vector2Int[1];

    public override bool Blocking => true;

    public PulseChainSequenceAction(
        BoardController board,
        List<TileView> initialSpecials,
        int areaHalf,
        float catchOverlapFraction,
        Func<TileView, List<BoardAction>> resolveOtherSpecial = null,
        PulseChainSequenceAction root = null)
    {
        this.board = board;
        this.initialSpecials = initialSpecials ?? new List<TileView>();
        this.areaHalf = Mathf.Max(1, areaHalf);
        this.catchOverlap = Mathf.Clamp01(catchOverlapFraction);
        this.resolveOtherSpecial = resolveOtherSpecial;
        this.root = root ?? this;
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
            ReleaseAnchor(t);
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
            else if (special == TileSpecial.LineV || special == TileSpecial.LineH)
            {
                yield return SweepLine(sequencer, t, cx, cy, special == TileSpecial.LineH, queue, processed);
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

        // Only the root chain runs the final settle: a sub-chain IS a background job,
        // so it must not wait on ActiveBackgroundJobs (it would see itself).
        if (root == this)
        {
            // Wait for background falls AND arrival sub-chains to finish. Anchors must
            // outlive the sub-chains: a released cell could let a not-yet-blasted
            // special fall away from its sub-chain mid-VFX.
            float safety = 0f;
            while (board.ActiveBackgroundJobs > 0 && safety < 5f)
            {
                safety += Time.deltaTime;
                yield return null;
            }

            // No anchor may outlive the chain (a leftover pending cell would block
            // gravity in that column forever). Released cells may leave holes → settle.
            if (ReleaseAllAnchors())
                yield return RunGravityWithOverlap(hasNext: false);
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

                // Movable obstacle (Plastic vb.) tile-clear yoluna GİRMEZ: view'ın yaşam
                // döngüsü obstacle hasarınındır (affectedCells hasarı vurur; yıkılırsa
                // HandleObstacleDestroyed view'ı kaldırır). Tile-clear + hücre-bazlı geç
                // hasar, eşzamanlı gravity'de taş kayınca state'i ıskalayıp görünmez
                // (orphan) obstacle bırakabiliyordu.
                if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    continue;

                var sp = tile.GetSpecial();

                // A special already claimed by another step/sub-chain but not yet
                // consumed → leave it alone (it must blast itself, not clear here).
                if (tile != pulse && sp != TileSpecial.None && processed.Contains(tile))
                    continue;

                // Another PulseCore → chains (explodes on its turn).
                if (tile != pulse && sp == TileSpecial.PulseCore && !processed.Contains(tile))
                {
                    if (!queue.Contains(tile)) queue.Enqueue(tile);
                    AnchorQueued(tile, x, y);
                    continue;
                }

                // A non-pulse special (lines run native, the rest inline) → chains too.
                if (tile != pulse && sp != TileSpecial.None && sp != TileSpecial.PulseCore
                    && (sp == TileSpecial.LineV || sp == TileSpecial.LineH || resolveOtherSpecial != null)
                    && !processed.Contains(tile))
                {
                    if (!queue.Contains(tile)) queue.Enqueue(tile);
                    AnchorQueued(tile, x, y);
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

    private IEnumerator SweepLine(
        ActionSequencer sequencer, TileView line, int cx, int cy, bool isHorizontal,
        Queue<TileView> queue, HashSet<TileView> processed)
    {
        // 1) Collect the row/column: normal tiles clear; a special on the path fires
        //    THE MOMENT the beam reaches its cell (arrival trigger → sub-chain), not
        //    on a later queue turn — the sweep must never pass over something without
        //    breaking/triggering it. Mirrors LineV/HSpecial.CollectColumn/Row: cells
        //    failing CanAffectCell are skipped entirely (no blocker damage on path).
        var clearTiles = new HashSet<TileView>();
        var visualTargets = new List<TileView>();
        var affectedCells = new HashSet<Vector2Int>();
        var arrivalSpecials = new List<TileView>();

        int len = isHorizontal ? board.Width : board.Height;
        for (int i = 0; i < len; i++)
        {
            int x = isHorizontal ? i : cx;
            int y = isHorizontal ? cy : i;

            if (!SpecialUtils.CanAffectCell(board, x, y))
                continue;

            affectedCells.Add(new Vector2Int(x, y));

            var tile = board.Tiles[x, y];
            if (tile == null) continue;

            // Movable obstacle (Plastic vb.) tile-clear yoluna girmez — beam'in
            // obstacle hasarı (TryHit) vurur, yıkılırsa view'ı handler kaldırır.
            // (ExplodePulse'taki orphan-obstacle açıklamasının aynısı.)
            if (board.ObstacleStateService != null && board.ObstacleStateService.IsMovableObstacleAt(x, y))
                continue;

            if (tile != line && tile.GetSpecial() != TileSpecial.None)
            {
                // Already claimed by another step/sub-chain → leave it alone.
                if (processed.Contains(tile)) continue;

                processed.Add(tile);
                AnchorQueued(tile, x, y); // stays put until its own blast resolves
                arrivalSpecials.Add(tile);
                continue;
            }

            clearTiles.Add(tile);
            visualTargets.Add(tile);
        }

        clearTiles.Add(line);

        // 2) Immediate clear with the line's lightning-strike presentation
        //    (same visuals the inline/combined line path uses).
        var strikes = new List<LightningLineStrike>
        {
            new LightningLineStrike(new Vector2Int(cx, cy), isHorizontal)
        };

        var stepClear = new MatchClearAction(
            clearTiles,
            doShake: true,
            animationMode: ClearAnimationMode.LightningStrike,
            affectedCells: affectedCells,
            includeAdjacentOverTileBlockerDamage: false,
            lightningVisualTargets: visualTargets,
            lightningLineStrikes: strikes,
            isSpecialPhase: true);

        // Beam arrival → launch the special's own chain right then (concurrent with
        // the rest of the sweep), same pattern as DrainDeferredLineOverrides.
        var firedArrivals = new HashSet<TileView>();
        foreach (var spTile in arrivalSpecials)
        {
            var captured = spTile;
            stepClear.AddArrivalTrigger(new Vector2Int(captured.X, captured.Y), () =>
            {
                if (firedArrivals.Add(captured))
                    LaunchArrivalSubChain(captured);
            });
        }

        yield return stepClear.ExecuteVisuals(sequencer);

        // Fallback: if the strike VFX couldn't play, arrival callbacks never fire —
        // the special must still trigger rather than silently survive.
        foreach (var spTile in arrivalSpecials)
            if (firedArrivals.Add(spTile))
                LaunchArrivalSubChain(spTile);

        // 3) Gravity + overlap.
        yield return RunGravityWithOverlap(queue.Count > 0);
    }

    // Runs a chained special as its own PulseChainSequenceAction in the background
    // (ActiveBackgroundJobs guarded; the root's final wait covers it). It shares the
    // root's anchor registry, released once the whole chain settles.
    private void LaunchArrivalSubChain(TileView tile)
    {
        if (tile == null || !tile) return;
        board.StartImmediateActionSequence(new List<BoardAction>
        {
            new PulseChainSequenceAction(board, new List<TileView> { tile }, areaHalf, catchOverlap, resolveOtherSpecial, root)
        });
    }

    private void AnchorQueued(TileView tile, int x, int y)
    {
        if (tile is null || root.anchoredCells.ContainsKey(tile)) return;
        var cell = new Vector2Int(x, y);
        root.anchoredCells[tile] = cell;
        root.anchorCellBuffer[0] = cell;
        board.SetPendingTriggeredSpecialCells(root.anchorCellBuffer);
    }

    // "is null" on purpose: a destroyed-but-referenced TileView must still release
    // its cell, and Unity's overloaded == would report it as null.
    private void ReleaseAnchor(TileView tile)
    {
        if (tile is null || !root.anchoredCells.TryGetValue(tile, out var cell)) return;
        root.anchoredCells.Remove(tile);
        root.anchorCellBuffer[0] = cell;
        board.ClearPendingTriggeredSpecialCells(root.anchorCellBuffer);
    }

    private bool ReleaseAllAnchors()
    {
        if (root.anchoredCells.Count == 0) return false;
        board.ClearPendingTriggeredSpecialCells(root.anchoredCells.Values);
        root.anchoredCells.Clear();
        return true;
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
