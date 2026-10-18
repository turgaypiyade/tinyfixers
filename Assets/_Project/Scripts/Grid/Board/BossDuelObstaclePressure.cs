using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Small, breakable debris and overlays only. Never throw furniture, generators or rewards.
public static class BossDuelObstaclePressure
{
    private enum LandResult { Placed, Retry, Drop }

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
                if (service.HasObstacleAt(x, y) || service.IsInteractionLockedAt(x, y)
                    || !board.IsTileFreeForDynamicObstacle(x, y)) continue;
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

    private static bool CanLandWithoutBlockingBoard(BoardController board, Vector2Int cell, List<ObstacleId> pool)
    {
        int alive = 0, usable = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (board.Holes[x, y]) continue;
                usable++;
                if (pool.Contains(board.ObstacleStateService.GetObstacleIdAt(x, y))) alive++;
            }
        int cap = Mathf.Min(Mathf.Max(1, board.ActiveLevelData.bossMaxPressureObstacles), Mathf.Max(1, usable / 4));
        return alive < cap && board.HasAnyPlayableSwapWithAdditionalLockedCells(new HashSet<Vector2Int> { cell });
    }

    /// Flight is visual-only; each destination is validated again at impact without locking input.
    public static IEnumerator Throw(BoardController board, RectTransform source, RectTransform effectsRoot,
        List<Vector2Int> targets, ObstacleId id, BossDuelCharacterView character = null,
        System.Func<bool> cancelled = null, System.Action onRelease = null, List<ObstacleId> capPool = null)
    {
        var def = board.ActiveLevelData.obstacleLibrary.Get(id);
        // İniş sınırı, hedef SEÇERKEN kullanılan listeyle sayılmalı (alet tehdidi kendi sayısıyla; eskiden
        // her zaman boss'un yağ havuzu sayılıyordu → tahtada yağ çoksa alet tehdidi hiç inemiyordu).
        var landingPool = capPool ?? GetPool(board.ActiveLevelData);
        var root = effectsRoot != null ? effectsRoot : board.TilesRoot;
        var projectiles = new List<RectTransform>();
        var ends = new List<Vector3>();
        Vector3 start = root.InverseTransformPoint(source != null ? source.position : board.transform.position);
        bool animated = character != null && character.HasThrowAnimation;
        bool released = !animated;
        IEnumerator animation = null;
        bool Cancelled() => board == null || root == null || (cancelled != null && cancelled());
        try
        {
            if (Cancelled() || targets.Count == 0) yield break;
            float worldCellSize = board.TilesRoot.TransformVector(Vector3.right * board.TileSize).magnitude;
            float size = root.InverseTransformVector(Vector3.right * worldCellSize).magnitude * 0.8f;
            float heldSize = animated
                ? root.InverseTransformVector(Vector3.up * character.HeldObstacleWorldSize).magnitude : size;
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
                rt.sizeDelta = Vector2.one * heldSize;
                rt.localPosition = start;
                go.SetActive(!animated);
                projectiles.Add(rt);
                ends.Add(root.InverseTransformPoint(board.GetCellWorldCenterPosition(cell.x, cell.y)));
            }
            if (animated)
            {
                animation = character.ThrowObstacle(hand =>
                {
                    start = root.InverseTransformPoint(hand);
                    // One visible prop in the hands; the volley fans out from that same point.
                    projectiles[0].gameObject.SetActive(true);
                    projectiles[0].localPosition = start;
                }, () =>
                {
                    released = true;
                    foreach (var projectile in projectiles)
                    {
                        projectile.localPosition = start;
                        projectile.gameObject.SetActive(true);
                    }
                }, Cancelled);
            }
            // İniş: hücre GEÇİCİ olarak uygun değilse (taş düşüyor/sürükleniyor, tetiklenmeyi bekleyen
            // special, rezerve hedef) mermi hücrede bekler ve oturunca yerleşir — eskiden engel sessizce
            // atılıyordu (oyuncu aynı anda hamle yapınca boss'un attığı hiçbir şey tahtada kalmıyordu).
            // KALICI ret (engel var, special/anahtar taş, sınır/oynanabilirlik) → vazgeç.
            const float MaxLandingWait = 2f;
            var pending = new List<int>();
            for (int i = 0; i < targets.Count; i++) pending.Add(i);
            bool landed = false;
            float landingWait = 0f;

            LandResult TryLand(Vector2Int cell)
            {
                var tile = board.Tiles[cell.x, cell.y];
                if (board.GridData[cell.x, cell.y] == null || tile == null) return LandResult.Retry;
                if (board.ObstacleStateService.HasObstacleAt(cell.x, cell.y)
                    || board.ObstacleStateService.IsInteractionLockedAt(cell.x, cell.y)) return LandResult.Drop;
                if (!tile.IsRuntimeIdle || tile.WasDragging
                    || board.IsPendingTriggeredSpecialCell(cell.x, cell.y)
                    || board.IsReservedTileTargetCell(cell.x, cell.y)) return LandResult.Retry;
                if (tile.GetSpecial() != TileSpecial.None || tile.GetTileType() == TileType.Key) return LandResult.Drop;
                if (!CanLandWithoutBlockingBoard(board, cell, landingPool)) return LandResult.Drop;
                if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(cell.x, cell.y, id)) return LandResult.Drop;

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
                return LandResult.Placed;
            }

            // Her çağrıda bekleyen hücreleri dener; biten (yerleşen/vazgeçilen) mermi gizlenir.
            void Land(bool giveUp)
            {
                bool anyPlaced = false;
                for (int k = pending.Count - 1; k >= 0; k--)
                {
                    int i = pending[k];
                    var result = TryLand(targets[i]);
                    if (result == LandResult.Retry && !giveUp) continue;
                    if (result == LandResult.Placed) anyPlaced = true;
                    if (projectiles[i] != null) projectiles[i].gameObject.SetActive(false);
                    pending.RemoveAt(k);
                }
                if (anyPlaced) board.RequestResolveAfterActionSequence();
                if (pending.Count == 0) landed = true;
            }

            const float duration = 0.38f;
            float flightTime = 0f;
            bool animationPlaying = animation != null;
            bool releaseNotified = false;
            while (true)
            {
                if (Cancelled()) yield break;
                if (animationPlaying) animationPlaying = animation.MoveNext();
                if (!released && !animationPlaying) yield break;
                if (released)
                {
                    if (!releaseNotified)
                    {
                        releaseNotified = true;
                        onRelease?.Invoke();
                    }
                    float t = Mathf.Clamp01(flightTime / duration);
                    for (int i = 0; i < projectiles.Count; i++)
                    {
                        if (projectiles[i] == null || !projectiles[i].gameObject.activeSelf) continue;
                        projectiles[i].localPosition = Vector3.Lerp(start, ends[i], t)
                            + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 90f);
                        projectiles[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * 25f);
                        projectiles[i].sizeDelta = Vector2.one * Mathf.Lerp(heldSize, size, t);
                    }
                    if (flightTime >= duration && !landed)
                    {
                        // Mermi hücreye vardı: oturmayı bekler (en fazla MaxLandingWait), sonra vazgeçer.
                        Land(giveUp: landingWait >= MaxLandingWait);
                        landingWait += Time.deltaTime;
                    }
                    if (landed && !animationPlaying) break;
                    flightTime += Time.deltaTime;
                }
                yield return null;
            }

        }
        finally
        {
            (animation as System.IDisposable)?.Dispose();
            foreach (var projectile in projectiles)
                if (projectile != null) Object.Destroy(projectile.gameObject);
        }
    }
}
