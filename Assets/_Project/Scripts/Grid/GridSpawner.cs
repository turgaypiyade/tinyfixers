using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridSpawner : MonoBehaviour
{
    private static readonly Vector2 IconReferenceSize = new Vector2(100f, 100f);

    [Header("Level")]
    public LevelData level;
    [SerializeField] private LevelRuntimeSelector levelRuntimeSelector;

    [Header("Tile")]
    public GameObject tilePrefab;
    public TileIconLibrary iconLibrary;
    public BoardController board;

    [Header("Layout")]
    public int tileSize = 105;
    // Auto-fit ayarları
    [SerializeField] private int fitSafetyMarginPx = 8;   // border güvenlik payı
    [SerializeField, Range(0.8f, 1f)] private float fitScale = 1f; // ekstra küçültme oranı

    [Header("Border System")]
    public DynamicBoardBorder borderDrawer;

    [Tooltip("BoardContent iç boşluk (kenarlara yapışmayı keser)")]
    public int boardPadding = 8;

    [Header("Cell BG")]
    public GameObject cellBgPrefab;
    [SerializeField] private Color underTileCellBgTint = new Color(0.72f, 0.86f, 1f, 1f);

    [SerializeField, Range(0.5f, 1f)]
    private float iconScale = 0.95f;
    [SerializeField] private Vector2 iconSize = new Vector2(100f, 100f);
    [SerializeField] private bool fullCellIcons = false;

    [Header("Spawn Parent (BoardMask altındaki BoardContent)")]
    [SerializeField] private RectTransform spawnParent;

    [Header("Roots (auto create)")]
    [SerializeField] private RectTransform cellBgRoot;
    [SerializeField] private RectTransform mudOverlayRoot;
    [SerializeField] private RectTransform obstaclesRoot;
    [SerializeField] private RectTransform underTilesObstaclesRoot;
    [SerializeField] private RectTransform overTilesObstaclesRoot;
    [SerializeField] private RectTransform tilesRoot;

    [Header("Mud Overlay")]
    [SerializeField] private MudOverlayService mudOverlayService;
    [SerializeField] private int mudDefaultHits = 2;
    [SerializeField] private Color runtimeBoardBg = new Color(0.78f, 0.88f, 0.97f, 1f);
    [SerializeField] private Color runtimeNormalCell = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private RectTransform gridLinesRoot;
    [SerializeField] private Color runtimeGridLineColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField, Min(1f)] private float runtimeGridLineThickness = 2f;

    [SerializeField] private RectTransform boardBgRoot;

    [Header("Tube Obstacle")]
    [SerializeField] private TubeObstacleService tubeObstacleService;
    [SerializeField] private TubeView tubeViewPrefab;
    [SerializeField] private RectTransform tubeRoot;

    [Header("Obstacle Visual (UI)")]
    [SerializeField] private bool drawObstacles = true;
    [Tooltip("ColorChest icin katmanli gorsel prefabi (ChestObstacleView component'i olmali)")]
    [SerializeField] private ChestObstacleView chestObstacleViewPrefab;
    [Tooltip("BatteryBox icin katmanli gorsel prefabi (BatteryBoxView component'i olmali)")]
    [SerializeField] private BatteryBoxView batteryBoxViewPrefab;

    [Header("Initial Resolve")]
    [SerializeField] private bool resolveInitialOnStart = false;

    [Header("Random Pool")]
    public TileType[] randomPool = { TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate };

    [SerializeField] private int referenceCols = 9;
    [SerializeField] private int referenceRows = 11;
    [SerializeField] private bool useReferenceGridSizing = true;
    [SerializeField] private bool useFixedTileSize = true;

    private int width;
    private int height;
    private LevelData resolvedLevel;
    private bool ownsResolvedLevelInstance;
    private readonly Dictionary<int, Image> obstacleViewsByOrigin = new();
    private readonly Dictionary<int, ObstacleDef> obstacleDefsByOrigin = new();
    private readonly Dictionary<int, ChestObstacleView> _chestViews = new();
    private readonly Dictionary<int, BatteryBoxView> _batteryBoxViews = new();
    private readonly Dictionary<int, GameObject> cellBgByIndex = new();
    private readonly Dictionary<int, Image> cellBgImageByIndex = new();
    private readonly Dictionary<int, Color> baseCellBgColorByIndex = new();
    private EnergyContainerService energyContainerService;

    private void Awake()
    {
        if (board == null) board = GetComponent<BoardController>()
                          ?? GetComponentInParent<BoardController>(true)
                          ?? FindFirstObjectByType<BoardController>();

        //if (borderDrawer == null) borderDrawer = GetComponent<DynamicBoardBorder>();
        if (borderDrawer == null) borderDrawer = GetComponent<DynamicBoardBorder>()
                                ?? GetComponentInChildren<DynamicBoardBorder>(true)
                                ?? FindFirstObjectByType<DynamicBoardBorder>();
    }

    private void OnEnable()
    {
        if (board != null)
            board.ObstacleVisualChanged += HandleObstacleVisualChanged;
    }

    private void OnDisable()
    {
        if (board != null)
            board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        UnbindBoardEvents();
    }

    private void OnDestroy()
    {
        if (ownsResolvedLevelInstance && resolvedLevel != null)
            Destroy(resolvedLevel);

        resolvedLevel = null;
        ownsResolvedLevelInstance = false;
    }

    private void Start()
    {
        resolvedLevel = ResolveLevelData();
        ApplyResolvedLevelToConsumers(resolvedLevel);
        StampTubeCellsIntoLevel(resolvedLevel); // must happen before SetLevelData

        if (board == null || resolvedLevel == null || tilePrefab == null || iconLibrary == null || cellBgPrefab == null)
        {
            Debug.LogError("GridSpawner: Eksik referans var (board/resolvedLevel/tilePrefab/iconLibrary/cellBgPrefab).");
            enabled = false;
            return;
        }
        PlayLevelMusic(resolvedLevel);
        width = resolvedLevel.width;
        height = resolvedLevel.height;

        if (!useFixedTileSize)
            AutoFitTileSizeToMask();

        EnsureRoots();
        ApplyPaddingToSpawnParent();

        board.Init(width, height, iconLibrary);
        board.SetLevelData(resolvedLevel);
        board.SetupFactory(tilePrefab, tilesRoot, tileSize, randomPool, iconScale, fullCellIcons, iconSize);

        BindBoardEvents();

        // board init sonrası subscribe güvence
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;

        BuildInitialGrid();

        if (resolveInitialOnStart)
            StartCoroutine(board.ResolveInitial());
    }

    private void PlayLevelMusic(LevelData activeLevel)
    {
        if (activeLevel == null)
            return;

        if (activeLevel.musicClip == null)
            return;

        if (MusicManager.Instance == null)
        {
            Debug.LogWarning("GridSpawner: MusicManager sahnede yok, level müziği çalınamadı.");
            return;
        }

        MusicManager.Instance.Play(activeLevel.musicClip, activeLevel.musicVolume);
    }
    private void BindBoardEvents()
    {
        UnbindBoardEvents();
        if (board == null) return;

        board.OnObstacleStageChanged       += HandleObstacleStageChanged;
        board.OnObstacleDestroyed           += HandleObstacleDestroyed;
        board.OnCellUnlocked                += HandleCellUnlocked;
        board.OnObstacleCreatedDynamic      += HandleObstacleCreatedDynamic;
        board.OnChestOpened                 += HandleChestOpened;
        board.OnChestColorRemoved           += HandleChestColorRemoved;
        board.OnBatteryHit                  += HandleBatteryHit;
    }

    private void UnbindBoardEvents()
    {
        if (board == null) return;

        board.OnObstacleStageChanged       -= HandleObstacleStageChanged;
        board.OnObstacleDestroyed          -= HandleObstacleDestroyed;
        board.OnCellUnlocked               -= HandleCellUnlocked;
        board.OnObstacleCreatedDynamic     -= HandleObstacleCreatedDynamic;
        board.OnChestOpened                -= HandleChestOpened;
        board.OnChestColorRemoved          -= HandleChestColorRemoved;
        board.OnBatteryHit                 -= HandleBatteryHit;
    }

    private void ApplyPaddingToSpawnParent()
    {
        if (spawnParent == null) return;

        float gridW = width * tileSize;
        float gridH = height * tileSize;

        spawnParent.anchorMin = new Vector2(0.5f, 0.5f);
        spawnParent.anchorMax = new Vector2(0.5f, 0.5f);
        spawnParent.pivot = new Vector2(0.5f, 0.5f);
        spawnParent.anchoredPosition = Vector2.zero;

        spawnParent.sizeDelta = new Vector2(
            gridW + boardPadding * 2f,
            gridH + boardPadding * 2f
        );

        Vector2 inner = new Vector2(boardPadding, -boardPadding);

        if (cellBgRoot != null) cellBgRoot.anchoredPosition = inner;
        if (mudOverlayRoot != null) mudOverlayRoot.anchoredPosition = inner;
        if (gridLinesRoot != null) gridLinesRoot.anchoredPosition = inner;
        if (tilesRoot != null) tilesRoot.anchoredPosition = inner;
        if (obstaclesRoot != null) obstaclesRoot.anchoredPosition = inner;
        if (underTilesObstaclesRoot != null) underTilesObstaclesRoot.anchoredPosition = Vector2.zero;
        if (overTilesObstaclesRoot != null) overTilesObstaclesRoot.anchoredPosition = Vector2.zero;
    }

    private void AlignBorderRootToSpawnParent()
    {
        if (borderDrawer == null || borderDrawer.borderRoot == null || spawnParent == null)
            return;

        RectTransform br = borderDrawer.borderRoot;

        br.anchorMin = new Vector2(0.5f, 0.5f);
        br.anchorMax = new Vector2(0.5f, 0.5f);
        br.pivot = new Vector2(0.5f, 0.5f);

        br.anchoredPosition = Vector2.zero;
        br.sizeDelta = spawnParent.rect.size;

        br.localRotation = Quaternion.identity;
        br.localScale = Vector3.one;
    }

    private void BuildInitialGrid()
    {
        ClearChildren(cellBgRoot);
        ClearChildren(gridLinesRoot);
        ClearChildren(underTilesObstaclesRoot);
        ClearChildren(overTilesObstaclesRoot);
        ClearChildren(tilesRoot);
        obstacleViewsByOrigin.Clear();
        obstacleDefsByOrigin.Clear();
        _chestViews.Clear();
        cellBgByIndex.Clear();
        cellBgImageByIndex.Clear();
        baseCellBgColorByIndex.Clear();

        bool[] blocked = BuildBlockedMap();

        if (drawObstacles)
        {
            DrawObstacleVisuals();
            DrawMudOverlays();
            DrawTubeObstacles();
        }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int idx = resolvedLevel.Index(x, y);
                bool isBlockedByObstacle = blocked[idx];

                bool isEmpty = (resolvedLevel.cells != null && idx >= 0 && idx < resolvedLevel.cells.Length && resolvedLevel.cells[idx] == (int)CellType.Empty);
                if (isEmpty)
                {
                    board.SetHole(x, y, true);
                    continue;
                }

                SpawnCellBg(x, y);
                if (isBlockedByObstacle)
                {
                    // DEBUG: Mud bloklamamalı; bloklanıyorsa def'i veya stage flag'lerini logla.
                    if ((ObstacleId)resolvedLevel.obstacles[idx] == ObstacleId.Mud)
                        Debug.LogError($"[MudDebug] Cell ({x},{y}) Mud OLMASINA RAĞMEN blocked sayıldı → hole olarak işaretleniyor!");
                    board.SetHole(x, y, true);
                    continue;
                }

                board.SetHole(x, y, false);
            }

        ApplyUnderTileCellBgTint();

        DrawGridLines();

        var initialTypes = board.SimulateInitialTypes();

        // Bir sütunda over-tile blocker varsa, onun altındaki hücreler tile spawner'ından
        // erişilemez — başlangıçta oralara taş spawn etme.
        var unreachableFromAbove = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            bool blockerAbove = false;
            for (int y = 0; y < height; y++)
            {
                int idx2 = resolvedLevel.Index(x, y);
                if (blocked[idx2])
                    blockerAbove = true;
                else if (blockerAbove)
                    unreachableFromAbove[x, y] = true;
            }
        }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (board.Holes[x, y]) continue;

                // MovableObstacle: hücre bloklanmamış ama obstacle var → tile yerine obstacle tile spawn et
                bool isMovableObstacle = board.ObstacleStateService != null
                    && board.ObstacleStateService.IsMovableObstacleAt(x, y);

                if (isMovableObstacle)
                {
                    SpawnMovableObstacleTile(x, y);
                }
                else if (!unreachableFromAbove[x, y])
                {
                    SpawnTile(x, y, initialTypes[x, y]);
                }
            }

        // Tüm tile'lar spawn edildikten sonra sıralamayı toplu yenile
        board.RefreshAllSortingOrders();
        board.RefreshOilOverlays();

        // ─── DEBUG: İlk yerleşim snapshot'u ───────────────────────────────
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[INITIAL BOARD SNAPSHOT] ({width}x{height}) (H=Hole, ·=null, first letter of type)");
            for (int dbgY = 0; dbgY < height; dbgY++)
            {
                sb.Append($"  row{dbgY}: ");
                for (int dbgX = 0; dbgX < width; dbgX++)
                {
                    if (board.Holes[dbgX, dbgY]) sb.Append("[H ]");
                    else { var td = board.GridData[dbgX, dbgY]; sb.Append(td == null ? "[· ]" : $"[{td.ToDebugString().PadRight(2)}]"); }
                }
                sb.AppendLine();
            }
            UnityEngine.Debug.Log(sb.ToString());
        }
        // ─── END DEBUG ─────────────────────────────────────────────────────

        // Sıralama: CellBG < Tiles < GridLines < Static Obstacles < Line-Travel VFX
        // Normal tile ve movable obstacle üzerinde grid çizgileri görünür.
        // Animasyon VFX parent'ları (lineTravelSpawnParent, afterImageParent) spawnParent'ın
        // direkt çocuğuysa son sıraya alınır — böylece combo animasyonları grid çizgilerinin
        // üstünde render edilir.
        if (cellBgRoot != null) cellBgRoot.SetAsFirstSibling();
        // Mud overlay grid çizgilerinin de üstünde durmalı ki cell sınırları boyunca
        // seamless texture kesintisiz görünsün. Tile altında, grid line üstünde.
        if (mudOverlayRoot != null) mudOverlayRoot.SetSiblingIndex(1);
        if (tilesRoot != null) tilesRoot.SetAsLastSibling();
        if (gridLinesRoot != null) gridLinesRoot.SetSiblingIndex(1);
        if (mudOverlayRoot != null) mudOverlayRoot.SetSiblingIndex(2);
        if (obstaclesRoot != null) obstaclesRoot.SetAsLastSibling();

        if (board != null && spawnParent != null)
        {
            var lineTravelParent = board.LineTravelSpawnParent as RectTransform;
            if (lineTravelParent != null && lineTravelParent.parent == spawnParent)
                lineTravelParent.SetAsLastSibling();

            if (board.lineTravelPlayer != null)
            {
                var afterImgParent = board.lineTravelPlayer.afterImageParent;
                if (afterImgParent != null && afterImgParent.parent == spawnParent)
                    afterImgParent.SetAsLastSibling();
            }
        }

        // var drawer = GetComponent<DynamicBoardBorder>();
        var drawer = borderDrawer;
        if (drawer != null)
        {
            drawer.level = resolvedLevel;
            drawer.tileSize = tileSize;
            drawer.contentOffset = Vector2.zero;
            drawer.includeObstaclesAsSolid = true;

            AlignBorderRootToSpawnParent();

            // board.Holes[x,y] → 1D array (hole olan hücreler border almaz)
            bool[] holes = new bool[resolvedLevel.width * resolvedLevel.height];
            for (int hy = 0; hy < resolvedLevel.height; hy++)
                for (int hx = 0; hx < resolvedLevel.width; hx++)
                    holes[resolvedLevel.Index(hx, hy)] = board.Holes[hx, hy];

            drawer.Draw(blocked, holes);
        }


    }

    private void SpawnMovableObstacleTile(int x, int y)
    {
        int idx = resolvedLevel.Index(x, y);
        var obsId = (ObstacleId)resolvedLevel.obstacles[idx];
        var def = resolvedLevel.obstacleLibrary?.Get(obsId);
        if (def == null) return;

        var tile = Instantiate(tilePrefab, tilesRoot);
        var rt = tile.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        var view = tile.GetComponent<TileView>();
        if (view == null)
        {
            Debug.LogError("GridSpawner: TileView yok (movable obstacle).");
            Destroy(tile);
            return;
        }

        view.SetIconScale(iconScale);
        view.SetIconSize(iconSize);
        view.SetUseFullCellIcon(false);
        view.SetMovableObstacleTile(true);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        view.ApplyTileSize(tileSize);

        board.RegisterTile(view, x, y);

        var dummyType = randomPool != null && randomPool.Length > 0
            ? randomPool[0]
            : TileType.Gear;
        view.SetType(dummyType);

        Sprite obstacleSprite = def.GetPreviewSprite();
        if (obstacleSprite != null && view.IconImage != null)
            view.IconImage.sprite = obstacleSprite;

        board.SyncTileData(x, y);
    }
    private void ApplyResolvedLevelToConsumers(LevelData activeLevel)
    {
        if (activeLevel == null) return;

        if (borderDrawer != null)
            borderDrawer.SetLevelData(activeLevel);

        var staticBorderDrawer = GetComponent<BoardBorderDrawer>();
        if (staticBorderDrawer != null)
            staticBorderDrawer.SetLevelData(activeLevel);
    }


    private LevelData ResolveLevelData()
    {
        var sourceLevel = levelRuntimeSelector != null
            ? levelRuntimeSelector.ResolveLevelData()
            : null;

        sourceLevel ??= level;
        var runtimeClone = CloneLevelDataForRuntime(sourceLevel);
        ownsResolvedLevelInstance = runtimeClone != null;
        return runtimeClone;
    }

    private LevelData CloneLevelDataForRuntime(LevelData source)
    {
        if (source == null)
            return null;

        var clone = ScriptableObject.CreateInstance<LevelData>();
        clone.name = $"{source.name}_Runtime";
        clone.width = source.width;
        clone.height = source.height;
        clone.moves = source.moves;
        clone.energyPerContainer = Mathf.Max(1, source.energyPerContainer);
        clone.musicClip = source.musicClip;
        clone.musicVolume = source.musicVolume;
        clone.obstacleLibrary = source.obstacleLibrary;
        clone.goals = CloneGoals(source.goals);
        clone.tubes = source.tubes != null
            ? (TubeEntry[])source.tubes.Clone()
            : System.Array.Empty<TubeEntry>();

        int size = Mathf.Max(1, source.width * source.height);

        clone.cells = new int[size];
        clone.obstacles = new int[size];
        clone.obstacleOrigins = new int[size];

        if (source.cells != null)
            System.Array.Copy(source.cells, clone.cells, Mathf.Min(size, source.cells.Length));
        if (source.obstacles != null)
            System.Array.Copy(source.obstacles, clone.obstacles, Mathf.Min(size, source.obstacles.Length));
        if (source.obstacleOrigins != null)
            System.Array.Copy(source.obstacleOrigins, clone.obstacleOrigins, Mathf.Min(size, source.obstacleOrigins.Length));

        return clone;
    }

    private LevelGoalDefinition[] CloneGoals(LevelGoalDefinition[] sourceGoals)
    {
        if (sourceGoals == null || sourceGoals.Length == 0)
            return System.Array.Empty<LevelGoalDefinition>();

        var cloned = new LevelGoalDefinition[sourceGoals.Length];
        for (int i = 0; i < sourceGoals.Length; i++)
        {
            var source = sourceGoals[i];
            if (source == null)
            {
                cloned[i] = new LevelGoalDefinition();
                continue;
            }

            cloned[i] = new LevelGoalDefinition
            {
                targetType = source.targetType,
                tileType = source.tileType,
                obstacleId = source.obstacleId,
                collectibleId = source.collectibleId,
                iconOverride = source.iconOverride,
                amount = Mathf.Max(1, source.amount)
            };
        }

        return cloned;
    }

    private bool[] BuildBlockedMap()
    {
        bool[] blocked = new bool[width * height];
        if (board?.ObstacleStateService == null)
            return blocked;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                blocked[resolvedLevel.Index(x, y)] = board.ObstacleStateService.IsCellBlocked(x, y);

        return blocked;
    }

    private void DrawObstacleVisuals()
    {
        if (resolvedLevel.obstacleLibrary == null || resolvedLevel.obstacles == null || resolvedLevel.obstacleOrigins == null) return;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int idx = resolvedLevel.Index(x, y);
                var obsId = (ObstacleId)resolvedLevel.obstacles[idx];
                if (obsId == ObstacleId.None) continue;
                if (resolvedLevel.obstacleOrigins[idx] != idx) continue;

                var def = resolvedLevel.obstacleLibrary.Get(obsId);
                if (def == null) continue;

                // ── MovableObstacle tile olarak yönetilir, ayrı visual çizilmez ──
                if (def.IsMovableObstacle) continue;

                // Mud kendi seamless renderer'ını kullanır, default sprite Image üretme.
                if (obsId == ObstacleId.Mud) continue;
                // Tube kendi TubeView renderer'ını kullanır.
                if (obsId == ObstacleId.Tube) continue;

                var image = DrawObstacleImage(def, x, y);
                if (image != null)
                {
                    obstacleViewsByOrigin[idx] = image;
                    obstacleDefsByOrigin[idx] = def;
                }
            }
    }

    private void DrawMudOverlays()
    {
        if (mudOverlayService == null) return;
        if (resolvedLevel?.obstacles == null || resolvedLevel.obstacleOrigins == null) return;
        if (mudOverlayRoot == null) return;

        mudOverlayService.Init(board, width, height);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int idx = resolvedLevel.Index(x, y);
            if ((ObstacleId)resolvedLevel.obstacles[idx] != ObstacleId.Mud) continue;
            // Mud her cell için 1x1 — origin kendi cell'i olmalı.
            if (resolvedLevel.obstacleOrigins[idx] != idx) continue;

            // Empty cell (hole) üzerine mud çizme — irregular grid shape'leri için.
            bool isEmpty = resolvedLevel.cells != null
                && idx < resolvedLevel.cells.Length
                && resolvedLevel.cells[idx] == (int)CellType.Empty;
            if (isEmpty) continue;

            var go = new GameObject(
                $"Mud_{x}_{y}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.RawImage),
                typeof(MudCellView));
            go.transform.SetParent(mudOverlayRoot, false);

            var view = go.GetComponent<MudCellView>();
            view.Init(
                mudOverlayService.SharedMudTexture,
                x, y,
                width, height,
                mudOverlayService.DarkTint,
                mudOverlayService.LightTint,
                mudOverlayService.MinAlphaAtLastStage);
            view.PlaceInCell(tileSize);

            int remaining = board != null && board.ObstacleStateService != null
                ? board.ObstacleStateService.GetRemainingHitsAt(x, y)
                : mudDefaultHits;
            if (remaining <= 0) remaining = mudDefaultHits;

            int max = Mathf.Max(remaining, mudDefaultHits);
            mudOverlayService.RegisterCell(x, y, view, remaining, max);
        }
    }

    private void StampTubeCellsIntoLevel(LevelData lvl)
    {
        if (lvl?.tubes == null || lvl.tubes.Length == 0) return;
        if (lvl.obstacles == null || lvl.obstacleOrigins == null) return;

        foreach (var entry in lvl.tubes)
        {
            int[] cells = TubeObstacleService.GetCellIndices(entry, lvl.width, lvl.height);
            if (cells == null) continue;

            foreach (int cellIdx in cells)
            {
                if (cellIdx < 0 || cellIdx >= lvl.obstacles.Length) continue;
                lvl.obstacles[cellIdx]       = (int)ObstacleId.Tube;
                lvl.obstacleOrigins[cellIdx] = entry.originCellIndex;
            }
        }
    }

    private void DrawTubeObstacles()
    {
        if (tubeObstacleService == null || tubeViewPrefab == null) return;
        if (resolvedLevel?.tubes == null || resolvedLevel.tubes.Length == 0) return;
        if (tubeRoot == null) return;

        tubeObstacleService.Init(board.ObstacleStateService);
        board.ObstacleStateService.TubeHitInterceptor = tubeObstacleService.HandleTubeHit;

        foreach (var entry in resolvedLevel.tubes)
        {
            int[] cellIndices = TubeObstacleService.GetCellIndices(entry, resolvedLevel.width, resolvedLevel.height);
            if (cellIndices == null || cellIndices.Length == 0) continue;

            var go   = Instantiate(tubeViewPrefab.gameObject);
            var view = go.GetComponent<TubeView>();
            if (view == null) { Destroy(go); continue; }

            view.Init(entry.direction, entry.length, tileSize);

            var (tlX, tlY) = TubeObstacleService.GetTopLeftCell(entry, resolvedLevel.width);
            view.PlaceOnGrid(tubeRoot, tlX, tlY);

            tubeObstacleService.RegisterTube(entry.originCellIndex, cellIndices, view);
        }
    }

    private void HandleObstacleStageChanged(int originIndex, ObstacleStageSnapshot nextStage)
    {
        if (nextStage.behavior == ObstacleBehaviorType.MovableObstacle)
        {
            int mx = originIndex % width;
            int my = originIndex / width;
            var tileView = board.GetTileViewAt(mx, my);
            if (tileView != null && nextStage.sprite != null && tileView.IconImage != null)
                tileView.IconImage.sprite = nextStage.sprite;
            return;
        }

        if (!obstacleViewsByOrigin.TryGetValue(originIndex, out var image) || image == null)
            return;

        if (ShouldLetEnergyContainerOwnVisual(originIndex))
            return;

        if (nextStage.sprite != null)
            image.sprite = nextStage.sprite;

        MoveObstacleToBehaviorRoot(image.rectTransform, nextStage.behavior);
        ApplyUnderTileCellBgTint();
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        if (obstacleId == ObstacleId.EnergyContainer)
        {
            // Don't destroy the image — EnergyContainerFx will apply the exhausted
            // visual on the same GameObject. Just stop tracking it here.
            obstacleViewsByOrigin.Remove(originIndex);
            obstacleDefsByOrigin.Remove(originIndex);
            return;
        }

        if (obstacleViewsByOrigin.TryGetValue(originIndex, out var image) && image != null)
            Destroy(image.gameObject);

        obstacleViewsByOrigin.Remove(originIndex);
        obstacleDefsByOrigin.Remove(originIndex);
        _chestViews.Remove(originIndex);
        ApplyUnderTileCellBgTint();
    }

    private void HandleCellUnlocked(int cellIndex)
    {
        int x = cellIndex % width;
        int y = cellIndex / width;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        if (!cellBgByIndex.ContainsKey(cellIndex) || cellBgByIndex[cellIndex] == null)
            SpawnCellBg(x, y);
    }

    private void EnsureRoots()
    {
        var root = spawnParent != null ? spawnParent : (RectTransform)transform;

        if (cellBgRoot == null)
            cellBgRoot = GetOrCreateChildRoot(root, "CellBGs");

        if (mudOverlayRoot == null)
            mudOverlayRoot = GetOrCreateChildRoot(root, "MudOverlay");

        if (tubeRoot == null)
            tubeRoot = GetOrCreateChildRoot(root, "TubeObstacles");

        if (gridLinesRoot == null)
            gridLinesRoot = GetOrCreateChildRoot(root, "GridLines");

        if (obstaclesRoot == null)
            obstaclesRoot = GetOrCreateChildRoot(root, "Obstacles");

        if (underTilesObstaclesRoot == null)
            underTilesObstaclesRoot = GetOrCreateChildRoot(obstaclesRoot, "UnderTiles");

        if (overTilesObstaclesRoot == null)
            overTilesObstaclesRoot = GetOrCreateChildRoot(obstaclesRoot, "OverTiles");

        if (tilesRoot == null)
            tilesRoot = GetOrCreateChildRoot(root, "Tiles");
    }

    private RectTransform GetOrCreateChildRoot(RectTransform parent, string name)
    {
        var found = parent.Find(name) as RectTransform;
        if (found != null) return found;
        return CreateChildRoot(parent, name);
    }

    private RectTransform GetOrCreateBoardBgRoot(RectTransform parent)
    {
        var found = parent.Find("BoardBG") as RectTransform;
        if (found != null)
        {
            if (found.TryGetComponent<Image>(out var foundImg))
            {
                foundImg.color = runtimeBoardBg;
                foundImg.raycastTarget = false;
            }
            return found;
        }

        var go = new GameObject("BoardBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = runtimeBoardBg;
        img.raycastTarget = false;

        return rt;
    }
    private RectTransform CreateChildRoot(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
    private void DrawGridLines()
    {
        if (gridLinesRoot == null || board == null)
            return;

        float thickness = Mathf.Max(1f, runtimeGridLineThickness);

        bool IsInside(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        bool IsVisibleCell(int x, int y)
        {
            if (!IsInside(x, y))
                return false;

            int idx = resolvedLevel.Index(x, y);
            return cellBgByIndex.ContainsKey(idx) && cellBgByIndex[idx] != null;
        }

        void CreateLine(string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(gridLinesRoot, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = runtimeGridLineColor;
            img.raycastTarget = false;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsVisibleCell(x, y))
                    continue;

                float x0 = x * tileSize;
                float y0 = -y * tileSize;
                float x1 = x0 + tileSize;
                float y1 = y0 - tileSize;

                // Top edge
                CreateLine(
                    $"GridLine_T_{x}_{y}",
                    new Vector2(x0, y0 + thickness * 0.5f),
                    new Vector2(tileSize, thickness)
                );

                // Left edge
                CreateLine(
                    $"GridLine_L_{x}_{y}",
                    new Vector2(x0 - thickness * 0.5f, y0),
                    new Vector2(thickness, tileSize)
                );

                // Right edge: sadece sağ komşu görünür değilse çiz.
                if (!IsVisibleCell(x + 1, y))
                {
                    CreateLine(
                        $"GridLine_R_{x}_{y}",
                        new Vector2(x1 - thickness * 0.5f, y0),
                        new Vector2(thickness, tileSize)
                    );
                }

                // Bottom edge: sadece alt komşu görünür değilse çiz.
                if (!IsVisibleCell(x, y + 1))
                {
                    CreateLine(
                        $"GridLine_B_{x}_{y}",
                        new Vector2(x0, y1 + thickness * 0.5f),
                        new Vector2(tileSize, thickness)
                    );
                }
            }
        }
    }
    private void SpawnCellBg(int x, int y)
    {
        var go = Instantiate(cellBgPrefab, cellBgRoot);
        var rt = go.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        go.transform.SetAsFirstSibling();

        int idx = resolvedLevel.Index(x, y);
        cellBgByIndex[idx] = go;
        if (go.TryGetComponent<Image>(out var image))
        {
            cellBgImageByIndex[idx] = image;
            baseCellBgColorByIndex[idx] = image.color;
        }
    }

    private void SpawnTile(int x, int y, TileType type)
    {
        var tile = Instantiate(tilePrefab, tilesRoot);
        var rt = tile.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        // Tile RectTransform tam hücre boyutunda; ikon boyutu ApplyTileSize ile ayarlanır.
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        // Sıralama ApplySortingOrder() ile yapılacak, burada müdahale etmeye gerek yok.
        var view = tile.GetComponent<TileView>();
        if (view == null)
        {
            Debug.LogError("GridSpawner: TileView yok.");
            Destroy(tile);
            return;
        }
        view.SetIconScale(iconScale);
        view.SetIconSize(iconSize);
        view.SetUseFullCellIcon(fullCellIcons);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        view.ApplyTileSize(tileSize);

        board.RegisterTile(view, x, y); // Init + coords + ilk SyncTileData (tipi henüz default olabilir)
        view.SetType(type);             // Doğru tipi ata
        board.SyncTileData(x, y);       // gridData'yı doğru tipte güncelle
        board.RefreshTileObstacleVisual(view);
        // Sıralama BuildInitialGrid sonunda toplu RefreshAllSortingOrders ile yapılacak
    }

    private void ApplyUnderTileCellBgTint()
    {
        if (resolvedLevel == null || resolvedLevel.obstacles == null || resolvedLevel.obstacleOrigins == null)
            return;

        foreach (var kv in cellBgImageByIndex)
        {
            var cellImage = kv.Value;
            if (cellImage == null) continue;

            int idx = kv.Key;
            if (!baseCellBgColorByIndex.TryGetValue(idx, out var baseColor))
                baseColor = cellImage.color;

            bool tint = false;
            if (idx >= 0 && idx < resolvedLevel.obstacles.Length)
            {
                int origin = idx < resolvedLevel.obstacleOrigins.Length ? resolvedLevel.obstacleOrigins[idx] : -1;
                if (origin >= 0 && obstacleDefsByOrigin.TryGetValue(origin, out var def) && def != null)
                {
                    var behavior = ResolveBehaviorForOrigin(origin, def);
                    tint = behavior == ObstacleBehaviorType.UnderTileLayered;
                }
            }

            cellImage.color = tint ? underTileCellBgTint : baseColor;
        }
    }

    private ObstacleBehaviorType ResolveBehaviorForOrigin(int originIndex, ObstacleDef fallbackDef)
    {
        if (originIndex < 0)
            return fallbackDef != null && fallbackDef.IsUnderTileBehavior
                ? ObstacleBehaviorType.UnderTileLayered
                : ObstacleBehaviorType.OverTileBlocker;

        if (board != null && board.ObstacleStateService != null)
        {
            int ox = originIndex % width;
            int oy = originIndex / width;
            if (board.ObstacleStateService.TryGetStageSnapshotAt(ox, oy, out var stage))
                return stage.behavior;
        }

        if (fallbackDef == null)
            return ObstacleBehaviorType.OverTileBlocker;

        var stageRule = fallbackDef.GetStageRuleForRemainingHits(Mathf.Max(1, fallbackDef.hits));
        return stageRule != null ? stageRule.behavior : ObstacleBehaviorType.OverTileBlocker;
    }

    private void MoveObstacleToBehaviorRoot(RectTransform obstacleRect, ObstacleBehaviorType behavior)
    {
        if (obstacleRect == null)
            return;

        var targetRoot = behavior == ObstacleBehaviorType.UnderTileLayered
            ? underTilesObstaclesRoot
            : overTilesObstaclesRoot;

        if (targetRoot == null || obstacleRect.parent == targetRoot)
            return;

        obstacleRect.SetParent(targetRoot, false);
    }

    private void HandleChestOpened(int originIndex)
    {
        if (_chestViews.TryGetValue(originIndex, out var view) && view != null)
            view.ShowAll();
    }

    private void HandleChestColorRemoved(int originIndex, ChestColorMask removedColor)
    {
        if (_chestViews.TryGetValue(originIndex, out var view) && view != null)
        {
            view.HideColor(removedColor);
            view.Shake();
        }
    }

    // ColorChest'i katmanli prefab olarak spawn eder.
    // Pozisyon ve boyut hesabi DrawObstacleImage ile aynidir (2x2 = 2*tileSize).
    private Image SpawnChestObstacleView(ObstacleDef def, int x, int y)
    {
        bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
        var parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;

        int w = Mathf.Max(1, def.size.x);
        int h = Mathf.Max(1, def.size.y);
        float gridOverlap = Mathf.Max(1f, Mathf.Ceil(runtimeGridLineThickness * 0.5f));
        int originIndex = resolvedLevel.Index(x, y);

        bool HasDifferentAt(int cx, int cy)
        {
            if (cx < 0 || cx >= width || cy < 0 || cy >= height) return false;
            int idx = resolvedLevel.Index(cx, cy);
            if (idx < 0 || idx >= resolvedLevel.obstacles.Length) return false;
            if ((ObstacleId)resolvedLevel.obstacles[idx] == ObstacleId.None) return false;
            return resolvedLevel.obstacleOrigins[idx] != originIndex;
        }

        bool diffLeft = false, diffRight = false, diffTop = false, diffBottom = false;
        for (int yy = y; yy < y + h; yy++) { if (HasDifferentAt(x - 1, yy)) diffLeft  = true; if (HasDifferentAt(x + w, yy)) diffRight  = true; }
        for (int xx = x; xx < x + w; xx++) { if (HasDifferentAt(xx, y - 1)) diffTop   = true; if (HasDifferentAt(xx, y + h)) diffBottom = true; }

        float lo = diffLeft   ? 0f : gridOverlap;
        float ro = diffRight  ? 0f : gridOverlap;
        float to = diffTop    ? 0f : gridOverlap;
        float bo = diffBottom ? 0f : gridOverlap;

        ChestObstacleView view;
        Image rootImage;

        if (chestObstacleViewPrefab != null)
        {
            view = Instantiate(chestObstacleViewPrefab, parent);
            rootImage = view.GetComponent<Image>();
            if (rootImage == null) rootImage = view.gameObject.AddComponent<Image>();
        }
        else
        {
            // Prefab atanmamissa plain Image ile devam et (sprite guncelleme calismaya devam eder)
            var fallback = new GameObject($"Obs_ColorChest_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fallback.transform.SetParent(parent, false);
            rootImage = fallback.GetComponent<Image>();
            view = null;
        }

        Sprite closedSprite = def.GetPreviewSprite();
        if (rootImage != null && closedSprite != null)
        {
            rootImage.sprite = closedSprite;
            rootImage.type = Image.Type.Simple;
            rootImage.preserveAspect = false;
        }

        var rt = rootImage != null ? rootImage.GetComponent<RectTransform>() : null;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * tileSize - lo, -y * tileSize + to);
            rt.sizeDelta = new Vector2(w * tileSize + lo + ro, h * tileSize + to + bo);
        }

        if (rootImage != null)
        {
            var clickProxy = rootImage.gameObject.AddComponent<ObstacleClickProxy>();
            clickProxy.Init(board, x, y);
        }

        if (view != null)
        {
            view.ApplyLayout(); // parent RT boyutu kesinleştikten sonra icon konumlarını set et
            view.HideAll();     // kapalı durum: renkli objeler gizli
            _chestViews[originIndex] = view;
        }

        return rootImage;
    }

    // BatteryBox: ColorChest ile aynı layout, fakat piller baştan açık ve 3 state'e sahip.
    private Image SpawnBatteryBoxView(ObstacleDef def, int x, int y)
    {
        bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
        var parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;

        int w = Mathf.Max(1, def.size.x);
        int h = Mathf.Max(1, def.size.y);
        float gridOverlap = Mathf.Max(1f, Mathf.Ceil(runtimeGridLineThickness * 0.5f));
        int originIndex = resolvedLevel.Index(x, y);

        bool HasDifferentAt(int cx, int cy)
        {
            if (cx < 0 || cx >= width || cy < 0 || cy >= height) return false;
            int idx = resolvedLevel.Index(cx, cy);
            if (idx < 0 || idx >= resolvedLevel.obstacles.Length) return false;
            if ((ObstacleId)resolvedLevel.obstacles[idx] == ObstacleId.None) return false;
            return resolvedLevel.obstacleOrigins[idx] != originIndex;
        }

        bool dL = false, dR = false, dT = false, dB = false;
        for (int yy = y; yy < y + h; yy++) { if (HasDifferentAt(x - 1, yy)) dL = true; if (HasDifferentAt(x + w, yy)) dR = true; }
        for (int xx = x; xx < x + w; xx++) { if (HasDifferentAt(xx, y - 1)) dT = true; if (HasDifferentAt(xx, y + h)) dB = true; }

        float lo = dL ? 0f : gridOverlap;
        float ro = dR ? 0f : gridOverlap;
        float to = dT ? 0f : gridOverlap;
        float bo = dB ? 0f : gridOverlap;

        BatteryBoxView view;
        Image rootImage;

        if (batteryBoxViewPrefab != null)
        {
            view = Instantiate(batteryBoxViewPrefab, parent);
            rootImage = view.GetComponent<Image>();
            if (rootImage == null) rootImage = view.gameObject.AddComponent<Image>();
        }
        else
        {
            var fallback = new GameObject($"Obs_BatteryBox_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fallback.transform.SetParent(parent, false);
            rootImage = fallback.GetComponent<Image>();
            view = fallback.AddComponent<BatteryBoxView>();
            CreateBatteryChildImages(view, fallback.transform);
        }

        Sprite boxSprite = def.GetPreviewSprite();
        if (rootImage != null && boxSprite != null)
        {
            rootImage.sprite = boxSprite;
            rootImage.type = Image.Type.Simple;
            rootImage.preserveAspect = false;
        }

        var rt = rootImage != null ? rootImage.GetComponent<RectTransform>() : null;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x * tileSize - lo, -y * tileSize + to);
            rt.sizeDelta = new Vector2(w * tileSize + lo + ro, h * tileSize + to + bo);
        }

        if (rootImage != null)
        {
            var clickProxy = rootImage.gameObject.AddComponent<ObstacleClickProxy>();
            clickProxy.Init(board, x, y);
            rootImage.raycastTarget = true;
        }

        if (view != null)
        {
            view.ApplyLayout();
            // BatteryBox baştan açık — tüm piller gösterilir, tam dolu state ile
            int hitsPerBattery = Mathf.Max(1, def.hits);
            view.ApplyBatteryState(ChestColorMask.Gear,  hitsPerBattery, hitsPerBattery);
            view.ApplyBatteryState(ChestColorMask.Core,  hitsPerBattery, hitsPerBattery);
            view.ApplyBatteryState(ChestColorMask.Bolt,  hitsPerBattery, hitsPerBattery);
            view.ApplyBatteryState(ChestColorMask.Plate, hitsPerBattery, hitsPerBattery);
            _batteryBoxViews[originIndex] = view;
        }

        return rootImage;
    }

    private static void CreateBatteryChildImages(BatteryBoxView view, Transform parent)
    {
        ChestColorMask[] colors = { ChestColorMask.Gear, ChestColorMask.Core, ChestColorMask.Bolt, ChestColorMask.Plate };
        foreach (var color in colors)
        {
            var go = new GameObject($"Battery_{color}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            view.SetBatteryImage(color, img);
        }
    }

    private void HandleBatteryHit(int originIndex, ChestColorMask color, int remaining)
    {
        if (!_batteryBoxViews.TryGetValue(originIndex, out var view) || view == null) return;
        if (!obstacleDefsByOrigin.TryGetValue(originIndex, out var def) || def == null) return;
        int maxHits = Mathf.Max(1, def.hits);
        view.ApplyBatteryState(color, remaining, maxHits);
        view.Shake();
    }

    private Image DrawObstacleImage(ObstacleDef def, int x, int y)
    {
        if (def == null)
            return null;

        // Movable obstacle burada ASLA büyütülmez.
        // O TileView üzerinden normal ikon gibi davranacak.
        if (def.IsMovableObstacle)
            return null;

        if (def.id == ObstacleId.ColorChest)
            return SpawnChestObstacleView(def, x, y);

        if (def.id == ObstacleId.BatteryBox)
            return SpawnBatteryBoxView(def, x, y);

        Sprite sprite = def.GetPreviewSprite();
        if (sprite == null) return null;

        int w = Mathf.Max(1, def.size.x);
        int h = Mathf.Max(1, def.size.y);

        var go = new GameObject($"Obs_{def.id}_{x}_{y}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
        var parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;
        go.transform.SetParent(parent, false);

        // Sadece static obstacle için grid çizgisi payını kapatıyoruz.
        // Static obstacle grid çizgisini kapatacak kadar büyür.
        // Böylece obstacle ile board grid çizgisi arasında boşluk kalmaz.
        // Obstacle dış kenarlarda grid çizgisini kapatsın,
        // ama başka bir obstacle ile ortak kenarda üst üste binmesin.
        float gridOverlap = Mathf.Max(1f, Mathf.Ceil(runtimeGridLineThickness * 0.5f));

        int originIndex = resolvedLevel.Index(x, y);

        bool HasDifferentObstacleAt(int cx, int cy)
        {
            if (cx < 0 || cx >= width || cy < 0 || cy >= height)
                return false;

            if (resolvedLevel == null ||
                resolvedLevel.obstacles == null ||
                resolvedLevel.obstacleOrigins == null)
                return false;

            int idx = resolvedLevel.Index(cx, cy);

            if (idx < 0 || idx >= resolvedLevel.obstacles.Length)
                return false;

            if ((ObstacleId)resolvedLevel.obstacles[idx] == ObstacleId.None)
                return false;

            if (idx >= resolvedLevel.obstacleOrigins.Length)
                return false;

            // Aynı obstacle'ın kendi hücreleri değil,
            // farklı origin'e sahip başka bir obstacle mı?
            return resolvedLevel.obstacleOrigins[idx] != originIndex;
        }

        bool hasDifferentLeft = false;
        bool hasDifferentRight = false;
        bool hasDifferentTop = false;
        bool hasDifferentBottom = false;

        for (int yy = y; yy < y + h; yy++)
        {
            if (HasDifferentObstacleAt(x - 1, yy))
                hasDifferentLeft = true;

            if (HasDifferentObstacleAt(x + w, yy))
                hasDifferentRight = true;
        }

        for (int xx = x; xx < x + w; xx++)
        {
            if (HasDifferentObstacleAt(xx, y - 1))
                hasDifferentTop = true;

            if (HasDifferentObstacleAt(xx, y + h))
                hasDifferentBottom = true;
        }

        // Başka obstacle olmayan dış kenarlarda grid çizgisini kapat.
        // Başka obstacle varsa o ortak kenarda taşma yapma.
        float leftOverlap = hasDifferentLeft ? 0f : gridOverlap;
        float rightOverlap = hasDifferentRight ? 0f : gridOverlap;
        float topOverlap = hasDifferentTop ? 0f : gridOverlap;
        float bottomOverlap = hasDifferentBottom ? 0f : gridOverlap;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        rt.anchoredPosition = new Vector2(
            x * tileSize - leftOverlap,
            -y * tileSize + topOverlap
        );

        rt.sizeDelta = new Vector2(
            w * tileSize + leftOverlap + rightOverlap,
            h * tileSize + topOverlap + bottomOverlap
        );

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = true;

        var clickProxy = go.AddComponent<ObstacleClickProxy>();
        clickProxy.Init(board, x, y);

        return img;
    }
    private void HandleObstacleCreatedDynamic(int x, int y)
    {
        if (resolvedLevel == null || resolvedLevel.obstacleLibrary == null) return;
        int idx = resolvedLevel.Index(x, y);
        if (obstacleViewsByOrigin.ContainsKey(idx)) return;
        if (resolvedLevel.obstacleOrigins[idx] != idx) return;

        var obsId = (ObstacleId)resolvedLevel.obstacles[idx];
        if (obsId == ObstacleId.None) return;

        var def = resolvedLevel.obstacleLibrary.Get(obsId);
        if (def == null || def.IsMovableObstacle) return;

        var image = DrawObstacleImage(def, x, y);
        if (image != null)
        {
            obstacleViewsByOrigin[idx] = image;
            obstacleDefsByOrigin[idx] = def;
        }
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (!obstacleViewsByOrigin.TryGetValue(change.originIndex, out var image) || image == null)
            return;

        if (change.cleared)
        {
            if (change.obstacleId == ObstacleId.EnergyContainer)
            {
                // Don't destroy — EnergyContainerFx takes over to show the exhausted state.
                obstacleViewsByOrigin.Remove(change.originIndex);
                obstacleDefsByOrigin.Remove(change.originIndex);
                return;
            }
            Destroy(image.gameObject);
            obstacleViewsByOrigin.Remove(change.originIndex);
            obstacleDefsByOrigin.Remove(change.originIndex);
            ApplyUnderTileCellBgTint();
            return;
        }

        if (ShouldLetEnergyContainerOwnVisual(change.originIndex))
            return;

        if (change.sprite != null)
            image.sprite = change.sprite;
    }

    private bool ShouldLetEnergyContainerOwnVisual(int originIndex)
    {
        if (originIndex < 0 || resolvedLevel == null || resolvedLevel.obstacles == null || originIndex >= resolvedLevel.obstacles.Length)
            return false;

        if ((ObstacleId)resolvedLevel.obstacles[originIndex] != ObstacleId.EnergyContainer)
            return false;

        if (energyContainerService == null)
            energyContainerService = FindFirstObjectByType<EnergyContainerService>();

        return energyContainerService != null && energyContainerService.IsExhausted(originIndex);
    }
    private void AutoFitTileSizeToMask()
    {
        if (spawnParent == null) return;

        RectTransform maskRt = spawnParent.parent as RectTransform; // BoardMask
        if (maskRt == null) return;

        float borderExtent = GetBorderExtentPx();

        float availableW = maskRt.rect.width - (boardPadding + borderExtent) * 2f - fitSafetyMarginPx * 2f;
        float availableH = maskRt.rect.height - (boardPadding + borderExtent) * 2f - fitSafetyMarginPx * 2f;

        int fitCols = useReferenceGridSizing ? referenceCols : width;
        int fitRows = useReferenceGridSizing ? referenceRows : height;

        int fit = Mathf.FloorToInt(Mathf.Min(availableW / fitCols, availableH / fitRows) * fitScale);
        tileSize = Mathf.Max(40, fit);
    }

    private float GetIconDrivenTileRatio()
    {
        if (fullCellIcons)
            return 1f;

        float ratioX = iconSize.x / Mathf.Max(1f, IconReferenceSize.x);
        float ratioY = iconSize.y / Mathf.Max(1f, IconReferenceSize.y);
        return Mathf.Max(0.1f, Mathf.Max(ratioX, ratioY));
    }

    private Vector2 GetVisualCellRectSize()
    {
        if (fullCellIcons)
            return new Vector2(tileSize, tileSize);

        float ratioX = iconSize.x / Mathf.Max(1f, IconReferenceSize.x);
        float ratioY = iconSize.y / Mathf.Max(1f, IconReferenceSize.y);
        return new Vector2(
            tileSize * Mathf.Max(0.1f, ratioX),
            tileSize * Mathf.Max(0.1f, ratioY));
    }

    private float GetBorderExtentPx()
    {
        var drawer = GetComponent<DynamicBoardBorder>();
        if (drawer == null) return 0f;

        // Border’ın grid dışına taştığı mesafe:
        // borderOutside + thickness/2
        return Mathf.Max(0f, drawer.borderOutside + drawer.straightH_height * 0.5f);
    }

}
