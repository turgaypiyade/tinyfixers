using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardAnimator
{
    private readonly BoardController board;
    private readonly TileClearEffectOrchestrator clearEffectOrchestrator;

    private readonly Color lightningColor = new Color(0.70f, 0.90f, 1f, 1f);

    private static readonly List<BoardController.PatchbotDashRequest> _patchbotDashBuffer = new();
    private static readonly Vector2Int[] OrthogonalDirs =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    // Cache WaitForSeconds instances to avoid GC allocations in frequently-called coroutines.
    // Keyed by milliseconds to keep lookups stable.
    private static readonly Dictionary<int, WaitForSeconds> _waitCache = new Dictionary<int, WaitForSeconds>(64);

    private static WaitForSeconds Wait(float seconds)
    {
        if (seconds <= 0f) return null;
        int ms = Mathf.Max(1, Mathf.RoundToInt(seconds * 1000f));
        if (_waitCache.TryGetValue(ms, out var w)) return w;
        w = new WaitForSeconds(ms / 1000f);
        _waitCache[ms] = w;
        return w;
    }

    public BoardAnimator(BoardController board)
    {
        this.board = board;
        clearEffectOrchestrator = new TileClearEffectOrchestrator(
            new GoalFlyTileClearEffect(board),
            new LightningStrikeTileClearEffect(board.BoardVfxPlayer, lightningColor),
            new DefaultPopTileClearEffect()
        );
    }

    public IEnumerator SwapTilesAnimated(TileView a, TileView b, float duration)
    {
        int ax = a.X, ay = a.Y;
        int bx = b.X, by = b.Y;

        board.Tiles[ax, ay] = b;
        board.Tiles[bx, by] = a;

        a.SetCoords(bx, by);
        b.SetCoords(ax, ay);

        yield return RunTogether(
            a.MoveToGrid(board.TileSize, duration, board.SwapMoveCurve),
            b.MoveToGrid(board.TileSize, duration, board.SwapMoveCurve)
        );
    }

    private IEnumerator RunTogether(IEnumerator c1, IEnumerator c2)
    {
        bool d1 = false, d2 = false;
        board.StartCoroutine(Wrap(c1, () => d1 = true));
        board.StartCoroutine(Wrap(c2, () => d2 = true));
        while (!d1 || !d2) yield return null;
    }

    private IEnumerator Wrap(IEnumerator c, Action onDone)
    {
        yield return c;
        onDone?.Invoke();
    }

    private IEnumerator WrapWithDelay(IEnumerator c, float delay, Action onDone)
    {
        var w = Wait(delay);
        if (w != null) yield return w;
        yield return c;
        onDone?.Invoke();
    }

    private IEnumerator RunMany(List<IEnumerator> routines)
    {
        int done = 0;
        for (int i = 0; i < routines.Count; i++)
            board.StartCoroutine(Wrap(routines[i], () => done++));

        while (done < routines.Count) yield return null;
    }

    private IEnumerator RunManyWithDelays(List<IEnumerator> routines, List<float> delays)
    {
        if (routines.Count != delays.Count)
        {
            yield return RunMany(routines);
            yield break;
        }

        int done = 0;
        for (int i = 0; i < routines.Count; i++)
            board.StartCoroutine(WrapWithDelay(routines[i], delays[i], () => done++));

        while (done < routines.Count) yield return null;
    }

    public IEnumerator ClearMatchesAnimated(
        HashSet<TileView> matches,
        bool doShake,
        Dictionary<TileView, float> staggerDelays = null,
        float staggerAnimTime = 0.16f,
        ClearAnimationMode animationMode = ClearAnimationMode.Default,
        HashSet<Vector2Int> affectedCells = null,
        ObstacleHitContext? obstacleHitContext = null,
        bool includeAdjacentOverTileBlockerDamage = true,
        TileView lightningOriginTile = null,
        Vector2Int? lightningOriginCell = null,
        IReadOnlyCollection<TileView> lightningVisualTargets = null)
    {
        var list = new List<TileView>(matches);
        var pops = new List<IEnumerator>();
        var pulseImpacts = new List<IEnumerator>();
        var shouldClearTile = new Dictionary<TileView, bool>();
        var clearedByType = new Dictionary<TileType, int>();

        float maxStaggerDelay = 0f;
        var impactCells = new HashSet<Vector2Int>();
        var obstacleDamageCells = new HashSet<Vector2Int>();

        board.ConsumePatchbotDashRequests(_patchbotDashBuffer);

        if (_patchbotDashBuffer.Count > 0 && board.PatchbotDashUI != null)
        {
            yield return board.PatchbotDashUI.PlayDashParallel(_patchbotDashBuffer, board);
        }

        ObstacleHitContext damageContext = obstacleHitContext ?? (board.IsSpecialActivationPhase
            ? ObstacleHitContext.SpecialActivation
            : ObstacleHitContext.NormalMatch);

        HashSet<TileView> lightningVisualSet = null;
        if (animationMode == ClearAnimationMode.LightningStrike && lightningVisualTargets != null)
            lightningVisualSet = new HashSet<TileView>(lightningVisualTargets);

        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            SortTilesForLightning(list, lightningOriginTile, lightningOriginCell);
        }

        List<TileView> orderedStrikeTargets = null;
        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            orderedStrikeTargets = lightningVisualTargets != null
                ? new List<TileView>(lightningVisualTargets)
                : new List<TileView>(list);

            SortTilesForLightning(orderedStrikeTargets, lightningOriginTile, lightningOriginCell);
        }

        float lightningStepDelay = animationMode == ClearAnimationMode.LightningStrike
            ? board.GetLightningStrikeStepDelay()
            : 0f;
        int lightningIndex = 0;
        float maxLightningPopCompletion = 0f;

        if (affectedCells != null)
        {
            foreach (var cell in affectedCells)
                impactCells.Add(cell);
        }

        for (int i = 0; i < list.Count; i++)
        {
            var tile = list[i];
            if (tile == null) continue;
            impactCells.Add(new Vector2Int(tile.X, tile.Y));
        }

        for (int i = 0; i < list.Count; i++)
        {
            var tile = list[i];
            if (tile == null) continue;

            if (!board.IsSpecialActivationPhase && tile.GetSpecial() != TileSpecial.None)
                continue;

            bool clearTile = true;
            if (board.ObstacleStateService != null)
                clearTile = !board.ObstacleStateService.IsCellBlocked(tile.X, tile.Y);

            shouldClearTile[tile] = clearTile;
            if (!clearTile) continue;

            if (staggerDelays != null && staggerDelays.TryGetValue(tile, out var d))
            {
                pulseImpacts.Add(tile.PlayPulseImpact(d, staggerAnimTime));
                if (d > maxStaggerDelay) maxStaggerDelay = d;
            }
            else
            {
                bool useLightningEffect = animationMode == ClearAnimationMode.LightningStrike
                    && (lightningVisualSet == null || lightningVisualSet.Contains(tile));

                // Goal tile mı?
                bool isGoalTile = false;
                var hud = board.TopHud;
                if (hud != null && board.GoalFlyFx != null)
                {
                    isGoalTile = hud.TryGetGoalTargetRectForTile(tile.GetTileType(), out _);
                }

                // Öncelik: goal fly > lightning per-tile > default
                float delay = useLightningEffect ? lightningIndex * lightningStepDelay : 0f;
                var tileAnimationMode =
                    isGoalTile ? ClearAnimationMode.GoalFlyToHud :
                    (useLightningEffect ? ClearAnimationMode.LightningStrike : ClearAnimationMode.Default);

                float clearDuration = board.GetClearDurationForCurrentPass();
                pops.Add(clearEffectOrchestrator.Play(tile, tileAnimationMode, delay, clearDuration));

                if (useLightningEffect)
                {
                    float completion = delay + clearDuration;
                    if (completion > maxLightningPopCompletion)
                        maxLightningPopCompletion = completion;

                    lightningIndex++;
                }
           }
        }

        float lightningDuration = 0f;
        if (animationMode == ClearAnimationMode.LightningStrike)
        {
            var strikeTargets = orderedStrikeTargets ?? list;
            lightningDuration = board.PlayLightningStrikeForTiles(strikeTargets, lightningOriginTile, lightningOriginCell, strikeTargets);
        }

        if (doShake)
        {
            if (board.PreClearDelay > 0f)
            {
                var __w = Wait(board.PreClearDelay);
                if (__w != null) yield return __w;
            }

            if (board.ShakeTarget != null)
                board.StartCoroutine(ShakeBoard(board.ShakeDuration, board.ShakeStrength));
        }

        if (pulseImpacts.Count > 0)
        {
            for (int i = 0; i < pulseImpacts.Count; i++)
                board.StartCoroutine(pulseImpacts[i]);
        }

        if (pops.Count > 0)
            yield return RunMany(pops);

        if (lightningDuration > 0f)
        {
            // Lightning ile tile pop animasyonları paralel çalışıyor.
            // Burada toplam lightning süresini tekrar beklemek, pop bittikten sonra
            // gereksiz "boş bekleme" yaratır. Sadece kalan kısmı bekleyelim.
            float remainingLightning = Mathf.Max(0f, lightningDuration - maxLightningPopCompletion);
            var __w = Wait(remainingLightning);
            if (__w != null) yield return __w;
        }

        if (pulseImpacts.Count > 0)
        {
            var __w = Wait(maxStaggerDelay + staggerAnimTime);
            if (__w != null) yield return __w;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var tile = list[i];
            if (tile == null) continue;

            if (!board.IsSpecialActivationPhase && tile.GetSpecial() != TileSpecial.None)
                continue;

            if (shouldClearTile.TryGetValue(tile, out var clearTile) && !clearTile)
                continue;

            tile.SetSpecial(TileSpecial.None);

            int x = tile.X;
            int y = tile.Y;

            if (x >= 0 && x < board.Width && y >= 0 && y < board.Height)
                board.Tiles[x, y] = null;

            var tileType = tile.GetTileType();
            clearedByType.TryGetValue(tileType, out int current);
            clearedByType[tileType] = current + 1;

            UnityEngine.Object.Destroy(tile.gameObject);
        }

        foreach (var pair in clearedByType)
            board.NotifyTilesCleared(pair.Key, pair.Value);

        if (board.ObstacleStateService == null)
            yield break;

        foreach (var cell in impactCells)
        {
            obstacleDamageCells.Add(cell);

            if (includeAdjacentOverTileBlockerDamage)
                CollectAdjacentOverTileBlockers(cell, obstacleDamageCells);
        }

        foreach (var cell in obstacleDamageCells)
            board.ApplyObstacleDamageAt(cell.x, cell.y, damageContext);
    }

    private static void SortTilesForLightning(List<TileView> tiles, TileView originTile, Vector2Int? originCell)
    {
        if (tiles == null || tiles.Count <= 1)
            return;

        Vector2Int origin = originTile != null
            ? new Vector2Int(originTile.X, originTile.Y)
            : originCell ?? new Vector2Int(tiles[0] != null ? tiles[0].X : 0, tiles[0] != null ? tiles[0].Y : 0);

        tiles.Sort((a, b) =>
        {
            if (a == b) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int da = Mathf.Abs(a.X - origin.x) + Mathf.Abs(a.Y - origin.y);
            int db = Mathf.Abs(b.X - origin.x) + Mathf.Abs(b.Y - origin.y);
            int byDistance = da.CompareTo(db);
            if (byDistance != 0) return byDistance;

            int byRow = a.Y.CompareTo(b.Y);
            if (byRow != 0) return byRow;

            return a.X.CompareTo(b.X);
        });
    }

    private void CollectAdjacentOverTileBlockers(Vector2Int centerCell, HashSet<Vector2Int> obstacleDamageCells)
    {
        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return;

        AddNeighbors(OrthogonalDirs);

        if (!obstacleStateService.GetAllowDiagonalAt(centerCell.x, centerCell.y))
            return;

        AddNeighbors(DiagonalDirs);

        void AddNeighbors(Vector2Int[] directions)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                var neighbor = centerCell + directions[i];
                if (neighbor.x < 0 || neighbor.x >= board.Width || neighbor.y < 0 || neighbor.y >= board.Height)
                    continue;

                if (!obstacleStateService.IsOverTileBlockerAt(neighbor.x, neighbor.y))
                    continue;

                obstacleDamageCells.Add(neighbor);
            }
        }
    }

    public IEnumerator ShakeBoard(float duration, float strength)
    {
        if (board.ShakeTarget == null) yield break;

        board.ShakeBasePos = board.ShakeTarget.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - (t / Mathf.Max(0.0001f, duration));

            float ox = UnityEngine.Random.Range(-strength, strength) * damper;
            float oy = UnityEngine.Random.Range(-strength, strength) * damper;

            board.ShakeTarget.anchoredPosition = board.ShakeBasePos + new Vector2(ox, oy);
            yield return null;
        }

        board.ShakeTarget.anchoredPosition = board.ShakeBasePos;
    }

    public IEnumerator MicroShake(float duration, float strength)
    {
        var target = board.ShakeTarget != null ? board.ShakeTarget : board.Parent;
        if (target == null)
            target = board.GetComponent<RectTransform>();
        if (target == null) yield break;

        Vector2 basePos = target.anchoredPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - (t / Mathf.Max(0.0001f, duration));

            float ox = UnityEngine.Random.Range(-strength, strength) * damper;
            float oy = UnityEngine.Random.Range(-strength, strength) * damper;

            target.anchoredPosition = basePos + new Vector2(ox, oy);
            yield return null;
        }

        target.anchoredPosition = basePos;
    }

    public IEnumerator CollapseColumnsAnimated()
    {
        var moves = new List<IEnumerator>();
        var moveDelays = new List<float>();

        for (int x = 0; x < board.Width; x++)
        {
            int segStartY = board.Height - 1;
            for (int y = board.Height - 1; y >= -1; y--)
            {
                bool isBoundary = (y == -1) || IsObstacleBlockedCell(x, y);

                if (!isBoundary)
                    continue;

                int segEndY = y + 1;
                if (segEndY <= segStartY)
                {
                    var slots = new List<int>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        slots.Add(yy);
                    }

                    var existing = new List<TileView>();
                    for (int yy = segStartY; yy >= segEndY; yy--)
                    {
                        if (board.Holes[x, yy]) continue;
                        var tv = board.Tiles[x, yy];
                        if (tv != null)
                            existing.Add(tv);
                    }

                    for (int i = 0; i < slots.Count; i++)
                        board.Tiles[x, slots[i]] = null;

                    for (int i = 0; i < existing.Count && i < slots.Count; i++)
                    {
                        int toY = slots[i];
                        var tile = existing[i];
                        int fromY = tile.Y;

                        if (fromY != toY)
                        {
                            board.Tiles[x, toY] = tile;
                            board.Tiles[x, fromY] = null;

                            tile.SetCoords(x, toY);

                            moves.Add(tile.MoveToGrid(
                                board.TileSize,
                                board.FallDurationWithMultiplier,
                                board.FallMoveCurve,
                                board.EnableFallSettle,
                                board.FallSettleDuration,
                                board.FallSettleStrength
                            ));
                            moveDelays.Add(0f);
                        }
                        else
                        {
                            board.Tiles[x, toY] = tile;
                        }
                    }
                }

                segStartY = y - 1;
            }
        }

        if (moves.Count > 0)
            yield return RunManyWithDelays(moves, moveDelays);
    }

    public IEnumerator SpawnNewTilesAnimated()
    {
        var moves = new List<IEnumerator>();

        for (int x = 0; x < board.Width; x++)
        {
            int nextSpawnY = Mathf.Min(-1, board.SpawnStartOffsetY);

            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                if (board.Tiles[x, y] != null) continue;

                var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                var view = go.GetComponent<TileView>();
                if (view == null)
                {
                    Debug.LogError("BoardController: Spawned prefab missing TileView.");
                    UnityEngine.Object.Destroy(go);
                    continue;
                }

                view.Init(board, x, y);

                view.SetCoords(x, nextSpawnY);
                view.SnapToGrid(board.TileSize);
                nextSpawnY--;

                view.SetCoords(x, y);
                board.Tiles[x, y] = view;

                view.SetType(GetRandomType());
                view.SetSpecial(TileSpecial.None);
                board.RefreshTileObstacleVisual(view);

                int dist = Mathf.Abs(y - nextSpawnY);
                float duration = board.GetFallDurationForDistance(dist);

                moves.Add(view.MoveToGrid(
                    board.TileSize,
                    duration,
                    board.FallMoveCurve,
                    board.ShouldEnableFallSettleThisPass(),
                    board.FallSettleDuration,
                    board.FallSettleStrength
                ));
            }
        }

        if (moves.Count > 0)
            yield return RunMany(moves);

        board.RefreshAllTileObstacleVisuals();
    }

    public IEnumerator CollapseAndSpawnAnimated()
    {
        var moves = new List<IEnumerator>();
        var moveDelays = new List<float>();

        for (int x = 0; x < board.Width; x++)
        {
            float baseDelay = x * board.FallColumnStep;
            int placedCount = 0;
            int spawnIndexInColumn = 0;

            int segmentTop = board.Height - 1;
            while (segmentTop >= 0)
            {
                while (segmentTop >= 0 && IsObstacleBlockedCell(x, segmentTop))
                    segmentTop--;

                if (segmentTop < 0)
                    break;

                int segmentBottom = segmentTop;
                while (segmentBottom >= 0 && !IsObstacleBlockedCell(x, segmentBottom))
                    segmentBottom--;

                int topY = segmentBottom + 1;
                bool touchesSpawnEdge = IsSegmentConnectedToSpawnEdge(x, topY);

                var slots = new List<int>();
                var existing = new List<TileView>();

                for (int y = segmentTop; y >= topY; y--)
                {
                    if (board.Holes[x, y]) continue;
                    slots.Add(y);

                    if (board.Tiles[x, y] != null)
                        existing.Add(board.Tiles[x, y]);
                }

                for (int i = 0; i < slots.Count; i++)
                    board.Tiles[x, slots[i]] = null;

                for (int i = 0; i < existing.Count && i < slots.Count; i++)
                {
                    int targetY = slots[i];
                    var tile = existing[i];

                    board.Tiles[x, targetY] = tile;
                    tile.SetCoords(x, targetY);

                    float startDelay = baseDelay + placedCount * board.FallCascadeStep;
                    placedCount++;

                    moves.Add(tile.MoveToGrid(
                        board.TileSize,
                        board.FallDurationWithMultiplier,
                        board.FallMoveCurve,
                        board.EnableFallSettle,
                        board.FallSettleDuration,
                        board.FallSettleStrength
                    ));
                    moveDelays.Add(startDelay);
                }

                if (touchesSpawnEdge)
                {
                    int nextSpawnY = topY + board.SpawnStartOffsetY;

                    for (int y = topY; y <= segmentTop; y++)
                    {
                        if (board.Holes[x, y]) continue;
                        if (board.Tiles[x, y] != null) continue;

                        var go = UnityEngine.Object.Instantiate(board.TilePrefab, board.Parent);
                        var view = go.GetComponent<TileView>();
                        if (view == null)
                        {
                            Debug.LogError("BoardController: Spawned prefab missing TileView.");
                            UnityEngine.Object.Destroy(go);
                            continue;
                        }

                        view.Init(board, x, y);

                        view.SetCoords(x, nextSpawnY);
                        view.SnapToGrid(board.TileSize);
                        nextSpawnY--;

                        view.SetCoords(x, y);
                        board.Tiles[x, y] = view;

                        view.SetType(GetRandomType());
                        view.SetSpecial(TileSpecial.None);
                        board.RefreshTileObstacleVisual(view);

                        int dist = Mathf.Abs(y - nextSpawnY);
                        float duration = board.GetFallDurationForDistance(dist);

                        float startDelay = baseDelay + (placedCount + spawnIndexInColumn) * board.FallCascadeStep;
                        spawnIndexInColumn++;

                        moves.Add(view.MoveToGrid(
                            board.TileSize,
                            duration,
                            board.FallMoveCurve,
                            board.ShouldEnableFallSettleThisPass(),
                            board.FallSettleDuration,
                            board.FallSettleStrength
                        ));
                        moveDelays.Add(startDelay);
                    }
                }

                segmentTop = segmentBottom - 1;
            }
        }

        if (moves.Count > 0)
            yield return RunManyWithDelays(moves, moveDelays);

        board.RefreshAllTileObstacleVisuals();
    }

    internal IEnumerator SlideFillAnimated()
    {
        const int maxPass = 32;

        for (int pass = 0; pass < maxPass; pass++)
        {
            bool movedAny = false;
            var moves = new List<IEnumerator>();
            var moveDelays = new List<float>();
            var movedThisPass = new HashSet<TileView>();

            for (int y = board.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsMaskHoleCell(x, y) || IsObstacleBlockedCell(x, y))
                        continue;

                    if (board.Tiles[x, y] != null) continue;
                    if (!IsSlideFillTarget(x, y)) continue;

                    bool TrySource(int sx, int sy)
                    {
                        LogVerbose($"[SOURCE] candidate=({sx},{sy}) target=({x},{y}) straightDown={CanTileFallStraightDown(sx,sy)}");
                        if (sx < 0 || sx >= board.Width || sy < 0 || sy >= board.Height) return false;
                        if (board.IsMaskHoleCell(sx, sy) || IsObstacleBlockedCell(sx, sy)) return false;
                        //if (IsAdjacentToMaskHole(sx, sy)) return false;

                        var t = board.Tiles[sx, sy];
                        if (t == null) return false;

                        // Normalde taş kendi kolonunda aşağı düşebiliyorsa diagonal'e öncelik vermeyelim.
                        // Ancak obstacle-ceplerinde (hedefin üstü blocker) bu kural çok katı kalıp
                        // boşlukların kilitlenmesine yol açabiliyor; bu durumda diagonal'e izin ver.
                        bool targetIsObstaclePocket = IsObstacleBlockedCell(x, y - 1);
                        if (!targetIsObstaclePocket && CanTileFallStraightDown(sx, sy))
                            return false;

                        return TryDiagonalFrom(sx, sy, x, y, movedThisPass, moves, moveDelays);
                    }

                    bool moved = TrySource(x - 1, y - 1) || TrySource(x + 1, y - 1);
                    if (moved) movedAny = true;
                }
            }

            if (moves.Count > 0)
                yield return RunManyWithDelays(moves, moveDelays);

            if (!movedAny)
            {
                // Sigorta: bazen CanTileFallStraightDown / obstacle state yüzünden bu pass "no move" sanılıyor.
                // Ama hâlâ doldurulabilir boşluk varsa bir kez daha collapse dene ve tekrar pass'e gir.
                if (!HasAnySlideFillTargetsRemaining())
                    yield break;

                yield return CollapseColumnsAnimated();
                continue;
            }

            yield return CollapseColumnsAnimated();

        }
    }

    private bool HasAnySlideFillTargetsRemaining()
    {
        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (board.IsMaskHoleCell(x, y) || IsObstacleBlockedCell(x, y))
                    continue;

                if (board.Tiles[x, y] != null) 
                    continue;

                if (!IsSlideFillTarget(x, y)) 
                    continue;

                return true; // hâlâ hedef boşluk var
            }
        }
        return false;
    }

    private bool IsSegmentConnectedToSpawnEdge(int x, int topY)
    {
        if (topY <= 0) return true;

        for (int y = topY - 1; y >= 0; y--)
        {
            if (IsObstacleBlockedCell(x, y))
                return false;

            if (!board.IsSpawnPassThroughCell(x, y))
                return false;
        }

        return true;
    }

    private bool IsSlideFillTarget(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        if (board.Tiles[x, y] != null)
            return false;

        if (IsNonObstacleHoleCell(x, y))
            return false;

        if (IsAdjacentToMaskHole(x, y))
            return false;

        bool obstacleAbove = IsObstacleBlockedCell(x, y - 1);
        if (obstacleAbove)
            return true;

        if (IsFloorPocketTarget(x, y))
            return true;

        return false;
    }

    private bool IsObstacleBlockedCell(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height)
            return false;

        var obstacleStateService = board.ObstacleStateService;
        if (obstacleStateService == null)
            return false;

        return obstacleStateService.IsCellBlocked(x, y);
    }

    private bool IsNonObstacleHoleCell(int hx, int hy)
    {
        return hx >= 0 && hx < board.Width && hy >= 0 && hy < board.Height
               && board.Holes[hx, hy]
               && !IsObstacleBlockedCell(hx, hy);
    }

    private bool HasAnyTileAboveInSameSegment(int x, int y)
    {
        for (int yy = y - 1; yy >= 0; yy--)
        {
            if (IsObstacleBlockedCell(x, yy))
                break;

            if (IsNonObstacleHoleCell(x, yy))
                continue;

            if (board.Tiles[x, yy] != null)
                return true;
        }
        return false;
    }

    private bool IsFloorPocketTarget(int x, int y)
    {
        bool hasBottomVoid = (y >= board.Height - 1) || IsNonObstacleHoleCell(x, y + 1);
        if (!hasBottomVoid)
            return false;

        if (HasAnyTileAboveInSameSegment(x, y))
            return false;

        return true;
    }

    private bool IsAdjacentToMaskHole(int x, int y)
    {
        if (IsNonObstacleHoleCell(x, y)) return true;

        if (x > 0 && IsNonObstacleHoleCell(x - 1, y)) return true;
        if (x < board.Width - 1 && IsNonObstacleHoleCell(x + 1, y)) return true;
        if (y > 0 && IsNonObstacleHoleCell(x, y - 1)) return true;
        if (y < board.Height - 1 && IsNonObstacleHoleCell(x, y + 1)) return true;

        return false;
    }

    private bool CanTileFallStraightDown(int fromX, int fromY)
    {
        int y = fromY + 1;
        while (y < board.Height)
        {
            if (IsObstacleBlockedCell(fromX, y))
                return false;

            if (board.Holes[fromX, y] && !IsObstacleBlockedCell(fromX, y))
            {
                y++;
                continue;
            }

            return board.Tiles[fromX, y] == null;
        }

        return false;
    }

    private bool TrySlideFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        List<IEnumerator> moves,
        List<float> delays)
    {
        if (fromX < 0 || fromX >= board.Width) return false;
        if (fromY < 0 || fromY >= board.Height) return false;

        if (board.Holes[fromX, fromY]) return false;

        var tile = board.Tiles[fromX, fromY];
        if (tile == null) return false;
        if (movedThisPass.Contains(tile)) return false;

        board.Tiles[fromX, fromY] = null;
        board.Tiles[toX, toY] = tile;
        tile.SetCoords(toX, toY);

        float slideDuration = board.GetFallDurationForDistance(1) * 0.6f;
        moves.Add(tile.MoveToGrid(
            board.TileSize,
            slideDuration,
            board.FallMoveCurve,
            false,
            0f,
            0f
        ));

        delays.Add(0f);
        movedThisPass.Add(tile);

        return true;
    }

   /* private bool TryDiagonalFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        List<IEnumerator> moves,
        List<float> delays)
    {
        return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
    }*/

    private bool TryDiagonalFrom(
        int fromX, int fromY,
        int toX, int toY,
        HashSet<TileView> movedThisPass,
        List<IEnumerator> moves,
        List<float> delays)
    {
        // Corner hücreler
        int cax = fromX, cay = toY;
        int cbx = toX,  cby = fromY;

        LogVerbose($"[DIAG-TRY] from=({fromX},{fromY}) to=({toX},{toY})");

        // Board sınırı
        if (cax < 0 || cax >= board.Width || cay < 0 || cay >= board.Height) return false;
        if (cbx < 0 || cbx >= board.Width || cby < 0 || cby >= board.Height) return false;

        // Mask hole / oynanamaz köşeden diagonal geçmesin
        if (board.IsMaskHoleCell(cax, cay) || board.IsMaskHoleCell(cbx, cby)) return false;

        var obs = board.ObstacleStateService;
        if (obs != null)
        {
            if (obs.IsCellBlocked(cax, cay))
            {
                if (!obs.GetAllowDiagonalAt(cax, cay))
                    return false;
            }

            if (obs.IsCellBlocked(cbx, cby))
            {
                if (!obs.GetAllowDiagonalAt(cbx, cby))
                    return false;
            }
        }
        bool ok = TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
        LogVerbose($"[DIAG-RESULT] from=({fromX},{fromY}) to=({toX},{toY}) ok={ok}");
        return ok;
       // return TrySlideFrom(fromX, fromY, toX, toY, movedThisPass, moves, delays);
    }
    private TileType GetRandomType()
    {
        if (board.RandomPool == null || board.RandomPool.Length == 0)
            return TileType.Gear;

        return board.RandomPool[UnityEngine.Random.Range(0, board.RandomPool.Length)];
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogVerbose(string message)
    {
        Debug.Log(message);
    }
}
