using System.Collections.Generic;
using UnityEngine;

// Pure C# obstacle state for headless simulation.
// Clones LevelData arrays — never mutates the original asset.
// EnergyContainer / EnergyOrb goal is intentionally excluded (caller handles separately).
public sealed class SimObstacleLayer : ISimObstacleQuery
{
    private readonly int[] _obstacles;  // ObstacleId per cell index
    private readonly int[] _origins;    // origin cell index per cell (-1 = none)
    private readonly int[] _remaining;  // remaining hits indexed by origin index
    private readonly ObstacleLibrary _lib;
    private readonly int _width, _height;

    // Tracks how many full clears happened per ObstacleId (for goal tracking)
    private readonly Dictionary<int, int> _cleared = new();

    // Permanent holes from level.cells (CellType.Empty) — never changes.
    // Needed to restore Holes[] after a MovableObstacle moves away from a cell.
    private readonly bool[] _originalHoles;

    public SimObstacleLayer(LevelData level)
    {
        _width  = level.width;
        _height = level.height;
        _lib    = level.obstacleLibrary;

        int size = _width * _height;
        _obstacles = (int[])level.obstacles.Clone();
        _origins   = (int[])level.obstacleOrigins.Clone();
        _remaining = new int[size];

        for (int i = 0; i < size; i++) _remaining[i] = -1;

        _originalHoles = new bool[size];
        for (int idx = 0; idx < size; idx++)
        {
            if (idx < level.cells.Length)
                _originalHoles[idx] = (CellType)level.cells[idx] == CellType.Empty;
        }

        for (int idx = 0; idx < size; idx++)
        {
            var id = (ObstacleId)_obstacles[idx];
            if (id == ObstacleId.None) continue;

            int origin = _origins[idx];
            if (origin != idx) continue; // only origin cells initialise hits

            var def  = _lib != null ? _lib.Get(id) : null;
            int hits = Mathf.Max(1, def != null ? def.hits : 1);
            _remaining[origin] = hits;
        }

        // Çok-hücreli obstacle'lar asset'te obstacles[]'a değil ayrı dizilerde tutulur
        // (oyun runtime'da stamp'ler). Sim de aynısını yapmalı, yoksa Tube/Magnet/Safe
        // levellarında hedef asla dolmaz (yanlış %0).
        StampMultiCellObstacles(level);
    }

    private void StampMultiCellObstacles(LevelData level)
    {
        if (level.tubes != null)
            foreach (var t in level.tubes) StampTube(t);
        if (level.magnets != null)
            foreach (var m in level.magnets) StampMagnet(m);
        if (level.safes != null)
            foreach (var sf in level.safes) StampSafe(sf);
    }

    private void StampTube(TubeEntry t)
    {
        int ox = t.originCellIndex % _width, oy = t.originCellIndex / _width;
        int dx = t.direction == TubeDirection.Left ? -1 : t.direction == TubeDirection.Right ? 1 : 0;
        int dy = t.direction == TubeDirection.Up   ? -1 : t.direction == TubeDirection.Down  ? 1 : 0;
        for (int i = 0; i < Mathf.Max(2, t.length); i++)
        {
            int cx = ox + dx * i, cy = oy + dy * i;
            if (!InBounds(cx, cy)) break;
            Stamp(Idx(cx, cy), t.originCellIndex, ObstacleId.Tube);
        }
        SetRemaining(t.originCellIndex, DefHits(ObstacleId.Tube, 3));
    }

    private void StampMagnet(MagnetEntry m)
    {
        if (m.pathCellIndices == null || m.pathCellIndices.Length == 0) return;
        int origin = m.pathCellIndices[0];
        foreach (int cell in m.pathCellIndices) Stamp(cell, origin, ObstacleId.Magnet);
        SetRemaining(origin, DefHits(ObstacleId.Magnet, 1));
    }

    private void StampSafe(SafeEntry sf)
    {
        int ox = sf.originCellIndex % _width, oy = sf.originCellIndex / _width;
        for (int dy = 0; dy < Mathf.Max(1, sf.height); dy++)
            for (int dx = 0; dx < Mathf.Max(1, sf.width); dx++)
            {
                int cx = ox + dx, cy = oy + dy;
                if (InBounds(cx, cy)) Stamp(Idx(cx, cy), sf.originCellIndex, ObstacleId.Safe);
            }
        SetRemaining(sf.originCellIndex, Mathf.Max(1, sf.redHits + sf.yellowHits + sf.greenHits));
    }

    private void Stamp(int cell, int origin, ObstacleId id)
    {
        if (cell < 0 || cell >= _obstacles.Length) return;
        _obstacles[cell] = (int)id;
        _origins[cell]   = origin;
    }

    private void SetRemaining(int origin, int hits)
    {
        if (origin >= 0 && origin < _remaining.Length) _remaining[origin] = Mathf.Max(1, hits);
    }

    private int DefHits(ObstacleId id, int fallback)
    {
        var def = _lib != null ? _lib.Get(id) : null;
        return Mathf.Max(1, def != null ? def.hits : fallback);
    }

    // ── ISimObstacleQuery ────────────────────────────────────────────────────

    public bool HasObstacleAt(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return (ObstacleId)_obstacles[Idx(x, y)] != ObstacleId.None;
    }

    public ObstacleId ObstacleIdAt(int x, int y)
    {
        if (!InBounds(x, y)) return ObstacleId.None;
        return (ObstacleId)_obstacles[Idx(x, y)];
    }

    public bool IsMovableObstacleAt(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def = _lib != null ? _lib.Get(id) : null;
        if (def == null) return false; // unknown def → not a movable obstacle
        int rem = ResolveRemaining(idx, def);
        return def.IsMovableObstacleForRemainingHits(rem);
    }

    public bool IsInteractionLockedAt(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (IsOverTileBlockerAt(x, y) && !IsMovableObstacleAt(x, y))
            return true;

        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def   = _lib != null ? _lib.Get(id) : null;
        if (def == null) return false;
        int rem   = ResolveRemaining(idx, def);
        var stage = def.GetStageRuleForRemainingHits(rem);
        return stage != null && stage.locksInteraction;
    }

    // ── Cell state queries ───────────────────────────────────────────────────

    public bool IsCellBlocked(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def = _lib != null ? _lib.Get(id) : null;
        if (def == null) return false; // unknown def → assume it doesn't block the cell
        int rem = ResolveRemaining(idx, def);
        return def.GetBlocksCellsForRemainingHits(rem);
    }

    // ── Damage ───────────────────────────────────────────────────────────────

    // Call when the tile at (matchX, matchY) was cleared by a normal match.
    // Damages adjacent OverTileBlocker obstacles and any UnderTile obstacle at the same cell.
    public void ProcessMatchClear(int matchX, int matchY, TileType clearedTileType)
    {
        // Same cell: damage UnderTile obstacle (e.g. Stone layered under a tile)
        TryDamage(matchX, matchY, clearedTileType, underTileOnly: true);
        // Same cell: damage OverTile obstacle whose blocksCells=false (e.g. plastic_orange, chest1)
        TryDamage(matchX, matchY, clearedTileType, underTileOnly: false);

        // Adjacent OverTileBlocker obstacles
        TryDamage(matchX - 1, matchY, clearedTileType, underTileOnly: false);
        TryDamage(matchX + 1, matchY, clearedTileType, underTileOnly: false);
        TryDamage(matchX, matchY - 1, clearedTileType, underTileOnly: false);
        TryDamage(matchX, matchY + 1, clearedTileType, underTileOnly: false);
    }

    // Returns how many origins of the given ObstacleId were fully cleared this game.
    public int GetClearedCount(ObstacleId id)
    {
        _cleared.TryGetValue((int)id, out int c);
        return c;
    }

    // Total obstacles cleared across all IDs (for diagnostics).
    public int GetTotalClearedCount()
    {
        int total = 0;
        foreach (var kv in _cleared) total += kv.Value;
        return total;
    }

    // Count of obstacle origins still present (diagnostic — call before game ends for initial count).
    public int GetTotalObstacleCount()
    {
        int count = 0;
        for (int i = 0; i < _obstacles.Length; i++)
            if ((ObstacleId)_obstacles[i] != ObstacleId.None && _origins[i] == i)
                count++;
        return count;
    }

    // ── Hole sync ────────────────────────────────────────────────────────────

    // Rebuild state.Holes from current obstacle state.
    // Must be called after tile clears so that:
    //   - cleared obstacle cells stop being holes (tiles can flow in, gravity works through them)
    //   - living obstacle cells stay as holes
    public void SyncHoles(SimState state)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int idx = Idx(x, y);
                bool origHole    = idx < _originalHoles.Length && _originalHoles[idx];
                bool hasObstacle = (ObstacleId)_obstacles[idx] != ObstacleId.None;
                state.Holes[x, y] = origHole || hasObstacle;
            }
        }
    }

    // ── MovableObstacle gravity ───────────────────────────────────────────────

    // Drop all 1×1 MovableObstacles one step downward into empty cells.
    // Call this AFTER tile clearing and BEFORE SimCascade.ApplyGravityAndRefill,
    // so that tiles falling above can fill the vacated cells.
    // Processes bottom-to-top so a single call cascades multiple steps.
    public void ApplyGravity(SimState state)
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = _height - 2; y >= 0; y--)
            {
                int idx = Idx(x, y);
                var id = (ObstacleId)_obstacles[idx];
                if (id == ObstacleId.None) continue;
                if (_origins[idx] != idx) continue; // skip non-origin / multi-cell cells

                var def = _lib?.Get(id);
                if (def == null) continue; // unknown obstacle — don't move
                int rem = ResolveRemaining(idx, def);
                if (!def.IsMovableObstacleForRemainingHits(rem)) continue;

                int ny = y + 1;
                int nIdx = Idx(x, ny);

                // Cell below must be free of holes, tiles, and other obstacles
                if (state.Holes[x, ny]) continue;
                if (state.Grid[x, ny] != null) continue;
                if ((ObstacleId)_obstacles[nIdx] != ObstacleId.None) continue;

                // Move obstacle one row down
                _obstacles[nIdx] = _obstacles[idx];
                _origins[nIdx]   = nIdx;              // 1×1: new origin = new cell
                _remaining[nIdx] = _remaining[idx];

                _obstacles[idx] = (int)ObstacleId.None;
                _origins[idx]   = -1;
                _remaining[idx] = -1;

                // Update state.Holes so SimCascade can fill the vacated cell
                state.Holes[x, y]  = _originalHoles[idx];  // restore permanent-hole status
                state.Holes[x, ny] = true;                  // obstacle now here
            }
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void TryDamage(int x, int y, TileType sourceTile, bool underTileOnly)
    {
        if (!InBounds(x, y)) return;

        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return;
        if (id == ObstacleId.EnergyContainer) return; // excluded by design

        int origin = _origins[idx];
        if (origin < 0 || origin >= _remaining.Length) return;

        var def = _lib != null ? _lib.Get(id) : null;

        int rem = ResolveRemaining(idx, def);
        if (rem <= 0) return;

        if (def == null)
        {
            // No definition — assume 1-hit OverTileBlocker, Any damage source
            if (underTileOnly) return; // no def → treat as over-tile, skip under-tile pass
            _remaining[origin] = rem - 1;
            if (_remaining[origin] <= 0)
                ClearObstacle(origin, id);
            return;
        }

        // underTileOnly: only damage UnderTileLayered obstacles
        bool isUnder = IsUnderTileAt(x, y, def, rem);
        if (underTileOnly && !isUnder) return;
        if (!underTileOnly && isUnder) return;

        // Damage source rule check
        var rule = def.GetDamageRuleForRemainingHits(rem);
        if (rule == ObstacleDamageSourceRule.SpecialOnly ||
            rule == ObstacleDamageSourceRule.BoosterOnly ||
            rule == ObstacleDamageSourceRule.Disabled    ||
            rule == ObstacleDamageSourceRule.FullyDisabled)
            return;

        // Tile type restriction
        if (def.restrictNormalMatchTileType && def.requiredNormalMatchTileType != sourceTile)
            return;

        // Apply hit
        _remaining[origin] = rem - 1;

        if (_remaining[origin] <= 0)
            ClearObstacle(origin, id);
    }

    private void ClearObstacle(int origin, ObstacleId id)
    {
        for (int i = 0; i < _obstacles.Length; i++)
        {
            if ((ObstacleId)_obstacles[i] != id) continue;
            if (_origins[i] != origin) continue;
            _obstacles[i] = (int)ObstacleId.None;
            _origins[i]   = -1;
        }
        _remaining[origin] = -1;

        _cleared.TryGetValue((int)id, out int prev);
        _cleared[(int)id] = prev + 1;
    }

    private bool IsOverTileBlockerAt(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int idx = Idx(x, y);
        var id  = (ObstacleId)_obstacles[idx];
        if (id == ObstacleId.None) return false;
        var def = _lib != null ? _lib.Get(id) : null;
        if (def == null) return false;
        int rem = ResolveRemaining(idx, def);
        return def.IsOverTileDamageBehaviorForRemainingHits(rem);
    }

    private static bool IsUnderTileAt(int x, int y, ObstacleDef def, int rem)
    {
        var stage = def.GetStageRuleForRemainingHits(rem);
        return stage != null && stage.behavior == ObstacleBehaviorType.UnderTileLayered;
    }

    private int ResolveRemaining(int idx, ObstacleDef def)
    {
        int origin = _origins[idx];
        if (origin >= 0 && origin < _remaining.Length && _remaining[origin] >= 0)
            return _remaining[origin];
        return Mathf.Max(1, def != null ? def.hits : 1);
    }

    private bool InBounds(int x, int y) =>
        x >= 0 && y >= 0 && x < _width && y < _height;

    private int Idx(int x, int y) => y * _width + x;
}
