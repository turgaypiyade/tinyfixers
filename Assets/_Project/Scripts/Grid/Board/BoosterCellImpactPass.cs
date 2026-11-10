using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared Lift/GelLauncher arrival impact. One hit per cell/origin per sweep;
/// normal tiles break, exposed specials activate, covered contents survive the uncovering hit.
/// clearObstaclesFully (GelLauncher, kullanıcı kuralı 2026-10-31): hücredeki engeller TAMAMEN yok edilir — engel
/// canı düştükçe vuruş tekrarlanır, üstteki kırılınca alttaki katman da vurulur. Canı düşmeyen engelde (kuş evi,
/// enerji kabı gibi üreticiler) tek vuruşta durulur: aynı hücreye tekrar vurmak bedava ödül üretmesin.</summary>
public sealed class BoosterCellImpactPass
{
    private readonly BoardController board;
    private readonly Func<TileView, bool> reserveSpecial;
    private readonly Action<TileView> launchSpecial;
    private readonly HashSet<Vector2Int> reachedCells = new();
    private readonly HashSet<int> hitObstacleOrigins = new();
    private readonly Dictionary<TileType, int> clearedByType = new();
    private readonly bool clearObstaclesFully;
    // İsteğe bağlı taş kırılma görseli (Sıcak Hava Balonu: taşı yana savurur); null → ortak PlayTileBreak.
    private readonly Action<TileView> tileBreakVisual;
    // Çağıranın yol tutması (HoldForFlow): bu hücrelerdeki special'lar "başka sahipli" sayılmaz, tetiklenir.
    private readonly CellHold ownHold;
    private const int MaxHitsPerCell = 12;   // en uzun yığın + en çok aşamalı engel için güvenli üst sınır

    internal BoosterCellImpactPass(BoardController board, Func<TileView, bool> reserveSpecial = null,
        Action<TileView> launchSpecial = null, bool clearObstaclesFully = false, Action<TileView> tileBreakVisual = null,
        CellHold ownHold = null)
    {
        this.board = board;
        this.clearObstaclesFully = clearObstaclesFully;
        this.tileBreakVisual = tileBreakVisual;
        this.ownHold = ownHold;
        this.reserveSpecial = reserveSpecial;
        this.launchSpecial = launchSpecial ?? board.TriggerSpecialTileFromBoardEffect;
    }

    public void HitCell(Vector2Int cell)
    {
        int x = cell.x, y = cell.y;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height
            || !reachedCells.Add(cell)) return;

        var obstacles = board.ObstacleStateService;
        if (obstacles != null && obstacles.IsUnbreakableMovableAt(x, y)) return;
        if (board.Holes[x, y] && (obstacles == null || !obstacles.HasObstacleAt(x, y))) return;

        // Capture eligibility before damage: breaking a covering obstacle must
        // not also consume the newly revealed content with the same hit.
        var tile = board.Tiles[x, y];
        bool canClear = tile != null && (obstacles == null
            || (!obstacles.IsCellBlocked(x, y)
                && !obstacles.IsInteractionLockedAt(x, y)
                && !obstacles.IsMovableObstacleAt(x, y)));
        TileView chainedSpecial = null;
        if (canClear)
        {
            board.MarkBoosterGelHit(tile);   // joker her vurduğu taşın hücresine jel bırakır
            if (tile.GetSpecial() != TileSpecial.None)
            {
                if (reserveSpecial != null ? reserveSpecial(tile)
                    : !board.IsSpecialOwnedElsewhere(x, y, ownHold))
                {
                    chainedSpecial = tile;
                }
            }
            else
            {
                if (tileBreakVisual != null) tileBreakVisual(tile);
                else board.BreakFx?.PlayTileBreak(tile);
                board.ClearAndDestroyTile(tile, clearedByType);
                foreach (var pair in clearedByType)
                    board.NotifyTilesCleared(pair.Key, pair.Value);
                clearedByType.Clear();
            }
        }

        if (obstacles != null && obstacles.HasObstacleAt(x, y))
        {
            bool magnet = obstacles.GetObstacleIdAt(x, y) == ObstacleId.Magnet;
            bool perCell = obstacles.TakesPerCellHits(x, y);
            int origin = obstacles.GetObstacleOriginAt(x, y);
            if ((!magnet || obstacles.IsMagnetEndpoint(x, y))
                && (perCell || origin < 0 || !hitObstacleOrigins.Contains(origin)))
            {
                if (clearObstaclesFully)
                {
                    if (ClearObstacles(x, y) && !perCell && origin >= 0) hitObstacleOrigins.Add(origin);
                }
                else
                {
                    var hit = board.ApplyObstacleDamageAt(x, y, ObstacleHitContext.Booster);
                    if (hit.didHit)
                    {
                        if (!perCell && origin >= 0) hitObstacleOrigins.Add(origin);
                        board.TriggerObstacleVisualChange(hit.visualChange);
                    }
                }
            }

            // Taşsız engel hücresi: vuruş engeli tamamen kaldırdıysa açılan hücre de jel olur.
            if (tile == null && !obstacles.HasObstacleAt(x, y) && board.Tiles[x, y] == null)
                board.PaintGelForBoosterCell(x, y);
        }

        // Launch after this impact is applied; the special owns its own clear
        // and runs concurrently with the remainder of the booster animation.
        if (chainedSpecial != null) launchSpecial(chainedSpecial);
    }


    // Hücrede engel kaldıkça vur; her vuruştan ÖNCE durum alınır, ilerleme yoksa durulur: kırılmayan/üretici engel
    // (aynı engel, aynı kalan can → kuş evi, enerji kabı tek vuruş alır) ve vuruşu reddeden engel. Üstteki katman
    // kırılınca alttaki vurulmaya devam eder. Duvar ailesinde can parça bazındadır (hücre aşaması görünmez) → didHit
    // yeterli; aşamalar sınırlı olduğu için MaxHitsPerCell içinde yıkılır. En az bir vuruş indiyse true.
    private bool ClearObstacles(int x, int y)
    {
        var obstacles = board.ObstacleStateService;
        bool anyHit = false;
        // Son inen vuruşun efekti bekletilir: arkasından aynı engele bir vuruş daha inerse ara adım olur (sessiz),
        // inmezse son vuruştur (parçacık + ses). Böylece çok vuruşluk engel tek efektle kırılır.
        bool hasPending = false;
        ObstacleVisualChange pending = default;
        for (int i = 0; i < MaxHitsPerCell && obstacles.HasObstacleAt(x, y); i++)
        {
            var id = obstacles.GetObstacleIdAt(x, y);
            if (id == ObstacleId.Magnet && !obstacles.IsMagnetEndpoint(x, y)) break;
            int origin = obstacles.GetObstacleOriginAt(x, y);
            int before = obstacles.GetRemainingHitsAt(x, y);

            var hit = board.ApplyObstacleDamageAt(x, y, ObstacleHitContext.Booster);
            if (!hit.didHit) break;
            anyHit = true;

            if (hasPending) board.TriggerObstacleVisualChangeSilent(pending);
            hasPending = false;
            if (hit.visualChange.cleared)
                board.TriggerObstacleVisualChange(hit.visualChange);   // kırılan katman her zaman tam efekt
            else
            {
                pending = hit.visualChange;
                hasPending = true;
            }

            bool progressed = hit.visualChange.cleared
                || ObstacleStateService.IsWallFamily(id)
                || obstacles.GetObstacleIdAt(x, y) != id
                || obstacles.GetObstacleOriginAt(x, y) != origin
                || obstacles.GetRemainingHitsAt(x, y) < before;
            if (!progressed) break;
        }
        if (hasPending) board.TriggerObstacleVisualChange(pending);
        return anyHit;
    }
}
