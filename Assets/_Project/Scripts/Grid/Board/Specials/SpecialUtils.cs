using UnityEngine;

/// <summary>
/// Shared utility methods used by all ISpecialBehavior implementations.
/// </summary>
public static class SpecialUtils
{
    /// <summary>
    /// Returns true if a special's effect can reach cell (x, y).
    /// A cell is reachable if it's within bounds and either not a hole,
    /// or a hole that has an obstacle on it.
    /// </summary>
    public static bool CanAffectCell(BoardController board, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (!board.Holes[x, y])
        {
            // Yalnızca sabit OverTileBlocker (Stone vb.) altındaki tile korunur.
            // MovableObstacle (Plastic vb.) kendisi hedef olduğundan engellenmez.
            if (board.ObstacleStateService != null
                && board.ObstacleStateService.IsOverTileBlockerAt(x, y)
                && !board.ObstacleStateService.IsMovableObstacleAt(x, y))
                return false;
            return true;
        }

        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }
}
