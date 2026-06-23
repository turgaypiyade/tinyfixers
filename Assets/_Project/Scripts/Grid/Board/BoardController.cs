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
    [Tooltip("Yeni taşların gridin üst kenarının KAÇ hücre üstünden akmaya başlayacağı (besleme boşluğu). " +
             "Sabit hızda büyük bir mesafe, mevcut taşlar oturduktan sonra spawn'ın geç gelmesine ve " +
             "arada şişen boşluğa yol açar. 0 = üst kenara bitişik (en sıkı, su gibi akış), " +
             "1 = hafif nefes payı, 2+ = belirgin boşluk. Eski negatif 'spawnStartOffsetY'nin yerine geçer.")]
    [SerializeField, Range(0, 4)] private int spawnFeedGap = 1;

    [Header("Movement Feel")]
    [SerializeField] private float swapDurationMultiplier = 1f;
    [SerializeField] private float fallColumnStep = 0.015f;
    [SerializeField] private float fallDurationMultiplier = 1f;
    [Tooltip("Cell/second cinsinden tek gerçek düşüş hızı. Dikey ve diyagonal tüm fall/slide süreleri bundan türetilir.")]
    [SerializeField] private float fallVelocityCellsPerSecond = 30f;
    [Tooltip("Bir cascade simülasyonunda her tile'ın kaç kez diyagonal kayma yapabileceği. 1=klasik (daha fazla cascade round), 2-3=daha akıcı akış.")]
    [SerializeField, Range(1, 4)] private int maxDiagonalSlidesPerCascade = 2;
    [Tooltip("Yukarıdan spawn olan taşların birbirine göre EK zaman gecikmesi (stagger) çarpanı. " +
             "Görsel pozisyon offset'i taşları zaten tam 1 hücre arayla dizdiği için bu ek gecikme " +
             "kolonu esnetir ve 'geç giren / yalpalayan' his yaratır. 0 = su gibi sıkı rigid akış, " +
             "1 = eski (esnek) davranış.")]
    [SerializeField, Range(0f, 1f)] private float fallSpawnStaggerMultiplier = 0.15f;
    [SerializeField] private AnimationCurve swapMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fallMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall Settle")]
    [SerializeField] private bool enableFallSettle = true;
    [SerializeField] private float fallSettleDuration = 0.22f;
    [Tooltip("Min scaleY = 1 - strength. 0.46 → 0.54 (video ölçümü). Üstü 0.9 ile capped.")]
    [SerializeField, Range(0f, 0.9f)] private float fallSettleStrength = 0.46f;
    [Tooltip("Çarpma anında X genişleme oranı. 0 = sabit, 0.2 = %20 geniş (jelly his). Volume-preserving değil.")]
    [SerializeField, Range(0f, 0.6f)] private float fallSettleStretchX = 0.20f;
    [Tooltip("Çarpma anında hedefin altına inme oranı (hücre boyuna göre). 0.12 = hücre yüksekliğinin %12'si kadar aşağı taşar.")]
    [SerializeField, Range(0f, 0.4f)] private float fallSettleOvershoot = 0.12f;
    internal float FallColumnStep => Mathf.Max(0f, fallColumnStep);
    internal int MaxDiagonalSlidesPerCascade => Mathf.Max(1, maxDiagonalSlidesPerCascade);

    [Header("Juice (Only 4+ / Power)")]
    [SerializeField] private float preClearDelay = 0.06f;
    [SerializeField] private float shakeDuration = 0.10f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private RectTransform shakeTarget;

    [Header("Special Combos")]
    [SerializeField] private int patchBotPulseComboSize = 4;


    [Header("PatchBot Combo Ghost Tuning")]
    [SerializeField] private PatchBotPairGhostTuning patchBotPairGhostTuning = PatchBotPairGhostTuning.Default;

    [Header("Special Chain Tempo")]
    [SerializeField, Range(0.2f, 1.5f)] private float specialChainDurationMultiplier = 0.4f;

    [Header("PulseCore Impact (premium stagger)")]
    [SerializeField] private float pulseImpactDelayStep = 0.02f;
    [SerializeField] private float pulseImpactAnimTime = 0.16f;

    [Tooltip("Zincirli PulseCore patlamalarında, bir patlamanın düşüş animasyonunun ne " +
             "kadarı geçince sıradaki patlamanın tetikleneceği (0..1). Yüksek = daha geç " +
             "(taşlar oturmaya yakın), düşük = erken (havada yakalama belirgin).")]
    [SerializeField, Range(0f, 1f)] private float pulseChainCatchOverlap = 0.4f;

    [Tooltip("Override+PulseCore zincirinde iki patlama arası MINIMUM bekleme (sn). Düşüş " +
             "süresinden bağımsız taban ritim — düz düşüşte zincirin çok hızlanmasını önler. " +
             "Büyük = daha yavaş/belirgin zincir.")]
    [SerializeField, Range(0f, 0.5f)] private float overridePulseChainStagger = 0.12f;

    [Header("Board VFX/SFX")]
    [FormerlySerializedAs("pulseCoreVfxPlayer")][SerializeField] private PulseCoreVfxPlayer boardVfxPlayer;
    [SerializeField] private LightningSpawner lightningSpawner;
    public LineTravelSplitSwapTestUI lineTravelPlayer;
    [SerializeField] private Transform lineTravelSpawnParent;
    [SerializeField] private BoardAudioDirector audioDirector;

    [Header("HUD / Goal Fly FX")]
    [SerializeField] private TopHudController topHud;
    [SerializeField] private GoalFlyFx goalFlyFx;

    [Header("Combo VFX")]
    [SerializeField] private OverrideComboController systemOverrideComboVfx;
    [SerializeField] private PulseEmitterComboController pulseEmitterComboVfx;
    [SerializeField] private RectTransform vfxSpace;
    [SerializeField] private GameObject pulsePulseExplosionPrefab;
    [SerializeField] private float pulsePulseExplosionLifetime = 1.0f;
    [SerializeField] private float pulsePulseChargeDuration = 2.0f;
    [Header("Obstacle Visual Tuning")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxPulseCoreBoom;
    [SerializeField] private AudioClip sfxPulseCoreWave;
    [SerializeField] private bool enablePulseMicroShake;
    [SerializeField] private float pulseMicroShakeDuration = 0.08f;
    [SerializeField] private float pulseMicroShakeStrength = 4f;
    [SerializeField] private PatchbotDashUI patchbotDashUI;

    [SerializeField] private AudioClip sfxTileFall;
    [SerializeField, Range(0f, 1f)] private float sfxTileFallVolume = 0.32f;
    [SerializeField] private float sfxTileFallMinInterval = 0.18f;

    private float lastTileFallSfxTime = -999f;

    [Header("Break FX")]
    [SerializeField] private GameObject tileBreakFxPrefab;
    [SerializeField] private float tileBreakFxLifetime = 0.35f;
    [SerializeField] private GameObject obstacleHitFxPrefab;
    [SerializeField] private float obstacleHitFxLifetime = 0.30f;
    [SerializeField] private GameObject obstacleBreakFxPrefab;
    [SerializeField] private float obstacleBreakFxLifetime = 0.40f;
    [Header("Booster FX")]
    [SerializeField] private RectTransform hammerBoosterFxPrefab;
    [SerializeField] private RectTransform cannonBoosterFxPrefab;
    [SerializeField] private RectTransform verticalBoosterFxPrefab;
    [SerializeField] private RectTransform boosterFxParent;
    [SerializeField] private Sprite hammerBoosterFallbackSprite;
    [SerializeField] private Sprite patchBotPropellerSprite;
    [SerializeField] private Sprite patchBotPropellerHubSprite;

    internal RectTransform HammerBoosterFxPrefab => hammerBoosterFxPrefab;
    internal RectTransform CannonBoosterFxPrefab => cannonBoosterFxPrefab;
    internal RectTransform VerticalBoosterFxPrefab => verticalBoosterFxPrefab;
    internal RectTransform BoosterFxParent => boosterFxParent != null ? boosterFxParent : parent;
    internal Sprite HammerBoosterFallbackSprite => hammerBoosterFallbackSprite;
    internal Sprite PatchBotPropellerSprite => patchBotPropellerSprite;
    internal Sprite PatchBotPropellerHubSprite => patchBotPropellerHubSprite;

    [Header("Special Tile Visual")]
    [SerializeField] private bool specialFillCell = false;
    [SerializeField, Range(-0.4f, 0.4f)] private float specialElevation = 0f;

    internal bool SpecialFillCell => specialFillCell;
    internal float SpecialElevation => specialElevation;

    [SerializeField] private bool allowPostSwapSettleValidation = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug / Tile Sync")]
    [SerializeField] private bool enableTileSyncValidation = true;
    [SerializeField] private bool enableSpecialChainTrace;
    [SerializeField] private bool throwOnTileSyncMismatch;
    [SerializeField] private float tilePositionEpsilon = 0.25f;
#endif

    public PatchbotDashUI PatchbotDashUI => patchbotDashUI;
    public BoardAudioDirector Audio => audioDirector;
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
    private float tileIconScale = 0.98f;
    private Vector2 tileIconSize = new Vector2(100f, 100f);
    private bool useFullCellIcons;
    private BoardBreakFxService boardBreakFxService;

    public int TileSize => tileSize;
    public RectTransform TilesRoot => parent;
    public bool IsBusy => CurrentState == BoardState.Resolving;
    public event Action OnBecameIdle;

    // BossDuel/Battlefield: oyuncu HP'si 0'a düşünce gibi, hamle/goal'den bağımsız
    // kesin kayıp tetiklenmesi için. LevelEndSimplePopupController dinler.
    public event Action OnLevelFailRequested;
    public void RequestLevelFail() => OnLevelFailRequested?.Invoke();

    internal void PlayTileFallSfx(int tileCount, int maxDist)
    {
        if (audioDirector != null)
        {
            audioDirector.ScheduleFallBatch(tileCount, maxDist, CurrentResolvePass);
            return;
        }

        if (!GameSettings.SoundEnabled)
            return;

        if (sfxSource == null || sfxTileFall == null)
            return;

        if (Time.time - lastTileFallSfxTime < sfxTileFallMinInterval)
            return;

        // Tek kliple hafif yoğunluk hissi ver
        float volumeMul = 1f;

        if (tileCount >= 6 || maxDist >= 4)
            volumeMul = 1.15f;
        else if (tileCount >= 3 || maxDist >= 2)
            volumeMul = 1.05f;

        sfxSource.PlayOneShot(sfxTileFall, sfxTileFallVolume * volumeMul);
        lastTileFallSfxTime = Time.time;
    }

    [System.Serializable]
    public struct PatchBotPairGhostTuning
    {
        [SerializeField] private float duration;
        [SerializeField] private float startRadiusFactor;
        [SerializeField] private float endRadiusFactor;
        [SerializeField] private float spinDegrees;
        [SerializeField] private float riseFactor;
        [SerializeField] private float travelStartRadiusFactor;
        [SerializeField] private float travelEndRadiusFactor;
        [SerializeField] private float travelSpinSpeed;
        [SerializeField] private float travelSpinFrequency;
        [SerializeField] private AnimationCurve fadeCurve;
        [SerializeField] private AnimationCurve travelApproachCurve;

        public float Duration => Mathf.Max(0.05f, duration);
        public float StartRadiusFactor => Mathf.Max(0f, startRadiusFactor);
        public float EndRadiusFactor => Mathf.Max(0f, endRadiusFactor);
        public float SpinDegrees => spinDegrees;
        public float RiseFactor => Mathf.Max(0f, riseFactor);
        public float TravelStartRadiusFactor => Mathf.Max(0f, travelStartRadiusFactor);
        public float TravelEndRadiusFactor => Mathf.Max(0f, travelEndRadiusFactor);
        public float TravelSpinSpeed => travelSpinSpeed;
        public float TravelSpinFrequency => Mathf.Max(0f, travelSpinFrequency);
        public AnimationCurve FadeCurve => fadeCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public AnimationCurve TravelApproachCurve => travelApproachCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public PatchBotPairGhostTuning Sanitized => new PatchBotPairGhostTuning
        {
            duration = Duration,
            startRadiusFactor = StartRadiusFactor,
            endRadiusFactor = EndRadiusFactor,
            spinDegrees = SpinDegrees,
            riseFactor = RiseFactor,
            travelStartRadiusFactor = TravelStartRadiusFactor,
            travelEndRadiusFactor = TravelEndRadiusFactor,
            travelSpinSpeed = TravelSpinSpeed,
            travelSpinFrequency = TravelSpinFrequency,
            fadeCurve = FadeCurve,
            travelApproachCurve = TravelApproachCurve,
        };

        public static PatchBotPairGhostTuning Default => new PatchBotPairGhostTuning
        {
            duration = 0.35f,
            startRadiusFactor = 0.22f,
            endRadiusFactor = 0.08f,
            spinDegrees = 270f,
            riseFactor = 0.08f,
            travelStartRadiusFactor = 0.28f,
            travelEndRadiusFactor = 0.11f,
            travelSpinSpeed = 540f,
            travelSpinFrequency = 1.25f,
            fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f),
            travelApproachCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
        };
    }

    [System.Serializable]
    public struct PatchbotDashRequest
    {
        public Vector2Int from;
        public Vector2Int to;

        public Sprite carriedSprite;
        public bool orbitCarry;

        public System.Action onStart;
        public System.Action onArrived;
    }

    private readonly List<PatchbotDashRequest> _patchbotDashRequests = new();

    public bool InputLocked => CurrentState == BoardState.Locked || IsBusy;
    public int RemainingMoves { get; private set; }
    public LevelData ActiveLevelData => levelData;

    // ── Tutorial swap filter ──
    private bool _tutorialSwapFilterActive;
    private Vector2Int _tutorialSwapFrom;
    private Vector2Int _tutorialSwapTo;
    private System.Action _tutorialSwapCompleted;

    public void SetTutorialSwapFilter(Vector2Int from, Vector2Int to, System.Action onCompleted = null)
    {
        _tutorialSwapFilterActive = true;
        _tutorialSwapFrom = from;
        _tutorialSwapTo = to;
        _tutorialSwapCompleted = onCompleted;
        SetInputLocked(true);
    }

    private void ClearTutorialSwapFilter()
    {
        _tutorialSwapFilterActive = false;
        var cb = _tutorialSwapCompleted;
        _tutorialSwapCompleted = null;
        SetInputLocked(false);
        cb?.Invoke();
    }

    private bool IsTutorialSwapAllowed(int ax, int ay, int bx, int by)
    {
        return _tutorialSwapFilterActive
            && ((ax == _tutorialSwapFrom.x && ay == _tutorialSwapFrom.y && bx == _tutorialSwapTo.x && by == _tutorialSwapTo.y)
             || (ax == _tutorialSwapTo.x   && ay == _tutorialSwapTo.y   && bx == _tutorialSwapFrom.x && by == _tutorialSwapFrom.y));
    }

    public event Action<int, ObstacleStageSnapshot> OnObstacleStageChanged;
    public event Action<int, ObstacleId> OnObstacleDestroyed;
    public event Action<int> OnCellUnlocked;
    public event Action<int, int> OnObstacleCreatedDynamic;
    public event Action<int> OnChestOpened;
    public event Action<int, ChestColorMask> OnChestColorRemoved;
    public event Action<int, ChestColorMask, int> OnBatteryHit;
    public event Action<int> OnWardrobeOpened;
    public event Action<int, int> OnWardrobeItemRemoved;
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
    public int ActiveBackgroundJobs = 0;
    private ActionSequencer actionSequencer;
    private PulseCoreImpactService pulseCoreImpactService;
    private ObstacleStateService obstacleStateService;
    private CascadeLogic cascadeLogic;
    private ObstacleResolutionService obstacleResolutionService;
    private SpecialCreationService specialCreationService;
    private PendingCreationStore pendingCreationStore;
    private PendingCreationApplicator pendingCreationApplicator;

    private OilSpreadService oilSpreadService;
    private readonly HashSet<Vector2Int> oilSuppressionCellsThisMove = new();
    private bool oilSpreadResolvedThisMove = true;

    // ── Extracted services ──
    private BoardInitService boardInitService;
    private BoardVfxService boardVfxService;
    private LineSweepService lineSweepService;
    private BoosterService boosterService;
    private readonly HashSet<Vector2Int> pendingTriggeredSpecialCells = new();

    private int busyScopeDepth;
    public readonly System.Collections.Generic.List<PatchbotDashRequest> TempPatchbotDashRequests =
        new System.Collections.Generic.List<PatchbotDashRequest>();
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
    internal float FallVelocityCellsPerSecond => Mathf.Max(0.0001f, fallVelocityCellsPerSecond) / Mathf.Max(0.01f, fallDurationMultiplier);
    internal float FallSpawnStaggerMultiplier => Mathf.Clamp01(fallSpawnStaggerMultiplier);
    internal AnimationCurve SwapMoveCurve => swapMoveCurve;
    internal AnimationCurve FallMoveCurve => fallMoveCurve;
    internal bool EnableFallSettle => enableFallSettle;
    internal float FallSettleDuration => Mathf.Max(0f, fallSettleDuration);
    internal float FallSettleStrength => Mathf.Max(0f, fallSettleStrength);
    internal float FallSettleStretchX => Mathf.Max(0f, fallSettleStretchX);
    internal float FallSettleOvershoot => Mathf.Max(0f, fallSettleOvershoot);
    internal float FallCascadeStep => 0f;
    internal float PreClearDelay => preClearDelay;
    internal float ShakeDuration => shakeDuration;
    internal float ShakeStrength => shakeStrength;
    internal RectTransform ShakeTarget => shakeTarget;
    // Spawn taşının başlangıç Y'si = topY - 1 - SpawnFeedGap (gridin üstünden besleme).
    internal int SpawnFeedGap => Mathf.Max(0, spawnFeedGap);
    internal GameObject TilePrefab => tilePrefab;
    internal RectTransform Parent => parent;
    // tilesRoot.parent = spawnParent; animasyon ghostları buraya parentlanırsa
    // gridLinesRoot ve obstaclesRoot'un üstünde render edilebilir.
    internal RectTransform ContentRoot => parent?.parent as RectTransform;
    internal TileType[] RandomPool => randomPool;
    internal LevelData LevelData => levelData;
    internal int PatchBotPulseComboSize => patchBotPulseComboSize;
    internal PatchBotPairGhostTuning PatchBotGhostTuning => patchBotPairGhostTuning.Sanitized;
    internal float PulseImpactDelayStep => pulseImpactDelayStep;
    internal float PulseImpactAnimTime => pulseImpactAnimTime;
    internal float PulseChainCatchOverlap => Mathf.Clamp01(pulseChainCatchOverlap);
    internal float OverridePulseChainStagger => Mathf.Max(0f, overridePulseChainStagger);
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
    internal PulseCoreImpactService PulseCoreImpactService => pulseCoreImpactService;
    internal SpecialBehaviorRegistry SpecialBehaviors => specialBehaviorRegistry;
    public CascadeLogic CascadeLogic => cascadeLogic;
    internal Transform LineTravelSpawnParent => lineTravelSpawnParent;

    // ── Event forwarders for LineSweepService ──
    internal void OnLineSweepStartedInternal(LightningLineStrike strike, float delay) => OnLineSweepStarted?.Invoke(strike, delay);
    internal void OnLineSweepCellReachedInternal(Vector2Int cell, LightningLineStrike strike) => OnLineSweepCellReached?.Invoke(cell, strike);
    internal ObstacleResolutionService Obstacles => obstacleResolutionService;
    // -- gems break animations
    internal BoardBreakFxService BreakFx => boardBreakFxService;
    internal RectTransform BreakFxParent => vfxSpace != null ? vfxSpace : parent;
    internal GameObject TileBreakFxPrefab => tileBreakFxPrefab;
    internal float TileBreakFxLifetime => Mathf.Max(0f, tileBreakFxLifetime);
    internal GameObject ObstacleHitFxPrefab => obstacleHitFxPrefab;
    internal float ObstacleHitFxLifetime => Mathf.Max(0f, obstacleHitFxLifetime);
    internal GameObject ObstacleBreakFxPrefab => obstacleBreakFxPrefab;
    internal float ObstacleBreakFxLifetime => Mathf.Max(0f, obstacleBreakFxLifetime);
    internal BoardInitService BoardInitService => boardInitService;

    internal bool IsPendingTriggeredSpecialCell(int x, int y)
    {
        return pendingTriggeredSpecialCells.Contains(new Vector2Int(x, y));
    }

    internal void SetPendingTriggeredSpecialCells(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
            return;

        foreach (var cell in cells)
            pendingTriggeredSpecialCells.Add(cell);
    }

    internal void ClearPendingTriggeredSpecialCells(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
            return;

        foreach (var cell in cells)
            pendingTriggeredSpecialCells.Remove(cell);
    }

    internal void ClearAllPendingTriggeredSpecialCells()
    {
        pendingTriggeredSpecialCells.Clear();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (shakeTarget != null) shakeBasePos = shakeTarget.anchoredPosition;
        if (audioDirector == null)
            audioDirector = GetComponentInChildren<BoardAudioDirector>(true);
        EnsureServices();
        TryResolveLightningSpawner();
        TryResolveLineTravelPlayer();
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
        specialResolver ??= new SpecialResolver(this, boardAnimator, pulseCoreImpactService);
        specialCreationService ??= new SpecialCreationService(matchFinder);
        pendingCreationStore ??= new PendingCreationStore();
        pendingCreationApplicator ??= new PendingCreationApplicator(this);
        obstacleStateService ??= new ObstacleStateService();
        obstacleResolutionService ??= new ObstacleResolutionService(this);
        oilSpreadService ??= new OilSpreadService(this, obstacleStateService);
        cascadeLogic ??= new CascadeLogic(this);
        boardInitService ??= new BoardInitService();
        boardVfxService ??= new BoardVfxService(this);
        boardBreakFxService ??= new BoardBreakFxService(this);
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

    // Starts a board action immediately as a background job, running concurrently
    // with any currently-playing ActionSequencer action.
    // ResolveBoard's loop already polls ActiveBackgroundJobs > 0 to wait for completion.
    public void StartImmediateAction(BoardAction action)
    {
        ActiveBackgroundJobs++;
        StartCoroutine(RunImmediateAction(action));
    }

    private System.Collections.IEnumerator RunImmediateAction(BoardAction action)
    {
        yield return StartCoroutine(action.ExecuteVisuals(actionSequencer));
        ActiveBackgroundJobs--;
    }

    // Manual background-job scope. Keeps the board "busy" for end-of-level evaluation
    // while an async visual finishes (e.g. a goal collectible flying to the HUD), so an
    // out-of-moves FAIL isn't shown before an in-flight collectible reaches its goal.
    // Always pair Begin/End; the ResolveBoard 5s timeout is the leak safety net.
    public void BeginBackgroundJob() => ActiveBackgroundJobs++;
    public void EndBackgroundJob()   => ActiveBackgroundJobs = Mathf.Max(0, ActiveBackgroundJobs - 1);

    // Runs a list of actions sequentially as a single background job.
    // Use this when actions have a defined order (e.g. Override fanout → clear).
    public void StartImmediateActionSequence(System.Collections.Generic.List<BoardAction> actions)
    {
        if (actions == null || actions.Count == 0) return;
        ActiveBackgroundJobs++;
        StartCoroutine(RunImmediateActionSequence(actions));
    }

    private System.Collections.IEnumerator RunImmediateActionSequence(System.Collections.Generic.List<BoardAction> actions)
    {
        foreach (var action in actions)
        {
            System.Collections.IEnumerator e = action.ExecuteVisuals(actionSequencer);
            while (e.MoveNext())
                yield return e.Current;
        }
        ActiveBackgroundJobs--;
    }

    public struct BonusLinePlacement
    {
        public int x, y;
        public bool isHorizontal;
        public BonusLinePlacement(int x, int y, bool isHorizontal) { this.x = x; this.y = y; this.isHorizontal = isHorizontal; }
    }

    private void OnDestroy()
    {
        if (obstacleStateService == null) return;
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
            obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
            obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
        }

        if (levelData == null) { obstacleStateService = null; oilSpreadService = null; return; }

        obstacleStateService ??= new ObstacleStateService();
        obstacleStateService.Initialize(levelData);
        oilSpreadService = new OilSpreadService(this, obstacleStateService);
        RebuildMaskHoleMap();
        BindObstacleEvents();
        RefreshOilOverlays();
    }

    public void SetupFactory(
        GameObject tilePrefab,
        RectTransform parent,
        int tileSize,
        TileType[] randomPool,
        float tileIconScale = 0.98f,
        bool useFullCellIcons = false,
        Vector2? tileIconSize = null)
    {
        this.tilePrefab = tilePrefab;
        this.parent = parent;
        this.tileSize = tileSize;
        this.randomPool = randomPool;
        this.tileIconScale = Mathf.Clamp(tileIconScale, 0.5f, 1f);
        this.tileIconSize = tileIconSize ?? new Vector2(100f, 100f);
        this.useFullCellIcons = useFullCellIcons;
        EnsureServices();
    }

    public void ConfigureTileView(TileView tile)
    {
        if (tile == null)
            return;

        tile.SetIconScale(tileIconScale);
        tile.SetIconSize(tileIconSize);
        tile.SetUseFullCellIcon(useFullCellIcons);
    }

    public TileType[,] SimulateInitialTypes(bool[,] unreachableCells = null)
    {
        var lockedMask = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                lockedMask[x, y] = holes[x, y] ||
                    (obstacleStateService != null &&
                     (obstacleStateService.IsMovableObstacleAt(x, y) ||
                      obstacleStateService.IsInteractionLockedAt(x, y))) ||
                    (unreachableCells != null && unreachableCells[x, y]);
            }
        }

        return boardInitService.SimulateInitialTypes(width, height, lockedMask, randomPool);
    }
    // ═══════════════════════════════════════════════════════════════
    //  Busy / State
    // ═══════════════════════════════════════════════════════════════

    internal void BeginBusy() { busyScopeDepth++; CurrentState = BoardState.Resolving; }

    internal void EndBusy()
    {
        if (busyScopeDepth > 0) busyScopeDepth--;
        if (busyScopeDepth == 0)
        {
            if (CurrentState == BoardState.Resolving)
                CurrentState = BoardState.Idle;
            // Fire regardless of whether state was Locked — ensures RunAfterIdleRoutine
            // callbacks always complete even if SetInputLocked was called mid-resolve.
            OnBecameIdle?.Invoke();
        }
    }

    public void SetInputLocked(bool isLocked)
    {
        if (isLocked) CurrentState = BoardState.Locked;
        else if (CurrentState == BoardState.Locked) CurrentState = BoardState.Idle;
    }

    public void ForceFullBoardSync()
    {
        ClearAllPendingTriggeredSpecialCells();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (tiles[x, y] != null)
                {
                    tiles[x, y].RefreshIcon();
                    RestoreTilePresentation(tiles[x, y]);
                    SyncTileData(x, y);
                }
                else if (gridData[x, y] != null)
                {
                    gridData[x, y] = null;
                }
            }

        RefreshAllSortingOrders();
    }

    public void RunAfterIdle(Action action)
    {
        if (action == null) return;
        StartCoroutine(RunAfterIdleRoutine(action));
    }

    private IEnumerator RunAfterIdleRoutine(Action action)
    {
        const float timeoutSeconds = 3f;
        float elapsed = 0f;

        while (true)
        {
            bool busy = IsBusy || ActiveBackgroundJobs > 0;

            if (!busy)
            {
                yield return null;
                action?.Invoke();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning(
                    $"[Board] RunAfterIdle timeout. " +
                    $"IsBusy={IsBusy}, CurrentState={CurrentState}, " +
                    $"busyScopeDepth={busyScopeDepth}, ActiveBackgroundJobs={ActiveBackgroundJobs}");

                action?.Invoke();
                yield break;
            }

            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  VFX Delegation
    // ═══════════════════════════════════════════════════════════════

    public float PlaySystemOverrideComboVfxAndGetDuration()
    {
        Sprite sprA = lastSwapA != null ? GetOverrideIcon(lastSwapA.GetTileType()) : null;
        Sprite sprB = lastSwapB != null ? GetOverrideIcon(lastSwapB.GetTileType()) : null;
        return boardVfxService.PlaySystemOverrideComboVfxAndGetDuration(systemOverrideComboVfx, sprA, sprB);
    }

    public float GetSystemOverrideComboPreClearDuration() =>
        systemOverrideComboVfx != null ? systemOverrideComboVfx.GetPreClearDuration() : 0f;

    public float GetSystemOverrideComboWaveDuration() =>
        systemOverrideComboVfx != null ? systemOverrideComboVfx.GetRadialWaveDuration() : ResolutionContext.OverrideRadialClearDuration;
    public void PlayPulseEmitterComboVfxAtCell(int x, int y) => boardVfxService.PlayPulseEmitterComboVfxAtCell(pulseEmitterComboVfx, vfxSpace, x, y);
    public void PlayPulsePulseExplosionVfxAtCell(int x, int y) => boardVfxService.PlayPulsePulseExplosionVfxAtCell(pulsePulseExplosionPrefab, vfxSpace, pulsePulseExplosionLifetime, x, y);
    internal HashSet<Vector2Int> BuildPulseEmitterTargets(int cx, int cy) => boardVfxService.BuildPulseEmitterTargets(cx, cy);

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

    internal float PlayLightningLineStrikes(IReadOnlyList<LightningLineStrike> lineStrikes, Action<Vector2Int, int> onSweepCellReached = null)
    {
        TryResolveLightningSpawner();
        EnsureLineTravelVisualReady();
        return lineSweepService.PlayLightningLineStrikes(lightningSpawner, lineTravelPlayer, lineStrikes, onSweepCellReached);
    }

    internal float PlayLineTravelInstanceWithStep(
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        Vector2Int originCell,
        int steps,
        float cellSizePx,
        float delaySeconds,
        Action<Vector2Int> onStep,
        Action onCompleted = null)
    {
        EnsureLineTravelVisualReady();
        return lineSweepService.PlayLineTravelInstanceWithStep(
            lineTravelPlayer,
            axis,
            originAnchored,
            originCell,
            steps,
            cellSizePx,
            delaySeconds,
            onStep,
            onCompleted);
    }

    internal float PlayLineTravelInstanceAsymmetric(
        LineTravelSplitSwapTestUI.LineAxis axis,
        Vector2 originAnchored,
        Vector2Int originCell,
        int stepsPos,
        int stepsNeg,
        float cellSizePx,
        float delaySeconds,
        Action<Vector2Int> onStep,
        Action onCompleted = null)
    {
        EnsureLineTravelVisualReady();
        return lineSweepService.PlayLineTravelInstanceAsymmetric(
            lineTravelPlayer,
            axis,
            originAnchored,
            originCell,
            stepsPos,
            stepsNeg,
            cellSizePx,
            delaySeconds,
            onStep,
            onCompleted);
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

    private void TryResolveLineTravelPlayer()
    {
        if (lineTravelPlayer == null)
        {
            lineTravelPlayer = GetComponentInChildren<LineTravelSplitSwapTestUI>(true);

            if (lineTravelPlayer == null && transform.parent != null)
                lineTravelPlayer = transform.parent.GetComponentInChildren<LineTravelSplitSwapTestUI>(true);
        }

        if (lineTravelPlayer == null)
            return;

        if (lineTravelSpawnParent == null)
        {
            if (lineTravelPlayer.afterImageParent != null)
                lineTravelSpawnParent = lineTravelPlayer.afterImageParent;
            else if (lineTravelPlayer.transform.parent != null)
                lineTravelSpawnParent = lineTravelPlayer.transform.parent;
        }

        if (lineTravelSpawnParent is RectTransform lineTravelParent)
        {
            if (lineTravelPlayer.afterImageParent == null)
                lineTravelPlayer.afterImageParent = lineTravelParent;

            if (lineTravelPlayer.impactParent == null)
                lineTravelPlayer.impactParent = lineTravelParent;

            if (lineTravelPlayer.trailParent == null)
                lineTravelPlayer.trailParent = lineTravelParent;
        }
    }

    private void EnsureLineTravelVisualReady()
    {
        TryResolveLineTravelPlayer();

        if (lineTravelSpawnParent != null)
        {
            EnsureTransformHierarchyActive(lineTravelSpawnParent);
            lineTravelSpawnParent.SetAsLastSibling();
        }

        if (lineTravelPlayer == null)
            return;

        if (lineTravelPlayer.transform.parent != null)
        {
            EnsureTransformHierarchyActive(lineTravelPlayer.transform.parent);
            lineTravelPlayer.transform.parent.SetAsLastSibling();
        }

        EnsureTransformHierarchyActive(lineTravelPlayer.afterImageParent);
        EnsureTransformHierarchyActive(lineTravelPlayer.impactParent);
        EnsureTransformHierarchyActive(lineTravelPlayer.trailParent);
    }

    private static void EnsureTransformHierarchyActive(Transform transformToActivate)
    {
        for (Transform current = transformToActivate; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tile / Data Management
    // ═══════════════════════════════════════════════════════════════

    public void EnqueuePatchbotDash(Vector2Int from, Vector2Int to)
    {
        _patchbotDashRequests.Add(new PatchbotDashRequest
        {
            from = from,
            to = to
        });
    }

    public void EnqueuePatchbotDash(PatchbotDashRequest req)
    {
        _patchbotDashRequests.Add(req);
    }

    public void ConsumePatchbotDashRequests(List<PatchbotDashRequest> outList) { outList.Clear(); outList.AddRange(_patchbotDashRequests); _patchbotDashRequests.Clear(); }

    public TopHudController TopHud { get { if (topHud == null) topHud = FindFirstObjectByType<TopHudController>(); return topHud; } }
    public GoalFlyFx GoalFlyFx { get { if (goalFlyFx == null) goalFlyFx = FindFirstObjectByType<GoalFlyFx>(); return goalFlyFx; } }

    public void SetHole(int x, int y, bool isHole) => holes[x, y] = isHole;
    public TileView GetTileViewAt(int x, int y) { if (tiles == null || x < 0 || x >= width || y < 0 || y >= height) return null; return tiles[x, y]; }
    public Sprite GetIcon(TileType type) => iconLibrary != null ? iconLibrary.Get(type) : null;
    public Sprite GetSpecialIcon(TileSpecial special) => iconLibrary != null ? iconLibrary.GetSpecialIcon(special) : null;
    public Sprite GetOverrideIcon(TileType baseType) => iconLibrary != null ? iconLibrary.GetOverrideIcon(baseType) : null;
    public Sprite GetPatchBotFlightIcon() => iconLibrary != null ? iconLibrary.GetPatchBotFlightIcon() : null;
    public Sprite GetPatchBotFullIcon()   => iconLibrary != null ? iconLibrary.GetPatchBotFullIcon()   : null;
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
    internal void RefreshTileObstacleVisual(TileView tile)
    {
        RestoreTilePresentation(tile);

        if (tile == null) return;

        // Movable obstacle (plastik vb.): bu hücre hâlâ movable obstacle ise görünümü yeniden zorla.
        // Fall/cascade/swap sonrası view yeniden oluşsa veya sprite normal taşa dönse bile
        // (görsel/mantık desync) ekranda movable görünmeli — yoksa goal/match mantığı bu hücreyi
        // movable sayar ama oyuncu normal taş görür (3'lü görünür ama kırılmaz, vb.).
        if (obstacleStateService == null) return;

        int tx = tile.X, ty = tile.Y;
        if (tx < 0 || tx >= width || ty < 0 || ty >= height) return;
        if (!obstacleStateService.IsMovableObstacleAt(tx, ty)) return;

        var id = obstacleStateService.GetObstacleIdAt(tx, ty);
        var lib = ActiveLevelData != null ? ActiveLevelData.obstacleLibrary : null;
        var def = lib != null ? lib.Get(id) : null;
        if (def == null) return;

        var sprite = def.GetPreviewSprite();
        if (sprite == null) return;

        tile.SetMovableObstacleTile(true);
        tile.SetUseFullCellIcon(false);
        tile.SetFullCellMovableSprite(def.fullCellSprite);
        tile.SetMovableObstacleSprite(sprite);
    }

    internal void RefreshAllTileObstacleVisuals()
    {
        if (tiles == null) return;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) RefreshTileObstacleVisual(tiles[x, y]);
    }

    internal void RefreshOilOverlays()
    {
        if (tiles == null) return;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tiles[x, y]?.SetCoveredByCellOverlay(false);

        if (obstacleStateService == null) return;
        var oilCells = obstacleStateService.GetAllOilCells();
        Debug.Log($"[OilOverlay] RefreshOilOverlays: {oilCells.Count} oil cells");
        foreach (var cell in oilCells)
        {
            if (cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height)
            {
                var tile = tiles[cell.x, cell.y];
                Debug.Log($"[OilOverlay] ({cell.x},{cell.y}) tile={tile != null}");
                tile?.SetCoveredByCellOverlay(true);
            }
        }
    }

    internal void RestoreTilePresentation(TileView tile)
    {
        if (tile == null)
            return;

        tile.SetIconAlpha(1f);

        if (tile.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        RectTransform rt = tile.RectTransform;
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }

    internal void RestoreAllTilePresentation()
    {
        if (tiles == null)
            return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                RestoreTilePresentation(tiles[x, y]);
            }
        }
    }

    /// <summary>
    /// Tüm tile'ların sibling sırasını Y koordinatına göre yeniden hesaplar.
    /// Cascade/spawn sonrası tutarsız sıralamayı düzeltir.
    /// Üst satırlar (Y küçük) önde render edilir → en son SetAsLastSibling çağrılır.
    /// SetSiblingIndex yerine SetAsLastSibling kullanılır çünkü SetSiblingIndex
    /// çağrıldığında diğer child'ların index'leri kayar ve tutarsızlık oluşur.
    /// </summary>
    internal void RefreshAllSortingOrders()
    {
        if (tiles == null) return;
        // En arkada olması gereken tile'dan başla (Y büyük = alt satır = arkada)
        // En son çağrılan SetAsLastSibling en önde olur
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                if (tiles[x, y] != null)
                    tiles[x, y].transform.SetAsLastSibling();
            }
        }
    }
    // ═══════════════════════════════════════════════════════════════
    //  Cell Clear
    // ═══════════════════════════════════════════════════════════════

    internal void ClearCell(int x, int y) { if (x < 0 || x >= width || y < 0 || y >= height) return; tiles[x, y] = null; gridData[x, y] = null; }

    internal void ClearAndDestroyTile(TileView tile, Dictionary<TileType, int> clearedByType = null)
    {
        if (tile == null || !tile)
            return;

        int x;
        int y;
        TileType tileType;
        TileSpecial special;
        bool wasLiveInGrid = false;

        try
        {
            x = tile.X;
            y = tile.Y;
            tileType = tile.GetTileType();
            special = tile.GetSpecial();

            wasLiveInGrid =
                x >= 0 && x < width &&
                y >= 0 && y < height &&
                tiles[x, y] == tile;
        }
        catch (MissingReferenceException)
        {
            return;
        }

        Debug.Log(
            $"[PulseClearDebug] ClearAndDestroyTile ENTER tile=({x},{y}) " +
            $"type={tileType} special={special} live={wasLiveInGrid}");

        if (wasLiveInGrid)
            ClearCell(x, y);

        if (clearedByType != null)
        {
            var effectiveType = special switch
            {
                TileSpecial.LineH => TileType.LineEmitter_H,
                TileSpecial.LineV => TileType.LineEmitter_V,
                _                 => tileType
            };
            clearedByType.TryGetValue(effectiveType, out int c);
            clearedByType[effectiveType] = c + 1;
        }

        if (tile != null && tile)
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
        if (IsBusy) return;

        int nx = from.X + dirX, ny = from.Y + dirY;

        if (InputLocked)
        {
            if (!IsTutorialSwapAllowed(from.X, from.Y, nx, ny)) return;
            ClearTutorialSwapFilter();
        }

        if (activeBooster != BoosterMode.None) return;
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
        if (holes[nx, ny] && (obstacleStateService == null || !obstacleStateService.HasObstacleAt(nx, ny))) return;
        if (obstacleStateService != null &&
            (obstacleStateService.IsOilAt(from.X, from.Y) || obstacleStateService.IsOilAt(nx, ny)))
            return;
        if (obstacleStateService != null &&
            ((obstacleStateService.IsUnderTileObstacleAt(from.X, from.Y) && !obstacleStateService.IsMudAt(from.X, from.Y))
             || (obstacleStateService.IsUnderTileObstacleAt(nx, ny) && !obstacleStateService.IsMudAt(nx, ny))))
            return;
        if (obstacleStateService != null &&
            (obstacleStateService.IsInteractionLockedAt(from.X, from.Y) || obstacleStateService.IsInteractionLockedAt(nx, ny)))
            return;
        TileView other = tiles[nx, ny]; if (other == null) return;
        StartCoroutine(ProcessSwap(from, other));
    }

    public void OnTileClicked(TileView tile)
    {
        if (IsBusy) return;

        if (InputLocked)
        {
            if (!_tutorialSwapFilterActive) return;
            // Tutorial click-to-swap: iki tıkla hedef hamlesi yapılabilir
            if (selected == null) { selected = tile; return; }
            if (selected == tile) { selected = null; return; }
            if (AreNeighbors(selected, tile) && IsTutorialSwapAllowed(selected.X, selected.Y, tile.X, tile.Y))
            {
                var a = selected; selected = null;
                ClearTutorialSwapFilter();
                StartCoroutine(ProcessSwap(a, tile));
            }
            else { selected = null; }
            return;
        }

        if (TryUseBooster(tile)) return;

        // Special'a tek tık → hareket ettirmeden solo aktive et. Swap (ve special+special
        // combo) için sürükleme kullanılır; TileView tap'i sürüklemeden ayırdığı için
        // sürüklenen special burada tetiklenmez, sadece gerçek tıklamada çalışır.
        if (tile != null && tile.GetSpecial() != TileSpecial.None)
        {
            selected = null;
            StartCoroutine(ProcessSpecialTap(tile));
            return;
        }

        if (selected == null) { selected = tile; return; }
        if (selected == tile) { selected = null; return; }
        if (AreNeighbors(selected, tile))
        {
            var a = selected;
            selected = null;
            bool underTileBlocked = obstacleStateService != null &&
                ((obstacleStateService.IsUnderTileObstacleAt(a.X, a.Y) && !obstacleStateService.IsMudAt(a.X, a.Y))
                 || (obstacleStateService.IsUnderTileObstacleAt(tile.X, tile.Y) && !obstacleStateService.IsMudAt(tile.X, tile.Y))
                 || obstacleStateService.IsInteractionLockedAt(a.X, a.Y) || obstacleStateService.IsInteractionLockedAt(tile.X, tile.Y));
            if (!underTileBlocked)
                StartCoroutine(ProcessSwap(a, tile));
            return;
        }
        selected = tile;
    }

    bool AreNeighbors(TileView a, TileView b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) == 1;

    // ═══════════════════════════════════════════════════════════════
    //  Booster Delegation
    // ═══════════════════════════════════════════════════════════════

    public void ActivateBooster(int idx)
    {
        switch (idx)
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
        if (mode != BoosterMode.None)
            BoardIdleHintAndComboGlowController.CancelHintsForBoard(this);
        OnBoosterTargetingChanged?.Invoke(activeBooster != BoosterMode.None);
    }
    public bool IsBoosterModeActive => activeBooster != BoosterMode.None;

    bool TryUseBooster(TileView tile)
    {
        if (activeBooster == BoosterMode.None) return false;
        if (tile == null) return true;
        if (IsBusy || InputLocked) return true;
        return TryUseBoosterAtCell(tile.X, tile.Y);
    }

    public bool TryUseBoosterAtCell(int x, int y)
    {
        Debug.Log($"[Booster] TryUseBoosterAtCell ({x},{y}) mode={activeBooster} busy={IsBusy} inputLocked={InputLocked}");

        if (activeBooster == BoosterMode.None) return false;
        if (IsBusy || InputLocked) { Debug.LogWarning("[Booster] Skip: busy or input locked"); return true; }
        if (x < 0 || x >= width || y < 0 || y >= height) return true;
        var mode = activeBooster; SetBoosterMode(BoosterMode.None); selected = null;
        var targetCell = new Vector2Int(x, y); var targetTile = tiles[x, y];

        if (mode == BoosterMode.Shuffle)
        {
            Debug.Log("[Booster] Shuffle mode → ShuffleBoardRoutine başlatılıyor.");
            StartCoroutine(boosterService.ShuffleBoardRoutine(actionSequencer));
        }
        else
        {
            StartCoroutine(boosterService.ApplyBoosterRoutine(mode, targetTile, targetCell,
                specialResolver, actionSequencer, cascadeLogic, lineSweepService, lightningSpawner, lineTravelPlayer));
        }
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ProcessSwap
    // ═══════════════════════════════════════════════════════════════

    // Special'ı yerinde (swap olmadan) tek tıkla aktive eder. Bir hamle tüketir,
    // ResolveSpecialSolo'yu oynatır, sonra board'u (cascade) çözer — swap'ın special
    // dalıyla aynı sonlandırma.
    IEnumerator ProcessSpecialTap(TileView tile)
    {
        if (tile == null || tile.GetSpecial() == TileSpecial.None)
            yield break;

        if (IsBusy) yield break;

        oilSuppressionCellsThisMove.Clear();
        oilSpreadResolvedThisMove = false;

        BeginBusy();
        lastSwapA = tile; lastSwapB = null; lastSwapUserMove = true;
        SyncAllTilesToGridData();

        ConsumeMove();

        actionSequencer.Enqueue(specialResolver.ResolveSpecialSolo(tile));
        yield return AnimateQueuedActions();

        yield return ResolveBoard(allowSpecialActivation: false, resolveEmptyCellsFirst: true);
        EndBusy();
    }

    IEnumerator ProcessSwap(TileView a, TileView b)
    {
        if (a == null || b == null)
            yield break;

        oilSuppressionCellsThisMove.Clear();
        oilSpreadResolvedThisMove = false;

        float _flowStart = Time.realtimeSinceStartup;
        float _flowLast = _flowStart;
        void FlowLog(string step)
        {
            float now = Time.realtimeSinceStartup;
            float delta = now - _flowLast;
            float total = now - _flowStart;
            Debug.Log($"[Flow] {step,-22} +{delta:0.000}s (total: {total:0.000}s)");
            _flowLast = now;
        }

        Debug.Log("[Flow] ═══ SWAP START ═══");
        BeginBusy();
        lastSwapA = a; lastSwapB = b; lastSwapUserMove = true;
        SyncAllTilesToGridData();

        int ax = a.X, ay = a.Y, bx = b.X, by = b.Y;

        // Swap öncesi movable obstacle state snapshot
        bool movableMovedAToB, movableMovedBToA;

        tiles[ax, ay] = b;
        tiles[bx, by] = a;
        a.SetCoords(bx, by);
        b.SetCoords(ax, ay);

        // Movable obstacle varsa logical obstacle state de tile ile birlikte taşınsın
        TryApplyMovableObstacleSwapState(ax, ay, bx, by, out movableMovedAToB, out movableMovedBToA);

        SyncTileData(ax, ay);
        SyncTileData(bx, by);
        RefreshAllSortingOrders();

        actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
        yield return AnimateQueuedActions();
        FlowLog("swap_anim");

        // SWAP ANINDAKI gerçek special state'i snapshot al.
        TileSpecial originalSa = a.GetSpecial();
        TileSpecial originalSb = b.GetSpecial();

        Debug.Log($"[ProcessSwap] SNAPSHOT a=({a.X},{a.Y}) specialA={originalSa} | b=({b.X},{b.Y}) specialB={originalSb} | origPos a=({ax},{ay}) b=({bx},{by})");

        // Override+Normal: normal partner tile kendi match'ine girip temizlenirse,
        // frame sınırı sonrasında Unity fake-null devreye girer. Type'ı şimdi yakala.
        TileType? capturedOverridePartnerType = null;
        if ((originalSa == TileSpecial.SystemOverride && originalSb == TileSpecial.None) ||
            (originalSa == TileSpecial.None           && originalSb == TileSpecial.SystemOverride))
        {
            var normalPartner = (originalSa == TileSpecial.None) ? a : b;
            capturedOverridePartnerType = normalPartner.GetTileType();
        }

        // ══════════════════════════════════════════════════════════
        //  Special swap path — en az bir taraf başlangıçta special
        // ══════════════════════════════════════════════════════════
        if (originalSa != TileSpecial.None || originalSb != TileSpecial.None)
        {
            bool bothOriginallySpecial = originalSa != TileSpecial.None && originalSb != TileSpecial.None;
            ConsumeMove();

            // Swap sırasında normal taraf eşleşmesinden oluşan yeni special hücreleri izler.
            // PulseCore bu hücreleri tüketmemeli — ResolveSpecialSwap'a geçirilir.
            HashSet<Vector2Int> swapProtectedCells = null;

            if (!bothOriginallySpecial)
            {
                var specialTile = (originalSa != TileSpecial.None) ? a : b;
                var normalTile = (originalSa != TileSpecial.None) ? b : a;
                int sx = specialTile.X, sy = specialTile.Y;

                var savedTile = tiles[sx, sy];
                var savedData = gridData[sx, sy];
                tiles[sx, sy] = null;
                gridData[sx, sy] = null;

                var normalMatches = matchFinder.FindMatchesAt(normalTile.X, normalTile.Y);

                tiles[sx, sy] = savedTile;
                gridData[sx, sy] = savedData;

                if (normalMatches.Count >= 3)
                {
                    var candidates = new HashSet<TileView>(normalMatches);
                    candidates.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);

                    var creations = specialCreationService.DecideUpToTwoFromMatches(
                        candidates,
                        new SpecialCreationService.CreationRequest(lastSwapA, lastSwapB, lastSwapUserMove));

                    var createdTiles = new List<TileView>();

                    if (creations != null && creations.Count > 0)
                    {
                        foreach (var creation in creations)
                        {
                            if (!creation.hasValue || creation.winner == null)
                                continue;

                            if (creation.winner.GetSpecial() != TileSpecial.None)
                                continue;

                            if (obstacleStateService != null
                                && obstacleStateService.IsMovableObstacleAt(creation.winner.X, creation.winner.Y))
                                continue;

                            var created = specialResolver.ApplyCreatedSpecial(creation.winner, creation.special);
                            if (created == null)
                                continue;

                            createdTiles.Add(created);

                            // Bu hücreyi PulseCore'dan koru — winner normalPartner olmayabilir.
                            (swapProtectedCells ??= new HashSet<Vector2Int>())
                                .Add(new Vector2Int(created.X, created.Y));

                            normalMatches.Remove(created);
                            candidates.Remove(created);
                        }

                        if (boardAnimatorRef != null)
                        {
                            foreach (var created in createdTiles)
                            {
                                if (created == null) continue;
                                var creationContributors = new HashSet<TileView>(normalMatches);
                                creationContributors.RemoveWhere(t => t == null || t == created || t.GetSpecial() != TileSpecial.None);
                                if (creationContributors.Count > 0)
                                    StartCoroutine(boardAnimatorRef.PlaySpecialCreationMerge(created, creationContributors));
                            }
                        }
                    }

                    normalMatches.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);
                    FlowLog($"special_creation({createdTiles.Count})");

                    if (normalMatches.Count > 0)
                    {
                        actionSequencer.Enqueue(new MatchClearAction(
                            normalMatches,
                            doShake: false,
                            suppressPerTileClearVfx: createdTiles.Count > 0,
                            implodeTargetCell: new Vector2Int(a.X, a.Y)));
                        yield return AnimateQueuedActions();
                        FlowLog("normal_side_clear");
                    }
                }
            }

            if (originalSa == TileSpecial.PulseCore && originalSb == TileSpecial.PulseCore)
            {
                Debug.Log($"[PulsePulseCharge] a=({a.X},{a.Y}) b=({b.X},{b.Y}) orig_a=({ax},{ay}) orig_b=({bx},{by})");

                int chargeX = bx;
                int chargeY = by;

                SpecialVisualService.HideTileVisualForCombo(a);
                SpecialVisualService.HideTileVisualForCombo(b);

                PlayPulsePulseExplosionVfxAtCell(chargeX, chargeY);
                yield return new WaitForSeconds(pulsePulseChargeDuration);

                if (pulseCoreImpactService != null)
                    pulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell(chargeX, chargeY, radiusCells: 2); // 5x5 alan — Inspector override'a bağımlı değil
            }

            actionSequencer.Enqueue(specialResolver.ResolveSpecialSwap(a, b, originalSa, originalSb, capturedOverridePartnerType, swapProtectedCells));
            yield return AnimateQueuedActions();
            FlowLog("special_resolve");

            yield return ResolveBoard(allowSpecialActivation: false, resolveEmptyCellsFirst: true);
            FlowLog("resolve_board");
            Debug.Log($"[Flow] ═══ SWAP END (special) ═══ total: {Time.realtimeSinceStartup - _flowStart:0.000}s");
            EndBusy();
            yield break;
        }

        // ══════════════════════════════════════════════════════════
        //  Normal swap path — iki taraf da normal taş
        // ══════════════════════════════════════════════════════════
        var matches = new HashSet<TileView>();
        foreach (var t in matchFinder.FindMatchesAt(a.X, a.Y)) matches.Add(t);
        foreach (var t in matchFinder.FindMatchesAt(b.X, b.Y)) matches.Add(t);
        FlowLog($"match_find({matches.Count})");

        bool shouldAcceptViaSettle = false;
        if (matches.Count == 0 && allowPostSwapSettleValidation)
        {
            shouldAcceptViaSettle = WouldCreatePostSwapSettleMatch();
            FlowLog($"post_settle_check({shouldAcceptViaSettle})");
        }

        if (matches.Count == 0 && !shouldAcceptViaSettle)
        {
            // Obstacle state de geri alınsın
            RestoreMovableObstacleSwapState(ax, ay, bx, by, movableMovedAToB, movableMovedBToA);

            tiles[ax, ay] = a;
            tiles[bx, by] = b;
            a.SetCoords(ax, ay);
            b.SetCoords(bx, by);

            SyncTileData(ax, ay);
            SyncTileData(bx, by);
            RefreshAllSortingOrders();

            actionSequencer.Enqueue(new SwapAction(a, b, SwapDurationWithMultiplier));
            yield return AnimateQueuedActions();
            FlowLog("swap_back");

            Debug.Log($"[Flow] ═══ SWAP END (no match) ═══ total: {Time.realtimeSinceStartup - _flowStart:0.000}s");
            EndBusy();
            yield break;
        }

        ConsumeMove();

        // Instant match yok ama settle sonrası match olacaksa, clear pass'e girmeden
        // board'un önce fall/cascade çözmesine izin ver.
        if (matches.Count == 0 && shouldAcceptViaSettle)
        {
            yield return ResolveBoard(resolveEmptyCellsFirst: true);
            FlowLog("resolve_board_after_settle");
            Debug.Log($"[Flow] ═══ SWAP END (settle-valid) ═══ total: {Time.realtimeSinceStartup - _flowStart:0.000}s");
            EndBusy();
            yield break;
        }

        yield return ExecuteClearPass(matches, allowSpecialActivation: true, swapCell: new Vector2Int(a.X, a.Y));
        FlowLog("clear_pass");
        yield return ResolveBoard(resolveEmptyCellsFirst: true);
        FlowLog("resolve_board");
        Debug.Log($"[Flow] ═══ SWAP END ═══ total: {Time.realtimeSinceStartup - _flowStart:0.000}s");
        EndBusy();
    }
    // ═══════════════════════════════════════════════════════════════
    //  Resolve Board
    // ═══════════════════════════════════════════════════════════════

    private void TryApplyMovableObstacleSwapState(
        int ax, int ay,
        int bx, int by,
        out bool movedAToB,
        out bool movedBToA)
    {
        movedAToB = false;
        movedBToA = false;

        if (obstacleStateService == null)
            return;

        bool aHasMovable = obstacleStateService.IsMovableObstacleAt(ax, ay);
        bool bHasMovable = obstacleStateService.IsMovableObstacleAt(bx, by);

        // İki taraf da movable ise hücreler yine movable kalır, ekstra taşıma gerekmez.
        if (aHasMovable == bHasMovable)
            return;

        if (aHasMovable)
        {
            obstacleStateService.MoveObstacle(ax, ay, bx, by);
            movedAToB = true;
        }
        else
        {
            obstacleStateService.MoveObstacle(bx, by, ax, ay);
            movedBToA = true;
        }
    }

    private void RestoreMovableObstacleSwapState(
        int ax, int ay,
        int bx, int by,
        bool movedAToB,
        bool movedBToA)
    {
        if (obstacleStateService == null)
            return;

        if (movedAToB)
            obstacleStateService.MoveObstacle(bx, by, ax, ay);
        else if (movedBToA)
            obstacleStateService.MoveObstacle(ax, ay, bx, by);
    }

    private bool WouldCreatePostSwapSettleMatch()
    {
        if (!allowPostSwapSettleValidation)
            return false;

        bool[,] simHasTile = new bool[width, height];
        TileType[,] simTypes = new TileType[width, height];
        TileSpecial[,] simSpecials = new TileSpecial[width, height];
        bool[,] simMovableObstacle = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = tiles[x, y];
                if (tile != null)
                {
                    simHasTile[x, y] = true;
                    simTypes[x, y] = tile.GetTileType();
                    simSpecials[x, y] = tile.GetSpecial();
                }

                simMovableObstacle[x, y] =
                    obstacleStateService != null &&
                    obstacleStateService.IsMovableObstacleAt(x, y);
            }
        }

        if (HasAnySimMatch(simHasTile, simTypes, simSpecials, simMovableObstacle))
            return true;

        const int maxPass = 32;
        for (int pass = 0; pass < maxPass; pass++)
        {
            bool moved = SimulateCollapseExistingTiles(
                simHasTile,
                simTypes,
                simSpecials,
                simMovableObstacle);

            if (!moved)
                return false;

            if (HasAnySimMatch(simHasTile, simTypes, simSpecials, simMovableObstacle))
                return true;
        }

        return false;
    }

    private bool SimulateCollapseExistingTiles(
        bool[,] simHasTile,
        TileType[,] simTypes,
        TileSpecial[,] simSpecials,
        bool[,] simMovableObstacle)
    {
        bool movedAny = false;

        var landingYs = new List<int>(height);
        var sourceYs = new List<int>(height);
        var sourceTypes = new List<TileType>(height);
        var sourceSpecials = new List<TileSpecial>(height);
        var sourceMovableFlags = new List<bool>(height);

        for (int x = 0; x < width; x++)
        {
            int segmentBottom = height - 1;

            while (segmentBottom >= 0)
            {
                while (segmentBottom >= 0 && IsObstacleBlockedCell(x, segmentBottom))
                    segmentBottom--;

                if (segmentBottom < 0)
                    break;

                int segmentTop = segmentBottom;
                while (segmentTop > 0 && !IsObstacleBlockedCell(x, segmentTop - 1))
                    segmentTop--;

                landingYs.Clear();
                sourceYs.Clear();
                sourceTypes.Clear();
                sourceSpecials.Clear();
                sourceMovableFlags.Clear();

                // Bu segmentte taşların yerleşebileceği gerçek slotlar
                for (int y = segmentBottom; y >= segmentTop; y--)
                {
                    if (IsMaskHoleCell(x, y))
                        continue;

                    if (IsObstacleBlockedCell(x, y))
                        continue;

                    landingYs.Add(y);
                }

                // Segmentteki mevcut taşları topla
                for (int y = segmentBottom; y >= segmentTop; y--)
                {
                    if (!simHasTile[x, y])
                        continue;

                    if (IsMaskHoleCell(x, y))
                        continue;

                    if (IsObstacleBlockedCell(x, y))
                        continue;

                    sourceYs.Add(y);
                    sourceTypes.Add(simTypes[x, y]);
                    sourceSpecials.Add(simSpecials[x, y]);
                    sourceMovableFlags.Add(simMovableObstacle[x, y]);

                    simHasTile[x, y] = false;
                    simTypes[x, y] = default;
                    simSpecials[x, y] = TileSpecial.None;
                    simMovableObstacle[x, y] = false;
                }

                // Taşları aşağı sıkıştır
                int count = Mathf.Min(sourceYs.Count, landingYs.Count);
                for (int i = 0; i < count; i++)
                {
                    int toY = landingYs[i];

                    simHasTile[x, toY] = true;
                    simTypes[x, toY] = sourceTypes[i];
                    simSpecials[x, toY] = sourceSpecials[i];
                    simMovableObstacle[x, toY] = sourceMovableFlags[i];

                    if (sourceYs[i] != toY)
                        movedAny = true;
                }

                segmentBottom = segmentTop - 1;
            }
        }

        return movedAny;
    }

    private bool HasAnySimMatch(
        bool[,] simHasTile,
        TileType[,] simTypes,
        TileSpecial[,] simSpecials,
        bool[,] simMovableObstacle)
    {
        // Horizontal
        for (int y = 0; y < height; y++)
        {
            int run = 0;
            TileType runType = default;

            for (int x = 0; x < width; x++)
            {
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x, y))
                {
                    if (run >= 3) return true;
                    run = 0;
                    continue;
                }

                var t = simTypes[x, y];
                if (run == 0)
                {
                    run = 1;
                    runType = t;
                }
                else if (t.Equals(runType))
                {
                    run++;
                }
                else
                {
                    if (run >= 3) return true;
                    run = 1;
                    runType = t;
                }
            }

            if (run >= 3) return true;
        }

        // Vertical
        for (int x = 0; x < width; x++)
        {
            int run = 0;
            TileType runType = default;

            for (int y = 0; y < height; y++)
            {
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x, y))
                {
                    if (run >= 3) return true;
                    run = 0;
                    continue;
                }

                var t = simTypes[x, y];
                if (run == 0)
                {
                    run = 1;
                    runType = t;
                }
                else if (t.Equals(runType))
                {
                    run++;
                }
                else
                {
                    if (run >= 3) return true;
                    run = 1;
                    runType = t;
                }
            }

            if (run >= 3) return true;
        }

        // 2x2
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x, y)) continue;
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x + 1, y)) continue;
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x, y + 1)) continue;
                if (!IsSimNormalMatchable(simHasTile, simSpecials, simMovableObstacle, x + 1, y + 1)) continue;

                var t = simTypes[x, y];
                if (!simTypes[x + 1, y].Equals(t)) continue;
                if (!simTypes[x, y + 1].Equals(t)) continue;
                if (!simTypes[x + 1, y + 1].Equals(t)) continue;

                return true;
            }
        }

        return false;
    }

    private bool IsSimNormalMatchable(
        bool[,] simHasTile,
        TileSpecial[,] simSpecials,
        bool[,] simMovableObstacle,
        int x,
        int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        if (!simHasTile[x, y])
            return false;

        if (IsMaskHoleCell(x, y))
            return false;

        if (IsObstacleBlockedCell(x, y))
            return false;

        if (simSpecials[x, y] != TileSpecial.None)
            return false;

        if (simMovableObstacle[x, y])
            return false;

        return true;
    }
    IEnumerator ResolveBoard(bool allowSpecialActivation = false, bool resolveEmptyCellsFirst = false)
    {
        float _rbStart = Time.realtimeSinceStartup;
        isSpecialActivationPhase = false;

        // Cascade/match sırası artık while loop başındaki strict barrier tarafından yönetiliyor.
        // resolveEmptyCellsFirst parametresi backward-compatible olarak bırakıldı.

        int safety = 0;
        const int MaxResolveLoops = 250;
        float backgroundJobWaitTime = 0f;

        while (true)
        {
            safety++;
            if (safety > MaxResolveLoops)
                yield break;

            CurrentResolvePass = safety;

            // ─────────────────────────────────────────────
            // STRICT ORDER BARRIER:
            // MatchFinder asla aktif boş hücre varken çalışmamalı.
            // Her resolve pass başında önce cascade settle yapılır:
            // 1) vertical, 2) diagonal, 3) vertical, 4) pocket fill.
            // Ancak boşluk kapandıktan sonra match aranır.
            // ─────────────────────────────────────────────
            if (cascadeLogic != null && cascadeLogic.HasAnyEmptyPlayableCell())
            {
                var preMatchCascades = cascadeLogic.CalculateCascades();

                if (preMatchCascades.Count > 0)
                {
                    DisableSettleIfMoreCascadesFollow(preMatchCascades);
                    Debug.Log($"[Resolve] pass={safety} pre_match_cascade actions={preMatchCascades.Count} +{(Time.realtimeSinceStartup - _rbStart):0.000}s");
                    actionSequencer.Enqueue(preMatchCascades);

                    while (actionSequencer.IsPlaying)
                        yield return null;

                    RefreshAllSortingOrders();
                    RefreshOilOverlays();
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    $"[Resolve] pass={safety} flow-fillable empty cells remain, but cascade produced no actions. " +
                    "Continuing to MatchFinder to avoid a hard lock.");
#endif
                // Güvenlik: board'u kilitleme. Normalde buraya düşmemeli;
                // CascadeLogic.HasAnyEmptyPlayableCell artık sadece flow-reachable boşlukları sayar.
            }

            // Hamle kalmadıysa cascade settle beklendi; yeni match aramaya gerek yok.
            // Ama uçan animasyonlar (PatchBot, EnergyBall) bitmeden çıkılmaz —
            // aksi hâlde hedef sayısı güncellenip popup açılmadan önce board idle olur.
            if (RemainingMoves <= 0 && ActiveBackgroundJobs == 0 && !actionSequencer.IsPlaying)
                yield break;

            var matches = matchFinder.FindAllMatches();

            if (matches.Count > 0)
            {
                var matchTiles = new HashSet<TileView>();
                var matchCells = new List<Vector2Int>();
                foreach (var t in matches)
                {
                    var tile = tiles[t.X, t.Y];
                    if (tile != null)
                    {
                        matchTiles.Add(tile);
                        matchCells.Add(new Vector2Int(t.X, t.Y));
                    }
                }

                if (matchTiles.Count > 0)
                {
                    Debug.Log($"[Resolve] pass={safety} cascade_match={matchTiles.Count} +{(Time.realtimeSinceStartup - _rbStart):0.000}s");

                    bool cleared = false;
                    yield return ExecuteClearPass(matchTiles, allowSpecialActivation, result => cleared = result);

                    if (cleared)
                    {
                        continue;
                    }
                }
            }

            var cascades = cascadeLogic.CalculateCascades();
            if (cascades.Count > 0)
            {
                DisableSettleIfMoreCascadesFollow(cascades);
                Debug.Log($"[Resolve] pass={safety} cascade_fall actions={cascades.Count} +{(Time.realtimeSinceStartup - _rbStart):0.000}s");
                actionSequencer.Enqueue(cascades);

                while (actionSequencer.IsPlaying)
                    yield return null;

                RefreshAllSortingOrders();
                RefreshOilOverlays();
                continue;
            }

            // Bottom-exit cargo: board oturduktan sonra alt satıra inen Cargo obstacle'ları
            // board'dan çıkar (goal +1) ve boşalan hücreleri refill için cascade'e bırak.
            if (TryCollectBottomExitCargo())
            {
                RefreshAllSortingOrders();
                continue;
            }

            if (!oilSpreadResolvedThisMove
                && oilSpreadService != null)
            {
                var spreadTargets = oilSpreadService.CalculateSpread(oilSuppressionCellsThisMove);
                oilSpreadResolvedThisMove = true;

                if (spreadTargets.Count > 0)
                {
                    Debug.Log($"[Oil] Spreading to {spreadTargets.Count} cells after board stabilized.");
                    actionSequencer.Enqueue(new OilSpreadAction(this, spreadTargets));

                    while (actionSequencer.IsPlaying)
                        yield return null;

                    RefreshOilOverlays();
                    continue;
                }
            }

            if (ActiveBackgroundJobs > 0 || actionSequencer.IsPlaying)
            {
                backgroundJobWaitTime += Time.deltaTime;

                if (backgroundJobWaitTime > 5f)
                {
                    Debug.LogWarning($"[ResolveBoard] Background job timeout — forcing continue. ActiveBackgroundJobs={ActiveBackgroundJobs}, IsPlaying={actionSequencer.IsPlaying}");
                    ActiveBackgroundJobs = 0;
                }

                yield return null;
                continue;
            }

            backgroundJobWaitTime = 0f;

            // ─────────────────────────────────────────────
            // Deadlock kontrolü:
            // Eğer board durmuşsa ve oynanabilir hiçbir swap yoksa
            // safe reshuffle yap, sonra resolve loop'una tekrar gir.
            // Special swap mümkünse HasAnyPlayableSwap() true döndürmeli.
            // ─────────────────────────────────────────────
            if (!matchFinder.HasAnyPlayableSwap())
            {
                Debug.Log($"[Resolve] pass={safety} deadlock_detected -> safe_shuffle +{(Time.realtimeSinceStartup - _rbStart):0.000}s");

                yield return boosterService.SafeShuffleBoardRoutine(boardInitService);

                // Shuffle sonrası board değiştiği için tekrar resolve et
                RefreshAllSortingOrders();
                continue;
            }

            break;
        }

        RestoreAllTilePresentation();
        RefreshAllSortingOrders();

        // Shake sonrası pozisyon kaymasını garanti et
        if (shakeTarget != null)
            shakeTarget.anchoredPosition = shakeBasePos;

        Debug.Log($"[Resolve] ═══ DONE ═══ passes={safety} total: {(Time.realtimeSinceStartup - _rbStart):0.000}s");
    }

    // Public wrapper for services (BoosterService)
    internal IEnumerator ResolveBoardPublic(bool allowSpecial = true) => ResolveBoard(allowSpecial, resolveEmptyCellsFirst: true);

    IEnumerator ExecuteClearPass(HashSet<TileView> matchTiles, bool allowSpecialActivation, Action<bool> onResult = null, Vector2Int? swapCell = null)
    {
        float _cpStart = Time.realtimeSinceStartup;
        float _cpLast = _cpStart;
        void CpLog(string step)
        {
            float now = Time.realtimeSinceStartup;
            Debug.Log($"[ClearPass] {step,-22} +{(now - _cpLast):0.000}s (total: {(now - _cpStart):0.000}s) matchCount={matchTiles.Count}");
            _cpLast = now;
        }
        // Kazanılmış special'lar normal match clear'ına girmez.
        // Sadece explicit activation veya effect-hit ile temizlenebilirler.
        var preservedSpecialTiles = new HashSet<TileView>();
        foreach (var tile in matchTiles)
        {
            if (tile != null && tile.GetSpecial() != TileSpecial.None)
                preservedSpecialTiles.Add(tile);
        }

        // Creation ve normal clear yalnızca non-special matched tiles üstünden çalışır.
        var nonSpecialMatchTiles = new HashSet<TileView>(matchTiles);
        nonSpecialMatchTiles.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);

        // ── 1. Special CREATION — artık en fazla 2 creation destekli ──
        var createdSpecialTiles = new List<TileView>();

        var creations = specialCreationService.DecideUpToTwoFromMatches(
            nonSpecialMatchTiles,
            new SpecialCreationService.CreationRequest(lastSwapA, lastSwapB, lastSwapUserMove));

        if (creations != null && creations.Count > 0)
        {
            foreach (var creation in creations)
            {
                if (!creation.hasValue || creation.winner == null)
                    continue;

                bool winnerAlreadySpecial = creation.winner.GetSpecial() != TileSpecial.None;
                if (winnerAlreadySpecial)
                    continue;

                // MovableObstacle tile'ına special atanamaz
                if (obstacleStateService != null
                    && obstacleStateService.IsMovableObstacleAt(creation.winner.X, creation.winner.Y))
                    continue;

                var created = specialResolver.ApplyCreatedSpecial(creation.winner, creation.special);
                if (created == null)
                    continue;

                createdSpecialTiles.Add(created);

                // Oluşan special kazanılmış haktır; normal clear listesinde kalmamalı.
                matchTiles.Remove(created);
                nonSpecialMatchTiles.Remove(created);
                preservedSpecialTiles.Add(created);

                // Special bu hücrede bir MATCH sonucu oluştu. Taş special'a dönüştüğü için
                // clear setinden çıkarıldı; ama match mantıksal olarak burada gerçekleşti —
                // hücrenin ALTINDAKİ obstacle (mud vb.) yine de bu match'ten hasar almalı.
                // Aksi hâlde "swap'la special oluşan hücredeki obstacle hiç vurulmadı" olur.
                if (obstacleStateService != null
                    && obstacleStateService.HasObstacleAt(created.X, created.Y)
                    && !obstacleStateService.IsMovableObstacleAt(created.X, created.Y))
                {
                    var createdCellContext = IsSpecialActivationPhase
                        ? ObstacleHitContext.SpecialActivation
                        : ObstacleHitContext.NormalMatch;
                    var createdCellHit = ApplyObstacleDamageAt(created.X, created.Y, createdCellContext);
                    if (createdCellHit.didHit)
                        TriggerObstacleVisualChange(createdCellHit.visualChange);
                }

                shakeNextClear = true;

                if (creation.special == TileSpecial.PatchBot)
                {
                    var fullGroup = matchFinder.FindMatchesAt(created.X, created.Y);
                    foreach (var pt in fullGroup)
                    {
                        if (pt == null || pt == created || pt.GetSpecial() != TileSpecial.None)
                            continue;

                        matchTiles.Add(pt);
                        nonSpecialMatchTiles.Add(pt);
                    }
                }
            }
        }

        lastSwapUserMove = false;
        CpLog($"special_creation({createdSpecialTiles.Count})");
        EmitCreatedSpecialSfx(createdSpecialTiles);

        matchTiles.RemoveWhere(t => t == null || t.GetSpecial() != TileSpecial.None);

        // ── 2. Special ACTIVATION — bu path yalnızca explicit effect kaynaklı zincirlerde kullanılmalı ──
        // Normal resolve/cascade sırasında matched set içindeki special'ları aktive etmiyoruz.
        bool hasLineActivation = false;
        bool hasAnySpecialActivation = false;

        if (matchTiles.Count == 0)
        {
            onResult?.Invoke(false);
            yield break;
        }

        bool doShake = shakeNextClear || hasLineActivation;
        shakeNextClear = false;

        ClearPresentationPlan presentationPlan =
            BuildCreatedSpecialPresentationPlan(createdSpecialTiles, matchTiles, doShake);

        CpLog($"pre_clear(tiles={matchTiles.Count} plan={presentationPlan != null})");

        // Swap cell varsa onu kullan, yoksa geometrik merkezi hesapla
        Vector2Int implodeCenter;
        if (swapCell.HasValue)
        {
            implodeCenter = swapCell.Value;
        }
        else
        {
            Vector2Int centerCell = Vector2Int.zero;
            int validTileCount = 0;
            foreach (var t in matchTiles)
            {
                if (t != null)
                {
                    centerCell.x += t.X;
                    centerCell.y += t.Y;
                    validTileCount++;
                }
            }
            if (validTileCount > 0)
            {
                centerCell.x = Mathf.RoundToInt((float)centerCell.x / validTileCount);
                centerCell.y = Mathf.RoundToInt((float)centerCell.y / validTileCount);
            }
            implodeCenter = centerCell;
        }

        actionSequencer.Enqueue(new MatchClearAction(
            matchTiles,
            doShake,
            isSpecialPhase: allowSpecialActivation && hasAnySpecialActivation,
            presentationPlan: presentationPlan,
            enqueueCascadeOnComplete: false,
            implodeTargetCell: implodeCenter));

        while (actionSequencer.IsPlaying)
            yield return null;

        CpLog("clear+cascade_done");
        onResult?.Invoke(true);
    }


    private void EmitCreatedSpecialSfx(List<TileView> createdSpecialTiles)
    {
        if (audioDirector == null || createdSpecialTiles == null || createdSpecialTiles.Count == 0)
            return;

        Dictionary<TileSpecial, int> counts = new Dictionary<TileSpecial, int>();

        for (int i = 0; i < createdSpecialTiles.Count; i++)
        {
            TileView tile = createdSpecialTiles[i];
            if (tile == null)
                continue;

            TileSpecial special = tile.GetSpecial();
            if (special == TileSpecial.None)
                continue;

            int current;
            counts.TryGetValue(special, out current);
            counts[special] = current + 1;
        }

        foreach (var kv in counts)
            audioDirector.Emit(BoardSfxRequest.SpecialCreate(kv.Key, kv.Value));
    }

    private ClearPresentationPlan BuildCreatedSpecialPresentationPlan(List<TileView> createdTiles, HashSet<TileView> clearTiles, bool doShake)
    {
        if (createdTiles == null || createdTiles.Count == 0 || clearTiles == null || clearTiles.Count == 0)
            return null;

        var validCreatedTiles = new List<TileView>();
        foreach (var tile in createdTiles)
        {
            if (tile != null)
                validCreatedTiles.Add(tile);
        }

        if (validCreatedTiles.Count == 0)
            return null;

        var plan = new ClearPresentationPlan
        {
            DoBoardShake = doShake,
            IncludeAdjacentOverTileBlockerDamage = true,
            ObstacleHitContext = IsSpecialActivationPhase
                ? ObstacleHitContext.SpecialActivation
                : ObstacleHitContext.NormalMatch
        };

        var finalClearTiles = new HashSet<TileView>(clearTiles);
        foreach (var created in validCreatedTiles)
            finalClearTiles.Remove(created);

        foreach (var created in validCreatedTiles)
        {
            var contributors = new List<TileView>();
            var contributorCells = new List<Vector2Int>();

            foreach (var tile in finalClearTiles)
            {
                if (tile == null)
                    continue;

                contributors.Add(tile);
                contributorCells.Add(new Vector2Int(tile.X, tile.Y));
            }

            if (contributors.Count == 0)
                continue;

            var createdGroup = created.GetComponent<CanvasGroup>();
            if (createdGroup == null)
                createdGroup = created.gameObject.AddComponent<CanvasGroup>();

            createdGroup.alpha = 0f;
            created.SetIconAlpha(0f);
            created.transform.localScale = Vector3.one * 0.18f;

            plan.Effects.Add(new SpecialCreationFormationEffectDescriptor(
                created,
                contributors,
                contributorCells,
                GetClearDurationForCurrentPass()));
        }

        foreach (var tile in finalClearTiles)
        {
            if (tile != null)
                plan.FinalClearTiles.Add(tile);
        }

        return plan.Effects.Count > 0 ? plan : null;
    }

    public IEnumerator ResolveInitial() { BeginBusy(); yield return ResolveBoard(false); EndBusy(); }

    internal IEnumerator ResolveEmptyPlayableCellsWithoutMatch()
    {
        var cascades = cascadeLogic.CalculateCascades();
        if (cascades.Count > 0)
        {
            actionSequencer.Enqueue(cascades);
            while (actionSequencer.IsPlaying) yield return null;
        }
    }
    // ═══════════════════════════════════════════════════════════════
    //  Obstacle Handling
    // ═══════════════════════════════════════════════════════════════
    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamage(ObstacleDamageRequest request)
    {
        var result = obstacleResolutionService != null
            ? obstacleResolutionService.ApplyDamage(request)
            : default;

        if (result.didHit && (result.visualChange.obstacleId == ObstacleId.Oil || result.stageTransition.obstacleId == ObstacleId.Oil))
        {
            oilSuppressionCellsThisMove.Add(request.cell);
        }

        return result;
    }

    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamageAt(
        int x,
        int y,
        ObstacleHitContext context)
    {
        return ApplyObstacleDamage(new ObstacleDamageRequest(x, y, context, null));
    }

    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamageAt(
        int x,
        int y,
        ObstacleHitContext context,
        TileType? sourceTileType)
    {
        return ApplyObstacleDamage(new ObstacleDamageRequest(x, y, context, sourceTileType));
    }

    public void TriggerObstacleVisualChange(ObstacleVisualChange change)
    {
        boardBreakFxService?.PlayObstacleBreak(change);
        ObstacleVisualChanged?.Invoke(change);
    }

    internal void MarkPatchBotForcedObstacleHit(int x, int y)
        => obstacleResolutionService?.MarkPatchBotForcedHit(x, y);

    internal void RaiseObstacleStageChanged(int originIndex, ObstacleStageSnapshot stage)
        => OnObstacleStageChanged?.Invoke(originIndex, stage);

    internal void RaiseObstacleCreatedDynamic(int x, int y)
        => OnObstacleCreatedDynamic?.Invoke(x, y);

    internal void SetHoleStateFromObstacle(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        holes[x, y] = IsMaskHole(x, y) || (obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y));
    }

    private void BindObstacleEvents()
    {
        if (obstacleStateService == null) return;
        obstacleStateService.OnObstacleDestroyed -= HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked -= HandleCellUnlocked;
        obstacleStateService.OnObstacleDestroyed += HandleObstacleDestroyed;
        obstacleStateService.OnCellUnlocked += HandleCellUnlocked;
        obstacleStateService.OnChestOpened -= HandleChestOpened;
        obstacleStateService.OnChestColorRemoved -= HandleChestColorRemoved;
        obstacleStateService.OnChestOpened += HandleChestOpened;
        obstacleStateService.OnChestColorRemoved += HandleChestColorRemoved;
        obstacleStateService.OnBatteryHit -= HandleBatteryHit;
        obstacleStateService.OnBatteryHit += HandleBatteryHit;
        obstacleStateService.OnWardrobeOpened -= HandleWardrobeOpened;
        obstacleStateService.OnWardrobeOpened += HandleWardrobeOpened;
        obstacleStateService.OnWardrobeItemRemoved -= HandleWardrobeItemRemoved;
        obstacleStateService.OnWardrobeItemRemoved += HandleWardrobeItemRemoved;
    }

    private void HandleWardrobeOpened(int originIndex)
        => OnWardrobeOpened?.Invoke(originIndex);

    private void HandleWardrobeItemRemoved(int originIndex, int itemsRemaining)
        => OnWardrobeItemRemoved?.Invoke(originIndex, itemsRemaining);

    private void HandleChestOpened(int originIndex)
        => OnChestOpened?.Invoke(originIndex);

    private void HandleChestColorRemoved(int originIndex, ChestColorMask removedColor)
        => OnChestColorRemoved?.Invoke(originIndex, removedColor);

    private void HandleBatteryHit(int originIndex, ChestColorMask color, int remaining)
        => OnBatteryHit?.Invoke(originIndex, color, remaining);

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        OnObstacleDestroyed?.Invoke(originIndex, obstacleId);

        int ox = originIndex % width;
        int oy = originIndex / width;
        if (ox < 0 || ox >= width || oy < 0 || oy >= height) return;

        if (obstacleId == ObstacleId.Oil)
        {
            tiles[ox, oy]?.SetCoveredByCellOverlay(false);
            return;
        }

        if (obstacleId == ObstacleId.Safe)
            return;

        // MovableObstacle kırıldığında o hücredeki tile'ı da yok et
        var tile = tiles[ox, oy];
        if (tile != null)
        {
            NotifyTilesCleared(tile.GetTileType(), 1);
            ClearAndDestroyTile(tile);
        }
    }

    private void HandleCellUnlocked(int cellIndex)
    {
        int x = cellIndex % width;
        int y = cellIndex / width;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        SetHoleStateFromObstacle(x, y);

        if (!holes[x, y])
            OnCellUnlocked?.Invoke(cellIndex);
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
    internal bool IsObstacleBlockedCell(int x, int y) => x >= 0 && x < width && y >= 0 && y < height && obstacleResolutionService != null && obstacleResolutionService.IsBlockedCell(x, y);
    internal bool IsSpawnPassThroughCell(int x, int y) => IsMaskHoleCell(x, y) && !IsObstacleBlockedCell(x, y);
    public bool TryGetCellState(int x, int y, out BoardCellStateSnapshot state) => BoardCellStateQuery.TryGet(this, x, y, out state);
    public bool HasAnyEmptyPlayableCell() { for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { if (holes[x, y]) continue; if (tiles[x, y] == null) return true; } return false; }

    // ═══════════════════════════════════════════════════════════════
    //  Timing / Utility
    // ═══════════════════════════════════════════════════════════════

    internal float GetFallDurationForDistance(int cellDistance)
    {
        int d = Mathf.Max(1, cellDistance);
        return GetFallDurationForMove(0, 0, 0, d);
    }

    internal float GetFallDurationForMove(int fromX, int fromY, int toX, int toY)
    {
        float distanceCells = Vector2.Distance(
            new Vector2(fromX, fromY),
            new Vector2(toX, toY));

        return GetFallDurationForDistanceCells(distanceCells);
    }

    internal float GetFallDurationForDistanceCells(float distanceCells)
    {
        float d = Mathf.Max(0.0001f, distanceCells);
        float duration = d / FallVelocityCellsPerSecond;

        return Mathf.Max(0.01f, duration * Mathf.Max(0.5f, GetCascadeFallSpeedMultiplier()));
    }

    internal float GetClearDurationForCurrentPass() => Mathf.Max(0.03f, ApplySpecialChainTempo(ClearDuration * GetCascadeClearSpeedMultiplier()));
    internal bool ShouldEnableFallSettleThisPass() => EnableFallSettle;

    // Su gibi akış: settle (iniş bounce'u) yalnızca GERÇEK SON inişte oynamalı.
    // CalculateCascades logical board'u final pozisyona güncellediği için, bu düşüşten
    // sonra hâlâ match veya doldurulacak boşluk varsa bu bir ara cascade'dir → settle kapat.
    // Böylece taşlar her ara inişte zıplayıp durmaz, akış kesilmez.
    private void DisableSettleIfMoreCascadesFollow(List<BoardAction> cascades)
    {
        if (cascades == null || cascades.Count == 0)
            return;

        if (!EnableFallSettle)
            return;

        bool moreComing =
            (matchFinder != null && matchFinder.FindAllMatches().Count > 0)
            || (cascadeLogic != null && cascadeLogic.HasAnyEmptyPlayableCell());

        if (!moreComing)
            return;

        foreach (var action in cascades)
            if (action is FallAction fa)
                fa.DisableSettle();
    }

    // En alt satıra inen Cargo (exitAtBottom) obstacle'larını board'dan çıkarır:
    // hücre verisini temizler (OnObstacleDestroyed → goal +1), tile'ı tabandan aşağı
    // animasyonla süzer. En az biri toplandıysa true döner (resolve loop refill etsin).
    internal bool TryCollectBottomExitCargo()
    {
        if (obstacleStateService == null || height <= 0 || width <= 0)
            return false;

        int by = height - 1;
        bool any = false;

        for (int x = 0; x < width; x++)
        {
            if (!obstacleStateService.IsExitAtBottomAt(x, by))
                continue;

            var tile = tiles[x, by];

            // Goal HUD slotunu önceden al (robot oraya zıplayacak).
            RectTransform goalSlot = null;
            var id = obstacleStateService.GetObstacleIdAt(x, by);
            TopHud?.TryGetGoalTargetRectForObstacle(id, out goalSlot);

            // Tile'ı önce ayır: CollectExitObstacleAt → OnObstacleDestroyed → HandleObstacleDestroyed
            // bu hücredeki tile'ı da yok etmeye çalışmasın (biz ayrıca animasyonla çıkarıyoruz).
            tiles[x, by] = null;
            gridData[x, by] = null;

            var collected = obstacleStateService.CollectExitObstacleAt(x, by);
            if (collected == ObstacleId.None)
            {
                tiles[x, by] = tile;   // beklenmedik: geri koy
                continue;
            }

            SetHoleStateFromObstacle(x, by);

            if (tile != null)
                StartCoroutine(CargoExitRoutine(tile, goalSlot));

            any = true;
        }

        return any;
    }

    // İşçi robot çıkışı: bir hücre aşağı düşer → diz büküp yere konar → TopHUD goal slotuna zıplar.
    // Animasyon boyunca board "busy" tutulur ki goal/level-end değerlendirmesi bunu beklesin.
    private IEnumerator CargoExitRoutine(TileView tile, RectTransform goalSlot)
    {
        BeginBackgroundJob();
        try
        {
            var fly = GoalFlyFx;
            if (fly != null && tile != null && tile)
            {
                // PlayCargoExit ilk yield'e kadar sprite + pozisyonu senkron yakalar;
                // hemen sonrasında gerçek tile'ı gizleyip ghost'a devrediyoruz (çift robot olmasın).
                var running = StartCoroutine(fly.PlayCargoExit(tile, goalSlot, 0.42f));
                if (tile != null && tile)
                    tile.gameObject.SetActive(false);
                yield return running;
            }
        }
        finally
        {
            if (tile != null && tile)
                Destroy(tile.gameObject);
            EndBackgroundJob();
        }
    }

    private float GetCascadeFallSpeedMultiplier()
    {
        // Eski: pass 2+ = 0.50 → taşlar 2x hızlı → "pat pat"
        // Yeni: kademeli hızlanma, asla 2x'i geçmez
        if (CurrentResolvePass <= 1) return 1f;
        if (CurrentResolvePass <= 2) return 0.85f;
        if (CurrentResolvePass <= 4) return 0.75f;
        return 0.70f;
    }

    private float GetCascadeClearSpeedMultiplier()
    {
        if (CurrentResolvePass <= 1) return 1f;
        if (CurrentResolvePass <= 2) return 0.80f;
        return 0.70f;
    }
    private TileType GetRandomType() => randomPool[UnityEngine.Random.Range(0, randomPool.Length)];
    private void ConsumeMove() { RemainingMoves = Mathf.Max(0, RemainingMoves - 1); OnMovesChanged?.Invoke(RemainingMoves); }
    public void AddMoves(int amount) { if (amount <= 0) return; RemainingMoves += amount; OnMovesChanged?.Invoke(RemainingMoves); }
    internal void ConsumeBonusMove() => ConsumeMove();

    internal Coroutine StartBonusLinesRoutine(System.Collections.Generic.List<BonusLinePlacement> placements)
    {
        if (boosterService == null || tiles == null || placements == null || placements.Count == 0) return null;
        BeginBusy();
        return StartCoroutine(BonusLinesRoutineInternal(placements));
    }

    private IEnumerator BonusLinesRoutineInternal(System.Collections.Generic.List<BonusLinePlacement> placements)
    {
        try
        {
            IsSpecialActivationPhase = true;

            var matches       = new HashSet<TileView>();
            var affectedCells = new HashSet<Vector2Int>();
            var visualTargets = new HashSet<TileView>();
            var strikes       = new System.Collections.Generic.List<LightningLineStrike>(placements.Count);
            var chainStrikes  = new System.Collections.Generic.List<LightningLineStrike>();

            foreach (var p in placements)
            {
                if (p.isHorizontal)
                {
                    boosterService.AddRow(matches, p.y);
                    boosterService.AddRowCells(affectedCells, p.y);
                }
                else
                {
                    boosterService.AddColumn(matches, p.x);
                    boosterService.AddColumnCells(affectedCells, p.x);
                }
                strikes.Add(new LightningLineStrike(new Vector2Int(p.x, p.y), p.isHorizontal));
            }

            if (matches.Count > 0 || affectedCells.Count > 0)
            {
                visualTargets = new HashSet<TileView>(matches);

                specialResolver.ExpandSpecialChain(
                    matches, affectedCells,
                    out _, out _,
                    lightningVisualTargets: visualTargets,
                    lightningLineStrikes: chainStrikes);
                strikes.AddRange(chainStrikes);
                visualTargets.UnionWith(matches);

                // presentationPlan:null → ClearMatchesAnimated → PlayLightningLineStrikes
                // (same path as normal LineV/H activation, works on iPhone).
                // lineHitClearedTiles inside ClearMatchesAnimated prevents double-clear
                // at row/column intersections when multiple strikes sweep the same cell.
                var action = new MatchClearAction(
                    matches,
                    doShake: true,
                    animationMode: ClearAnimationMode.LightningStrike,
                    affectedCells: affectedCells,
                    obstacleHitContext: ObstacleHitContext.Booster,
                    includeAdjacentOverTileBlockerDamage: false,
                    lightningVisualTargets: new System.Collections.Generic.List<TileView>(visualTargets),
                    lightningLineStrikes: strikes,
                    isSpecialPhase: true,
                    presentationPlan: null,
                    enqueueCascadeOnComplete: true);

                actionSequencer.Enqueue(action);

                while (actionSequencer.IsPlaying)
                    yield return null;

                yield return ResolveBoardPublic();
            }
        }
        finally
        {
            IsSpecialActivationPhase = false;
            EndBusy();
        }
    }

    private MatchClearAction BuildBonusLineClearAction(
        HashSet<TileView> matches,
        HashSet<Vector2Int> affectedCells,
        HashSet<TileView> visualTargets,
        System.Collections.Generic.List<LightningLineStrike> strikes)
    {
        var presentationPlan = new ClearPresentationPlan();
        presentationPlan.DoBoardShake = true;
        presentationPlan.IncludeAdjacentOverTileBlockerDamage = false;
        presentationPlan.ObstacleHitContext = ObstacleHitContext.Booster;

        var targetTiles = new System.Collections.Generic.List<TileView>(visualTargets);
        var targetCells = new System.Collections.Generic.List<Vector2Int>(affectedCells);

        presentationPlan.Effects.Add(new LineSweepEffectDescriptor(
            targetTiles,
            targetCells,
            strikes,
            originTile: null,
            originCell: null));

        foreach (var tile in matches)
        {
            if (tile != null)
                presentationPlan.FinalClearTiles.Add(tile);
        }

        return new MatchClearAction(
            matches,
            doShake: true,
            animationMode: ClearAnimationMode.Default,
            affectedCells: affectedCells,
            obstacleHitContext: ObstacleHitContext.Booster,
            includeAdjacentOverTileBlockerDamage: false,
            isSpecialPhase: true,
            presentationPlan: presentationPlan,
            enqueueCascadeOnComplete: true);
    }
    internal void NotifyTilesCleared(TileType tileType, int amount) { if (amount > 0) { OnTilesCleared?.Invoke(tileType, amount); GameEventBus.EmitTileCleared(tileType, amount); } }

    internal void SyncAllTilesToGridData() { for (int sy = 0; sy < height; sy++) for (int sx = 0; sx < width; sx++) if (tiles[sx, sy] != null) SyncTileData(sx, sy); }
    private IEnumerator AnimateQueuedActions() { while (actionSequencer.IsPlaying) yield return null; }

    public Vector3 GetCellWorldPosition(int x, int y)
    {
        if (parent != null) return parent.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
        return transform.TransformPoint(new Vector3(x * tileSize, -y * tileSize, 0f));
    }

    public Vector3 GetCellWorldCenterPosition(int x, int y)
    {
        Vector3 localCenter = new Vector3(x * tileSize + tileSize * 0.5f, -y * tileSize - tileSize * 0.5f, 0f);
        if (parent != null)
            return parent.TransformPoint(localCenter);

        return transform.TransformPoint(localCenter);
    }

    internal Vector2 WorldToAnchoredIn(RectTransform targetParent, Vector3 worldPos)
    {
        if (targetParent == null) return Vector2.zero;

        return targetParent.InverseTransformPoint(worldPos);
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
    private IEnumerator EnsurePlayableBoardAfterSettleRoutine()
    {
        if (matchFinder == null || boosterService == null || boardInitService == null)
            yield break;

        const int maxShuffleRetries = 4;

        for (int attempt = 0; attempt < maxShuffleRetries; attempt++)
        {
            // HasAnyPlayableSwap zaten:
            // - normal match yaratacak swap'ı
            // - special tile varsa onun komşu swap'ını
            // birlikte kontrol ediyor
            if (matchFinder.HasAnyPlayableSwap())
                yield break;

            yield return boosterService.SafeShuffleBoardRoutine(boardInitService);

            SyncAllTilesToGridData();
            RefreshAllTileObstacleVisuals();
            RefreshAllSortingOrders();

            if (matchFinder.HasAnyPlayableSwap())
                yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[Board] No playable move found even after safe shuffle retries.");
#endif
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
