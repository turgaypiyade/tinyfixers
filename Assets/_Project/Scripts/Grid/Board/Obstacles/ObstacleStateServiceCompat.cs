using System;
using System.Reflection;
using UnityEngine;

public static class ObstacleStateServiceCompat
{
    private static readonly MethodInfo TryGetStageSnapshotAtMethod =
        typeof(ObstacleStateService).GetMethod("TryGetStageSnapshotAt", BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo GetAllowDiagonalAtMethod =
        typeof(ObstacleStateService).GetMethod("GetAllowDiagonalAt", BindingFlags.Public | BindingFlags.Instance);

    private static readonly FieldInfo LevelField =
        typeof(ObstacleStateService).GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo RemainingHitsByOriginField =
        typeof(ObstacleStateService).GetField("remainingHitsByOrigin", BindingFlags.NonPublic | BindingFlags.Instance);

    public static bool TryGetStageSnapshotAtCompat(this ObstacleStateService service, int x, int y, out ObstacleStageSnapshot snapshot)
    {
        snapshot = default;
        if (service == null)
            return false;

        if (TryInvokeSnapshotApi(service, x, y, out snapshot))
            return true;

        return TryBuildSnapshotByReflection(service, x, y, out snapshot);
    }

    public static bool GetAllowDiagonalAtCompat(this ObstacleStateService service, int x, int y)
    {
        if (service == null)
            return false;

        if (GetAllowDiagonalAtMethod != null)
        {
            try
            {
                return (bool)GetAllowDiagonalAtMethod.Invoke(service, new object[] { x, y });
            }
            catch
            {
                // Fall through to snapshot-based fallback.
            }
        }

        if (service.TryGetStageSnapshotAtCompat(x, y, out var snapshot))
            return snapshot.allowDiagonal;

        return false;
    }

    private static bool TryInvokeSnapshotApi(ObstacleStateService service, int x, int y, out ObstacleStageSnapshot snapshot)
    {
        snapshot = default;
        if (TryGetStageSnapshotAtMethod == null)
            return false;

        var args = new object[] { x, y, default(ObstacleStageSnapshot) };

        try
        {
            bool success = (bool)TryGetStageSnapshotAtMethod.Invoke(service, args);
            if (!success)
                return false;

            snapshot = (ObstacleStageSnapshot)args[2];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildSnapshotByReflection(ObstacleStateService service, int x, int y, out ObstacleStageSnapshot snapshot)
    {
        snapshot = default;

        try
        {
            var level = LevelField?.GetValue(service) as LevelData;
            if (level == null || level.obstacles == null || level.obstacleOrigins == null)
                return false;

            if (!level.InBounds(x, y))
                return false;

            int idx = level.Index(x, y);
            if (idx < 0 || idx >= level.obstacles.Length || idx >= level.obstacleOrigins.Length)
                return false;

            var id = (ObstacleId)level.obstacles[idx];
            if (id == ObstacleId.None)
                return false;

            var library = level.obstacleLibrary;
            var def = library != null ? library.Get(id) : null;
            if (def == null)
                return false;

            int remaining = Mathf.Max(1, def.hits);
            int origin = level.obstacleOrigins[idx];
            if (origin >= 0)
            {
                var remainingHits = RemainingHitsByOriginField?.GetValue(service) as int[];
                if (remainingHits != null && origin < remainingHits.Length && remainingHits[origin] > 0)
                    remaining = remainingHits[origin];
            }

            var stage = def.GetStageRuleForRemainingHits(remaining);
            if (stage == null)
                return false;

            snapshot = new ObstacleStageSnapshot(stage.behavior, stage.blocksCells, stage.allowDiagonal, stage.damageRule, stage.sprite);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
