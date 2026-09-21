using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Small, breakable debris and overlays only. Never throw furniture, generators or rewards.
public static class BossDuelObstaclePressure
{
    public static bool IsThrowable(LevelData level, ObstacleId id)
    {
        switch (id)
        {
            case ObstacleId.Oil:
            case ObstacleId.Mud:
            case ObstacleId.SpreadingGel:
            case ObstacleId.plastic:
            case ObstacleId.plastic_orange:
            case ObstacleId.Plastic_Yellow:
            case ObstacleId.Plastic_Blue:
            case ObstacleId.Plastic_Red:
            case ObstacleId.Plastic_Green:
            case ObstacleId.HelmetPorcelain:
            case ObstacleId.PlasticTwoStage:
                var def = level != null && level.obstacleLibrary != null ? level.obstacleLibrary.Get(id) : null;
                return def != null && def.size == Vector2Int.one && def.GetPreviewSprite() != null;
            default:
                return false;
        }
    }

    public static List<ObstacleId> GetPool(LevelData level)
    {
        var pool = new List<ObstacleId>();
        if (level == null) return pool;
        if (level.bossThrownObstacles == null || level.bossThrownObstacles.Length == 0)
        {
            if (IsThrowable(level, ObstacleId.Oil)) pool.Add(ObstacleId.Oil);
            return pool;
        }
        foreach (var id in level.bossThrownObstacles)
            if (IsThrowable(level, id) && !pool.Contains(id)) pool.Add(id);
        return pool;
    }

    public static List<Vector2Int> PickTargets(BoardController board, List<ObstacleId> pool, int count)
    {
        var candidates = new List<Vector2Int>();
        var picked = new List<Vector2Int>();
        var reserved = new HashSet<Vector2Int>();
        var service = board.ObstacleStateService;
        if (service == null || pool.Count == 0) return picked;
        int alive = 0, usable = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y]) continue;
                usable++;
                if (pool.Contains(service.GetObstacleIdAt(x, y))) alive++;
                var tile = board.Tiles[x, y];
                if (service.HasObstacleAt(x, y) || service.IsInteractionLockedAt(x, y)
                    || tile == null || !tile.IsRuntimeIdle || board.GridData[x, y] == null
                    || tile.GetSpecial() != TileSpecial.None || board.IsReservedTileTargetCell(x, y)) continue;
                candidates.Add(new Vector2Int(x, y));
            }
        // Pressure cannot occupy more than a quarter of the board, even with an oversized editor value.
        int cap = Mathf.Min(Mathf.Max(1, board.ActiveLevelData.bossMaxPressureObstacles), Mathf.Max(1, usable / 4));
        count = Mathf.Clamp(count, 0, Mathf.Max(0, cap - alive));
        while (picked.Count < count && candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            var cell = candidates[index];
            candidates.RemoveAt(index);
            reserved.Add(cell);
            // Treat every projectile as a blocker for this check, including harmless overlays.
            if (!board.HasAnyPlayableSwapWithAdditionalLockedCells(reserved))
            {
                reserved.Remove(cell);
                continue;
            }
            picked.Add(cell);
        }
        return picked;
    }

    /// Caller owns a brief input lock and waits for current board jobs before choosing targets.
    public static IEnumerator Throw(BoardController board, RectTransform source, RectTransform effectsRoot,
        List<Vector2Int> targets, ObstacleId id)
    {
        var def = board.ActiveLevelData.obstacleLibrary.Get(id);
        var root = effectsRoot != null ? effectsRoot : board.TilesRoot;
        var projectiles = new List<RectTransform>();
        var ends = new List<Vector3>();
        Vector3 start = root.InverseTransformPoint(source != null ? source.position : board.transform.position);
        try
        {
            foreach (var cell in targets)
            {
                var go = new GameObject("BossThrownObstacle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                BossDuelController.MatchParentLayer(rt);
                var image = go.GetComponent<Image>();
                image.sprite = def.GetPreviewSprite();
                image.preserveAspect = true;
                image.raycastTarget = false;
                float worldCellSize = board.TilesRoot.TransformVector(Vector3.right * board.TileSize).magnitude;
                float size = root.InverseTransformVector(Vector3.right * worldCellSize).magnitude * 0.8f;
                rt.sizeDelta = Vector2.one * size;
                rt.localPosition = start;
                projectiles.Add(rt);
                ends.Add(root.InverseTransformPoint(board.GetCellWorldCenterPosition(cell.x, cell.y)));
            }
            const float duration = 0.38f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / duration);
                for (int i = 0; i < projectiles.Count; i++)
                {
                    projectiles[i].localPosition = Vector3.Lerp(start, ends[i], t)
                        + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 90f);
                    projectiles[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * 25f);
                }
                yield return null;
            }
            foreach (var cell in targets)
            {
                var tile = board.Tiles[cell.x, cell.y];
                if (tile == null || tile.GetSpecial() != TileSpecial.None
                    || board.ObstacleStateService.HasObstacleAt(cell.x, cell.y)) continue;
                if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(cell.x, cell.y, id)) continue;
                if (def.IsMovableObstacle)
                {
                    // Convert in place: no tile-clear event, player power, goal credit or refill.
                    tile.SetUseFullCellIcon(false);
                    tile.SetMovableObstacleTile(true);
                    tile.SetFullCellMovableSprite(def.fullCellSprite);
                    tile.SetVisualLayout(TileView.TileVisualLayout.Centered);
                    tile.SetMovableObstacleSprite(def.GetPreviewSprite());
                    tile.ApplyTileSize(board.TileSize);
                }
                board.RaiseObstacleCreatedDynamic(cell.x, cell.y);
            }
        }
        finally
        {
            foreach (var projectile in projectiles)
                if (projectile != null) Object.Destroy(projectile.gameObject);
        }
    }
}
