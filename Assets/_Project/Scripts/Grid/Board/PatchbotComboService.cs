using System.Collections.Generic;
using UnityEngine;

public class PatchbotComboService
{
    private readonly BoardController board;
    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();
    private readonly List<(int x, int y, TileView tile)> bestImpactBuffer = new();

    public PatchbotComboService(BoardController board)
    {
        this.board = board;
    }

    public bool HasObstacleAt(int x, int y)
    {
        return board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y);
    }

    public bool HasContentAt(int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (HasObstacleAt(x, y)) return true;
        if (board.Holes[x, y]) return false;
        return board.GridData[x, y] != null;
    }

    public void EnqueueDash(
        TileView fromTile,
        int targetX,
        int targetY,
        TileView carriedTile = null,
        System.Action onDashStart = null,
        System.Action onArrived = null)
    {
        if (fromTile == null) return;

        // Dash uçarken board akışı devam edebilir, ama "iş bitti" denilemez.
        // ActiveBackgroundJobs içinde kalır; BlockingBackgroundJobs hesabından düşülür.
        board.BeginPatchBotDashFlight();

        Sprite carriedSprite = null;
        bool orbitCarry = false;

        if (carriedTile != null && carriedTile.GetSpecial() != TileSpecial.None)
        {
            carriedSprite = carriedTile.GetIconSprite();
            orbitCarry = carriedSprite != null;
        }

        board.EnqueuePatchbotDash(
            new BoardController.PatchbotDashRequest
            {
                from = new Vector2Int(fromTile.X, fromTile.Y),
                to = new Vector2Int(targetX, targetY),
                carriedSprite = carriedSprite,
                orbitCarry = orbitCarry,
                onStart = onDashStart,
                onArrived = () =>
                {
                    try
                    {
                        // onArrived içinde combo (örn. PulseCorePatchBotCombo) kendi
                        // ActiveBackgroundJobs++'ını çağırabilir. Bu yapılmalı çünkü
                        // bu bloğun finally'si dash'ı serbest bırakacak ve combo'nun
                        // kendi job'ı devam edecek.
                        onArrived?.Invoke();
                    }
                    finally
                    {
                        // Dash kendisi bitti. Ancak combo callback yeni bir background job
                        // başlatmış olabilir; o iş kendi sayacını ayrı yönetiyor.
                        board.EndPatchBotDashFlight();
                    }
                }
            }
        );
    }

    /// <summary>
    /// Backward-compatible entry point.
    /// Existing callers keep working, but fallback re-targeting has no partner/excluded context.
    /// Prefer the overload below for PatchBot + PatchBot and partner combos.
    /// </summary>
    public void EnqueueDashFromIntent(
        TileView fromTile,
        PatchBotIntent intent,
        PatchBotTargetCoordinator coordinator,
        TileView carriedTile = null,
        System.Action onDashStart = null,
        System.Action<int, int, PatchBotIntent> onArrived = null)
    {
        EnqueueDashFromIntent(
            fromTile,
            intent,
            coordinator,
            partnerTile: null,
            excluded: null,
            carriedTile,
            onDashStart,
            onArrived);
    }

    /// <summary>
    /// Preferred PatchBot dash path.
    /// The intent is picked before the visual dash is queued, but resolved again exactly when
    /// PatchbotDashUI leaves hover and starts the dive. If the original intent died during
    /// cascade, the coordinator can pick a fresh target while still knowing the actor, partner,
    /// and already-used targets.
    /// </summary>
    public void EnqueueDashFromIntent(
        TileView fromTile,
        PatchBotIntent intent,
        PatchBotTargetCoordinator coordinator,
        TileView partnerTile,
        HashSet<TileView> excluded,
        TileView carriedTile = null,
        System.Action onDashStart = null,
        System.Action<int, int, PatchBotIntent> onArrived = null)
    {
        if (fromTile == null || intent == null || coordinator == null)
            return;

        var fromCell = new Vector2Int(fromTile.X, fromTile.Y);
        var initialTarget = intent.CurrentCell(board);
        if (!IsInside(initialTarget.x, initialTarget.y))
            initialTarget = intent.InitialCell;

        if (!IsInside(initialTarget.x, initialTarget.y))
            return;

        PatchBotIntent liveIntent = intent;
        Vector2Int liveTarget = initialTarget;

        PatchbotLiveDashTargetRegistry.Register(fromCell, initialTarget, () =>
        {
            var resolved = coordinator.ResolveIntentToCell(
                liveIntent,
                fromTile,
                partnerTile,
                excluded);

            // hasCell=false ise ölü intent koordinatörde ZATEN release edildi; referansı
            // düşür ki bir sonraki çağrı aynı intent'i ikinci kez release etmesin
            // (resolver artık uçuş boyunca tekrar tekrar çağrılıyor).
            liveIntent = resolved.intent;

            if (!resolved.hasCell || !IsInside(resolved.cell.x, resolved.cell.y))
                return null;

            liveTarget = resolved.cell;
            return liveTarget;
        });

        EnqueueDash(
            fromTile,
            initialTarget.x,
            initialTarget.y,
            carriedTile,
            onDashStart,
            () => onArrived?.Invoke(liveTarget.x, liveTarget.y, liveIntent));
    }

    public void ConsumePatchBotOnly(HashSet<TileView> matches, TileView patchBotTile, System.Action<TileView> markAffectedCell)
    {
        if (patchBotTile == null) return;

        matches.Add(patchBotTile);
        markAffectedCell?.Invoke(patchBotTile);
    }

    public void ResolveTargetImpact(HashSet<TileData> matches, int targetX, int targetY, bool hasObstacleAtTarget, System.Action<int, int> markAffectedCell, System.Action<TileView> markAffectedTile)
    {
        if (hasObstacleAtTarget)
        {
            // Under-tile obstacle (Mud vb.) + üstte tile varsa: taşı kır,
            // doğal hasar yoluyla obstacle zaten hit alır.
            var obstacleService = board.ObstacleStateService;
            bool isUnderTile = obstacleService != null && obstacleService.IsUnderTileObstacleAt(targetX, targetY);
            var tileOnTop = board.Tiles[targetX, targetY];

            if (isUnderTile && tileOnTop != null)
            {
                // Tile clear path — Mud kendiliğinden hasar alır
                HitCellOnce(matches, targetX, targetY, tileOnTop, markAffectedCell, markAffectedTile);
                return;
            }

            // Over-tile blocker veya taşı olmayan under-tile → direkt obstacle hit
            board.MarkPatchBotForcedObstacleHit(targetX, targetY);
            markAffectedCell?.Invoke(targetX, targetY);
            return;
        }

        HitCellOnce(matches, targetX, targetY, board.Tiles[targetX, targetY], markAffectedCell, markAffectedTile);
    }

    public void HitCellOnce(HashSet<TileData> matches, int x, int y, TileView tileAtCell, System.Action<int, int> markAffectedCell, System.Action<TileView> markAffectedTile)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return;
        if (board.Holes[x, y] && !HasObstacleAt(x, y)) return;

        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null && obstacleService.GetObstacleIdAt(x, y) != ObstacleId.None)
        {
            // Under-tile + tile varsa: taşı kır (Mud doğal hit alır)
            bool isUnderTile = obstacleService.IsUnderTileObstacleAt(x, y);
            var tileOnTop = tileAtCell ?? board.Tiles[x, y];

            if (isUnderTile && tileOnTop != null)
            {
                var tdUnder = board.GridData[x, y];
                if (tdUnder != null)
                {
                    matches.Add(tdUnder);
                    markAffectedTile?.Invoke(tileOnTop);
                    return;
                }
            }

            // Over-tile blocker veya tile yok → obstacle hit yolu
            markAffectedCell?.Invoke(x, y);
            return;
        }

        var tileView = tileAtCell ?? board.Tiles[x, y];
        if (tileView != null
            && (board.GridData[x, y] == null
                || board.GridData[x, y].Type != tileView.GetTileType()
                || board.GridData[x, y].Special != tileView.GetSpecial()))
        {
            board.SyncTileData(x, y);
        }

        var tileData = board.GridData[x, y];
        if (tileData == null) return;

        matches.Add(tileData);
        if (tileView != null) markAffectedTile?.Invoke(tileView);
    }

    public (TileView tile, int x, int y, bool hasCell) FindTarget(TileView patchBotTile, TileView partnerTile, HashSet<TileView> excluded, params TileView[] additionalExcluded)
    {
        var cargoDropPathCells = new List<(int x, int y, TileView tile)>();
        var obstacleGoalCells = new List<(int x, int y, TileView tile)>();
        var tileGoalCells = new List<(int x, int y, TileView tile)>();
        var otherObstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();

        var activeGoals = board.TopHud;
        activeGoalsBuffer.Clear();
        activeGoals?.GetActiveGoals(activeGoalsBuffer);

        var activeObstacleGoals = new HashSet<ObstacleId>();
        var activeTileGoals = new List<TileType>();
        for (int i = 0; i < activeGoalsBuffer.Count; i++)
        {
            var goal = activeGoalsBuffer[i];
            if (goal.targetType == LevelGoalTargetType.Obstacle && goal.obstacleId != ObstacleId.None)
                activeObstacleGoals.Add(goal.obstacleId);
            else if (goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId == CollectibleId.EnergyOrb)
            {
                // EnergyOrb hem EnergyContainer hem HatLauncher'dan çıkar → ikisi de goal obstacle.
                activeObstacleGoals.Add(ObstacleId.EnergyContainer);
                activeObstacleGoals.Add(ObstacleId.HatLauncher);
            }
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
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y] && !HasObstacleAt(x, y)) continue;

                var tile = board.Tiles[x, y];

                bool hasObstacle = board.ObstacleStateService != null &&
                                   board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.None;

                if (hasObstacle)
                {
                    var obstacleId = board.ObstacleStateService.GetObstacleIdAt(x, y);

                    // Cargo (exitAtBottom) KIRILMAZ; üstüne konmak faydasız. Bunun yerine
                    // düşüş yolunu açmak için ALTINDAKI normal taşı hedefle → cargo aşağı
                    // düşüp tabandan çıkar (hedef ilerler).
                    if (board.ObstacleStateService.IsExitAtBottomAt(x, y))
                    {
                        TryAddCargoDropPathTarget(x, y, cargoDropPathCells, IsExcludedTile);
                        continue;
                    }

                    bool isObstacleGoalCell = activeObstacleGoals.Contains(obstacleId);

                    // Under-tile obstacle (Mud vb.): üzerinde taş varsa taşı hedefle —
                    // taş kırılınca obstacle zaten hit alır (doğal hasar yolu).
                    // Üzerinde taş yoksa direkt obstacle'a vurulur.
                    bool isUnderTile = board.ObstacleStateService.IsUnderTileObstacleAt(x, y);
                    bool hasTileOnTop = tile != null
                                        && board.GridData[x, y] != null
                                        && SpecialUtils.CanTargetTileContent(board, x, y)
                                        && !IsExcludedTile(tile);

                    if (isUnderTile && hasTileOnTop)
                    {
                        // Taşı hedefle; obstacle goal'sa yine yüksek öncelikli (tile goal kovasına koy).
                        if (isObstacleGoalCell || IsGoalTile(tile))
                            tileGoalCells.Add((x, y, tile));
                        else
                            normalCells.Add((x, y, tile));
                    }
                    else if (isObstacleGoalCell)
                    {
                        obstacleGoalCells.Add((x, y, tile));
                    }
                    else
                    {
                        otherObstacleCells.Add((x, y, tile));
                    }
                }
                else if (tile != null
                         && board.GridData[x, y] != null
                         && SpecialUtils.CanTargetTileContent(board, x, y)
                         && !IsExcludedTile(tile))
                {
                    if (IsGoalTile(tile))
                        tileGoalCells.Add((x, y, tile));
                    else
                        normalCells.Add((x, y, tile));
                }
            }

        // Önce en yüksek öncelikli dolu kova; içinden RASTGELE değil, payload'ın en çok hücreye
        // değeceği (en yoğun küme) hücre seçilir → bot tepedeki/kenardaki tek hücreyi değil,
        // ortadaki yoğunluğu hedefler. Tüm patchbot kombinasyonları bu seçimi paylaşır.

        // Cargo düşüş yolu en yüksek öncelik: cargo başka türlü kırılamadığı için,
        // altındaki taşı açmak hedefi ilerletmenin tek yolu.
        if (cargoDropPathCells.Count > 0)
            return PickHighestImpact(cargoDropPathCells);

        if (obstacleGoalCells.Count > 0)
            return PickHighestImpact(obstacleGoalCells);

        if (tileGoalCells.Count > 0)
            return PickHighestImpact(tileGoalCells);

        if (otherObstacleCells.Count > 0)
            return PickHighestImpact(otherObstacleCells);

        if (normalCells.Count > 0)
            return PickHighestImpact(normalCells);

        return (null, -1, -1, false);
    }

    // Cargo (exitAtBottom) kendisi kırılmaz. Onu ilerletmek için, (varsa cargo yığınının)
    // hemen ALTINDAKI ilk normal taşı hedef listesine ekler — o taş temizlenince cargo bir
    // sıra aşağı düşer, tabana ulaşınca board'dan çıkar. Alt hücre hole/başka obstacle ise
    // ya da cargo zaten tabandaysa yardım edecek bir taş yoktur (eklemez).
    private void TryAddCargoDropPathTarget(int cargoX, int cargoY,
        List<(int x, int y, TileView tile)> outCells, System.Func<TileView, bool> isExcluded)
    {
        var obs = board.ObstacleStateService;
        if (obs == null) return;

        int by = cargoY + 1;
        while (by < board.Height && obs.IsExitAtBottomAt(cargoX, by))
            by++;                                  // üst üste cargo → yığının altına in

        if (by >= board.Height) return;            // cargo zaten tabanda; sıradaki resolve toplar
        if (obs.GetObstacleIdAt(cargoX, by) != ObstacleId.None) return; // altı başka obstacle
        if (board.Holes[cargoX, by]) return;

        var belowTile = board.Tiles[cargoX, by];
        if (belowTile == null) return;
        if (board.GridData[cargoX, by] == null) return;
        if (!SpecialUtils.CanTargetTileContent(board, cargoX, by)) return;
        if (isExcluded(belowTile)) return;

        for (int i = 0; i < outCells.Count; i++)
            if (outCells[i].x == cargoX && outCells[i].y == by) return; // aynı hücreyi iki kez ekleme

        outCells.Add((cargoX, by, belowTile));
    }

    // Payload'ın etki yarıçapı (PulseCore 5x5 ≈ 2; line/bomb için de yoğunluk iyi bir proxy).
    private const int PatchbotImpactRadius = 2;

    // Adayı, AYNI kovadaki kaç hücrenin payload yarıçapına girdiğine göre puanlar; en yüksek
    // puanlıyı seçer, eşitlikte rastgele kırar (hep aynı hücreyi seçip tekdüze olmasın).
    private (TileView tile, int x, int y, bool hasCell) PickHighestImpact(
        List<(int x, int y, TileView tile)> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return (null, -1, -1, false);

        int bestScore = -1;
        bestImpactBuffer.Clear();

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            int score = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (j == i) continue;
                var o = candidates[j];
                if (Mathf.Abs(o.x - c.x) <= PatchbotImpactRadius &&
                    Mathf.Abs(o.y - c.y) <= PatchbotImpactRadius)
                    score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestImpactBuffer.Clear();
                bestImpactBuffer.Add(c);
            }
            else if (score == bestScore)
            {
                bestImpactBuffer.Add(c);
            }
        }

        var pick = bestImpactBuffer[Random.Range(0, bestImpactBuffer.Count)];
        return (pick.tile, pick.x, pick.y, true);
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < board.Width && y >= 0 && y < board.Height;
    }
}
