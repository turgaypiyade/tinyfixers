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

    // ── Pooled buffers — zero GC per call ──
    private readonly Dictionary<Vector2Int, TileView> _cellMapBuffer = new Dictionary<Vector2Int, TileView>(32);
    private readonly HashSet<TileView> _consumedBuffer = new HashSet<TileView>();

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
    public List<CreationResult> DecideUpToTwoFromMatches(
        HashSet<TileView> matches,
        CreationRequest request)
    {
        var results = new List<CreationResult>(2);

        if (matches == null || matches.Count == 0)
            return results;

        var working = new HashSet<TileView>(matches);
        working.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);

        if (working.Count == 0)
            return results;

        var first = DecideFromMatches(working, request);
        if (!first.hasValue || first.winner == null)
            return results;

        results.Add(first);

        var firstConsumed = GetConsumedTilesForCreation(working, first);
        if (firstConsumed.Count > 0)
            working.ExceptWith(firstConsumed);
        else
            working.Remove(first.winner);

        if (working.Count < 3)
            return results;

        var second = DecideBestFromMatchedTiles(working);
        if (!second.hasValue || second.winner == null)
            return results;

        var secondConsumed = GetConsumedTilesForCreation(working, second);
        if (secondConsumed.Count == 0)
            return results;

        results.Add(second);
        return results;
    }
    public CreationResult DecideFromSwapTiles(HashSet<TileView> matches, TileView a, TileView b)
    {
        if (matches == null || matches.Count == 0)
            return CreationResult.None;

        TileView bestTile = null;
        TileSpecial bestSpecial = TileSpecial.None;
        int bestScore = 0;

        // Bu, oyuncunun SWAP'ı sonucu special oluşumu — parmak hareketinin yönü 4'lü
        // Line'ın yönünü belirler (yatay swap→LineH, dikey swap→LineV). Swap her zaman
        // ya aynı satır (yatay) ya aynı sütundur (dikey).
        bool? swapHorizontal = null;
        if (a != null && b != null)
        {
            if (a.Y == b.Y && a.X != b.X) swapHorizontal = true;       // yatay swap
            else if (a.X == b.X && a.Y != b.Y) swapHorizontal = false; // dikey swap
        }

        if (a != null && matches.Contains(a) && a.GetSpecial() == TileSpecial.None)
        {
            TileSpecial aSpec = matchFinder.DecideSpecialAt(a.X, a.Y, swapHorizontal);
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
            TileSpecial bSpec = matchFinder.DecideSpecialAt(b.X, b.Y, swapHorizontal);
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
    internal HashSet<TileView> GetConsumedTilesForCreation(HashSet<TileView> matches, CreationResult creation)
    {
        var result = new HashSet<TileView>();
        if (!creation.hasValue || creation.winner == null || matches == null || matches.Count == 0)
            return result;

        switch (creation.special)
        {
            case TileSpecial.LineH:
            case TileSpecial.LineV:
            {
                // Tüketilen tile'lar special TİPİNE değil, winner'ın bulunduğu asıl 4'lü
                // diziye göre. Special tipi swap yönüyle belirlenebildiği için (yatay
                // swap→LineH) tip, match geometrisinden farklı olabilir; tüketimi tipe
                // bağlarsak dik yöndeki dizi tüketilmeden kalıp İKİNCİ bir special'a
                // dönüşüyordu (yatay swap + dikey 4'lü → yanlışlıkla LineH+LineV).
                var hRun = CollectHorizontalRun(matches, creation.winner);
                var vRun = CollectVerticalRun(matches, creation.winner);
                return hRun.Count >= vRun.Count ? hRun : vRun;
            }

            case TileSpecial.PulseCore:
                {
                    var h = CollectHorizontalRun(matches, creation.winner);
                    var v = CollectVerticalRun(matches, creation.winner);
                    h.UnionWith(v);
                    return h;
                }

            case TileSpecial.SystemOverride:
                {
                    var h = CollectHorizontalRun(matches, creation.winner);
                    var v = CollectVerticalRun(matches, creation.winner);

                    if (h.Count >= 5 && v.Count >= 5)
                    {
                        h.UnionWith(v);
                        return h;
                    }

                    if (h.Count >= v.Count)
                        return h;

                    return v;
                }

            case TileSpecial.PatchBot:
                return CollectPatchBotSquare(matches, creation.winner);

            default:
                return result;
        }
    }

    private HashSet<TileView> CollectHorizontalRun(HashSet<TileView> matches, TileView center)
    {
        var result = new HashSet<TileView>();
        if (center == null)
            return result;

        var map = BuildCellMap(matches);
        var type = center.GetTileType();
        int y = center.Y;

        int x = center.X;
        while (map.TryGetValue(new Vector2Int(x, y), out var tile) &&
               tile != null &&
               tile.GetSpecial() == TileSpecial.None &&
               tile.GetTileType().Equals(type))
        {
            result.Add(tile);
            x--;
        }

        x = center.X + 1;
        while (map.TryGetValue(new Vector2Int(x, y), out var tile) &&
               tile != null &&
               tile.GetSpecial() == TileSpecial.None &&
               tile.GetTileType().Equals(type))
        {
            result.Add(tile);
            x++;
        }

        return result;
    }

    private HashSet<TileView> CollectVerticalRun(HashSet<TileView> matches, TileView center)
    {
        var result = new HashSet<TileView>();
        if (center == null)
            return result;

        var map = BuildCellMap(matches);
        var type = center.GetTileType();
        int x = center.X;

        int y = center.Y;
        while (map.TryGetValue(new Vector2Int(x, y), out var tile) &&
               tile != null &&
               tile.GetSpecial() == TileSpecial.None &&
               tile.GetTileType().Equals(type))
        {
            result.Add(tile);
            y--;
        }

        y = center.Y + 1;
        while (map.TryGetValue(new Vector2Int(x, y), out var tile) &&
               tile != null &&
               tile.GetSpecial() == TileSpecial.None &&
               tile.GetTileType().Equals(type))
        {
            result.Add(tile);
            y++;
        }

        return result;
    }

    private HashSet<TileView> CollectPatchBotSquare(HashSet<TileView> matches, TileView center)
    {
        var result = new HashSet<TileView>();
        if (center == null)
            return result;

        var map = BuildCellMap(matches);
        var type = center.GetTileType();

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int sx = center.X + ox;
                int sy = center.Y + oy;

                var p0 = new Vector2Int(sx, sy);
                var p1 = new Vector2Int(sx + 1, sy);
                var p2 = new Vector2Int(sx, sy + 1);
                var p3 = new Vector2Int(sx + 1, sy + 1);

                if (!map.TryGetValue(p0, out var a)) continue;
                if (!map.TryGetValue(p1, out var b)) continue;
                if (!map.TryGetValue(p2, out var c)) continue;
                if (!map.TryGetValue(p3, out var d)) continue;

                if (a == null || b == null || c == null || d == null)
                    continue;

                if (a.GetSpecial() != TileSpecial.None ||
                    b.GetSpecial() != TileSpecial.None ||
                    c.GetSpecial() != TileSpecial.None ||
                    d.GetSpecial() != TileSpecial.None)
                    continue;

                if (!a.GetTileType().Equals(type) ||
                    !b.GetTileType().Equals(type) ||
                    !c.GetTileType().Equals(type) ||
                    !d.GetTileType().Equals(type))
                    continue;

                result.Add(a);
                result.Add(b);
                result.Add(c);
                result.Add(d);
                return result;
            }
        }

        return result;
    }

    private Dictionary<Vector2Int, TileView> BuildCellMap(HashSet<TileView> matches)
    {
        _cellMapBuffer.Clear();
        foreach (var tile in matches)
        {
            if (tile == null)
                continue;

            _cellMapBuffer[new Vector2Int(tile.X, tile.Y)] = tile;
        }
        return _cellMapBuffer;
    }
    public int Score(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.SystemOverride: return 60;
            case TileSpecial.PulseCore: return 50;
            case TileSpecial.LineH:
            case TileSpecial.LineV: return 30;
            case TileSpecial.PatchBot: return 20;
            default: return 0;
        }
    }
}