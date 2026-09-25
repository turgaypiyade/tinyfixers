using System.Collections.Generic;

/// <summary>
/// Simülasyonun saf C# board state'i — hiçbir Unity sahne nesnesi yok.
///
/// Önceki sürümden farklar:
///  • Palette: level.randomPool'u kullanır (sabit 4 renk DEĞİL) — renk sayısı zorluğun en büyük çarpanı.
///  • Phantom: arama (lookahead) modunda üretilen "oyuncunun göremeyeceği" taşlar eşleşmez.
///  • TileData havuzu: CopyFrom/clear/refill döngüsü GC üretmez → binlerce oyun paralel koşabilir.
///  • CopyFrom: arama dalları için allocation'sız derin kopya.
/// </summary>
public sealed class SimState
{
    public readonly int Width;
    public readonly int Height;

    /// <summary>Grid[x,y] == null → hücre boş (taş temizlendi / henüz düşmedi).</summary>
    public readonly TileData[,] Grid;

    /// <summary>Holes[x,y] == true → taş tutamaz (level deliği VEYA hücreyi bloklayan obstacle).</summary>
    public readonly bool[,] Holes;

    /// <summary>Level şeklinden gelen KALICI delikler. Obstacle kırılınca Holes buradan geri kurulur.</summary>
    public readonly bool[,] PermanentHoles;

    /// <summary>
    /// Arama modunda spawn edilen taş: oyuncu bu taşı göremez, bu yüzden eşleşmez.
    /// Böylece bot "rastgele refill patladı" diye sahte puan kazanmaz.
    /// </summary>
    public readonly bool[,] Phantom;

    /// <summary>Bu tahtada kullanılan taş renkleri (level.randomPool).</summary>
    public readonly TileType[] Palette;

    public readonly ISimObstacleQuery Obstacles;

    /// <summary>Refill artık phantom taş üretsin mi? (arama dalları true)</summary>
    public bool SearchMode;

    /// <summary>
    /// Bir taşın bu cascade geçişinde kaç kez diagonal kaydığı. Canlı oyun bunu
    /// MaxDiagonalSlidesPerCascade ile sınırlar (sahne değeri 3) — sınırsız kayma
    /// taşların engellerin etrafından süzülmesine ve tahtanın olduğundan akışkan
    /// görünmesine yol açardı.
    /// </summary>
    public readonly byte[,] DiagSlides;

    /// <summary>
    /// Magnet tarafından KAFESLENMİŞ special. Oyuncu onu takas edemez (elinden gitmiştir);
    /// yalnız düşmeye devam eder ve bir AoE içine girerse patlar.
    /// Bayrak hücrede tutulur ama taşla birlikte taşınır (Phantom gibi).
    /// </summary>
    public readonly bool[,] SpecialLocked;

    private readonly Stack<TileData> _pool = new();

    public SimState(int width, int height, TileType[] palette, ISimObstacleQuery obstacles)
    {
        Width          = width;
        Height         = height;
        Palette        = palette != null && palette.Length > 0 ? palette : DefaultPalette;
        Obstacles      = obstacles;
        Grid           = new TileData[width, height];
        Holes          = new bool[width, height];
        PermanentHoles = new bool[width, height];
        Phantom        = new bool[width, height];
        DiagSlides     = new byte[width, height];
        SpecialLocked  = new bool[width, height];
    }

    /// <summary>Canlı board'dan alınan anlık görüntüden state kurar (SimFidelityTest).</summary>
    public static SimState FromSnapshot(int width, int height, TileData[,] grid, bool[,] holes,
        ISimObstacleQuery obstacles)
    {
        var s = new SimState(width, height, DefaultPalette, obstacles);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                s.Holes[x, y] = holes[x, y];
                s.PermanentHoles[x, y] = holes[x, y];
                s.Grid[x, y] = grid[x, y];
            }
        return s;
    }

    public static readonly TileType[] DefaultPalette =
        { TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate };

    // ── Tile havuzu ──────────────────────────────────────────────────────────

    public TileData Acquire(int x, int y, TileType type)
    {
        if (_pool.Count > 0)
        {
            var t = _pool.Pop();
            t.SetCoords(x, y);
            t.SetType(type);
            t.ClearSpecial();
            return t;
        }
        return new TileData(x, y, type);
    }

    public void Release(TileData tile)
    {
        if (tile == null) return;
        _pool.Push(tile);
    }

    /// <summary>Hücreyi boşaltır ve taşı havuza iade eder.</summary>
    public void ClearCell(int x, int y)
    {
        var t = Grid[x, y];
        if (t == null) return;
        Grid[x, y] = null;
        Phantom[x, y] = false;
        SpecialLocked[x, y] = false;
        Release(t);
    }

    public TileType RandomColor(System.Random rng) => Palette[rng.Next(Palette.Length)];

    // ── Sorgular ─────────────────────────────────────────────────────────────

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsPlayable(int x, int y) => InBounds(x, y) && !Holes[x, y];

    /// <summary>Eşleşmeye katılabilir normal taş mı? (phantom ve special hariç)</summary>
    public bool IsMatchable(int x, int y)
    {
        if (!IsPlayable(x, y) || Phantom[x, y]) return false;
        if (Obstacles != null && (Obstacles.IsMovableObstacleAt(x, y)
            || Obstacles.IsInteractionLockedAt(x, y))) return false;
        var t = Grid[x, y];
        return t != null && t.Special == TileSpecial.None;
    }

    public bool IsColor(int x, int y, TileType color)
        => IsMatchable(x, y) && Grid[x, y].Type == color;

    // ── Derin kopya (arama dalları için, allocation'sız) ──────────────────────

    /// <summary>
    /// Bu state'i <paramref name="src"/>'nin birebir kopyası yapar. Hedefin mevcut TileData
    /// nesneleri yeniden kullanılır — arama sırasında sıfır GC.
    /// </summary>
    public void CopyFrom(SimState src)
    {
        System.Array.Copy(src.Holes,          Holes,          Holes.Length);
        System.Array.Copy(src.PermanentHoles, PermanentHoles, PermanentHoles.Length);
        System.Array.Copy(src.Phantom,        Phantom,        Phantom.Length);
        System.Array.Clear(DiagSlides, 0, DiagSlides.Length);
        System.Array.Copy(src.SpecialLocked, SpecialLocked, SpecialLocked.Length);
        SearchMode = src.SearchMode;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var s = src.Grid[x, y];
                var d = Grid[x, y];

                if (s == null)
                {
                    if (d != null) { Grid[x, y] = null; Release(d); }
                    continue;
                }

                if (d == null)
                {
                    d = Acquire(x, y, s.Type);
                    Grid[x, y] = d;
                }
                else
                {
                    d.SetCoords(x, y);
                    d.SetType(s.Type);
                    d.ClearSpecial();
                }

                if (s.Special != TileSpecial.None)
                {
                    d.SetSpecial(s.Special);
                    if (s.HasOverrideBaseType) d.SetOverrideBaseType(s.OverrideBaseType);
                }
            }
        }
    }

    // ── İlk tahta kurulumu ───────────────────────────────────────────────────

    /// <summary>
    /// Canlı oyunun BoardInitService.SimulateInitialTypes davranışını taklit eder:
    /// HAZIR EŞLEŞME YOK + en az bir oynanabilir hamle var. (Eski sim rastgele doldurup
    /// ilk cascade'i bedava hediye ediyordu → win% şişiyordu.)
    /// </summary>
    public void FillInitial(SimLevel level, System.Random rng)
    {
        const int maxAttempts = 96;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (Grid[x, y] != null) ClearCell(x, y);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (Holes[x, y]) continue;
                    Grid[x, y] = Acquire(x, y, PickTypeAvoidingMatch(x, y, rng));
                }

            ApplyPinned(level);

            if (!HasImmediateMatch() && HasAnyPlayableMove())
                return;
        }
        // 96 denemede temiz tahta çıkmadıysa son hâliyle devam — canlı oyun da öyle yapıyor.
        ApplyPinned(level);
    }

    /// <summary>Levelda sabitlenmiş taş tipi / special varsa uygula (GridSpawner ile aynı).</summary>
    private void ApplyPinned(SimLevel level)
    {
        if (level.pinnedTileTypes == null && level.pinnedSpecialTypes == null) return;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var tile = Grid[x, y];
                if (tile == null) continue;
                int idx = level.Index(x, y);

                if (level.pinnedTileTypes != null && idx < level.pinnedTileTypes.Length)
                {
                    int v = level.pinnedTileTypes[idx];
                    if (v > 0) tile.SetType((TileType)(v - 1));
                }

                if (level.pinnedSpecialTypes != null && idx < level.pinnedSpecialTypes.Length)
                {
                    var sp = (TileSpecial)level.pinnedSpecialTypes[idx];
                    if (sp != TileSpecial.None) tile.SetSpecial(sp);
                }
            }
        }
    }

    private TileType PickTypeAvoidingMatch(int x, int y, System.Random rng)
    {
        int start = rng.Next(Palette.Length);
        for (int i = 0; i < Palette.Length; i++)
        {
            var candidate = Palette[(start + i) % Palette.Length];
            if (!CreatesRunAt(x, y, candidate)) return candidate;
        }
        return Palette[start];
    }

    // Sadece SOLA ve YUKARI bakar — sağ/alt henüz doldurulmadı.
    private bool CreatesRunAt(int x, int y, TileType t)
    {
        int left = 0;
        for (int i = x - 1; i >= 0 && IsColor(i, y, t); i--) left++;
        if (left >= 2) return true;

        int up = 0;
        for (int i = y - 1; i >= 0 && IsColor(x, i, t); i--) up++;
        return up >= 2;
    }

    public bool HasImmediateMatch()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsMatchable(x, y)) continue;
                var t = Grid[x, y].Type;
                if (x >= 2 && IsColor(x - 1, y, t) && IsColor(x - 2, y, t)) return true;
                if (y >= 2 && IsColor(x, y - 1, t) && IsColor(x, y - 2, t)) return true;
                // 2x2
                if (x >= 1 && y >= 1 && IsColor(x - 1, y, t) && IsColor(x, y - 1, t) && IsColor(x - 1, y - 1, t))
                    return true;
            }
        }
        return false;
    }

    public bool HasAnyPlayableMove()
    {
        var buf = new List<SimSwap>(8);
        SimMoves.FindValid(this, buf, stopAfterFirst: true);
        return buf.Count > 0;
    }

    /// <summary>
    /// Canlı oyunun shuffle'ı: mevcut taşları karıştırır, hazır eşleşme bırakmaz,
    /// oynanabilir hamle garantiler. Deadlock'ta çağrılır.
    /// </summary>
    public bool Shuffle(System.Random rng, int maxAttempts = 24)
    {
        var colors = new List<TileType>();
        var cells  = new List<(int x, int y)>();

        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var t = Grid[x, y];
                if (t == null || t.Special != TileSpecial.None) continue;   // special'lar yerinde kalır
                colors.Add(t.Type);
                cells.Add((x, y));
            }

        if (cells.Count < 3) return false;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (colors[i], colors[j]) = (colors[j], colors[i]);
            }

            for (int i = 0; i < cells.Count; i++)
                Grid[cells[i].x, cells[i].y].SetType(colors[i]);

            if (!HasImmediateMatch() && HasAnyPlayableMove())
                return true;
        }

        return HasAnyPlayableMove();
    }
}
