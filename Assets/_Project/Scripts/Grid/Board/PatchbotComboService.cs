using System.Collections.Generic;
using UnityEngine;

public class PatchbotComboService
{
    private readonly BoardController board;

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
        bool targetResolved = false;

        Vector2Int? ResolveLiveTarget()
        {
            targetResolved = true;
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
            {
                liveTarget = new Vector2Int(-1, -1);
                return null;
            }

            liveTarget = resolved.cell;
            return liveTarget;
        }

        PatchbotLiveDashTargetRegistry.Register(fromCell, initialTarget, ResolveLiveTarget);

        EnqueueDash(
            fromTile,
            initialTarget.x,
            initialTarget.y,
            carriedTile,
            onDashStart,
            () =>
            {
                // Headless playback may never acquire the visual resolver. Otherwise do not
                // apply an impact to a target that died after the last visual retarget tick.
                if (!targetResolved) ResolveLiveTarget();
                if (liveIntent == null || !liveIntent.IsAlive(board))
                    liveTarget = new Vector2Int(-1, -1);
                // Invalid coordinates cancel the impact, but still run caller cleanup.
                onArrived?.Invoke(liveTarget.x, liveTarget.y, liveIntent);
            });
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

    // ── SpreadingGel hedeflemesi ─────────────────────────────────────────────
    // Jel hiç kırılmaz: jelli bir hücreye vurmak kaplama hedefini İLERLETMEZ. O yüzden (a) jel
    // hücreleri "obstacle goal" sayılmaz, (b) bot bulaş taşıyorsa (payload'ı jel bırakacaksa) ve
    // kırılacak obstacle kalmadıysa HENÜZ JEL OLMAYAN hücrelere gider → hedefi ilerletir.
    public bool ShouldPreferNonGelCells(TileView patchBotTile, bool gelGoalActive)
    {
        if (!gelGoalActive) return false;

        var gel = board.SpreadingGelService;
        if (gel == null) return false;

        if (board.IsGelSpreadActiveThisMove) return true;
        if (patchBotTile == null || !patchBotTile) return false;

        return patchBotTile.GelContaminated || gel.IsSpreadSourceAt(patchBotTile.X, patchBotTile.Y);
    }

    /// Jel YAYILABİLECEK hücre: henüz jel yok ve bot'un kıracağı bir taş var (jel, kırılan taşın
    /// hücresine düşer). Engelle kapalı / içeriği hedeflenemeyen (oil, kafes, cargo) ve movable
    /// obstacle hücreleri jel almaz → hedef değil. Obstacle'ın cinsine bakılmaz (mud vb. altına da yayılır).
    public bool IsGelSpreadTarget(int x, int y, TileView tile)
    {
        var gel = board.SpreadingGelService;
        if (gel == null || tile == null || !tile || tile.GetSpecial() != TileSpecial.None) return false;
        if (board.IsMaskHoleCell(x, y) || gel.IsGelAt(x, y)) return false;
        if (board.GridData[x, y] == null || !SpecialUtils.CanTargetTileContent(board, x, y)) return false;
        return board.ObstacleStateService == null || !board.ObstacleStateService.IsMovableObstacleAt(x, y);
    }

    // Payload'ın etki yarıçapı (PulseCore 5x5 ≈ 2; line/bomb için de yoğunluk iyi bir proxy).
    private const int PatchbotImpactRadius = 2;

    // ── Cargo düşüş yolu ────────────────────────────────────────────────────
    // Cargo kırılmaz; altındaki sütundan taş kırıldıkça iner. Bot, sütun boyunca payload'ının o
    // sütunda EN ÇOK taşı temizleyeceği hücreye vurur (ör. PulseCore 5x5: cargo'nun 2 altı → 5 taş;
    // hemen altı → 3). Tek vuruş / LineV'de temizlenen miktar değişmez → en yakın (hemen alt) hücre.
    public static (int up, int down) PayloadColumnReach(TileView payload, int boardHeight)
    {
        if (payload == null || !payload) return (0, 0);
        switch (payload.GetSpecial())
        {
            case TileSpecial.PulseCore: return (PatchbotImpactRadius, PatchbotImpactRadius);
            case TileSpecial.LineV: return (boardHeight, boardHeight);
            default: return (0, 0);
        }
    }

    public void AddCargoDropPathTarget(int cargoX, int cargoY, TileView payload,
        List<(int x, int y, TileView tile)> outCells, System.Func<TileView, bool> isExcluded)
    {
        var obs = board.ObstacleStateService;
        if (obs == null) return;

        int top = cargoY + 1;
        while (top < board.Height && obs.IsExitAtBottomAt(cargoX, top))
            top++;                                  // üst üste cargo → yığının altından başla
        if (top >= board.Height) return;            // cargo zaten tabanda; sıradaki resolve toplar

        // Cargo'nun altındaki kesintisiz kırılabilir taş dizisi (obstacle/hole/boşlukta biter).
        int bottom = top - 1;
        while (bottom + 1 < board.Height && IsCargoPathTile(cargoX, bottom + 1))
            bottom++;
        if (bottom < top) return;                   // hemen altı kırılamaz → yardım edecek taş yok

        var (up, down) = PayloadColumnReach(payload, board.Height);
        int bestY = -1, bestCleared = 0;
        for (int y = top; y <= bottom; y++)
        {
            var tile = board.Tiles[cargoX, y];
            if (isExcluded(tile)) continue;
            int cleared = Mathf.Min(bottom, y + down) - Mathf.Max(top, y - up) + 1;
            if (cleared > bestCleared)                 // eşitlikte cargo'ya en yakın kalır
            {
                bestCleared = cleared;
                bestY = y;
            }
        }
        if (bestY < 0) return;

        for (int i = 0; i < outCells.Count; i++)
            if (outCells[i].x == cargoX && outCells[i].y == bestY) return; // aynı hücreyi iki kez ekleme

        outCells.Add((cargoX, bestY, board.Tiles[cargoX, bestY]));
    }

    private bool IsCargoPathTile(int x, int y)
    {
        var obs = board.ObstacleStateService;
        if (obs != null && obs.GetObstacleIdAt(x, y) != ObstacleId.None) return false;
        if (board.Holes[x, y]) return false;
        return board.Tiles[x, y] != null && board.GridData[x, y] != null
            && SpecialUtils.CanTargetTileContent(board, x, y);
    }


    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < board.Width && y >= 0 && y < board.Height;
    }
}
