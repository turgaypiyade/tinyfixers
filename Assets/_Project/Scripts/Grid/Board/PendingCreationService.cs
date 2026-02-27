using System.Collections.Generic;
using UnityEngine;

public class PendingCreationService
{
    readonly struct PendingCreation
    {
        public readonly int x;
        public readonly int y;
        public readonly TileSpecial special;

        public PendingCreation(int x, int y, TileSpecial special)
        {
            this.x = x;
            this.y = y;
            this.special = special;
        }
    }

    private readonly BoardController board;
    private readonly MatchFinder matchFinder;
    private readonly SpecialResolver specialResolver;
    private readonly List<PendingCreation> pendingCreations = new();

    public PendingCreationService(BoardController board, MatchFinder matchFinder, SpecialResolver specialResolver)
    {
        this.board = board;
        this.matchFinder = matchFinder;
        this.specialResolver = specialResolver;
    }

    public bool HasPending => pendingCreations.Count > 0;
    public (int x, int y, TileSpecial special) LastCaptured { get; private set; }

    public void Clear()
    {
        pendingCreations.Clear();
        LastCaptured = default;
    }

    public bool CapturePendingCreation(TileView a, TileView b)
    {
        var candidates = new HashSet<TileView>();
        foreach (var t in matchFinder.FindMatchesAt(a.X, a.Y)) candidates.Add(t);
        foreach (var t in matchFinder.FindMatchesAt(b.X, b.Y)) candidates.Add(t);
        matchFinder.Add2x2Candidates(candidates, a.X, a.Y);
        matchFinder.Add2x2Candidates(candidates, b.X, b.Y);

        TileSpecial aSpec = matchFinder.DecideSpecialAt(a.X, a.Y);
        TileSpecial bSpec = matchFinder.DecideSpecialAt(b.X, b.Y);
        var (winner, wSpec) = specialResolver.PickWinner(a, aSpec, b, bSpec);

        if (winner != null && wSpec != TileSpecial.None)
        {
            StorePending(winner.X, winner.Y, wSpec);
            return true;
        }

        TileView bestTile = null;
        TileSpecial bestSpec = TileSpecial.None;
        int bestScore = 0;

        foreach (var t in candidates)
        {
            if (t == null) continue;
            var spec = matchFinder.DecideSpecialAt(t.X, t.Y);
            int score = specialResolver.SpecialScore(spec);
            if (score > bestScore)
            {
                bestScore = score;
                bestSpec = spec;
                bestTile = t;
            }
        }

        if (bestTile == null || bestSpec == TileSpecial.None)
            return false;

        StorePending(bestTile.X, bestTile.Y, bestSpec);
        return true;
    }

    void StorePending(int x, int y, TileSpecial special)
    {
        var pending = new PendingCreation(x, y, special);
        pendingCreations.Add(pending);
        LastCaptured = (pending.x, pending.y, pending.special);
    }

    public void ApplyPendingCreations()
    {
        foreach (var pending in pendingCreations)
        {
            var targetTile = board.Tiles[pending.x, pending.y];
            if (targetTile == null)
            {
                targetTile = FindAndDropTile(pending.x, pending.y);
            }

            if (targetTile == null) continue;

            targetTile.SetSpecial(pending.special);
            if (pending.special == TileSpecial.SystemOverride)
                targetTile.SetOverrideBaseType(targetTile.GetTileType());

        }

        pendingCreations.Clear();
    }

    TileView FindAndDropTile(int x, int y)
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

    void TeleportTile(TileView tile, int targetX, int targetY)
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
    }
}
