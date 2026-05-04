using System.Collections.Generic;
using UnityEngine;

public readonly struct OilSpreadPair
{
    public readonly Vector2Int Source;
    public readonly Vector2Int Target;
    public OilSpreadPair(Vector2Int source, Vector2Int target) { Source = source; Target = target; }
}

/// <summary>
/// Rule: if ANY Oil was hit during this player move → no spread.
/// Otherwise one Oil cell spreads to one valid neighbour after board stabilises.
/// </summary>
public sealed class OilSpreadService
{
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0,  1),
        new Vector2Int(0, -1),
        new Vector2Int(1,  0),
        new Vector2Int(-1, 0),
    };

    private readonly BoardController board;
    private readonly ObstacleStateService obstacles;

    public OilSpreadService(BoardController board, ObstacleStateService obstacles)
    {
        this.board = board;
        this.obstacles = obstacles;
    }

    public List<OilSpreadPair> CalculateSpread(IReadOnlyCollection<Vector2Int> oilHitCellsThisMove)
    {
        var result = new List<OilSpreadPair>();

        if (board == null || obstacles == null)
            return result;

        if (oilHitCellsThisMove != null && oilHitCellsThisMove.Count > 0)
        {
            Debug.Log($"[Oil] Spread skipped. Oil hit this move: {oilHitCellsThisMove.Count}");
            return result;
        }

        var oilCells = obstacles.GetAllOilCells();
        if (oilCells == null || oilCells.Count == 0)
            return result;

        var reserved = new HashSet<Vector2Int>();

        foreach (var oil in oilCells)
        {
            if (TryPickTarget(oil, reserved, out var target))
            {
                reserved.Add(target);
                result.Add(new OilSpreadPair(oil, target));
                Debug.Log($"[Oil] Spread source=({oil.x},{oil.y}) target=({target.x},{target.y})");
                break;
            }
        }

        return result;
    }

    private bool TryPickTarget(Vector2Int source, HashSet<Vector2Int> reserved, out Vector2Int target)
    {
        foreach (var dir in Dirs)
        {
            var candidate = source + dir;
            if (reserved.Contains(candidate)) continue;
            if (!CanSpreadTo(candidate)) continue;
            target = candidate;
            return true;
        }
        target = default;
        return false;
    }

    private bool CanSpreadTo(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) return false;
        if (board.Holes[cell.x, cell.y]) return false;
        if (board.GetTileViewAt(cell.x, cell.y) == null) return false;
        return obstacles.CanOilSpreadTo(cell.x, cell.y);
    }
}
