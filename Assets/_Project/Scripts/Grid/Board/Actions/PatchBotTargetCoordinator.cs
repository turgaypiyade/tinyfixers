using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PatchBot intent — bot'un "neyi hedeflediğinin" hafızası.
/// Cell index değil, asıl hedef referansı tutulur ki taş düşse de takip edilebilsin.
/// </summary>
public sealed class PatchBotIntent
{
    /// <summary>Normal taş hedefi. Obstacle hedefi ise null.</summary>
    public TileView TargetTile;

    /// <summary>Obstacle hedefi origin index'i. Normal taş hedefi ise -1.</summary>
    public int ObstacleOriginIndex;

    /// <summary>Seçim anındaki cell — debug ve fallback için.</summary>
    public Vector2Int InitialCell;

    public bool IsObstacle => ObstacleOriginIndex >= 0;
    public bool IsTile => TargetTile != null;

    /// <summary>
    /// Intent hâlâ canlı mı? Taş board'da mı, obstacle hâlâ duruyor mu?
    /// </summary>
    public bool IsAlive(BoardController board)
    {
        if (board == null) return false;

        if (IsObstacle)
        {
            var obstacleService = board.ObstacleStateService;
            if (obstacleService == null) return false;

            // Origin'i tara — obstacle hâlâ o origin index'inde duruyor mu ve hit alabilir mi?
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (obstacleService.GetObstacleOriginAt(x, y) == ObstacleOriginIndex
                        && board.TargetPool.IsHittableObstacleCell(x, y))
                        return true;
                }
            return false;
        }

        if (TargetTile == null) return false;

        // Tile'ın koordinatına bak — board.Tiles[X,Y] hâlâ aynı view mı?
        int tx = TargetTile.X;
        int ty = TargetTile.Y;
        if (tx < 0 || tx >= board.Width || ty < 0 || ty >= board.Height) return false;
        if (board.Tiles[tx, ty] != TargetTile) return false;
        if (!SpecialUtils.CanTargetTileContent(board, tx, ty)) return false;

        // Mantıksal otorite GridData: view hâlâ sahnede dursa bile hücre verisi
        // temizlendiyse hedef ölmüştür (clear animasyonu data'dan geç kalabilir).
        return board.GridData[tx, ty] != null;
    }

    /// <summary>
    /// Intent'in şu anki cell'i. IsAlive false ise (-1,-1) döner.
    /// </summary>
    public Vector2Int CurrentCell(BoardController board)
    {
        if (!IsAlive(board)) return new Vector2Int(-1, -1);

        if (IsObstacle)
        {
            var obstacleService = board.ObstacleStateService;
            // İlk bulduğun origin-eşleşen ve hit alabilir hücreyi döndür.
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    if (obstacleService.GetObstacleOriginAt(x, y) == ObstacleOriginIndex
                        && board.TargetPool.IsHittableObstacleCell(x, y))
                        return new Vector2Int(x, y);
                }
            return new Vector2Int(-1, -1);
        }

        return new Vector2Int(TargetTile.X, TargetTile.Y);
    }
}

/// <summary>
/// PatchBot ailesinin hedef SEÇİM POLİTİKASI (öncelik + yoğunluk). Uygunluk ve rezervasyonlar bu sınıfta
/// DEĞİL, tahta başına tek <see cref="BoardTargetPool"/>'dadır (board.TargetPool) → kaç örnek oluşturulursa
/// oluşturulsun (combo/roket sepeti/Override grubu/iniş anı yeniden hedefleme) herkes aynı rezervasyonları görür.
///   1) Obstacle: origin bazlı kapasite (kalan anlamlı vuruş − ortak rezervasyon); çok-hücreli engel TEK aday.
///   2) Normal tile: TileView referansı bazlı — taş düşse de aynı view aynı bot tarafından takip edilir.
///
/// Yeni Intent API'si:
///   - PickIntent: kalkış anında bir hedef seçer + soft reservation koyar.
///   - ResolveIntentToCell: dive başında çağrılır. Intent canlıysa güncel cell'i, ölmüşse yeni hedef döner.
///   - ReleaseIntent: vuruş tamamlandıktan sonra çağrılır.
///
/// Eski API (ReserveTarget) geriye uyumluluk için tutuldu.
/// </summary>
public class PatchBotTargetCoordinator
{
    private readonly BoardController board;
    private readonly PatchbotComboService patchbotService;

    private BoardTargetPool Pool => board.TargetPool;

    private int activeBotCount;   // bu örneğin (tek kombo/grup) bot sayısı
    public int ActiveBotCount => activeBotCount;

    public PatchBotTargetCoordinator(BoardController board, PatchbotComboService patchbotService)
    {
        this.board = board;
        this.patchbotService = patchbotService;
    }

    // ─────────────────────────────────────────────
    // YENİ INTENT API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Kalkış anında çağrılır. Bir hedef seçer ve soft reservation koyar.
    /// hasIntent false ise board'da hedef yok demektir — bot ölmeli.
    /// </summary>
    public (PatchBotIntent intent, bool hasIntent) PickIntent(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        params TileView[] additionalExcluded)
    {
        Vector2Int fromCell = patchBotTile != null
            ? new Vector2Int(patchBotTile.X, patchBotTile.Y)
            : new Vector2Int(-1, -1);
        return PickIntentCore(patchBotTile, partnerTile, excluded, additionalExcluded, fromCell);
    }

    /// <summary>
    /// Kaynak hücresi bilinen ama tile'ı temizlenmiş botlar için (phantom, resolve sonrası).
    /// </summary>
    public (PatchBotIntent intent, bool hasIntent) PickIntentFrom(
        Vector2Int fromCell,
        TileView patchBotTile = null,
        TileView partnerTile = null,
        HashSet<TileView> excluded = null)
    {
        return PickIntentCore(patchBotTile, partnerTile, excluded, null, fromCell);
    }

    private (PatchBotIntent intent, bool hasIntent) PickIntentCore(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        TileView[] additionalExcluded,
        Vector2Int fromCell)
    {
        var pick = FindTargetWithReservations(patchBotTile, partnerTile, excluded, additionalExcluded, fromCell);
        if (!pick.hasCell)
            return (null, false);

        var intent = new PatchBotIntent
        {
            InitialCell = new Vector2Int(pick.x, pick.y)
        };

        var obstacleService = board.ObstacleStateService;
        bool isObstacle = obstacleService != null
            && obstacleService.GetObstacleIdAt(pick.x, pick.y) != ObstacleId.None;

        if (isObstacle)
        {
            int origin = obstacleService.GetObstacleOriginAt(pick.x, pick.y);
            intent.ObstacleOriginIndex = origin;
            intent.TargetTile = null;

            Pool.ReserveObstacle(origin);
        }
        else
        {
            intent.ObstacleOriginIndex = -1;
            intent.TargetTile = pick.tile;

            Pool.ReserveTile(pick.tile);
        }

        activeBotCount++;
        return (intent, true);
    }

    /// <summary>
    /// Dive başında çağrılır. Intent hâlâ canlı mı kontrol eder.
    ///   - Canlıysa: hedefin GÜNCEL cell'ini döner (taş düşmüşse yeni y, vs.)
    ///   - Ölmüşse: eski intent'i serbest bırakır, yeni bir intent seçer.
    ///   - Hiç hedef kalmadıysa: hasCell=false döner.
    /// </summary>
    public (Vector2Int cell, PatchBotIntent intent, bool hasCell) ResolveIntentToCell(
        PatchBotIntent intent,
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        params TileView[] additionalExcluded)
    {
        if (intent != null && intent.IsAlive(board))
        {
            var current = intent.CurrentCell(board);
            if (current.x >= 0)
                return (current, intent, true);
        }

        // Intent öldü — serbest bırak, yeni hedef ara.
        if (intent != null)
            ReleaseIntent(intent);

        var (newIntent, hasNew) = PickIntent(patchBotTile, partnerTile, excluded, additionalExcluded);
        if (!hasNew)
            return (new Vector2Int(-1, -1), null, false);

        var cell = newIntent.CurrentCell(board);
        if (cell.x < 0)
        {
            ReleaseIntent(newIntent);
            return (new Vector2Int(-1, -1), null, false);
        }

        return (cell, newIntent, true);
    }

    /// <summary>
    /// Kaynak hücresi bilinen ama tile'ı temizlenmiş botlar için (phantom, ResolveAllDiveTargets).
    /// </summary>
    public (Vector2Int cell, PatchBotIntent intent, bool hasCell) ResolveIntentFrom(
        PatchBotIntent intent,
        Vector2Int fromCell,
        TileView patchBotTile = null,
        TileView partnerTile = null,
        HashSet<TileView> excluded = null)
    {
        if (intent != null && intent.IsAlive(board))
        {
            var current = intent.CurrentCell(board);
            if (current.x >= 0)
                return (current, intent, true);
        }

        if (intent != null)
            ReleaseIntent(intent);

        var (newIntent, hasNew) = PickIntentFrom(fromCell, patchBotTile, partnerTile, excluded);
        if (!hasNew)
            return (new Vector2Int(-1, -1), null, false);

        var cell = newIntent.CurrentCell(board);
        if (cell.x < 0)
        {
            ReleaseIntent(newIntent);
            return (new Vector2Int(-1, -1), null, false);
        }

        return (cell, newIntent, true);
    }

    /// <summary>
    /// Bot vuruşunu tamamlayınca veya iptal edince çağrılır.
    /// Intent'in tipine göre doğru havuzdan rezervasyon düşer.
    /// </summary>
    public void ReleaseIntent(PatchBotIntent intent)
    {
        if (intent == null) return;

        if (intent.IsObstacle)
            Pool.ReleaseObstacle(intent.ObstacleOriginIndex);
        else if (intent.TargetTile != null)
            Pool.ReleaseTile(intent.TargetTile);

        activeBotCount = Mathf.Max(0, activeBotCount - 1);
    }

    // ─────────────────────────────────────────────
    // ESKİ API (Geriye uyumluluk — diğer kodlar bunu çağırıyor olabilir)
    // ─────────────────────────────────────────────

    public (TileView tile, int x, int y, bool hasCell) ReserveTarget(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        params TileView[] additionalExcluded)
    {
        var (intent, hasIntent) = PickIntent(patchBotTile, partnerTile, excluded, additionalExcluded);
        if (!hasIntent)
            return (null, -1, -1, false);

        var cell = intent.CurrentCell(board);
        return (intent.TargetTile, cell.x, cell.y, true);
    }

    public void ReleaseReservation(int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService != null)
        {
            int origin = obstacleService.GetObstacleOriginAt(x, y);
            if (origin >= 0 && Pool.ReservedObstacleHits(origin) > 0)
            {
                Pool.ReleaseObstacle(origin);
                activeBotCount = Mathf.Max(0, activeBotCount - 1);
                return;
            }
        }

        // Normal tile: cell üzerinden tile'ı bulup release et
        if (x >= 0 && x < board.Width && y >= 0 && y < board.Height)
        {
            Pool.ReleaseTile(board.Tiles[x, y]);
        }
        activeBotCount = Mathf.Max(0, activeBotCount - 1);
    }

    /// Engelin kalan kapasitesi (ortak havuz): anlamlı vuruş − tüm kaynakların rezervasyonları.
    public int GetEffectiveObstacleHitsRemaining(int x, int y) => Pool.ObstacleCapacityAt(x, y);

    public bool IsTileReserved(TileView tile) => Pool.IsTileReserved(tile);

    // ─────────────────────────────────────────────
    // INTERNAL: Reservation-aware target finder
    // ─────────────────────────────────────────────

    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();

    private (TileView tile, int x, int y, bool hasCell) FindTargetWithReservations(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        TileView[] additionalExcluded,
        Vector2Int fromCell)
    {
        var cargoDropPathCells = new List<(int x, int y, TileView tile)>();
        var obstacleGoalCells = new List<(int x, int y, TileView tile)>();
        var tileGoalCells = new List<(int x, int y, TileView tile)>();
        var otherObstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();
        var gelSpreadCells = new List<(int x, int y, TileView tile)>();
        var obstacleUnitCells = new Dictionary<int, List<(int x, int y, TileView tile)>>();
        var obstacleUnitIsGoal = new Dictionary<int, bool>();

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
            else if (goal.targetType == LevelGoalTargetType.Collectible && goal.collectibleId == CollectibleId.EnergyOrb)
            {
                // EnergyOrb hem EnergyContainer hem HatLauncher'dan çıkar → ikisi de goal obstacle.
                activeObstacleGoals.Add(ObstacleId.EnergyContainer);
                activeObstacleGoals.Add(ObstacleId.HatLauncher);
            }
            else if (goal.targetType == LevelGoalTargetType.Tile)
                activeTileGoals.Add(goal.tileType);
        }

        // Jel kırılmaz → jelli hücre "goal obstacle" değildir; bot bulaş taşıyorsa jelsiz alana gider.
        bool gelGoalActive = activeObstacleGoals.Remove(ObstacleId.SpreadingGel);
        bool preferNonGel = patchbotService != null
                            && patchbotService.ShouldPreferNonGelCells(patchBotTile, gelGoalActive);

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

                if (preferNonGel && patchbotService.IsGelSpreadTarget(x, y, tile)
                    && !IsExcludedTile(tile) && !IsTileReserved(tile))
                    gelSpreadCells.Add((x, y, tile));

                bool hasObstacle = board.ObstacleStateService != null
                                   && board.ObstacleStateService.GetObstacleIdAt(x, y) != ObstacleId.None;

                if (hasObstacle)
                {
                    var obstacleId = board.ObstacleStateService.GetObstacleIdAt(x, y);

                    // Cargo (exitAtBottom) KIRILMAZ; üstüne konmak faydasız. Bunun yerine
                    // düşüş yolunu açmak için ALTINDAKI normal taşı hedefle → cargo aşağı
                    // düşüp tabandan çıkar (hedef ilerler).
                    if (board.ObstacleStateService.IsExitAtBottomAt(x, y))
                    {
                        patchbotService.AddCargoDropPathTarget(x, y, partnerTile, cargoDropPathCells,
                            t => IsExcludedTile(t) || IsTileReserved(t));
                        continue;
                    }

                    // Jel kırılmaz → obstacle olarak hedef değil. Üstünde taş varsa taş olarak
                    // değerlendirilsin (bulaş taşıyan bot zaten jelsiz hücreleri tercih eder).
                    if (obstacleId == ObstacleId.SpreadingGel)
                    {
                        if (tile != null
                            && board.GridData[x, y] != null
                            && SpecialUtils.CanTargetTileContent(board, x, y)
                            && !IsExcludedTile(tile)
                            && !IsTileReserved(tile))
                        {
                            if (IsGoalTile(tile))
                                tileGoalCells.Add((x, y, tile));
                            else
                                normalCells.Add((x, y, tile));
                        }
                        continue;
                    }

                    // Uygunluk + kapasite ORTAK havuzdan (tüp tabanı, örtülü/pasif engel, kasanın gerçek
                    // kalan vuruşu, tüm kaynakların rezervasyonları). Çok-hücreli engel TEK aday: origin'in
                    // hücreleri toplanır, tarama sonunda merkeze en yakın hücre temsilci olur.
                    if (Pool.ObstacleCapacityAt(x, y) <= 0)
                        continue;

                    int unitOrigin = board.ObstacleStateService.GetObstacleOriginAt(x, y);
                    if (unitOrigin < 0) unitOrigin = x + y * board.Width;
                    if (!obstacleUnitCells.TryGetValue(unitOrigin, out var unitCells))
                    {
                        obstacleUnitCells[unitOrigin] = unitCells = new List<(int x, int y, TileView tile)>();
                        obstacleUnitIsGoal[unitOrigin] = activeObstacleGoals.Contains(obstacleId);
                    }
                    unitCells.Add((x, y, tile));
                }
                else if (tile != null
                         && board.GridData[x, y] != null
                         && SpecialUtils.CanTargetTileContent(board, x, y)
                         && !IsExcludedTile(tile))
                {
                    if (IsTileReserved(tile))
                        continue;

                    if (IsGoalTile(tile))
                        tileGoalCells.Add((x, y, tile));
                    else
                        normalCells.Add((x, y, tile));
                }
            }
        }

        // Çok-hücreli engel = tek aday (4x4 kasa 16 aday değil): temsilci = footprint merkezine en yakın hücre.
        foreach (var kv in obstacleUnitCells)
        {
            var cells = kv.Value;
            float cx = 0f, cy = 0f;
            foreach (var c in cells) { cx += c.x; cy += c.y; }
            cx /= cells.Count; cy /= cells.Count;
            var rep = cells[0];
            float best = float.MaxValue;
            foreach (var c in cells)
            {
                float d = (c.x - cx) * (c.x - cx) + (c.y - cy) * (c.y - cy);
                if (d < best) { best = d; rep = c; }
            }
            if (obstacleUnitIsGoal[kv.Key]) obstacleGoalCells.Add(rep);
            else otherObstacleCells.Add(rep);
        }

        // Hedef, "kaynaktan en uzak" veya rastgele DEĞİL; payload'ın en çok hücreye değeceği
        // (en yoğun küme) hücre seçilir. Önceki davranış (FarthestIndex) bot'u en tepedeki/
        // köşedeki tek hücreye gönderiyordu → en az hasar. Çoklu bot için, zaten rezerve edilmiş
        // hedeflere yakın adaylar cezalandırılır → botlar farklı yoğun kümelere yayılır.
        var reservedCells = Pool.ReservedCells();

        int PickIdx(List<(int x, int y, TileView tile)> list)
        {
            if (list.Count == 1) return 0;
            return HighestImpactIndex(list, reservedCells);
        }

        // Cargo düşüş yolu en yüksek öncelik: cargo başka türlü kırılamadığı için,
        // altındaki taşı açmak hedefi ilerletmenin tek yolu.
        if (cargoDropPathCells.Count > 0)
        {
            var pick = cargoDropPathCells[PickIdx(cargoDropPathCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (obstacleGoalCells.Count > 0)
        {
            var pick = obstacleGoalCells[PickIdx(obstacleGoalCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (tileGoalCells.Count > 0)
        {
            var pick = tileGoalCells[PickIdx(tileGoalCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        // Jel taşıyan bot (jel kaplama hedefi aktif): jelsiz bölgenin EN YOĞUN yerine — hedef değeri
        // olmayan diğer obstacle'lardan önce. Rezervasyon cezası botları farklı jelsiz bölgelere yayar.
        if (gelSpreadCells.Count > 0)
        {
            var pick = gelSpreadCells[PickIdx(gelSpreadCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (otherObstacleCells.Count > 0)
        {
            var pick = otherObstacleCells[PickIdx(otherObstacleCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        if (normalCells.Count > 0)
        {
            var pick = normalCells[PickIdx(normalCells)];
            return (pick.tile, pick.x, pick.y, true);
        }

        return (null, -1, -1, false);
    }

    // Payload etki yarıçapı (PulseCore 5x5 ≈ 2; line/bomb için de yoğunluk iyi bir proxy).
    private const int PatchbotImpactRadius = 2;
    private readonly List<int> impactPickBuffer = new();

    // Adayı: yarıçap içindeki AYNI kovadaki hücre sayısı (yoğunluk) eksi rezerve hedeflere
    // yakınlık cezası (botları farklı yoğun kümelere yaymak için). En yüksek skorlu seçilir,
    // eşitlikte rastgele kırılır (tekdüze olmasın).
    private int HighestImpactIndex(List<(int x, int y, TileView tile)> list, List<Vector2Int> reservedCells)
    {
        int bestScore = int.MinValue;
        impactPickBuffer.Clear();

        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];

            int density = 0;
            for (int j = 0; j < list.Count; j++)
            {
                if (j == i) continue;
                var o = list[j];
                if (Mathf.Abs(o.x - c.x) <= PatchbotImpactRadius &&
                    Mathf.Abs(o.y - c.y) <= PatchbotImpactRadius)
                    density++;
            }

            int penalty = 0;
            if (reservedCells != null)
            {
                for (int r = 0; r < reservedCells.Count; r++)
                {
                    var rc = reservedCells[r];
                    if (Mathf.Abs(rc.x - c.x) <= PatchbotImpactRadius &&
                        Mathf.Abs(rc.y - c.y) <= PatchbotImpactRadius)
                        penalty += 3;
                }
            }

            int score = density - penalty;
            if (score > bestScore)
            {
                bestScore = score;
                impactPickBuffer.Clear();
                impactPickBuffer.Add(i);
            }
            else if (score == bestScore)
            {
                impactPickBuffer.Add(i);
            }
        }

        return impactPickBuffer.Count > 0
            ? impactPickBuffer[Random.Range(0, impactPickBuffer.Count)]
            : 0;
    }
}
