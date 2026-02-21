using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridSpawner : MonoBehaviour
{
    [Header("Level")]
    public LevelData level;
    [SerializeField] private LevelRuntimeSelector levelRuntimeSelector;

    [Header("Tile")]
    public GameObject tilePrefab;
    public TileIconLibrary iconLibrary;
    public BoardController board;

    [Header("Layout")]
    public int tileSize = 100;
    // Auto-fit ayarları
    [SerializeField] private int fitSafetyMarginPx = 8;   // border güvenlik payı
    [SerializeField, Range(0.8f, 1f)] private float fitScale = 0.96f; // ekstra küçültme oranı

    [Header("Border System")]
    public DynamicBoardBorder borderDrawer;

    [Tooltip("BoardContent iç boşluk (kenarlara yapışmayı keser)")]
    public int boardPadding = 8;

    [Header("Cell BG")]
    public GameObject cellBgPrefab;
    [SerializeField] private Color underTileCellBgTint = new Color(0.72f, 0.86f, 1f, 1f);

    [SerializeField, Range(0.5f, 1f)]
    private float iconScale = 0.82f;

    [Header("Spawn Parent (BoardMask altındaki BoardContent)")]
    [SerializeField] private RectTransform spawnParent;

    [Header("Roots (auto create)")]
    [SerializeField] private RectTransform cellBgRoot;
    [SerializeField] private RectTransform obstaclesRoot;
    [SerializeField] private RectTransform underTilesObstaclesRoot;
    [SerializeField] private RectTransform overTilesObstaclesRoot;
    [SerializeField] private RectTransform tilesRoot;
    [SerializeField] private RectTransform overTilesRoot;

    [Header("Obstacle Visual (UI)")]
    [SerializeField] private bool drawObstacles = true;

    [Header("Initial Resolve")]
    [SerializeField] private bool resolveInitialOnStart = false;

    [Header("Random Pool")]
    public TileType[] randomPool = { TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate };

    private int width;
    private int height;
    private LevelData resolvedLevel;
    private bool ownsResolvedLevelInstance;
    private readonly Dictionary<int, Image> obstacleViewsByOrigin = new();
    private readonly Dictionary<int, ObstacleDef> obstacleDefsByOrigin = new();
    private readonly Dictionary<int, GameObject> cellBgByIndex = new();
    private readonly Dictionary<int, Color> baseCellBgColorByIndex = new();

    private void Awake()
    {
        if (board == null) board = GetComponent<BoardController>()
                          ?? GetComponentInParent<BoardController>(true)
                          ?? FindFirstObjectByType<BoardController>();

        if (borderDrawer == null) borderDrawer = GetComponent<DynamicBoardBorder>();
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

        width = resolvedLevel.width;
        height = resolvedLevel.height;

        AutoFitTileSizeToMask(); 
        EnsureRoots();
        ApplyPaddingToSpawnParent();

        board.Init(width, height, iconLibrary);
        board.SetLevelData(resolvedLevel);
        board.SetupFactory(tilePrefab, tilesRoot, tileSize, randomPool);

        BindBoardEvents();

        // board init sonrası subscribe güvence
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;

        BuildInitialGrid();

        if (resolveInitialOnStart)
            StartCoroutine(board.ResolveInitial());
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

        // spawnParent her zaman ortada
        spawnParent.anchorMin = new Vector2(0.5f, 0.5f);
        spawnParent.anchorMax = new Vector2(0.5f, 0.5f);
        spawnParent.pivot     = new Vector2(0.5f, 0.5f);
        spawnParent.anchoredPosition = Vector2.zero;

        // spawnParent grid + padding alanını taşıyor
        spawnParent.sizeDelta = new Vector2(gridW + boardPadding * 2f, gridH + boardPadding * 2f);

        // içerideki root'lar grid top-left + padding'den başlasın
        Vector2 inner = new Vector2(boardPadding, -boardPadding);
        if (cellBgRoot != null) cellBgRoot.anchoredPosition = inner;
        if (tilesRoot != null)  tilesRoot.anchoredPosition  = inner;
        if (obstaclesRoot != null) obstaclesRoot.anchoredPosition = inner;

        if (underTilesObstaclesRoot != null) underTilesObstaclesRoot.anchoredPosition = inner;
        if (overTilesObstaclesRoot != null)  overTilesObstaclesRoot.anchoredPosition  = inner;
        // STEP 1: Fit BoardMask (RectMask2D) to grid size so spawned tiles above grid get clipped
        FitParentMaskToGrid();

    }


    private void FitParentMaskToGrid()
    {
        if (spawnParent == null) return;

        // BoardMask genelde spawnParent'ın parent'ı (BoardContent -> BoardMask)
        var mask = spawnParent.GetComponentInParent<UnityEngine.UI.RectMask2D>();
        if (mask == null) return;

        RectTransform maskRt = mask.rectTransform;

        float gridW = width * tileSize;
        float gridH = height * tileSize;

        // Mask'i center anchor/pivot'ta tut (sende zaten böyle)
        maskRt.anchorMin = new Vector2(0.5f, 0.5f);
        maskRt.anchorMax = new Vector2(0.5f, 0.5f);
        maskRt.pivot     = new Vector2(0.5f, 0.5f);
        maskRt.anchoredPosition = Vector2.zero;

        maskRt.sizeDelta = new Vector2(
            gridW + (boardPadding + GetBorderExtentPx()) * 2f,
            gridH + (boardPadding + GetBorderExtentPx()) * 2f
        );

    }

    private void BuildInitialGrid()
    {
        ClearChildren(cellBgRoot);
        ClearChildren(underTilesObstaclesRoot);
        ClearChildren(overTilesObstaclesRoot);
        ClearChildren(tilesRoot);
        obstacleViewsByOrigin.Clear();
        obstacleDefsByOrigin.Clear();
        cellBgByIndex.Clear();
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

        var initialTypes = board.SimulateInitialTypes();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (board.Holes[x, y]) continue;
            SpawnTile(x, y, initialTypes[x, y]);
        }

        // CellBG < UnderTileObs < Tiles < OverTileObs
        if (cellBgRoot != null) cellBgRoot.SetAsFirstSibling();
        if (underTilesObstaclesRoot != null) underTilesObstaclesRoot.SetAsFirstSibling();
        if (cellBgRoot != null) cellBgRoot.SetAsFirstSibling();
        if (tilesRoot != null) tilesRoot.SetAsLastSibling();
        if (overTilesObstaclesRoot != null) overTilesObstaclesRoot.SetAsLastSibling();

        var drawer = GetComponent<DynamicBoardBorder>();
        if (drawer != null)
        {
            drawer.level = resolvedLevel;
            drawer.tileSize = tileSize;

            // ✅ BorderRoot'u grid container'ın altına al ve aynı coordinate space'e sok
            if (drawer.borderRoot != null && spawnParent != null)
            {
                var br = drawer.borderRoot;

                br.SetParent(spawnParent, false);

                // spawnParent alanını kaplasın
                br.anchorMin = Vector2.zero;
                br.anchorMax = Vector2.one;
                br.pivot = new Vector2(0f, 1f);   // top-left referansı
                br.offsetMin = Vector2.zero;
                br.offsetMax = Vector2.zero;
                br.localScale = Vector3.one;
            }

            // ✅ artık offset sadece padding (grid top-left içeride)
            drawer.contentOffset = new Vector2(boardPadding, -boardPadding);

            drawer.includeObstaclesAsSolid = true;
            drawer.Draw(blocked);
        }


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

    private void SpawnCellBg(int x, int y)
    {
        var go = Instantiate(cellBgPrefab, cellBgRoot);
        var rt = go.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);

        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        int idx = resolvedLevel.Index(x, y);
        cellBgByIndex[idx] = go;
        if (go.TryGetComponent<Image>(out var image))
            baseCellBgColorByIndex[idx] = image.color;
    }

    private void SpawnTile(int x, int y, TileType type)
    {
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
            Debug.LogError("GridSpawner: TileView yok.");
            Destroy(tile);
            return;
        }
        view.SetIconScale(iconScale);
        view.ApplyTileSize(tileSize);

        board.RegisterTile(view, x, y);
        view.SetType(type);
        board.RefreshTileObstacleVisual(view);
    }

    private void ApplyUnderTileCellBgTint()
    {
        if (resolvedLevel == null || resolvedLevel.obstacles == null || resolvedLevel.obstacleOrigins == null)
            return;

        foreach (var kv in cellBgByIndex)
        {
            if (kv.Value == null) continue;
            if (!kv.Value.TryGetComponent<Image>(out var cellImage)) continue;

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

        // ✅ Grid + padding + border her iki yanda yer kaplar
        float availableW = maskRt.rect.width  - (boardPadding + borderExtent) * 2f - fitSafetyMarginPx * 2f;
        float availableH = maskRt.rect.height - (boardPadding + borderExtent) * 2f - fitSafetyMarginPx * 2f;

        int fit = Mathf.FloorToInt(Mathf.Min(availableW / width, availableH / height) * fitScale);
        tileSize = Mathf.Max(40, fit);
    }

    private float GetBorderExtentPx()
    {
        var drawer = GetComponent<DynamicBoardBorder>();
        if (drawer == null) return 0f;

        // Border’ın grid dışına taştığı mesafe:
        // borderOutside + thickness/2
        return Mathf.Max(0f, drawer.borderOutside + drawer.thickness * 0.5f);
    }

}
