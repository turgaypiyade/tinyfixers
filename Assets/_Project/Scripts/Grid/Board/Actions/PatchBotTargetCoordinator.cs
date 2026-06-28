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
                        && obstacleService.GetRemainingHitsAt(x, y) > 0
                        && !obstacleService.IsFullyDisabledAt(x, y))
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
                        && obstacleService.GetRemainingHitsAt(x, y) > 0
                        && !obstacleService.IsFullyDisabledAt(x, y))
                        return new Vector2Int(x, y);
                }
            return new Vector2Int(-1, -1);
        }

        return new Vector2Int(TargetTile.X, TargetTile.Y);
    }
}

/// <summary>
/// PatchBot'ların hedef çakışmasını önleyen koordinatör.
///
/// İki tür rezervasyon tutar:
///   1) Obstacle: origin index bazlı — multi-cell obstacle'lar aynı origin'i paylaştığı için
///      kalan vuruş sayısı origin üzerinden doğru hesaplanır.
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

    // obstacle origin index → bu origin'e atanmış PatchBot sayısı
    private readonly Dictionary<int, int> obstacleReservationsByOrigin = new();

    // Normal tile rezervasyonları — TileView referansı bazlı (taş düşse de takip edilir).
    private readonly HashSet<TileView> tileReservations = new();

    private int activeBotCount;
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

            if (origin >= 0)
            {
                if (!obstacleReservationsByOrigin.ContainsKey(origin))
                    obstacleReservationsByOrigin[origin] = 0;
                obstacleReservationsByOrigin[origin]++;
            }
        }
        else
        {
            intent.ObstacleOriginIndex = -1;
            intent.TargetTile = pick.tile;

            if (pick.tile != null)
                tileReservations.Add(pick.tile);
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
        {
            int origin = intent.ObstacleOriginIndex;
            if (origin >= 0 && obstacleReservationsByOrigin.ContainsKey(origin))
            {
                obstacleReservationsByOrigin[origin]--;
                if (obstacleReservationsByOrigin[origin] <= 0)
                    obstacleReservationsByOrigin.Remove(origin);
            }
        }
        else if (intent.TargetTile != null)
        {
            tileReservations.Remove(intent.TargetTile);
        }

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
            if (origin >= 0 && obstacleReservationsByOrigin.ContainsKey(origin))
            {
                obstacleReservationsByOrigin[origin]--;
                if (obstacleReservationsByOrigin[origin] <= 0)
                    obstacleReservationsByOrigin.Remove(origin);
                activeBotCount = Mathf.Max(0, activeBotCount - 1);
                return;
            }
        }

        // Normal tile: cell üzerinden tile'ı bulup release et
        if (x >= 0 && x < board.Width && y >= 0 && y < board.Height)
        {
            var tile = board.Tiles[x, y];
            if (tile != null)
                tileReservations.Remove(tile);
        }
        activeBotCount = Mathf.Max(0, activeBotCount - 1);
    }

    public int GetEffectiveObstacleHitsRemaining(int x, int y)
    {
        var obstacleService = board.ObstacleStateService;
        if (obstacleService == null) return 0;

        // FullyDisabled stage hiçbir context'ten hit almaz — efektif hedef değil.
        if (obstacleService.IsFullyDisabledAt(x, y))
            return 0;

        // FullyDisabled olan final stage'ler (EnergyContainer exhausted stage gibi)
        // gerçek hit sayımından dışlanır; aksi halde birden fazla PatchBot aynı
        // kapsiteye sahip olmayan hedefe yönlendirilebilir.
        int actual = obstacleService.GetActiveMeaningfulHitsAt(x, y);
        int origin = obstacleService.GetObstacleOriginAt(x, y);
        if (origin < 0) return actual;

        int reserved = obstacleReservationsByOrigin.ContainsKey(origin)
            ? obstacleReservationsByOrigin[origin]
            : 0;

        return actual - reserved;
    }

    public bool IsTileReserved(TileView tile)
    {
        return tile != null && tileReservations.Contains(tile);
    }

    // ─────────────────────────────────────────────
    // INTERNAL: Reservation-aware target finder
    // ─────────────────────────────────────────────

    private readonly List<TopHudController.ActiveGoal> activeGoalsBuffer = new();

    // Şu an rezerve edilmiş hedeflerin board üzerindeki hücrelerini döner.
    private List<Vector2Int> GetReservedTargetCells()
    {
        var cells = new List<Vector2Int>(tileReservations.Count + obstacleReservationsByOrigin.Count);

        foreach (var tile in tileReservations)
        {
            if (tile != null)
                cells.Add(new Vector2Int(tile.X, tile.Y));
        }

        if (obstacleReservationsByOrigin.Count > 0)
        {
            var obstacleService = board.ObstacleStateService;
            if (obstacleService != null)
            {
                foreach (var kvp in obstacleReservationsByOrigin)
                {
                    if (kvp.Value <= 0) continue;
                    bool found = false;
                    for (int x = 0; x < board.Width && !found; x++)
                        for (int y = 0; y < board.Height && !found; y++)
                            if (obstacleService.GetObstacleOriginAt(x, y) == kvp.Key)
                            { cells.Add(new Vector2Int(x, y)); found = true; }
                }
            }
        }

        return cells;
    }

    private (TileView tile, int x, int y, bool hasCell) FindTargetWithReservations(
        TileView patchBotTile,
        TileView partnerTile,
        HashSet<TileView> excluded,
        TileView[] additionalExcluded,
        Vector2Int fromCell)
    {
        var obstacleGoalCells = new List<(int x, int y, TileView tile)>();
        var tileGoalCells = new List<(int x, int y, TileView tile)>();
        var otherObstacleCells = new List<(int x, int y, TileView tile)>();
        var normalCells = new List<(int x, int y, TileView tile)>();

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
                    var obstacleId = board.ObstacleStateService.GetObstacleIdAt(x, y);

                    // Tube: only the base (origin) cell is a valid target.
                    if (obstacleId == ObstacleId.Tube)
                    {
                        int origin = board.ObstacleStateService.GetObstacleOriginAt(x, y);
                        if (origin < 0 || origin % board.Width != x || origin / board.Width != y)
                            continue;
                    }

                    int effectiveHits = GetEffectiveObstacleHitsRemaining(x, y);
                    if (effectiveHits <= 0)
                        continue;

                    bool isObstacleGoalCell = activeObstacleGoals.Contains(obstacleId);

                    if (isObstacleGoalCell)
                        obstacleGoalCells.Add((x, y, tile));
                    else
                        otherObstacleCells.Add((x, y, tile));
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

        // Hedef, "kaynaktan en uzak" veya rastgele DEĞİL; payload'ın en çok hücreye değeceği
        // (en yoğun küme) hücre seçilir. Önceki davranış (FarthestIndex) bot'u en tepedeki/
        // köşedeki tek hücreye gönderiyordu → en az hasar. Çoklu bot için, zaten rezerve edilmiş
        // hedeflere yakın adaylar cezalandırılır → botlar farklı yoğun kümelere yayılır.
        var reservedCells = GetReservedTargetCells();

        int PickIdx(List<(int x, int y, TileView tile)> list)
        {
            if (list.Count == 1) return 0;
            return HighestImpactIndex(list, reservedCells);
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
