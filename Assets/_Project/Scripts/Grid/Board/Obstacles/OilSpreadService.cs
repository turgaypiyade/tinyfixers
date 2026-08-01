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

        // Hangi oil hücresinin yayılacağı da rastgele seçilsin
        int startCell = Random.Range(0, oilCells.Count);
        for (int i = 0; i < oilCells.Count; i++)
        {
            var oil = oilCells[(startCell + i) % oilCells.Count];
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
        // Rastgele bir yönden baslayarak döngüsel tara
        int start = Random.Range(0, Dirs.Length);
        for (int i = 0; i < Dirs.Length; i++)
        {
            var candidate = source + Dirs[(start + i) % Dirs.Length];
            if (reserved.Contains(candidate)) continue;
            if (!CanSpreadTo(candidate)) continue;
            if (!WouldKeepPlayableMove(candidate, reserved)) continue;
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

    private bool WouldKeepPlayableMove(Vector2Int candidate, HashSet<Vector2Int> reserved)
    {
        if (board == null)
            return false;

        reserved.Add(candidate);
        bool hasPlayableMove = board.HasAnyPlayableSwapWithAdditionalLockedCells(reserved);
        reserved.Remove(candidate);

        if (!hasPlayableMove)
            Debug.Log($"[Oil] Spread target ({candidate.x},{candidate.y}) skipped; it would leave no playable move.");

        return hasPlayableMove;
    }
}
