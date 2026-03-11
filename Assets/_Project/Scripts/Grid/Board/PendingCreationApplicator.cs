using System.Collections.Generic;
using UnityEngine;

public sealed class PendingCreationApplicator
{
    private readonly BoardController board;

    public PendingCreationApplicator(BoardController board)
    {
        this.board = board;
    }

    public void ApplyAll(IReadOnlyList<PendingCreationStore.PendingCreation> pendingItems)
    {
        if (pendingItems == null || pendingItems.Count == 0)
            return;

        for (int i = 0; i < pendingItems.Count; i++)
        {
            ApplyOne(pendingItems[i]);
        }
    }

    public void ApplyOne(PendingCreationStore.PendingCreation pending)
    {
        var targetTile = board.Tiles[pending.x, pending.y];

        if (targetTile == null)
        {
            targetTile = FindAndDropTile(pending.x, pending.y);
            if (targetTile == null)
                targetTile = SpawnTileAt(pending.x, pending.y);
        }

        if (targetTile == null)
            return;

        targetTile.SetSpecial(pending.special);

        if (pending.special == TileSpecial.SystemOverride)
            targetTile.SetOverrideBaseType(targetTile.GetTileType());

        board.SyncTileData(targetTile.X, targetTile.Y);
        board.RefreshTileObstacleVisual(targetTile);
    }

    private TileView FindAndDropTile(int x, int y)
    {
        for (int yy = y - 1; yy >= 0; yy--)
        {
            if (board.Holes[x, yy]) continue;

            var tile = board.Tiles[x, yy];
            if (tile == null) continue;

            TeleportTile(tile, x, y);
            return tile;
        }

        return null;
    }

    private TileView SpawnTileAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return null;
        if (board.Holes[x, y]) return null;
        if (board.Tiles[x, y] != null) return board.Tiles[x, y];

        var go = Object.Instantiate(board.TilePrefab, board.Parent);
        var tile = go.GetComponent<TileView>();

        if (tile == null)
        {
            Object.Destroy(go);
            return null;
        }

        tile.Init(board, x, y);
        tile.SetCoords(x, y);
        tile.SnapToGrid(board.TileSize);

        board.Tiles[x, y] = tile;

        var pool = board.RandomPool;
        if (pool != null && pool.Length > 0)
            tile.SetType(pool[Random.Range(0, pool.Length)]);

        tile.SetSpecial(TileSpecial.None);

        board.SyncTileData(x, y);
        board.RefreshTileObstacleVisual(tile);

        return tile;
    }

    private void TeleportTile(TileView tile, int targetX, int targetY)
    {
        if (tile == null) return;
        if (targetX < 0 || targetX >= board.Width || targetY < 0 || targetY >= board.Height) return;
        if (board.Holes[targetX, targetY]) return;

        var targetTile = board.Tiles[targetX, targetY];
        int sourceX = tile.X;
        int sourceY = tile.Y;

        board.Tiles[sourceX, sourceY] = targetTile;
        if (targetTile != null)
        {
            targetTile.SetCoords(sourceX, sourceY);
            targetTile.SnapToGrid(board.TileSize);
        }

        board.Tiles[targetX, targetY] = tile;
        tile.SetCoords(targetX, targetY);
        tile.SnapToGrid(board.TileSize);

        board.SyncTileData(sourceX, sourceY);
        board.SyncTileData(targetX, targetY);
    }
}