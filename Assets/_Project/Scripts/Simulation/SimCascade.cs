/// <summary>
/// Headless yerçekimi + refill.
///
/// Eski sürüm her "segmenti" kendi tepesinden dolduruyordu → obstacle'la mühürlenmiş cepler
/// bile sonsuz taş alıyordu (oyun olduğundan kolay görünüyordu). Yeni sürüm canlı oyunun
/// kurallarını izler:
///   • Taş yalnız ÜST KENARA bağlı hücrelere spawn olur (arada obstacle varsa spawn yok;
///     level şekli delikleri geçirgendir — CascadeLogic.IsSpawnPassThroughCell ile aynı).
///   • Düz düşemeyen taş diagonal kayar (CascadeLogic.TrySlide).
///   • holdsTile (Oil) olan hücredeki taş düşmez.
/// Adım adım çalışır (her adımda 1 hücre) — canlı akışla aynı sıralama.
/// </summary>
public static class SimCascade
{
    /// <summary>Canlı oyunla aynı: bir cascade geçişinde bir taş en fazla bu kadar diagonal kayar.</summary>
    private const int MaxDiagonalSlidesPerCascade = 3;

    /// <summary>Tahta durulana kadar yerçekimi + refill uygular. Spawn edilen taş sayısını döner.</summary>
    public static int Settle(SimState s, SimObstacleLayer obs, SimGoalSet goals, System.Random rng)
    {
        int spawned = 0;
        int guard = s.Width * s.Height * 4 + 16;   // sonsuz döngü emniyeti

        System.Array.Clear(s.DiagSlides, 0, s.DiagSlides.Length);
        bool movableSpawnedThisPass = false;

        while (guard-- > 0)
        {
            bool changed = false;

            if (obs != null && obs.ApplyGravityStep(s)) changed = true;
            if (ApplyTileGravityStep(s, obs)) changed = true;

            int made = Spawn(s, obs, goals, rng, ref movableSpawnedThisPass);
            spawned += made;
            if (made > 0) changed = true;

            if (!changed) break;
        }

        return spawned;
    }

    // ── Yerçekimi (tek adım) ─────────────────────────────────────────────────

    private static bool ApplyTileGravityStep(SimState s, SimObstacleLayer obs)
    {
        bool moved = false;

        for (int y = s.Height - 1; y >= 1; y--)
        {
            for (int x = 0; x < s.Width; x++)
            {
                if (!CanReceive(s, x, y)) continue;

                // 1) Düz düşüş
                if (TryMove(s, obs, x, y - 1, x, y)) { moved = true; continue; }

                // 2) Diagonal kayma — yalnız düz düşemeyen taş kayar
                if (TryDiagonal(s, obs, x - 1, y - 1, x, y)) { moved = true; continue; }
                if (TryDiagonal(s, obs, x + 1, y - 1, x, y)) { moved = true; }
            }
        }

        return moved;
    }

    private static bool TryDiagonal(SimState s, SimObstacleLayer obs, int fromX, int fromY, int toX, int toY)
    {
        if (!s.InBounds(fromX, fromY)) return false;

        // Kaynak düz düşebiliyorsa diagonal kaymaz.
        if (CanReceive(s, fromX, fromY + 1)) return false;

        // Aynı taş bir geçişte sınırsız kayamaz (BoardController.MaxDiagonalSlidesPerCascade).
        if (s.DiagSlides[fromX, fromY] >= MaxDiagonalSlidesPerCascade) return false;

        // KÖŞE KURALI (CascadeLogic.TrySlide): iki köşeden en az biri geçilebilir olmalı.
        // Mask deliği köşe olarak GEÇMEZ; bloklayan obstacle ancak allowDiagonal ise geçer.
        // Magnet gibi allowDiagonal=0 engellerin ALTI bu yüzden boş kalır — taş etrafından süzülemez.
        bool cornerA = IsDiagonalPassable(s, obs, fromX, toY);
        bool cornerB = IsDiagonalPassable(s, obs, toX, fromY);
        if (!cornerA && !cornerB) return false;

        byte slides = s.DiagSlides[fromX, fromY];
        if (!TryMove(s, obs, fromX, fromY, toX, toY)) return false;

        s.DiagSlides[fromX, fromY] = 0;
        s.DiagSlides[toX, toY] = (byte)(slides + 1);
        return true;
    }

    private static bool IsDiagonalPassable(SimState s, SimObstacleLayer obs, int x, int y)
    {
        if (!s.InBounds(x, y)) return false;
        if (s.PermanentHoles[x, y]) return false;                 // mask deliği köşe olmaz
        if (obs == null) return true;
        if (!obs.IsCellBlocked(x, y)) return true;                // bloklamayan obstacle geçilir
        return obs.IsDiagonalAllowedAt(x, y);
    }

    private static bool TryMove(SimState s, SimObstacleLayer obs, int fromX, int fromY, int toX, int toY)
    {
        if (!s.InBounds(fromX, fromY)) return false;
        var tile = s.Grid[fromX, fromY];
        if (tile == null) return false;
        if (s.Holes[fromX, fromY]) return false;
        if (obs != null && obs.HoldsTileAt(fromX, fromY)) return false;   // Oil: taş yerinde kalır

        s.Grid[toX, toY] = tile;
        s.Grid[fromX, fromY] = null;
        tile.SetCoords(toX, toY);

        s.Phantom[toX, toY] = s.Phantom[fromX, fromY];
        s.Phantom[fromX, fromY] = false;
        s.DiagSlides[toX, toY] = s.DiagSlides[fromX, fromY];
        s.DiagSlides[fromX, fromY] = 0;
        s.SpecialLocked[toX, toY] = s.SpecialLocked[fromX, fromY];
        s.SpecialLocked[fromX, fromY] = false;
        return true;
    }

    private static bool CanReceive(SimState s, int x, int y)
        => s.InBounds(x, y) && !s.Holes[x, y] && s.Grid[x, y] == null;

    // ── Spawn ────────────────────────────────────────────────────────────────

    private static int Spawn(SimState s, SimObstacleLayer obs, SimGoalSet goals,
        System.Random rng, ref bool movableSpawnedThisPass)
    {
        int spawned = 0;

        for (int x = 0; x < s.Width; x++)
        {
            int y = TopSpawnCell(s, x);
            if (y < 0) continue;

            // Taş yerine hedef movable obstacle doğabilir (plastic_red vb. — kırdıkça yenisi gelir).
            if (obs != null && goals != null)
            {
                var spawnedId = obs.TrySpawnMovableGoal(s, x, y, goals, movableSpawnedThisPass);
                if (spawnedId != ObstacleId.None)
                {
                    movableSpawnedThisPass = true;
                    spawned++;
                    continue;
                }
            }

            s.Grid[x, y] = s.Acquire(x, y, PickRefillColor(s, x, y, rng));
            s.Phantom[x, y] = s.SearchMode;   // aramada: oyuncunun göremeyeceği taş
            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// Refill rengi. Canlı oyunla (CascadeLogic.PickRefillType) aynı kural: 3'lü match serbest
    /// (cascade olsun), ama 4+ dizi ENGELLİ — yoksa refill bedava special yağdırır ve sim
    /// oyunu olduğundan kolay gösterir.
    /// </summary>
    private const int MaxRefillRun = 3;

    private static TileType PickRefillColor(SimState s, int x, int y, System.Random rng)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var candidate = s.RandomColor(rng);
            if (!WouldExtendRunTooFar(s, x, y, candidate)) return candidate;
        }
        return s.RandomColor(rng);
    }

    private static bool WouldExtendRunTooFar(SimState s, int x, int y, TileType candidate)
    {
        // Dikey: aşağıya doğru ardışık aynı renk (aşağısı bu noktada kesinleşmiş).
        int down = 0;
        for (int yy = y + 1; yy < s.Height && s.IsColor(x, yy, candidate); yy++) down++;
        if (down + 1 > MaxRefillRun) return true;

        // Yatay: sola doğru (sağ komşular henüz spawn olmadı).
        int left = 0;
        for (int xx = x - 1; xx >= 0 && s.IsColor(xx, y, candidate); xx--) left++;
        return left + 1 > MaxRefillRun;
    }

    /// <summary>
    /// Bu sütunda spawn'ın düşebileceği en üst boş hücre. Üstünde OBSTACLE varsa -1
    /// (level şekli delikleri geçirgendir, obstacle değil).
    /// </summary>
    private static int TopSpawnCell(SimState s, int x)
    {
        for (int y = 0; y < s.Height; y++)
        {
            if (s.Holes[x, y])
            {
                // Obstacle bloğu → bu sütunun altı spawn almaz.
                if (!s.PermanentHoles[x, y]) return -1;
                continue;   // level deliği: taş içinden geçer
            }

            return s.Grid[x, y] == null ? y : -1;   // ilk oynanabilir hücre doluysa spawn yeri yok
        }

        return -1;
    }
}
