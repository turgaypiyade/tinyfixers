using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles booster activation, application, and shuffle.
/// Coroutines use board.StartCoroutine.
/// </summary>
public class BoosterService
{
    private readonly BoardController board;

    public BoosterService(BoardController board)
    {
        this.board = board;
    }

    public IEnumerator ApplyBoosterRoutine(BoardController.BoosterMode mode, TileView target, Vector2Int? targetCell,
        SpecialResolver specialResolver, ActionSequencer actionSequencer, CascadeLogic cascadeLogic,
        LineSweepService lineSweepService, LightningSpawner lightningSpawner, LineTravelSplitSwapTestUI lineTravelPlayer)
    {
        board.BeginBusy();
        board.IsSpecialActivationPhase = true;

        bool hasValidTargetCell = targetCell.HasValue
                                  && targetCell.Value.x >= 0 && targetCell.Value.x < board.Width
                                  && targetCell.Value.y >= 0 && targetCell.Value.y < board.Height;

        if (target == null && !hasValidTargetCell)
        {
            board.IsSpecialActivationPhase = false;
            board.EndBusy();
            yield break;
        }

        var matches = new HashSet<TileView>();
        HashSet<TileView> initialLightningTargets = null;
        var affectedCells = new HashSet<Vector2Int>();

        switch (mode)
        {
            case BoardController.BoosterMode.Single:
                if (target != null) matches.Add(target);
                if (hasValidTargetCell && IsCellBoosterAffectable(targetCell.Value.x, targetCell.Value.y))
                    affectedCells.Add(targetCell.Value);
                break;
            case BoardController.BoosterMode.Row:
                int rowY = target != null ? target.Y : targetCell.GetValueOrDefault().y;
                AddRow(matches, rowY);
                AddRowCells(affectedCells, rowY);
                break;
            case BoardController.BoosterMode.Column:
                int columnX = target != null ? target.X : targetCell.GetValueOrDefault().x;
                AddColumn(matches, columnX);
                AddColumnCells(affectedCells, columnX);
                break;
        }

        if ((mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column) && matches.Count > 0)
            initialLightningTargets = new HashSet<TileView>(matches);

        if (matches.Count > 0 || affectedCells.Count > 0)
        {
            bool hasLineActivation = false;

            var chainLineStrikes = new List<LightningLineStrike>();
            specialResolver.ExpandSpecialChain(
                matches, affectedCells,
                out hasLineActivation, out _,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: chainLineStrikes);

            var animationMode = (mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column)
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            if (hasLineActivation) animationMode = ClearAnimationMode.LightningStrike;

            ObstacleHitContext obstacleHitContext = ObstacleHitContext.Booster;

            List<LightningLineStrike> lightningLineStrikes = null;
            if (animationMode == ClearAnimationMode.LightningStrike)
            {
                lightningLineStrikes = chainLineStrikes.Count > 0 ? chainLineStrikes : new List<LightningLineStrike>();

                if (targetCell.HasValue && (mode == BoardController.BoosterMode.Row || mode == BoardController.BoosterMode.Column))
                    lightningLineStrikes.Add(new LightningLineStrike(targetCell.Value, mode == BoardController.BoosterMode.Row));

                if (lightningLineStrikes.Count == 0) lightningLineStrikes = null;
            }

            actionSequencer.Enqueue(new MatchClearAction(
                matches, doShake: true, animationMode: animationMode,
                affectedCells: affectedCells, obstacleHitContext: obstacleHitContext,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: target, lightningOriginCell: targetCell,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: lightningLineStrikes,
                enqueueCascadeOnComplete: true));
            while (actionSequencer.IsPlaying) yield return null;

            yield return board.ResolveBoardPublic();
        }

        board.IsSpecialActivationPhase = false;
        board.EndBusy();
    }


    public IEnumerator ShuffleBoardRoutine(ActionSequencer actionSequencer)
    {
        yield return SafeShuffleBoardRoutine(board.BoardInitService);
    }

    // BoosterService.cs

    public IEnumerator SafeShuffleBoardRoutine(BoardInitService boardInitService)
    {
        board.BeginBusy();

        var currentTypes = new TileType[board.Width, board.Height];
        var lockedMask = new bool[board.Width, board.Height];

        BuildSafeShuffleState(currentTypes, lockedMask);

        if (boardInitService != null &&
            boardInitService.TryBuildSafeShuffleTypes(currentTypes, lockedMask, board.RandomPool, out var finalTypes))
        {
            var sourceForDest = new Vector2Int[board.Width, board.Height];

            bool hasMapping = BuildShuffleSourceMap(currentTypes, finalTypes, lockedMask, sourceForDest);

            if (hasMapping)
            {
                yield return AnimateShufflePreview(sourceForDest, lockedMask);
                CommitShuffleFromSourceMap(sourceForDest, lockedMask);
            }
            else
            {
                // çok nadir fallback
                ApplyShuffledTypes(finalTypes, lockedMask);
            }

            board.SyncAllTilesToGridData();
            board.RefreshAllTileObstacleVisuals();
            board.RefreshAllSortingOrders();
        }

        board.EndBusy();
    }
    private bool BuildShuffleSourceMap(
        TileType[,] currentTypes,
        TileType[,] finalTypes,
        bool[,] lockedMask,
        Vector2Int[,] sourceForDest)
    {
        var buckets = new Dictionary<TileType, Queue<Vector2Int>>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                sourceForDest[x, y] = new Vector2Int(x, y);

                if (lockedMask[x, y])
                    continue;

                var type = currentTypes[x, y];
                if (!buckets.TryGetValue(type, out var q))
                {
                    q = new Queue<Vector2Int>();
                    buckets[type] = q;
                }

                q.Enqueue(new Vector2Int(x, y));
            }
        }

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var finalType = finalTypes[x, y];

                if (!buckets.TryGetValue(finalType, out var q) || q.Count == 0)
                    return false;

                sourceForDest[x, y] = q.Dequeue();
            }
        }

        return true;
    }

    private IEnumerator AnimateShufflePreview(Vector2Int[,] sourceForDest, bool[,] lockedMask)
    {
        var movingTiles = new List<TileView>();
        var starts = new List<Vector2>();
        var ends = new List<Vector2>();

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var src = sourceForDest[x, y];
                if (src.x == x && src.y == y)
                    continue;

                var tile = board.Tiles[src.x, src.y];
                if (tile == null)
                    continue;

                movingTiles.Add(tile);
                starts.Add(tile.RectTransform.anchoredPosition);
                ends.Add(new Vector2(x * board.TileSize, -y * board.TileSize));

                // üstte çizilsin
                tile.transform.SetAsLastSibling();
            }
        }

        if (movingTiles.Count == 0)
            yield break;

        float duration = Mathf.Max(0.08f, board.SwapDurationWithMultiplier * 0.85f);
        var curve = board.SwapMoveCurve;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = Mathf.Clamp01(t);
            float s = (curve != null && curve.length > 0)
                ? Mathf.Clamp01(curve.Evaluate(k))
                : k;

            for (int i = 0; i < movingTiles.Count; i++)
            {
                var tile = movingTiles[i];
                if (tile == null) continue;
                tile.RectTransform.anchoredPosition = Vector2.LerpUnclamped(starts[i], ends[i], s);
            }

            yield return null;
        }

        for (int i = 0; i < movingTiles.Count; i++)
        {
            var tile = movingTiles[i];
            if (tile == null) continue;
            tile.RectTransform.anchoredPosition = ends[i];
        }
    }

    private void CommitShuffleFromSourceMap(Vector2Int[,] sourceForDest, bool[,] lockedMask)
    {
        var snapshot = new TileView[board.Width, board.Height];

        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                snapshot[x, y] = board.Tiles[x, y];

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y])
                    continue;

                var src = sourceForDest[x, y];
                var tile = snapshot[src.x, src.y];

                board.Tiles[x, y] = tile;

                if (tile != null)
                {
                    tile.SetCoords(x, y);
                    tile.SnapToGrid(board.TileSize);
                    board.RefreshTileObstacleVisual(tile);
                }
            }
        }
    }
    public void AddRow(HashSet<TileView> matches, int y)
    {
        if (y < 0 || y >= board.Height) return;
        for (int x = 0; x < board.Width; x++)
            if (!board.Holes[x, y] && board.Tiles[x, y] != null)
                matches.Add(board.Tiles[x, y]);
    }

    public void AddColumn(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= board.Width) return;
        for (int y = 0; y < board.Height; y++)
            if (!board.Holes[x, y] && board.Tiles[x, y] != null)
                matches.Add(board.Tiles[x, y]);
    }

    public void AddRowCells(HashSet<Vector2Int> affectedCells, int y)
    {
        if (affectedCells == null || y < 0 || y >= board.Height) return;
        for (int x = 0; x < board.Width; x++)
            if (IsCellBoosterAffectable(x, y)) affectedCells.Add(new Vector2Int(x, y));
    }

    public void AddColumnCells(HashSet<Vector2Int> affectedCells, int x)
    {
        if (affectedCells == null || x < 0 || x >= board.Width) return;
        for (int y = 0; y < board.Height; y++)
            if (IsCellBoosterAffectable(x, y)) affectedCells.Add(new Vector2Int(x, y));
    }

    public bool IsCellBoosterAffectable(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (!board.Holes[x, y]) return true;
        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }


    private void BuildSafeShuffleState(TileType[,] currentTypes, bool[,] lockedMask)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                bool locked = false;

                if (board.Holes[x, y])
                {
                    locked = true;
                }
                else
                {
                    var tile = board.Tiles[x, y];
                    if (tile == null)
                    {
                        locked = true;
                    }
                    else if (tile.GetSpecial() != TileSpecial.None)
                    {
                        locked = true;
                    }
                    else if (board.ObstacleStateService != null &&
                             board.ObstacleStateService.IsMovableObstacleAt(x, y))
                    {
                        locked = true;
                    }
                }

                lockedMask[x, y] = locked;

                var tv = board.Tiles[x, y];
                currentTypes[x, y] = tv != null ? tv.GetTileType() : default;
            }
        }
    }

    private void ApplyShuffledTypes(TileType[,] finalTypes, bool[,] lockedMask)
    {
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (lockedMask[x, y]) continue;

                var tile = board.Tiles[x, y];
                if (tile == null) continue;

                tile.SetType(finalTypes[x, y]);
                board.SyncTileData(x, y);
                board.RefreshTileObstacleVisual(tile);
            }
        }
    }
}