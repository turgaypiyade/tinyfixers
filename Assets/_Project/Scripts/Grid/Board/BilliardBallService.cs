using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bilardo topu + cep (ObstacleId.BilliardBall / BilliardPocket; Docs/BilliardBall_Plan.md) — tahta düzeyi:
///  • Cebe uzaklık haritası: STATİK topolojiden (yalnız mask hole + kenar + cep; kırılabilir engeller zamanla
///    açılır sayılır). Yuvarlanmada eşitlik bozucu, takılma tespitinde ulaşılabilirlik.
///  • Cebe giriş: veri plan anında toplanır (hedef +1); görsel ghost'ta yuvarlanır, halkanın arkasına batar,
///    hedef slotuna uçar (goal-orb uçuşu gibi non-blocking; level-end bekler).
///  • Takılma: kalan top hedefi, cebe hâlâ ulaşabilen top sayısını aşarsa hamle bitmeden fail ("topu kurtar"
///    devamı: takılı top cebe ulaşabilen en yakın hücreye taşınır).
/// Yuvarlanma kuralları planlayıcıdadır (CascadeLogic.Billiard).
/// </summary>
internal sealed class BilliardBallService
{
    private readonly BoardController board;
    private int[,] pocketDistance;
    private bool levelHasBilliard;
    private readonly Queue<(int x, int y)> bfsQueue = new();
    private readonly List<TopHudController.ActiveGoal> goalsBuffer = new(4);
    private readonly HashSet<TileView> liftingBalls = new();
    // Yana yuvarlanmakta olan toplar → yön (+1 sağ, −1 sol). Havadayken önü boşalırsa yol aynı yönde uzar.
    private readonly Dictionary<TileView, int> rollDirs = new();

    public bool TryGetRollDir(TileView ball, out int dir) => rollDirs.TryGetValue(ball, out dir);
    public void SetRollDir(TileView ball, int dir) => rollDirs[ball] = dir;
    public void ClearRollDir(TileView ball) => rollDirs.Remove(ball);

    /// Takılma fail'i gösterilirken true: fail ekranı "topu kurtar" moduna geçer.
    public bool StuckFailActive { get; private set; }

    public BilliardBallService(BoardController board) => this.board = board;

    public bool HasBalls => levelHasBilliard;

    /// Kurtarma uçuşundaki top: planlayıcı onu yuvarlamaz (uçuşla düşüş motoru aynı taşı sürmesin).
    public bool IsLifting(TileView ball) => liftingBalls.Contains(ball);

    /// Level kurulurken: haritayı bayatlat, level'da top/cep var mı bak (yoksa her plan ucuzca atlanır).
    public void ResetForLevel(LevelData level)
    {
        pocketDistance = null;
        StuckFailActive = false;
        liftingBalls.Clear();
        rollDirs.Clear();
        levelHasBilliard = false;
        if (level == null || level.obstacles == null) return;
        foreach (int id in level.obstacles)
            if (id == (int)ObstacleId.BilliardBall || id == (int)ObstacleId.BilliardPocket)
            {
                levelHasBilliard = true;
                return;
            }
    }

    // ── Statik topoloji ──────────────────────────────────────────

    /// Hücrenin altındaki ilk mask-hole olmayan hücre (taşlar hole'dan düşerek geçer).
    public bool TryGetCellBelow(int x, int y, out Vector2Int below)
    {
        for (int by = y + 1; by < board.Height; by++)
        {
            if (board.IsMaskHoleCell(x, by)) continue;
            below = new Vector2Int(x, by);
            return true;
        }
        below = default;
        return false;
    }

    /// Topun bu hücreden en yakın cebe kaç adımda ulaşabileceği (statik yol yoksa int.MaxValue).
    public int DistanceToPocket(int x, int y)
    {
        EnsureDistanceMap();
        return pocketDistance[x, y];
    }

    public bool CanReachPocket(int x, int y) => DistanceToPocket(x, y) != BilliardReach.Unreachable;

    // Statik harita (BilliardReach): hole/cep hiç değişmez → level başına bir kez kurulur.
    private void EnsureDistanceMap()
    {
        if (pocketDistance != null) return;
        pocketDistance = new int[board.Width, board.Height];
        var obs = board.ObstacleStateService;
        BilliardReach.ComputePocketDistances(board.Width, board.Height,
            board.IsMaskHoleCell, (x, y) => obs != null && obs.IsBilliardPocketAt(x, y),
            pocketDistance, bfsQueue);
    }

    // ── Cebe giriş ───────────────────────────────────────────────

    /// Plan sırasında (board.Tiles boşken) cebe giren topun verisini topla → OnObstacleDestroyed → hedef +1.
    public void CollectBallData(Vector2Int cell)
    {
        if (board.ObstacleStateService != null && board.ObstacleStateService.CollectBilliardBallAt(cell.x, cell.y))
            board.SetHoleStateFromObstacle(cell.x, cell.y);
    }

    /// Topun görselini ghost'a devret: yol boyunca yuvarlan → cebe bat → hedef slotuna uç. Gerçek taş hemen havuza.
    public void PlayPocketed(TileView view, Vector2Int[] path)
    {
        if (view == null || !view || path == null || path.Length < 2) return;
        board.StartCoroutine(PocketRoutine(view, path));
    }

    private IEnumerator PocketRoutine(TileView view, Vector2Int[] path)
    {
        board.BeginGoalOrbFlight();
        try
        {
            var fly = board.GoalFlyFx;
            var def = GetDef(ObstacleId.BilliardBall);
            var pocketDef = GetDef(ObstacleId.BilliardPocket);
            RectTransform goalSlot = null;
            board.TopHud?.TryGetGoalTargetRectForObstacle(ObstacleId.BilliardBall, out goalSlot);

            // Yol noktaları dünya koordinatında: önce topun ŞU ANKİ yeri (yuvarlanırken cebe varan top havada olabilir),
            // sonra yolun hücreleri. Hücre konumu = taşın kendi konumundan anchored farkı.
            var rt = view.RectTransform;
            Vector3 current = rt.TransformPoint(rt.rect.center);
            var parent = rt.parent as RectTransform;
            var points = new Vector3[path.Length + 1];
            points[0] = current;
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 delta = view.GetFallCellPosition(path[i].x, path[i].y, board.TileSize) - rt.anchoredPosition;
                points[i + 1] = current + (parent != null ? parent.TransformVector(delta) : (Vector3)delta);
            }

            // Cebin tahtadaki görseli: halka ghost'u tam onun üstüne hizalanır.
            var pocketCell = path[path.Length - 1];
            RectTransform pocketRect = null;
            if (board.ObstacleViewByOriginLookup != null)
            {
                var img = board.ObstacleViewByOriginLookup(pocketCell.y * board.Width + pocketCell.x);
                if (img != null) pocketRect = img.rectTransform;
            }

            // StartCoroutine ilk yield'e kadar senkron koşar: ghost sprite + konumu ŞİMDİ yakalar.
            // Sonra gerçek taş havuza (iade görseli sıfırlar; çift top görünmez).
            Coroutine running = fly != null
                ? board.StartCoroutine(fly.PlayBilliardPocket(view, points, Aux(def, 1), Aux(def, 2), Aux(def, 0),
                    Aux(pocketDef, 0), pocketRect, goalSlot))
                : null;
            board.ReleaseTile(view);

            if (running != null)
                yield return running;
        }
        finally
        {
            board.EndGoalOrbFlight();
        }
    }

    private ObstacleDef GetDef(ObstacleId id)
    {
        var lib = board.ActiveLevelData != null ? board.ActiveLevelData.obstacleLibrary : null;
        return lib != null ? lib.Get(id) : null;
    }

    private static Sprite Aux(ObstacleDef def, int index)
        => def != null && def.auxiliarySprites != null && def.auxiliarySprites.Count > index
            ? def.auxiliarySprites[index] : null;

    // ── Takılma (erken fail) + kurtarma ──────────────────────────

    /// Hamle bitip tahta durduğunda: kalan top hedefi, cebe ulaşabilen top sayısından fazlaysa erken fail.
    public void CheckStuckAfterMove()
    {
        if (!levelHasBilliard || StuckFailActive || board.ObstacleStateService == null) return;

        int remaining = RemainingBallGoal();
        if (remaining <= 0) return;

        int reachable = 0;
        ForEachBall((x, y) => { if (CanReachPocket(x, y)) reachable++; });
        if (reachable >= remaining) return;

        StuckFailActive = true;
        board.RequestLevelFail();
    }

    private int RemainingBallGoal()
    {
        var hud = board.TopHud;
        if (hud == null) return 0;
        goalsBuffer.Clear();
        hud.GetActiveGoals(goalsBuffer);
        foreach (var g in goalsBuffer)
            if (g.targetType == LevelGoalTargetType.Obstacle && g.obstacleId == ObstacleId.BilliardBall)
                return g.remaining;
        return 0;
    }

    private void ForEachBall(System.Action<int, int> visit)
    {
        var obs = board.ObstacleStateService;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (obs.IsBilliardBallAt(x, y))
                    visit(x, y);
    }

    /// "Topu kurtar" devamı (bir kare sonra): fail modu hemen kapanır, takılı toplar sonraki karede taşınır.
    public IEnumerator RescueNextFrame()
    {
        StuckFailActive = false;
        yield return null;
        RescueStuckBalls();
    }

    /// Cebe ulaşamayan her top, cebe ulaşabilen en yakın normal taşla yer değiştirir (top yaylanarak oraya
    /// uçar, taş topun eski yerinde belirir). En az bir top kurtarıldıysa true.
    public bool RescueStuckBalls()
    {
        StuckFailActive = false;
        if (board.ObstacleStateService == null) return false;

        var stuck = new List<Vector2Int>();
        ForEachBall((x, y) => { if (!CanReachPocket(x, y)) stuck.Add(new Vector2Int(x, y)); });

        bool any = false;
        foreach (var from in stuck)
        {
            if (!TryFindRescueCell(from, out var to)) continue;
            SwapBallWithTile(from, to);
            any = true;
        }
        return any;
    }

    // Cebe ulaşabilen, içinde engelsiz normal taş olan en yakın hücre; eşitlikte daha yukarıdaki.
    private bool TryFindRescueCell(Vector2Int from, out Vector2Int best)
    {
        best = default;
        float bestScore = float.MaxValue;
        var obs = board.ObstacleStateService;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (!CanReachPocket(x, y) || obs.HasObstacleAt(x, y)) continue;
                var tile = board.Tiles[x, y];
                if (tile == null || !tile || tile.GetSpecial() != TileSpecial.None || !tile.IsRuntimeIdle) continue;
                float score = (new Vector2(x, y) - from).sqrMagnitude + y * 0.01f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = new Vector2Int(x, y);
            }
        return bestScore < float.MaxValue;
    }

    private void SwapBallWithTile(Vector2Int from, Vector2Int to)
    {
        var ball = board.Tiles[from.x, from.y];
        var tile = board.Tiles[to.x, to.y];

        board.ObstacleStateService.MoveObstacle(from.x, from.y, to.x, to.y);
        board.Tiles[to.x, to.y] = ball;
        board.Tiles[from.x, from.y] = tile;
        if (tile != null)
        {
            tile.SetCoords(from.x, from.y);
            tile.SnapToGrid(board.TileSize);
            tile.RectTransform.localScale = Vector3.zero;   // LiftRoutine büyüterek belirtir
        }
        board.SyncTileData(from.x, from.y);
        board.SyncTileData(to.x, to.y);
        board.SetHoleStateFromObstacle(from.x, from.y);
        board.SetHoleStateFromObstacle(to.x, to.y);
        board.RefreshTileObstacleVisual(tile);
        board.RefreshTileObstacleVisual(ball);

        if (ball != null)
        {
            Vector2 start = ball.RectTransform.anchoredPosition;
            ball.SetCoords(to.x, to.y);
            board.StartCoroutine(LiftRoutine(ball, tile, start, ball.GetFallCellPosition(to.x, to.y, board.TileSize)));
        }
    }

    // Kurtarma uçuşu: top yaylanarak (yukarı kavis) yeni hücresine iner. Level-end bekler. İnince tahta yeniden
    // çözülür (top cebe doğru yuvarlanabilir).
    private IEnumerator LiftRoutine(TileView ball, TileView swapped, Vector2 start, Vector2 end)
    {
        board.BeginGoalOrbFlight();
        liftingBalls.Add(ball);
        try
        {
            const float duration = 0.55f;
            float arc = board.TileSize * 1.6f;
            var rt = ball.RectTransform;
            for (float t = 0f; t < duration && ball != null && ball; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                float e = k * k * (3f - 2f * k);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * arc);
                if (swapped != null && swapped)
                    swapped.RectTransform.localScale = Vector3.one * Mathf.Clamp01(k * 1.6f);
                yield return null;
            }
            if (ball != null && ball)
                ball.SnapToGrid(board.TileSize);
            if (swapped != null && swapped)
                swapped.RectTransform.localScale = Vector3.one;
        }
        finally
        {
            liftingBalls.Remove(ball);
            board.EndGoalOrbFlight();
            board.RequestResolveAfterActionSequence();
        }
    }
}
