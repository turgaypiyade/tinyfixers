using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tahta akışının tek kuralı (BoardController.useFlowPump; kesintisiz düşüş şart):
///
///   Her kare, TUTULMAYAN hücrelerde
///     1. doldurulabilir boşluk varsa yerçekimi HEMEN planlanır (taşlar bir sonraki karede akar),
///     2. taşları oturmuş her eşleşme grubu KENDİ BAŞINA temizlenir (diğer grupları/düşüşleri beklemez).
///
/// Tutulan hücre (CellHold) = uçuştaki bir işin hâlâ KONUMLA dokunacağı hücre: temizlenmekte olan taş,
/// pulse alanı, line süpürmesi, anchor'lı special, swap, booster, override hedefi. Yerçekimi oraya taş
/// koymaz / oradan taş almaz, eşleşme oraya dokunmaz. Special'ın görseli yalnız kendi tuttuğu bölgeyi
/// meşgul eder; tahtanın geri kalanı hiçbir global kapıyı beklemez.
///
/// Pompa karar vermez, yalnız başlatır: düşüş = DetachedFall job, grup temizliği = FlowClear job. İkisi
/// de level-end'in beklediği ActiveBackgroundJobs'a sayılır; resolve döngüsü IsQuiet'i bekleyip sakin
/// tahta işlerine (oil yayılımı, roket, deadlock) geçer.
/// </summary>
internal sealed class BoardFlowPump
{
    private readonly BoardController board;
    private readonly HashSet<TileView> matched = new();
    private readonly HashSet<TileView> visited = new();
    private readonly Stack<TileView> stack = new();
    private readonly List<TileView> group = new();
    private int lastDiagonalTry = -1;

    public BoardFlowPump(BoardController board) => this.board = board;

    public void Tick()
    {
        if (board.CascadeLogic == null || board.MatchFinder == null || board.Tiles == null)
            return;

        bool worked = false;

        if (HasOpenCell() && ShouldPlanGravity())
        {
            var falls = board.CascadeLogic.CalculateCascades();
            foreach (var fall in falls)
                board.StartFlowAction(fall, BoardController.BoardJobKind.DetachedFall);
            if (falls.Count > 0)
            {
                board.RefreshAllSortingOrders();
                worked = true;
            }
        }

        if (ClearSettledGroups())
            worked = true;

        if (worked)
            board.NoteFlowPumpWork();
    }

    // Dikey dolum gereken boşluk varsa her zaman planla. Yalnız çapraz dolabilecek boşluk varsa
    // (üstü kapalı cep) ancak bir taş oturduğunda / hücre temizlendiğinde dene: ölü cepte her kare plan
    // üretmesin. (Havadaki taşların çapraz kayması zaten dikey planla birlikte önceden planlanır.)
    private bool ShouldPlanGravity()
    {
        if (board.CascadeLogic.HasAnyResolvableEmptyPlayableCell())
            return true;

        int changes = board.FallMotion.LandedCount + board.ClearedCellCount;
        if (changes == lastDiagonalTry)
            return false;

        lastDiagonalTry = changes;
        return board.CascadeLogic.HasAnyEmptyPlayableCell();
    }

    /// <summary>Akış durdu mu: düşüş, temizlik, boşluk ve eşleşme yok.</summary>
    public bool IsQuiet =>
        board.CascadeLogic != null
        && !board.CascadeLogic.IsGravityBusy
        && board.FlowJobsInFlight == 0
        && !board.IsActionSequencePlaying
        && !board.CascadeLogic.HasAnyResolvableEmptyPlayableCell()
        && board.MatchFinder.FindAllMatches(logDiagnostics: false).Count == 0;

    // Ucuz ön kontrol (her kare): taşsız, hole/blocker olmayan hücre var mı? Yoksa segment analizine gerek yok.
    private bool HasOpenCell()
    {
        var tiles = board.Tiles;
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
                if (tiles[x, y] == null && !board.IsMaskHoleCell(x, y) && !board.IsObstacleBlockedCell(x, y))
                    return true;
        return false;
    }

    private bool ClearSettledGroups()
    {
        var matches = board.MatchFinder.FindAllMatches(logDiagnostics: false);
        if (matches.Count == 0)
            return false;

        matched.Clear();
        foreach (var data in matches)
        {
            var tile = board.GetTileViewAt(data.X, data.Y);
            if (tile != null)
                matched.Add(tile);
        }

        bool started = false;
        visited.Clear();
        foreach (var seed in matched)
        {
            if (!visited.Add(seed))
                continue;

            CollectGroup(seed);
            if (IsGroupSettled() && board.StartFlowClear(group))
                started = true;
        }

        return started;
    }

    // Aynı renkte, 4-komşu bağlı eşleşme taşları tek grup (L/T şekilleri special oluşumu için bütün kalır).
    private void CollectGroup(TileView seed)
    {
        group.Clear();
        stack.Clear();
        stack.Push(seed);
        var type = seed.GetTileType();

        while (stack.Count > 0)
        {
            var tile = stack.Pop();
            group.Add(tile);
            Visit(tile.X + 1, tile.Y);
            Visit(tile.X - 1, tile.Y);
            Visit(tile.X, tile.Y + 1);
            Visit(tile.X, tile.Y - 1);
        }

        void Visit(int x, int y)
        {
            var next = board.GetTileViewAt(x, y);
            if (next != null && matched.Contains(next) && next.GetTileType() == type && visited.Add(next))
                stack.Push(next);
        }
    }

    private bool IsGroupSettled()
    {
        foreach (var tile in group)
            if (!IsSettled(tile))
                return false;
        return true;
    }

    private bool IsSettled(TileView tile)
    {
        if (board.IsCellHeld(tile.X, tile.Y))
            return false;
        if (tile.RuntimeState == TileRuntimeState.Clearing || tile.RuntimeState == TileRuntimeState.Swapping)
            return false;
        // Varış (lead) ateşlendiyse taş hücresine giriyor → temizlik ona yetişir.
        if (!tile.HasArrivedForPlannedFall && !board.IsTileReadyForContinuousMatch(tile))
            return false;
        return !WillFall(tile.X, tile.Y);
    }

    // Altında (hole'lardan geçerek) boş bir taş yuvası varsa taş düşecek; o grup düşüşten sonra
    // yeniden değerlendirilir. Tutulan boş hücre de sayılır: bırakılınca taş oraya iner.
    private bool WillFall(int x, int y)
    {
        var obstacles = board.ObstacleStateService;
        for (int below = y + 1; below < board.Height; below++)
        {
            if (board.IsObstacleBlockedCell(x, below) || (obstacles != null && obstacles.HoldsTileAt(x, below)))
                return false;
            if (board.IsMaskHoleCell(x, below))
                continue;
            return board.Tiles[x, below] == null;
        }
        return false;
    }
}

/// <summary>
/// Uçuştaki bir işin konumla dokunacağı hücreler (bkz. BoardFlowPump). Dispose tümünü bırakır (bir kez).
/// releaseWhenCleared: hücrenin eski taşı EKRANDAN kalkınca (BoardController.ReleaseTile / ClearCellVisualOnly)
/// o hücre bırakılır → kırılan taşın yeri efektin geri kalanını beklemeden, ama kaybolan taşın üstüne
/// binmeden dolar. (Veri silinmesi yetmez: pop/implode/uçuş animasyonu veriden sonra sürer.)
/// </summary>
internal sealed class CellHold : IDisposable
{
    private BoardController board;
    private readonly HashSet<Vector2Int> cells;
    private readonly int epoch;

    internal CellHold(BoardController board, HashSet<Vector2Int> cells, int epoch)
    {
        this.board = board;
        this.cells = cells;
        this.epoch = epoch;
    }

    internal void Release(Vector2Int cell)
    {
        if (board != null && cells.Remove(cell))
            board.ReleaseHeldCell(cell, epoch);
    }

    internal bool Contains(Vector2Int cell, int currentEpoch) =>
        board != null && epoch == currentEpoch && cells.Contains(cell);

    public void Dispose()
    {
        var owner = board;
        if (owner == null)
            return;

        board = null;
        foreach (var cell in cells)
            owner.ReleaseHeldCell(cell, epoch);
        cells.Clear();
        owner.ForgetClearReleasedHold(this);
    }
}
