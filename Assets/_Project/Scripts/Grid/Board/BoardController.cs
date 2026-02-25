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
    [SerializeField] private int spawnStartOffsetY = -2; // UI'da negatif yukarı

    [Header("Movement Feel")]
    [Tooltip("Swap animasyonunda süre çarpanı. 1'den büyük değerler hareketi daha akışkan hissettirir.")]
    [SerializeField] private float swapDurationMultiplier = 1f;
    [SerializeField] private float fallColumnStep = 0.015f;

    [Tooltip("Düşme/yeniden dolma animasyonunda süre çarpanı. 1'den büyük değerler daha yumuşak bir akış sağlar.")]
    [SerializeField] private float fallDurationMultiplier = 1f;

    [Tooltip("Swap easing eğrisi. 0→1 zamanını pozisyon ilerlemesine map eder.")]
    [SerializeField] private AnimationCurve swapMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Fall/Spawn easing eğrisi. 0→1 zamanını pozisyon ilerlemesine map eder.")]
    [SerializeField] private AnimationCurve fallMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall Settle")]
    [Tooltip("Düşme hareketi tamamlandıktan sonra kısa squash-and-return settle etkisini açar.")]
    [SerializeField] private bool enableFallSettle;

    [Tooltip("Settle fazı süresi (sn).")]
    [SerializeField] private float fallSettleDuration = 0.06f;

    [Tooltip("Settle squash gücü. 0.04 => (1.04, 0.96, 1)")]
    [SerializeField] private float fallSettleStrength = 0.04f;

    [Tooltip("Aynı kolondaki taşların düşüş başlangıcı arasındaki kademeli gecikme (sn). 0 => hepsi aynı anda başlar.")]
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
    [Tooltip("PulseCore etkisi sırasında hücreler arası gecikme (Manhattan dist * step)")]
    [SerializeField] private float pulseImpactDelayStep = 0.02f;

    [Tooltip("PulseCore etkisi sırasında TileView.PlayPulseImpact toplam animasyon süresi")]
    [SerializeField] private float pulseImpactAnimTime = 0.16f;

    [Header("Board VFX/SFX")]
    [FormerlySerializedAs("pulseCoreVfxPlayer")][SerializeField] private PulseCoreVfxPlayer boardVfxPlayer;
    [SerializeField] private LightningSpawner lightningSpawner;

    [Header("HUD / Goal Fly FX")]
    [SerializeField] private TopHudController topHud;
    [SerializeField] private GoalFlyFx goalFlyFx;

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

    [Header("Combo VFX")]
    [SerializeField] private OverrideComboController systemOverrideComboVfx;   
    [SerializeField] private PulseEmitterComboController pulseEmitterComboVfx;
    [SerializeField] private RectTransform vfxSpace; // VFXPlayer RectTransform sürükleyeceksin

    [SerializeField]private GameObject pulsePulseExplosionPrefab; // <-- EKLE
    [SerializeField] private float pulsePulseExplosionLifetime = 1.0f; // <-- EKLE (0.6 - 1.2 arası ok)

    [Header("Obstacle Visual Tuning")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxPulseCoreBoom;
    [SerializeField] private AudioClip sfxPulseCoreWave;
    [SerializeField] private bool enablePulseMicroShake;
    [SerializeField] private float pulseMicroShakeDuration = 0.08f;
    [SerializeField] private float pulseMicroShakeStrength = 4f;

    private Vector2 shakeBasePos;

    private TileView[,] tiles;
    private bool[,] holes;
    private bool[,] maskHoles;

    private int width;
    private int height;

    private TileView selected;
        // Busy/idle tracking
    private int busyCount = 0;
    private BoosterMode activeBooster = BoosterMode.None;

    private GameObject tilePrefab;
    private RectTransform parent;
    private int tileSize;

    public int TileSize => tileSize;
    public bool IsBusy => busyCount > 0;

    // Fired when board becomes fully idle (no resolves/animations in progress)
    public event Action OnBecameIdle;

    private void BeginBusy()
    {
        busyCount++;
    }

    private void EndBusy()
    {
        busyCount = Mathf.Max(0, busyCount - 1);
        if (busyCount == 0)
            OnBecameIdle?.Invoke();
    }

    /// <summary>
    /// Runs an action after the board finishes all current resolve/animation work.
    /// If the board is already idle, runs immediately.
    /// </summary>
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

        // extra safety: let any queued coroutines start this frame
        yield return null;

        action();
    }

    public bool InputLocked { get; private set; }
    public int RemainingMoves { get; private set; }
    public LevelData ActiveLevelData => levelData;

    public event Action<int, ObstacleStageSnapshot> OnObstacleStageChanged;
    public event Action<int, ObstacleId> OnObstacleDestroyed;
    public event Action<int> OnCellUnlocked;
    public event Action<int> OnMovesChanged;
    public event Action<TileType, int> OnTilesCleared;
    public event Action<bool> OnBoosterTargetingChanged;

    // Swap sonrası power üretmek için
    private TileView lastSwapA;
    private TileView lastSwapB;
    private bool lastSwapUserMove;
    // Bu tur temizlemede shake olsun mu? (4+ veya power)
    private bool shakeNextClear;
    private bool isSpecialActivationPhase;

    private TileType[] randomPool;

    private MatchFinder matchFinder;
    private SpecialResolver specialResolver;
    private BoardAnimator boardAnimator;
    private PendingCreationService pendingCreationService;
    private PulseCoreImpactService pulseCoreImpactService;
    private ObstacleStateService obstacleStateService;
    private readonly List<Vector3> lightningTargetPositionsBuffer = new List<Vector3>(32);
    private bool didLogMissingLightningSpawner;
    private readonly HashSet<int> patchBotForcedObstacleHits = new();

    public event System.Action<ObstacleVisualChange> ObstacleVisualChanged;

    internal TileView[,] Tiles => tiles;
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

    private void Awake()
    {
        if (shakeTarget != null)
            shakeBasePos = shakeTarget.anchoredPosition;
        EnsureServices();
        TryResolveLightningSpawner();
        EnsureGoalFlyFx();

        if (lightningSpawner == null && !didLogMissingLightningSpawner)
        {
            didLogMissingLightningSpawner = true;
            Debug.LogWarning("[Lightning][BoardController] lightningSpawner reference is not assigned and auto-resolve failed. Assign it in inspector or place LightningSpawner under BoardController.");
        }
    }

    /// <summary>
    /// Ensures GoalFlyFx + its OverlayRoot exists under a Canvas.
    /// This is designed to work across all levels/scenes without manual scene wiring.
    /// </summary>
    private void EnsureGoalFlyFx()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        // 1) Overlay root
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

        // Keep it on top so ghosts are visible over HUD/board.
        overlay.SetAsLastSibling();

        // 2) GoalFlyFx instance
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

        // 3) Wire overlay root into GoalFlyFx without requiring inspector access.
        // Uses SendMessage so GoalFlyFx doesn't have to expose a public setter.
        if (goalFlyFx != null)
        {
            goalFlyFx.gameObject.SendMessage("SetOverlayRoot", overlay, SendMessageOptions.DontRequireReceiver);
        }
    }

    public void PlaySystemOverrideComboVfx()
    {
        if (systemOverrideComboVfx == null) return;
        systemOverrideComboVfx.gameObject.SetActive(true); 
        systemOverrideComboVfx.Play();
    }

    public void PlayPulseEmitterComboVfxAtCell(int x, int y)
    {
        if (pulseEmitterComboVfx == null)
        {
            Debug.LogError("[PulseEmitterCombo] pulseEmitterComboVfx is NULL (Inspector assign missing)");
            return;
        }

        if (vfxSpace == null)
        {
            Debug.LogError("[PulseEmitterCombo] vfxSpace is NULL (assign Board VFX space RectTransform)");
            return;
        }

        pulseEmitterComboVfx.gameObject.SetActive(true);

        // 1) Swap midpoint (en doğru: LastSwapA/B)
        TileView ta = lastSwapA;
        TileView tb = lastSwapB;

        // Fallback: (x,y) hücresi
        if (ta == null || tb == null)
        {
            ta = (x >= 0 && x < Width && y >= 0 && y < Height) ? tiles[x, y] : null;
            tb = ta;
        }

        Vector3 worldA = GetTileWorldCenter(ta);
        Vector3 worldB = GetTileWorldCenter(tb);
        Vector3 worldMid = (worldA + worldB) * 0.5f;

        // 2) Midpoint'i vfxSpace local anchored pos'a çevir
        Vector2 localMid = (Vector2)vfxSpace.InverseTransformPoint(worldMid);

        // 3) Board size (grid değişse bile)
        Vector2 boardSize = vfxSpace.rect.size; // en güvenlisi: gerçek UI alanı
        if (boardSize.sqrMagnitude < 1f)
            boardSize = new Vector2(Width * TileSize, Height * TileSize);

        pulseEmitterComboVfx.SetTileSize(TileSize);

        // Artık Vector2.zero değil, swap midpoint
        pulseEmitterComboVfx.PlayAt(localMid, boardSize);

    }

    public void PlayPulsePulseExplosionVfxAtCell(int x, int y)
    {
        if (pulsePulseExplosionPrefab == null)
        {
            Debug.LogError("[PulsePulseCombo] pulsePulseExplosionPrefab is NULL (Inspector assign missing)");
            return;
        }

        if (vfxSpace == null)
        {
            Debug.LogError("[PulsePulseCombo] vfxSpace is NULL (assign VFXPlayer RectTransform)");
            return;
        }

        // Swap midpoint (tercih edilen)
        TileView ta = lastSwapA;
        TileView tb = lastSwapB;

        // Fallback: (x,y) hücresi
        if (ta == null || tb == null)
        {
            ta = (x >= 0 && x < Width && y >= 0 && y < Height) ? tiles[x, y] : null;
            tb = ta;
        }

        Vector3 worldA = GetTileWorldCenter(ta);
        Vector3 worldB = GetTileWorldCenter(tb);
        Vector3 worldMid = (worldA + worldB) * 0.5f;

        // vfxSpace local (anchored) konuma çevir
        Vector2 localMid = (Vector2)vfxSpace.InverseTransformPoint(worldMid);

        var go = Instantiate(pulsePulseExplosionPrefab, vfxSpace);
        go.SetActive(true);

        // UI prefab varsayımı: RectTransform
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = localMid;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            // World-space prefab fallback
            go.transform.position = worldMid;
        }

        // Efekt bitince temizle
        Destroy(go, pulsePulseExplosionLifetime);

    }

    public Vector3 GetTileWorldCenter(TileView tile)
    {
        if (tile == null) return Vector3.zero;

        var rt = tile.GetComponent<RectTransform>();
        if (rt != null)
            return rt.TransformPoint(rt.rect.center);   // pivot ne olursa olsun gerçek merkez

        return tile.transform.position;
    }

    private void TryResolveLightningSpawner()
    {
        if (lightningSpawner != null)
            return;

        lightningSpawner = GetComponentInChildren<LightningSpawner>(true);
        if (lightningSpawner == null && transform.parent != null)
            lightningSpawner = transform.parent.GetComponentInChildren<LightningSpawner>(true);

    }

    void EnsureServices()
    {
        matchFinder ??= new MatchFinder(this);
        boardAnimator ??= new BoardAnimator(this);
        pulseCoreImpactService ??= new PulseCoreImpactService(this, boardAnimator);
        specialResolver ??= new SpecialResolver(this, matchFinder, boardAnimator, pulseCoreImpactService);
        pendingCreationService ??= new PendingCreationService(this, matchFinder, specialResolver);
        obstacleStateService ??= new ObstacleStateService();
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


    internal bool ApplyObstacleDamageAt(int x, int y, ObstacleHitContext context)
    {
        if (obstacleStateService == null) return false;

        bool patchBotForcedHit = ConsumePatchBotForcedObstacleHit(x, y);

        var result = obstacleStateService.TryDamageAt(x, y, context);

        if (PatchBotDebugLogging)
            Debug.Log($"[PatchBotDebug][ApplyObstacleDamageAt] primaryTry didHit={result.didHit} rejectedByRule={result.rejectedByRule}");

        ObstacleStateService.ObstacleHitResult TryFallback(ObstacleHitContext fallbackContext)
        {
            if (fallbackContext == context)
                return default;

            return obstacleStateService.TryDamageAt(x, y, fallbackContext);
        }

        // Booster jokers (hammer/row/column) ürün kuralı gereği en güçlü etki olmalı:
        // ilk context reddedilirse bile aynı hücrede alternatif context'lerle tekrar dene.
        // Böylece yolundaki obstacle, stage rule'u ne olursa olsun en az bir hasar alır.
        if (!result.didHit && context == ObstacleHitContext.Booster)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.NormalMatch);
        }
        else if (!result.didHit && patchBotForcedHit)
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.NormalMatch);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.Scripted);
        }
        else if (!result.didHit && IsCrossContextFallbackAllowedAt(x, y))
        {
            result = TryFallback(ObstacleHitContext.SpecialActivation);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.Booster);
            if (!result.didHit)
                result = TryFallback(ObstacleHitContext.NormalMatch);
        }

        if (!result.didHit)
        {
            if (PatchBotDebugLogging)
                Debug.LogWarning($"[PatchBotDebug][ApplyObstacleDamageAt] NO_HIT cell=({x},{y}) context={context} forcedConsumed={patchBotForcedHit}");
            return false;
        }

        if (PatchBotDebugLogging)
            Debug.Log($"[PatchBotDebug][ApplyObstacleDamageAt] HIT cell=({x},{y}) cleared={result.visualChange.cleared} remaining={result.visualChange.remainingHits} spriteNull={(result.visualChange.sprite == null)}");

        ConsumeObstacleStageTransition(result);

        ObstacleVisualChanged?.Invoke(result.visualChange);
        return true;
    }

    internal void MarkPatchBotForcedObstacleHit(int x, int y)
    {
        if (obstacleStateService == null || !obstacleStateService.HasObstacleAt(x, y))
            return;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        patchBotForcedObstacleHits.Add(y * width + x);
    }

    private bool ConsumePatchBotForcedObstacleHit(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        int index = y * width + x;
        if (!patchBotForcedObstacleHits.Contains(index))
            return false;

        patchBotForcedObstacleHits.Remove(index);
        return true;
    }

    private void ConsumeObstacleStageTransition(ObstacleStateService.ObstacleHitResult result)
    {
        if (!result.stageTransition.hasTransition)
            return;

        if (!result.stageTransition.cleared)
            OnObstacleStageChanged?.Invoke(result.stageTransition.originIndex, result.stageTransition.currentStage);

        var affected = result.affectedCellIndices;
        if (affected == null || affected.Length == 0)
            return;

        for (int i = 0; i < affected.Length; i++)
        {
            int cellIndex = affected[i];
            int x = cellIndex % width;
            int y = cellIndex / width;
            if (x < 0 || x >= width || y < 0 || y >= height)
                continue;

            bool isMaskHole = IsMaskHole(x, y);
            bool blockedByObstacle = obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
            holes[x, y] = isMaskHole || blockedByObstacle;
        }
    }

    private bool IsCrossContextFallbackAllowedAt(int x, int y)
    {
        if (levelData == null || levelData.obstacles == null || levelData.obstacleOrigins == null)
            return false;

        if (!levelData.InBounds(x, y))
            return false;

        int idx = levelData.Index(x, y);
        if (idx < 0 || idx >= levelData.obstacles.Length)
            return false;

        var obstacleId = (ObstacleId)levelData.obstacles[idx];
        if (obstacleId == ObstacleId.None)
            return false;

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

        int nx = from.X + dirX;
        int ny = from.Y + dirY;

        if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
        if (holes[nx, ny]) return;

        TileView other = tiles[nx, ny];
        if (other == null) return;

        StartCoroutine(ProcessSwap(from, other));
    }

    public void SetHole(int x, int y, bool isHole) => holes[x, y] = isHole;

    public Sprite GetIcon(TileType type) => iconLibrary != null ? iconLibrary.Get(type) : null;
    public Sprite GetSpecialIcon(TileSpecial special) => iconLibrary != null ? iconLibrary.GetSpecialIcon(special) : null;

    public void RegisterTile(TileView tile, int x, int y)
    {
        tiles[x, y] = tile;
        tile.Init(this, x, y);
        tile.SetCoords(x, y);
        tile.SnapToGrid(tileSize);
    }

    internal void RefreshTileObstacleVisual(TileView tile)
    {
        if (tile == null)
            return;

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
            case 3:
                // Shuffle da diğer jokerlar gibi hedefleme moduna girsin;
                // oyuncu board'a tıklayınca tetiklensin ki vazgeçebilsin.
                SetBoosterMode(BoosterMode.Shuffle);
                break;
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
        if (activeBooster == BoosterMode.None)
            return false;

        if (tile == null)
        {
            return true;
        }

        if (IsBusy || InputLocked)
        {
            return true;
        }

        return TryUseBoosterAtCell(tile.X, tile.Y);
    }

    public bool TryUseBoosterAtCell(int x, int y)
    {
        if (activeBooster == BoosterMode.None)
            return false;

        if (IsBusy || InputLocked)
        {
            return true;
        }

        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return true;
        }

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
                if (target != null)
                    matches.Add(target);

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
            specialResolver.ExpandSpecialChain(matches, affectedCells, out hasLineActivation, out _);

            var animationMode = (mode == BoosterMode.Row || mode == BoosterMode.Column)
                ? ClearAnimationMode.LightningStrike
                : ClearAnimationMode.Default;

            if (hasLineActivation)
                animationMode = ClearAnimationMode.LightningStrike;

            // Product kararı: hedef seçerek tetiklenen booster hamlesi (emitter dahil)
            // obstacle hasarı açısından "Booster" sayılır; stageDamageRules'ta BoosterOnly/Any ile yönetilir.
            ObstacleHitContext obstacleHitContext = ObstacleHitContext.Booster;

            // Row/Column booster etkisinde yalnızca hedeflenen hat hasar almalı.
            // Komşu over-tile blocker ek hasarı, sütun/satır tetiklemelerinde
            // beklenmeyen fazla stage düşüşüne sebep olabiliyor.
            bool includeAdjacentOverTileBlockerDamage = false;
            yield return boardAnimator.ClearMatchesAnimated(matches, doShake: true, animationMode: animationMode, affectedCells: affectedCells, obstacleHitContext: obstacleHitContext, includeAdjacentOverTileBlockerDamage: includeAdjacentOverTileBlockerDamage, lightningOriginTile: target, lightningOriginCell: targetCell, lightningVisualTargets: initialLightningTargets );
            yield return boardAnimator.CollapseAndSpawnAnimated();
            yield return ResolveBoard();
        }

        isSpecialActivationPhase = false;
        EndBusy();
    }

    internal float PlayLightningStrikeForTiles(
        IReadOnlyCollection<TileView> matches,
        TileView originTile = null,
        Vector2Int? fallbackOriginCell = null,
        IReadOnlyCollection<TileView> visualTargets = null)
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

        if (matches == null || matches.Count == 0)
        {
            return 0f;
        }

        var targetsForVisuals = visualTargets ?? matches;

        // 1) Origin’i önce belirle (hedef listesini doldurmadan)
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
            // İlk non-null hedefi origin kabul et
            originWorldPos = default;
            bool found = false;
            foreach (var t in targetsForVisuals)
            {
                if (t == null) continue;
                originWorldPos = GetTileWorldCenter(t);
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogWarning("[Lightning][BoardController] No valid lightning origin found, skipping.");
                return 0f;
            }
        }

        // 2) Hedefleri doldur (origin’e çok yakın olanları ve duplicate’leri çıkar)
        lightningTargetPositionsBuffer.Clear();

        const float kMinDistFromOrigin = 0.05f; // tile aralığın ~0.5 ise bu güvenli
        float minDistSqr = kMinDistFromOrigin * kMinDistFromOrigin;

        foreach (var tile in targetsForVisuals)
        {
            if (tile == null) continue;

            var p = GetTileWorldCenter(tile);

            // origin’e “kendine çakma” çizgisi üretmesin
            if ((p - originWorldPos).sqrMagnitude <= minDistSqr)
                continue;

            // basit duplicate filtresi (aynı noktaya birden fazla target gelirse)
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
                lightningTargetPositionsBuffer.Add(p);
        }

    // Eğer hepsini filtrelediysek (ör. tek hedef origin’in kendisiydi), en az 1 hedef bırak
    if (lightningTargetPositionsBuffer.Count == 0)
    {
        foreach (var t in targetsForVisuals)
        {
            if (t == null) continue;
            lightningTargetPositionsBuffer.Add(GetTileWorldCenter(t));
            break;
        }
    }

    if (lightningTargetPositionsBuffer.Count == 0)
    {
        return 0f;
    }

    float playbackDuration = lightningSpawner.GetPlaybackDuration(lightningTargetPositionsBuffer.Count);
    if (playbackDuration <= 0f)
    {
        playbackDuration = MinLightningLeadTime;
        Debug.LogWarning($"[Lightning][BoardController] Spawner playbackDuration was <= 0. Using fallback lead time {playbackDuration:0.000}s.");
    }

    lightningSpawner.PlayEmitterLightning(originWorldPos, lightningTargetPositionsBuffer);
    return playbackDuration;
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
            tile.SetSpecial(TileSpecial.None);
            tile.SetType(types[i]);
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
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        if (!holes[x, y])
            return true;

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

        yield return boardAnimator.SwapTilesAnimated(a, b, SwapDurationWithMultiplier );

        pendingCreationService.Clear();
        if (pendingCreationService.CapturePendingCreation(a, b))
        {
        }

        // Power swap: zaten “big” his -> shake
        TileSpecial sa = a.GetSpecial();
        TileSpecial sb = b.GetSpecial();

        if (sa != TileSpecial.None || sb != TileSpecial.None)
        {
            ConsumeMove();
            yield return specialResolver.ResolveSpecialSwap(a, b );
            if (pendingCreationService.HasPending)
            {
                pendingCreationService.ApplyPendingCreations();
                yield return boardAnimator.CollapseColumnsAnimated();
            }
            yield return ResolveBoard();
            EndBusy();
            yield break;
        }

        // Sadece swaplanan iki taşın etrafında match ara
        var matches = new HashSet<TileView>();
        foreach (var t in matchFinder.FindMatchesAt(a.X, a.Y)) matches.Add(t);
        foreach (var t in matchFinder.FindMatchesAt(b.X, b.Y)) matches.Add(t);

        if (matches.Count == 0)
        {
            matchFinder.Add2x2Candidates(matches, a.X, a.Y);
            matchFinder.Add2x2Candidates(matches, b.X, b.Y);
        }

        if (matches.Count == 0)
        {
            yield return boardAnimator.SwapTilesAnimated(a, b, SwapDurationWithMultiplier );
            EndBusy();
            yield break;
        }

        // Bu swap sonucu 4+ oluştuysa, ilk clear’da shake olsun
        shakeNextClear = matchFinder.HasAnyRunAtLeast(4);
        ConsumeMove();

        yield return ResolveBoard();
        EndBusy();
    }


    private bool HasAnyEmptyPlayableCell()
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (holes[x, y]) continue; // hole or blocked cell (no tile allowed)
            if (tiles[x, y] == null) return true;
        }
        return false;
    }

    // Duration helpers: distance-based + cascade speed-up on pass>=2
    internal float GetFallDurationForDistance(int cellDistance)
    {
        // base duration already tuned for "typical" falls; scale by distance but clamp.
        float baseDur = FallDurationWithMultiplier;
        int d = Mathf.Max(1, cellDistance);

        // distance scaling: 1 cell fast, longer falls slower but capped.
        float distDur = Mathf.Clamp(d * 0.06f, 0.06f, 0.22f);

        // blend with base (so your existing tuning still matters)
        float duration = Mathf.Min(baseDur, distDur) * Mathf.Max(0.25f, GetCascadeFallSpeedMultiplier());

        return Mathf.Max(0.04f, duration);
    }

    internal float GetClearDurationForCurrentPass()
    {
        return Mathf.Max(0.03f, ClearDuration * GetCascadeClearSpeedMultiplier());
    }

    internal bool ShouldEnableFallSettleThisPass()
    {
        // Settle looks nice on the first impact, but slows long cascades.
        return EnableFallSettle && CurrentResolvePass <= 1;
    }

    private float GetCascadeFallSpeedMultiplier()
    {
        // pass=1 => 1.0, pass>=2 => faster
        return (CurrentResolvePass <= 1) ? 1f : 0.75f;
    }

    private float GetCascadeClearSpeedMultiplier()
    {
        return (CurrentResolvePass <= 1) ? 1f : 0.85f;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float _resolveProfT0;
    private void ResolveProfBegin(string tag)
    {
        _resolveProfT0 = Time.realtimeSinceStartup;
    }

    private void ResolveProfStep(string label)
    {
        float ms = (Time.realtimeSinceStartup - _resolveProfT0) * 1000f;
        _resolveProfT0 = Time.realtimeSinceStartup;
    }
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
                Debug.LogWarning($"[ResolveBoard] Safety break! loops={safety} (possible infinite resolve/spawn).");
                yield break;
            }

            CurrentResolvePass = safety; // 1-based pass index

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfBegin($"pass={CurrentResolvePass}");
#endif

            var matches = matchFinder.FindAllMatches();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep($"FindMatches (count={matches.Count})");
#endif

            if (matches.Count == 0)
                yield break;

            matches.RemoveWhere(t => t != null && t.GetSpecial() != TileSpecial.None);
            if (matches.Count == 0)
                yield break;

            // Special üretimi cascade'de kalabilir (tasarım tercihi)
            if (allowSpecial)
            {
                var created = specialResolver.TryCreateSpecial(matches);
                if (created != null)
                    shakeNextClear = true;
            }

            bool doShake = shakeNextClear;
            shakeNextClear = false;

            yield return boardAnimator.ClearMatchesAnimated(matches, doShake );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep("ClearMatchesAnimated");
#endif

            yield return boardAnimator.CollapseAndSpawnAnimated();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ResolveProfStep("CollapseAndSpawnAnimated#1");
#endif

            // ✅ Only do slide fill (and a second collapse/spawn) if there are still empty playable cells
            if (HasAnyEmptyPlayableCell())
            {
                yield return boardAnimator.SlideFillAnimated();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ResolveProfStep("SlideFillAnimated");
#endif

                if (HasAnyEmptyPlayableCell())
                {
                    yield return boardAnimator.CollapseAndSpawnAnimated();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    ResolveProfStep("CollapseAndSpawnAnimated#2");
#endif
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                ResolveProfStep("Skip SlideFill (no empty)");
            }
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
            if (!HasAnyMatched(matched))
                return types;
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
                if (holes[x, y])
                {
                    MarkRunIfNeeded(run, runStart, y, true, matched);
                    run = 0;
                    continue;
                }

                var t = types[x, y];
                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runStart = x;
                    continue;
                }

                if (t.Equals(runType))
                {
                    run++;
                    continue;
                }

                MarkRunIfNeeded(run, runStart, y, true, matched);
                run = 1;
                runType = t;
                runStart = x;
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
                if (holes[x, y])
                {
                    MarkRunIfNeeded(run, runStart, x, false, matched);
                    run = 0;
                    continue;
                }

                var t = types[x, y];
                if (run == 0)
                {
                    run = 1;
                    runType = t;
                    runStart = y;
                    continue;
                }

                if (t.Equals(runType))
                {
                    run++;
                    continue;
                }

                MarkRunIfNeeded(run, runStart, x, false, matched);
                run = 1;
                runType = t;
                runStart = y;
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
            if (matched[x, y])
                return true;

        return false;
    }

    private TileType PickTypeAvoidingMatch(TileType[,] types, bool[,] filled, int x, int y)
    {
        if (randomPool == null || randomPool.Length == 0)
            return default;

        int start = UnityEngine.Random.Range(0, randomPool.Length);
        for (int i = 0; i < randomPool.Length; i++)
        {
            var candidate = randomPool[(start + i) % randomPool.Length];
            if (!CreatesMatch(types, filled, x, y, candidate))
                return candidate;
        }

        return randomPool[start];
    }

    private bool CreatesMatch(TileType[,] types, bool[,] filled, int x, int y, TileType candidate)
    {
        int count = 1;
        int lx = x - 1;
        while (lx >= 0 && !holes[lx, y] && filled[lx, y] && types[lx, y].Equals(candidate))
        {
            count++;
            lx--;
        }

        int rx = x + 1;
        while (rx < width && !holes[rx, y] && filled[rx, y] && types[rx, y].Equals(candidate))
        {
            count++;
            rx++;
        }

        if (count >= 3) return true;

        count = 1;
        int uy = y - 1;
        while (uy >= 0 && !holes[x, uy] && filled[x, uy] && types[x, uy].Equals(candidate))
        {
            count++;
            uy--;
        }

        int dy = y + 1;
        while (dy < height && !holes[x, dy] && filled[x, dy] && types[x, dy].Equals(candidate))
        {
            count++;
            dy++;
        }

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

    private void HandleObstacleStageChanged(int originIndex, ObstacleStageSnapshot stage)
    {
        // Stage transition event is consumed in ApplyObstacleDamageAt to keep runtime state update centralized.
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
        => OnObstacleDestroyed?.Invoke(originIndex, obstacleId);

    private void HandleCellUnlocked(int cellIndex)
    {
        int x = cellIndex % width;
        int y = cellIndex / width;
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        bool isMaskHole = IsMaskHole(x, y);
        bool blockedByObstacle = obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
        holes[x, y] = isMaskHole || blockedByObstacle;

        if (!holes[x, y])
            OnCellUnlocked?.Invoke(cellIndex);
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
        if (maskHoles == null)
            return false;
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;
        return maskHoles[x, y];
    }

    internal bool IsMaskHoleCell(int x, int y) => IsMaskHole(x, y);

    internal bool IsObstacleBlockedCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        return obstacleStateService != null && obstacleStateService.IsCellBlocked(x, y);
    }

    internal bool IsSpawnPassThroughCell(int x, int y)
    {
        return IsMaskHoleCell(x, y) && !IsObstacleBlockedCell(x, y);
    }

    private TileType GetRandomType()
    {
        return randomPool[UnityEngine.Random.Range(0, randomPool.Length)];
    }

    public void SetInputLocked(bool isLocked)
    {
        InputLocked = isLocked;
    }

    private void ConsumeMove()
    {
        RemainingMoves = Mathf.Max(0, RemainingMoves - 1);
        OnMovesChanged?.Invoke(RemainingMoves);
    }

    public void AddMoves(int amount)
    {
        if (amount <= 0)
            return;

        RemainingMoves += amount;
        OnMovesChanged?.Invoke(RemainingMoves);
    }

    internal void NotifyTilesCleared(TileType tileType, int amount)
    {
        if (amount <= 0) return;
        OnTilesCleared?.Invoke(tileType, amount);
    }
}
