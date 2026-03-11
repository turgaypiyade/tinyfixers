using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BoardController : MonoBehaviour
{
    // Resolve / cascade state
    public int CurrentResolvePass { get; private set; } = 0;
    public int FallGeneration { get; private set; } = 0;
    internal void IncrementFallGeneration() => FallGeneration++;

    private const float MinLightningLeadTime = 0.05f;
    private const bool PatchBotDebugLogging = true;

    public enum BoosterMode
    {
        None,
        Single,
        Row,
        Column,
        Shuffle
    }

    [SerializeField] private TileIconLibrary iconLibrary;
    [SerializeField] private LevelData levelData;

    [Header("Animation")]
    [SerializeField] private float swapDuration = 0.28f;
    [SerializeField] private float fallDuration = 0.22f;
    [SerializeField] private float clearDuration = 0.16f;
    [SerializeField] private int spawnStartOffsetY = -2;

    [Header("Movement Feel")]
    [SerializeField] private float swapDurationMultiplier = 1f;
    [SerializeField] private float fallColumnStep = 0.015f;
    [SerializeField] private float fallDurationMultiplier = 1f;
    [SerializeField] private AnimationCurve swapMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fallMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall Settle")]
    [SerializeField] private bool enableFallSettle;
    [SerializeField] private float fallSettleDuration = 0.06f;
    [SerializeField] private float fallSettleStrength = 0.04f;
    [SerializeField] private float fallCascadeStep = 0.02f;
    internal float FallColumnStep => Mathf.Max(0f, fallColumnStep);

    [Header("Juice (Only 4+ / Power)")]
    [SerializeField] private float preClearDelay = 0.06f;
    [SerializeField] private float shakeDuration = 0.10f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private RectTransform shakeTarget;

    [Header("Special Combos")]
    [SerializeField] private int patchBotPulseComboSize = 4;

    [Header("PulseCore Impact (premium stagger)")]
    [SerializeField] private float pulseImpactDelayStep = 0.02f;
    [SerializeField] private float pulseImpactAnimTime = 0.16f;

    [Header("Board VFX/SFX")]
    [FormerlySerializedAs("pulseCoreVfxPlayer")][SerializeField] private PulseCoreVfxPlayer boardVfxPlayer;
    [SerializeField] private LightningSpawner lightningSpawner;
    public LineTravelSplitSwapTestUI lineTravelPlayer;
    [SerializeField] private Transform lineTravelSpawnParent;

    [Header("HUD / Goal Fly FX")]
    [SerializeField] private TopHudController topHud;
    [SerializeField] private GoalFlyFx goalFlyFx;

    [Header("Combo VFX")]
    [SerializeField] private OverrideComboController systemOverrideComboVfx;
    [SerializeField] private PulseEmitterComboController pulseEmitterComboVfx;
    [SerializeField] private RectTransform vfxSpace;

    [SerializeField] private GameObject pulsePulseExplosionPrefab;
    [SerializeField] private float pulsePulseExplosionLifetime = 1.0f;

    [Header("Obstacle Visual Tuning")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxPulseCoreBoom;
    [SerializeField] private AudioClip sfxPulseCoreWave;
    [SerializeField] private bool enablePulseMicroShake;
    [SerializeField] private float pulseMicroShakeDuration = 0.08f;
    [SerializeField] private float pulseMicroShakeStrength = 4f;

    [SerializeField] private PatchbotDashUI patchbotDashUI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug / Tile Sync")]
    [SerializeField] private bool enableTileSyncValidation = true;
    [SerializeField] private bool throwOnTileSyncMismatch;
    [SerializeField] private float tilePositionEpsilon = 0.25f;
#endif

    public PatchbotDashUI PatchbotDashUI => patchbotDashUI;
    private Vector2 shakeBasePos;

    private TileView[,] tiles;
    private TileData[,] gridData; // The logical state of the tiles
    
    private bool[,] holes;
    private bool[,] maskHoles;

    private int width;
    private int height;

    private TileView selected;
    public BoardState CurrentState { get; private set; } = BoardState.Idle;
    private BoosterMode activeBooster = BoosterMode.None;
    public  BoosterMode ActiveBooster => activeBooster; // TileView drag kontrolü için

    private GameObject tilePrefab;
    private RectTransform parent;
    private int tileSize;

    public int TileSize => tileSize;
    public bool IsBusy => CurrentState == BoardState.Resolving;

    public event Action OnBecameIdle;

    [System.Serializable]
    public struct PatchbotDashRequest
    {
        public Vector2Int from;
        public Vector2Int to;
    }

    private readonly List<PatchbotDashRequest> _patchbotDashRequests = new();



    public bool InputLocked => CurrentState == BoardState.Locked || IsBusy;
    public int RemainingMoves { get; private set; }
    public LevelData ActiveLevelData => levelData;

    public event Action<int, ObstacleStageSnapshot> OnObstacleStageChanged;
    public event Action<int, ObstacleId> OnObstacleDestroyed;
    public event Action<int> OnCellUnlocked;
    public event Action<int> OnMovesChanged;
    public event Action<TileType, int> OnTilesCleared;
    public event Action<bool> OnBoosterTargetingChanged;

    private TileView lastSwapA;
    private TileView lastSwapB;
    private bool lastSwapUserMove;
    private bool shakeNextClear;
    private bool isSpecialActivationPhase;

    private TileType[] randomPool;

    private MatchFinder matchFinder;
    private SpecialResolver specialResolver;
    private SpecialBehaviorRegistry specialBehaviorRegistry;
    private BoardAnimator boardAnimator;
    private ActionSequencer actionSequencer;
    private PendingCreationService pendingCreationService;
    private PulseCoreImpactService pulseCoreImpactService;
    private ObstacleStateService obstacleStateService;
    private CascadeLogic cascadeLogic;
    private readonly List<Vector3> lightningTargetPositionsBuffer = new List<Vector3>(32);
    private bool didLogMissingLightningSpawner;
    private readonly HashSet<int> patchBotForcedObstacleHits = new();
    private int busyScopeDepth;

    public event System.Action<ObstacleVisualChange> ObstacleVisualChanged;

    internal TileView[,] Tiles => tiles;
    internal TileData[,] GridData => gridData; // newly added getter
    internal BoardAnimator boardAnimatorRef => boardAnimator; // Helper for sequencer
    
    internal bool[,] Holes => holes;
    internal int Width => width;
    internal int Height => height;
    internal float ClearDuration => clearDuration;
    internal float FallDuration => fallDuration;
    internal float SwapDurationWithMultiplier => swapDuration * Mathf.Max(0.01f, swapDurationMultiplier);
    internal float FallDurationWithMultiplier => fallDuration * Mathf.Max(0.01f, fallDurationMultiplier);
    internal AnimationCurve SwapMoveCurve => swapMoveCurve;
    internal AnimationCurve FallMoveCurve => fallMoveCurve;
    internal bool EnableFallSettle => enableFallSettle;
    internal float FallSettleDuration => Mathf.Max(0f, fallSettleDuration);
    internal float FallSettleStrength => Mathf.Max(0f, fallSettleStrength);
    internal float FallCascadeStep => Mathf.Max(0f, fallCascadeStep);
    internal float PreClearDelay => preClearDelay;
    internal float ShakeDuration => shakeDuration;
    internal float ShakeStrength => shakeStrength;
    internal RectTransform ShakeTarget => shakeTarget;
    internal int SpawnStartOffsetY => spawnStartOffsetY;
    internal GameObject TilePrefab => tilePrefab;
    internal RectTransform Parent => parent;
    internal TileType[] RandomPool => randomPool;
    internal LevelData LevelData => levelData;
    internal int PatchBotPulseComboSize => patchBotPulseComboSize;
    internal float PulseImpactDelayStep => pulseImpactDelayStep;
    internal float PulseImpactAnimTime => pulseImpactAnimTime;
    internal PulseCoreVfxPlayer BoardVfxPlayer => boardVfxPlayer;
    internal AudioSource SfxSource => sfxSource;
    internal AudioClip SfxPulseCoreBoom => sfxPulseCoreBoom;
    internal AudioClip SfxPulseCoreWave => sfxPulseCoreWave;
    internal bool EnablePulseMicroShake => enablePulseMicroShake;
    internal float PulseMicroShakeDuration => pulseMicroShakeDuration;
    internal float PulseMicroShakeStrength => pulseMicroShakeStrength;
    internal Vector2 ShakeBasePos { get => shakeBasePos; set => shakeBasePos = value; }
    internal TileView LastSwapA => lastSwapA;
    internal TileView LastSwapB => lastSwapB;
    internal bool LastSwapUserMove { get => lastSwapUserMove; set => lastSwapUserMove = value; }
    internal bool ShakeNextClear { get => shakeNextClear; set => shakeNextClear = value; }
    internal bool IsSpecialActivationPhase { get => isSpecialActivationPhase; set => isSpecialActivationPhase = value; }
    internal ObstacleStateService ObstacleStateService => obstacleStateService;
    internal SpecialBehaviorRegistry SpecialBehaviors => specialBehaviorRegistry;
    public CascadeLogic CascadeLogic => cascadeLogic;

    public void EnqueuePatchbotDash(Vector2Int from, Vector2Int to)
    {
        _patchbotDashRequests.Add(new PatchbotDashRequest { from = from, to = to });
    }

    public void ConsumePatchbotDashRequests(List<PatchbotDashRequest> outList)
    {
        outList.Clear();
        outList.AddRange(_patchbotDashRequests);
        _patchbotDashRequests.Clear();
    }

    public TopHudController TopHud
    {
        get
        {
            if (topHud == null)
                topHud = FindFirstObjectByType<TopHudController>();
            return topHud;
        }
    }

    public GoalFlyFx GoalFlyFx
    {
        get
        {
            if (goalFlyFx == null)
                goalFlyFx = FindFirstObjectByType<GoalFlyFx>();
            return goalFlyFx;
        }
    }

    internal void BeginBusy()
    {
        busyScopeDepth++;
        CurrentState = BoardState.Resolving;
    }

    internal void EndBusy()
    {
        if (busyScopeDepth > 0)
            busyScopeDepth--;

        if (busyScopeDepth == 0 && CurrentState == BoardState.Resolving)
        {
            CurrentState = BoardState.Idle;
            OnBecameIdle?.Invoke();
        }
    }

    /// <summary>
    /// Force full board resync: copies all TileView state into GridData.
    /// Call this at checkpoints where drift may have accumulated (e.g. after popup).
    /// </summary>
    public void ForceFullBoardSync()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ValidateTileSync("ForceFullBoardSync.Before");
#endif
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (tiles[x, y] != null)
            {
                // Force visual icon to match internal special state
                tiles[x, y].RefreshIcon();
                // Reset CanvasGroup alpha in case HideTileVisualForCombo left it invisible
                if (tiles[x, y].TryGetComponent<CanvasGroup>(out var cg))
                    cg.alpha = 1f;
                SyncTileData(x, y);
            }
            else if (gridData[x, y] != null)
            {
                Debug.LogError($"[ForceFullBoardSync] GridData orphan at ({x},{y}) data={gridData[x,y].ToDebugString()} — clearing.");
                gridData[x, y] = null;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ValidateTileSync("ForceFullBoardSync.After");
#endif
    }

    public void RunAfterIdle(Action action)
    {
        if (action == null) return;
        StartCoroutine(RunAfterIdleRoutine(action));
    }

    private IEnumerator RunAfterIdleRoutine(Action action)
    {
        if (!IsBusy)
        {
            action();
            yield break;
        }

        bool idle = false;

        void Handler()
        {
            OnBecameIdle -= Handler;
            idle = true;
        }

        OnBecameIdle += Handler;

        while (!idle)
            yield return null;

        yield return null;
        action();
    }
    
    private void Awake()
    {
        if (shakeTarget != null)
            shakeBasePos = shakeTarget.anchoredPosition;
        EnsureServices();
        TryResolveLightningSpawner();
        if (lineTravelSpawnParent == null && lineTravelPlayer != null && lineTravelPlayer.transform.parent != null)
            lineTravelSpawnParent = lineTravelPlayer.transform.parent;
        EnsureGoalFlyFx();

        if (lightningSpawner == null && !didLogMissingLightningSpawner)
        {
            didLogMissingLightningSpawner = true;
            Debug.LogWarning("[Lightning][BoardController] lightningSpawner reference is not assigned and auto-resolve failed.");
        }
    }

    private void EnsureGoalFlyFx()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var overlay = canvas.transform.Find("GoalFlyOverlayRoot") as RectTransform;
        if (overlay == null)
        {
            var overlayGo = new GameObject("GoalFlyOverlayRoot", typeof(RectTransform));
            overlay = overlayGo.GetComponent<RectTransform>();
            overlay.SetParent(canvas.transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.localScale = Vector3.one;
        }

        overlay.SetAsLastSibling();

        if (goalFlyFx == null)
        {
            var fxTr = canvas.transform.Find("GoalFlyFx");
            if (fxTr != null)
                goalFlyFx = fxTr.GetComponent<GoalFlyFx>();

            if (goalFlyFx == null)
            {
                var fxGo = new GameObject("GoalFlyFx", typeof(RectTransform), typeof(GoalFlyFx));
                fxGo.transform.SetParent(canvas.transform, false);
                goalFlyFx = fxGo.GetComponent<GoalFlyFx>();
            }
        }

        if (goalFlyFx != null)
            goalFlyFx.gameObject.SendMessage("SetOverlayRoot", overlay, SendMessageOptions.DontRequireReceiver);
    }

    public float PlaySystemOverrideComboVfxAndGetDuration()
    {
        if (systemOverrideComboVfx == null) return 0f;
        systemOverrideComboVfx.gameObject.SetActive(true);
        systemOverrideComboVfx.Play();
        return systemOverrideComboVfx.GetTotalDuration();
    }

    public void PlaySystemOverrideComboVfx()
    {
        PlaySystemOverrideComboVfxAndGetDuration();
    }

    public void PlayPulseEmitterComboVfxAtCell(int x, int y)
    {
        if (pulseEmitterComboVfx == null) return;
        if (vfxSpace == null) return;

        pulseEmitterComboVfx.gameObject.SetActive(true);

        TileView ta = lastSwapA;
        TileView tb = lastSwapB;

        if (ta == null || tb == null)
        {
            ta = (x >= 0 && x < Width && y >= 0 && y < Height) ? tiles[x, y] : null;
            tb = ta;
        }

        Vector3 worldA = GetTileWorldCenter(ta);
        Vector3 worldB = GetTileWorldCenter(tb);
        Vector3 worldMid = (worldA + worldB) * 0.5f;

        Vector2 localMid = (Vector2)vfxSpace.InverseTransformPoint(worldMid);
        Vector2 boardSize = vfxSpace.rect.size;
        if (boardSize.sqrMagnitude < 1f)
            boardSize = new Vector2(Width * TileSize, Height * TileSize);

        pulseEmitterComboVfx.SetTileSize(TileSize);
        pulseEmitterComboVfx.PlayAt(localMid, boardSize);
    }

    public void PlayPulsePulseExplosionVfxAtCell(int x, int y)
    {
        if (pulsePulseExplosionPrefab == null) return;
        if (vfxSpace == null) return;

        TileView ta = lastSwapA;
        TileView tb = lastSwapB;

        if (ta == null || tb == null)
        {
            ta = (x >= 0 && x < Width && y >= 0 && y < Height) ? tiles[x, y] : null;
            tb = ta;
        }

        Vector3 worldA = GetTileWorldCenter(ta);
        Vector3 worldB = GetTileWorldCenter(tb);
        Vector3 worldMid = (worldA + worldB) * 0.5f;
        Vector2 localMid = (Vector2)vfxSpace.InverseTransformPoint(worldMid);

        var go = Instantiate(pulsePulseExplosionPrefab, vfxSpace);
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = localMid;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.position = worldMid;
        }

        Destroy(go, pulsePulseExplosionLifetime);
    }

    public Vector3 GetTileWorldCenter(TileView tile)
    {
        if (tile == null) return Vector3.zero;
        var rt = tile.GetComponent<RectTransform>();
        if (rt != null)
            return rt.TransformPoint(rt.rect.center);
        return tile.transform.position;
    }

    private void TryResolveLightningSpawner()
    {
        if (lightningSpawner != null) return;
        lightningSpawner = GetComponentInChildren<LightningSpawner>(true);
        if (lightningSpawner == null && transform.parent != null)
            lightningSpawner = transform.parent.GetComponentInChildren<LightningSpawner>(true);
    }

    void EnsureServices()
    {
        matchFinder ??= new MatchFinder(this);
        boardAnimator ??= new BoardAnimator(this);
        pulseCoreImpactService ??= new PulseCoreImpactService(this, boardAnimator);
        specialBehaviorRegistry ??= new SpecialBehaviorRegistry();
        specialResolver ??= new SpecialResolver(this, matchFinder, boardAnimator, pulseCoreImpactService);
        pendingCreationService ??= new PendingCreationService(this, matchFinder, specialResolver);
        obstacleStateService ??= new ObstacleStateService();
        cascadeLogic ??= new CascadeLogic(this);

        if (actionSequencer == null)
        {
            actionSequencer = GetComponent<ActionSequencer>();
            if (actionSequencer == null) actionSequencer = gameObject.AddComponent<ActionSequencer>();
            actionSequencer.Initialize(this);
        }
    }

    public void OnActionSequenceFinished()
    {
        // For now, doing nothing. Board resolves cascades as part of ProcessSwap/ResolveBoard loops.
    }

    private void OnDestroy()
    {
        if (obstacleStateService == null) return;
        obstacleStateService.OnObstacleStageChanged -= HandleObstacleStageChanged;
        obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
    }

    public void Init(int width, int height, TileIconLibrary iconLibrary)
    {
        this.width = width;
        this.height = height;
        this.iconLibrary = iconLibrary;

        tiles = new TileView[width, height];
        gridData = new TileData[width, height];
        holes = new bool[width, height];
        maskHoles = new bool[width, height];
        EnsureServices();
        if (levelData != null)
            SetLevelData(levelData);
    }

    public void SetLevelData(LevelData levelData)
    {
        this.levelData = levelData;
        RemainingMoves = levelData != null ? Mathf.Max(0, levelData.moves) : 0;
        OnMovesChanged?.Invoke(RemainingMoves);

        EnsureServices();

        if (obstacleStateService != null)
        {
            obstacleStateService.OnObstacleStageChanged -= HandleObstacleStageChanged;
            obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
            obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
        }

        if (levelData == null)
        {
            obstacleStateService = null;
            return;
        }

        obstacleStateService ??= new ObstacleStateService();
        obstacleStateService.Initialize(levelData);
        RebuildMaskHoleMap();
        BindObstacleEvents();
    }

    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamageAt(int x, int y, ObstacleHitContext context)
    {
        if (obstacleStateService == null) return default;

        bool patchBotForcedHit = ConsumePatchBotForcedObstacleHit(x, y);

        var result = obstacleStateService.TryDamageAt(x, y, context);

        ObstacleStateService.ObstacleHitResult TryFallback(ObstacleHitContext fallbackContext)
        {
            if (fallbackContext == context) return default;
            return obstacleStateService.TryDamageAt(x, y, fallbackContext);
        }

        if (!result.didHit && context == ObstacleHitContext.Booster)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
        }
        else if (!result.didHit && patchBotForcedHit)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Scripted);
        }
        else if (!result.didHit && IsCrossContextFallbackAllowedAt(x, y))
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch);
        }

        if (!result.didHit) return result;

        ConsumeObstacleStageTransition(result);
        return result;
    }

    public void TriggerObstacleVisualChange(ObstacleVisualChange change)
    {
        ObstacleVisualChanged?.Invoke(change);
    }

    internal void MarkPatchBotForcedObstacleHit(int x, int y)
    {
        if (obstacleStateService == null || !obstacleStateService.HasObstacleAt(x, y)) return;
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        patchBotForcedObstacleHits.Add(y * width + x);
    }

    private bool ConsumePatchBotForcedObstacleHit(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        int index = y * width + x;
        if (!patchBotForcedObstacleHits.Contains(index)) return false;
        patchBotForcedObstacleHits.Remove(index);
        return true;
    }

    private void ConsumeObstacleStageTransition(ObstacleStateService.ObstacleHitResult result)
    {
        if (!result.stageTransition.hasTransition) return;

        if (!result.stageTransition.cleared)
            OnObstacleStageChanged?.Invoke(result.stageTransition.originIndex, result.stageTransition.currentStage);

        var affected = result.affectedCellIndices;
        if (affected == null || affected.Length == 0) return;

        for (int i = 0; i < affected.Length; i++)
        {
            int cellIndex = affected[i];
            int x = cellIndex % width;
            int y = cellIndex / width;
            if (x < 0 || x >= width || y < 0 || y >= height) continue;

            bool isMaskHole = IsMaskHole(x, y);
            bool blockedByObstacle = obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
            holes[x, y] = isMaskHole || blockedByObstacle;
        }
    }

    private bool IsCrossContextFallbackAllowedAt(int x, int y)
    {
        if (levelData == null || levelData.obstacles == null || levelData.obstacleOrigins == null) return false;
        if (!levelData.InBounds(x, y)) return false;

        int idx = levelData.Index(x, y);
        if (idx < 0 || idx >= levelData.obstacles.Length) return false;

        var obstacleId = (ObstacleId)levelData.obstacles[idx];
        if (obstacleId == ObstacleId.None) return false;

        var obstacleDef = levelData.obstacleLibrary != null ? levelData.obstacleLibrary.Get(obstacleId) : null;
        return obstacleDef != null && obstacleDef.allowCrossContextFallback;
    }

    public void SetupFactory(GameObject tilePrefab, RectTransform parent, int tileSize, TileType[] randomPool)
    {
        this.tilePrefab = tilePrefab;
        this.parent = parent;
        this.tileSize = tileSize;
        this.randomPool = randomPool;
        EnsureServices();
    }

    public void RequestSwapFromDrag(TileView from, int dirX, int dirY)
    {
        if (IsBusy || InputLocked) return;
        if (activeBooster != BoosterMode.None) return; // booster aktifken drag blokla, click açık kalır

        int nx = from.X + dirX;
        int ny = from.Y + dirY;

        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
        if (holes[nx, ny] && (obstacleStateService == null || !obstacleStateService.HasObstacleAt(nx, ny))) return;

        TileView other = tiles[nx, ny];
        if (other == null) return;

        StartCoroutine(ProcessSwap(from, other));
    }

    public void SetHole(int x, int y, bool isHole) => holes[x, y] = isHole;

    public TileView GetTileViewAt(int x, int y)
    {
        if (tiles == null) return null;
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return tiles[x, y];
    }

    public Sprite GetIcon(TileType type) => iconLibrary != null ? iconLibrary.Get(type) : null;
    public Sprite GetSpecialIcon(TileSpecial special) => iconLibrary != null ? iconLibrary.GetSpecialIcon(special) : null;

    public void RegisterTile(TileView tile, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        
        tiles[x, y] = tile;
        tile.Init(this, x, y);
        tile.SetCoords(x, y);
        tile.SnapToGrid(tileSize);
        
        SyncTileData(x, y); // Initialize data model 
        ValidateTileSync($"RegisterTile ({x},{y})");
    }

    public void SyncTileData(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        var tile = tiles[x, y];
        if (tile == null)
        {
            gridData[x, y] = null;
            return;
        }
        
        if (gridData[x, y] == null)
        {
            gridData[x, y] = new TileData(x, y, tile.GetTileType());
        }
        
        var data = gridData[x, y];
        data.SetCoords(x, y);
        data.SetType(tile.GetTileType());
        data.SetSpecial(tile.GetSpecial());

        if (tile.GetSpecial() == TileSpecial.SystemOverride && tile.GetOverrideBaseType(out var baseType))
        {
            data.SetOverrideBaseType(baseType);
        }

        ValidateTileSync($"SyncTileData ({x},{y})", x, y, checkVisualPosition: false);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ValidateTileSync(string context, int onlyX = -1, int onlyY = -1, bool checkVisualPosition = true)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableTileSyncValidation || tiles == null || gridData == null) return;

        int startX = (onlyX >= 0) ? onlyX : 0;
        int endX = (onlyX >= 0) ? onlyX : width - 1;
        int startY = (onlyY >= 0) ? onlyY : 0;
        int endY = (onlyY >= 0) ? onlyY : height - 1;

        bool mismatch = false;
        var sb = new System.Text.StringBuilder();

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                var tile = tiles[x, y];
                var data = gridData[x, y];

                if (tile == null && data != null)
                {
                    mismatch = true;
                    sb.AppendLine($"[{context}] Data var ama view yok @ ({x},{y}) data={data.ToDebugString()}");
                    continue;
                }

                if (tile != null && data == null)
                {
                    mismatch = true;
                    sb.AppendLine($"[{context}] View var ama data yok @ ({x},{y}) view={tile.GetTileType()}/{tile.GetSpecial()}");
                    continue;
                }

                if (tile == null) continue;

                if (tile.X != x || tile.Y != y)
                {
                    mismatch = true;
                    sb.AppendLine($"[{context}] TileView koordinat sapması @ ({x},{y}) viewXY=({tile.X},{tile.Y})");
                }

                if (data.X != x || data.Y != y)
                {
                    mismatch = true;
                    sb.AppendLine($"[{context}] TileData koordinat sapması @ ({x},{y}) dataXY=({data.X},{data.Y})");
                }

                if (data.Type != tile.GetTileType() || data.Special != tile.GetSpecial())
                {
                    mismatch = true;
                    sb.AppendLine($"[{context}] Type/Special mismatch @ ({x},{y}) data={data.Type}/{data.Special} view={tile.GetTileType()}/{tile.GetSpecial()}");
                }

                var rt = tile.GetComponent<RectTransform>();
                if (rt != null && checkVisualPosition)
                {
                    Vector2 expected = new Vector2(x * tileSize, -y * tileSize);
                    if (Vector2.Distance(rt.anchoredPosition, expected) > tilePositionEpsilon)
                    {
                        mismatch = true;
                        sb.AppendLine($"[{context}] Görsel pozisyon sapması @ ({x},{y}) pos={rt.anchoredPosition} expected={expected}");
                    }
                }
            }
        }

        if (!mismatch) return;

        string log = $"[TileSyncValidation] {context}\n{sb}";
        if (throwOnTileSyncMismatch)
            throw new InvalidOperationException(log);

        Debug.LogError(log, this);
#endif
    }

    internal void RefreshTileObstacleVisual(TileView tile)
    {
        if (tile == null) return;
        tile.SetIconAlpha(1f);
    }

    internal void RefreshAllTileObstacleVisuals()
    {
        if (tiles == null) return;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            RefreshTileObstacleVisual(tiles[x, y]);
    }

    public IEnumerator ResolveInitial()
    {
        BeginBusy();
        yield return ResolveBoard(false);
        EndBusy();
    }

    public void ActivateBooster(int boosterIndex)
    {
        switch (boosterIndex)
        {
            case 0: SetBoosterMode(BoosterMode.Single); break;
            case 1: SetBoosterMode(BoosterMode.Row); break;
            case 2: SetBoosterMode(BoosterMode.Column); break;
            case 3: SetBoosterMode(BoosterMode.Shuffle); break;
            default: SetBoosterMode(BoosterMode.None); break;
        }
    }

    void SetBoosterMode(BoosterMode mode)
    {
        activeBooster = mode;
        OnBoosterTargetingChanged?.Invoke(activeBooster != BoosterMode.None);
    }

    bool TryUseBooster(TileView tile)
    {
        if (activeBooster == BoosterMode.None) return false;
        if (tile == null) return true;
        if (IsBusy || InputLocked) return true;
        return TryUseBoosterAtCell(tile.X, tile.Y);
    }

    public bool TryUseBoosterAtCell(int x, int y)
    {
        if (activeBooster == BoosterMode.None) return false;
        if (IsBusy || InputLocked) return true;
        if (x < 0 || x >= width || y < 0 || y >= height) return true;

        var mode = activeBooster;
        SetBoosterMode(BoosterMode.None);
        selected = null;

        var targetCell = new Vector2Int(x, y);
        var targetTile = tiles[x, y];

        if (mode == BoosterMode.Shuffle)
            StartCoroutine(ShuffleBoardRoutine());
        else
            StartCoroutine(ApplyBoosterRoutine(mode, targetTile, targetCell));

        return true;
    }

    public void OnTileClicked(TileView tile)
    {
        if (IsBusy || InputLocked) return;
        if (TryUseBooster(tile)) return;

        if (selected == null) { selected = tile; return; }
        if (selected == tile) { selected = null; return; }

        if (AreNeighbors(selected, tile))
        {
            var a = selected;
            var b = tile;
            selected = null;
            StartCoroutine(ProcessSwap(a, b));
            return;
        }

        selected = tile;
    }

    IEnumerator ApplyBoosterRoutine(BoosterMode mode, TileView target, Vector2Int? targetCell = null)
    {
        BeginBusy();
        isSpecialActivationPhase = true;

        bool hasValidTargetCell = targetCell.HasValue
                                  && targetCell.Value.x >= 0 && targetCell.Value.x < width
                                  && targetCell.Value.y >= 0 && targetCell.Value.y < height;

        if (target == null && !hasValidTargetCell)
        {
            isSpecialActivationPhase = false;
            EndBusy();
            yield break;
        }

        var matches = new HashSet<TileView>();
        HashSet<TileView> initialLightningTargets = null;
        var affectedCells = new HashSet<Vector2Int>();

        switch (mode)
        {
            case BoosterMode.Single:
                if (target != null) matches.Add(target);
                if (hasValidTargetCell && IsCellBoosterAffectable(targetCell.Value.x, targetCell.Value.y))
                    affectedCells.Add(targetCell.Value);
                break;
            case BoosterMode.Row:
                int rowY = target != null ? target.Y : targetCell.GetValueOrDefault().y;
                AddRow(matches, rowY);
                AddRowCells(affectedCells, rowY);
                break;
            case BoosterMode.Column:
                int columnX = target != null ? target.X : targetCell.GetValueOrDefault().x;
                AddColumn(matches, columnX);
                AddColumnCells(affectedCells, columnX);
                break;
        }

        if ((mode == BoosterMode.Row || mode == BoosterMode.Column) && matches.Count > 0)
            initialLightningTargets = new HashSet<TileView>(matches);

        if (matches.Count > 0 || affectedCells.Count > 0)
        {
            bool hasLineActivation = false;

            // ✅ FIX: Tek ExpandSpecialChain çağrısı (önceden iki kez çağrılıyordu — state kirlenmesi)
            var chainLineStrikes = new List<LightningLineStrike>();
            specialResolver.ExpandSpecialChain(
                matches,
                affectedCells,
                out hasLineActivation,
                out _,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: chainLineStrikes);

            var animationMode = (mode == BoosterMode.Row || mode == BoosterMode.Column)
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            if (hasLineActivation)
                animationMode = ClearAnimationMode.LightningStrike;

            ObstacleHitContext obstacleHitContext = ObstacleHitContext.Booster;

            List<LightningLineStrike> lightningLineStrikes = null;
            if (animationMode == ClearAnimationMode.LightningStrike)
            {
                lightningLineStrikes = chainLineStrikes.Count > 0
                    ? chainLineStrikes
                    : new List<LightningLineStrike>();

                if (targetCell.HasValue && (mode == BoosterMode.Row || mode == BoosterMode.Column))
                    lightningLineStrikes.Add(new LightningLineStrike(targetCell.Value, mode == BoosterMode.Row));

                if (lightningLineStrikes.Count == 0)
                    lightningLineStrikes = null;
            }

            actionSequencer.Enqueue(new MatchClearAction(
                matches,
                doShake: true,
                animationMode: animationMode,
                affectedCells: affectedCells,
                obstacleHitContext: obstacleHitContext,
                includeAdjacentOverTileBlockerDamage: false,
                lightningOriginTile: target,
                lightningOriginCell: targetCell,
                lightningVisualTargets: initialLightningTargets,
                lightningLineStrikes: lightningLineStrikes));
            while(actionSequencer.IsPlaying) yield return null;

            var cascadeActions_c = cascadeLogic.CalculateCascades();
            if (cascadeActions_c.Count > 0)
            {
                actionSequencer.Enqueue(cascadeActions_c);
                while (actionSequencer.IsPlaying) yield return null;
            }
            yield return ResolveEmptyPlayableCellsWithoutMatch();
            yield return ResolveBoard();
        }

        isSpecialActivationPhase = false;
        EndBusy();
    }

    internal float PlayLightningStrikeForTiles(
        IReadOnlyCollection<TileView> matches,
        TileView originTile = null,
        Vector2Int? fallbackOriginCell = null,
        IReadOnlyCollection<TileView> visualTargets = null,
        bool allowCondense = true,
        Action<TileView> onTargetBeamSpawned = null)
    {
        TryResolveLightningSpawner();

        if (lightningSpawner == null)
        {
            if (!didLogMissingLightningSpawner)
            {
                didLogMissingLightningSpawner = true;
                Debug.LogWarning("[Lightning][BoardController] lightningSpawner is null, skipping emitter lightning VFX.");
            }
            return 0f;
        }

        if (matches == null || matches.Count == 0) return 0f;

        var targetsForVisuals = visualTargets ?? matches;
        if (onTargetBeamSpawned != null) allowCondense = false;

        Vector3 originWorldPos;
        if (originTile != null)
        {
            originWorldPos = GetTileWorldCenter(originTile);
        }
        else if (fallbackOriginCell.HasValue)
        {
            originWorldPos = GetCellWorldPosition(fallbackOriginCell.Value.x, fallbackOriginCell.Value.y);
        }
        else
        {
            originWorldPos = default;
            bool found = false;
            foreach (var t in targetsForVisuals)
            {
                if (t == null) continue;
                originWorldPos = GetTileWorldCenter(t);
                found = true;
                break;
            }
            if (!found) return 0f;
        }

        lightningTargetPositionsBuffer.Clear();
        var lightningTargetTiles = new List<TileView>(32);

        const float kMinDistFromOrigin = 0.05f;
        float minDistSqr = kMinDistFromOrigin * kMinDistFromOrigin;

        foreach (var tile in targetsForVisuals)
        {
            if (tile == null) continue;
            var p = GetTileWorldCenter(tile);
            if ((p - originWorldPos).sqrMagnitude <= minDistSqr) continue;

            bool dup = false;
            for (int i = 0; i < lightningTargetPositionsBuffer.Count; i++)
            {
                if ((lightningTargetPositionsBuffer[i] - p).sqrMagnitude <= 0.0001f)
                {
                    dup = true;
                    break;
                }
            }
            if (!dup)
            {
                lightningTargetPositionsBuffer.Add(p);
                lightningTargetTiles.Add(tile);
            }
        }

        if (allowCondense && visualTargets == null)
            TryCondenseLightningTargetsToSingleLine(originWorldPos, lightningTargetPositionsBuffer);

        if (lightningTargetPositionsBuffer.Count == 0)
        {
            foreach (var t in targetsForVisuals)
            {
                if (t == null) continue;
                lightningTargetPositionsBuffer.Add(GetTileWorldCenter(t));
                lightningTargetTiles.Add(t);
                break;
            }
        }

        if (lightningTargetPositionsBuffer.Count == 0) return 0f;

        float playbackDuration = lightningSpawner.GetPlaybackDuration(lightningTargetPositionsBuffer.Count);
        if (playbackDuration <= 0f)
        {
            playbackDuration = MinLightningLeadTime;
            Debug.LogWarning($"[Lightning][BoardController] Spawner playbackDuration was <= 0. Using fallback {playbackDuration:0.000}s.");
        }

        if (onTargetBeamSpawned != null)
        {
            lightningSpawner.PlayEmitterLightning(originWorldPos, lightningTargetPositionsBuffer, idx =>
            {
                if (idx < 0 || idx >= lightningTargetTiles.Count) return;
                onTargetBeamSpawned(lightningTargetTiles[idx]);
            });
        }
        else
        {
            lightningSpawner.PlayEmitterLightning(originWorldPos, lightningTargetPositionsBuffer);
        }

        return playbackDuration;
    }

    private void TryCondenseLightningTargetsToSingleLine(Vector3 originWorldPos, List<Vector3> targets)
    {
        if (targets == null || targets.Count < 2) return;

        float tolerance = Mathf.Max(0.01f, tileSize * 0.18f);
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            var p = targets[i];
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        bool looksLikeRow = (maxY - minY) <= tolerance;
        bool looksLikeCol = (maxX - minX) <= tolerance;
        if (!looksLikeRow && !looksLikeCol) return;

        targets.Clear();
        if (looksLikeRow)
        {
            targets.Add(new Vector3(minX, originWorldPos.y, originWorldPos.z));
            targets.Add(new Vector3(maxX, originWorldPos.y, originWorldPos.z));
            return;
        }

        targets.Add(new Vector3(originWorldPos.x, minY, originWorldPos.z));
        targets.Add(new Vector3(originWorldPos.x, maxY, originWorldPos.z));
    }

    internal float PlayLightningLineStrikes(
        IReadOnlyList<LightningLineStrike> lineStrikes,
        Action<Vector2Int> onSweepCellReached = null)
    {
        TryResolveLightningSpawner();

        if (lineStrikes == null || lineStrikes.Count == 0) return 0f;
        if (lineTravelPlayer == null && lightningSpawner == null) return 0f;

        const float StrikeStagger = 0.03f;
        float maxEndTime = 0f;

        for (int i = 0; i < lineStrikes.Count; i++)
        {
            var strike = lineStrikes[i];
            int x = strike.originCell.x;
            int y = strike.originCell.y;
            if (x < 0 || x >= width || y < 0 || y >= height) continue;

            float delay = StrikeStagger * i;

            float endTime = strike.isHorizontal
                ? PlayTwoWaySweepHorizontal(x, y, delay, onSweepCellReached)
                : PlayTwoWaySweepVertical(x, y, delay, onSweepCellReached);

            if (endTime > maxEndTime) maxEndTime = endTime;
        }

        return maxEndTime;
    }

    // ✅ FIX: afterImageParent null olsa bile çalışır; callback OnStepCell üzerinden senkron gelir
    private float PlayTwoWaySweepHorizontal(
        int originX, int y,
        float delaySeconds = 0f,
        Action<Vector2Int> onSweepCellReached = null)
    {
        if (lineTravelPlayer != null)
        {
            Vector3 worldCenter = GetCellWorldCenterPosition(originX, y);

            // ✅ afterImageParent null ise lineTravelSpawnParent'ı kullan
            RectTransform spaceRt = lineTravelPlayer.afterImageParent != null
                ? lineTravelPlayer.afterImageParent
                : lineTravelSpawnParent as RectTransform;

            if (spaceRt == null && lineTravelPlayer.transform.parent != null)
                spaceRt = lineTravelPlayer.transform.parent as RectTransform;

            if (spaceRt != null)
            {
                Vector2 originAnchored = WorldToAnchoredIn(spaceRt, worldCenter);
                int steps = Mathf.Max(originX, width - 1 - originX);

                // ✅ FIX B: EmitHorizontalSweepCallbacks kaldırıldı.
                //    Callback OnStepCell üzerinden görselle senkron gelir.
                PlayLineTravelInstanceWithStep(
                    LineTravelSplitSwapTestUI.LineAxis.Horizontal,
                    originAnchored,
                    new Vector2Int(originX, y),
                    steps,
                    tileSize,
                    delaySeconds,
                    onSweepCellReached);

                float duration = lineTravelPlayer.EstimateDuration(steps);
                return delaySeconds + duration;
            }
        }

        // ─── Fallback: lightning sweep ────────────────────────────────────────
        TryResolveLightningSpawner();
        if (lightningSpawner == null) return 0f;

        var left = new List<Vector3>(originX + 1);
        for (int x = originX; x >= 0; x--)
            left.Add(GetCellWorldCenterPosition(x, y));

        var right = new List<Vector3>(width - originX);
        for (int x = originX; x < width; x++)
            right.Add(GetCellWorldCenterPosition(x, y));

        lightningSpawner.PlayLineSweepSteps(left);
        lightningSpawner.PlayLineSweepSteps(right);

        float dl = lightningSpawner.GetPlaybackDuration(left.Count);
        float dr = lightningSpawner.GetPlaybackDuration(right.Count);
        float sweepDur = Mathf.Max(dl, dr);

        EmitHorizontalSweepCallbacks(originX, y, delaySeconds, sweepDur, onSweepCellReached);
        return delaySeconds + sweepDur;
    }

    // ✅ FIX: aynı pattern — OnStepCell üzerinden senkron callback
    private float PlayTwoWaySweepVertical(
        int x, int originY,
        float delaySeconds = 0f,
        Action<Vector2Int> onSweepCellReached = null)
    {
        if (lineTravelPlayer != null)
        {
            Vector3 worldCenter = GetCellWorldCenterPosition(x, originY);

            RectTransform spaceRt = lineTravelPlayer.afterImageParent != null
                ? lineTravelPlayer.afterImageParent
                : lineTravelSpawnParent as RectTransform;

            if (spaceRt == null && lineTravelPlayer.transform.parent != null)
                spaceRt = lineTravelPlayer.transform.parent as RectTransform;

            if (spaceRt != null)
            {
                Vector2 originAnchored = WorldToAnchoredIn(spaceRt, worldCenter);
                int steps = Mathf.Max(originY, height - 1 - originY);

                PlayLineTravelInstanceWithStep(
                    LineTravelSplitSwapTestUI.LineAxis.Vertical,
                    originAnchored,
                    new Vector2Int(x, originY),
                    steps,
                    tileSize,
                    delaySeconds,
                    onSweepCellReached);

                float duration = lineTravelPlayer.EstimateDuration(steps);
                return delaySeconds + duration;
            }
        }

        // ─── Fallback: lightning sweep ────────────────────────────────────────
        TryResolveLightningSpawner();
        if (lightningSpawner == null) return 0f;

        var down = new List<Vector3>(originY + 1);
        for (int y = originY; y >= 0; y--)
            down.Add(GetCellWorldCenterPosition(x, y));

        var up = new List<Vector3>(height - originY);
        for (int y = originY; y < height; y++)
            up.Add(GetCellWorldCenterPosition(x, y));

        lightningSpawner.PlayLineSweepSteps(down);
        lightningSpawner.PlayLineSweepSteps(up);

        float dd = lightningSpawner.GetPlaybackDuration(down.Count);
        float du = lightningSpawner.GetPlaybackDuration(up.Count);
        float sweepDurV = Mathf.Max(dd, du);

        EmitVerticalSweepCallbacks(x, originY, delaySeconds, sweepDurV, onSweepCellReached);
        return delaySeconds + sweepDurV;
    }

    // Fallback path için korunuyor (lightning sweep yolunda hâlâ gerekli)
    private void EmitHorizontalSweepCallbacks(
        int originX, int y,
        float delaySeconds, float sweepDuration,
        Action<Vector2Int> onSweepCellReached)
    {
        if (onSweepCellReached == null) return;

        int maxDistance = Mathf.Max(originX, width - 1 - originX);
        float stepInterval = maxDistance > 0 ? sweepDuration / maxDistance : 0f;

        StartCoroutine(CoEmitLineSweepCellCallbacks(delaySeconds, stepInterval, maxDistance, step =>
        {
            int leftX = originX - step;
            if (leftX >= 0 && leftX < width)
                onSweepCellReached(new Vector2Int(leftX, y));

            if (step == 0) return;

            int rightX = originX + step;
            if (rightX >= 0 && rightX < width)
                onSweepCellReached(new Vector2Int(rightX, y));
        }));
    }

    private void EmitVerticalSweepCallbacks(
        int x, int originY,
        float delaySeconds, float sweepDuration,
        Action<Vector2Int> onSweepCellReached)
    {
        if (onSweepCellReached == null) return;

        int maxDistance = Mathf.Max(originY, height - 1 - originY);
        float stepInterval = maxDistance > 0 ? sweepDuration / maxDistance : 0f;

        StartCoroutine(CoEmitLineSweepCellCallbacks(delaySeconds, stepInterval, maxDistance, step =>
        {
            int downY = originY - step;
            if (downY >= 0 && downY < height)
                onSweepCellReached(new Vector2Int(x, downY));

            if (step == 0) return;

            int upY = originY + step;
            if (upY >= 0 && upY < height)
                onSweepCellReached(new Vector2Int(x, upY));
        }));
    }

    private IEnumerator CoEmitLineSweepCellCallbacks(
        float delaySeconds, float stepInterval,
        int maxDistance, Action<int> emitStep)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        for (int step = 0; step <= maxDistance; step++)
        {
            emitStep?.Invoke(step);
            if (stepInterval > 0f)
                yield return new WaitForSeconds(stepInterval);
            else
                yield return null;
        }
    }

    private Vector3 GetCellWorldCenterPosition(int x, int y)
    {
        Vector3 localCenter = new Vector3(x * tileSize + tileSize * 0.5f, -y * tileSize - tileSize * 0.5f, 0f);
        if (parent != null)
            return parent.TransformPoint(localCenter);
        return transform.TransformPoint(localCenter);
    }

    internal float GetLightningStrikeStepDelay()
    {
        TryResolveLightningSpawner();
        if (lightningSpawner == null) return 0f;
        return lightningSpawner.GetStepDelay();
    }

    public Vector3 GetCellWorldPosition(int x, int y)
    {
        if (parent != null)
            return parent.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
        return transform.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
    }

    IEnumerator ShuffleBoardRoutine()
    {
        BeginBusy();
        selected = null;

        var activeTiles = new List<TileView>();
        var types = new List<TileType>();

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (holes[x, y]) continue;
            var tile = tiles[x, y];
            if (tile == null) continue;
            if (tile.GetSpecial() != TileSpecial.None) continue;
            activeTiles.Add(tile);
            types.Add(tile.GetTileType());
        }

        for (int i = types.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (types[i], types[j]) = (types[j], types[i]);
        }

        for (int i = 0; i < activeTiles.Count; i++)
        {
            var tile = activeTiles[i];
            tile.SetType(types[i]);
            SyncTileData(tile.X, tile.Y); // Sync Data model AFTER shuffle type change
            RefreshTileObstacleVisual(tile);
        }

        yield return ResolveBoard();
        EndBusy();
    }

    void AddRow(HashSet<TileView> matches, int y)
    {
        if (y < 0 || y >= height) return;
        for (int x = 0; x < width; x++)
            if (!holes[x, y] && tiles[x, y] != null)
                matches.Add(tiles[x, y]);
    }

    void AddColumn(HashSet<TileView> matches, int x)
    {
        if (x < 0 || x >= width) return;
        for (int y = 0; y < height; y++)
            if (!holes[x, y] && tiles[x, y] != null)
                matches.Add(tiles[x, y]);
    }

    void AddRowCells(HashSet<Vector2Int> affectedCells, int y)
    {
        if (affectedCells == null || y < 0 || y >= height) return;
        for (int x = 0; x < width; x++)
        {
            if (!IsCellBoosterAffectable(x, y)) continue;
            affectedCells.Add(new Vector2Int(x, y));
        }
    }

    void AddColumnCells(HashSet<Vector2Int> affectedCells, int x)
    {
        if (affectedCells == null || x < 0 || x >= width) return;
        for (int y = 0; y < height; y++)
        {
            if (!IsCellBoosterAffectable(x, y)) continue;
            affectedCells.Add(new Vector2Int(x, y));
        }
    }

    bool IsCellBoosterAffectable(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        if (!holes[x, y]) return true;
        return obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y);
    }

    bool AreNeighbors(TileView a, TileView b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return dx + dy == 1;
    }

    IEnumerator ProcessSwap(TileView a, TileView b)
    {
        BeginBusy();

        lastSwapA = a;
        lastSwapB = b;
        lastSwapUserMove = true;

        // ─── FULL SYNC VALIDATION: catch any drift that accumulated during cascades ──
        ValidateTileSync("ProcessSwap.Entry");
        // Force a full resync to prevent stale data from causing wrong combos
        for (int sy = 0; sy < height; sy++)
        for (int sx = 0; sx < width; sx++)
        {
            if (tiles[sx, sy] != null)
                SyncTileData(sx, sy);
        }

        // ─── DEBUG: Hamle öncesi snapshot ──────────────────────────────────
        {
            var swapSb = new System.Text.StringBuilder();
            swapSb.AppendLine($"[PRE-SWAP SNAPSHOT] A=({a.X},{a.Y}) B=({b.X},{b.Y})");
            for (int dbgY = 0; dbgY < height; dbgY++)
            {
                swapSb.Append($"  row{dbgY}: ");
                for (int dbgX = 0; dbgX < width; dbgX++)
                {
                    if (holes[dbgX, dbgY]) swapSb.Append("[H ]");
                    else { var td = gridData[dbgX, dbgY]; swapSb.Append(td == null ? "[· ]" : $"[{td.ToDebugString().PadRight(2)}]"); }
                }
                swapSb.AppendLine();
            }
            Debug.Log(swapSb.ToString());
        }
        // ─── 1. LOGIK SWAP (DATA) ─────────────────────────────────────────
        int ax = a.X, ay = a.Y;
        int bx = b.X, by = b.Y;

        tiles[ax, ay] = b;
        tiles[bx, by] = a;
        
        a.SetCoords(bx, by);
        b.SetCoords(ax, ay);
        
        SyncTileData(ax, ay);
        SyncTileData(bx, by);

        // ─── 2. GÖRSEL SWAP (ACTION) ──────────────────────────────────────
        actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
        while(actionSequencer.IsPlaying) yield return null;
        ValidateTileSync("ProcessSwap.AfterSwapAnimation");

        pendingCreationService.Clear();
        bool hasPendingCreation = pendingCreationService.CapturePendingCreation(a, b);

        TileSpecial sa = a.GetSpecial();
        TileSpecial sb = b.GetSpecial();

        if (sa != TileSpecial.None || sb != TileSpecial.None)
        {
            ConsumeMove();

            // ── Yalnızca bir taraf special ise: normal tarafın oluşturduğu match'i
            //    special aktivasyonundan ÖNCE resolve et.
            //    Örnek: SP ↔ YT swap'ı, 4'lü YT sütunu oluşuyor →
            //           önce LineV oluşsun (normalTile pozisyonunda) + fazla taşlar temizlensin,
            //           sonra SP çalışsın.
            bool onlyOneSpecial = (sa != TileSpecial.None) ^ (sb != TileSpecial.None);
            if (onlyOneSpecial)
            {
                var normalTile  = (sa == TileSpecial.None) ? a : b;
                var specialTile = (sa != TileSpecial.None) ? a : b;

                var matchSet = new HashSet<TileData>();
                foreach (var t in matchFinder.FindMatchesAt(normalTile.X, normalTile.Y))
                    matchSet.Add(t);

                var normalSideMatches = new HashSet<TileData>();
                if (matchSet.Count >= 3)
                {
                    foreach (var md in matchSet)
                    {
                        if (tiles[md.X, md.Y] != null)
                            normalSideMatches.Add(md);
                    }
                }

                if (normalSideMatches.Count == 0)
                    matchFinder.Add2x2Candidates(normalSideMatches, normalTile.X, normalTile.Y);

                // specialTile ve normalTile bu aşamada temizlenmemeli
                if (specialTile != null && gridData[specialTile.X, specialTile.Y] != null) 
                    normalSideMatches.Remove(gridData[specialTile.X, specialTile.Y]);
                
                if (normalTile != null && gridData[normalTile.X, normalTile.Y] != null) 
                    normalSideMatches.Remove(gridData[normalTile.X, normalTile.Y]);

                if (normalSideMatches.Count > 0)
                {
                    // Bu match yeni bir special üretmeli mi? (4+ → LineV/LineH, 5 → PulseCore vb.)
                    TileSpecial newSpec = matchFinder.DecideSpecialAt(normalTile.X, normalTile.Y);

                    bool normalGotNewSpecial = false;
                    if (newSpec != TileSpecial.None)
                    {
                        // Special'ı normalTile'ın kendisine yerleştir (swap pozisyonu)
                        normalTile.SetSpecial(newSpec);
                        if (newSpec == TileSpecial.SystemOverride)
                            normalTile.SetOverrideBaseType(normalTile.GetTileType());
                        
                        SyncTileData(normalTile.X, normalTile.Y); // Sync Data model
                        normalGotNewSpecial = true;

                        // Override'ı collapse öncesi gizle — kullanıcı düşüşü görmesin,
                        // ResolveSpecialSolo yerleştiği pozisyondan ışın gönderecek
                        if (!specialTile.TryGetComponent<CanvasGroup>(out var cg))
                            cg = specialTile.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                        cg.interactable = false;
                    }

                    // Kalan eşleşen taşları temizle + collapse
                    pendingCreationService.Clear();
                    
                    var normalMatchViews = new HashSet<TileView>();
                    foreach (var data in normalSideMatches)
                    {
                        if (tiles[data.X, data.Y] != null) normalMatchViews.Add(tiles[data.X, data.Y]);
                    }

                    actionSequencer.Enqueue(new MatchClearAction(normalMatchViews, true));
                    while(actionSequencer.IsPlaying) yield return null;

                    var cascadeActions_c2 = cascadeLogic.CalculateCascades();
                    if (cascadeActions_c2.Count > 0)
                    {
                        actionSequencer.Enqueue(cascadeActions_c2);
                        while (actionSequencer.IsPlaying) yield return null;
                    }
                    yield return ResolveEmptyPlayableCellsWithoutMatch();

                    // normalTile'a yeni special yerleştirildiyse →
                    // specialTile'ı solo aktive et (combo DEĞİL, bağımsız).
                    // Yeni special, chain mekanizmasıyla (SP etki alanındaysa)
                    // veya sonraki cascade/hamlelerde tetiklenir.
                    if (normalGotNewSpecial)
                    {
                        var soloActions = specialResolver.ResolveSpecialSolo(specialTile);
                        actionSequencer.Enqueue(soloActions);
                        while (actionSequencer.IsPlaying) yield return null;
                        var cascadeActions_c3 = cascadeLogic.CalculateCascades();
                        if (cascadeActions_c3.Count > 0)
                        {
                            actionSequencer.Enqueue(cascadeActions_c3);
                            while (actionSequencer.IsPlaying) yield return null;
                        }

                        yield return ResolveEmptyPlayableCellsWithoutMatch();
                        yield return ResolveBoard();
                        EndBusy();
                        yield break;
                    }
                }
            }

            // İki taraf da special ise eski pending creation akışını koru
            if (pendingCreationService.HasPending)
                pendingCreationService.ApplyPendingCreations();

            var swapActions = specialResolver.ResolveSpecialSwap(a, b);
            actionSequencer.Enqueue(swapActions);
            while (actionSequencer.IsPlaying) yield return null;
            var cascadeActions_c = cascadeLogic.CalculateCascades();
            if (cascadeActions_c.Count > 0)
            {
                actionSequencer.Enqueue(cascadeActions_c);
                while (actionSequencer.IsPlaying) yield return null;
            }

            yield return ResolveEmptyPlayableCellsWithoutMatch();
            yield return ResolveBoard();
            EndBusy();
            yield break;
        }

        var matches = new HashSet<TileData>();
        foreach (var t in matchFinder.FindMatchesAt(a.X, a.Y)) matches.Add(t);
        foreach (var t in matchFinder.FindMatchesAt(b.X, b.Y)) matches.Add(t);

        if (matches.Count == 0)
        {
            matchFinder.Add2x2Candidates(matches, a.X, a.Y);
            matchFinder.Add2x2Candidates(matches, b.X, b.Y);
        }

        if (matches.Count == 0)
        {
            // ─── 1. REVERT LOGIK SWAP (DATA) ──────────────────────────────
            tiles[ax, ay] = a;
            tiles[bx, by] = b;
            
            a.SetCoords(ax, ay);
            b.SetCoords(bx, by);
            
            SyncTileData(ax, ay);
            SyncTileData(bx, by);

            // ─── 2. REVERT GÖRSEL SWAP (ACTION) ───────────────────────────
            actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
            while(actionSequencer.IsPlaying) yield return null;

            EndBusy();
            yield break;
        }

        shakeNextClear = matchFinder.HasAnyRunAtLeast(4);
        ConsumeMove();

        yield return ResolveBoard();
        EndBusy();
    }

    public bool HasAnyEmptyPlayableCell()
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (holes[x, y]) continue;
            if (tiles[x, y] == null) return true;
        }
        return false;
    }

    internal IEnumerator ResolveEmptyPlayableCellsWithoutMatch()
    {
        var cascades = cascadeLogic.CalculateCascades();
        if (cascades.Count > 0)
        {
            actionSequencer.Enqueue(cascades);
            while (actionSequencer.IsPlaying) yield return null;
        }
    }

    internal float GetFallDurationForDistance(int cellDistance)
    {
        float baseDur = FallDurationWithMultiplier;
        int d = Mathf.Max(1, cellDistance);
        float distDur = Mathf.Clamp(d * 0.06f, 0.06f, 0.22f);
        float duration = Mathf.Min(baseDur, distDur) * Mathf.Max(0.25f, GetCascadeFallSpeedMultiplier());
        return Mathf.Max(0.04f, duration);
    }

    internal float GetClearDurationForCurrentPass()
    {
        return Mathf.Max(0.03f, ClearDuration * GetCascadeClearSpeedMultiplier());
    }

    internal bool ShouldEnableFallSettleThisPass()
    {
        return EnableFallSettle && CurrentResolvePass <= 1;
    }

    private float GetCascadeFallSpeedMultiplier() => (CurrentResolvePass <= 1) ? 1f : 0.75f;
    private float GetCascadeClearSpeedMultiplier() => (CurrentResolvePass <= 1) ? 1f : 0.85f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float _resolveProfT0;
    private void ResolveProfBegin(string tag) { _resolveProfT0 = Time.realtimeSinceStartup; }
    private void ResolveProfStep(string label) { _resolveProfT0 = Time.realtimeSinceStartup; }
#endif

    IEnumerator ResolveBoard(bool allowSpecial = true)
    {
        isSpecialActivationPhase = false;
        int safety = 0;
        const int MaxResolveLoops = 25;

        while (true)
        {
            safety++;
            if (safety > MaxResolveLoops)
            {
                Debug.LogWarning($"[ResolveBoard] Safety break! loops={safety}");
                yield break; // EndBusy is called by the caller (ProcessSwap, ResolveInitial, etc.)
            }

            CurrentResolvePass = safety;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfBegin($"pass={CurrentResolvePass}");
#endif

            var matches = matchFinder.FindAllMatches();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep($"FindMatches (count={matches.Count})");
#endif

            if (matches.Count == 0) yield break;

            var matchTiles = new HashSet<TileView>();
            foreach(var t in matches) 
            {
                if (tiles[t.X, t.Y] != null) matchTiles.Add(tiles[t.X, t.Y]);
            }

            matchTiles.RemoveWhere(t => t != null && t.GetSpecial() != TileSpecial.None);
            if (matchTiles.Count == 0) yield break;

            if (allowSpecial)
            {
                var created = specialResolver.TryCreateSpecial(matchTiles);
                if (created != null) shakeNextClear = true;
            }

            bool doShake = shakeNextClear;
            shakeNextClear = false;

            actionSequencer.Enqueue(new MatchClearAction(matchTiles, doShake));
            while(actionSequencer.IsPlaying) yield return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep("ClearMatchesAnimated");
#endif

            var cascadeActions = cascadeLogic.CalculateCascades();
            if (cascadeActions.Count > 0)
            {
                actionSequencer.Enqueue(cascadeActions);
                while (actionSequencer.IsPlaying) yield return null;
                ValidateTileSync($"ResolveBoard.Pass{CurrentResolvePass}.AfterCascade");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep("Cascades Processed");
#endif
        }
    }

    public TileType[,] SimulateInitialTypes()
    {
        var types = new TileType[width, height];
        var matched = new bool[width, height];
        var filled = new bool[width, height];

        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            System.Array.Clear(types, 0, types.Length);
            System.Array.Clear(matched, 0, matched.Length);
            System.Array.Clear(filled, 0, filled.Length);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (holes[x, y]) continue;
                types[x, y] = PickTypeAvoidingMatch(types, filled, x, y);
                filled[x, y] = true;
            }

            MarkInitialMatches(types, matched);
            if (!HasAnyMatched(matched)) return types;
        }

        return types;
    }

    private void MarkInitialMatches(TileType[,] types, bool[,] matched)
    {
        for (int y = 0; y < height; y++)
        {
            int run = 0;
            TileType runType = default;
            int runStart = 0;

            for (int x = 0; x < width; x++)
            {
                if (holes[x, y]) { MarkRunIfNeeded(run, runStart, y, true, matched); run = 0; continue; }
                var t = types[x, y];
                if (run == 0) { run = 1; runType = t; runStart = x; continue; }
                if (t.Equals(runType)) { run++; continue; }
                MarkRunIfNeeded(run, runStart, y, true, matched);
                run = 1; runType = t; runStart = x;
            }
            MarkRunIfNeeded(run, runStart, y, true, matched);
        }

        for (int x = 0; x < width; x++)
        {
            int run = 0;
            TileType runType = default;
            int runStart = 0;

            for (int y = 0; y < height; y++)
            {
                if (holes[x, y]) { MarkRunIfNeeded(run, runStart, x, false, matched); run = 0; continue; }
                var t = types[x, y];
                if (run == 0) { run = 1; runType = t; runStart = y; continue; }
                if (t.Equals(runType)) { run++; continue; }
                MarkRunIfNeeded(run, runStart, x, false, matched);
                run = 1; runType = t; runStart = y;
            }
            MarkRunIfNeeded(run, runStart, x, false, matched);
        }
    }

    private void MarkRunIfNeeded(int run, int runStart, int fixedIndex, bool horizontal, bool[,] matched)
    {
        if (run < 3) return;
        for (int i = 0; i < run; i++)
        {
            int x = horizontal ? runStart + i : fixedIndex;
            int y = horizontal ? fixedIndex : runStart + i;
            matched[x, y] = true;
        }
    }

    private bool HasAnyMatched(bool[,] matched)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (matched[x, y]) return true;
        return false;
    }

    private TileType PickTypeAvoidingMatch(TileType[,] types, bool[,] filled, int x, int y)
    {
        if (randomPool == null || randomPool.Length == 0) return default;

        int start = UnityEngine.Random.Range(0, randomPool.Length);
        for (int i = 0; i < randomPool.Length; i++)
        {
            var candidate = randomPool[(start + i) % randomPool.Length];
            if (!CreatesMatch(types, filled, x, y, candidate)) return candidate;
        }
        return randomPool[start];
    }

    private bool CreatesMatch(TileType[,] types, bool[,] filled, int x, int y, TileType candidate)
    {
        int count = 1;
        int lx = x - 1;
        while (lx >= 0 && !holes[lx, y] && filled[lx, y] && types[lx, y].Equals(candidate)) { count++; lx--; }
        int rx = x + 1;
        while (rx < width && !holes[rx, y] && filled[rx, y] && types[rx, y].Equals(candidate)) { count++; rx++; }
        if (count >= 3) return true;

        if (x > 0 && y > 0)
        {
            if (!holes[x - 1, y] && filled[x - 1, y] &&
                !holes[x, y - 1] && filled[x, y - 1] &&
                !holes[x - 1, y - 1] && filled[x - 1, y - 1] &&
                types[x - 1, y].Equals(candidate) &&
                types[x, y - 1].Equals(candidate) &&
                types[x - 1, y - 1].Equals(candidate))
                return true;
        }

        count = 1;
        int uy = y - 1;
        while (uy >= 0 && !holes[x, uy] && filled[x, uy] && types[x, uy].Equals(candidate)) { count++; uy--; }
        int dy = y + 1;
        while (dy < height && !holes[x, dy] && filled[x, dy] && types[x, dy].Equals(candidate)) { count++; dy++; }
        return count >= 3;
    }

    private void BindObstacleEvents()
    {
        if (obstacleStateService == null) return;
        obstacleStateService.OnObstacleStageChanged -= HandleObstacleStageChanged;
        obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
        obstacleStateService.OnObstacleStageChanged += HandleObstacleStageChanged;
        obstacleStateService.OnObstacleDestroyed += HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked += HandleCellUnlocked;
    }

    private void HandleObstacleStageChanged(int originIndex, ObstacleStageSnapshot stage) { }
    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId) => OnObstacleDestroyed?.Invoke(originIndex, obstacleId);

    private void HandleCellUnlocked(int cellIndex)
    {
        int x = cellIndex % width;
        int y = cellIndex / width;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        bool isMaskHole = IsMaskHole(x, y);
        bool blockedByObstacle = obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
        holes[x, y] = isMaskHole || blockedByObstacle;

        if (!holes[x, y]) OnCellUnlocked?.Invoke(cellIndex);
    }

    private void RebuildMaskHoleMap()
    {
        if (maskHoles == null || maskHoles.GetLength(0) != width || maskHoles.GetLength(1) != height)
            maskHoles = new bool[width, height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool isMaskHole = false;
            if (levelData != null && levelData.cells != null)
            {
                int idx = levelData.Index(x, y);
                if (idx >= 0 && idx < levelData.cells.Length)
                    isMaskHole = levelData.cells[idx] == (int)CellType.Empty;
            }
            maskHoles[x, y] = isMaskHole;
        }
    }

    private bool IsMaskHole(int x, int y)
    {
        if (maskHoles == null) return false;
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return maskHoles[x, y];
    }

    internal bool IsMaskHoleCell(int x, int y) => IsMaskHole(x, y);

    internal bool IsObstacleBlockedCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
    }

    internal bool IsSpawnPassThroughCell(int x, int y) => IsMaskHoleCell(x, y) && !IsObstacleBlockedCell(x, y);

    public bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state)
    {
        return BoardCellStateQuery.TryGet(this, x, y, out state);
    }

    private TileType GetRandomType()
    {
        return randomPool[UnityEngine.Random.Range(0, randomPool.Length)];
    }

    public void SetInputLocked(bool isLocked) 
    { 
        if (isLocked) CurrentState = BoardState.Locked;
        else if (CurrentState == BoardState.Locked) CurrentState = BoardState.Idle;
    }

    private void ConsumeMove()
    {
        RemainingMoves = Mathf.Max(0, RemainingMoves - 1);
        OnMovesChanged?.Invoke(RemainingMoves);
    }

    public void AddMoves(int amount)
    {
        if (amount <= 0) return;
        RemainingMoves += amount;
        OnMovesChanged?.Invoke(RemainingMoves);
    }

    internal void NotifyTilesCleared(TileType tileType, int amount)
    {
        if (amount <= 0) return;
        OnTilesCleared?.Invoke(tileType, amount);
    }
    public PulseLineComboAction CreatePulseEmitterComboAction(int cx, int cy)
    {
        var targets = BuildPulseEmitterTargets(cx, cy);

        RectTransform space = null;
        if (lineTravelPlayer != null)
        {
            space = lineTravelPlayer.afterImageParent != null
                ? lineTravelPlayer.afterImageParent
                : (lineTravelSpawnParent as RectTransform);
        }

        // 1) PRE-CALCULATE ORIGINS 
        var hOrigins = new List<(Vector2Int cell, Vector2 anch)>();
        var vOrigins = new List<(Vector2Int cell, Vector2 anch)>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= height) continue;
            var originTile = tiles[cx, yy];
            if (originTile == null) continue;

            var rt = originTile.GetComponent<RectTransform>();
            Vector3 worldCenter = rt.TransformPoint(new Vector3(tileSize * 0.5f, -tileSize * 0.5f, 0f));
            hOrigins.Add((new Vector2Int(cx, yy), WorldToAnchoredIn(space, worldCenter)));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= width) continue;
            var originTile = tiles[xx, cy];
            if (originTile == null) continue;

            var rt = originTile.GetComponent<RectTransform>();
            Vector3 worldCenter = rt.TransformPoint(new Vector3(tileSize * 0.5f, -tileSize * 0.5f, 0f));
            vOrigins.Add((new Vector2Int(xx, cy), WorldToAnchoredIn(space, worldCenter)));
        }

        // Gather visuals for the Action before Data is destroyed
        var targetVisuals = new Dictionary<Vector2Int, (TileType, TileView)>();
        foreach (var c in targets)
        {
            var t = tiles[c.x, c.y];
            if (t != null)
            {
                targetVisuals[c] = (t.GetTileType(), t);
            }
        }

        // CLEAR DATA ONLY (Instantly)
        foreach (var c in targets)
        {
            ClearCellDataOnly(c);
        }

        // Return the visual action container
        return new PulseLineComboAction(this, cx, cy, targets, hOrigins, vOrigins, targetVisuals);
    }

    // ✅ FIX: afterImageParent ve impactParent template'den clone'a kopyalanıyor
    private void PlayLineTravelInstance(
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        int steps,
        float cellSizePx,
        float delaySeconds)
    {
        if (lineTravelPlayer == null) return;

        Transform parentTr = lineTravelSpawnParent != null
            ? lineTravelSpawnParent
            : (lineTravelPlayer.transform.parent != null
                ? lineTravelPlayer.transform.parent
                : transform);

        var go = Instantiate(lineTravelPlayer.gameObject, parentTr);
        go.SetActive(true);

        var inst = go.GetComponent<LineTravelSplitSwapTestUI>();
        if (inst == null) { Destroy(go); return; }

        // ✅ FIX: Instantiate scene-object referanslarını kopyalamaz; manuel aktar
        if (inst.afterImageParent == null && lineTravelPlayer.afterImageParent != null)
            inst.afterImageParent = lineTravelPlayer.afterImageParent;

        if (inst.impactParent == null && lineTravelPlayer.impactParent != null)
            inst.impactParent = lineTravelPlayer.impactParent;

        StartCoroutine(PlayLineTravelRoutine(inst, axis, originAnchored, steps, cellSizePx, delaySeconds));

        float totalLife = Mathf.Max(0f, delaySeconds) + lineTravelPlayer.EstimateDuration(steps) + 0.10f;
        StartCoroutine(DestroyAfterUnscaled(go, totalLife));
    }

    // ✅ FIX: afterImageParent ve impactParent kopyalanıyor
    internal float PlayLineTravelInstanceWithStep(
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        Vector2Int originCell,
        int steps,
        float cellSizePx,
        float delaySeconds,
        Action<Vector2Int> onStep)
    {
        if (lineTravelPlayer == null) return 0f;

        Transform parentTr = lineTravelSpawnParent != null
            ? lineTravelSpawnParent
            : (lineTravelPlayer.transform.parent != null
                ? lineTravelPlayer.transform.parent
                : transform);

        var go = Instantiate(lineTravelPlayer.gameObject, parentTr);
        go.SetActive(true);

        var inst = go.GetComponent<LineTravelSplitSwapTestUI>();
        if (inst == null) { Destroy(go); return 0f; }

        // ✅ FIX: referansları aktar
        if (inst.afterImageParent == null && lineTravelPlayer.afterImageParent != null)
            inst.afterImageParent = lineTravelPlayer.afterImageParent;

        if (inst.impactParent == null && lineTravelPlayer.impactParent != null)
            inst.impactParent = lineTravelPlayer.impactParent;

        StartCoroutine(PlayLineTravelRoutine(inst, axis, originAnchored, steps, cellSizePx, delaySeconds, originCell, onStep));

        float dur = lineTravelPlayer.EstimateDuration(steps);
        float totalLife = Mathf.Max(0f, delaySeconds) + dur + 0.15f;
        StartCoroutine(DestroyAfterUnscaled(go, totalLife));

        return delaySeconds + dur;
    }

    private IEnumerator PlayLineTravelRoutine(
        LineTravelSplitSwapTestUI inst,
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        int steps,
        float cellSizePx,
        float delaySeconds,
        Vector2Int originCell,
        Action<Vector2Int> onStep)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        if (inst != null)
            inst.Play(axis, originAnchored, originCell, steps, cellSizePx, onStep);
    }

    private IEnumerator PlayLineTravelRoutine(
        LineTravelSplitSwapTestUI inst,
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        int steps,
        float cellSizePx,
        float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        if (inst != null)
            inst.Play(axis, originAnchored, steps, cellSizePx);
    }

    private IEnumerator DestroyAfterUnscaled(GameObject go, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);
        if (go != null) Destroy(go);
    }

    /// <summary>
    /// Canonical data-only clear. Sets Tiles and GridData to null at (x,y).
    /// All tile clearing MUST go through this method.
    /// </summary>
    internal void ClearCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        tiles[x, y] = null;
        gridData[x, y] = null;
    }

    /// <summary>
    /// Canonical clear + destroy. Clears data, accumulates type counts for batch notification,
    /// and destroys the TileView GameObject.
    /// Call NotifyTilesCleared for each entry in clearedByType after the batch.
    /// </summary>
    internal void ClearAndDestroyTile(TileView tile, Dictionary<TileType, int> clearedByType = null)
    {
        if (tile == null) return;
        if (!tile) return; // Unity null check for destroyed objects

        int x = tile.X;
        int y = tile.Y;

        if (x >= 0 && x < width && y >= 0 && y < height && tiles[x, y] == tile)
            ClearCell(x, y);

        tile.SetSpecial(TileSpecial.None);

        if (clearedByType != null)
        {
            var tileType = tile.GetTileType();
            clearedByType.TryGetValue(tileType, out int current);
            clearedByType[tileType] = current + 1;
        }

        Destroy(tile.gameObject);
    }

    internal void ClearCellDataOnly(Vector2Int c)
    {
        int x = c.x;
        int y = c.y;
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (IsMaskHoleCell(x, y)) return;

        if (obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y))
        {
            var hit = ApplyObstacleDamageAt(x, y, ObstacleHitContext.SpecialActivation);
            if (hit.didHit) TriggerObstacleVisualChange(hit.visualChange);
        }

        var t = tiles[x, y];
        if (t == null) return;

        ClearCell(x, y);
    }

    internal void ClearCellVisualOnly(Vector2Int c, TileType type, TileView t)
    {
        if (t != null && t.gameObject != null)
        {
            Destroy(t.gameObject);
            NotifyTilesCleared(type, 1);
        }
    }

    private void ClearCellImmediate(Vector2Int c)
    {
        int x = c.x;
        int y = c.y;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        var t = tiles[x, y];
        if (t == null) return;
        TileType type = t.GetTileType();

        ClearCellDataOnly(c);
        ClearCellVisualOnly(c, type, t);
    }

    private HashSet<Vector2Int> BuildPulseEmitterTargets(int cx, int cy)
    {
        var set = new HashSet<Vector2Int>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= height) continue;
            for (int x = 0; x < width; x++)
                if (!IsMaskHoleCell(x, yy)) set.Add(new Vector2Int(x, yy));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= width) continue;
            for (int y = 0; y < height; y++)
                if (!IsMaskHoleCell(xx, y)) set.Add(new Vector2Int(xx, y));
        }

        return set;
    }

    private Vector2 WorldToAnchoredIn(RectTransform targetParent, Vector3 worldPos)
    {
        if (targetParent == null) return Vector2.zero;

        var canvas = FindFirstObjectByType<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, screen, cam, out Vector2 localPoint);
        return localPoint;
    }
}
