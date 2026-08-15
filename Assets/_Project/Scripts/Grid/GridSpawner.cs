using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridSpawner : MonoBehaviour
{
    private static readonly Vector2 IconReferenceSize = new Vector2(100f, 100f);
    private static readonly Vector3[] RectWorldCorners = new Vector3[4];
    private const float BarrellV2HitShakeDuration = 0.18f;
    private const float BarrellV2HitShakeCycles = 3f;
    private const float WardrobeFinalItemDestroyDelay = 0.75f;

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

    [Header("Grass Overlay")]
    [Tooltip("Grass hücre görsellerinin doğacağı root — taşların ÜSTÜNDE olmalı. Boşsa otomatik kurulur.")]
    [SerializeField] private RectTransform grassOverlayRoot;
    [SerializeField] private GrassOverlayService grassOverlayService;

    [Header("Key Generator")]
    [Tooltip("KeyGenerator katmanlı makine görseli (kol + draft + yeşil tarama). crankSprite=KG_C1, draftSprite=KG_D1 ata; boşsa eski pulse davranışı korunur. Servis runtime'da bu GO'ya eklendiği için ayarlar burada.")]
    [SerializeField] private KeyGeneratorMachineConfig keyGeneratorMachineConfig = new KeyGeneratorMachineConfig();

    [Header("Rocket Basket")]
    [SerializeField] private RocketBasketService rocketBasketService;
    [SerializeField] private Color runtimeBoardBg = new Color(0.78f, 0.88f, 0.97f, 1f);
    [SerializeField] private Color runtimeNormalCell = new Color(1f, 1f, 1f, 0.16f);
    [SerializeField] private RectTransform gridLinesRoot;
    [SerializeField] private Color runtimeGridLineColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField, Min(1f)] private float runtimeGridLineThickness = 2f;
    [SerializeField] private bool gridJunctionEnabled = false;
    [Tooltip("Diamond boyutu. 0 = otomatik (çizgi kalınlığı × 2.5).")]
    [SerializeField, Min(0f)] private float gridJunctionSize = 0f;
    [Tooltip("Yatay uzama. 1 = kare elmas ◇, >1 = geniş <> şekli.")]
    [SerializeField, Min(0.1f)] private float gridJunctionAspectX = 1.6f;
    [Tooltip("Dikey daralma. 1 = kare elmas ◇, <1 = yassı <> şekli.")]
    [SerializeField, Min(0.1f)] private float gridJunctionAspectY = 0.6f;

    [SerializeField] private RectTransform boardBgRoot;

    [Header("Tube Obstacle")]
    [SerializeField] private TubeObstacleService tubeObstacleService;
    [SerializeField] private TubeView tubeViewPrefab;
    [SerializeField] private RectTransform tubeRoot;

    [Header("Magnet Obstacle")]
    [SerializeField] private MagnetObstacleService magnetObstacleService;
    [SerializeField] private MagnetView magnetViewPrefab;
    [SerializeField] private RectTransform magnetRoot;

    [Header("Safe (Kasa) Obstacle")]
    [SerializeField] private SafeObstacleView safeViewPrefab;
    [Tooltip("Boşsa obstaclesRoot kullanılır.")]
    [SerializeField] private RectTransform safeRoot;
    private SafeObstacleService safeObstacleService;   // StampSafeCellsIntoLevel'de bulunur

    // Generic stacked-obstacle + Safe beneath kayıtları. Stamp aşaması (SetLevelData ÖNCESİ)
    // burada toplar; ObstacleStateService SetLevelData'da oluştuğu için kayıt SONRASINDA yapılır.
    private readonly System.Collections.Generic.List<(int cell, ObstacleId beneathId, int beneathOrigin, int overOrigin)>
        pendingStampedBeneath = new();

    // Overlay altındaki obstacle'ı BAŞTAN göstermek için: stamp aşamasında toplanan beneath
    // kayıtlarının kalıcı kopyası (pendingStampedBeneath register'da temizleniyor). Beneath
    // view'ları overlay'in ARKASINA çizilir; overlay kırılınca reveal bu view'ı promote eder.
    private readonly System.Collections.Generic.List<(int cell, ObstacleId beneathId, int beneathOrigin, int overOrigin)>
        stampedBeneathVisuals = new();
    private readonly Dictionary<int, Image> beneathViewsByCell = new();

    [Header("Obstacle Visual (UI)")]
    [SerializeField] private bool drawObstacles = true;
    [Tooltip("ColorChest icin katmanli gorsel prefabi (ChestObstacleView component'i olmali)")]
    [SerializeField] private ChestObstacleView chestObstacleViewPrefab;
    [Tooltip("BatteryBox icin katmanli gorsel prefabi (BatteryBoxView component'i olmali)")]
    [SerializeField] private BatteryBoxView batteryBoxViewPrefab;
    [Tooltip("OverrideBatteryBox icin katmanli gorsel prefabi (OverrideBatteryBoxView component'i olmali)")]
    [SerializeField] private OverrideBatteryBoxView overrideBatteryBoxViewPrefab;
    [Header("OverrideBatteryBox sprite'lari (prefab yoksa kod fallback kullanir)")]
    [Tooltip("Core / kirmizi bar sprite'i (yatay). Sol panele 90° donuk oturur.")]
    [SerializeField] private Sprite overrideBarRedSprite;
    [Tooltip("Gear / sari bar sprite'i (yatay). Sag panele 90° donuk oturur.")]
    [SerializeField] private Sprite overrideBarYellowSprite;
    [Tooltip("Bolt / mavi bar sprite'i (yatay). Ust panele duz oturur.")]
    [SerializeField] private Sprite overrideBarBlueSprite;
    [Tooltip("Plate / yesil bar sprite'i (yatay). Alt panele duz oturur.")]
    [SerializeField] private Sprite overrideBarGreenSprite;
    [Tooltip("Gauge ibresi (pin) sprite'i. Gobek dogru donsun diye needlePivot ayarla.")]
    [SerializeField] private Sprite overrideNeedleSprite;
    [Tooltip("Merkez gauge/dial sprite'i (ibresiz). Barlarin USTUNDE, ibrenin ALTINDA render edilir.")]
    [SerializeField] private Sprite overrideGaugeSprite;
    [Tooltip("Patlama oncesi buhar puf sprite'i (opsiyonel). Bossa duz beyaz kare kullanilir.")]
    [SerializeField] private Sprite overrideSteamSprite;
    [Header("OverrideBatteryBox yerlesim ayari (fallback, canli)")]
    [Tooltip("Mavi/yesil (ust/alt) bar genislik orani.")]
    [SerializeField, Range(0.2f, 1.2f)] private float obbHorizontalBarWidthScale = 0.85f;
    [Tooltip("Mavi/yesil (ust/alt) bar yukseklik orani.")]
    [SerializeField, Range(0.2f, 1.2f)] private float obbHorizontalBarHeightScale = 0.6f;
    [Tooltip("Kirmizi/sari (sol/sag, donuk) bar genislik orani.")]
    [SerializeField, Range(0.2f, 1.2f)] private float obbRotatedBarWidthScale = 0.62f;
    [Tooltip("Kirmizi/sari (sol/sag, donuk) bar yukseklik orani.")]
    [SerializeField, Range(0.2f, 1.2f)] private float obbRotatedBarHeightScale = 1.0f;
    [Tooltip("Ibre boyutu (kutunun kisa kenarina oran).")]
    [SerializeField, Range(0.1f, 1.2f)] private float obbNeedleSize = 0.34f;
    [Tooltip("Merkez gauge/dial boyutu (kutunun kisa kenarina oran).")]
    [SerializeField, Range(0.1f, 1.2f)] private float obbGaugeSize = 0.34f;
    [Tooltip("Gauge merkezi (kutu ici fraksiyon).")]
    [SerializeField] private Vector2 obbGaugeCenter = new Vector2(0.5f, 0.47f);
    [Tooltip("Ibre sprite'i icindeki gobek (pivot) fraksiyonu.")]
    [SerializeField] private Vector2 obbNeedlePivot = new Vector2(0.5f, 0.42f);
    [Tooltip("progress=0 iken ibre acisi (sol/yesil). Hizalarken 0 yapip gobegi ortala.")]
    [SerializeField] private float obbNeedleStartAngle = 90f;
    [Tooltip("progress=total iken ibre acisi (sag/kirmizi).")]
    [SerializeField] private float obbNeedleEndAngle = -90f;
    [Tooltip("Wardrobe obstacle prefabi (WardrobeObstacleView component'i olmali). Null bırakılırsa fallback ile spawn edilir.")]
    [SerializeField] private WardrobeObstacleView wardrobeObstacleViewPrefab;

    [Header("Initial Resolve")]
    [SerializeField] private bool resolveInitialOnStart = false;

    [Header("Random Pool")]
    [Tooltip("Varsayılan random havuz. LevelData.randomPool doluysa o levelda ONUN yerine level'inki kullanılır.")]
    public TileType[] randomPool = { TileType.Gear, TileType.Core, TileType.Bolt, TileType.Plate };

    // Level bazlı override çözülmüş hali: LevelData.randomPool doluysa o, değilse yukarıdaki varsayılan.
    private TileType[] effectiveRandomPool;

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
    private readonly Dictionary<int, Coroutine> obstacleHitShakeRoutines = new();
    private readonly Dictionary<int, Vector2> obstacleHitShakeBasePositions = new();
    private readonly Dictionary<int, ChestObstacleView> _chestViews = new();
    private readonly Dictionary<int, BatteryBoxView> _batteryBoxViews = new();
    private readonly Dictionary<int, OverrideBatteryBoxView> _overrideBatteryBoxViews = new();
    // Seri detonasyon: kutu destroy olunca view HEMEN patlatılmaz; burada bekler, BoardController
    // kuyruğu sırası gelince OnRequestObbDetonationView ile tetikler (çoklu kutu aynı anda patlamaz).
    private readonly Dictionary<int, OverrideBatteryBoxView> _pendingObbDetonationViews = new();
    private readonly Dictionary<int, WardrobeObstacleView> _wardrobeViews = new();
    private readonly Dictionary<int, GameObject> cellBgByIndex = new();
    private readonly Dictionary<int, Image> cellBgImageByIndex = new();
    private readonly Dictionary<int, Color> baseCellBgColorByIndex = new();
    private readonly Dictionary<int, GameObject> tubeClickProxyByCell = new();
    private readonly Dictionary<int, GameObject> safeClickProxyByCell = new();
    private EnergyContainerService energyContainerService;
    private KeyGeneratorService keyGeneratorService;
    private RectTransform cachedTopHudRect;
    private RectTransform cachedBottomAreaRect;

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
        StopAllObstacleHitShakes();
    }

    private void OnDestroy()
    {
        StopAllObstacleHitShakes();

        if (ownsResolvedLevelInstance && resolvedLevel != null)
            Destroy(resolvedLevel);

        resolvedLevel = null;
        ownsResolvedLevelInstance = false;
    }

    private IEnumerator Start()
    {
        // SafeAreaFitter applies after the first frame. Build the board after that so it
        // appears directly in its final TopHUD/BottomBar-centered slot instead of shifting.
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        resolvedLevel = ResolveLevelData();
        ApplyResolvedLevelToConsumers(resolvedLevel);
        pendingStampedBeneath.Clear();
        StampTubeCellsIntoLevel(resolvedLevel);    // must happen before SetLevelData
        StampMagnetCellsIntoLevel(resolvedLevel);  // must happen before SetLevelData
        StampStackedObstaclesIntoLevel(resolvedLevel); // must happen before SetLevelData (saves beneath content)
        StampSafeCellsIntoLevel(resolvedLevel);    // must happen after generic stacks so Safe is the top cover

        if (board == null || resolvedLevel == null || tilePrefab == null || iconLibrary == null || cellBgPrefab == null)
        {
            Debug.LogError("GridSpawner: Eksik referans var (board/resolvedLevel/tilePrefab/iconLibrary/cellBgPrefab).");
            enabled = false;
            yield break;
        }
        PlayLevelMusic(resolvedLevel);
        width = resolvedLevel.width;
        height = resolvedLevel.height;

        if (!useFixedTileSize)
            AutoFitTileSizeToMask();

        EnsureRoots();
        ApplyPaddingToSpawnParent();
        board.RebaseShakeHomeToCurrent();

        // Event item'ları level boyunca staging'de bekler: kazanınca commit,
        // kaybetmeyi kabul edince discard (LevelEndSimplePopupController yönetir).
        ProgressEventService.Instance?.BeginLevelStaging();

        board.Init(width, height, iconLibrary);
        board.SetLevelData(resolvedLevel);
        EnsureKeyGeneratorService();

        // ObstacleStateService artık var (SetLevelData onu init etti). Stamp aşamasında toplanan
        // beneath kayıtlarını şimdi push et — init store'ları temizledikten SONRA.
        RegisterPendingStampedBeneath();

        effectiveRandomPool = resolvedLevel.randomPool != null && resolvedLevel.randomPool.Length > 0
            ? resolvedLevel.randomPool
            : randomPool;

        board.SetupFactory(tilePrefab, tilesRoot, tileSize, effectiveRandomPool, iconScale, fullCellIcons, iconSize);

        BindBoardEvents();

        // board init sonrası subscribe güvence
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;

        BuildInitialGrid();

        // Board taşlar dizili halde sağdan sola kayarak otursun (giriş animasyonu).
        // Initial settle (varsa) ekran dışında çalışır; sonra board kayarak gelir.
        StartCoroutine(board.PlayBoardEntrance(resolveInitialOnStart ? board.ResolveInitial() : null));
    }

    private void PlayLevelMusic(LevelData activeLevel)
    {
        if (MusicManager.Instance == null)
        {
            Debug.LogWarning("GridSpawner: MusicManager sahnede yok, level müziği çalınamadı.");
            return;
        }

        if (MusicState.TryGetSelectedTrack(out var selectedClip, out var selectedVolume))
        {
            MusicManager.Instance.Play(selectedClip, selectedVolume);
            return;
        }

        if (activeLevel == null)
            return;

        if (activeLevel.musicClip == null)
            return;

        MusicManager.Instance.Play(activeLevel.musicClip, activeLevel.musicVolume);
    }
    private void BindBoardEvents()
    {
        UnbindBoardEvents();
        if (board == null) return;

        board.ObstacleViewByOriginLookup    = GetObstacleViewImage;
        board.OnObstacleStageChanged       += HandleObstacleStageChanged;
        board.OnObstacleDestroyed           += HandleObstacleDestroyed;
        board.OnCellUnlocked                += HandleCellUnlocked;
        board.OnObstacleCreatedDynamic      += HandleObstacleCreatedDynamic;
        board.OnObstacleViewRestored        += HandleObstacleCreatedDynamic;
        board.OnChestOpened                 += HandleChestOpened;
        board.OnChestColorRemoved           += HandleChestColorRemoved;
        board.OnBatteryHit                  += HandleBatteryHit;
        board.OnOverrideBatteryBoxHit       += HandleOverrideBatteryBoxHit;
        board.OnRequestObbDetonationView    += HandleRequestObbDetonationView;
        board.OnWardrobeOpened              += HandleWardrobeOpened;
        board.OnWardrobeItemRemoved         += HandleWardrobeItemRemoved;
    }

    private void EnsureKeyGeneratorService()
    {
        if (keyGeneratorService != null)
            return;

        keyGeneratorService = GetComponent<KeyGeneratorService>()
                              ?? GetComponentInChildren<KeyGeneratorService>(true)
                              ?? FindFirstObjectByType<KeyGeneratorService>();

        if (keyGeneratorService == null)
            keyGeneratorService = gameObject.AddComponent<KeyGeneratorService>();

        // Servis runtime'da eklendiği için editörde inspector'ı yok; makine görseli ayarlarını
        // GridSpawner'dan (gerçek scene/prefab objesi) buraya pushluyoruz.
        keyGeneratorService.ApplyMachineConfig(keyGeneratorMachineConfig);
    }

    private void UnbindBoardEvents()
    {
        if (board == null) return;

        board.OnObstacleStageChanged       -= HandleObstacleStageChanged;
        board.OnObstacleDestroyed          -= HandleObstacleDestroyed;
        board.OnCellUnlocked               -= HandleCellUnlocked;
        board.OnObstacleCreatedDynamic     -= HandleObstacleCreatedDynamic;
        board.OnObstacleViewRestored       -= HandleObstacleCreatedDynamic;
        board.OnChestOpened                -= HandleChestOpened;
        board.OnChestColorRemoved          -= HandleChestColorRemoved;
        board.OnBatteryHit                 -= HandleBatteryHit;
        board.OnOverrideBatteryBoxHit      -= HandleOverrideBatteryBoxHit;
        board.OnRequestObbDetonationView   -= HandleRequestObbDetonationView;
        board.OnWardrobeOpened             -= HandleWardrobeOpened;
        board.OnWardrobeItemRemoved        -= HandleWardrobeItemRemoved;
    }

    private void ApplyPaddingToSpawnParent()
    {
        if (spawnParent == null) return;

        float gridW = width * tileSize;
        float gridH = height * tileSize;
        Vector2 slotSize = GetBoardSlotSize(gridW, gridH);

        spawnParent.anchorMin = new Vector2(0.5f, 0.5f);
        spawnParent.anchorMax = new Vector2(0.5f, 0.5f);
        spawnParent.pivot = new Vector2(0.5f, 0.5f);
        spawnParent.anchoredPosition = Vector2.zero;

        spawnParent.sizeDelta = new Vector2(
            slotSize.x + boardPadding * 2f,
            slotSize.y + boardPadding * 2f
        );
        AlignBoardRootToLayoutSlot(spawnParent.sizeDelta);

        Vector2 inner = GetBoardContentAnchoredPosition(gridW, gridH, slotSize);

        ApplyCellAnchoredRootPosition(cellBgRoot, inner);
        ApplyCellAnchoredRootPosition(mudOverlayRoot, inner);
        ApplyCellAnchoredRootPosition(grassOverlayRoot, inner);   // grass PlaceInCell top-left hizası (mud ile aynı)
        ApplyCellAnchoredRootPosition(gridLinesRoot, inner);
        ApplyCellAnchoredRootPosition(tilesRoot, inner);
        ApplyCellAnchoredRootPosition(obstaclesRoot, inner);
        ApplyCellAnchoredRootPosition(underTilesObstaclesRoot, inner);
        ApplyCellAnchoredRootPosition(overTilesObstaclesRoot, inner);
        ApplyCellAnchoredRootPosition(tubeRoot, inner);
        ApplyCellAnchoredRootPosition(magnetRoot, inner);
        ApplyCellAnchoredRootPosition(safeRoot, inner);
    }

    private void AlignBoardRootToLayoutSlot(Vector2 contentSize)
    {
        if (IsBossDuelLevel() || spawnParent == null)
            return;

        var maskRoot = spawnParent.parent as RectTransform;
        var boardRoot = maskRoot != null ? maskRoot.parent as RectTransform : null;
        var boardArea = boardRoot != null ? boardRoot.parent as RectTransform : null;
        if (boardRoot == null || boardArea == null)
            return;

        Vector2 frameSize = GetBoardFrameSize(contentSize);

        boardRoot.sizeDelta = frameSize;
        if (maskRoot != null && maskRoot.parent == boardRoot)
            maskRoot.sizeDelta = frameSize;

        if (boardArea.parent is RectTransform layoutParent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutParent);
        Canvas.ForceUpdateCanvases();

        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = boardRoot.anchorMin;
        boardRoot.pivot = new Vector2(0.5f, 0.5f);
        boardRoot.anchoredPosition = Vector2.zero;
        boardRoot.localRotation = Quaternion.identity;
        boardRoot.localScale = Vector3.one;

        if (TryGetBoardPlayAreaWorldCenter(boardArea, out Vector3 playAreaCenter))
            boardRoot.position = playAreaCenter;
    }

    private bool TryGetBoardPlayAreaWorldCenter(RectTransform boardArea, out Vector3 center)
    {
        center = Vector3.zero;
        if (boardArea == null)
            return false;

        cachedTopHudRect = ResolveCachedRect(cachedTopHudRect, boardArea, "TopHUD");
        if (cachedBottomAreaRect == null)
            cachedBottomAreaRect = FindRectInCanvas(boardArea, "BottomArea")
                ?? FindRectInCanvas(boardArea, "BottomBar");

        RectTransform topHud = cachedTopHudRect;
        RectTransform bottomArea = cachedBottomAreaRect;
        if (topHud == null || bottomArea == null)
            return false;

        float topHudBottomY = GetWorldBottomY(topHud);
        float bottomAreaTopY = GetWorldTopY(bottomArea);
        if (topHudBottomY <= bottomAreaTopY)
            return false;

        center = GetWorldCenter(boardArea);
        center.y = (topHudBottomY + bottomAreaTopY) * 0.5f;
        return true;
    }

    private static RectTransform ResolveCachedRect(RectTransform cached, RectTransform source, string rectName)
    {
        return cached != null ? cached : FindRectInCanvas(source, rectName);
    }

    private static RectTransform FindRectInCanvas(RectTransform source, string rectName)
    {
        if (source == null || string.IsNullOrEmpty(rectName))
            return null;

        var canvas = source.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : source.root;
        var rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            var rect = rects[i];
            if (rect != null && rect.name == rectName)
                return rect;
        }

        return null;
    }

    private static Vector3 GetWorldCenter(RectTransform rect)
    {
        rect.GetWorldCorners(RectWorldCorners);
        return (RectWorldCorners[0] + RectWorldCorners[2]) * 0.5f;
    }

    private static float GetWorldTopY(RectTransform rect)
    {
        rect.GetWorldCorners(RectWorldCorners);
        return RectWorldCorners[1].y;
    }

    private static float GetWorldBottomY(RectTransform rect)
    {
        rect.GetWorldCorners(RectWorldCorners);
        return RectWorldCorners[0].y;
    }

    private void ApplyCellAnchoredRootPosition(RectTransform root, Vector2 inner)
    {
        if (root == null || root == spawnParent)
            return;

        RectTransform boardRoot = spawnParent != null ? spawnParent : (RectTransform)transform;
        bool isChildOfObstacleRoot = obstaclesRoot != null && root.parent == obstaclesRoot;
        if (root.parent != boardRoot && !isChildOfObstacleRoot)
            return;

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = isChildOfObstacleRoot ? Vector2.zero : inner;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
    }

    private Vector2 GetBoardSlotSize(float gridW, float gridH)
    {
        return new Vector2(gridW, gridH);
    }

    private bool IsBossDuelLevel()
        => resolvedLevel != null && resolvedLevel.levelKind == LevelKind.BossDuel;

    private Vector2 GetBoardContentAnchoredPosition()
    {
        float gridW = width * tileSize;
        float gridH = height * tileSize;
        return GetBoardContentAnchoredPosition(gridW, gridH, GetBoardSlotSize(gridW, gridH));
    }

    private Vector2 GetBoardContentAnchoredPosition(float gridW, float gridH, Vector2 slotSize)
    {
        Vector2 gridOffset = new Vector2((slotSize.x - gridW) * 0.5f, -(slotSize.y - gridH) * 0.5f);
        return new Vector2(boardPadding, -boardPadding) + gridOffset;
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
        br.sizeDelta = GetBoardFrameSize(spawnParent.rect.size);

        br.localRotation = Quaternion.identity;
        br.localScale = Vector3.one;
    }

    private void BuildInitialGrid()
    {
        StopAllObstacleHitShakes();
        ClearChildren(cellBgRoot);
        ClearChildren(gridLinesRoot);
        ClearChildren(underTilesObstaclesRoot);
        ClearChildren(overTilesObstaclesRoot);
        ClearChildren(tilesRoot);
        obstacleViewsByOrigin.Clear();
        obstacleDefsByOrigin.Clear();
        beneathViewsByCell.Clear();
        _chestViews.Clear();
        cellBgByIndex.Clear();
        cellBgImageByIndex.Clear();
        baseCellBgColorByIndex.Clear();
        tubeClickProxyByCell.Clear();
        safeClickProxyByCell.Clear();

        bool[] blocked = BuildBlockedMap();

        if (drawObstacles)
        {
            // Grass artık standart obstacle image yolu ile çiziliyor ama grassOverlayRoot'a
            // (magnetin üstü, mask dışı). Root'u önce hazırla ki DrawObstacleVisuals grass'ı oraya koysun.
            EnsureGrassRootSetup();
            DrawObstacleVisuals();
            DrawStampedBeneathVisuals();   // overlay altındaki obstacle'ı arkada baştan göster
            DrawMudOverlays();
            DrawTubeObstacles();
            DrawMagnetObstacles();
            DrawSafeObstacles();
            // Grass root'unu border'ın üstüne taşı (worldPositionStays: hücre hizası korunur) +
            // child layer'larını border'a eşle — yapraklar mask'a takılmaz, magnetin üstünde kalır.
            LiftGrassRootAboveBorder();
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

        // İlk açılışta yalnızca gravity'nin GERÇEKTEN erişebildiği hücrelere taş koy.
        // Normal kural: hole'lar geçirgen (taş hole'dan akar + yanından diagonal kayar),
        // sadece gravity-blocklayan obstacle'lar (chest vb.) akışı durdurur. Erişilemeyen
        // hücreler (örn. chest'in dikey gölgesindeki orta mud) boş bırakılır — chest
        // kırılınca runtime cascade (CalculateCascades) o hücreleri kendi doldurur.
        bool[,] gravityIsolated = new bool[width, height];
        if (board.CascadeLogic != null)
        {
            bool[,] reachable = board.CascadeLogic.ComputeGravityReachableMask();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (board.Holes[x, y]) continue;
                    if (!reachable[x, y])
                        gravityIsolated[x, y] = true;
                }
        }

        // Tip simülasyonu/oynanabilirlik garantisi de izole hücreleri hariç tutmalı,
        // aksi hâlde garanti edilen tek hamle boş bırakılan bölgeye düşebilir.
        var initialTypes = board.SimulateInitialTypes(gravityIsolated);

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
                    // Gravity'nin erişemediği hücre: ilk açılışta taş koyma (boş mud kalır).
                    // İSTİSNA: holdsTile obstacle (Oil) cell'inde bir taş TUTULUR. Oil görseli
                    // artık cell-anchored (OilOverlayRenderer) olduğu için GÖRSEL açıdan tile
                    // gerekmez; ama gameplay için altta bir taş bulunmalı — oil temizlenince o taş
                    // açığa çıksın (yoksa boş hücre kalır). Tamamı oil olan sütunlar gravity-izole
                    // sayıldığından spawn'da hiç taş almıyordu; holdsTile hücrelerini izole olsalar
                    // da seed et (gravity-blocked oldukları için taş yerinde kalır).
                    bool holdsTileCell = board.ObstacleStateService != null
                        && board.ObstacleStateService.HoldsTileAt(x, y);

                    int idx = resolvedLevel.Index(x, y);

                    // Designer'ın elle koyduğu pinned special (emitter) / pinned tile'ı ÖNCE oku.
                    // Bunlar blocker'larla çevrili gravity-izole bir hücrede olsa bile spawn
                    // EDİLMELİ — yoksa (örn. alt köşe emitter'ları) hiç yerleşmezdi ("2 eklendi,
                    // gerisi eklenmedi" bug'ı).
                    TileSpecial pinnedSpecial = TileSpecial.None;
                    if (resolvedLevel.pinnedSpecialTypes != null && idx < resolvedLevel.pinnedSpecialTypes.Length)
                        pinnedSpecial = (TileSpecial)resolvedLevel.pinnedSpecialTypes[idx];

                    int pinnedTileVal = 0;
                    if (resolvedLevel.pinnedTileTypes != null && idx < resolvedLevel.pinnedTileTypes.Length)
                        pinnedTileVal = resolvedLevel.pinnedTileTypes[idx];

                    bool hasPinned = pinnedSpecial != TileSpecial.None || pinnedTileVal > 0;

                    // Per-obstacle gölge dolgusu: hücrenin üstündeki kolondaki TÜM blocker'lar
                    // fillsShadowBeneath=true ise (örn. Oil duvarı) gölge hücre de dolu başlar.
                    // Chest gibi default-false blocker varsa eski davranış: boş kalır.
                    bool fillShadowed = gravityIsolated[x, y] && !holdsTileCell && !hasPinned
                        && ShadowCastersAllowFill(x, y);

                    if (gravityIsolated[x, y] && !holdsTileCell && !hasPinned && !fillShadowed)
                        continue;

                    TileType tileType = initialTypes[x, y];

                    // İzole hücre (holdsTile/pinned/fillShadowed) VEYA interaction-locked hücre (Grass/Oil)
                    // SimulateInitialTypes'ta locked sayılıp default tip (hep aynı) bırakıldı; grass'la
                    // kaplı board'da tüm taşlar aynı geliyordu. Rastgele tip ver — sol/üst komşuya bakarak
                    // anlık 3'lü oluşturmadan (pinned tile type varsa aşağıda ezilir). Locked hücrede match
                    // kilitli olduğu için "3'lü" görsel de sorun değil.
                    bool interactionLocked = board.ObstacleStateService != null
                        && board.ObstacleStateService.IsInteractionLockedAt(x, y);
                    if (((gravityIsolated[x, y] && (holdsTileCell || hasPinned || fillShadowed)) || interactionLocked)
                        && effectiveRandomPool != null && effectiveRandomPool.Length > 0)
                        tileType = PickIsolatedRandomType(x, y, effectiveRandomPool);

                    if (pinnedTileVal > 0)
                        tileType = (TileType)(pinnedTileVal - 1);

                    SpawnTile(x, y, tileType);

                    if (pinnedSpecial != TileSpecial.None)
                    {
                        var view = board.GetTileViewAt(x, y);
                        if (view != null)
                        {
                            if (pinnedSpecial == TileSpecial.SystemOverride)
                                view.SetOverrideBaseType(tileType, deferVisualUpdate: true);
                            view.SetSpecial(pinnedSpecial);
                            board.SyncTileData(x, y);
                        }
                    }
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

        // Sıralama: CellBG < GridLines < underlays < Tiles < Static Obstacles < Line-Travel VFX
        // Hücre içi grid çizgileri obstacle'ların altında kalır; dynamic board border ise
        // BoardMask altında BoardContent'in üstünde tutulur.
        // Animasyon VFX parent'ları (lineTravelSpawnParent, afterImageParent) spawnParent'ın
        // direkt çocuğuysa son sıraya alınır — böylece combo animasyonları grid çizgilerinin
        // üstünde render edilir.
        if (cellBgRoot != null) cellBgRoot.SetAsFirstSibling();
        // Mud overlay tile altında kalır; hücre içi grid çizgileri obstacle'ları bölmez.
        if (mudOverlayRoot != null) mudOverlayRoot.SetSiblingIndex(1);
        if (tilesRoot != null) tilesRoot.SetAsLastSibling();
        if (gridLinesRoot != null) gridLinesRoot.SetSiblingIndex(1);
        if (underTilesObstaclesRoot != null) underTilesObstaclesRoot.SetSiblingIndex(2);
        if (mudOverlayRoot != null) mudOverlayRoot.SetSiblingIndex(3);
        if (obstaclesRoot != null) obstaclesRoot.SetAsLastSibling();
        // Grass örtüsü taşların ÜSTÜNDE görünmeli (görüntüyü örter) → tiles/obstacles'tan sonra son sıraya.
        if (grassOverlayRoot != null) grassOverlayRoot.SetAsLastSibling();

        BringForegroundRootsToFront();

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
            PlaceBorderRootAboveBoardContent(drawer.borderRoot);

            if (grassOverlayRoot != null)
                grassOverlayRoot.SetAsLastSibling();
            BringForegroundRootsToFront();
        }


    }

    private void PlaceBorderRootAboveBoardContent(RectTransform borderRoot)
    {
        if (borderRoot == null)
            return;

        if (tilesRoot != null && borderRoot.parent == tilesRoot.parent)
        {
            int tileIndex = tilesRoot.GetSiblingIndex();
            if (borderRoot.GetSiblingIndex() <= tileIndex)
                borderRoot.SetSiblingIndex(tileIndex + 1);
            return;
        }

        if (spawnParent != null && borderRoot.parent == spawnParent.parent)
        {
            int spawnIndex = spawnParent.GetSiblingIndex();
            if (borderRoot.GetSiblingIndex() <= spawnIndex)
                borderRoot.SetSiblingIndex(spawnIndex + 1);
        }
    }

    private Vector2 GetBoardFrameSize(Vector2 contentSize)
    {
        float extraPadding = Mathf.Max(0f, GetRequiredBorderPadding() - boardPadding);
        if (extraPadding <= 0f)
            return contentSize;

        return contentSize + new Vector2(extraPadding * 2f, extraPadding * 2f);
    }

    private float GetRequiredBorderPadding()
    {
        if (borderDrawer == null)
            return boardPadding;

        float borderThickness = Mathf.Max(1f, borderDrawer.borderThickness);
        float spriteCoreRatio = Mathf.Clamp(borderDrawer.spriteCoreRatio, 0.1f, 1f);
        float visibleBorderThickness = borderThickness * spriteCoreRatio;
        float outsideCenterOffset = borderDrawer.keepBorderOutsideGrid
            ? Mathf.Max(0f, borderDrawer.outsideGap) + visibleBorderThickness * 0.5f
            : 0f;

        return Mathf.Max(boardPadding, Mathf.Ceil(outsideCenterOffset + borderThickness * 0.5f));
    }

    private void BringForegroundRootsToFront()
    {
        // Magnet path overlay'i (glow yol + uç sprite'ları) taşların ve obstacle'ların ÜSTÜNDE
        // render edilmeli; aksi halde tilesRoot.SetAsLastSibling magnet'i taşların arkasında bırakır
        // ve soft glow yol tamamen kaybolur. magnetRoot AYRI bir child root olmalı (spawnParent'ın
        // KENDİSİ değil) — root'un kendisini reorder etmek anlamsızdır, o yüzden o durumu atlıyoruz.
        // (Inspector'da magnetRoot None bırakılırsa GridSpawner otomatik "MagnetObstacles" child'ı kurar.)
        var magnetBoardRoot = spawnParent != null ? spawnParent : (RectTransform)transform;
        if (magnetRoot != null && magnetRoot != magnetBoardRoot)
            magnetRoot.SetAsLastSibling();

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
    }

    // Gölgedeki boş hücre ilk dağıtımda doldurulsun mu? Hücrenin ÜSTÜNDEKİ kolonu tarar:
    // en az bir gravity-blocker (blocksCells veya holdsTile) bulunmalı ve blocker'ların
    // HEPSİ fillsShadowBeneath=true olmalı. Chest gibi default-false bir blocker varsa
    // (veya kolonda hiç blocker yoksa — salt geometri gölgesi) eski davranış: boş kalır.
    private bool ShadowCastersAllowFill(int x, int y)
    {
        var lib = resolvedLevel != null ? resolvedLevel.obstacleLibrary : null;
        var obstacles = board != null ? board.ObstacleStateService : null;
        if (lib == null || obstacles == null) return false;

        bool anyBlocker = false;
        for (int yy = y - 1; yy >= 0; yy--)
        {
            bool blocks = obstacles.IsCellBlocked(x, yy) || obstacles.HoldsTileAt(x, yy);
            if (!blocks) continue;

            anyBlocker = true;
            var def = lib.Get(obstacles.GetObstacleIdAt(x, yy));
            if (def == null || !def.fillsShadowBeneath)
                return false;
        }
        return anyBlocker;
    }

    // İzole (gravity-gölgesi) hücre için rastgele tip: sol-sol ve üst-üst komşulara bakarak
    // ilk dağıtımda anlık 3'lü match oluşturmayı önler. Spawn döngüsü satır-satır (üst→alt,
    // sol→sağ) ilerlediğinden sol ve üst taşlar bu noktada zaten yerleşmiştir.
    private TileType PickIsolatedRandomType(int x, int y, TileType[] pool)
    {
        TileType? banH = null, banV = null;

        var l1 = board.GetTileViewAt(x - 1, y);
        var l2 = board.GetTileViewAt(x - 2, y);
        if (l1 != null && l2 != null && l1.GetTileType() == l2.GetTileType())
            banH = l1.GetTileType();

        var u1 = board.GetTileViewAt(x, y - 1);
        var u2 = board.GetTileViewAt(x, y - 2);
        if (u1 != null && u2 != null && u1.GetTileType() == u2.GetTileType())
            banV = u1.GetTileType();

        for (int attempt = 0; attempt < 12; attempt++)
        {
            var t = pool[UnityEngine.Random.Range(0, pool.Length)];
            if ((banH == null || t != banH.Value) && (banV == null || t != banV.Value))
                return t;
        }
        return pool[UnityEngine.Random.Range(0, pool.Length)];
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
        view.SetFullCellMovableSprite(def.fullCellSprite);
        view.SetVisualLayout(TileView.TileVisualLayout.Centered);
        view.ApplyTileSize(tileSize);

        // GoldMoney: ince idle "para dönme" animasyonu (arada bir sağa-sola).
        if (obsId == ObstacleId.GoldMoney && view.IconImage != null &&
            view.IconImage.GetComponent<CoinIdleWobble>() == null)
            view.IconImage.gameObject.AddComponent<CoinIdleWobble>();

        // Cargo (işçi robot): süzülerek iniyor hissi — sürekli hafif tilt sway.
        if (def.exitAtBottom && view.IconImage != null &&
            view.IconImage.GetComponent<CargoFloatSway>() == null)
            view.IconImage.gameObject.AddComponent<CargoFloatSway>();

        board.RegisterTile(view, x, y);

        Sprite obstacleSprite = def.GetPreviewSprite();
        if (obstacleSprite != null)
            view.SetMovableObstacleSprite(obstacleSprite);

        var pool = effectiveRandomPool ?? randomPool;
        var dummyType = pool != null && pool.Length > 0
            ? pool[0]
            : TileType.Gear;
        view.SetType(dummyType);
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

    // Runtime'da level üzerinde mutasyon yaparız (obstacle stamp'leme, hedef ilerlemesi vb.) ama
    // kaynak ScriptableObject ASSET'i kirletmemeliyiz. Bu yüzden runtime için bağımsız bir kopya alırız.
    //
    // Unity'nin Instantiate'i serialization-tabanlı derin kopya yapar: TÜM serialized alanlar (diziler,
    // [Serializable] sınıflar dahil) yeni instance olarak kopyalanır; UnityEngine.Object referansları
    // (sprite, AudioClip, obstacleLibrary) paylaşılır. Bu sayede LevelData'ya eklenen YENİ alanlar
    // otomatik kopyalanır — eski elle alan-alan kopyalamada her yeni alan sessizce düşüyordu
    // (stackedObstacles bu tuzağa düşmüştü). Sayısal clamp'leri LevelData.OnValidate zaten uyguluyor.
    private LevelData CloneLevelDataForRuntime(LevelData source)
    {
        if (source == null)
            return null;

        var clone = Instantiate(source);
        clone.name = $"{source.name}_Runtime";
        return clone;
    }

    // Yukarıdan dikey düşme veya diyagonal kayma ile ulaşılabilen hücreleri hesaplar.
    // Algoritma CascadeLogic.TrySlide ile aynı köşe-geçirgenlik kurallarını kullanır:
    // diyagonal için iki köşeden en az biri geçilebilir olmalı (!hole && !blocked).
    // Tek top-to-bottom geçiş yeterlidir; her hücre yalnızca bir önceki satırdan beslenir.
    private bool[,] ComputeReachable(bool[,] holes)
    {
        var reachable = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (holes[x, y]) continue;

                if (y == 0) { reachable[x, y] = true; continue; }

                // Dikey düşme
                if (reachable[x, y - 1]) { reachable[x, y] = true; continue; }

                // Sol diyagonal: (x-1, y-1) → (x, y)
                // köşeA=(fromX=x-1, toY=y), köşeB=(toX=x, fromY=y-1)
                if (x > 0 && reachable[x - 1, y - 1])
                {
                    if (!holes[x - 1, y] || !holes[x, y - 1])
                    { reachable[x, y] = true; continue; }
                }

                // Sağ diyagonal: (x+1, y-1) → (x, y)
                // köşeA=(fromX=x+1, toY=y), köşeB=(toX=x, fromY=y-1)
                if (x < width - 1 && reachable[x + 1, y - 1])
                {
                    if (!holes[x + 1, y] || !holes[x, y - 1])
                        reachable[x, y] = true;
                }
            }
        }

        return reachable;
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
                // Safe kendi SafeObstacleView renderer'ını kullanır.
                if (obsId == ObstacleId.Safe) continue;
                // Magnet kendi MagnetView renderer'ını (DrawMagnetObstacles) kullanır; buradaki
                // def yalnızca goal ikonu içindir, board görselini üretmez (çift-çizim olmasın).
                if (obsId == ObstacleId.Magnet) continue;

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

        mudOverlayService.Init(board, width, height, tileSize);
        mudOverlayService.ClearAll();

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

            SpawnMudOverlayCell(x, y);
        }

        // TÜM mud hücreleri kaydolduktan sonra tek yetkili exposure geçişi — artımlı komşu
        // refresh'inin sınır hücrelerinde bıraktığı bayat bevel'i (izole "kutu") giderir.
        mudOverlayService.RefreshAllBorders();
    }

    private void SpawnMudOverlayCell(int x, int y)
    {
        if (mudOverlayService == null || mudOverlayRoot == null || resolvedLevel == null)
            return;

        if (mudOverlayService.TryGetView(x, y, out var existing) && existing != null)
            return;

        mudOverlayService.Init(board, width, height, tileSize);

        var go = new GameObject(
            $"Mud_{x}_{y}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image),
            typeof(MudCellView));
        go.transform.SetParent(mudOverlayRoot, false);

        var view = go.GetComponent<MudCellView>();
        view.Init(
            mudOverlayService.BorderedMudSprite,
            mudOverlayService.PlainMudTexture,
            x, y,
            width, height);
        view.SetStage0InteriorStyle(
            mudOverlayService.UseFlatStage0Interior,
            mudOverlayService.FlatStage0InteriorColor,
            mudOverlayService.Stage0InteriorOffsetPixels);
        view.PlaceInCell(tileSize);

        int mudMaxHits = resolvedLevel.obstacleLibrary?.Get(ObstacleId.Mud)?.hits ?? 1;
        int remaining  = board?.ObstacleStateService?.GetRemainingHitsAt(x, y) ?? mudMaxHits;
        // Overlay altında baştan çizilen mud kapalıyken üstteki obstacle'ın hit'ini okuyabilir;
        // mud reveal'da full sayıldığından mudMaxHits'e clamp'le.
        remaining = Mathf.Min(remaining, mudMaxHits);
        if (remaining <= 0) remaining = mudMaxHits;

        mudOverlayService.RegisterCell(x, y, view, remaining, mudMaxHits);
    }

    // ── Grass overlay ────────────────────────────────────────────────────────────

    /// Grass, Oil gibi gerçek bir CellAnchoredOverlay obstacle'ıdır (obstacles[]'te kalır). Bu metod
    /// yalnızca GÖRSELİ çizer: id==Grass hücrelerine GrassCellView doğurur; hasar/temizlenme
    // Grass image'larının çizileceği root'u hazırlar (service/GrassCellView YOK — yalnız parent
    // transform). DrawObstacleVisuals'tan ÖNCE çağrılır; grass obstacle image'ları buraya konur.
    private void EnsureGrassRootSetup()
    {
        var rootParent = spawnParent != null ? spawnParent : (RectTransform)transform;
        if (grassOverlayRoot == null)
            grassOverlayRoot = GetOrCreateChildRoot(rootParent, "GrassOverlay");

        ClearChildren(grassOverlayRoot);
        if (spawnParent != null && grassOverlayRoot.parent != spawnParent)
            grassOverlayRoot.SetParent(spawnParent, worldPositionStays: false);

        grassOverlayRoot.anchorMin = new Vector2(0f, 1f);
        grassOverlayRoot.anchorMax = new Vector2(0f, 1f);
        grassOverlayRoot.pivot     = new Vector2(0f, 1f);
        grassOverlayRoot.anchoredPosition = GetBoardContentAnchoredPosition();
        grassOverlayRoot.sizeDelta = Vector2.zero;
        grassOverlayRoot.localScale = Vector3.one;
        grassOverlayRoot.gameObject.layer = rootParent.gameObject.layer;   // culling'i önle
    }

    // Grass root'unu BoardMask DIŞINA + border'ın ÜSTÜNE taşır (worldPositionStays: hücre hizası
    // korunur): kenar yaprakları mask'a takılıp kesilmez, magnet gibi üst renderer'lı obstacle'ların
    // ÜSTÜNDE kalır. DrawObstacleVisuals + magnet/safe çizimlerinden SONRA çağrılır.
    private void LiftGrassRootAboveBorder()
    {
        if (grassOverlayRoot == null) return;

        var grassBorderRoot = borderDrawer != null ? borderDrawer.borderRoot : null;
        if (grassBorderRoot == null || grassBorderRoot.parent == null) return;

        grassOverlayRoot.SetParent(grassBorderRoot.parent, worldPositionStays: true);
        grassOverlayRoot.SetAsLastSibling();

        int lyr = grassBorderRoot.gameObject.layer;
        grassOverlayRoot.gameObject.layer = lyr;
        foreach (Transform ch in grassOverlayRoot)
            ch.gameObject.layer = lyr;
    }

    /// ObstacleVisualChanged üzerinden GrassOverlayService'e akar.
    private void DrawGrassOverlays()
    {
        if (resolvedLevel?.obstacles == null || resolvedLevel.obstacleOrigins == null) return;

        // Root her hâlükârda EnsureRoots'ta otomatik kurulur; yine de garanti altına al.
        if (grassOverlayRoot == null)
        {
            var rootParent = spawnParent != null ? spawnParent : (RectTransform)transform;
            grassOverlayRoot = GetOrCreateChildRoot(rootParent, "GrassOverlay");
            grassOverlayRoot.gameObject.layer = rootParent.gameObject.layer; // culling'i önle
            grassOverlayRoot.anchoredPosition = GetBoardContentAnchoredPosition();
            grassOverlayRoot.SetAsLastSibling();
        }

        // Service atanmamışsa: sahnede bul, yoksa root'a otomatik ekle (default ayarlar + library sprite).
        if (grassOverlayService == null)
            grassOverlayService = FindFirstObjectByType<GrassOverlayService>();
        if (grassOverlayService == null)
            grassOverlayService = grassOverlayRoot.gameObject.AddComponent<GrassOverlayService>();

        grassOverlayService.Init(board, width, height, tileSize);

        // Rebuild temizliği + spawnParent altında hizala (hücreler doğru koordinatta doğsun; geçen
        // build'de grass root border'ın yanına/mask dışına taşınmış olabilir → önce geri al).
        grassOverlayService.ClearAll();
        ClearChildren(grassOverlayRoot);
        if (spawnParent != null && grassOverlayRoot.parent != spawnParent)
            grassOverlayRoot.SetParent(spawnParent, worldPositionStays: false);
        grassOverlayRoot.anchorMin = new Vector2(0f, 1f);
        grassOverlayRoot.anchorMax = new Vector2(0f, 1f);
        grassOverlayRoot.pivot     = new Vector2(0f, 1f);
        grassOverlayRoot.anchoredPosition = GetBoardContentAnchoredPosition();
        grassOverlayRoot.sizeDelta = Vector2.zero;
        grassOverlayRoot.localScale = Vector3.one;

        // Temel görsel: ObstacleLibrary Grass def'inin stage sprite'ı. Service A/B doluysa checkerboard override.
        var grassDef = resolvedLevel.obstacleLibrary != null
            ? resolvedLevel.obstacleLibrary.Get(ObstacleId.Grass)
            : null;
        if (grassDef != null) grassOverlayService.SetBaseSprite(grassDef.GetPreviewSprite());

        int grassMaxHits = grassDef != null ? Mathf.Max(1, grassDef.hits) : 1;
        int spawned = 0;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int idx = resolvedLevel.Index(x, y);
            if ((ObstacleId)resolvedLevel.obstacles[idx] != ObstacleId.Grass) continue;
            // Grass 1x1 — origin kendi hücresi.
            if (resolvedLevel.obstacleOrigins[idx] != idx) continue;

            bool isEmpty = resolvedLevel.cells != null
                && idx < resolvedLevel.cells.Length
                && resolvedLevel.cells[idx] == (int)CellType.Empty;
            if (isEmpty) continue;

            SpawnGrassOverlayCell(x, y, grassMaxHits);
            spawned++;
        }

        // Grass'ı BoardMask DIŞINA + border'ın ÜSTÜNE taşı (worldPositionStays: hücre hizası korunur):
        // kenar yaprakları mask'a takılıp kesilmez, border çerçevesinin üstüne taşar.
        var grassBorderRoot = borderDrawer != null ? borderDrawer.borderRoot : null;
        if (grassBorderRoot != null && grassBorderRoot.parent != null)
        {
            grassOverlayRoot.SetParent(grassBorderRoot.parent, worldPositionStays: true);
            grassOverlayRoot.SetAsLastSibling();
            int lyr = grassBorderRoot.gameObject.layer;
            grassOverlayRoot.gameObject.layer = lyr;
            foreach (Transform ch in grassOverlayRoot) ch.gameObject.layer = lyr;
        }

        Debug.Log($"[Grass] {spawned} view doğdu. sprite={(grassOverlayService.GetSpriteForCell(0, 0) != null ? "VAR" : "YOK")}");
    }

    private void SpawnGrassOverlayCell(int x, int y, int maxHits)
    {
        if (grassOverlayService == null || grassOverlayRoot == null) return;
        if (grassOverlayService.TryGetView(x, y, out var existing) && existing != null) return;

        var go = new GameObject(
            $"Grass_{x}_{y}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image),
            typeof(GrassCellView));
        go.layer = grassOverlayRoot.gameObject.layer;   // Screen Space Camera culling'i önle
        go.transform.SetParent(grassOverlayRoot, false);

        int remaining = board?.ObstacleStateService?.GetRemainingHitsAt(x, y) ?? maxHits;
        if (remaining <= 0) remaining = maxHits;

        var view = go.GetComponent<GrassCellView>();
        grassOverlayService.RegisterCell(x, y, view, remaining, maxHits);
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

    private void StampMagnetCellsIntoLevel(LevelData lvl)
    {
        if (lvl?.magnets == null || lvl.magnets.Length == 0) return;
        if (lvl.obstacles == null || lvl.obstacleOrigins == null) return;

        foreach (var entry in lvl.magnets)
        {
            if (entry.pathCellIndices == null || entry.pathCellIndices.Length < 2) continue;
            int origin = entry.pathCellIndices[0];

            foreach (int cellIdx in entry.pathCellIndices)
            {
                if (cellIdx < 0 || cellIdx >= lvl.obstacles.Length) continue;
                lvl.obstacles[cellIdx]       = (int)ObstacleId.Magnet;
                lvl.obstacleOrigins[cellIdx] = origin;
            }
        }
    }

    // Safe (kasa): NxN bölgeyi kaplar. Model (a): altındaki MEVCUT içeriği per-cell kaydeder
    // (beneath store), sonra hücreleri Safe ile stamp eder. Kasa kırılınca içerik geri yüklenir.
    private void StampSafeCellsIntoLevel(LevelData lvl)
    {
        if (lvl?.safes == null || lvl.safes.Length == 0) return;
        if (lvl.obstacles == null || lvl.obstacleOrigins == null) return;

        safeObstacleService = FindFirstObjectByType<SafeObstacleService>();
        var safeService = safeObstacleService;
        safeService?.Clear();

        int W = lvl.width, H = lvl.height;
        foreach (var entry in lvl.safes)
        {
            int origin = entry.originCellIndex;
            if (origin < 0 || origin >= lvl.obstacles.Length) continue;

            int ox = origin % W, oy = origin / W;
            int w = Mathf.Max(1, entry.width), h = Mathf.Max(1, entry.height);

            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int cx = ox + c, cy = oy + r;
                    if (cx >= W || cy >= H) continue;
                    int cell = cy * W + cx;
                    if (cell < 0 || cell >= lvl.obstacles.Length) continue;

                    // 1) Altındaki mevcut içeriği generic beneath store için işaretle (kayıt SetLevelData sonrası).
                    pendingStampedBeneath.Add((cell, (ObstacleId)lvl.obstacles[cell], lvl.obstacleOrigins[cell], origin));
                    // 2) Safe ile stamp et.
                    lvl.obstacles[cell]       = (int)ObstacleId.Safe;
                    lvl.obstacleOrigins[cell] = origin;
                }

            safeService?.RegisterSafe(
                origin,
                entry.redHits,
                entry.yellowHits,
                entry.greenHits,
                entry.lockHitMode,
                entry.firstLock,
                entry.secondLock,
                entry.thirdLock);
        }
    }

    // Generic stacking: stackedObstacles[] entry'lerini obstacles[]'a stamp eder; altındaki authored
    // içeriği (Mud, Stone...) beneath store için işaretler. Safe ile aynı 'beneath' akışı, her obstacle
    // için. SetLevelData ÖNCESİ çağrılır; beneath kaydı RegisterPendingStampedBeneath ile SONRA yapılır.
    private void StampStackedObstaclesIntoLevel(LevelData lvl)
    {
        if (lvl?.stackedObstacles == null || lvl.stackedObstacles.Length == 0) return;
        if (lvl.obstacles == null || lvl.obstacleOrigins == null) return;

        int W = lvl.width, H = lvl.height;
        var lib = lvl.obstacleLibrary;

        foreach (var entry in lvl.stackedObstacles)
        {
            var overId = entry.obstacleId;
            if (overId == ObstacleId.None) continue;

            int origin = entry.originCellIndex;
            if (origin < 0 || origin >= lvl.obstacles.Length) continue;

            var def = lib != null ? lib.Get(overId) : null;
            int w = def != null ? Mathf.Max(1, def.size.x) : 1;
            int h = def != null ? Mathf.Max(1, def.size.y) : 1;
            int ox = origin % W, oy = origin / W;

            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int cx = ox + c, cy = oy + r;
                    if (cx >= W || cy >= H) continue;
                    int cell = cy * W + cx;
                    if (cell < 0 || cell >= lvl.obstacles.Length) continue;

                    // 1) Altındaki authored içeriği beneath store için işaretle.
                    pendingStampedBeneath.Add((cell, (ObstacleId)lvl.obstacles[cell], lvl.obstacleOrigins[cell], origin));
                    // 2) Üstteki obstacle ile stamp et.
                    lvl.obstacles[cell]       = (int)overId;
                    lvl.obstacleOrigins[cell] = origin;
                }
        }
    }

    // Stamp aşamasında toplanan beneath kayıtlarını ObstacleStateService'e push eder.
    // SetLevelData (ObstacleStateService init + store clear) SONRASINDA çağrılmalıdır.
    private void RegisterPendingStampedBeneath()
    {
        if (pendingStampedBeneath.Count == 0) return;

        var state = board != null ? board.ObstacleStateService : null;
        if (state != null)
            foreach (var p in pendingStampedBeneath)
                state.RegisterStampedBeneath(p.cell, p.beneathId, p.beneathOrigin, p.overOrigin);

        // Beneath view'larını baştan çizebilmek için kalıcı kopya (pendingStampedBeneath temizlenir).
        stampedBeneathVisuals.Clear();
        stampedBeneathVisuals.AddRange(pendingStampedBeneath);

        pendingStampedBeneath.Clear();
    }

    // Overlay altındaki obstacle'ı overlay'in ARKASINDA baştan çizer. Yalnızca origin hücresinde,
    // yalnızca "çizilebilir over-tile" beneath'ler için (movable/Mud/Oil/Safe/None hariç — bunların
    // ayrı renderer'ları var). Overlay kırılınca HandleObstacleCreatedDynamic bu view'ı promote eder.
    private void DrawStampedBeneathVisuals()
    {
        if (stampedBeneathVisuals.Count == 0 || resolvedLevel?.obstacleLibrary == null) return;

        var topVisualIndexByCell = new Dictionary<int, int>();
        for (int i = 0; i < stampedBeneathVisuals.Count; i++)
            topVisualIndexByCell[stampedBeneathVisuals[i].cell] = i;

        for (int i = 0; i < stampedBeneathVisuals.Count; i++)
        {
            var p = stampedBeneathVisuals[i];
            if (!topVisualIndexByCell.TryGetValue(p.cell, out int topIndex) || topIndex != i)
                continue;

            if (p.beneathId == ObstacleId.None) continue;
            if (p.cell != p.beneathOrigin) continue;              // beneath'i yalnızca origin'inde çiz
            if (beneathViewsByCell.ContainsKey(p.cell)) continue;

            // Safe VE OverrideBatteryBox overlay'i: altındaki beneath'i BAŞTAN gösterme (kenardan
            // sızmasın). Overlay kırılınca beneath normal reveal yoluyla (ReinitRestoredBeneathOrigin
            // → RequestObstacleViewCreate) taze çizilir. Diğer stacked overlay'ler (Chest/Wardrobe...)
            // beneath'i eskisi gibi baştan gösterir.
            var overlayHere = (ObstacleId)resolvedLevel.obstacles[p.cell];
            if (overlayHere == ObstacleId.Safe || overlayHere == ObstacleId.OverrideBatteryBox) continue;

            int bx = p.cell % resolvedLevel.width;
            int by = p.cell / resolvedLevel.width;

            // Mud: ayrı seamless renderer (mudOverlayRoot, taşların/obstacle'ların ALTINDA çizilir →
            // z-order zaten arkada). Hücreyi geçici Mud'a çevirip mud view'ı spawn et. Kapalıyken
            // full sayılır (StampedBeneath remaining tutmaz) — SpawnMudOverlayCell mudMaxHits'e clamp'ler.
            if (p.beneathId == ObstacleId.Mud)
            {
                int sObs = resolvedLevel.obstacles[p.cell];
                int sOrg = resolvedLevel.obstacleOrigins[p.cell];
                resolvedLevel.obstacles[p.cell] = (int)ObstacleId.Mud;
                resolvedLevel.obstacleOrigins[p.cell] = p.cell;
                SpawnMudOverlayCell(bx, by);
                resolvedLevel.obstacles[p.cell] = sObs;
                resolvedLevel.obstacleOrigins[p.cell] = sOrg;
                continue;   // mudOverlayService yönetir; beneathViewsByCell'e girmez
            }

            // Diğer ayrı renderer'lı / özel tipler v1'de kapsam dışı. Grass da pre-draw EDİLMEZ:
            // grassOverlayRoot üstte çizdiğinden beneath grass cover'ın üstüne sızardı. Cover
            // kırılınca grass reveal dinamik yolla (HandleObstacleCreatedDynamic) taze çizilir.
            if (p.beneathId == ObstacleId.Oil ||
                p.beneathId == ObstacleId.Safe || p.beneathId == ObstacleId.Tube ||
                p.beneathId == ObstacleId.Magnet || p.beneathId == ObstacleId.Grass)
                continue;

            var def = resolvedLevel.obstacleLibrary.Get(p.beneathId);
            if (def == null || def.IsMovableObstacle) continue;

            // DrawObstacleImage level state'ini (obstacles/origins) beneath'e göre okusun diye
            // hücreyi geçici olarak beneath'e çevir, çiz, sonra overlay'e geri al.
            int savedObs = resolvedLevel.obstacles[p.cell];
            int savedOrg = resolvedLevel.obstacleOrigins[p.cell];
            resolvedLevel.obstacles[p.cell] = (int)p.beneathId;
            resolvedLevel.obstacleOrigins[p.cell] = p.beneathOrigin;

            Image beneathImage = DrawObstacleImage(def, bx, by);

            resolvedLevel.obstacles[p.cell] = savedObs;
            resolvedLevel.obstacleOrigins[p.cell] = savedOrg;

            if (beneathImage != null)
            {
                // Overlay'in arkasına gönder (ayni root'ta ilk sibling → en altta çizilir).
                beneathImage.rectTransform.SetAsFirstSibling();
                beneathImage.raycastTarget = false;   // tıklama overlay'e gitsin
                beneathViewsByCell[p.cell] = beneathImage;
            }
        }
    }

    // Her SafeEntry için bir SafeObstacleView spawn eder: NxN bölgeye konumlandır + boyutlandır,
    // service event'lerine bağla. Body NxN'e göre ölçeklenir, LockPanel prefab'da ortalı/sabit.
    private void DrawSafeObstacles()
    {
        if (safeViewPrefab == null) return;
        if (resolvedLevel?.safes == null || resolvedLevel.safes.Length == 0) return;
        if (safeObstacleService == null) safeObstacleService = FindFirstObjectByType<SafeObstacleService>();
        if (safeObstacleService == null) return;

        var root = safeRoot != null ? safeRoot : obstaclesRoot;
        if (root == null) return;

        int W = resolvedLevel.width, H = resolvedLevel.height;
        foreach (var entry in resolvedLevel.safes)
        {
            int origin = entry.originCellIndex;
            if (origin < 0 || origin >= W * H) continue;

            int ox = origin % W, oy = origin / W;
            int w = Mathf.Max(1, entry.width), h = Mathf.Max(1, entry.height);

            var view = Instantiate(safeViewPrefab, root);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // top-left, tile'larla aynı
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(ox * tileSize, -oy * tileSize);
            rt.sizeDelta = new Vector2(w * tileSize, h * tileSize);

            view.SetBodySize(w * tileSize, h * tileSize);
            view.Setup(safeObstacleService, origin);

            AddSafeCellClickProxies(entry, root);
        }
    }

    private void AddSafeCellClickProxies(SafeEntry entry, RectTransform root)
    {
        if (root == null || resolvedLevel == null) return;

        int W = resolvedLevel.width;
        int H = resolvedLevel.height;
        int origin = entry.originCellIndex;
        if (origin < 0 || origin >= W * H) return;

        int ox = origin % W;
        int oy = origin / W;
        int w = Mathf.Max(1, entry.width);
        int h = Mathf.Max(1, entry.height);

        for (int r = 0; r < h; r++)
        for (int c = 0; c < w; c++)
        {
            int cx = ox + c;
            int cy = oy + r;
            if (cx < 0 || cx >= W || cy < 0 || cy >= H) continue;

            int cell = cy * W + cx;
            if (safeClickProxyByCell.ContainsKey(cell)) continue;

            var clickGo = new GameObject(
                $"SafeClick_{cx}_{cy}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ObstacleClickProxy));
            clickGo.transform.SetParent(root, false);

            var rt = clickGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(cx * tileSize, -cy * tileSize);
            rt.sizeDelta = new Vector2(tileSize, tileSize);

            var img = clickGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            var proxy = clickGo.GetComponent<ObstacleClickProxy>();
            proxy.Init(board, cx, cy);

            clickGo.transform.SetAsLastSibling();
            safeClickProxyByCell[cell] = clickGo;
        }
    }

    private void RemoveSafeCellClickProxiesForOrigin(int origin)
    {
        if (resolvedLevel?.safes == null || resolvedLevel.safes.Length == 0)
            return;

        int W = resolvedLevel.width;
        int H = resolvedLevel.height;

        foreach (var entry in resolvedLevel.safes)
        {
            if (entry.originCellIndex != origin)
                continue;

            int ox = origin % W;
            int oy = origin / W;
            int w = Mathf.Max(1, entry.width);
            int h = Mathf.Max(1, entry.height);

            for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                int cx = ox + c;
                int cy = oy + r;
                if (cx < 0 || cx >= W || cy < 0 || cy >= H) continue;

                int cell = cy * W + cx;
                if (!safeClickProxyByCell.TryGetValue(cell, out var clickGo))
                    continue;

                if (clickGo != null)
                    Destroy(clickGo);
                safeClickProxyByCell.Remove(cell);
            }

            return;
        }
    }

    private void DrawMagnetObstacles()
    {
        if (magnetObstacleService == null || magnetViewPrefab == null) return;
        if (resolvedLevel?.magnets == null || resolvedLevel.magnets.Length == 0) return;
        if (magnetRoot == null) return;

        magnetObstacleService.Init(board.ObstacleStateService);
        board.ObstacleStateService.MagnetHitInterceptor = magnetObstacleService.HandleMagnetHit;
        board.ObstacleStateService.MagnetEndpointQuery = magnetObstacleService.IsMagnetEndpoint;

        foreach (var entry in resolvedLevel.magnets)
        {
            if (entry.pathCellIndices == null || entry.pathCellIndices.Length < 2) continue;

            var go   = Instantiate(magnetViewPrefab.gameObject);
            var view = go.GetComponent<MagnetView>();
            if (view == null) { Destroy(go); continue; }

            view.Init(entry.pathCellIndices, resolvedLevel.width, tileSize, magnetRoot);

            int origin = entry.pathCellIndices[0];
            magnetObstacleService.RegisterMagnet(origin, entry.pathCellIndices, view);
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

            AddTubeCellClickProxies(cellIndices);
        }
    }

    // TubeView görselleri raycastTarget=false. Tube hücresi blocked → tile yok.
    // Hammer (Single booster) gibi tek hücre hedefli joker'lerin Tube'u vurabilmesi için
    // her tube hücresine görünmez bir click proxy serilir.
    private void AddTubeCellClickProxies(int[] cellIndices)
    {
        if (cellIndices == null || tubeRoot == null) return;

        foreach (int idx in cellIndices)
        {
            if (tubeClickProxyByCell.ContainsKey(idx)) continue;

            int cx = idx % width;
            int cy = idx / width;

            var clickGo = new GameObject(
                $"TubeClick_{cx}_{cy}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ObstacleClickProxy));
            clickGo.transform.SetParent(tubeRoot, false);

            var rt = clickGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(cx * tileSize, -cy * tileSize);
            rt.sizeDelta = new Vector2(tileSize, tileSize);

            var img = clickGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            var proxy = clickGo.GetComponent<ObstacleClickProxy>();
            proxy.Init(board, cx, cy);

            clickGo.transform.SetAsFirstSibling();
            tubeClickProxyByCell[idx] = clickGo;
        }
    }

    // O(1) obstacle-view lookup by origin (wired to board.ObstacleViewByOriginLookup).
    // Lets services avoid full-hierarchy GetComponentsInChildren<Image> scans.
    private Image GetObstacleViewImage(int originIndex)
    {
        return obstacleViewsByOrigin.TryGetValue(originIndex, out var image) ? image : null;
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
        if (obstacleId == ObstacleId.Safe)
            RemoveSafeCellClickProxiesForOrigin(originIndex);

        if (obstacleId == ObstacleId.EnergyContainer)
        {
            // Don't destroy the image — EnergyContainerFx will apply the exhausted
            // visual on the same GameObject. Just stop tracking it here.
            if (IsTrackedObstacleViewFor(originIndex, obstacleId))
            {
                obstacleViewsByOrigin.Remove(originIndex);
                obstacleDefsByOrigin.Remove(originIndex);
            }
            return;
        }

        if (obstacleId == ObstacleId.OverrideBatteryBox)
        {
            // Detonasyon VIEW'ını HEMEN oynatma — seri kuyruk (BoardController) sırası gelince
            // OnRequestObbDetonationView ile tetikler. View'ı beklemede tut (çoklu kutu aynı anda
            // patlamasın). Kuyruk tek kutuda da geçerli; tek frame gecikmeyle tetiklenir.
            if (_overrideBatteryBoxViews.TryGetValue(originIndex, out var view) && view != null)
                _pendingObbDetonationViews[originIndex] = view;

            obstacleViewsByOrigin.Remove(originIndex);
            obstacleDefsByOrigin.Remove(originIndex);
            _overrideBatteryBoxViews.Remove(originIndex);
            ApplyUnderTileCellBgTint();
            return;
        }

        if (IsTrackedObstacleViewFor(originIndex, obstacleId))
        {
            if (obstacleViewsByOrigin.TryGetValue(originIndex, out var image) && image != null)
                Destroy(image.gameObject);

            obstacleViewsByOrigin.Remove(originIndex);
            obstacleDefsByOrigin.Remove(originIndex);
        }
        _chestViews.Remove(originIndex);
        _batteryBoxViews.Remove(originIndex);
        _overrideBatteryBoxViews.Remove(originIndex);
        ApplyUnderTileCellBgTint();
    }

    private void HandleCellUnlocked(int cellIndex)
    {
        int x = cellIndex % width;
        int y = cellIndex / width;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        if (!cellBgByIndex.ContainsKey(cellIndex) || cellBgByIndex[cellIndex] == null)
            SpawnCellBg(x, y);

        if (tubeClickProxyByCell.TryGetValue(cellIndex, out var clickGo))
        {
            if (clickGo != null) Destroy(clickGo);
            tubeClickProxyByCell.Remove(cellIndex);
        }

        if (safeClickProxyByCell.TryGetValue(cellIndex, out var safeClickGo))
        {
            if (safeClickGo != null) Destroy(safeClickGo);
            safeClickProxyByCell.Remove(cellIndex);
        }
    }

    private void EnsureRoots()
    {
        var root = spawnParent != null ? spawnParent : (RectTransform)transform;

        if (cellBgRoot == null)
            cellBgRoot = GetOrCreateChildRoot(root, "CellBGs");

        if (mudOverlayRoot == null)
            mudOverlayRoot = GetOrCreateChildRoot(root, "MudOverlay");

        if (tubeRoot == null || tubeRoot == root)
            tubeRoot = GetOrCreateChildRoot(root, "TubeObstacles");

        if (magnetRoot == null || magnetRoot == root)
            magnetRoot = GetOrCreateChildRoot(root, "MagnetObstacles");

        if (gridLinesRoot == null)
            gridLinesRoot = GetOrCreateChildRoot(root, "GridLines");

        if (obstaclesRoot == null)
            obstaclesRoot = GetOrCreateChildRoot(root, "Obstacles");

        if (underTilesObstaclesRoot == null)
            underTilesObstaclesRoot = GetOrCreateChildRoot(obstaclesRoot, "UnderTiles");

        if (overTilesObstaclesRoot == null)
            overTilesObstaclesRoot = GetOrCreateChildRoot(obstaclesRoot, "OverTiles");

        // Under-tile visuals (including MudUnder) must sit below the seamless MudOverlay.
        // Keep over-tile obstacles under Obstacles so they remain above the board content.
        if (underTilesObstaclesRoot != null && underTilesObstaclesRoot.parent != root)
            underTilesObstaclesRoot.SetParent(root, false);

        if (tilesRoot == null)
            tilesRoot = GetOrCreateChildRoot(root, "Tiles");

        // Grass overlay: mud gibi board'a hizalı bir child root; sibling sırası taşların ÜSTÜNE alınır
        // (aşağıda). Inspector'da atanmadıysa otomatik kurulur — elle kurulum gerekmez.
        if (grassOverlayRoot == null)
        {
            grassOverlayRoot = GetOrCreateChildRoot(root, "GrassOverlay");
            grassOverlayRoot.gameObject.layer = root.gameObject.layer; // Screen Space Camera culling'i önle
        }
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

        // Diamond junction'lar — çizgilerin kesiştiği node noktalara elmas koyar.
        if (gridJunctionEnabled)
        {
            float jSize = gridJunctionSize > 0f ? gridJunctionSize : thickness * 2.5f;

            for (int ny = 0; ny <= height; ny++)
            {
                for (int nx = 0; nx <= width; nx++)
                {
                    bool hasAbove = IsVisibleCell(nx - 1, ny - 1) || IsVisibleCell(nx, ny - 1);
                    bool hasBelow = IsVisibleCell(nx - 1, ny)     || IsVisibleCell(nx, ny);
                    bool hasLeft  = IsVisibleCell(nx - 1, ny - 1) || IsVisibleCell(nx - 1, ny);
                    bool hasRight = IsVisibleCell(nx, ny - 1)     || IsVisibleCell(nx, ny);

                    // Sadece hem H hem V çizgisi kesişen noktalara koy.
                    bool hCross = (hasAbove || hasBelow);
                    bool vCross = (hasLeft  || hasRight);
                    if (!hCross || !vCross) continue;

                    // Wrapper: konumlandırma + aspect ölçekleme
                    var wrapper = new GameObject($"GridJunction_{nx}_{ny}", typeof(RectTransform));
                    wrapper.transform.SetParent(gridLinesRoot, false);
                    var wRt = wrapper.GetComponent<RectTransform>();
                    wRt.anchorMin = new Vector2(0f, 1f);
                    wRt.anchorMax = new Vector2(0f, 1f);
                    wRt.pivot     = new Vector2(0.5f, 0.5f);
                    wRt.anchoredPosition = new Vector2(nx * tileSize, -ny * tileSize);
                    wRt.sizeDelta        = Vector2.zero;
                    wRt.localScale       = new Vector3(gridJunctionAspectX, gridJunctionAspectY, 1f);
                    wRt.localRotation    = Quaternion.identity;

                    // Child: kare elmas (45° rotated square) → wrapper scale ile <> şekli
                    var jGo = new GameObject("Diamond", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    jGo.transform.SetParent(wrapper.transform, false);
                    var jRt = jGo.GetComponent<RectTransform>();
                    jRt.anchorMin = new Vector2(0.5f, 0.5f);
                    jRt.anchorMax = new Vector2(0.5f, 0.5f);
                    jRt.pivot     = new Vector2(0.5f, 0.5f);
                    jRt.anchoredPosition = Vector2.zero;
                    jRt.sizeDelta        = new Vector2(jSize, jSize);
                    jRt.localRotation    = Quaternion.Euler(0f, 0f, 45f);
                    jRt.localScale       = Vector3.one;

                    var jImg = jGo.GetComponent<Image>();
                    jImg.color         = runtimeGridLineColor;
                    jImg.raycastTarget = false;
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
        board.ApplyNormalVisualFillRatio(view);   // ilk board taşları da fill-ratio alsın (cascade spawn zaten alıyordu)

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
            view.OpenCabinet();   // kapaklar düşer + iç görünür (eski ShowAll yerine)
    }

    private void HandleChestColorRemoved(int originIndex, ChestColorMask removedColor)
    {
        if (_chestViews.TryGetValue(originIndex, out var view) && view != null)
        {
            // İlk hit kapakları düşürür (HandleChestOpened); sonraki hit'lerde her renk objesi
            // KENDİ sprite'ıyla STANDART break particle'a parçalanır. Renk ObstacleLibrary'den
            // gelmiyor (runtime match rengine bağlı), o yüzden sprite'ı view'den alıp besliyoruz.
            var brokenSprite = view.GetColorSprite(removedColor);
            Vector3 worldPos = view.GetColorWorldCenter(removedColor);

            view.HideColor(removedColor);
            view.Shake();

            if (brokenSprite != null && board != null && board.BreakFx != null && board.ObstacleBreakFxPrefab != null)
                board.BreakFx.PlaySplashFx(
                    board.ObstacleBreakFxPrefab,
                    board.ObstacleBreakFxLifetime,
                    worldPos,
                    new[] { brokenSprite });
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

        // Her kenarda gridOverlap (yarım grid-çizgisi = 4px çizgide 2px) taşma. Chest sprite art'ı
        // hücre sınırının biraz ötesine geçip aradaki grid çizgisini/dikişi örter. Bitişik chest'ler
        // ikisi de aynı miktar taştığından blok kesintisiz görünür; aynı opak art olduğu için
        // örtüşme GÖRÜNMEZ. (Eskiden bitişik obstacle'a doğru taşma 0'dı → aradaki çizgi/dikiş
        // sprite kenar-payından sızıyordu. Kullanıcı isteği: chest başına +gridOverlap her kenar.)
        float lo = gridOverlap, ro = gridOverlap, to = gridOverlap, bo = gridOverlap;

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
            clickProxy.Init(board, x, y, w, h, tileSize);
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
            clickProxy.Init(board, x, y, w, h, tileSize);
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

    private Image SpawnOverrideBatteryBoxView(ObstacleDef def, int x, int y)
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

        OverrideBatteryBoxView view;
        Image rootImage;

        if (overrideBatteryBoxViewPrefab != null)
        {
            view = Instantiate(overrideBatteryBoxViewPrefab, parent);
            rootImage = view.GetComponent<Image>();
            if (rootImage == null) rootImage = view.gameObject.AddComponent<Image>();
        }
        else
        {
            var fallback = new GameObject($"Obs_OverrideBatteryBox_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fallback.transform.SetParent(parent, false);
            rootImage = fallback.GetComponent<Image>();
            view = fallback.AddComponent<OverrideBatteryBoxView>();
            CreateOverrideBatteryChildImages(view, fallback.transform, Mathf.Max(1, def.hits));
        }

        Sprite boxSprite = def.GetPreviewSprite();
        if (rootImage != null)
        {
            rootImage.sprite = boxSprite;
            rootImage.type = Image.Type.Simple;
            rootImage.preserveAspect = false;
            rootImage.color = boxSprite != null ? Color.white : new Color(0.02f, 0.55f, 0.58f, 0.95f);
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
            clickProxy.Init(board, x, y, w, h, tileSize);
            rootImage.raycastTarget = true;
        }

        if (view != null)
        {
            int hitsPerColor = Mathf.Max(1, def.hits);
            view.ConfigureLayout(obbHorizontalBarWidthScale, obbHorizontalBarHeightScale,
                obbRotatedBarWidthScale, obbRotatedBarHeightScale,
                obbNeedleSize, obbGaugeSize, obbGaugeCenter, obbNeedlePivot,
                obbNeedleStartAngle, obbNeedleEndAngle);
            view.ApplyLayout();
            view.Initialize(hitsPerColor);
            _overrideBatteryBoxViews[originIndex] = view;
        }

        return rootImage;
    }

    private void CreateOverrideBatteryChildImages(OverrideBatteryBoxView view, Transform parent, int hitsPerColor)
    {
        int count = Mathf.Max(1, hitsPerColor);
        AddBars(view, parent, ChestColorMask.Core,  "Red",    count, overrideBarRedSprite,    new Color(1.0f, 0.10f, 0.08f, 1f));
        AddBars(view, parent, ChestColorMask.Gear,  "Yellow", count, overrideBarYellowSprite, new Color(1.0f, 0.78f, 0.05f, 1f));
        AddBars(view, parent, ChestColorMask.Bolt,  "Blue",   count, overrideBarBlueSprite,   new Color(0.02f, 0.47f, 1.0f, 1f));
        AddBars(view, parent, ChestColorMask.Plate, "Green",  count, overrideBarGreenSprite,  new Color(0.33f, 0.86f, 0.02f, 1f));

        // Radial fill isteğe bağlı: varsayılan alfa=0 (kapalı); baked arc + ibre yeterli.
        var progress = CreateUiImage("CenterProgress", parent, new Color(1f, 0.08f, 0.04f, 0.0f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Radial360;
        view.SetProgressImage(progress);

        // Merkez gauge/dial: barların ÜSTÜNDE render edilir ki barlar onu ezmesin (ibrenin altında).
        if (overrideGaugeSprite != null)
        {
            var gauge = CreateUiImage("CenterGauge", parent, Color.white);
            gauge.sprite = overrideGaugeSprite;
            gauge.preserveAspect = true;
            view.SetCenterImage(gauge);
        }

        var pin = CreateUiImage("CenterPin", parent, overrideNeedleSprite != null ? Color.white : new Color(0.16f, 0.17f, 0.19f, 1f));
        if (overrideNeedleSprite != null)
        {
            pin.sprite = overrideNeedleSprite;
            pin.preserveAspect = true;
        }
        view.SetPinImage(pin);

        view.SetSteamSprite(overrideSteamSprite);
    }

    private static void AddBars(OverrideBatteryBoxView view, Transform parent, ChestColorMask color, string label, int count, Sprite sprite, Color tint)
    {
        for (int i = 0; i < count; i++)
        {
            var img = CreateUiImage($"{label}_Bar_{i}", parent, sprite != null ? Color.white : tint);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            view.SetBarImage(color, i, img);
        }
    }

    private static Image CreateUiImage(string name, Transform parent, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = tint;
        return img;
    }

    private void HandleOverrideBatteryBoxHit(int originIndex, ChestColorMask color, int remaining, int progress, int total)
    {
        if (!_overrideBatteryBoxViews.TryGetValue(originIndex, out var view) || view == null) return;
        if (!obstacleDefsByOrigin.TryGetValue(originIndex, out var def) || def == null) return;
        int maxHits = Mathf.Max(1, def.hits);
        view.ApplyColorState(color, remaining, maxHits);
        view.ApplyProgress(progress, Mathf.Max(1, total));
        view.Shake();
    }

    // Seri kuyruk: sırası gelen OBB'nin bekleyen detonasyon VIEW'ını şimdi oynat.
    private void HandleRequestObbDetonationView(int originIndex)
    {
        if (_pendingObbDetonationViews.TryGetValue(originIndex, out var view))
        {
            _pendingObbDetonationViews.Remove(originIndex);
            if (view != null)
                view.PlayDetonationAndDestroy();
        }
    }

    // ─── Wardrobe ───────────────────────────────────────────────────────────

    private Image SpawnRocketBasketView(ObstacleDef def, int x, int y)
    {
        bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
        var parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;
        int originIndex = resolvedLevel.Index(x, y);

        var go = new GameObject($"Obs_RocketBasket_{x}_{y}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RocketBasketView));
        go.transform.SetParent(parent, false);

        var rootImage = go.GetComponent<Image>();
        rootImage.sprite = def.GetPreviewSprite();
        rootImage.type = Image.Type.Simple;
        rootImage.preserveAspect = false;

        var rt = rootImage.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);

        var clickProxy = rootImage.gameObject.AddComponent<ObstacleClickProxy>();
        clickProxy.Init(board, x, y, 1, 1, tileSize);

        if (rocketBasketService == null)
            rocketBasketService = FindFirstObjectByType<RocketBasketService>();

        var view = go.GetComponent<RocketBasketView>();
        view.Init(rocketBasketService, rootImage, tileSize);
        rocketBasketService?.RegisterView(originIndex, view);

        return rootImage;
    }

    private Image SpawnWardrobeView(ObstacleDef def, int x, int y)
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

        WardrobeObstacleView view;
        Image rootImage;

        if (wardrobeObstacleViewPrefab != null)
        {
            view = Instantiate(wardrobeObstacleViewPrefab, parent);
            rootImage = view.GetComponent<Image>();
            if (rootImage == null) rootImage = view.gameObject.AddComponent<Image>();
        }
        else
        {
            var fallback = new GameObject($"Obs_Wardrobe_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fallback.transform.SetParent(parent, false);
            rootImage = fallback.GetComponent<Image>();
            view = fallback.AddComponent<WardrobeObstacleView>();
        }

        Sprite closedSprite = def.GetPreviewSprite();
        if (rootImage != null)
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
            clickProxy.Init(board, x, y, w, h, tileSize);
        }

        view.SetClosedSprite(closedSprite);
        _wardrobeViews[originIndex] = view;
        return rootImage;
    }

    private void HandleWardrobeOpened(int originIndex)
    {
        if (!_wardrobeViews.TryGetValue(originIndex, out var view) || view == null) return;
        if (!obstacleDefsByOrigin.TryGetValue(originIndex, out var def) || def == null) return;

        Sprite openBg = def.stages != null && def.stages.Count > 1 ? def.stages[1].sprite : null;
        int shelfCount = Mathf.Max(1, def.size.y);
        view.OpenDoor(openBg, def.wardrobeItemSprites, shelfCount);
        view.Shake();
    }

    private void HandleWardrobeItemRemoved(int originIndex, int itemsRemaining)
    {
        if (!_wardrobeViews.TryGetValue(originIndex, out var view) || view == null) return;
        view.RemoveFrontItem();
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

        // Grass artık chest1 gibi STANDART bir OverTileBlocker obstacle'dır: aşağıdaki generic
        // per-origin Image yolundan çizilir ve HandleObstacleVisualChanged ile veriye 1:1 bağlı
        // güvenilir temizlenir. (Eski ayrı GrassOverlayService registry'si veriyle desync olup
        // "hit aldı ama ekranda kaldı" hayaletini üretiyordu — o yol tamamen bırakıldı.)

        if (def.id == ObstacleId.ColorChest)
            return SpawnChestObstacleView(def, x, y);

        if (def.id == ObstacleId.BatteryBox)
            return SpawnBatteryBoxView(def, x, y);

        if (def.id == ObstacleId.OverrideBatteryBox)
            return SpawnOverrideBatteryBoxView(def, x, y);

        if (def.id == ObstacleId.Wardrobe)
            return SpawnWardrobeView(def, x, y);

        if (def.id == ObstacleId.RocketBasket)
            return SpawnRocketBasketView(def, x, y);

        // Grass: doğal görünüm için (x+y) parity ile A/B checkerboard sprite seçimi korunur.
        // Sprite kaynağı GrassOverlayService (yalnız GetSpriteForCell — A/B mantığı); çizim +
        // temizleme yine bu standart per-origin Image yolunda (güvenilir), overlay registry YOK.
        Sprite sprite;
        if (def.id == ObstacleId.Grass)
        {
            if (grassOverlayService == null)
                grassOverlayService = FindFirstObjectByType<GrassOverlayService>();
            sprite = grassOverlayService != null ? grassOverlayService.GetSpriteForCell(x, y) : null;
            if (sprite == null) sprite = def.GetPreviewSprite();
        }
        else
        {
            sprite = def.GetPreviewSprite();
        }
        if (sprite == null) return null;

        int w = Mathf.Max(1, def.size.x);
        int h = Mathf.Max(1, def.size.y);

        var go = new GameObject($"Obs_{def.id}_{x}_{y}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        // Grass: eskiden olduğu gibi grassOverlayRoot'a çiz (BoardMask DIŞI + border ÜSTÜ, magnetin
        // ÜSTÜNDE) — overhang yaprakları mask'a takılıp kesilmez ve magnet gibi üst renderer'lı
        // obstacle'ların altından sızmaz. Çizim/temizleme yine standart per-origin Image yolunda.
        RectTransform parent;
        if (def.id == ObstacleId.Grass && grassOverlayRoot != null)
        {
            parent = grassOverlayRoot;
        }
        else
        {
            bool drawUnder = ResolveBehaviorForOrigin(resolvedLevel.Index(x, y), def) == ObstacleBehaviorType.UnderTileLayered;
            parent = drawUnder ? underTilesObstaclesRoot : overTilesObstaclesRoot;
        }
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling'i önle

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

        // Grass: komşu grass ile dikişsiz binme + grid/board çizgilerini biraz örtme. Generic edge
        // logic komşu grass'ı "farklı obstacle" (per-cell self-origin) sayıp taşmayı 0'lıyor → aralarda
        // boşluk. Grass için HER yönde eşit overhang uygula (A/B farklı boy; B daha büyük A'nın boşluğunu örter).
        if (def.id == ObstacleId.Grass)
        {
            float expand = grassOverlayService != null ? grassOverlayService.GetOverhangExpandPixels(x, y) : 15f;
            float half = expand * 0.5f;
            rt.anchoredPosition = new Vector2(x * tileSize - half, -y * tileSize + half);
            rt.sizeDelta = new Vector2(w * tileSize + expand, h * tileSize + expand);
        }

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;

        // Grass dekoratiftir: raycast'i kapat + click proxy ekleme, alttaki taşın tıklaması engellenmesin.
        bool isGrassDecor = def.id == ObstacleId.Grass;
        img.raycastTarget = !isGrassDecor;

        if (!isGrassDecor)
        {
            var clickProxy = go.AddComponent<ObstacleClickProxy>();
            clickProxy.Init(board, x, y, w, h, tileSize);
        }

        return img;
    }
    private void HandleObstacleCreatedDynamic(int x, int y)
    {
        if (resolvedLevel == null || resolvedLevel.obstacleLibrary == null) return;
        int idx = resolvedLevel.Index(x, y);
        if (resolvedLevel.obstacleOrigins[idx] != idx) return;

        var obsId = (ObstacleId)resolvedLevel.obstacles[idx];
        if (obsId == ObstacleId.None) return;

        // Beneath baştan çizildiyse (overlay altında görünüyordu): yeni view çizme, mevcut olanı
        // öne al ve obstacleViewsByOrigin'e promote et — böylece damage/visual event'leri çalışır.
        if (beneathViewsByCell.TryGetValue(idx, out var pre) && pre != null)
        {
            beneathViewsByCell.Remove(idx);

            // Özel view isteyen obstacle'larda generic beneath image promote edilmez.
            // ön-çizilen generic beneath image'ı promote etme; sil ve normal spawn akışına düş.
            if (obsId == ObstacleId.RocketBasket || obsId == ObstacleId.OverrideBatteryBox)
            {
                Destroy(pre.gameObject);
            }
            else
            {
                pre.raycastTarget = true;
                pre.rectTransform.SetAsLastSibling();
                obstacleViewsByOrigin[idx] = pre;
                var preDef = resolvedLevel.obstacleLibrary.Get(obsId);
                if (preDef != null) obstacleDefsByOrigin[idx] = preDef;
                return;
            }
        }

        // Mud ayrı overlay service'iyle çizilir; aynı origin'deki cover view henüz temizlenmemiş
        // olsa bile Mud reveal view'ı skip'lenmemeli.
        if (obsId == ObstacleId.Mud)
        {
            SpawnMudOverlayCell(x, y);
            return;
        }

        if (obstacleViewsByOrigin.ContainsKey(idx)) return;

        var def = resolvedLevel.obstacleLibrary.Get(obsId);
        if (def == null || obsId == ObstacleId.Safe) return;

        // Magnet/Tube already have path-level renderers created from LevelData entries.
        // When a cover such as Grass reveals one cell, do not spawn a generic 1x1
        // obstacle image on top of that renderer; it has no path orientation and looks flipped.
        if (obsId == ObstacleId.Magnet || obsId == ObstacleId.Tube)
            return;

        // Oil ayrı bir overlay renderer'ı kullanır (obstacleViewsByOrigin değil). Bir cover'ın
        // (Chest vb.) altından oil reveal edilince, oil overlay'lerini yenile — yoksa generic
        // DrawObstacleImage yolu oil'i yanlış çizerdi.
        if (obsId == ObstacleId.Oil)
        {
            board?.RefreshOilOverlays();
            return;
        }

        if (def.IsMovableObstacle)
        {
            if (board != null && board.GetTileViewAt(x, y) == null)
                SpawnMovableObstacleTile(x, y);
            return;
        }

        // Cover altından reveal edilen RocketBasket, ilk spawn'la aynı özel view'ı alır
        // (RocketBasketView + service RegisterView) — generic image ateşleme görsellerini taşıyamaz.
        if (def.id == ObstacleId.RocketBasket)
        {
            var basketImage = SpawnRocketBasketView(def, x, y);
            if (basketImage != null)
            {
                obstacleViewsByOrigin[idx] = basketImage;
                obstacleDefsByOrigin[idx] = def;
            }
            return;
        }

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

        if (!IsTrackedObstacleViewFor(change.originIndex, change.obstacleId))
            return;

        if (change.cleared)
        {
            StopObstacleHitShake(change.originIndex, image.rectTransform);

            if (change.obstacleId == ObstacleId.EnergyContainer)
            {
                // Don't destroy — EnergyContainerFx takes over to show the exhausted state.
                obstacleViewsByOrigin.Remove(change.originIndex);
                obstacleDefsByOrigin.Remove(change.originIndex);
                return;
            }
            if (change.obstacleId == ObstacleId.Wardrobe)
            {
                float destroyDelay = WardrobeFinalItemDestroyDelay;
                if (_wardrobeViews.TryGetValue(change.originIndex, out var wardrobeView) && wardrobeView != null)
                    destroyDelay = Mathf.Max(destroyDelay, wardrobeView.RecommendedClearDestroyDelay);
                Destroy(image.gameObject, destroyDelay);
                _wardrobeViews.Remove(change.originIndex);
            }
            else
            {
                Destroy(image.gameObject);
            }

            obstacleViewsByOrigin.Remove(change.originIndex);
            obstacleDefsByOrigin.Remove(change.originIndex);
            ApplyUnderTileCellBgTint();
            return;
        }

        if (ShouldLetEnergyContainerOwnVisual(change.originIndex))
            return;

        // Grass: hit'te sprite'ı DEĞİŞTİRME — A/B checkerboard görseli (DrawObstacleImage'da
        // seçilen) tüm hit'ler boyunca korunmalı; stage sprite'ı doğal görünümü bozmasın.
        if (change.sprite != null && change.obstacleId != ObstacleId.Grass)
            image.sprite = change.sprite;

        if (change.obstacleId == ObstacleId.Barrell_v2)
            PlayObstacleHitShake(change.originIndex, image.rectTransform);
    }

    private void PlayObstacleHitShake(int originIndex, RectTransform target)
    {
        if (target == null)
            return;

        StopObstacleHitShake(originIndex, target);
        obstacleHitShakeBasePositions[originIndex] = target.anchoredPosition;
        obstacleHitShakeRoutines[originIndex] = StartCoroutine(ObstacleHitShakeRoutine(originIndex, target));
    }

    private IEnumerator ObstacleHitShakeRoutine(int originIndex, RectTransform target)
    {
        float elapsed = 0f;
        float amplitude = Mathf.Clamp(tileSize * 0.095f, 7f, 14f);
        Vector2 basePos = obstacleHitShakeBasePositions.TryGetValue(originIndex, out var storedBase)
            ? storedBase
            : target.anchoredPosition;

        while (target != null && elapsed < BarrellV2HitShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / BarrellV2HitShakeDuration);
            float damp = 1f - t;
            float x = Mathf.Sin(t * Mathf.PI * 2f * BarrellV2HitShakeCycles) * amplitude * damp;
            target.anchoredPosition = basePos + new Vector2(x, 0f);
            yield return null;
        }

        if (target != null)
            target.anchoredPosition = basePos;

        obstacleHitShakeRoutines.Remove(originIndex);
        obstacleHitShakeBasePositions.Remove(originIndex);
    }

    private void StopObstacleHitShake(int originIndex, RectTransform target)
    {
        if (obstacleHitShakeRoutines.TryGetValue(originIndex, out var routine) && routine != null)
            StopCoroutine(routine);

        if (target != null && obstacleHitShakeBasePositions.TryGetValue(originIndex, out var basePos))
            target.anchoredPosition = basePos;

        obstacleHitShakeRoutines.Remove(originIndex);
        obstacleHitShakeBasePositions.Remove(originIndex);
    }

    private void StopAllObstacleHitShakes()
    {
        foreach (var kvp in obstacleHitShakeBasePositions)
        {
            if (obstacleViewsByOrigin.TryGetValue(kvp.Key, out var image) && image != null)
                image.rectTransform.anchoredPosition = kvp.Value;
        }

        foreach (var routine in obstacleHitShakeRoutines.Values)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        obstacleHitShakeRoutines.Clear();
        obstacleHitShakeBasePositions.Clear();
    }

    private bool IsTrackedObstacleViewFor(int originIndex, ObstacleId obstacleId)
    {
        if (!obstacleDefsByOrigin.TryGetValue(originIndex, out var trackedDef) || trackedDef == null)
            return true;

        return trackedDef.id == obstacleId;
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
