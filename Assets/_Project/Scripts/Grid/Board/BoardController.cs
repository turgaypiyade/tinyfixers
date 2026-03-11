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

    public enum BoosterMode { None, Single, Row, Column, Shuffle }

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

    [Header("Special Chain Tempo")]
    [SerializeField, Range(0.2f, 1.5f)] private float specialChainDurationMultiplier = 0.75f;

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
    [SerializeField] private bool enableSpecialChainTrace;
    [SerializeField] private bool throwOnTileSyncMismatch;
    [SerializeField] private float tilePositionEpsilon = 0.25f;
#endif

    public PatchbotDashUI PatchbotDashUI => patchbotDashUI;
    private Vector2 shakeBasePos;

    private TileView[,] tiles;
    private TileData[,] gridData;
    private bool[,] holes;
    private bool[,] maskHoles;
    private int width, height;
    private TileView selected;
    public BoardState CurrentState { get; private set; } = BoardState.Idle;
    private BoosterMode activeBooster = BoosterMode.None;
    public BoosterMode ActiveBooster => activeBooster;
    private GameObject tilePrefab;
    private RectTransform parent;
    private int tileSize;

    public int TileSize => tileSize;
    public bool IsBusy => CurrentState == BoardState.Resolving;
    public event Action OnBecameIdle;

    [System.Serializable]
    public struct PatchbotDashRequest { public Vector2Int from; public Vector2Int to; }
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
    public event Action<LightningLineStrike, float> OnLineSweepStarted;
    public event Action<Vector2Int, LightningLineStrike> OnLineSweepCellReached;

    private TileView lastSwapA, lastSwapB;
    private bool lastSwapUserMove;
    private bool shakeNextClear;
    private bool isSpecialActivationPhase;
    private TileType[] randomPool;

    private MatchFinder matchFinder;
    private SpecialResolver specialResolver;
    private SpecialBehaviorRegistry specialBehaviorRegistry;
    private BoardAnimator boardAnimator;
    private ActionSequencer actionSequencer;
    private PulseCoreImpactService pulseCoreImpactService;
    private ObstacleStateService obstacleStateService;
    private CascadeLogic cascadeLogic;
    private SpecialCreationService specialCreationService;
    private PendingCreationStore pendingCreationStore;
    private PendingCreationApplicator pendingCreationApplicator;

    // ── Extracted services ──
    private BoardInitService boardInitService;
    private BoardVfxService boardVfxService;
    private LineSweepService lineSweepService;
    private BoosterService boosterService;

    private readonly HashSet<int> patchBotForcedObstacleHits = new();
    private int busyScopeDepth;

    public event Action<ObstacleVisualChange> ObstacleVisualChanged;

    // ── Internal accessors ──
    internal TileView[,] Tiles => tiles;
    internal TileData[,] GridData => gridData;
    internal BoardAnimator boardAnimatorRef => boardAnimator;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal bool EnableSpecialChainTrace => enableSpecialChainTrace;
#else
    internal bool EnableSpecialChainTrace => false;
#endif
    internal ObstacleStateService ObstacleStateService => obstacleStateService;
    internal SpecialBehaviorRegistry SpecialBehaviors => specialBehaviorRegistry;
    public CascadeLogic CascadeLogic => cascadeLogic;
    internal Transform LineTravelSpawnParent => lineTravelSpawnParent;

    // ── Event forwarders for LineSweepService ──
    internal void OnLineSweepStartedInternal(LightningLineStrike strike, float delay) => OnLineSweepStarted?.Invoke(strike, delay);
    internal void OnLineSweepCellReachedInternal(Vector2Int cell, LightningLineStrike strike) => OnLineSweepCellReached?.Invoke(cell, strike);

    // ═══════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (shakeTarget != null) shakeBasePos = shakeTarget.anchoredPosition;
        EnsureServices();
        TryResolveLightningSpawner();
        if (lineTravelSpawnParent == null && lineTravelPlayer != null && lineTravelPlayer.transform.parent != null)
            lineTravelSpawnParent = lineTravelPlayer.transform.parent;
        EnsureGoalFlyFx();

        if (lightningSpawner == null)
            Debug.LogWarning("[Lightning][BoardController] lightningSpawner reference is not assigned and auto-resolve failed.");
    }

    void EnsureServices()
    {
        matchFinder ??= new MatchFinder(this);
        boardAnimator ??= new BoardAnimator(this);
        pulseCoreImpactService ??= new PulseCoreImpactService(this, boardAnimator);
        specialBehaviorRegistry ??= new SpecialBehaviorRegistry();
        specialResolver ??= new SpecialResolver(this, matchFinder, boardAnimator, pulseCoreImpactService);
        specialCreationService ??= new SpecialCreationService(matchFinder);
        pendingCreationStore ??= new PendingCreationStore();
        pendingCreationApplicator ??= new PendingCreationApplicator(this);
        obstacleStateService ??= new ObstacleStateService();
        cascadeLogic ??= new CascadeLogic(this);
        boardInitService ??= new BoardInitService();
        boardVfxService ??= new BoardVfxService(this);
        lineSweepService ??= new LineSweepService(this);
        boosterService ??= new BoosterService(this);

        if (actionSequencer == null)
        {
            actionSequencer = GetComponent<ActionSequencer>();
            if (actionSequencer == null) actionSequencer = gameObject.AddComponent<ActionSequencer>();
            actionSequencer.Initialize(this);
        }
    }

    public void OnActionSequenceFinished() { }

    private void OnDestroy()
    {
        if (obstacleStateService == null) return;
        obstacleStateService.OnObstacleStageChanged -= HandleObstacleStageChanged;
        obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Init / Setup
    // ═══════════════════════════════════════════════════════════════

    public void Init(int width, int height, TileIconLibrary iconLibrary)
    {
        this.width = width; this.height = height; this.iconLibrary = iconLibrary;
        tiles = new TileView[width, height];
        gridData = new TileData[width, height];
        holes = new bool[width, height];
        maskHoles = new bool[width, height];
        EnsureServices();
        if (levelData != null) SetLevelData(levelData);
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

        if (levelData == null) { obstacleStateService = null; return; }

        obstacleStateService ??= new ObstacleStateService();
        obstacleStateService.Initialize(levelData);
        RebuildMaskHoleMap();
        BindObstacleEvents();
    }

    public void SetupFactory(GameObject tilePrefab, RectTransform parent, int tileSize, TileType[] randomPool)
    {
        this.tilePrefab = tilePrefab; this.parent = parent; this.tileSize = tileSize; this.randomPool = randomPool;
        EnsureServices();
    }

    public TileType[,] SimulateInitialTypes() => boardInitService.SimulateInitialTypes(width, height, holes, randomPool);

    // ═══════════════════════════════════════════════════════════════
    //  Busy / State
    // ═══════════════════════════════════════════════════════════════

    internal void BeginBusy() { busyScopeDepth++; CurrentState = BoardState.Resolving; }

    internal void EndBusy()
    {
        if (busyScopeDepth > 0) busyScopeDepth--;
        if (busyScopeDepth == 0 && CurrentState == BoardState.Resolving)
        { CurrentState = BoardState.Idle; OnBecameIdle?.Invoke(); }
    }

    public void SetInputLocked(bool isLocked)
    {
        if (isLocked) CurrentState = BoardState.Locked;
        else if (CurrentState == BoardState.Locked) CurrentState = BoardState.Idle;
    }

    public void ForceFullBoardSync()
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (tiles[x, y] != null)
            {
                tiles[x, y].RefreshIcon();
                if (tiles[x, y].TryGetComponent<CanvasGroup>(out var cg)) cg.alpha = 1f;
                SyncTileData(x, y);
            }
            else if (gridData[x, y] != null) { gridData[x, y] = null; }
        }
    }

    public void RunAfterIdle(Action action)
    {
        if (action == null) return;
        StartCoroutine(RunAfterIdleRoutine(action));
    }

    private IEnumerator RunAfterIdleRoutine(Action action)
    {
        if (!IsBusy) { action(); yield break; }
        bool idle = false;
        void Handler() { OnBecameIdle -= Handler; idle = true; }
        OnBecameIdle += Handler;
        while (!idle) yield return null;
        yield return null;
        action();
    }

    // ═══════════════════════════════════════════════════════════════
    //  VFX Delegation
    // ═══════════════════════════════════════════════════════════════

    public float PlaySystemOverrideComboVfxAndGetDuration() => boardVfxService.PlaySystemOverrideComboVfxAndGetDuration(systemOverrideComboVfx);
    public void PlayPulseEmitterComboVfxAtCell(int x, int y) => boardVfxService.PlayPulseEmitterComboVfxAtCell(pulseEmitterComboVfx, vfxSpace, x, y);
    public void PlayPulsePulseExplosionVfxAtCell(int x, int y) => boardVfxService.PlayPulsePulseExplosionVfxAtCell(pulsePulseExplosionPrefab, vfxSpace, pulsePulseExplosionLifetime, x, y);

    public Vector3 GetTileWorldCenter(TileView tile)
    {
        if (tile == null) return Vector3.zero;
        var rt = tile.GetComponent<RectTransform>();
        if (rt != null) return rt.TransformPoint(rt.rect.center);
        return tile.transform.position;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Lightning / Line Sweep Delegation
    // ═══════════════════════════════════════════════════════════════

    internal float PlayLightningStrikeForTiles(
        IReadOnlyCollection<TileView> matches, TileView originTile = null,
        Vector2Int? fallbackOriginCell = null, IReadOnlyCollection<TileView> visualTargets = null,
        bool allowCondense = true, Action<TileView> onTargetBeamSpawned = null)
    {
        TryResolveLightningSpawner();
        return lineSweepService.PlayLightningStrikeForTiles(lightningSpawner, matches, originTile, fallbackOriginCell, visualTargets, allowCondense, onTargetBeamSpawned);
    }

    internal float PlayLightningLineStrikes(IReadOnlyList<LightningLineStrike> lineStrikes, Action<Vector2Int> onSweepCellReached = null)
    {
        TryResolveLightningSpawner();
        return lineSweepService.PlayLightningLineStrikes(lightningSpawner, lineTravelPlayer, lineStrikes, onSweepCellReached);
    }

    internal float PlayLineTravelInstanceWithStep(
        LineTravelSplitSwapTestUI.LineAxis axis, Vector2 originAnchored, Vector2Int originCell,
        int steps, float cellSizePx, float delaySeconds, Action<Vector2Int> onStep)
    {
        return lineSweepService.PlayLineTravelInstanceWithStep(lineTravelPlayer, axis, originAnchored, originCell, steps, cellSizePx, delaySeconds, onStep);
    }

    internal float GetLightningStrikeStepDelay()
    {
        TryResolveLightningSpawner();
        if (lightningSpawner == null) return 0f;
        return ApplySpecialChainTempo(lightningSpawner.GetStepDelay());
    }

    internal float GetSpecialChainDurationMultiplier() => Mathf.Clamp(specialChainDurationMultiplier, 0.2f, 1.5f);

    internal float ApplySpecialChainTempo(float duration)
    {
        if (duration <= 0f) return 0f;
        if (!isSpecialActivationPhase) return duration;
        return duration * GetSpecialChainDurationMultiplier();
    }

    private void TryResolveLightningSpawner()
    {
        if (lightningSpawner != null) return;
        lightningSpawner = GetComponentInChildren<LightningSpawner>(true);
        if (lightningSpawner == null && transform.parent != null)
            lightningSpawner = transform.parent.GetComponentInChildren<LightningSpawner>(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tile / Data Management
    // ═══════════════════════════════════════════════════════════════

    public void EnqueuePatchbotDash(Vector2Int from, Vector2Int to) => _patchbotDashRequests.Add(new PatchbotDashRequest { from = from, to = to });
    public void ConsumePatchbotDashRequests(List<PatchbotDashRequest> outList) { outList.Clear(); outList.AddRange(_patchbotDashRequests); _patchbotDashRequests.Clear(); }

    public TopHudController TopHud { get { if (topHud == null) topHud = FindFirstObjectByType<TopHudController>(); return topHud; } }
    public GoalFlyFx GoalFlyFx { get { if (goalFlyFx == null) goalFlyFx = FindFirstObjectByType<GoalFlyFx>(); return goalFlyFx; } }

    public void SetHole(int x, int y, bool isHole) => holes[x, y] = isHole;
    public TileView GetTileViewAt(int x, int y) { if (tiles == null || x < 0 || x >= width || y < 0 || y >= height) return null; return tiles[x, y]; }
    public Sprite GetIcon(TileType type) => iconLibrary != null ? iconLibrary.Get(type) : null;
    public Sprite GetSpecialIcon(TileSpecial special) => iconLibrary != null ? iconLibrary.GetSpecialIcon(special) : null;

    public void RegisterTile(TileView tile, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        tiles[x, y] = tile;
        tile.Init(this, x, y);
        tile.SetCoords(x, y);
        tile.SnapToGrid(tileSize);
        SyncTileData(x, y);
    }

    public void SyncTileData(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        var tile = tiles[x, y];
        if (tile == null) { gridData[x, y] = null; return; }
        if (gridData[x, y] == null) gridData[x, y] = new TileData(x, y, tile.GetTileType());
        var data = gridData[x, y];
        data.SetCoords(x, y);
        data.SetType(tile.GetTileType());
        data.SetSpecial(tile.GetSpecial());
        if (tile.GetSpecial() == TileSpecial.SystemOverride && tile.GetOverrideBaseType(out var baseType))
            data.SetOverrideBaseType(baseType);
    }

    internal void RefreshTileObstacleVisual(TileView tile) { if (tile != null) tile.SetIconAlpha(1f); }

    internal void RefreshAllTileObstacleVisuals()
    {
        if (tiles == null) return;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) RefreshTileObstacleVisual(tiles[x, y]);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Cell Clear
    // ═══════════════════════════════════════════════════════════════

    internal void ClearCell(int x, int y) { if (x < 0 || x >= width || y < 0 || y >= height) return; tiles[x, y] = null; gridData[x, y] = null; }

    internal void ClearAndDestroyTile(TileView tile, Dictionary<TileType, int> clearedByType = null)
    {
        if (tile == null || !tile) return;
        int x = tile.X, y = tile.Y;
        if (x >= 0 && x < width && y >= 0 && y < height && tiles[x, y] == tile) ClearCell(x, y);
        tile.SetSpecial(TileSpecial.None);
        if (clearedByType != null) { var tt = tile.GetTileType(); clearedByType.TryGetValue(tt, out int c); clearedByType[tt] = c + 1; }
        Destroy(tile.gameObject);
    }

    internal void ClearCellDataOnly(Vector2Int c)
    {
        int x = c.x, y = c.y;
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (IsMaskHoleCell(x, y)) return;
        if (obstacleStateService != null && obstacleStateService.HasObstacleAt(x, y))
        {
            var hit = ApplyObstacleDamageAt(x, y, ObstacleHitContext.SpecialActivation);
            if (hit.didHit) TriggerObstacleVisualChange(hit.visualChange);
        }
        var t = tiles[x, y]; if (t == null) return; ClearCell(x, y);
    }

    internal void ClearCellVisualOnly(Vector2Int c, TileType type, TileView t)
    { if (t != null && t.gameObject != null) { Destroy(t.gameObject); NotifyTilesCleared(type, 1); } }

    // ═══════════════════════════════════════════════════════════════
    //  Input / Click / Drag
    // ═══════════════════════════════════════════════════════════════

    public void RequestSwapFromDrag(TileView from, int dirX, int dirY)
    {
        if (IsBusy || InputLocked) return;
        if (activeBooster != BoosterMode.None) return;
        int nx = from.X + dirX, ny = from.Y + dirY;
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
        if (holes[nx, ny] && (obstacleStateService == null || !obstacleStateService.HasObstacleAt(nx, ny))) return;
        TileView other = tiles[nx, ny]; if (other == null) return;
        StartCoroutine(ProcessSwap(from, other));
    }

    public void OnTileClicked(TileView tile)
    {
        if (IsBusy || InputLocked) return;
        if (TryUseBooster(tile)) return;
        if (selected == null) { selected = tile; return; }
        if (selected == tile) { selected = null; return; }
        if (AreNeighbors(selected, tile)) { var a = selected; selected = null; StartCoroutine(ProcessSwap(a, tile)); return; }
        selected = tile;
    }

    bool AreNeighbors(TileView a, TileView b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) == 1;

    // ═══════════════════════════════════════════════════════════════
    //  Booster Delegation
    // ═══════════════════════════════════════════════════════════════

    public void ActivateBooster(int idx)
    {
        switch (idx) { case 0: SetBoosterMode(BoosterMode.Single); break; case 1: SetBoosterMode(BoosterMode.Row); break;
            case 2: SetBoosterMode(BoosterMode.Column); break; case 3: SetBoosterMode(BoosterMode.Shuffle); break;
            default: SetBoosterMode(BoosterMode.None); break; }
    }

    void SetBoosterMode(BoosterMode mode) { activeBooster = mode; OnBoosterTargetingChanged?.Invoke(activeBooster != BoosterMode.None); }

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
        var mode = activeBooster; SetBoosterMode(BoosterMode.None); selected = null;
        var targetCell = new Vector2Int(x, y); var targetTile = tiles[x, y];

        if (mode == BoosterMode.Shuffle)
            StartCoroutine(boosterService.ShuffleBoardRoutine(actionSequencer));
        else
            StartCoroutine(boosterService.ApplyBoosterRoutine(mode, targetTile, targetCell,
                specialResolver, actionSequencer, cascadeLogic, lineSweepService, lightningSpawner, lineTravelPlayer));
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ProcessSwap
    // ═══════════════════════════════════════════════════════════════

    IEnumerator ProcessSwap(TileView a, TileView b)
    {
        BeginBusy();
        lastSwapA = a; lastSwapB = b; lastSwapUserMove = true;
        SyncAllTilesToGridData();

        int ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
        tiles[ax, ay] = b; tiles[bx, by] = a;
        a.SetCoords(bx, by); b.SetCoords(ax, ay);
        SyncTileData(ax, ay); SyncTileData(bx, by);

        actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
        yield return AnimateQueuedActions();

        TileSpecial sa = a.GetSpecial(), sb = b.GetSpecial();

        // ── Special swap path ──
        if (sa != TileSpecial.None || sb != TileSpecial.None)
        {
            pendingCreationStore.Clear();
            var specialSwapMatches = CollectMatchedTilesForSwap(a, b);
            var pendingCreation = specialCreationService.DecideFromMatches(specialSwapMatches, new SpecialCreationService.CreationRequest(a, b, true));
            if (pendingCreation.hasValue) pendingCreationStore.Store(pendingCreation.winner.X, pendingCreation.winner.Y, pendingCreation.special);
            ConsumeMove();

            bool bothSpecial = sa != TileSpecial.None && sb != TileSpecial.None;
            actionSequencer.Enqueue(specialResolver.ResolveSpecialSwap(a, b));
            yield return AnimateQueuedActions();

            if (!bothSpecial && pendingCreationStore.HasPending)
            {
                var pendingItems = pendingCreationStore.Drain();
                pendingCreationApplicator.ApplyAll(pendingItems);
                for (int pi = 0; pi < pendingItems.Count; pi++)
                {
                    var pending = pendingItems[pi];
                    if (pending.x < 0 || pending.x >= width || pending.y < 0 || pending.y >= height) continue;
                    var createdTile = tiles[pending.x, pending.y]; if (createdTile == null) continue;
                    var surroundingMatches = matchFinder.FindMatchesAt(pending.x, pending.y);
                    surroundingMatches.Remove(createdTile);
                    surroundingMatches.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);
                    if (surroundingMatches.Count > 0) { actionSequencer.Enqueue(new MatchClearAction(surroundingMatches, doShake: false)); yield return AnimateQueuedActions(); }
                }
            }
            else { pendingCreationStore.Clear(); }

            yield return ResolveEmptyPlayableCellsWithoutMatch();
            yield return ResolveBoard(allowSpecial: false);
            EndBusy();
            yield break;
        }

        // ── Normal swap path ──
        var matches = new HashSet<TileView>();
        foreach (var t in matchFinder.FindMatchesAt(a.X, a.Y)) matches.Add(t);
        foreach (var t in matchFinder.FindMatchesAt(b.X, b.Y)) matches.Add(t);

        if (matches.Count == 0)
        {
            tiles[ax, ay] = a; tiles[bx, by] = b;
            a.SetCoords(ax, ay); b.SetCoords(bx, by);
            SyncTileData(ax, ay); SyncTileData(bx, by);
            actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
            yield return AnimateQueuedActions();
            EndBusy(); yield break;
        }

        ConsumeMove();
        yield return ExecuteClearPass(matches, allowSpecial: true);
        yield return ResolveEmptyPlayableCellsWithoutMatch();
        yield return ResolveBoard();
        EndBusy();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Resolve Board
    // ═══════════════════════════════════════════════════════════════

    IEnumerator ResolveBoard(bool allowSpecial = true)
    {
        isSpecialActivationPhase = false;
        int safety = 0;
        const int MaxResolveLoops = 25;
        while (true)
        {
            safety++;
            if (safety > MaxResolveLoops) yield break;
            CurrentResolvePass = safety;
            var matches = matchFinder.FindAllMatches();
            if (matches.Count == 0) yield break;
            var matchTiles = new HashSet<TileView>();
            foreach (var t in matches) { var tile = tiles[t.X, t.Y]; if (tile != null) matchTiles.Add(tile); }
            if (matchTiles.Count == 0) yield break;
            bool cleared = false;
            yield return ExecuteClearPass(matchTiles, allowSpecial, result => cleared = result);
            if (!cleared) yield break;
        }
    }

    // Public wrapper for services (BoosterService)
    internal IEnumerator ResolveBoardPublic(bool allowSpecial = true) => ResolveBoard(allowSpecial);

    IEnumerator ExecuteClearPass(HashSet<TileView> matchTiles, bool allowSpecial, Action<bool> onResult = null)
    {
        var nonSpecialMatchTiles = new HashSet<TileView>(matchTiles);
        nonSpecialMatchTiles.RemoveWhere(t => t != null && t.GetSpecial() != TileSpecial.None);

        if (allowSpecial)
        {
            var creation = specialCreationService.DecideFromMatches(nonSpecialMatchTiles, new SpecialCreationService.CreationRequest(lastSwapA, lastSwapB, LastSwapUserMove));
            if (creation.hasValue)
            {
                var created = specialResolver.ApplyCreatedSpecial(creation.winner, creation.special);
                if (created != null)
                {
                    matchTiles.Remove(created);
                    shakeNextClear = true;
                    if (creation.special == TileSpecial.PatchBot)
                    {
                        var fullGroup = matchFinder.FindMatchesAt(created.X, created.Y);
                        foreach (var pt in fullGroup)
                        { if (pt == null || pt == created || pt.GetSpecial() != TileSpecial.None) continue; matchTiles.Add(pt); }
                    }
                }
            }
            LastSwapUserMove = false;
        }

        specialResolver.ExpandSpecialChain(matchTiles, null, out bool hasLineActivation, out _);
        if (matchTiles.Count == 0) { onResult?.Invoke(false); yield break; }

        bool doShake = shakeNextClear || hasLineActivation;
        shakeNextClear = false;
        actionSequencer.Enqueue(new MatchClearAction(matchTiles, doShake));
        while (actionSequencer.IsPlaying) yield return null;

        var cascadeActions = cascadeLogic.CalculateCascades();
        if (cascadeActions.Count > 0)
        { actionSequencer.Enqueue(cascadeActions); while (actionSequencer.IsPlaying) yield return null; }
        onResult?.Invoke(true);
    }

    public IEnumerator ResolveInitial() { BeginBusy(); yield return ResolveBoard(false); EndBusy(); }

    internal IEnumerator ResolveEmptyPlayableCellsWithoutMatch()
    {
        var cascades = cascadeLogic.CalculateCascades();
        if (cascades.Count > 0) { actionSequencer.Enqueue(cascades); while (actionSequencer.IsPlaying) yield return null; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Obstacle Handling
    // ═══════════════════════════════════════════════════════════════

    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamageAt(int x, int y, ObstacleHitContext context)
    {
        if (obstacleStateService == null) return default;
        bool patchBotForcedHit = ConsumePatchBotForcedObstacleHit(x, y);
        var result = obstacleStateService.TryDamageAt(x, y, context);

        ObstacleStateService.ObstacleHitResult TryFallback(ObstacleHitContext fb)
        { return fb == context ? default : obstacleStateService.TryDamageAt(x, y, fb); }

        if (!result.didHit && context == ObstacleHitContext.Booster)
        { result = TryFallback(ObstacleHitContext.SpecialActivation); if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch); }
        else if (!result.didHit && patchBotForcedHit)
        { result = TryFallback(ObstacleHitContext.SpecialActivation); if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster); if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch); if (!result.didHit) result = TryFallback(ObstacleHitContext.Scripted); }
        else if (!result.didHit && IsCrossContextFallbackAllowedAt(x, y))
        { result = TryFallback(ObstacleHitContext.SpecialActivation); if (!result.didHit) result = TryFallback(ObstacleHitContext.Booster); if (!result.didHit) result = TryFallback(ObstacleHitContext.NormalMatch); }

        if (!result.didHit) return result;
        ConsumeObstacleStageTransition(result);
        return result;
    }

    public void TriggerObstacleVisualChange(ObstacleVisualChange change) => ObstacleVisualChanged?.Invoke(change);
    internal void MarkPatchBotForcedObstacleHit(int x, int y) { if (obstacleStateService == null || !obstacleStateService.HasObstacleAt(x, y) || x < 0 || x >= width || y < 0 || y >= height) return; patchBotForcedObstacleHits.Add(y * width + x); }
    private bool ConsumePatchBotForcedObstacleHit(int x, int y) { if (x < 0 || x >= width || y < 0 || y >= height) return false; int idx = y * width + x; return patchBotForcedObstacleHits.Remove(idx); }

    private void ConsumeObstacleStageTransition(ObstacleStateService.ObstacleHitResult result)
    {
        if (!result.stageTransition.hasTransition) return;
        if (!result.stageTransition.cleared) OnObstacleStageChanged?.Invoke(result.stageTransition.originIndex, result.stageTransition.currentStage);
        var affected = result.affectedCellIndices;
        if (affected == null || affected.Length == 0) return;
        for (int i = 0; i < affected.Length; i++)
        { int idx = affected[i]; int x = idx % width, y = idx / width; if (x < 0 || x >= width || y < 0 || y >= height) continue; holes[x, y] = IsMaskHole(x, y) || (obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y)); }
    }

    private bool IsCrossContextFallbackAllowedAt(int x, int y)
    {
        if (levelData == null || levelData.obstacles == null || !levelData.InBounds(x, y)) return false;
        int idx = levelData.Index(x, y); if (idx < 0 || idx >= levelData.obstacles.Length) return false;
        var obstacleId = (ObstacleId)levelData.obstacles[idx]; if (obstacleId == ObstacleId.None) return false;
        var def = levelData.obstacleLibrary != null ? levelData.obstacleLibrary.Get(obstacleId) : null;
        return def != null && def.allowCrossContextFallback;
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
        int x = cellIndex % width, y = cellIndex / width;
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        holes[x, y] = IsMaskHole(x, y) || (obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y));
        if (!holes[x, y]) OnCellUnlocked?.Invoke(cellIndex);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Mask / Hole Helpers
    // ═══════════════════════════════════════════════════════════════

    private void RebuildMaskHoleMap()
    {
        if (maskHoles == null || maskHoles.GetLength(0) != width || maskHoles.GetLength(1) != height)
            maskHoles = new bool[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool isMH = false;
            if (levelData != null && levelData.cells != null)
            { int idx = levelData.Index(x, y); if (idx >= 0 && idx < levelData.cells.Length) isMH = levelData.cells[idx] == (int)CellType.Empty; }
            maskHoles[x, y] = isMH;
        }
    }

    private bool IsMaskHole(int x, int y) { if (maskHoles == null || x < 0 || x >= width || y < 0 || y >= height) return false; return maskHoles[x, y]; }
    internal bool IsMaskHoleCell(int x, int y) => IsMaskHole(x, y);
    internal bool IsObstacleBlockedCell(int x, int y) => x >= 0 && x < width && y >= 0 && y < height && obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
    internal bool IsSpawnPassThroughCell(int x, int y) => IsMaskHoleCell(x, y) && !IsObstacleBlockedCell(x, y);
    public bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state) => BoardCellStateQuery.TryGet(this, x, y, out state);
    public bool HasAnyEmptyPlayableCell() { for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { if (holes[x, y]) continue; if (tiles[x, y] == null) return true; } return false; }

    // ═══════════════════════════════════════════════════════════════
    //  Timing / Utility
    // ═══════════════════════════════════════════════════════════════

    internal float GetFallDurationForDistance(int cellDistance)
    {
        float baseDur = FallDurationWithMultiplier;
        int d = Mathf.Max(1, cellDistance);
        float distDur = Mathf.Clamp(d * 0.06f, 0.06f, 0.22f);
        return Mathf.Max(0.04f, Mathf.Min(baseDur, distDur) * Mathf.Max(0.25f, GetCascadeFallSpeedMultiplier()));
    }

    internal float GetClearDurationForCurrentPass() => Mathf.Max(0.03f, ApplySpecialChainTempo(ClearDuration * GetCascadeClearSpeedMultiplier()));
    internal bool ShouldEnableFallSettleThisPass() => EnableFallSettle && CurrentResolvePass <= 1;
    private float GetCascadeFallSpeedMultiplier() => (CurrentResolvePass <= 1) ? 1f : 0.75f;
    private float GetCascadeClearSpeedMultiplier() => (CurrentResolvePass <= 1) ? 1f : 0.85f;
    private TileType GetRandomType() => randomPool[UnityEngine.Random.Range(0, randomPool.Length)];
    private void ConsumeMove() { RemainingMoves = Mathf.Max(0, RemainingMoves - 1); OnMovesChanged?.Invoke(RemainingMoves); }
    public void AddMoves(int amount) { if (amount <= 0) return; RemainingMoves += amount; OnMovesChanged?.Invoke(RemainingMoves); }
    internal void NotifyTilesCleared(TileType tileType, int amount) { if (amount > 0) OnTilesCleared?.Invoke(tileType, amount); }

    private void SyncAllTilesToGridData() { for (int sy = 0; sy < height; sy++) for (int sx = 0; sx < width; sx++) if (tiles[sx, sy] != null) SyncTileData(sx, sy); }
    private IEnumerator AnimateQueuedActions() { while (actionSequencer.IsPlaying) yield return null; }

    public Vector3 GetCellWorldPosition(int x, int y)
    {
        if (parent != null) return parent.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
        return transform.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
    }

    internal Vector2 WorldToAnchoredIn(RectTransform targetParent, Vector3 worldPos)
    {
        if (targetParent == null) return Vector2.zero;
        var canvas = FindFirstObjectByType<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetParent, RectTransformUtility.WorldToScreenPoint(cam, worldPos), cam, out Vector2 localPoint);
        return localPoint;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PulseEmitter Combo Action Factory
    // ═══════════════════════════════════════════════════════════════

    public PulseLineComboAction CreatePulseEmitterComboAction(int cx, int cy)
    {
        PulseBehaviorEvents.EmitPulseEmitterComboTriggered(new Vector2Int(cx, cy));
        var targets = boardVfxService.BuildPulseEmitterTargets(cx, cy);

        RectTransform space = null;
        if (lineTravelPlayer != null)
            space = lineTravelPlayer.afterImageParent != null ? lineTravelPlayer.afterImageParent : (lineTravelSpawnParent as RectTransform);

        var hOrigins = new List<(Vector2Int cell, Vector2 anch)>();
        var vOrigins = new List<(Vector2Int cell, Vector2 anch)>();

        for (int yy = cy - 1; yy <= cy + 1; yy++)
        {
            if (yy < 0 || yy >= height) continue;
            var originTile = tiles[cx, yy]; if (originTile == null) continue;
            var rt = originTile.GetComponent<RectTransform>();
            Vector3 wc = rt.TransformPoint(new Vector3(tileSize * 0.5f, -tileSize * 0.5f, 0f));
            hOrigins.Add((new Vector2Int(cx, yy), WorldToAnchoredIn(space, wc)));
        }

        for (int xx = cx - 1; xx <= cx + 1; xx++)
        {
            if (xx < 0 || xx >= width) continue;
            var originTile = tiles[xx, cy]; if (originTile == null) continue;
            var rt = originTile.GetComponent<RectTransform>();
            Vector3 wc = rt.TransformPoint(new Vector3(tileSize * 0.5f, -tileSize * 0.5f, 0f));
            vOrigins.Add((new Vector2Int(xx, cy), WorldToAnchoredIn(space, wc)));
        }

        var targetVisuals = new Dictionary<Vector2Int, (TileType, TileView)>();
        foreach (var c in targets) { var t = tiles[c.x, c.y]; if (t != null) targetVisuals[c] = (t.GetTileType(), t); }
        foreach (var c in targets) ClearCellDataOnly(c);
        return new PulseLineComboAction(this, cx, cy, targets, hOrigins, vOrigins, targetVisuals);
    }

    private HashSet<TileView> CollectMatchedTilesForSwap(TileView a, TileView b)
    {
        var result = new HashSet<TileView>();
        if (a != null) foreach (var data in matchFinder.FindMatchesAt(a.X, a.Y)) { var tile = tiles[data.X, data.Y]; if (tile != null) result.Add(tile); }
        if (b != null) foreach (var data in matchFinder.FindMatchesAt(b.X, b.Y)) { var tile = tiles[data.X, data.Y]; if (tile != null) result.Add(tile); }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GoalFlyFx Setup
    // ═══════════════════════════════════════════════════════════════

    private void EnsureGoalFlyFx()
    {
        var canvas = FindFirstObjectByType<Canvas>(); if (canvas == null) return;
        var overlay = canvas.transform.Find("GoalFlyOverlayRoot") as RectTransform;
        if (overlay == null)
        {
            var go = new GameObject("GoalFlyOverlayRoot", typeof(RectTransform));
            overlay = go.GetComponent<RectTransform>(); overlay.SetParent(canvas.transform, false);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero; overlay.localScale = Vector3.one;
        }
        overlay.SetAsLastSibling();

        if (goalFlyFx == null)
        {
            var fxTr = canvas.transform.Find("GoalFlyFx");
            if (fxTr != null) goalFlyFx = fxTr.GetComponent<GoalFlyFx>();
            if (goalFlyFx == null) { var fxGo = new GameObject("GoalFlyFx", typeof(RectTransform), typeof(GoalFlyFx)); fxGo.transform.SetParent(canvas.transform, false); goalFlyFx = fxGo.GetComponent<GoalFlyFx>(); }
        }
        if (goalFlyFx != null) goalFlyFx.gameObject.SendMessage("SetOverlayRoot", overlay, SendMessageOptions.DontRequireReceiver);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ValidateTileSync(string context, int onlyX = -1, int onlyY = -1, bool checkVisualPosition = true)
    {
        if (!enableTileSyncValidation || tiles == null || gridData == null) return;
        int startX = (onlyX >= 0) ? onlyX : 0, endX = (onlyX >= 0) ? onlyX : width - 1;
        int startY = (onlyY >= 0) ? onlyY : 0, endY = (onlyY >= 0) ? onlyY : height - 1;
        bool mismatch = false; var sb = new System.Text.StringBuilder();
        for (int y = startY; y <= endY; y++) for (int x = startX; x <= endX; x++)
        {
            var tile = tiles[x, y]; var data = gridData[x, y];
            if (tile == null && data != null) { mismatch = true; sb.AppendLine($"[{context}] Data var ama view yok @ ({x},{y})"); continue; }
            if (tile != null && data == null) { mismatch = true; sb.AppendLine($"[{context}] View var ama data yok @ ({x},{y})"); continue; }
            if (tile == null) continue;
            if (tile.X != x || tile.Y != y) { mismatch = true; sb.AppendLine($"[{context}] TileView koordinat sapması @ ({x},{y}) viewXY=({tile.X},{tile.Y})"); }
            if (data.X != x || data.Y != y) { mismatch = true; sb.AppendLine($"[{context}] TileData koordinat sapması @ ({x},{y})"); }
            if (data.Type != tile.GetTileType() || data.Special != tile.GetSpecial()) { mismatch = true; sb.AppendLine($"[{context}] Type/Special mismatch @ ({x},{y})"); }
            if (checkVisualPosition) { var rt = tile.GetComponent<RectTransform>(); if (rt != null) { Vector2 exp = new Vector2(x * tileSize, -y * tileSize); if (Vector2.Distance(rt.anchoredPosition, exp) > tilePositionEpsilon) { mismatch = true; sb.AppendLine($"[{context}] Pozisyon sapması @ ({x},{y})"); } } }
        }
        if (!mismatch) return;
        string log = $"[TileSyncValidation] {context}\n{sb}";
        if (throwOnTileSyncMismatch) throw new InvalidOperationException(log);
        Debug.LogError(log, this);
    }
#else
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ValidateTileSync(string context, int onlyX = -1, int onlyY = -1, bool checkVisualPosition = true) { }
#endif
}