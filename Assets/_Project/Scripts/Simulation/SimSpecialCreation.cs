using System.Collections.Generic;

/// <summary>
/// SpecialCreationService's two-creation policy over TileData. Selection is completed
/// before mutating the board, so the first creation cannot change the second's geometry.
/// </summary>
public static class SimSpecialCreation
{
    public readonly struct Creation
    {
        public readonly TileData Tile;
        public readonly TileSpecial Special;
        public Creation(TileData tile, TileSpecial special) { Tile = tile; Special = special; }
    }

    public static int Rank(TileSpecial special) => special switch
    {
        TileSpecial.SystemOverride => 60,
        TileSpecial.PulseCore => 50,
        TileSpecial.LineH => 30,
        TileSpecial.LineV => 30,
        TileSpecial.PatchBot => 20,
        _ => 0
    };

    public static void Select(SimState state, SimMatchFinder finder, HashSet<TileData> matches,
        SimSwap? swap, List<Creation> into)
    {
        into.Clear();
        var working = new HashSet<TileData>(matches);
        working.RemoveWhere(t => t == null || t.Special != TileSpecial.None);
        var first = Best(finder, working);

        if (swap.HasValue && !swap.Value.IsTap)
        {
            var sw = swap.Value;
            // Live swapA is now at B; equal-ranked choices prefer that endpoint.
            var preferred = Candidate(finder, working, state.Grid[sw.BX, sw.BY], sw.Horizontal);
            var other = Candidate(finder, working, state.Grid[sw.AX, sw.AY], sw.Horizontal);
            if (Rank(other.Special) > Rank(preferred.Special)) preferred = other;
            if (preferred.Tile != null && Rank(preferred.Special) >= Rank(first.Special)) first = preferred;
        }

        if (first.Tile == null) return;
        into.Add(first);
        working.ExceptWith(Consumed(state, working, first));
        if (working.Count < 3) return;
        var second = Best(finder, working);
        if (second.Tile != null && Consumed(state, working, second).Count > 0) into.Add(second);
    }

    private static Creation Best(SimMatchFinder finder, HashSet<TileData> matches)
    {
        var best = default(Creation);
        foreach (var tile in matches)
        {
            var candidate = Candidate(finder, matches, tile, null);
            if (Rank(candidate.Special) > Rank(best.Special)) best = candidate;
        }
        return best;
    }

    private static Creation Candidate(SimMatchFinder finder, HashSet<TileData> matches,
        TileData tile, bool? horizontal)
    {
        if (tile == null || !matches.Contains(tile) || tile.Special != TileSpecial.None) return default;
        var special = finder.DecideSpecialAt(tile.X, tile.Y, horizontal);
        return special == TileSpecial.None ? default : new Creation(tile, special);
    }

    private static HashSet<TileData> Consumed(SimState state, HashSet<TileData> matches, Creation creation)
    {
        var tile = creation.Tile;
        var horizontal = Run(state, matches, tile, 1, 0);
        var vertical = Run(state, matches, tile, 0, 1);
        switch (creation.Special)
        {
            case TileSpecial.PulseCore:
                horizontal.UnionWith(vertical);
                return horizontal;
            case TileSpecial.SystemOverride:
                if (horizontal.Count >= 5 && vertical.Count >= 5)
                {
                    horizontal.UnionWith(vertical);
                    return horizontal;
                }
                return horizontal.Count >= vertical.Count ? horizontal : vertical;
            case TileSpecial.LineH:
            case TileSpecial.LineV:
                return horizontal.Count >= vertical.Count ? horizontal : vertical;
            case TileSpecial.PatchBot:
                for (int ox = -1; ox <= 0; ox++)
                    for (int oy = -1; oy <= 0; oy++)
                    {
                        var square = new HashSet<TileData>();
                        for (int dx = 0; dx < 2; dx++)
                            for (int dy = 0; dy < 2; dy++)
                            {
                                int x = tile.X + ox + dx, y = tile.Y + oy + dy;
                                if (!state.InBounds(x, y)) continue;
                                var t = state.Grid[x, y];
                                if (t != null && matches.Contains(t) && t.Special == TileSpecial.None
                                    && t.Type == tile.Type) square.Add(t);
                            }
                        if (square.Count == 4) return square;
                    }
                break;
        }
        return new HashSet<TileData>();
    }

    private static HashSet<TileData> Run(SimState state, HashSet<TileData> matches,
        TileData center, int dx, int dy)
    {
        var result = new HashSet<TileData>();
        for (int sign = -1; sign <= 1; sign += 2)
            for (int step = sign < 0 ? 0 : 1; ; step++)
            {
                int x = center.X + dx * step * sign, y = center.Y + dy * step * sign;
                if (!state.InBounds(x, y)) break;
                var t = state.Grid[x, y];
                if (t == null || !matches.Contains(t) || t.Special != TileSpecial.None || t.Type != center.Type) break;
                result.Add(t);
            }
        return result;
    }
}
