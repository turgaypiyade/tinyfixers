using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One bounded board snapshot per target choice; valid airborne intents keep their cached target. Scores direct
/// payload work only: no speculative cascades, matches or chain-reaction simulation.
/// Eligibility/reservations remain owned by BoardTargetPool. Buffers are reused.
/// </summary>
internal sealed class PatchBotImpactScorer
{
    private struct Cell
    {
        public ObstacleId id;
        public int origin, hits, value, cargo;
        public bool hittable, candidate, gel, perCell;
    }

    private struct Footprint
    {
        public int count, minX, minY, maxX, maxY;
    }

    private readonly BoardController board;
    private readonly PatchbotComboService service;
    private Cell[] cells = Array.Empty<Cell>();
    private Footprint[] footprints = Array.Empty<Footprint>();
    private int[] seenOrigins = Array.Empty<int>();
    private int stamp;
    private readonly List<int> ties = new();
    private readonly List<Vector2Int> reserved = new();

    public PatchBotImpactScorer(BoardController board, PatchbotComboService service)
    {
        this.board = board;
        this.service = service;
    }

    public (TileView tile, int x, int y, bool hasCell, bool gelSpread) Pick(
        TileSpecial payload, TileView source, TileView partner, HashSet<TileView> excluded,
        TileView[] additionalExcluded, HashSet<ObstacleId> obstacleGoals,
        List<TileType> tileGoals, bool preferNonGel)
    {
        bool area = payload == TileSpecial.PulseCore || payload == TileSpecial.LineH || payload == TileSpecial.LineV;
        var obs = board.ObstacleStateService;
        int width = board.Width, height = board.Height, size = width * height;
        if (cells.Length != size)
        {
            cells = new Cell[size];
            footprints = new Footprint[size];
            seenOrigins = new int[size];
            stamp = 0;
        }
        Array.Clear(footprints, 0, size);
        int usefulWaveWork = 0, bestSingleGoal = 0;
        bool nearReadyHelper = false;
        NextStamp();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                var tile = board.Tiles[x, y];
                var c = new Cell { origin = -1 };
                c.id = obs != null ? obs.GetObstacleIdAt(x, y) : ObstacleId.None;
                c.origin = obs != null ? obs.GetObstacleOriginAt(x, y) : -1;
                if (c.origin >= 0 && c.origin < size)
                {
                    ref var f = ref footprints[c.origin];
                    if (f.count++ == 0) { f.minX = f.maxX = x; f.minY = f.maxY = y; }
                    else { f.minX = Mathf.Min(f.minX, x); f.maxX = Mathf.Max(f.maxX, x); f.minY = Mathf.Min(f.minY, y); f.maxY = Mathf.Max(f.maxY, y); }
                }
                c.hittable = board.TargetPool.IsHittableObstacleCell(x, y);
                c.perCell = c.hittable && obs.TakesPerCellHits(x, y);
                bool tileHit = board.TargetPool.IsTargetableTileCell(x, y);
                bool goal = obstacleGoals.Contains(c.id)
                    || (c.id == ObstacleId.Grass && obstacleGoals.Contains(ObstacleId.GrassFlower)
                        && obs.HasGrassFlowerAt(c.origin));
                if (c.hittable)
                {
                    c.hits = obs.GetActiveMeaningfulHitsAt(x, y);
                    c.value = goal ? 12 : 5;
                    c.candidate = board.TargetPool.ObstacleCapacityAt(x, y) > 0;
                    if (!c.candidate) c.value = 0; // already covered by other airborne hits
                    if (c.candidate && c.id == ObstacleId.OverrideBatteryBox && c.hits <= 2)
                        nearReadyHelper = true;
                }
                else if (tileHit && (c.id == ObstacleId.None || c.id == ObstacleId.SpreadingGel))
                {
                    bool tileGoal = tileGoals.Contains(tile.GetTileType());
                    c.value = tileGoal ? 6 : 1;
                    if (tileGoal) usefulWaveWork += c.value;
                    c.candidate = tile.GetSpecial() == TileSpecial.None && tile != source && tile != partner
                        && (excluded == null || !excluded.Contains(tile))
                        && !board.TargetPool.IsTileReserved(tile)
                        && !board.TargetPool.IsCellReserved(new Vector2Int(x, y));
                }
                if (tile != null && additionalExcluded != null)
                    for (int n = 0; n < additionalExcluded.Length; n++)
                        if (tile == additionalExcluded[n]) { c.candidate = false; break; }
                if (tile != null && (tile == source || tile == partner || (excluded != null && excluded.Contains(tile))))
                    c.candidate = false;
                if (preferNonGel && service != null && service.IsGelSpreadTarget(x, y, tile))
                {
                    c.gel = true;
                    c.value += 3;
                }
                // A large box contributes one wave hit, never its footprint's cell count.
                if (c.hittable && (c.origin < 0 || c.origin >= size || seenOrigins[c.origin] != stamp
                    || c.perCell))
                {
                    usefulWaveWork += c.value;
                    if (goal) bestSingleGoal = Mathf.Max(bestSingleGoal, c.value);
                    if (c.origin >= 0 && c.origin < size) seenOrigins[c.origin] = stamp;
                }
                cells[i] = c;
            }

        // Single bots retain their normal policy unless a nearly charged box offers
        // substantially more useful board work than another direct goal hit.
        if (!area && (!nearReadyHelper || usefulWaveWork < Mathf.Max(20, bestSingleGoal * 2)))
            return (null, -1, -1, false, false);

        // Cargo lanes include breakable obstacles as well as ordinary tiles. A
        // protected/unbreakable barrier stops the route; damaging it cannot help cargo.
        for (int x = 0; x < width; x++)
        {
            bool belowCargo = false;
            for (int y = 0; y < height; y++)
            {
                int i = y * width + x;
                if (obs != null && obs.IsUnbreakableMovableAt(x, y)) { belowCargo = true; continue; }
                if (!belowCargo) continue;
                if (board.Holes[x, y] || (cells[i].id != ObstacleId.None
                    && cells[i].id != ObstacleId.SpreadingGel && !cells[i].hittable))
                { belowCargo = false; continue; }
                if (cells[i].value > 0) cells[i].cargo = cells[i].hits <= 1 ? 16 : 8;
            }
        }

        // The helper's future full-board hit is amortized by remaining charge.
        // It is a useful option, not a permanently promoted fake level goal.
        for (int i = 0; i < size; i++)
            if (cells[i].id == ObstacleId.OverrideBatteryBox && cells[i].hittable && cells[i].value > 0)
                cells[i].value += usefulWaveWork / (2 * Mathf.Max(1, cells[i].hits));

        int boardWeight = 0, boardSumX = 0, boardSumY = 0;
        for (int i = 0; i < size; i++)
        {
            int value = cells[i].value + cells[i].cargo;
            boardWeight += value; boardSumX += value * (i % width); boardSumY += value * (i / width);
        }

        board.TargetPool.CopyReservedCells(reserved);
        ties.Clear();
        int bestScore = int.MinValue;
        float bestCenterDistance = float.MaxValue;
        for (int i = 0; i < size; i++)
        {
            var c = cells[i];
            if (!c.candidate) continue;
            if (!area && (c.id != ObstacleId.OverrideBatteryBox || c.hits > 2)) continue;
            int cx = i % width, cy = i / width;
            if (payload == TileSpecial.PulseCore && c.origin >= 0 && c.origin < size
                && c.id != ObstacleId.Magnet && c.id != ObstacleId.Tube)
            {
                var f = footprints[c.origin];
                if (f.count > 1 && f.count == (f.maxX - f.minX + 1) * (f.maxY - f.minY + 1))
                { cx = (f.minX + f.maxX + 1) / 2; cy = (f.minY + f.maxY + 1) / 2; }
            }
            int x0 = payload == TileSpecial.LineH ? 0 : Mathf.Max(0, cx - (payload == TileSpecial.PulseCore ? 2 : 0));
            int x1 = payload == TileSpecial.LineH ? width - 1 : Mathf.Min(width - 1, cx + (payload == TileSpecial.PulseCore ? 2 : 0));
            int y0 = payload == TileSpecial.LineV ? 0 : Mathf.Max(0, cy - (payload == TileSpecial.PulseCore ? 2 : 0));
            int y1 = payload == TileSpecial.LineV ? height - 1 : Mathf.Min(height - 1, cy + (payload == TileSpecial.PulseCore ? 2 : 0));
            int score = 0, weight = 0, sumX = 0, sumY = 0;
            NextStamp();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var hit = cells[y * width + x];
                    int value = hit.value + hit.cargo;
                    if (value <= 0) continue;
                    if (hit.hittable && hit.origin >= 0 && hit.origin < size && !hit.perCell)
                    {
                        if (seenOrigins[hit.origin] == stamp) continue;
                        seenOrigins[hit.origin] = stamp;
                    }
                    score += value;
                    weight += value; sumX += value * x; sumY += value * y;
                }
            for (int r = 0; r < reserved.Count; r++)
                if (reserved[r].x >= x0 && reserved[r].x <= x1 && reserved[r].y >= y0 && reserved[r].y <= y1)
                    score -= 15;
            // Equal-effect line targets aim near the work's center, not its first row.
            float dx = weight > 0 ? cx - (float)sumX / weight : 0f;
            float dy = weight > 0 ? cy - (float)sumY / weight : 0f;
            float distance = dx * dx + dy * dy;
            if (boardWeight > 0)
            {
                float gx = cx - (float)boardSumX / boardWeight, gy = cy - (float)boardSumY / boardWeight;
                distance += 0.01f * (gx * gx + gy * gy);
            }
            if (score > bestScore || (score == bestScore && distance < bestCenterDistance - 0.001f))
            {
                bestScore = score; bestCenterDistance = distance; ties.Clear(); ties.Add(i);
            }
            else if (score == bestScore && Mathf.Abs(distance - bestCenterDistance) <= 0.001f) ties.Add(i);
        }
        if (ties.Count == 0) return (null, -1, -1, false, false);
        int chosen = ties[UnityEngine.Random.Range(0, ties.Count)];
        var selected = cells[chosen];
        bool gelTarget = selected.gel && !selected.hittable;
        return (board.Tiles[chosen % width, chosen / width], chosen % width, chosen / width, true, gelTarget);
    }

    private void NextStamp()
    {
        if (stamp == int.MaxValue) { Array.Clear(seenOrigins, 0, seenOrigins.Length); stamp = 0; }
        stamp++;
    }
}
