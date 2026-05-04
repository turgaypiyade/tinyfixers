using System.Collections.Generic;
using UnityEngine;

public sealed class OilSpreadService
{
    private static readonly Vector2Int[] _fourDirs =
    {
        new Vector2Int(0,  1),
        new Vector2Int(0, -1),
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
    };

    private readonly BoardController _board;
    private readonly ObstacleStateService _obstacles;

    public OilSpreadService(BoardController board, ObstacleStateService obstacles)
    {
        _board = board;
        _obstacles = obstacles;
    }

    // Returns cells Oil should spread to this pass.
    // Spread rule: each Oil cell checks 4-adjacent neighbours.
    //   If neighbour was cleared this turn (in clearedCells) AND CanOilSpreadTo → add to result.
    public List<Vector2Int> CalculateSpread(IReadOnlyCollection<Vector2Int> clearedCells)
    {
        if (clearedCells == null || clearedCells.Count == 0)
            return new List<Vector2Int>();

        var clearedSet = new HashSet<Vector2Int>(clearedCells);
        var oilCells = _obstacles.GetAllOilCells();
        var result = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();

        foreach (var oil in oilCells)
        {
            foreach (var dir in _fourDirs)
            {
                var neighbour = oil + dir;
                if (!clearedSet.Contains(neighbour)) continue;
                if (!IsValidSpreadTarget(neighbour.x, neighbour.y)) continue;
                if (seen.Add(neighbour))
                    result.Add(neighbour);
            }
        }

        return result;
    }

    public bool HasPendingSpread(IReadOnlyCollection<Vector2Int> clearedCells)
    {
        if (clearedCells == null || clearedCells.Count == 0) return false;
        var clearedSet = new HashSet<Vector2Int>(clearedCells);
        foreach (var oil in _obstacles.GetAllOilCells())
            foreach (var dir in _fourDirs)
            {
                var nb = oil + dir;
                if (clearedSet.Contains(nb) && IsValidSpreadTarget(nb.x, nb.y))
                    return true;
            }
        return false;
    }

    // Spread hedefi: bounds içinde, hole değil, şu an başka obstacle yok.
    // Tile varlığı kontrol EDİLMEZ — cleared hücre boş olur, cascade sonra tile koyar.
    private bool IsValidSpreadTarget(int x, int y)
    {
        if (x < 0 || x >= _board.Width || y < 0 || y >= _board.Height) return false;
        if (_board.Holes[x, y]) return false;
        return _obstacles.CanOilSpreadTo(x, y);
    }
}
