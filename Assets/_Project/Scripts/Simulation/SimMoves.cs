using System.Collections.Generic;

public readonly struct SimSwap
{
    public readonly int AX, AY, BX, BY;
    public SimSwap(int ax, int ay, int bx, int by) { AX = ax; AY = ay; BX = bx; BY = by; }

    public bool Horizontal => AY == BY && AX != BX;
    public bool IsTap => AX == BX && AY == BY;
    public static SimSwap Tap(int x, int y) => new(x, y, x, y);
    public override string ToString() => IsTap ? $"tap({AX},{AY})" : $"({AX},{AY})<->({BX},{BY})";
}

/// <summary>
/// Geçerli komşu takasları üretir.
/// Geçerli takas: (a) taraflardan biri special, VEYA (b) 3'lü/2x2 eşleşme oluşturur.
///
/// NOT: Eski sürüm paylaşılan STATIC bir listeye yazıyordu — lookahead sırasında iç içe
/// çağrı olunca liste altından kayıyordu ve paralel çalıştırma imkânsızdı. Artık çağıran
/// kendi buffer'ını verir.
/// </summary>
public static class SimMoves
{
    public static void FindValid(SimState s, List<SimSwap> into, bool stopAfterFirst = false)
    {
        into.Clear();

        for (int y = 0; y < s.Height; y++)
        {
            for (int x = 0; x < s.Width; x++)
            {
                if (!s.IsPlayable(x, y) || s.Grid[x, y] == null) continue;

                if (CanTap(s, x, y))
                {
                    into.Add(SimSwap.Tap(x, y));
                    if (stopAfterFirst) return;
                }

                if (x + 1 < s.Width && s.IsPlayable(x + 1, y) && s.Grid[x + 1, y] != null &&
                    IsValidSwap(s, x, y, x + 1, y))
                {
                    into.Add(new SimSwap(x, y, x + 1, y));
                    if (stopAfterFirst) return;
                }

                if (y + 1 < s.Height && s.IsPlayable(x, y + 1) && s.Grid[x, y + 1] != null &&
                    IsValidSwap(s, x, y, x, y + 1))
                {
                    into.Add(new SimSwap(x, y, x, y + 1));
                    if (stopAfterFirst) return;
                }
            }
        }
    }

    /// <summary>Takası uygular (Grid'i yerinde değiştirir).</summary>
    public static void Apply(SimState s, SimSwap swap)
    {
        if (swap.IsTap) return;
        var a = s.Grid[swap.AX, swap.AY];
        var b = s.Grid[swap.BX, swap.BY];
        s.Grid[swap.AX, swap.AY] = b;
        s.Grid[swap.BX, swap.BY] = a;
        a?.SetCoords(swap.BX, swap.BY);
        b?.SetCoords(swap.AX, swap.AY);

        (s.Phantom[swap.AX, swap.AY], s.Phantom[swap.BX, swap.BY]) =
            (s.Phantom[swap.BX, swap.BY], s.Phantom[swap.AX, swap.AY]);

        (s.SpecialLocked[swap.AX, swap.AY], s.SpecialLocked[swap.BX, swap.BY]) =
            (s.SpecialLocked[swap.BX, swap.BY], s.SpecialLocked[swap.AX, swap.AY]);
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    public static bool CanTap(SimState s, int x, int y)
        => s.IsPlayable(x, y) && s.Grid[x, y] != null
        && s.Grid[x, y].Special != TileSpecial.None
        && !s.Phantom[x, y] && !s.SpecialLocked[x, y]
        && (s.Obstacles == null || !s.Obstacles.IsInteractionLockedAt(x, y));

    public static bool IsLegal(SimState s, SimSwap move)
    {
        if (move.IsTap) return CanTap(s, move.AX, move.AY);
        if (System.Math.Abs(move.AX - move.BX) + System.Math.Abs(move.AY - move.BY) != 1)
            return false;
        return s.IsPlayable(move.AX, move.AY) && s.IsPlayable(move.BX, move.BY)
            && s.Grid[move.AX, move.AY] != null && s.Grid[move.BX, move.BY] != null
            && IsValidSwap(s, move.AX, move.AY, move.BX, move.BY);
    }

    private static bool IsValidSwap(SimState s, int ax, int ay, int bx, int by)
    {
        if (s.Obstacles != null)
        {
            if (s.Obstacles.IsInteractionLockedAt(ax, ay)) return false;
            if (s.Obstacles.IsInteractionLockedAt(bx, by)) return false;
            if (s.Obstacles.IsMovableObstacleAt(ax, ay)) return false;
            if (s.Obstacles.IsMovableObstacleAt(bx, by)) return false;
        }

        var a = s.Grid[ax, ay];
        var b = s.Grid[bx, by];

        // Phantom (arama modunda üretilmiş, oyuncunun göremeyeceği) taş oynanmaz.
        if (s.Phantom[ax, ay] || s.Phantom[bx, by]) return false;

        // Magnet'in kafeslediği special oyuncunun elinden çıkmıştır — takas edilemez.
        if (s.SpecialLocked[ax, ay] || s.SpecialLocked[bx, by]) return false;

        bool aSpecial = a.Special != TileSpecial.None;
        bool bSpecial = b.Special != TileSpecial.None;
        if (aSpecial || bSpecial) return true;   // special + herhangi bir şey = her zaman oynanır

        return WouldCreateMatch(s, ax, ay, bx, by);
    }

    private static bool WouldCreateMatch(SimState s, int ax, int ay, int bx, int by)
    {
        var swap = new SimSwap(ax, ay, bx, by);
        Apply(s, swap);
        bool match = CheckRunAt(s, ax, ay) || CheckRunAt(s, bx, by);
        Apply(s, swap);   // geri al
        return match;
    }

    private static bool CheckRunAt(SimState s, int x, int y)
    {
        if (!s.IsMatchable(x, y)) return false;
        var t = s.Grid[x, y].Type;

        int count = 1;
        for (int lx = x - 1; lx >= 0 && s.IsColor(lx, y, t); lx--) count++;
        for (int rx = x + 1; rx < s.Width && s.IsColor(rx, y, t); rx++) count++;
        if (count >= 3) return true;

        count = 1;
        for (int uy = y - 1; uy >= 0 && s.IsColor(x, uy, t); uy--) count++;
        for (int dy = y + 1; dy < s.Height && s.IsColor(x, dy, t); dy++) count++;
        if (count >= 3) return true;

        // 2x2
        for (int ox = -1; ox <= 0; ox++)
            for (int oy = -1; oy <= 0; oy++)
                if (s.IsColor(x + ox, y + oy, t) && s.IsColor(x + ox + 1, y + oy, t) &&
                    s.IsColor(x + ox, y + oy + 1, t) && s.IsColor(x + ox + 1, y + oy + 1, t))
                    return true;

        return false;
    }
}
