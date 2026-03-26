using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot'ların hedef çakışmasını önleyen koordinatör.
///
/// İki tür rezervasyon tutar:
///   1) Obstacle: origin index bazlı — multi-cell obstacle'lar aynı origin'i paylaştığı için
///      kalan vuruş sayısı origin üzerinden doğru hesaplanır.
///   2) Normal tile: cell index bazlı — her taşa en fazla 1 bot atanır.
///
/// Kullanım:
///   var coordinator = new PatchBotTargetCoordinator(board, patchbotService);
///   var target = coordinator.ReserveTarget(botTile, null, null);
///   // ... dash animasyonu ...
///   coordinator.ReleaseReservation(target.x, target.y);  // varışta çağır
/// </summary>
public class PatchBotTargetCoordinator
{
    private readonly BoardController board;
    private readonly PatchbotComboService patchbotService;

    // obstacle origin index → bu origin'e atanmış PatchBot sayısı
    private readonly Dictionary<int, int> obstacleReservationsByOrigin = new();

    // normal (obstacle olmayan) taşlar için cell index bazlı rezervasyon
    private readonly HashSet<int> tileReservations = new();

    // Aktif PatchBot sayısı (havalanmış, henüz vurmamış)
    private int activeBotCount;

    public int ActiveBotCount => activeBotCount;

    public PatchBotTargetCoordinator(BoardController board, PatchbotComboService patchbotService)
    {
        this.board = board;
        this.patchbotService = patchbotService;
    }

    /// <summary>
    /// Koordinatörlü hedef bulma + rezervasyon.
    /// Obstacle'larda: kalan vuruş - mevcut rezervasyon > 0 olmalı.
    /// Normal taşlarda: aynı hücreye 2 bot atanamaz.
    /// Tüm goal hedefleri tükendiyse herhangi bir boş normal taşa yönlendirir.
    /// </summary>
    public (TileView tile, int x, int y, bool hasCell) ReserveTarget(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        params TileView[] additionalExcluded)
    {
        var result = FindTargetWithReservations(patchBotTile, partnerTile, excluded, additionalExcluded);

        if (result.hasCell)
        {
            RegisterReservation(result.x, result.y);
            activeBotCount++;
        }

        return result;
    }

    /// <summary>
    /// PatchBot hedefe varıp vurduktan sonra çağrılır.
    /// Rezervasyonu serbest bırakır.
    /// </summary>
    public void ReleaseReservation(int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null)
        {
            int origin = obstacleService.GetObstacleOriginAt(x, y);
            if (origin >= 0 && obstacleReservationsByOrigin.ContainsKey(origin))
            {
                obstacleReservationsByOrigin[origin]--;
                if (obstacleReservationsByOrigin[origin] <= 0)
                    obstacleReservationsByOrigin.Remove(origin);

                activeBotCount = Mathf.Max(0, activeBotCount - 1);
                return;
            }
        }

        // Normal tile reservation
        tileReservations.Remove(CellIndex(x, y));
        activeBotCount = Mathf.Max(0, activeBotCount - 1);
    }

    /// <summary>
    /// Obstacle'ın efektif kalan vuruşu: gerçek kalan - origin bazlı aktif rezervasyonlar.
    /// </summary>
    public int GetEffectiveObstacleHitsRemaining(int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService == null) return 0;

        int actual = obstacleService.GetRemainingHitsAt(x, y);
        int origin = obstacleService.GetObstacleOriginAt(x, y);
        if (origin < 0) return actual;

        int reserved = obstacleReservationsByOrigin.ContainsKey(origin)
            ? obstacleReservationsByOrigin[origin]
            : 0;

        return actual - reserved;
    }

    /// <summary>
    /// Normal taş bu koordinatör tarafından zaten reserve edilmiş mi?
    /// </summary>
    public bool IsTileReserved(int x, int y)
    {
        return tileReservations.Contains(CellIndex(x, y));
    }

    // ─────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────

    private void RegisterReservation(int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null)
        {
            int origin = obstacleService.GetObstacleOriginAt(x, y);
            if (origin >= 0)
            {
                if (!obstacleReservationsByOrigin.ContainsKey(origin))
                    obstacleReservationsByOrigin[origin] = 0;
                obstacleReservationsByOrigin[origin]++;
                return;
            }
        }

        // Normal tile
        tileReservations.Add(CellIndex(x, y));
    }

    // ─────────────────────────────────────────────
    // Koordinatörlü FindTarget — PatchbotComboService.FindTarget'ın
    // reservation-aware versiyonu
    // ─────────────────────────────────────────────

    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();

    private (TileView tile, int x, int y, bool hasCell) FindTargetWithReservations(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        TileView[] additionalExcluded)
    {
        var obstacleGoalCells = new List<(int x, int y, TileView tile)>();
        var tileGoalCells = new List<(int x, int y, TileView tile)>();
        var otherObstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();

        // Aktif hedefleri topla
        activeGoalsBuffer.Clear();
        var activeGoals = board.TopHud;
        activeGoals?.GetActiveGoals(activeGoalsBuffer);

        var activeObstacleGoals = new HashSet<ObstacleId>();
        var activeTileGoals = new List<TileType>();
        for (int i = 0; i < activeGoalsBuffer.Count; i++)
        {
            var goal = activeGoalsBuffer[i];
            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId != ObstacleId.None)
                activeObstacleGoals.Add(goal.obstacleId);
            else if (goal.targetType == LevelGoalTargetType.Tile)
                activeTileGoals.Add(goal.tileType);
        }

        bool IsExcludedTile(TileView tile)
        {
            if (tile == null) return true;
            if (excluded != null && excluded.Contains(tile)) return true;
            if (tile == patchBotTile || tile == partnerTile) return true;
            if (additionalExcluded != null)
            {
                for (int i = 0; i < additionalExcluded.Length; i++)
                {
                    if (tile == additionalExcluded[i]) return true;
                }
            }
            return false;
        }

        bool IsGoalTile(TileView tile)
        {
            if (tile == null) return false;
            var type = tile.GetTileType();
            for (int i = 0; i < activeTileGoals.Count; i++)
            {
                if (activeTileGoals[i].Equals(type)) return true;
            }
            return false;
        }

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] && !patchbotService.HasObstacleAt(x, y)) continue;

                var tile = board.Tiles[x, y];

                bool hasObstacle = board.ObstacleStateService != null
                                   && board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.None;

                if (hasObstacle)
                {
                    // ── Origin-based reservation kontrolü ──
                    int effectiveHits = GetEffectiveObstacleHitsRemaining(x, y);
                    if (effectiveHits <= 0)
                        continue; // Bu obstacle'a yeterince bot atanmış, atla

                    var obstacleId = board.ObstacleStateService.GetObstacleIdAt(x, y);
                    bool isObstacleGoalCell = activeObstacleGoals.Contains(obstacleId);

                    if (isObstacleGoalCell)
                        obstacleGoalCells.Add((x, y, tile));
                    else
                        otherObstacleCells.Add((x, y, tile));
                }
                else if (tile != null && !IsExcludedTile(tile))
                {
                    // ── Cell-based reservation kontrolü ──
                    if (IsTileReserved(x, y))
                        continue;

                    if (IsGoalTile(tile))
                        tileGoalCells.Add((x, y, tile));
                    else
                        normalCells.Add((x, y, tile));
                }
            }
        }

        // Öncelik sırası: obstacle goals > tile goals > diğer obstacles > normal taşlar
        if (obstacleGoalCells.Count > 0)
        {
            var pick = obstacleGoalCells[Random.Range(0, obstacleGoalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (tileGoalCells.Count > 0)
        {
            var pick = tileGoalCells[Random.Range(0, tileGoalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (otherObstacleCells.Count > 0)
        {
            var pick = otherObstacleCells[Random.Range(0, otherObstacleCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        // ── Fallback: tüm goal hedefleri tükendi, herhangi bir boş taşa vur ──
        if (normalCells.Count > 0)
        {
            var pick = normalCells[Random.Range(0, normalCells.Count)];
            return (pick.tile, pick.x, pick.y, true);
        }

        return (null, -1, -1, false);
    }

    private int CellIndex(int x, int y)
    {
        return y * board.Width + x;
    }
}