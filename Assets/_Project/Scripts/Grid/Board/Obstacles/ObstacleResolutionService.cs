using System.Collections.Generic;
using UnityEngine;

public sealed class ObstacleResolutionService
{
    private readonly BoardController board;
    private readonly Dictionary<int, int> patchBotForcedObstacleHits = new();

    public ObstacleResolutionService(BoardController board)
    {
        this.board = board;
    }

    public ObstacleStateService ObstacleState => board.ObstacleStateService;

    public ObstacleStateService.ObstacleHitResult ApplyDamageAt(int x, int y, ObstacleHitContext context)
    {
        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return default;

        bool patchBotForcedHit = ConsumePatchBotForcedHit(x, y);
        var result = obstacleStateService.TryDamageAt(x, y, context);

        ObstacleStateService.ObstacleHitResult TryFallback(ObstacleHitContext fallbackContext)
        {
            if (fallbackContext == context)
                return default;

            return obstacleStateService.TryDamageAt(x, y, fallbackContext);
        }

        if (!result.didHit && context == ObstacleHitContext.Booster)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
        }
        else if (!result.didHit && patchBotForcedHit)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Scripted);
        }
        else if (!result.didHit && IsCrossContextFallbackAllowedAt(x, y))
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
        }

        if (!result.didHit)
            return result;

        ConsumeStageTransition(result);
        return result;
    }

    public void MarkPatchBotForcedHit(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return;

        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return;

        if (!obstacleStateService.HasObstacleAt(x, y))
            return;

        int idx = y * board.Width + x;
        patchBotForcedObstacleHits.TryGetValue(idx, out int count);
        patchBotForcedObstacleHits[idx] = count + 1;
    }

    public bool HasObstacleAt(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        return obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y);
    }

    public ObstacleId GetObstacleIdAt(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        return obstacleStateService != null ? obstacleStateService.GetObstacleIdAt(x, y) : ObstacleId.None;
    }

    public bool IsBlockedCell(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        return obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
    }

    public bool IsOverTileBlockerAt(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        return obstacleStateService != null && obstacleStateService.IsOverTileBlockerAt(x, y);
    }

    public bool IsDiagonalAllowedAt(int x, int y)
    {
        var obstacleStateService = board.ObstacleStateService;
        return obstacleStateService != null && obstacleStateService.IsDiagonalAllowedAt(x, y);
    }

    private bool ConsumePatchBotForcedHit(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        int idx = y * board.Width + x;
        if (!patchBotForcedObstacleHits.TryGetValue(idx, out int count) || count <= 0)
            return false;

        count--;
        if (count <= 0) patchBotForcedObstacleHits.Remove(idx);
        else patchBotForcedObstacleHits[idx] = count;

        return true;
    }

    private void ConsumeStageTransition(ObstacleStateService.ObstacleHitResult result)
    {
        if (!result.stageTransition.hasTransition)
            return;

        if (!result.stageTransition.cleared)
            board.RaiseObstacleStageChanged(result.stageTransition.originIndex, result.stageTransition.currentStage);

        var affected = result.affectedCellIndices;
        if (affected == null || affected.Length == 0)
            return;

        for (int i = 0; i < affected.Length; i++)
        {
            int idx = affected[i];
            int x = idx % board.Width;
            int y = idx / board.Width;

            if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
                continue;

            board.SetHoleStateFromObstacle(x, y);
        }
    }

    private bool IsCrossContextFallbackAllowedAt(int x, int y)
    {
        var levelData = board.ActiveLevelData;
        if (levelData == null || levelData.obstacles == null || !levelData.InBounds(x, y))
            return false;

        int idx = levelData.Index(x, y);
        if (idx < 0 || idx >= levelData.obstacles.Length)
            return false;

        var obstacleId = (ObstacleId)levelData.obstacles[idx];
        if (obstacleId == ObstacleId.None)
            return false;

        var def = levelData.obstacleLibrary != null ? levelData.obstacleLibrary.Get(obstacleId) : null;
        return def != null && def.allowCrossContextFallback;
    }
}