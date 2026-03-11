using System.Collections.Generic;
using UnityEngine;

public sealed class SpecialCreationService
{
    public readonly struct CreationRequest
    {
        public readonly TileView swapA;
        public readonly TileView swapB;
        public readonly bool preferSwapTiles;

        public CreationRequest(TileView swapA, TileView swapB, bool preferSwapTiles)
        {
            this.swapA = swapA;
            this.swapB = swapB;
            this.preferSwapTiles = preferSwapTiles;
        }
    }

    public readonly struct CreationResult
    {
        public readonly bool hasValue;
        public readonly TileView winner;
        public readonly TileSpecial special;

        public CreationResult(TileView winner, TileSpecial special)
        {
            this.hasValue = winner != null && special != TileSpecial.None;
            this.winner = winner;
            this.special = special;
        }

        public static CreationResult None => new CreationResult(null, TileSpecial.None);
    }

    private readonly MatchFinder matchFinder;

    public SpecialCreationService(MatchFinder matchFinder)
    {
        this.matchFinder = matchFinder;
    }

    public CreationResult DecideFromMatches(HashSet<TileView> matches, CreationRequest request)
    {
        if (matches == null || matches.Count == 0)
            return CreationResult.None;

        if (request.preferSwapTiles && request.swapA != null && request.swapB != null)
        {
            var swapPreferred = DecideFromSwapTiles(matches, request.swapA, request.swapB);
            if (swapPreferred.hasValue)
                return swapPreferred;
        }

        return DecideBestFromMatchedTiles(matches);
    }

    public CreationResult DecideFromSwapTiles(HashSet<TileView> matches, TileView a, TileView b)
    {
        if (matches == null || matches.Count == 0)
            return CreationResult.None;

        TileView bestTile = null;
        TileSpecial bestSpecial = TileSpecial.None;
        int bestScore = 0;

        if (a != null && matches.Contains(a) && a.GetSpecial() == TileSpecial.None)
        {
            TileSpecial aSpec = matchFinder.DecideSpecialAt(a.X, a.Y);
            int aScore = Score(aSpec);

            if (aScore > bestScore)
            {
                bestScore = aScore;
                bestSpecial = aSpec;
                bestTile = a;
            }
        }

        if (b != null && matches.Contains(b) && b.GetSpecial() == TileSpecial.None)
        {
            TileSpecial bSpec = matchFinder.DecideSpecialAt(b.X, b.Y);
            int bScore = Score(bSpec);

            if (bScore > bestScore)
            {
                bestScore = bScore;
                bestSpecial = bSpec;
                bestTile = b;
            }
        }

        if (bestTile == null || bestSpecial == TileSpecial.None)
            return CreationResult.None;

        return new CreationResult(bestTile, bestSpecial);
    }

    public CreationResult DecideBestFromMatchedTiles(HashSet<TileView> matches)
    {
        if (matches == null || matches.Count == 0)
            return CreationResult.None;

        TileView bestTile = null;
        TileSpecial bestSpecial = TileSpecial.None;
        int bestScore = 0;

        foreach (var tile in matches)
        {
            if (tile == null) continue;
            if (tile.GetSpecial() != TileSpecial.None) continue;

            TileSpecial candidate = matchFinder.DecideSpecialAt(tile.X, tile.Y);
            int score = Score(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                bestSpecial = candidate;
                bestTile = tile;
            }
        }

        if (bestTile == null || bestSpecial == TileSpecial.None)
            return CreationResult.None;

        return new CreationResult(bestTile, bestSpecial);
    }
    public int Score(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.SystemOverride: return 60;
            case TileSpecial.PulseCore:      return 50;
            case TileSpecial.LineH:
            case TileSpecial.LineV:          return 30;
            case TileSpecial.PatchBot:       return 20;
            default:                         return 0;
        }
    }
}