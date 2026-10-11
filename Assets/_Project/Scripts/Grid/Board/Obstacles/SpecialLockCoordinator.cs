using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic "cage" lock over special tiles. NOT magnet-specific — any obstacle can lock a
/// special and plug in its own timeout/release behavior.
///
/// Central rules enforced here (identical for every owner):
///  • A locked special is UNBREAKABLE: SpecialUtils.CanTargetTileContent returns false for it,
///    so every clear / match / special-targeting path skips it. (Enforced via TileView.IsSpecialLocked.)
///  • A locked special STILL FALLS: gravity moves the TileView object and the lock flag rides along,
///    so nothing extra is needed for falling.
///  • IMMUNE TO AoE: a special effect covering a locked special neither fires it nor clears it
///    (SpecialChainRunner skips caged tiles; dispatcher paths skip via IsInteractionLocked).
///    ReleaseCoveredBy exists for an "AoE frees the cage" rule but is NOT wired (user rule 2026-09-25:
///    locked → no explosion, no break).
///  • OPT-IN RELEASE: a lock created with releaseWhenCovered (Laser Cannon) is freed IMMEDIATELY by NoteCovered
///    (adjacent match / special area). The freed tile gets TileView.CageGrace — AoE still skips it until the
///    player's next move or the move-end pass — so a later wave of the same chain cannot explode it.
///  • TIMEOUT: each lock carries an unlock window in MOVES. If the window elapses without release,
///    the owner onTimeout fires (e.g. magnet pulls it through the tube). The owner is responsible for
///    disposing the tile/lock inside that callback.
///
/// Ownership model: the flag lives on TileView (so it follows the tile through gravity); the metadata
/// (window + callbacks + owner tag) lives here keyed by TileView.
/// </summary>
public class SpecialLockCoordinator
{
    private sealed class LockEntry
    {
        public TileView Tile;
        public int MovesUntilTimeout;   // decremented at each move-end; 0 → timeout fires
        public Action<TileView> OnTimeout;
        public Action<TileView> OnReleased;
        public string Owner;
        public bool ReleaseWhenCovered;
    }

    private readonly Dictionary<TileView, LockEntry> locks = new();
    // Reused scratch buffers so move-end / release passes don't allocate.
    private readonly List<LockEntry> timedOut = new();
    private readonly List<TileView> stale = new();
    // Etki anında serbest kalan (CageGrace) taşlar; yeni hamlede / hamle sonunda grace kalkar.
    private readonly List<TileView> graceTiles = new();

    public bool HasAnyLock => locks.Count > 0;

    // The TileView flag is the single source of truth. A caged special can still leave the board
    // OUTSIDE the coordinator (e.g. level teardown, shuffle repair). When that happens the tile is
    // pooled and its flag reset in PrepareForRelease, but our dict entry lingers. So every read/pass
    // treats "flag is false" as authoritative and prunes the stale entry — this prevents a reused pool
    // tile from inheriting a bogus lock or firing a phantom timeout.
    public bool IsLocked(TileView tile) =>
        tile != null && tile && tile.IsSpecialLocked && locks.ContainsKey(tile);

    /// <summary>
    /// Lock a special. <paramref name="unlockWindowMoves"/> is how many upcoming moves the player has
    /// to release it (via a special AoE) before <paramref name="onTimeout"/> fires. Magnet uses 1.
    /// Re-locking the same tile refreshes its window/callbacks rather than stacking.
    /// </summary>
    public void LockSpecial(
        TileView tile,
        int unlockWindowMoves,
        Action<TileView> onTimeout,
        Action<TileView> onReleased = null,
        string owner = null,
        bool releaseWhenCovered = false)
    {
        if (tile == null || !tile)
            return;

        if (!locks.TryGetValue(tile, out var entry))
        {
            entry = new LockEntry();
            locks[tile] = entry;
        }

        entry.Tile = tile;
        entry.MovesUntilTimeout = Mathf.Max(1, unlockWindowMoves);
        entry.OnTimeout = onTimeout;
        entry.OnReleased = onReleased;
        entry.Owner = owner;
        entry.ReleaseWhenCovered = releaseWhenCovered;
        tile.CageGrace = false;

        tile.SetSpecialLocked(true);
    }

    /// <summary>
    /// Release a specific locked tile. Fires onReleased unless suppressed (e.g. the owner is
    /// consuming the tile itself in a timeout and doesn't want the "freed" visual).
    /// </summary>
    public void ReleaseSpecial(TileView tile, bool invokeReleasedCallback = true)
    {
        if (tile == null)
            return;
        if (!locks.TryGetValue(tile, out var entry))
            return;

        locks.Remove(tile);
        if (tile)
            tile.SetSpecialLocked(false);

        if (invokeReleasedCallback)
            entry.OnReleased?.Invoke(tile);
    }

    /// <summary>
    /// Called from the special-activation path with the cells a special just affected. Any locked
    /// special sitting on one of those cells is freed. Cheap early-out when nothing is locked.
    /// </summary>
    public void ReleaseCoveredBy(ICollection<Vector2Int> aoeCells)
    {
        if (locks.Count == 0 || aoeCells == null || aoeCells.Count == 0)
            return;

        stale.Clear();
        foreach (var kv in locks)
        {
            var tile = kv.Key;
            // Consumed elsewhere (exploded / pooled) → flag reset → prune without firing onReleased.
            if (tile == null || !tile || !tile.IsSpecialLocked)
            {
                stale.Add(tile);
                continue;
            }
            if (aoeCells.Contains(new Vector2Int(tile.X, tile.Y)))
                stale.Add(tile);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            var tile = stale[i];
            bool consumed = tile == null || !tile || !tile.IsSpecialLocked;
            ReleaseSpecial(tile, invokeReleasedCallback: !consumed);
        }
    }

    /// <summary>
    /// Adjacent match / special area reached this locked tile (BoardController.NoteCageImpact, SpecialChainRunner).
    /// Only opt-in locks react: freed right now (cage visual breaks in sync), protected by CageGrace until
    /// <see cref="ClearGrace"/>.
    /// </summary>
    public void NoteCovered(TileView tile)
    {
        if (tile == null || !tile || !locks.TryGetValue(tile, out var entry) || !entry.ReleaseWhenCovered)
            return;
        tile.CageGrace = true;
        graceTiles.Add(tile);
        ReleaseSpecial(tile);
    }

    /// Yeni oyuncu hamlesi başladı ya da tahta duruldu: serbest kalanlar artık normal special.
    public void ClearGrace()
    {
        for (int i = 0; i < graceTiles.Count; i++)
            if (graceTiles[i]) graceTiles[i].CageGrace = false;
        graceTiles.Clear();
    }

    /// <summary>Release every lock owned by <paramref name="owner"/> (e.g. the owning obstacle was destroyed).</summary>
    public void ReleaseOwnedBy(string owner)
    {
        if (locks.Count == 0 || owner == null)
            return;
        stale.Clear();
        foreach (var kv in locks)
            if (kv.Value.Owner == owner)
                stale.Add(kv.Key);
        for (int i = 0; i < stale.Count; i++)
        {
            var tile = stale[i];
            ReleaseSpecial(tile, invokeReleasedCallback: tile && tile.IsSpecialLocked);
        }
        stale.Clear();
    }

    /// <summary>
    /// Call once per completed move (after that move's special AoE releases have been processed).
    /// Decrements every surviving lock's window; those that reach zero fire their owner onTimeout.
    /// IMPORTANT: invoke this BEFORE new locks are created for this same move-end, so a freshly
    /// created lock isn't decremented in the move it was born.
    /// </summary>
    public void OnMoveResolved()
    {
        ClearGrace();
        if (locks.Count == 0)
            return;

        timedOut.Clear();
        stale.Clear();

        foreach (var kv in locks)
        {
            var entry = kv.Value;
            // Consumed elsewhere (exploded in a special's AoE / pooled) → flag reset → prune, no timeout.
            if (entry.Tile == null || !entry.Tile || !entry.Tile.IsSpecialLocked)
            {
                stale.Add(kv.Key);
                continue;
            }

            entry.MovesUntilTimeout--;
            if (entry.MovesUntilTimeout <= 0)
                timedOut.Add(entry);
        }

        for (int i = 0; i < stale.Count; i++)
            locks.Remove(stale[i]);

        // Fire timeouts. The owner callback is responsible for disposing the tile/lock; we remove
        // the bookkeeping entry first so the callback sees a consistent (unlocked) coordinator and
        // can, e.g., re-drop the same special elsewhere without tripping IsLocked.
        for (int i = 0; i < timedOut.Count; i++)
        {
            var entry = timedOut[i];
            var tile = entry.Tile;
            locks.Remove(tile);
            if (tile)
                tile.SetSpecialLocked(false);
            entry.OnTimeout?.Invoke(tile);
        }
    }

    /// <summary>Drop all locks (level teardown / reload). Does not fire callbacks.</summary>
    public void Clear()
    {
        foreach (var kv in locks)
        {
            if (kv.Key)
                kv.Key.SetSpecialLocked(false);
        }
        locks.Clear();
        ClearGrace();
    }
}
