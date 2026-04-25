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
    [SerializeField] private RectTransform obstaclesRoot;
    [SerializeField] private RectTransform underTilesObstaclesRoot;
    [SerializeField] private RectTransform overTilesObstaclesRoot;
    [SerializeField] private RectTransform tilesRoot;
    [SerializeField] private Color runtimeBoardBg = new Color(0.78f, 0.88f, 0.97f, 1f);
    [SerializeField] private Color runtimeNormalCell = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private RectTransform gridLinesRoot;
    [SerializeField] private Color runtimeGridLineColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField, Min(1f)] private float runtimeGridLineThickness = 2f;

    [SerializeField] private RectTransform boardBgRoot;

    [Header("Obstacle Visual (UI)")]
    [SerializeField] private bool drawObstacles = true;

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
    private readonly Dictionary<int, GameObject> cellBgByIndex = new();
    private readonly Dictionary<int, Image> cellBgImageByIndex = new();
    private readonly Dictionary<int, Color> baseCellBgColorByIndex = new();

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

        board.OnObstacleStageChanged += HandleObstacleStageChanged;
        board.OnObstacleDestroyed += HandleObstacleDestroyed;
        board.OnCellUnlocked += HandleCellUnlocked;
    }

    private void UnbindBoardEvents()
    {
        if (board == null) return;

        board.OnObstacleStageChanged -= HandleObstacleStageChanged;
        board.OnObstacleDestroyed -= HandleObstacleDestroyed;
        board.OnCellUnlocked -= HandleCellUnlocked;
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
        if (gridLinesRoot != null) gridLinesRoot.anchoredPosition = inner;
        if (tilesRoot != null) tilesRoot.anchoredPosition = inner;
        if (obstaclesRoot != null) obstaclesRoot.anchoredPosition = inner;
        if (underTilesObstaclesRoot != null) underTilesObstaclesRoot.anchoredPosition = inner;
        if (overTilesObstaclesRoot != null) overTilesObstaclesRoot.anchoredPosition = inner;
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
        cellBgByIndex.Clear();
        cellBgImageByIndex.Clear();
        baseCellBgColorByIndex.Clear();

        bool[] blocked = BuildBlockedMap();

        if (drawObstacles)
            DrawObstacleVisuals();

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
                    board.SetHole(x, y, true);
                    continue;
                }

                board.SetHole(x, y, false);
            }

        ApplyUnderTileCellBgTint();

        DrawGridLines();

        var initialTypes = board.SimulateInitialTypes();
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
                else
                {
                    SpawnTile(x, y, initialTypes[x, y]);
                }
            }

        // Tüm tile'lar spawn edildikten sonra sıralamayı toplu yenile
        board.RefreshAllSortingOrders();

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

        // Sıralama: CellBG < Tiles < Obstacles
        // Obstacle'lar (over/under dahil) tile'ların önünde render edilmeli
        if (cellBgRoot != null) cellBgRoot.SetAsFirstSibling();
        if (gridLinesRoot != null) gridLinesRoot.SetSiblingIndex(cellBgRoot != null ? cellBgRoot.GetSiblingIndex() + 1 : 0);
        if (tilesRoot != null) tilesRoot.SetAsLastSibling();
        if (obstaclesRoot != null) obstaclesRoot.SetAsLastSibling();

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
        clone.musicClip = source.musicClip;
        clone.musicVolume = source.musicVolume;
        clone.obstacleLibrary = source.obstacleLibrary;
        clone.goals = CloneGoals(source.goals);

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

                var image = DrawObstacleImage(def, x, y);
                if (image != null)
                {
                    obstacleViewsByOrigin[idx] = image;
                    obstacleDefsByOrigin[idx] = def;
                }
            }
    }

    private void HandleObstacleStageChanged(int originIndex, ObstacleStageSnapshot nextStage)
    {
        if (!obstacleViewsByOrigin.TryGetValue(originIndex, out var image) || image == null)
            return;

        if (nextStage.sprite != null)
            image.sprite = nextStage.sprite;

        MoveObstacleToBehaviorRoot(image.rectTransform, nextStage.behavior);
        ApplyUnderTileCellBgTint();
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        if (obstacleViewsByOrigin.TryGetValue(originIndex, out var image) && image != null)
            Destroy(image.gameObject);

        obstacleViewsByOrigin.Remove(originIndex);
        obstacleDefsByOrigin.Remove(originIndex);
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

        bool IsVisibleCell(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            return !board.Holes[x, y];
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

                // Top edge: her visible cell için çiz
                CreateLine(
                    $"GridLine_T_{x}_{y}",
                    new Vector2(x0, y0 + thickness * 0.5f),
                    new Vector2(tileSize, thickness)
                );

                // Left edge: her visible cell için çiz
                CreateLine(
                    $"GridLine_L_{x}_{y}",
                    new Vector2(x0 - thickness * 0.5f, y0),
                    new Vector2(thickness, tileSize)
                );

                // Right edge: sadece sağ komşu yoksa / hole ise çiz
                if (!IsVisibleCell(x + 1, y))
                {
                    CreateLine(
                        $"GridLine_R_{x}_{y}",
                        new Vector2(x1 - thickness * 0.5f, y0),
                        new Vector2(thickness, tileSize)
                    );
                }

                // Bottom edge: sadece alt komşu yoksa / hole ise çiz
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

    private Image DrawObstacleImage(ObstacleDef def, int x, int y)
    {
        Sprite sprite = def.GetPreviewSprite();
        if (sprite == null) return null;

        int w = Mathf.Max(1, def.size.x);
        int h = Mathf.Max(1, def.size.y);

        var go = new GameObject($"Obs_{def.id}_{x}_{y}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
        var parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(w * tileSize, h * tileSize);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false;
        img.raycastTarget = true;

        var clickProxy = go.AddComponent<ObstacleClickProxy>();
        clickProxy.Init(board, x, y);
        return img;
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (!obstacleViewsByOrigin.TryGetValue(change.originIndex, out var image) || image == null)
            return;

        if (change.cleared)
        {
            Destroy(image.gameObject);
            obstacleViewsByOrigin.Remove(change.originIndex);
            obstacleDefsByOrigin.Remove(change.originIndex);
            ApplyUnderTileCellBgTint();
            return;
        }

        if (change.sprite != null)
            image.sprite = change.sprite;
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