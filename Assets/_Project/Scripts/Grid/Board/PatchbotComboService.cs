using System.Collections.Generic;
using UnityEngine;

public class PatchbotComboService
{
    private readonly BoardController board;
    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();

    public PatchbotComboService(BoardController board)
    {
        this.board = board;
    }

    public bool HasObstacleAt(int x, int y)
    {
        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    public void EnqueueDash(TileView fromTile, int targetX, int targetY)
    {
        if (fromTile == null) return;
        board.EnqueuePatchbotDash(
            new Vector2Int(fromTile.X, fromTile.Y),
            new Vector2Int(targetX, targetY)
        );
    }

    public void ConsumeSwapSource(HashSet<TileView> matches, TileView patchBotTile, TileView partnerTile, System.Action<TileView> markAffectedCell)
    {
        if (patchBotTile == null || partnerTile == null) return;
        matches.Add(patchBotTile);
        matches.Add(partnerTile);
        markAffectedCell?.Invoke(patchBotTile);
        markAffectedCell?.Invoke(partnerTile);
    }

    public void ResolveTargetImpact(HashSet<TileView> matches, int targetX, int targetY, bool hasObstacleAtTarget, System.Action<int, int> markAffectedCell, System.Action<TileView> markAffectedTile)
    {
        if (hasObstacleAtTarget)
        {
            board.MarkPatchBotForcedObstacleHit(targetX, targetY);
            markAffectedCell?.Invoke(targetX, targetY);
            return;
        }

        HitCellOnce(matches, targetX, targetY, board.Tiles[targetX, targetY], markAffectedCell, markAffectedTile);
    }

    public void HitCellOnce(HashSet<TileView> matches, int x, int y, TileView tileAtCell, System.Action<int, int> markAffectedCell, System.Action<TileView> markAffectedTile)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (board.Holes[x, y] && !HasObstacleAt(x, y)) return;

        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null && obstacleService.GetObstacleIdAt(x, y) != ObstacleId.None)
        {
            markAffectedCell?.Invoke(x, y);
            return;
        }

        var tile = tileAtCell ?? board.Tiles[x, y];
        if (tile == null) return;

        matches.Add(tile);
        markAffectedTile?.Invoke(tile);
    }

    public (TileView tile, int x, int y, bool hasCell) FindTarget(TileView patchBotTile, TileView partnerTile, HashSet<TileView> excluded, params TileView[] additionalExcluded)
    {
        var obstacleGoalCells = new List<(int x, int y, TileView tile)>();
        var tileGoalCells = new List<(int x, int y, TileView tile)>();
        var otherObstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();

        var activeGoals = board.TopHud;
        activeGoalsBuffer.Clear();
        activeGoals?.GetActiveGoals(activeGoalsBuffer);

        var activeObstacleGoals = new HashSet<ObstacleId>();
        var activeTileGoals = new List<TileType>();
        for (int i = 0; i < activeGoalsBuffer.Count; i++)
        {
            var goal = activeGoalsBuffer[i];
            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId != ObstacleId.None)
                activeObstacleGoals.Add(goal.obstacleId);
            else if (goal.targetType == LevelGoalTargetType.Tile)
                activeTileGoals.Add(goal.tileType);
        }

        bool IsExcluded(TileView tile)
        {
            if (tile == null) return false;
            if (excluded != null && excluded.Contains(tile)) return true;
            if (tile == patchBotTile || tile == partnerTile) return true;
            if (additionalExcluded != null)
            {
                for (int i = 0; i < additionalExcluded.Length; i++)
                {
                    if (tile == additionalExcluded[i]) return true;
                }
            }

            return false;
        }

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] && !HasObstacleAt(x, y)) continue;

                var tile = board.Tiles[x, y];
                if (tile != null && IsExcluded(tile)) continue;

                bool isTileGoalCell = false;
                if (tile != null)
                {
                    var type = tile.GetTileType();
                    for (int i = 0; i < activeTileGoals.Count; i++)
                    {
                        if (activeTileGoals[i].Equals(type))
                        {
                            isTileGoalCell = true;
                            break;
                        }
                    }
                }

                var obstacleId = board.ObstacleStateService != null
                    ? board.ObstacleStateService.GetObstacleIdAt(x, y)
                    : ObstacleId.None;

                bool hasObstacle = obstacleId != ObstacleId.None;
                bool isObstacleGoalCell = hasObstacle && activeObstacleGoals.Contains(obstacleId);

                if (isObstacleGoalCell)
                    obstacleGoalCells.Add((x, y, tile));

                if (isTileGoalCell)
                    tileGoalCells.Add((x, y, tile));

                if (hasObstacle)
                {
                    if (!isObstacleGoalCell)
                        otherObstacleCells.Add((x, y, tile));
                }
                else if (tile != null)
                    normalCells.Add((x, y, tile));
            }

        if (obstacleGoalCells.Count > 0)
        {
            var pick = obstacleGoalCells[Random.Range(0, obstacleGoalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (tileGoalCells.Count > 0)
        {
            var pick = tileGoalCells[Random.Range(0, tileGoalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (otherObstacleCells.Count > 0)
        {
            var pick = otherObstacleCells[Random.Range(0, otherObstacleCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (normalCells.Count > 0)
        {
            var pick = normalCells[Random.Range(0, normalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        return (null, -1, -1, false);
    }
}
