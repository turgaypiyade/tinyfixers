using System.Collections.Generic;
using UnityEngine;

public class CascadeLogic
{
    private readonly BoardController board;

    // True iken diagonal slide tamamen kapalı; taşlar yalnızca düz düşer.
    // Override+PulseCore gibi anchor'lı (pending-triggered) special zincirlerinde kullanılır:
    // anchor'lı special'lar geçici blocker gibi davrandığı için etraflarındaki taşlar diagonal
    // kayıp "engel yokken kayıyor" gibi kötü bir görüntü oluşturuyordu. Zincir boyunca düz düşüş.
    public bool SuppressDiagonalSlides { get; set; }

    // Buffer for active goals
    private readonly List<TopHudController.ActiveGoal> _activeGoalsBuffer = new List<TopHudController.ActiveGoal>(4);

    public CascadeLogic(BoardController board)
    {
        this.board = board;
    }

    private class VirtualTile
    {
        public TileView View;
        public TileType SpawnType;
        public bool IsSpawned;

        public bool IsMovableObstacle;
        public ObstacleId SpawnObstacleId;

        // Cargo (exitAtBottom): yalnızca düz aşağı düşer, asla diagonal kaymaz.
        public bool IsStraightFallOnly;

        // How many times this tile has slid diagonally in this cascade simulation.
        // Capped by BoardController.MaxDiagonalSlidesPerCascade to limit spread while
        // still allowing tiles to reach their rest position in fewer cascade rounds.
        public int DiagonalSlideCount;

        public List<Vector2Int> Path = new List<Vector2Int>();
    }

    // Refill'de spawn tipi seçimi: 4+ aynı-renk run (special eşiği) oluşturmayı önler.
    // Aşağıdaki (toY+1..) ve soldaki (x-1..) hücreler bu noktada kesinleşmiş durumda
    // (kolon x sırayla, taşlar altta oturmuş). Üstteki spawn'lar da bu run'ı görüp
    // uzatmayı reddedeceği için dikey run doğal olarak MAX_REFILL_RUN'da kapanır.
    private const int MAX_REFILL_RUN = 3; // 3'lü match serbest (cascade), 4+ (special) engelli

    private TileType PickRefillType(VirtualTile[,] vb, int x, int toY)
    {
        var pool = board.RandomPool;
        if (pool == null || pool.Length == 0)
            return TileType.Gear;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            var cand = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (!WouldExtendRunTooFar(vb, x, toY, cand))
                return cand;
        }

        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }

    private bool WouldExtendRunTooFar(VirtualTile[,] vb, int x, int toY, TileType cand)
    {
        // Dikey: aşağıya doğru ardışık aynı renk say.
        int down = 0;
        for (int yy = toY + 1; yy < board.Height; yy++)
        {
            if (TryGetVirtualColor(vb, x, yy, out var t) && t == cand) down++;
            else break;
        }
        if (down + 1 > MAX_REFILL_RUN) return true; // aday dahil >= 4 → engelle

        // Yatay: sola doğru ardışık aynı renk say (sağ komşular henüz spawn olmadı).
        int left = 0;
        for (int xx = x - 1; xx >= 0; xx--)
        {
            if (TryGetVirtualColor(vb, xx, toY, out var t) && t == cand) left++;
            else break;
        }
        if (left + 1 > MAX_REFILL_RUN) return true;

        return false;
    }

    // Bir sanal hücrenin match'lenebilir rengini döndürür. Obstacle/movable/boş = renk yok.
    private bool TryGetVirtualColor(VirtualTile[,] vb, int x, int y, out TileType type)
    {
        type = default;
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;

        var vt = vb[x, y];
        if (vt == null || vt.IsMovableObstacle) return false;

        if (vt.IsSpawned)
        {
            type = vt.SpawnType;
            return true;
        }

        if (vt.View == null) return false;

        type = vt.View.GetTileType();
        return true;
    }

    public List<BoardAction> CalculateCascades()
    {
        board.IncrementFallGeneration();

        VirtualTile[,] virtualBoard = new VirtualTile[board.Width, board.Height];

        // 1. Initialize Virtual Board
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                var view = board.Tiles[x, y];
                if (view != null)
                {
                    view.MarkPlannedToMoveThisFallPass(false);
                    virtualBoard[x, y] = new VirtualTile
                    {
                        View = view,
                        IsSpawned = false,
                        IsStraightFallOnly = board.ObstacleStateService != null
                                             && board.ObstacleStateService.IsExitAtBottomAt(x, y),
                        Path = new List<Vector2Int> { new Vector2Int(x, y) }
                    };
                    board.Tiles[x, y] = null; // Clear actual board, we will re-assign at the end
                }
            }
        }

        HashSet<Vector2Int> verticalOnlyGaps = new HashSet<Vector2Int>();
        bool changed = true;

        // SIMULATION LOOP
        const int MAX_ITERATIONS = 32;
        int iter = 0;
        bool spawnedMovableThisPass = false;
        Dictionary<ObstacleId, int> spawnedMovableCounts = new Dictionary<ObstacleId, int>();
        cargoSpawnColumnsThisPass.Clear();

        while (changed && iter < MAX_ITERATIONS)
        {
            changed = false;
            iter++;

            // Step 1: Vertical Collapse & Spawn
            for (int x = 0; x < board.Width; x++)
            {
                changed |= ProcessVerticalGravityAndSpawn(virtualBoard, x, ref spawnedMovableThisPass, spawnedMovableCounts);
            }

            // Step 2: Diagonal Slide
            // SuppressDiagonalSlides açıksa hiç diagonal yapma — yalnızca düz düşüş (anchor'lı
            // pulse zincirinde temiz görüntü için).
            // Aksi hâlde: önce SADECE normal taşlar diagonal kaysın. Special'lar düz inmeyi
            // tercih eder; yalnızca normal taşlarla hiç ilerleme olmazsa (son çare) special kayar.
            bool slided = false;
            if (!SuppressDiagonalSlides)
            {
                slided = DoDiagonalSlidePass(virtualBoard, verticalOnlyGaps, skipSpecials: true);
                if (!slided)
                    slided = DoDiagonalSlidePass(virtualBoard, verticalOnlyGaps, skipSpecials: false);
            }
            changed |= slided;

            // Prune unfillable VerticalOnly gaps
            PruneVerticalOnlyGaps(virtualBoard, verticalOnlyGaps);
        }

        // COMPILE ACTION
        var action = new FallAction();

        // Step 3a: Movable obstacle VERİ hareketlerini önce topla ve TEK batch'te uygula.
        // Compile döngüsü içinde tek tek MoveObstacle çağırmak kolon işleme sırasına
        // duyarlıydı: çapraz kayan bir movable'ın hedefi, henüz taşınmamış başka bir
        // movable'ın kaynağıysa canlı veri eziliyordu (bkz. MoveMovablesBatch). Ayrıca
        // batch'in spawn kayıtlarından ÖNCE koşması, spawn hedeflerinin taşınıp gitmiş
        // eski movable verisine çarpmamasını sağlar.
        if (board.ObstacleStateService != null)
        {
            List<(Vector2Int from, Vector2Int to)> movableMoves = null;
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var vTile = virtualBoard[x, y];
                    if (vTile == null || vTile.IsSpawned || vTile.View == null) continue;

                    var from = vTile.Path[0];
                    if (from.x == x && from.y == y) continue;
                    if (!board.ObstacleStateService.IsMovableObstacleAt(from.x, from.y)) continue;

                    (movableMoves ??= new List<(Vector2Int, Vector2Int)>()).Add((from, new Vector2Int(x, y)));
                }
            }
            if (movableMoves != null)
                board.ObstacleStateService.MoveMovablesBatch(movableMoves);
        }

        // Step 3: Apply virtual board back to actual board
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = board.Height - 1; y >= 0; y--)
            {
                var vTile = virtualBoard[x, y];
                if (vTile == null)
                {
                    board.Tiles[x, y] = null;
                    board.SyncTileData(x, y);
                    continue;
                }

                var compressedPath = CompressPath(vTile.Path);
                int finalX = compressedPath[compressedPath.Count - 1].x;
                int finalY = compressedPath[compressedPath.Count - 1].y;

                TileView view = vTile.View;

                if (view == null)
                {
                    // Create newly spawned tile!
                    if (vTile.IsMovableObstacle)
                    {
                        view = SpawnMovableObstacleTileForFall(finalX, finalY, compressedPath[0].x, compressedPath[0].y, vTile.SpawnObstacleId);
                    }
                    else
                    {
                        var go = board.AcquireTileObject(board.Parent);   // havuz açıksa iadeden, yoksa Instantiate
                        view = go.GetComponent<TileView>();
                        view.Init(board, finalX, finalY);
                        board.ConfigureTileView(view);
                        view.SetCoords(compressedPath[0].x, compressedPath[0].y); // start coord visually
                        view.SnapToGrid(board.TileSize);
                        
                        view.SetCoords(finalX, finalY); // final coord internally
                        view.SetType(vTile.SpawnType);
                        view.SetSpecial(TileSpecial.None);
                        board.RefreshTileObstacleVisual(view);
                    }
                }
                if (view != null)
                {
                    view.MarkPlannedToMoveThisFallPass(true);
                    board.Tiles[finalX, finalY] = view;
                    view.SetCoords(finalX, finalY);

                    // REGISTER newly spawned movable obstacles
                    if (vTile.IsSpawned && vTile.IsMovableObstacle && board.ObstacleStateService != null)
                    {
                        board.ObstacleStateService.TrySpawnSingleCellObstacleAt(finalX, finalY, vTile.SpawnObstacleId);
                    }
                }
                else
                {
                    board.Tiles[finalX, finalY] = null;
                }
                board.SyncTileData(finalX, finalY);

                if (view == null) continue;

                if (compressedPath.Count > 1)
                {
                    // Movable obstacle veri hareketi Step 3a'da batch olarak uygulandı.

                    // Calculate segments duration
                    float[] segmentDurations = new float[compressedPath.Count - 1];
                    for (int i = 0; i < compressedPath.Count - 1; i++)
                    {
                        segmentDurations[i] = board.GetFallDurationForMove(compressedPath[i].x, compressedPath[i].y, compressedPath[i + 1].x, compressedPath[i + 1].y);
                    }

                    // Settle logic (only if resting on something solid that isn't moving)
                    bool useSettle = false;
                    float settleDur = board.FallSettleDuration;
                    float settleStr = board.FallSettleStrength;

                    if (board.ShouldEnableFallSettleThisPass())
                    {
                        int belowY = finalY + 1;
                        bool hasSupport = false;

                        if (belowY >= board.Height) {
                            hasSupport = true;
                        } else if (IsGravityBlockedCell(finalX, belowY)) {
                            hasSupport = true;
                        } else {
                            var belowVTile = virtualBoard[finalX, belowY];
                            // Altında oturan bir taş varsa (hareketli VEYA sabit) → squash yapsın.
                            // Böylece squash kolonda YUKARI DOĞRU zincirlenir: alttaki önce iner+squash,
                            // üstteki ona değince aynı squash'ı yapar. (Eskiden yalnız sabit-alt taş
                            // squash yapıyordu → toptan düşen kolonda sadece en alt taş; zincirleme yoktu.)
                            if (belowVTile != null) {
                                hasSupport = true;
                            }
                        }

                        if (hasSupport)
                        {
                            useSettle = true;
                            // Calculate total distance for settle strength modifier
                            float totalDistY = Mathf.Abs(finalY - compressedPath[0].y);
                            float dist01 = Mathf.Clamp01((totalDistY - 1f) / 4f);
                            settleDur *= Mathf.Lerp(0.92f, 1.12f, dist01);
                            settleStr *= Mathf.Lerp(0.88f, 1.08f, dist01);
                        }
                    }

                    action.AddPathMove(
                        view,
                        compressedPath.ToArray(),
                        segmentDurations,
                        useSettle,
                        settleDur,
                        settleStr,
                        board.FallMoveCurve
                    );
                }
                else
                {
                    // Didn't move, just placed back
                    view.MarkPlannedToMoveThisFallPass(false);
                }
            }
        }

        board.RefreshAllTileObstacleVisuals();

        if (action.HasMoves)
            return new List<BoardAction> { action };

        return new List<BoardAction>();
    }

    public bool HasAnyEmptyPlayableCell()
    {
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Tiles[x, y] == null && IsTileSlotCell(x, y))
                {
                    // Basic check: if it's not blocked, consider it empty and playable
                    if (!IsGravityBlockedCell(x, y)) return true;
                }
            }
        }
        return false;
    }

    public bool HasAnyResolvableEmptyPlayableCell()
    {
        for (int x = 0; x < board.Width; x++)
        {
            int segmentTop = board.Height - 1;
            while (segmentTop >= 0)
            {
                while (segmentTop >= 0 && IsGravityBlockedCell(x, segmentTop))
                    segmentTop--;

                if (segmentTop < 0)
                    break;

                int segmentBottom = segmentTop;
                while (segmentBottom >= 0 && !IsGravityBlockedCell(x, segmentBottom))
                    segmentBottom--;

                int topY = segmentBottom + 1;
                bool touchesSpawn = IsSegmentConnectedToSpawnEdge(x, topY);
                int sourceTiles = 0;
                List<int> slotYs = null;

                for (int y = segmentTop; y >= topY; y--)
                {
                    if (!IsTileSlotCell(x, y))
                        continue;

                    (slotYs ??= new List<int>()).Add(y);

                    if (board.Tiles[x, y] != null)
                        sourceTiles++;
                }

                if (slotYs != null)
                {
                    if (touchesSpawn)
                    {
                        for (int i = 0; i < slotYs.Count; i++)
                            if (board.Tiles[x, slotYs[i]] == null)
                                return true;
                    }
                    else
                    {
                        int fillableSlots = Mathf.Min(sourceTiles, slotYs.Count);
                        for (int i = 0; i < fillableSlots; i++)
                            if (board.Tiles[x, slotYs[i]] == null)
                                return true;
                    }
                }

                segmentTop = segmentBottom - 1;
            }
        }

        return false;
    }

    // Builds a full-board mask of cells a spawned tile can ever reach via gravity,
    // using the SAME rules as the cascade: holes are passable (tiles fall through
    // them), diagonal slides work, only gravity-blocking obstacles (chest etc.) stop
    // the flow. Used at INITIAL board setup to leave gravity-shadowed cells empty
    // (e.g. mud directly under a chest stays bare until the chest breaks).
    // NOTE: only for initial placement — runtime fill is handled by CalculateCascades,
    // which naturally fills these cells once the blocking obstacle is cleared.
    public bool[,] ComputeGravityReachableMask()
    {
        var reachable = new bool[board.Width, board.Height];
        var queue = new Queue<Vector2Int>();

        void Seed(int cx, int cy)
        {
            if (reachable[cx, cy]) return;
            reachable[cx, cy] = true;
            queue.Enqueue(new Vector2Int(cx, cy));
        }

        // Seed every tile-slot in a spawn-connected vertical segment. A segment spans
        // holes (they're not gravity-blocked), so tiles falling through a side hole
        // reach the slots below it.
        for (int cx = 0; cx < board.Width; cx++)
        {
            int segTop = board.Height - 1;
            while (segTop >= 0)
            {
                while (segTop >= 0 && IsGravityBlockedCell(cx, segTop)) segTop--;
                if (segTop < 0) break;
                int segBot = segTop;
                while (segBot >= 0 && !IsGravityBlockedCell(cx, segBot)) segBot--;
                int topY = segBot + 1;
                if (IsSegmentConnectedToSpawnEdge(cx, topY))
                    for (int sy = topY; sy <= segTop; sy++)
                        if (IsTileSlotCell(cx, sy)) Seed(cx, sy);
                segTop = segBot - 1;
            }
        }

        // BFS: expand via gravity (straight down) and diagonal slide.
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();

            int ny = c.y + 1;
            if (ny >= board.Height) continue;

            // Straight fall: (cx, cy) → (cx, cy+1)
            if (IsTileSlotCell(c.x, ny) && !IsGravityBlockedCell(c.x, ny))
                Seed(c.x, ny);

            // Diagonal slide: (cx, cy) → (cx±1, cy+1), needs one open corner.
            for (int dx = -1; dx <= 1; dx += 2)
            {
                int nx = c.x + dx;
                if (nx < 0 || nx >= board.Width) continue;
                if (!IsTileSlotCell(nx, ny) || IsGravityBlockedCell(nx, ny)) continue;
                if (!IsDiagonalPassableCell(c.x, ny) && !IsDiagonalPassableCell(nx, c.y)) continue;
                Seed(nx, ny);
            }
        }

        return reachable;
    }

    private bool ProcessVerticalGravityAndSpawn(VirtualTile[,] virtualBoard, int x, ref bool spawnedMovableThisPass, Dictionary<ObstacleId, int> spawnedMovableCounts)
    {
        bool moved = false;

        int segmentTop = board.Height - 1;
        while (segmentTop >= 0)
        {
            while (segmentTop >= 0 && IsGravityBlockedCell(x, segmentTop))
                segmentTop--;

            if (segmentTop < 0)
                break;

            int segmentBottom = segmentTop;
            while (segmentBottom >= 0 && !IsGravityBlockedCell(x, segmentBottom))
                segmentBottom--;

            int topY = segmentBottom + 1;
            bool touchesSpawn = IsSegmentConnectedToSpawnEdge(x, topY);

            // 1. Compact segment
            List<VirtualTile> segmentTiles = new List<VirtualTile>();
            for (int y = segmentTop; y >= topY; y--)
            {
                if (!IsTileSlotCell(x, y)) continue;
                if (virtualBoard[x, y] != null)
                {
                    segmentTiles.Add(virtualBoard[x, y]);
                    virtualBoard[x, y] = null;
                }
            }

            List<int> slotYs = new List<int>();
            for (int y = segmentTop; y >= topY; y--)
            {
                if (IsTileSlotCell(x, y)) slotYs.Add(y);
            }

            for (int i = 0; i < segmentTiles.Count; i++)
            {
                int toY = slotYs[i];
                var tile = segmentTiles[i];
                virtualBoard[x, toY] = tile;

                if (tile.Path[tile.Path.Count - 1].y != toY || tile.Path[tile.Path.Count - 1].x != x)
                {
                    tile.Path.Add(new Vector2Int(x, toY));
                    moved = true;
                }
            }

            // 2. Spawn for remaining slots
            if (touchesSpawn)
            {
                int nextSpawnY = topY - 1 - board.SpawnFeedGap;
                
                for (int i = segmentTiles.Count; i < slotYs.Count; i++)
                {
                    int toY = slotYs[i];
                    int spawnFromY = nextSpawnY;

                    var newTile = new VirtualTile
                    {
                        IsSpawned = true,
                        Path = new List<Vector2Int> { new Vector2Int(x, spawnFromY), new Vector2Int(x, toY) }
                    };

                    // Cargo pass-kilidine tabi değil (kendi hamle bütçesi/tavanı var);
                    // diğer movable'lar (plastic vb.) pass başına 1 kuralında kalır.
                    if (TryPickMovableGoalToSpawn(x, spawnedMovableThisPass, out var goalObstacleId))
                    {
                        newTile.IsMovableObstacle = true;
                        newTile.SpawnObstacleId = goalObstacleId;
                        spawnedMovableThisPass = true;
                    }
                    else
                    {
                        // Refill: 3'lü match'lere (cascade) İZİN VER ama 4+ (special) run
                        // oluşturmayı önle → "yukarıdan 5 aynı taş gelip special çıkması"
                        // gibi kazara fazla special'lar azalır, normal cascade'ler korunur.
                        newTile.SpawnType = PickRefillType(virtualBoard, x, toY);
                    }

                    virtualBoard[x, toY] = newTile;
                    moved = true;
                }
            }

            segmentTop = segmentBottom - 1;
        }

        return moved;
    }

    // Tek bir diagonal slide geçişi. skipSpecials=true ise special kaynaklar atlanır
    // (special'lar düz inmeyi tercih etsin diye). En az bir slide olduysa true döner.
    private bool DoDiagonalSlidePass(VirtualTile[,] virtualBoard, HashSet<Vector2Int> verticalOnlyGaps, bool skipSpecials)
    {
        bool slided = false;
        for (int y = board.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (IsSlotEmpty(virtualBoard, x, y) && !verticalOnlyGaps.Contains(new Vector2Int(x, y))
                    && !GapExpectsVerticalFill(virtualBoard, x, y))
                {
                    // Right-top priority
                    if (TrySlide(virtualBoard, x + 1, y - 1, x, y, verticalOnlyGaps, skipSpecials))
                    {
                        slided = true;
                        continue;
                    }

                    // Left-top fallback
                    if (TrySlide(virtualBoard, x - 1, y - 1, x, y, verticalOnlyGaps, skipSpecials))
                    {
                        slided = true;
                        continue;
                    }
                }
            }
        }
        return slided;
    }

    private bool TrySlide(VirtualTile[,] virtualBoard, int fromX, int fromY, int toX, int toY, HashSet<Vector2Int> verticalOnlyGaps, bool skipSpecials = false)
    {
        if (fromX < 0 || fromX >= board.Width || fromY < 0 || fromY >= board.Height) return false;

        VirtualTile sourceTile = virtualBoard[fromX, fromY];
        int sourceY = fromY;

        if (sourceTile != null)
        {
            if (!TryGetCellState(fromX, fromY, out var state)) return false;
            if (state.isObstacleBlocked) return false;
        }
        else
        {
            // Try stealing from below if fromY is a Mask Hole or PassThrough empty cell
            if (TryGetCellState(fromX, fromY, out var state) && state.isPassThroughVoid)
            {
                // Find a tile below that passed through this cell
                for (int y = fromY + 1; y < board.Height; y++)
                {
                    var belowTile = virtualBoard[fromX, y];
                    if (belowTile != null)
                    {
                        if (belowTile.Path.Count > 0 && belowTile.Path[0].y <= fromY)
                        {
                            sourceTile = belowTile;
                            sourceY = y;
                        }
                        break;
                    }
                    if (IsGravityBlockedCell(fromX, y)) break;
                }
            }
        }

        if (sourceTile == null) return false;

        // Cargo (exitAtBottom): asla diagonal kaymaz, yalnızca düz aşağı düşer.
        if (sourceTile.IsStraightFallOnly) return false;

        // Kaynak taşın HEMEN ALTINDA patlamayı bekleyen (anchor'lı / pending-triggered)
        // bir special varsa, taş onun üstünde DURSUN — diagonal kayıp kaçmasın. Special
        // patlayınca boşalan hücreye düz düşer. Bu, "çok aksiyonlu" zincirlerde taşların
        // altındaki special daha patlamadan yana kayması glitch'ini önler. Kalıcı
        // obstacle'lar pending değildir → onların etrafından diagonal normal davranışını korur.
        if (board.IsPendingTriggeredSpecialCell(fromX, sourceY + 1))
            return false;

        // Special'lar diagonal'e savrulmasın: düz inmeyi tercih ederler. skipSpecials=true
        // olan ilk geçişte special kaynak atlanır; yalnızca son çare geçişinde (başka filler yoksa)
        // diagonal kayabilirler. Böylece special'lar yana kaymadan normal taş gibi düz iner.
        if (skipSpecials && sourceTile.View != null && sourceTile.View.GetSpecial() != TileSpecial.None)
            return false;

        if (sourceTile.DiagonalSlideCount >= board.MaxDiagonalSlidesPerCascade) return false;

        // Check if diagonal pass is possible (at least one corner must be open)
        bool cornerA = IsDiagonalPassableCell(fromX, toY);
        bool cornerB = IsDiagonalPassableCell(toX, fromY);

        if (!cornerA && !cornerB) return false;

        // Move it
        sourceTile.DiagonalSlideCount++;
        virtualBoard[fromX, sourceY] = null;
        virtualBoard[toX, toY] = sourceTile;

        if (sourceY > fromY)
        {
            for (int i = sourceTile.Path.Count - 1; i >= 0; i--)
            {
                if (sourceTile.Path[i].y > fromY)
                {
                    sourceTile.Path.RemoveAt(i);
                }
            }
            if (sourceTile.Path.Count == 0 || sourceTile.Path[sourceTile.Path.Count - 1].y != fromY)
            {
                sourceTile.Path.Add(new Vector2Int(fromX, fromY));
            }
        }

        sourceTile.Path.Add(new Vector2Int(toX, toY));

        verticalOnlyGaps.Add(new Vector2Int(fromX, sourceY));
        return true;
    }

    private void PruneVerticalOnlyGaps(VirtualTile[,] virtualBoard, HashSet<Vector2Int> verticalOnlyGaps)
    {
        if (verticalOnlyGaps.Count == 0) return;

        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var gap in verticalOnlyGaps)
        {
            bool stillEmpty = IsSlotEmpty(virtualBoard, gap.x, gap.y);

            // Keep the lock only if it is empty AND can be filled vertically
            bool keepLock = stillEmpty && HasVerticalFillPathFor(virtualBoard, gap.x, gap.y);

            if (!keepLock)
            {
                toRemove.Add(gap);
            }
        }

        foreach (var gap in toRemove)
        {
            verticalOnlyGaps.Remove(gap);
        }
    }

    private bool HasVerticalFillPathFor(VirtualTile[,] virtualBoard, int x, int y)
    {
        int segmentTop = FindSegmentTopY(x, y);

        if (IsSegmentConnectedToSpawnEdge(x, segmentTop))
            return true;

        for (int yy = y - 1; yy >= segmentTop; yy--)
        {
            if (virtualBoard[x, yy] != null) return true;
        }

        return false;
    }

    private int FindSegmentTopY(int x, int y)
    {
        int topY = y;
        while (topY > 0 && !IsGravityBlockedCell(x, topY - 1))
            topY--;
        return topY;
    }

    private List<Vector2Int> CompressPath(List<Vector2Int> rawPath)
    {
        if (rawPath.Count <= 2) return new List<Vector2Int>(rawPath);

        List<Vector2Int> optimized = new List<Vector2Int>();
        optimized.Add(rawPath[0]);

        for (int i = 1; i < rawPath.Count - 1; i++)
        {
            Vector2Int prev = optimized[optimized.Count - 1];
            Vector2Int curr = rawPath[i];
            Vector2Int next = rawPath[i + 1];

            bool isVertical = (prev.x == curr.x && curr.x == next.x);
            bool isHorizontal = (prev.y == curr.y && curr.y == next.y);

            if (!isVertical && !isHorizontal)
            {
                optimized.Add(curr);
            }
        }

        optimized.Add(rawPath[rawPath.Count - 1]);
        return optimized;
    }

    // ====== Helper Methods ======

    private bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state)
    {
        state = default;
        return board != null && board.TryGetCellState(x, y, out state);
    }

    private bool IsTileSlotCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        return state.canContainTile;
    }

    private bool IsSlotEmpty(VirtualTile[,] virtualBoard, int x, int y)
    {
        if (x < 0 || x >= board.Width || y < 0 || y >= board.Height) return false;
        if (!TryGetCellState(x, y, out var state)) return false;
        if (!state.canContainTile || state.isObstacleBlocked) return false;
        return virtualBoard[x, y] == null;
    }

    // Boş hücreye DİKEY dolum bekleniyor mu? (Kullanıcı kuralı 2026-07-19: diagonal'ın
    // TEK meşru sebebi kalıcı statik engeldir. Üstte düşecek BİR ŞEY — taş, special,
    // movable, ya da anchor'lı (pending) special — varsa boşluk ONA rezervedir; alta en
    // yakın olan önce iner, üsttekiler izler. Diagonal bu sırayı ÇALAMAZ.)
    // Yukarı tarama: kalıcı tutucu/statik engel görülürse dikey dolum imkânsız → diagonal
    // serbest; virtual tile veya pending hücre görülürse dikey dolum gelecek → yasak.
    // Tepeye kadar bomboşsa: kolon spawn'a kapalı demektir (Step1 doldururdu) → serbest.
    private bool GapExpectsVerticalFill(VirtualTile[,] virtualBoard, int x, int y)
    {
        for (int yy = y - 1; yy >= 0; yy--)
        {
            // Oil vb. holdsTile: taşı KALICI tutar — üstü asla inmez → dikey dolum yok.
            if (board.ObstacleStateService != null && board.ObstacleStateService.HoldsTileAt(x, yy))
                return false;

            if (virtualBoard[x, yy] != null)
                return true;   // düşecek taş/special/movable var → boşluk ona rezerve

            if (board.IsPendingTriggeredSpecialCell(x, yy))
                return true;   // anchor'lı special (geçici) → patlayınca kolon iner

            if (IsGravityBlockedCell(x, yy))
                return false;  // kalıcı statik engel → klasik kural: diagonal serbest
        }
        return false;
    }

    private bool IsGravityBlockedCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        if (state.isObstacleBlocked || board.IsPendingTriggeredSpecialCell(x, y)) return true;
        // holdsTile=true obstacle (Oil): tile dikey olarak bu hücreden kayamaz, kalır
        return board.ObstacleStateService?.HoldsTileAt(x, y) ?? false;
    }

    private bool IsSegmentConnectedToSpawnEdge(int x, int topY)
    {
        if (topY <= 0) return true;

        for (int y = topY - 1; y >= 0; y--)
        {
            if (IsGravityBlockedCell(x, y)) return false;
            if (!board.IsSpawnPassThroughCell(x, y)) return false;
        }

        return true;
    }

    private bool IsDiagonalPassableCell(int x, int y)
    {
        if (!TryGetCellState(x, y, out var state)) return false;
        return state.allowsDiagonalPassThrough;
    }

    // Cargo (exitAtBottom) auto-spawn: KREDİ SİSTEMİ (kullanıcı kuralı 2026-07-21).
    // Üretim doğrudan oyuncunun kırdırdığı taşa bağlı: her kırılan taş 2/5 cargo
    // kredisi ekler (5 taş → 2 cargo); kredi 1'i geçince spawn yapılabilir, spawn
    // 1 kredi harcar. Kredi hamleler arası TAŞINIR. Self-play doğal olarak imkânsız:
    // cargo çıkışı taş kırmaz → kredi üretmez → konveyör kendini besleyemez.
    // Ek sınırlar: sütundaki cargo oynanabilir hücrelerin yarısını aşamaz + pass
    // başına sütun başına en fazla 1 cargo (yayılma).
    private const float CargoPerConsumedTile = 0.4f;   // 2/5
    private float cargoSpawnCredits;

    /// <summary>Kırılan taş başına cargo üretim kredisi ekler (ClearAndDestroyTile çağırır).</summary>
    public void AddCargoSpawnCredits(int consumedTiles)
    {
        if (consumedTiles > 0)
            cargoSpawnCredits += consumedTiles * CargoPerConsumedTile;
    }

    /// <summary>Level başında kredi sıfırlanır (board yeniden kurulurken çağrılır).</summary>
    public void ResetCargoSpawnCredits() => cargoSpawnCredits = 0f;

    // Pass başına sütun başına en fazla 1 cargo: sütunlar soldan sağa işlendiği için
    // sınırsız bırakılırsa geniş temizlikte üretimin tamamı ilk refill olan bir-iki
    // sütuna yığılıyordu; bu set ile aynı dalgada üretim sütunlara yayılır.
    private readonly HashSet<int> cargoSpawnColumnsThisPass = new();

    private bool CanSpawnExitCargo(int x)
    {
        if (cargoSpawnCredits < 1f) return false;
        if (cargoSpawnColumnsThisPass.Contains(x)) return false;

        var obstacleService = board.ObstacleStateService;
        int cargoInColumn = 0, playableInColumn = 0;
        for (int cy = 0; cy < board.Height; cy++)
        {
            if (IsTileSlotCell(x, cy)) playableInColumn++;
            if (obstacleService.IsExitAtBottomAt(x, cy)) cargoInColumn++;
        }

        return cargoInColumn * 2 < playableInColumn;
    }

    private bool TryPickMovableGoalToSpawn(int x, bool movableSpawnedThisPass, out ObstacleId obstacleId)
    {
        obstacleId = ObstacleId.None;

        var topHud = board.TopHud;
        if (topHud == null || board.ObstacleStateService == null || board.ActiveLevelData == null)
            return false;

        _activeGoalsBuffer.Clear();
        topHud.GetActiveGoals(_activeGoalsBuffer);

        for (int i = 0; i < _activeGoalsBuffer.Count; i++)
        {
            var goal = _activeGoalsBuffer[i];
            if (goal.targetType != LevelGoalTargetType.Obstacle) continue;
            if (goal.remaining <= 0) continue;

            var def = board.ActiveLevelData.obstacleLibrary != null
                ? board.ActiveLevelData.obstacleLibrary.Get(goal.obstacleId)
                : null;

            if (def == null) continue;

            if (!def.IsMovableObstacleForRemainingHits(Mathf.Max(1, def.hits))) continue;

            // Cargo dışındaki movable'lar (plastic vb.): pass başına 1 spawn kuralı.
            if (!def.exitAtBottom && movableSpawnedThisPass) continue;

            int alive = board.ObstacleStateService.CountAliveOrStampedOrigins(goal.obstacleId);
            if (alive >= goal.remaining) continue;

            if (def.exitAtBottom)
            {
                if (!CanSpawnExitCargo(x)) continue;
                cargoSpawnCredits -= 1f;
                cargoSpawnColumnsThisPass.Add(x);
            }

            obstacleId = goal.obstacleId;
            return true;
        }

        return false;
    }

    private TileView SpawnMovableObstacleTileForFall(int targetX, int targetY, int startX, int startY, ObstacleId obstacleId)
    {
        if (board.ObstacleStateService == null) return null;

        if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(targetX, targetY, obstacleId))
            return null;

        var def = board.ActiveLevelData != null && board.ActiveLevelData.obstacleLibrary != null
            ? board.ActiveLevelData.obstacleLibrary.Get(obstacleId)
            : null;

        if (def == null) return null;

        var go = board.AcquireTileObject(board.Parent);   // havuz açıksa iadeden, yoksa Instantiate
        var view = go.GetComponent<TileView>();
        if (view == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        view.Init(board, targetX, targetY);
        board.ConfigureTileView(view);
        view.SetUseFullCellIcon(false);
        view.SetMovableObstacleTile(true);
        // İlk açılıştaki spawn (GridSpawner.SpawnMovableObstacleTile) gibi full-cell
        // stretch bayrağını uygula — aksi hâlde runtime'da üretilen movable obstacle'lar
        // hücreye tam stretch olmaz, merkezi küçük ikon olarak kalırdı.
        view.SetFullCellMovableSprite(def.fullCellSprite);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        view.ApplyTileSize(board.TileSize);

        // Setup initial visual coordinate before falling
        view.SetCoords(startX, startY);
        view.SnapToGrid(board.TileSize);

        // Target logical coordinate
        view.SetCoords(targetX, targetY);

        TileType dummyType = board.RandomPool != null && board.RandomPool.Length > 0
            ? board.RandomPool[0]
            : TileType.Gear;

        Sprite obstacleSprite = def.GetPreviewSprite();
        if (obstacleSprite != null)
            view.SetMovableObstacleSprite(obstacleSprite);

        view.SetType(dummyType);
        view.SetSpecial(TileSpecial.None);

        return view;
    }
}
