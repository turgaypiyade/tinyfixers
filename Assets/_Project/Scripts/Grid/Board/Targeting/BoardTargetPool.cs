using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tahta başına TEK hedef havuzu — "neye vurulabilir" ve "kim neyi ayırdı" bilgisinin tek kaynağı.
///
/// Neden: hedefleyiciler (PatchBot ailesi, roket sepeti, Override+PatchBot, Line+PatchBot, yumurta kuşu)
/// kendi kopyalarını tutuyordu; her kural (kilitli/örtülü engel, kasanın gerçek kalan vuruşu, kargo, jel…)
/// her kopyaya ayrı taşınmak zorundaydı ve rezervasyonlar koordinatör ÖRNEĞİNE aitti (8 ayrı `new`) → aynı
/// anda uçan farklı kaynaklı botlar birbirinin hedefini görmüyordu.
///
/// Kurallar:
///  - UYGUNLUK tek yerde: <see cref="IsHittableObstacleCell"/>, <see cref="IsTargetableTileCell"/>,
///    <see cref="IsAnyHitTarget"/>. Yeni engel kuralı YALNIZ buraya eklenir.
///  - KAPASİTE engel (origin) bazında: kalan anlamlı vuruş − rezerve vuruş. 4x4 kasa tek hedeftir.
///  - REZERVASYON tahta geneli ve ortak. Seçim POLİTİKASI (öncelik+yoğunluk / rastgele) seçicinindir.
///  - Sızıntı koruması: bırakılmayan rezervasyon <see cref="ReservationTtlSeconds"/> sonra yok sayılır
///    (bot yok edilip Release çağrılmazsa kapasite kalıcı düşmesin).
/// </summary>
public sealed class BoardTargetPool
{
    private const float ReservationTtlSeconds = 8f;

    private readonly BoardController board;
    private readonly Dictionary<int, List<float>> obstacleReservations = new();   // origin → rezervasyon anları
    private readonly Dictionary<TileView, float> tileReservations = new();

    public BoardTargetPool(BoardController board)
    {
        this.board = board;
    }

    private static float Now => Time.unscaledTime;

    // ── Uygunluk ─────────────────────────────────────────────────

    /// Hücredeki engel bir vuruşu gerçekten TÜKETEBİLİR mi (rezervasyon hariç)? Kargo kırılmaz, jel kırılmaz,
    /// tüpün yalnız tabanı hedeftir, örtülü (hit-locked) ve pasif aşamadaki engel vuruş almaz.
    public bool IsHittableObstacleCell(int x, int y)
    {
        var obs = board.ObstacleStateService;
        if (obs == null || x < 0 || y < 0 || x >= board.Width || y >= board.Height) return false;

        var id = obs.GetObstacleIdAt(x, y);
        // Hamster bir karakter: hedeflenmez (PatchBot onu "kırmaya" gitmez; hamster da kendini hedef seçmez).
        if (id == ObstacleId.None || id == ObstacleId.SpreadingGel || id == ObstacleId.Hamster) return false;
        if (obs.IsExitAtBottomAt(x, y)) return false;
        if (id == ObstacleId.Tube)
        {
            int origin = obs.GetObstacleOriginAt(x, y);
            if (origin < 0 || origin % board.Width != x || origin / board.Width != y) return false;
        }
        if (obs.IsHitLockedAt(x, y)) return false;
        if (obs.IsFullyDisabledAt(x, y)) return false;
        return obs.GetActiveMeaningfulHitsAt(x, y) > 0;
    }

    /// Hücrede hedeflenebilir (vurulunca temizlenecek) bir taş var mı?
    public bool IsTargetableTileCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= board.Width || y >= board.Height) return false;
        var tile = board.Tiles[x, y];
        return tile != null
               && board.GridData[x, y] != null
               && SpecialUtils.CanTargetTileContent(board, x, y);
    }

    /// Tek atışlık / rastgele hedefleyiciler için (ör. yumurta kuşu): bu hücreye inen vuruş bir şey yapar mı?
    /// Vurulabilir engel → evet. Vurulamayan engel (kargo, örtülü, pasif) → hayır. Engel yok ya da jel (taş
    /// altında kalan, kırılmaz örtü) → üstündeki taş hedeflenebiliyorsa evet.
    public bool IsAnyHitTarget(int x, int y)
    {
        if (IsHittableObstacleCell(x, y)) return true;
        var obs = board.ObstacleStateService;
        if (obs != null)
        {
            var id = obs.GetObstacleIdAt(x, y);
            if (id != ObstacleId.None && id != ObstacleId.SpreadingGel) return false;
        }
        return IsTargetableTileCell(x, y);
    }

    /// Engelin (origin) kalan KAPASİTESİ: anlamlı vuruş − ortak rezervasyonlar. Vurulamıyorsa 0.
    public int ObstacleCapacityAt(int x, int y)
    {
        if (!IsHittableObstacleCell(x, y)) return 0;
        var obs = board.ObstacleStateService;
        int origin = obs.GetObstacleOriginAt(x, y);
        int actual = obs.GetActiveMeaningfulHitsAt(x, y);
        return origin < 0 ? actual : actual - ReservedObstacleHits(origin);
    }

    // ── Rezervasyonlar (tahta geneli) ────────────────────────────

    public void ReserveObstacle(int origin)
    {
        if (origin < 0) return;
        if (!obstacleReservations.TryGetValue(origin, out var list))
            obstacleReservations[origin] = list = new List<float>();
        list.Add(Now);
    }

    public void ReleaseObstacle(int origin)
    {
        if (origin < 0 || !obstacleReservations.TryGetValue(origin, out var list)) return;
        if (list.Count > 0) list.RemoveAt(0);
        if (list.Count == 0) obstacleReservations.Remove(origin);
    }

    public int ReservedObstacleHits(int origin)
    {
        if (!obstacleReservations.TryGetValue(origin, out var list)) return 0;
        float cutoff = Now - ReservationTtlSeconds;
        list.RemoveAll(t => t < cutoff);
        if (list.Count == 0) { obstacleReservations.Remove(origin); return 0; }
        return list.Count;
    }

    public void ReserveTile(TileView tile)
    {
        if (tile != null) tileReservations[tile] = Now;
    }

    public void ReleaseTile(TileView tile)
    {
        if (tile != null) tileReservations.Remove(tile);
    }

    public bool IsTileReserved(TileView tile)
    {
        if (tile == null || !tileReservations.TryGetValue(tile, out float at)) return false;
        if (Now - at <= ReservationTtlSeconds) return true;
        tileReservations.Remove(tile);
        return false;
    }

    /// Canlı rezervasyonların tahtadaki hücreleri (seçicilerin "botları farklı kümelere yay" cezası için).
    public List<Vector2Int> ReservedCells()
    {
        var cells = new List<Vector2Int>(tileReservations.Count + obstacleReservations.Count);
        var staleTiles = new List<TileView>();
        foreach (var kv in tileReservations)
        {
            if (kv.Key == null || Now - kv.Value > ReservationTtlSeconds) { staleTiles.Add(kv.Key); continue; }
            cells.Add(new Vector2Int(kv.Key.X, kv.Key.Y));
        }
        foreach (var t in staleTiles) tileReservations.Remove(t);

        var obs = board.ObstacleStateService;
        if (obs == null || obstacleReservations.Count == 0) return cells;
        foreach (int origin in new List<int>(obstacleReservations.Keys))
        {
            if (ReservedObstacleHits(origin) <= 0) continue;
            cells.Add(new Vector2Int(origin % board.Width, origin / board.Width));
        }
        return cells;
    }
}
