using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Referans oyuna yaklaştırılmış ivmeli taş düşüşü + landing ayarları.
// Tüm hız/ivme HÜCRE cinsindendir (piksele değil) → 9/10/11 satırlı board'da aynı his.
// Değerler frame bazlı tutulur (referenceFps=60); saniye karşılığı ReferenceFramesToSeconds ile alınır.
[Serializable]
public class ReferenceFallMotionSettings
{
    [Header("Fall (ivmeli hız entegrasyonu)")]
    [Min(1f)] public float referenceFps = 60f;
    [Min(0f)] public float initialFallSpeedCells = 5.0f;
    [Min(0f)] public float fallAccelerationCells = 38.0f;
    [Min(0.001f)] public float maxFallSpeedCells = 26.0f;

    public float initialSpeedCellsPerFrame => initialFallSpeedCells / referenceFps;
    public float accelerationCellsPerFrameSquared => fallAccelerationCells / (referenceFps * referenceFps);
    public float maxSpeedCellsPerFrame => maxFallSpeedCells / referenceFps;

    [Header("Stagger (yeni vs mevcut taş, ayrı)")]
    [Min(0f)] public float existingTileStagger = 2f / 60f;
    [Min(0f)] public float spawnTileStagger = 4f / 60f;

    public float spawnIntervalFrames => spawnTileStagger * referenceFps;
    public float existingIntervalFrames => existingTileStagger * referenceFps;

    [Header("Fall Stretch (hıza bağlı, hafif)")]
    public Vector2 maxFallStretchScale = new Vector2(0.975f, 1.04f);
    public float maxFallStretchX => maxFallStretchScale.x;
    public float maxFallStretchY => maxFallStretchScale.y;

    [Header("Landing (overshoot + impact squeeze)")]
    [Min(0f)] public float landingOvershootCells = 0.08f;
    public Vector2 impactScale = new Vector2(1.06f, 0.92f);
    public float impactScaleX => impactScale.x;
    public float impactScaleY => impactScale.y;

    [Min(0f)] public float landingOvershootFrames = 2f;
    [Min(0f)] public float impactHoldDuration = 1f / 60f;
    [Min(0f)] public float settleDuration = 5f / 60f;

    public float impactHoldFrames => impactHoldDuration * referenceFps;
    public float landingReturnFrames => settleDuration * referenceFps;

    [Header("LineV Beam")]
    [Range(0.35f, 0.45f)] public float beamWidthCells = 0.40f;
    public float beamFullOpacityDuration = 7f / 60f;
    public float beamFadeDuration = 10f / 60f;

    [Header("Fragments / Debris")]
    public float fragmentLifetimeMin = 0.22f;
    public float fragmentLifetimeMax = 0.32f;

    [Range(0.1f, 1.2f)] public float tileVisualFillRatio = 0.88f;

    public float ReferenceFramesToSeconds(float frames)
    {
        return Mathf.Max(0f, frames) / Mathf.Max(1f, referenceFps);
    }
}

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
    [Tooltip("Min scaleY = 1 - strength. 0.20 → 0.80 (inişte %20 kısa). Belirgin squash.")]
    [SerializeField, Range(0f, 0.9f)] private float fallSettleStrength = 0.20f;
    [Tooltip("Çarpma anında X genişleme oranı. 0.20 = %20 geniş (belirgin jelly his).")]
    [SerializeField, Range(0f, 0.6f)] private float fallSettleStretchX = 0.20f;
    [Tooltip("Çarpma anında hedefin altına inme oranı (hücre boyuna göre). 0.02 = hücre yüksekliğinin %2'si kadar aşağı taşar.")]
    [SerializeField, Range(0f, 0.4f)] private float fallSettleOvershoot = 0.02f;

    [Header("Fall Stretch (düşüş sırasında esneme)")]
    [Tooltip("Düşerken taşın dikeyde uzama oranı (squash&stretch). 0 = kapalı, 0.10 = %10 uzar; " +
             "yatay bunun ~yarısı kadar incelir. Hedefe yaklaşınca normale döner, inişte settle squash devralır.")]
    [SerializeField, Range(0f, 0.35f)] private float fallStretchAmount = 0.10f;
    [Tooltip("Uçuşun son bu ORANLIK kısmında esneme normale döner (0.45 = son %45'te toparlar).")]
    [SerializeField, Range(0.1f, 0.8f)] private float fallStretchRecover = 0.45f;

    [Header("Reference Fall Motion")]
    [Tooltip("KAPALI (önerilen): ivmeli düşüşü legacy path'in ActiveFallProfile'ından alır — obstacle/diagonal/" +
             "combo/spawn akışını doğru yönetir. AÇILINCA ayrı bir reference-motion path devreye girer; bu path " +
             "obstacle-yolu ve combo cascade'lerinde spawn hizalama/karışma sorunları yaşıyor (eksik/deneysel). " +
             "İvme zaten ActiveFallProfile ile geldiği için normalde açmaya gerek yok.")]
    [SerializeField] private bool useReferenceFallMotion = true;
    [SerializeField] private bool debugReferenceFallMotionLogs;
    [Tooltip("Kapalıyken mevcut Tiny Fixers taş görsel boyutu korunur. Açılırsa normal taş ikonları cellSize * tileVisualFillRatio olur.")]
    [SerializeField] private bool applyReferenceTileVisualFillRatio = false;
    [SerializeField] private ReferenceFallMotionSettings referenceFallMotion = new ReferenceFallMotionSettings();
    internal float FallColumnStep => Mathf.Max(0f, fallColumnStep);
    internal int MaxDiagonalSlidesPerCascade => Mathf.Max(1, maxDiagonalSlidesPerCascade);

    // ── Decoupled resolve (Faz 1): normal fall→clear overlap ────────────────
    // Docs/DecoupledResolve_Plan.md §6. false → bugünkü seri yol (anında geri-alma).
    [Header("Decoupled Resolve (Faz 1)")]
    [SerializeField] private bool useDecoupledResolve = true;
    // Taş hedefe kaç hücre KALA FallArrived event'ini atsın (hücreye tam oturmadan biraz üstünde
    // clear başlasın → referans hissi). 0 = tam varışta. Timed sync DEĞİL — animasyonun içinden,
    // pozisyon eşiğiyle bir kez atılır. Küçük tut (~0.15-0.25); büyükse taş görünür şekilde havada kırılır.
    [SerializeField, Range(0f, 0.5f)] private float fallArrivalLeadCells = 0.2f;
    internal bool UseDecoupledResolve => useDecoupledResolve;
    internal float FallArrivalLeadCells => fallArrivalLeadCells;
    private BoardVisualCoordinator visualCoordinator;

    [Header("Juice (Only 4+ / Power)")]
    [SerializeField] private float preClearDelay = 0.06f;
    [SerializeField] private float shakeDuration = 0.10f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private RectTransform shakeTarget;

    [Header("Board Entrance (slide-in)")]
    [Tooltip("Level yüklenince board taşlar dizili halde sağdan sola kayarak otursun.")]
    [SerializeField] private bool enableEntranceSlide = true;
    [SerializeField, Min(0.05f)] private float entranceSlideDuration = 0.5f;
    [Tooltip("Başlangıçta board'un sağa kaydırılma mesafesi (px). 0 = otomatik (board genişliği).")]
    [SerializeField, Min(0f)] private float entranceSlideOffsetX = 0f;
    [Tooltip("Loading ekranı kalkıp arka plan göründükten sonra, board slide'ı başlamadan önceki bekleme (sn). Örn. 1 = arka planı 1 sn gör, sonra board kaysın.")]
    [SerializeField, Min(0f)] private float entranceStartDelay = 1.0f;

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
    // LineH artık roket yerine tek dönen drill kullanır (atanmışsa). LineV roket kalır.
    [SerializeField] private DrillSweepPlayer drillSweepPlayer;
    internal DrillSweepPlayer DrillSweepPlayer => drillSweepPlayer;
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
    // Patlama halkası ölçeği — 9x9→7x7 alan değişimiyle orantılı (~7/9). Inspector'dan ince ayarla.
    [SerializeField] private float pulsePulseExplosionScale = 0.78f;
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
    [SerializeField] private Sprite rowBoosterWithDrillSprite;
    [SerializeField] private Sprite rowBoosterWithoutDrillSprite;
    [SerializeField] private RectTransform boosterFxParent;
    [SerializeField] private Sprite hammerBoosterFallbackSprite;
    [SerializeField] private Sprite patchBotPropellerSprite;
    [SerializeField] private Sprite patchBotPropellerHubSprite;
    [Tooltip("Uçan patchbot pervanesinin frame'leri (tile_test'teki 2-sprite spin ile aynı). " +
             "Atanırsa uçuş pervanesi rotasyon yerine bu frame'leri döngüler. Boşsa eski rotasyon.")]
    [SerializeField] private Sprite[] patchBotPropellerFrames;
    [SerializeField, Min(1f)] private float patchBotPropellerFrameFps = 18f;

    internal RectTransform HammerBoosterFxPrefab => hammerBoosterFxPrefab;
    internal RectTransform CannonBoosterFxPrefab => cannonBoosterFxPrefab;
    internal RectTransform VerticalBoosterFxPrefab => verticalBoosterFxPrefab;
    internal Sprite RowBoosterWithDrillSprite => rowBoosterWithDrillSprite;
    internal Sprite RowBoosterWithoutDrillSprite => rowBoosterWithoutDrillSprite;
    internal RectTransform BoosterFxParent => boosterFxParent != null ? boosterFxParent : ContentRoot ?? parent;
    internal Sprite HammerBoosterFallbackSprite => hammerBoosterFallbackSprite;
    internal Sprite PatchBotPropellerSprite => patchBotPropellerSprite;
    internal Sprite PatchBotPropellerHubSprite => patchBotPropellerHubSprite;
    internal Sprite[] PatchBotPropellerFrames => patchBotPropellerFrames;
    internal float PatchBotPropellerFrameFps => patchBotPropellerFrameFps;

    [Header("Special Tile Visual")]
    [SerializeField] private bool specialFillCell = false;
    [SerializeField, Range(-0.4f, 0.4f)] private float specialElevation = 0f;

    internal bool SpecialFillCell => specialFillCell;
    internal float SpecialElevation => specialElevation;

    [SerializeField] private bool allowPostSwapSettleValidation = true;

    // Faz 7 (Docs/Match3_MasterRoadmap.md A7): gravity/refill simülasyonu sütun-bazlı bir motora
    // (ColumnFlowEngine) taşınır. 7A = DAVRANIŞ-BİREBİR: aynı çıktı, ama her sütunun bağımsız işlendiği
    // ve hangi sütunların "meşgul" olduğunun (ColumnBusy) bilindiği yapı → Faz 8 (düşerken hamle) ön
    // koşulu. Kapalı = eski whole-board yolu HİÇ değişmez. Prod'da da çalışması gerek (release'te de
    // tanımlı olsun diye #if DIŞINDA).
    [Tooltip("Faz 7: sütun-bazlı gravity motoru (ColumnFlowEngine). Kapalı=eski whole-board yolu.")]
    [SerializeField] private bool usePerColumnGravity;
    public bool UsePerColumnGravity => usePerColumnGravity;

    // Faz 7 ColumnBusy sinyali: son CalculateCascades'te hangi sütunlar hareket etti (üretildi;
    // 7A'da tüketen yok — Faz 8 input gating'i okuyacak). Lazy-alloc, Width boyutunda.
    private bool[] columnBusyThisResolve;
    public bool IsColumnBusy(int x) =>
        columnBusyThisResolve != null && x >= 0 && x < columnBusyThisResolve.Length && columnBusyThisResolve[x];

    // CascadeLogic/ColumnFlowEngine çağırır: bu resolve pass'inde aktif olan sütunları kaydeder.
    internal void ReportColumnBusy(IReadOnlyCollection<int> activeColumns)
    {
        if (columnBusyThisResolve == null || columnBusyThisResolve.Length != width)
            columnBusyThisResolve = new bool[Mathf.Max(0, width)];
        System.Array.Clear(columnBusyThisResolve, 0, columnBusyThisResolve.Length);
        if (activeColumns == null) return;
        foreach (var x in activeColumns)
            if (x >= 0 && x < columnBusyThisResolve.Length)
                columnBusyThisResolve[x] = true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Debug / Tile Sync")]
    [SerializeField] private bool enableTileSyncValidation = true;
    [SerializeField] private bool enableSpecialChainTrace;
    [SerializeField] private bool enableBoardFlowTrace;

    // ── Faz 0 perf logger (ölçüm-önce; Docs/UnifiedSpecialFlow_Plan.md) ──
    // Açıkken her frame'i örnekler: eşik üstü frame süresi VEYA GC-alloc sıçraması = [PerfSpike]
    // (board bağlamıyla: busy/specialPhase/jobs/seq → takılma NE ZAMAN + NE koşarken). Her ~3s
    // [PerfBaseline] (avg/worst ms + net GC). Cihazda repro → Editor.log'dan hotspot. Yeni component/
    // sahne değişikliği yok, tek inspector bool.
    [Header("Perf")]
    [SerializeField] private bool enablePerfMonitor;
    [Tooltip("Bu ms üstü frame = spike log. 60fps→16.7, 30fps→33; 28 hitch'leri yakalar.")]
    [SerializeField] private float perfSpikeMs = 28f;
    [Tooltip("Bir frame'de bu KB üstü managed-heap artışı = alloc spike log.")]
    [SerializeField] private int perfGcSpikeKB = 64;

    // Object pool (Docs/UnifiedSpecialFlow_Plan.md §3.3). Açıkken taşlar Instantiate/Destroy yerine
    // havuzdan alınır/iade edilir → match-3'ün yarat-yok-et GC spike'ı biter. Kapalı = eski davranış.
    [Tooltip("Taş havuzu: Instantiate/Destroy yerine havuzdan al/iade (GC spike fix). Kapalı=eski yol.")]
    [SerializeField] private bool useTilePool;

    // Faz 2+ (Docs/UnifiedSpecialFlow_Plan.md): clear/special işleri FlowScheduler'a Activity olarak
    // kaydolur. Faz 2'de EK kayıt (eski job/bayrak da sürüyor) → davranış aynı, ama (a) bitişte otomatik
    // Pump (lost-wakeup imkânsız), (b) special-phase scope-garantili (finally) → "asılı bayrak" biter.
    [Tooltip("FlowScheduler Activity kaydı (Faz 2+). Kapalı=eski yol.")]
    [SerializeField] private bool useFlowActivities;
    public bool UseFlowActivities => useFlowActivities;
    [SerializeField] private bool throwOnTileSyncMismatch;
    [SerializeField] private float tilePositionEpsilon = 0.25f;
#endif

    public PatchbotDashUI PatchbotDashUI => patchbotDashUI;
    public BoardAudioDirector Audio => audioDirector;

    // Board'un OTORİTATİF ev (resting) pozisyonu. Level yüklenirken bir kez yakalanır ve
    // asla shake/efekt sırasında canlı transformdan yeniden okunmaz — eşzamanlı patlamalar
    // (level 26) üst üste shake tetiklediğinde, ikinci shake zaten kaymış pozisyonu base
    // sanıp board'u kaydırıyordu. Tüm shake'ler bu sabit eve göre salınır ve eve döner.
    private Vector2 shakeBasePos;
    private bool shakeHomeCaptured;

    // Entrance slide sürerken board kasıtlı olarak ev DIŞINDA durur; ResolveBoard'un
    // sonundaki "eve dön" garantisi bu sırada bastırılır (yoksa offscreen'i ezerdi).
    private bool entranceInProgress;

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
    public bool IsActionSequencePlaying => actionSequencer != null && actionSequencer.IsPlaying;
    public event Action OnBecameIdle;
    private bool resolveAfterActionSequenceRequested;
    private bool resolveAfterActionSequenceRunning;

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
    // Açık kilit (popup/intro SetInputLocked) — resolve kaynaklı IsBusy'den ayrışır;
    // BossDuel düşman saldırısını popup açıkken durdurmak için okur.
    public bool IsExplicitlyLocked => CurrentState == BoardState.Locked;
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
    // Stacked-beneath restore: authored bir obstacle (Mud, Stone...) üstteki kırılınca geri
    // yüklendi. Yalnızca view oluşturmak için — goal sayacı BÜYÜMEZ (obstacle yeni değil,
    // level'ın authored goal amount'ında zaten sayılı; OnObstacleCreatedDynamic olsaydı
    // TopHud onu dinamik yaratım sanıp +1 ekler, kırılınca net değişim 0 görünürdü).
    public event Action<int, int> OnObstacleViewRestored;
    // Set by GridSpawner: O(1) obstacle-view (Image) lookup by origin index. Lets services
    // (KeyGeneratorService) avoid full-hierarchy GetComponentsInChildren<Image> scans.
    public Func<int, Image> ObstacleViewByOriginLookup;
    // Set by KeyGeneratorService. While false (still producing) PatchBot targeting prefers the
    // GENERATOR (to emit keys); once true (all keys produced) it targets the Key tiles to collect.
    public bool KeyGeneratorProductionComplete;
    // Bir barrel'ın mud yayılımı (BarrelSpreadAction) tamamlandığında bir kez tetiklenir.
    // Mud goal'ündeki o barrel'a ait placeholder, mud stamp edildikten SONRA düşürülür ki
    // sayaç mud eklenmeden 0'a inip erken WIN tetiklemesin.
    public event Action OnBarrelResolved;
    public event Action<int> OnChestOpened;
    public event Action<int, ChestColorMask> OnChestColorRemoved;
    public event Action<int, ChestColorMask, int> OnBatteryHit;
    public event Action<int, ChestColorMask, int, int, int> OnOverrideBatteryBoxHit;
    public event Action<int> OnWardrobeOpened;
    public event Action<int, int> OnWardrobeItemRemoved;
    public event Action<int> OnMovesChanged;
    public event Action<TileType, int> OnTilesCleared;
    public event Action<int> OnMoveClearPraise;
    // Her tekil special aktivasyonu (zincirdekiler dahil) — BossDuel bonus hasarı dinler.
    public event Action<TileSpecial, Vector2Int> OnSpecialActivated;
    public event Action<bool> OnBoosterTargetingChanged;
    public event Action<LightningLineStrike, float> OnLineSweepStarted;
    public event Action<Vector2Int, LightningLineStrike> OnLineSweepCellReached;

    private TileView lastSwapA, lastSwapB;
    private bool lastSwapUserMove;
    private bool shakeNextClear;
    private bool isSpecialActivationPhase;
    private int moveClearPraiseCount;
    private bool moveClearPraiseTracking;
    private bool moveClearPraiseEmitted;
    // Decoupled overlap güvenliği: bu resolve'da herhangi bir special aktivitesi (line sweep:
    // LineV/Override/PulseCore) olduysa true. Match-overlap yalnız bu FALSE iken çalışır → special'ların
    // mevcut event-driven sequencer senkronu HİÇ bozulmaz. ResolveBoard başında sıfırlanır.
    private bool hadSpecialActivityThisResolve;
    private TileType[] randomPool;

    private MatchFinder matchFinder;
    private SpecialResolver specialResolver;
    private SpecialBehaviorRegistry specialBehaviorRegistry;
    private BoardAnimator boardAnimator;
    // Background-job accounting (typed handle/gate). One count slot per BoardJobKind; see BeginJob.
    // Level-end waits on the total (ActiveBackgroundJobs); resolve/input wait only on the Resolve slot.
    private readonly int[] _jobCounts = new int[7]; // sized to BoardJobKind
    private int _jobEpoch = 0;
    private ActionSequencer actionSequencer;
    private PulseCoreImpactService pulseCoreImpactService;
    private ObstacleStateService obstacleStateService;
    private CascadeLogic cascadeLogic;
    private ObstacleResolutionService obstacleResolutionService;
    private SpecialCreationService specialCreationService;
    private PendingCreationStore pendingCreationStore;
    private PendingCreationApplicator pendingCreationApplicator;
    private MoveClearPraisePopupController moveClearPraisePopup;

    private OilSpreadService oilSpreadService;
    private readonly HashSet<Vector2Int> oilSuppressionCellsThisMove = new();
    private bool oilSpreadResolvedThisMove = true;

    // Bu hamlede tetiklenen RocketBasket roketleri; board tam oturunca PatchBot gibi uçarlar.
    private readonly List<RocketBasketLaunchAction.Launch> rocketLaunchesThisMove = new();

    // ── Extracted services ──
    private BoardInitService boardInitService;
    private BoardVfxService boardVfxService;
    private LineSweepService lineSweepService;
    private BoosterService boosterService;
    private readonly HashSet<Vector2Int> pendingTriggeredSpecialCells = new();

    // KeyGenerator: cells reserved by keys currently in flight. Prevents two
    // simultaneously-produced keys from targeting the same cell (which would make
    // the second key visually fly onto a cell that just became a Key).
    private readonly HashSet<Vector2Int> reservedKeyLandingCells = new();

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

    /// <summary>
    /// Board'un metronomu: 1 hücrelik düşüş süresi (sn). Olaylar arası boşluklar
    /// (special doğuşu, zincir adımları vb.) buna oranlanır — fall velocity değişince
    /// tüm board tek vücut hızlanır/yavaşlar, "farklı oyun" hissi oluşmaz.
    /// </summary>
    // Board metronomu: olay-arası boşluklar (special reveal, zincir ritmi) için sabit ritim.
    // BİLEREK aktif düşüş profilinden BAĞIMSIZ: profile bağlanınca (fps 32'de 1 hücre 0.122s)
    // büyük kırılma sonrası aralar %70 uzayıp "kırılma anı board boş kaldı" hissi yarattı.
    // Düşüş formu/temposu ActiveFallProfile'dan, geçiş ritmi buradan gelir.
    internal float CellTime => 1f / FallVelocityCellsPerSecond;
    internal float FallSpawnStaggerMultiplier => Mathf.Clamp01(fallSpawnStaggerMultiplier);
    internal AnimationCurve SwapMoveCurve => swapMoveCurve;
    internal AnimationCurve FallMoveCurve => fallMoveCurve;
    internal bool EnableFallSettle => enableFallSettle;
    internal float FallSettleDuration => Mathf.Max(0f, fallSettleDuration);
    internal float FallSettleStrength => Mathf.Max(0f, fallSettleStrength);
    internal float FallStretchAmount => Mathf.Max(0f, fallStretchAmount);
    internal float FallStretchRecover => Mathf.Clamp(fallStretchRecover, 0.1f, 0.8f);
    internal float FallSettleStretchX => Mathf.Max(0f, fallSettleStretchX);
    internal float FallSettleOvershoot => Mathf.Max(0f, fallSettleOvershoot);
    // Reference fall motion is disabled until the whole diagonal/segmented flow is rebuilt
    // as one coherent system. Partial/hybrid use breaks dense diagonal boards such as LevelP_00060.
    internal bool UseReferenceFallMotion => useReferenceFallMotion;
    internal bool DebugReferenceFallMotionLogs => debugReferenceFallMotionLogs;
    internal bool ApplyReferenceTileVisualFillRatio => applyReferenceTileVisualFillRatio;
    internal ReferenceFallMotionSettings ReferenceFallMotion
    {
        get
        {
            if (referenceFallMotion == null)
                referenceFallMotion = new ReferenceFallMotionSettings();
            return referenceFallMotion;
        }
    }
    internal float FallCascadeStep => 0f;
    internal bool BoardFlowTraceEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return enableBoardFlowTrace;
#else
            return false;
#endif
        }
    }
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
    // Salt-okunur: ev pozisyonu yalnızca CaptureShakeHome ile, bir kez set edilir.
    internal Vector2 ShakeBasePos => shakeBasePos;

    // Ev pozisyonunu bir kez yakalar (idempotent). shakeTarget'ın dinlenme hâlindeki
    // anchoredPosition'ı = ev. Sonradan asla shake/efekt tarafından ezilmez.
    internal void CaptureShakeHome()
    {
        if (shakeHomeCaptured || shakeTarget == null) return;
        shakeBasePos = shakeTarget.anchoredPosition;
        shakeHomeCaptured = true;
    }

    internal void RebaseShakeHomeToCurrent()
    {
        if (shakeTarget == null) return;
        shakeBasePos = shakeTarget.anchoredPosition;
        shakeHomeCaptured = true;
    }

    // Board'u KALICI olarak kaydırır (BossDuel: board'u BottomArea üstüne yaslamak için).
    // Ev pozisyonu ile mevcut pozisyonu BİRLİKTE taşır — home-invariantı korunur; shake,
    // entrance ve tüm dönüşler yeni evi kullanır.
    internal void ShiftBoardHome(Vector2 delta)
    {
        if (shakeTarget == null) return;
        CaptureShakeHome();
        shakeBasePos += delta;
        shakeTarget.anchoredPosition += delta;
    }
    internal TileView LastSwapA => lastSwapA;
    internal TileView LastSwapB => lastSwapB;
    internal bool LastSwapUserMove { get => lastSwapUserMove; set => lastSwapUserMove = value; }
    internal bool ShakeNextClear { get => shakeNextClear; set => shakeNextClear = value; }
    internal bool IsSpecialActivationPhase
    {
        get => isSpecialActivationPhase;
        // Sticky: bir special aktivasyon fazı açıldıysa bu resolve match-overlap YAPMAZ (special'ların
        // event-driven sequencer senkronu korunur). false'a set etmek sticky bayrağı temizlemez;
        // yalnız ResolveBoard başındaki reset temizler. Field'a direkt yazan reset bunu tetiklemez.
        set { isSpecialActivationPhase = value; if (value) hadSpecialActivityThisResolve = true; }
    }
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

    public bool HasAnyPlayableSwapWithAdditionalLockedCells(IReadOnlyCollection<Vector2Int> additionallyLockedCells)
    {
        EnsureServices();
        return matchFinder != null && matchFinder.HasAnyPlayableSwap(additionallyLockedCells);
    }

    // ── Event forwarders for LineSweepService ──
    internal void RaiseSpecialActivated(TileSpecial special, Vector2Int cell) => OnSpecialActivated?.Invoke(special, cell);
    internal void OnLineSweepStartedInternal(LightningLineStrike strike, float delay)
    {
        // Bu resolve special dokundu → match-overlap kapansın (special event-senkronu korunur).
        hadSpecialActivityThisResolve = true;
        OnLineSweepStarted?.Invoke(strike, delay);
    }
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

    internal bool TryFindKeyLandingCell(out Vector2Int cell)
    {
        cell = default;

        // Allocation-free uniform pick via reservoir sampling: single pass, no List/GC.
        // Called per produced key (plus re-validation/reroute), so on dense boards with many
        // generators this ran many times per move — the per-call List alloc was the hotspot.
        int seen = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!CanReplaceTileWithGeneratedKey(x, y))
                    continue;

                var candidate = new Vector2Int(x, y);
                // Skip cells already claimed by another key mid-flight so two
                // simultaneous keys never race for the same landing spot.
                if (reservedKeyLandingCells.Contains(candidate))
                    continue;

                seen++;
                // Replace the running pick with probability 1/seen → uniform over all valid cells.
                if (UnityEngine.Random.Range(0, seen) == 0)
                    cell = candidate;
            }
        }

        if (seen == 0)
            return false;

        reservedKeyLandingCells.Add(cell);
        return true;
    }

    internal void ReleaseKeyLandingReservation(Vector2Int cell)
    {
        reservedKeyLandingCells.Remove(cell);
    }

    // Live check used by KeyGeneratorService to re-validate a key's target cell just
    // before the flight — the cell may have been filled by a Key/special via gravity
    // after it was first picked.
    internal bool CanReplaceGeneratedKeyCell(Vector2Int cell)
        => CanReplaceTileWithGeneratedKey(cell.x, cell.y);

    internal bool TryPlaceGeneratedKeyAt(Vector2Int preferredCell, out Vector2Int placedCell)
    {
        placedCell = preferredCell;

        // Flight is over: the preferred cell's reservation is no longer needed.
        reservedKeyLandingCells.Remove(preferredCell);

        // If the preferred cell is no longer valid (e.g. it became a Key or a
        // special while this key was flying), reroute to a fresh landing cell.
        if (!CanReplaceTileWithGeneratedKey(preferredCell.x, preferredCell.y))
        {
            if (!TryFindKeyLandingCell(out placedCell))
                return false;

            // Placement below is synchronous, so release immediately.
            reservedKeyLandingCells.Remove(placedCell);
        }

        var tile = tiles[placedCell.x, placedCell.y];
        if (tile == null)
            return false;

        tile.SetSpecial(TileSpecial.None);
        tile.SetType(TileType.Key);
        SyncTileData(placedCell.x, placedCell.y);
        matchFinder?.InvalidateRunCache();
        RestoreTilePresentation(tile);
        RefreshAllSortingOrders();
        return true;
    }

    internal int CountTilesOfType(TileType type)
    {
        if (tiles == null)
            return 0;

        int count = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = tiles[x, y];
                if (tile != null && tile.GetTileType() == type)
                    count++;
            }
        }

        return count;
    }

    private bool CanReplaceTileWithGeneratedKey(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;
        if (holes == null || holes[x, y])
            return false;

        var tile = tiles != null ? tiles[x, y] : null;
        if (tile == null || tile.GetSpecial() != TileSpecial.None || tile.GetTileType() == TileType.Key)
            return false;

        if (obstacleStateService != null)
        {
            if (obstacleStateService.HasObstacleAt(x, y))
                return false;
            if (obstacleStateService.IsInteractionLockedAt(x, y))
                return false;
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        CaptureShakeHome();
        if (audioDirector == null)
            audioDirector = GetComponentInChildren<BoardAudioDirector>(true);
        EnsureServices();
        TryResolveLightningSpawner();
        TryResolveLineTravelPlayer();
        EnsureGoalFlyFx();
        EnsureMoveClearPraisePopup();

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

        visualCoordinator ??= new BoardVisualCoordinator(this, actionSequencer);
    }

    public void OnActionSequenceFinished()
    {
        TryStartResolveAfterActionSequence();
    }

    internal void RequestResolveAfterActionSequence()
    {
        resolveAfterActionSequenceRequested = true;
        TryStartResolveAfterActionSequence();
    }

    internal void TryStartResolveAfterActionSequence()
    {
        if (!resolveAfterActionSequenceRequested || resolveAfterActionSequenceRunning)
            return;

        if (CurrentState == BoardState.Locked || IsBusy || BlockingBackgroundJobs > 0 || IsActionSequencePlaying)
            return;

        resolveAfterActionSequenceRunning = true;
        StartCoroutine(ResolveAfterActionSequenceRoutine());
    }

    private IEnumerator ResolveAfterActionSequenceRoutine()
    {
        resolveAfterActionSequenceRequested = false;

        BeginBusy();
        yield return ResolveBoardPublic();
        EndBusy();

        resolveAfterActionSequenceRunning = false;
        TryStartResolveAfterActionSequence();
    }

    // ── Continuous resolve pump (FlowScheduler Faz A, 2026-08-16) ──
    // RequestResolveAfterActionSequence EDGE-TRIGGERED'dı: resolve istendiğinde board meşgulse TryStart
    // döner; blocker (BlockingBackgroundJobs/IsBusy/sequencer) SESSİZCE temizlenince (job Dispose event
    // ATMAZ) kimse yeniden denemiyordu → "board boşta ama resolve asılı" donması (OBB'de yakalandı ama
    // GENEL bir sınıf). Tek otorite: her frame, board tam idle olunca bekleyen resolve'u kick et →
    // lost-wakeup sınıfı KÖKTEN biter. TryStart guard'lı (yalnız istek varsa + tam idle ise koşar),
    // istek yoksa saf no-op → maliyet ihmal edilebilir.
    private void Update()
    {
        // Sürekli pompa TEK OTORİTEDEN (FlowScheduler). Edge-triggered değil → blocking iş sessizce
        // bitse bile bekleyen resolve asılı kalmaz (lost-wakeup sınıfı yapısal olarak kapalı).
        Flow.Pump();

        if (enablePerfMonitor)
            SamplePerf();
    }

    // ── Faz 0 perf sampler ──
    private long _perfLastGc;
    private bool _perfInit;
    private float _perfWindowStart;
    private int _perfWindowFrames;
    private float _perfWindowSumMs;
    private float _perfWindowWorstMs;
    private long _perfWindowStartGc;
    private float _perfLastSpikeLog;
    private int _perfSuppressedSpikes;
    private int _perfIdleFrames;
    private long _perfIdleGcKB;

    private void SamplePerf()
    {
        long gc = System.GC.GetTotalMemory(false);

        if (!_perfInit)
        {
            _perfInit = true;
            _perfLastGc = gc;
            _perfWindowStart = Time.realtimeSinceStartup;
            _perfWindowStartGc = gc;
            return;
        }

        float ms = Time.unscaledDeltaTime * 1000f;
        long gcDeltaKB = (gc - _perfLastGc) / 1024;   // + = bu frame alloc; - = collection oldu
        _perfLastGc = gc;

        // GÖZLEMCİ ETKİSİ FIX: eşik her karede aşılırsa Debug.Log fırtınası olur; Editor'de her log
        // stack-trace yakalar (ek alloc + yavaşlama) → ölçüm kendini şişirir. En fazla 0.5s'de bir yaz,
        // aradaki spike'ları say. Sayaçlar (baseline) TÜM kareleri kapsamaya devam eder.
        if (ms >= perfSpikeMs || gcDeltaKB >= perfGcSpikeKB)
        {
            _perfSuppressedSpikes++;
            if (Time.realtimeSinceStartup - _perfLastSpikeLog >= 0.5f)
            {
                Debug.Log($"[PerfSpike] t={Time.realtimeSinceStartup:0.000} frameMs={ms:0.0} gcKB={gcDeltaKB} " +
                    $"(son 0.5s'de {_perfSuppressedSpikes} spike) " +
                    $"| busy={IsBusy} specialPhase={IsSpecialActivationPhase} activeJobs={ActiveBackgroundJobs} " +
                    $"blockJobs={BlockingBackgroundJobs} seq={IsActionSequencePlaying} state={CurrentState}");
                _perfLastSpikeLog = Time.realtimeSinceStartup;
                _perfSuppressedSpikes = 0;
            }
        }

        // IDLE ALLOCATION: board hiçbir iş yapmazken kare başına ne kadar çöp üretiliyor? Taş
        // havuzu bunu ETKİLEMEZ (idle'da taş doğmaz/ölmez) → buradaki değer "board dışı" kaynağı
        // (UI/canvas/timer/log) işaret eder. Asıl GC baskısı burada mı, resolve'da mı ayırt eder.
        if (!IsBusy && ActiveBackgroundJobs == 0 && !IsActionSequencePlaying)
        {
            _perfIdleFrames++;
            _perfIdleGcKB += gcDeltaKB;
        }

        _perfWindowFrames++;
        _perfWindowSumMs += ms;
        if (ms > _perfWindowWorstMs) _perfWindowWorstMs = ms;

        if (Time.realtimeSinceStartup - _perfWindowStart >= 3f)
        {
            float avg = _perfWindowFrames > 0 ? _perfWindowSumMs / _perfWindowFrames : 0f;
            long netGcKB = (gc - _perfWindowStartGc) / 1024;
            float idleKbPerFrame = _perfIdleFrames > 0 ? (float)_perfIdleGcKB / _perfIdleFrames : 0f;
            Debug.Log($"[PerfBaseline] {_perfWindowFrames}f/3s avgMs={avg:0.0} worstMs={_perfWindowWorstMs:0.0} " +
                      $"netGcKB={netGcKB} | IDLE: {_perfIdleFrames}f alloc={idleKbPerFrame:0.0}KB/frame");
            _perfWindowStart = Time.realtimeSinceStartup;
            _perfWindowFrames = 0;
            _perfWindowSumMs = 0f;
            _perfWindowWorstMs = 0f;
            _perfWindowStartGc = gc;
            _perfIdleFrames = 0;
            _perfIdleGcKB = 0;
        }
    }

    // Starts a board action immediately as a background job, running concurrently
    // with any currently-playing ActionSequencer action.
    // ResolveBoard's loop polls BlockingBackgroundJobs/actionSequencer to wait for completion.
    public void StartImmediateAction(BoardAction action)
    {
        StartCoroutine(RunImmediateAction(action, BeginJob(BoardJobKind.Resolve)));
    }

    private System.Collections.IEnumerator RunImmediateAction(BoardAction action, System.IDisposable job)
    {
        try
        {
            yield return StartCoroutine(action.ExecuteVisuals(actionSequencer));
        }
        finally
        {
            job.Dispose();
        }
    }

    // ---- Typed background-job gate ----
    // A job holds a handle; Dispose() = exactly one decrement (double-Dispose is a no-op).
    // ForceDrainAllJobs bumps _jobEpoch so any still-outstanding handle's Dispose becomes a no-op —
    // this replaces the old "set counters = 0" timeout hack WITHOUT leaving stale decrements behind.
    public enum BoardJobKind
    {
        Resolve = 0,         // resolve/settle loop MUST wait (clear, cascade, combo, safe hold)
        GoalOrbFlight = 1,   // async: resolve flows, only level-end waits (orb + cargo flights)
        PatchBotDash = 2,    // async: PatchBot dash, RocketBasket, LineV/H PatchBot combo
        BossStrikeDrain = 3, // async: BossDuel strike queue
        ObstacleSpread = 4,  // async: barrel mud splatter (data committed up-front, visual async)
        KeyFlight = 5,       // async: KeyGenerator key flight
        PresentationFx = 6   // async: clear-presentation visual effects
    }

    private sealed class BoardJobHandle : System.IDisposable
    {
        private BoardController _board;
        private readonly BoardJobKind _kind;
        private readonly int _epoch;
        private bool _disposed;

        public BoardJobHandle(BoardController board, BoardJobKind kind)
        {
            _board = board;
            _kind = kind;
            _epoch = board._jobEpoch;
            board._jobCounts[(int)kind]++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_board != null && _epoch == _board._jobEpoch)
                _board._jobCounts[(int)_kind] = Mathf.Max(0, _board._jobCounts[(int)_kind] - 1);
            _board = null;
        }
    }

    // Begin a background job. Dispose the returned handle exactly once when it finishes
    // (prefer `using` / try-finally). Kind decides whether resolve waits (Resolve) or only level-end.
    public System.IDisposable BeginJob(BoardJobKind kind) => new BoardJobHandle(this, kind);

    // Total in-flight jobs. Level-end waits on this — any pending job can still change the result.
    public int ActiveBackgroundJobs
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _jobCounts.Length; i++) sum += _jobCounts[i];
            return sum;
        }
    }

    // Only genuine Resolve work parks the resolve/settle loop and input; async flights/spreads don't.
    public int BlockingBackgroundJobs => _jobCounts[(int)BoardJobKind.Resolve];

    // Named subset accessors kept for diagnostics/log parity.
    public int FlyingGoalOrbs       => _jobCounts[(int)BoardJobKind.GoalOrbFlight];
    public int FlyingPatchBotDashes => _jobCounts[(int)BoardJobKind.PatchBotDash];
    public int DrainingBossStrikes  => _jobCounts[(int)BoardJobKind.BossStrikeDrain];
    public int PresentationFxInFlight => _jobCounts[(int)BoardJobKind.PresentationFx];
    public int SpreadingObstacles    => _jobCounts[(int)BoardJobKind.ObstacleSpread];

    // Sequencer'ın non-blocking (detached) action kuyruğu — FlowScheduler bunu da "akış sürüyor"
    // sinyaline katar (IsPlaying detached tail'leri görmez, bkz. ActionSequencer).
    public int DetachedSequencerActions =>
        actionSequencer != null ? actionSequencer.DetachedActionsInFlight : 0;

    // ── Board akışının TEK OTORİTESİ (Docs/UnifiedSpecialFlow_Plan.md Faz 1) ──
    // Yeni kod "board oturdu mu / adım koşabilir mi" için elle sinyal birleştirmemeli; buna sormalı.
    // Faz 1'de adapter (davranış aynı); sonraki fazlarda special/combo/clear buraya taşınır.
    private BoardFlowScheduler flowScheduler;
    public BoardFlowScheduler Flow => flowScheduler ??= new BoardFlowScheduler(this);

    // Faz 4 (decoupled-resolve): a special is VISUALLY in flight if any board-tile-affecting special
    // visual is still running — the detached PresentationFx sweep (line/pulse/override beams committed
    // then animated, BlockingBackgroundJobs==0 so the old gate missed it), a PatchBot dash sweep, OR a
    // non-blocking sequencer action tail (ActionSequencer fires those with StartCoroutine and IsPlaying
    // flips false while they run). EXCLUDED on purpose: goal-orb / key / boss-strike flights (don't clear
    // or move the cells a fresh cascade fills) AND obstacle spread (barrel mud is designed to splatter
    // concurrently with flow and never gated overlap before — including it would regress barrel levels).
    // This precise predicate REPLACES the blunt whole-resolve `hadSpecialActivityThisResolve` lock in the
    // overlap gate: overlap re-enables the instant the special's own visual drains instead of staying off
    // for the rest of the resolve.
    internal bool IsSpecialVisualInFlight =>
        IsSpecialActivationPhase
        || PresentationFxInFlight > 0
        || FlyingPatchBotDashes > 0
        || (actionSequencer != null && actionSequencer.DetachedActionsInFlight > 0)
        // Faz 2: kayıtlı SpecialSweep Activity'si de sayılır. Bu, TARİHSEL BOŞLUĞU kapatır:
        // MatchClearAction.RunClear, IsSpecialActivationPhase'i EnqueueCascadeIfNeeded'DEN ÖNCE geri
        // alıyor → special'ın kendi takip cascade'i bayrak false iken koşuyordu (LineV "sütun bitmeden
        // refill düştü" bug'ının kaynağı; sticky bayrak bu yüzden vardı). Activity finally'ye kadar
        // sürdüğü için o pencere artık kapalı. Yalnız EK sinyal (asla azaltmaz) → güvenli.
        || (useFlowActivities && flowScheduler != null
            && flowScheduler.Count(BoardFlowScheduler.ActivityKind.SpecialSweep) > 0);

    // Leak safety net: invalidate every outstanding handle (their Dispose becomes a no-op via epoch)
    // and zero counts. Called by ResolveBoard's 5s blocking timeout AND by the level-end recovery when
    // a NON-blocking async job leaks (resolve loop already exited, so its timeout can't clear it).
    public void ForceDrainAllJobs()
    {
        _jobEpoch++;
        System.Array.Clear(_jobCounts, 0, _jobCounts.Length);
        flowScheduler?.DrainAll();   // yeni Activity kayıtları da geçersiz kılınsın (tek otorite tutarlı kalsın)
    }

    // ---- Legacy paired Begin/End shims (call sites are already try/finally-paired) ----
    // Generic blocking scope — resolve waits (e.g. Safe break hold).
    public void BeginBackgroundJob() => _jobCounts[(int)BoardJobKind.Resolve]++;
    public void EndBackgroundJob()   => _jobCounts[(int)BoardJobKind.Resolve] = Mathf.Max(0, _jobCounts[(int)BoardJobKind.Resolve] - 1);

    // Non-blocking flights: resolve flows, level-end waits.
    public void BeginGoalOrbFlight() => _jobCounts[(int)BoardJobKind.GoalOrbFlight]++;
    public void EndGoalOrbFlight()   => _jobCounts[(int)BoardJobKind.GoalOrbFlight] = Mathf.Max(0, _jobCounts[(int)BoardJobKind.GoalOrbFlight] - 1);

    public void BeginPatchBotDashFlight() => _jobCounts[(int)BoardJobKind.PatchBotDash]++;
    public void EndPatchBotDashFlight()   => _jobCounts[(int)BoardJobKind.PatchBotDash] = Mathf.Max(0, _jobCounts[(int)BoardJobKind.PatchBotDash] - 1);

    // BossDuel: son hamle harcandı ama vuruş kuyruğu/havadaki boltlar hâlâ hasar yazacak —
    // out-of-moves fail bunlar boşalana kadar bekler (resolve parklanmaz).
    public void BeginBossStrikeDrain() => _jobCounts[(int)BoardJobKind.BossStrikeDrain]++;
    public void EndBossStrikeDrain()   => _jobCounts[(int)BoardJobKind.BossStrikeDrain] = Mathf.Max(0, _jobCounts[(int)BoardJobKind.BossStrikeDrain] - 1);

    // Runs a list of actions sequentially as a single background job.
    // Use this when actions have a defined order (e.g. Override fanout → clear).
    public void StartImmediateActionSequence(System.Collections.Generic.List<BoardAction> actions)
    {
        if (actions == null || actions.Count == 0) return;
        StartCoroutine(RunImmediateActionSequence(actions, BeginJob(BoardJobKind.Resolve)));
    }

    private System.Collections.IEnumerator RunImmediateActionSequence(System.Collections.Generic.List<BoardAction> actions, System.IDisposable job)
    {
        try
        {
            foreach (var action in actions)
            {
                System.Collections.IEnumerator e = action.ExecuteVisuals(actionSequencer);
                while (e.MoveNext())
                    yield return e.Current;
            }
        }
        finally
        {
            job.Dispose();
        }
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
        cascadeLogic?.ResetCargoSpawnCredits();

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
        tile.SetNormalVisualFillRatioOverride(applyReferenceTileVisualFillRatio, ReferenceFallMotion.tileVisualFillRatio);
    }

    // Yalnız normal-taş görsel dolgu oranını uygular (icon scale/fullcell mantığına dokunmadan).
    // GridSpawner'ın ilk board spawn'ları ConfigureTileView çağırmadığından initial taşlar oranı
    // almıyordu; bu helper onları da kapsar.
    public void ApplyNormalVisualFillRatio(TileView tile)
    {
        if (tile == null)
            return;
        tile.SetNormalVisualFillRatioOverride(applyReferenceTileVisualFillRatio, ReferenceFallMotion.tileVisualFillRatio);
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
            TryStartResolveAfterActionSequence();
        }
    }

    public void SetInputLocked(bool isLocked)
    {
        if (isLocked) CurrentState = BoardState.Locked;
        else if (CurrentState == BoardState.Locked)
        {
            CurrentState = BoardState.Idle;
            TryStartResolveAfterActionSequence();
        }
    }

    public void ForceFullBoardSync()
    {
        ClearAllPendingTriggeredSpecialCells();
        reservedKeyLandingCells.Clear();

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

        RefreshAllTileObstacleVisuals();
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
            bool busy = IsBusy || BlockingBackgroundJobs > 0 || IsActionSequencePlaying;

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
                    $"busyScopeDepth={busyScopeDepth}, ActiveBackgroundJobs={ActiveBackgroundJobs}, " +
                    $"BlockingBackgroundJobs={BlockingBackgroundJobs}, FlyingGoalOrbs={FlyingGoalOrbs}, " +
                    $"FlyingPatchBotDashes={FlyingPatchBotDashes}, DrainingBossStrikes={DrainingBossStrikes}, " +
                    $"ActionSequencePlaying={IsActionSequencePlaying}");

                action?.Invoke();
                yield break;
            }

            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  VFX Delegation
    // ═══════════════════════════════════════════════════════════════

    public event System.Action<float> OnSystemOverrideWaveProgress;
    public void InvokeSystemOverrideWaveProgress(float radiusPx) => OnSystemOverrideWaveProgress?.Invoke(radiusPx);

    public float PlaySystemOverrideComboVfxAndGetDuration()
    {
        Vector2Int originCell = lastSwapA != null
            ? new Vector2Int(lastSwapA.X, lastSwapA.Y)
            : lastSwapB != null
                ? new Vector2Int(lastSwapB.X, lastSwapB.Y)
                : new Vector2Int(Width / 2, Height / 2);
        return PlaySystemOverrideComboVfxAndGetDuration(originCell);
    }

    public float PlaySystemOverrideComboVfxAndGetDuration(Vector2Int originCell)
    {
        Sprite sprA = lastSwapA != null ? GetOverrideIcon(lastSwapA.GetTileType()) : null;
        Sprite sprB = lastSwapB != null ? GetOverrideIcon(lastSwapB.GetTileType()) : null;
        return boardVfxService.PlaySystemOverrideComboVfxAndGetDuration(systemOverrideComboVfx, vfxSpace, originCell.x, originCell.y, sprA, sprB);
    }

    public float GetSystemOverrideComboPreClearDuration() =>
        systemOverrideComboVfx != null ? systemOverrideComboVfx.GetPreClearDuration() : 0f;

    public float GetSystemOverrideComboWaveDuration() =>
        systemOverrideComboVfx != null ? systemOverrideComboVfx.GetRadialWaveDuration() : ResolutionContext.OverrideRadialClearDuration;
    public void PlayPulseEmitterComboVfxAtCell(int x, int y) => boardVfxService.PlayPulseEmitterComboVfxAtCell(pulseEmitterComboVfx, vfxSpace, x, y);
    public GameObject PlayPulsePulseExplosionVfxAtCell(int x, int y) => boardVfxService.PlayPulsePulseExplosionVfxAtCell(pulsePulseExplosionPrefab, vfxSpace, pulsePulseExplosionLifetime, x, y, pulsePulseExplosionScale);
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

    internal LightningBeam BeginPersistentLightning(Func<Vector3> startWorldProvider, Func<Vector3> endWorldProvider, Color color)
    {
        TryResolveLightningSpawner();
        return lightningSpawner != null
            ? lightningSpawner.BeginPersistentLightning(startWorldProvider, endWorldProvider, color)
            : null;
    }

    internal float PlayLightningLineStrikes(IReadOnlyList<LightningLineStrike> lineStrikes, Action<Vector2Int, int> onSweepCellReached = null)
    {
        TryResolveLightningSpawner();
        EnsureLineTravelVisualReady();
        float __dur = lineSweepService.PlayLightningLineStrikes(lightningSpawner, lineTravelPlayer, lineStrikes, onSweepCellReached);
        Debug.Log($"[BonusDebug] gate4 PlayLightningLineStrikes strikes={(lineStrikes!=null?lineStrikes.Count:-1)} duration={__dur} " +
                  $"lineTravelPlayer={(lineTravelPlayer==null?"NULL":"ok")} " +
                  $"playerActive={(lineTravelPlayer!=null && lineTravelPlayer.gameObject.activeInHierarchy)} " +
                  $"spawner={(lightningSpawner==null?"NULL":"ok")}");
        return __dur;
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

    // Birikmiş TÜM patchbot dash'lerini TEK PlayDashParallel çağrısıyla başlatır (içsel 0.02s stagger →
    // "hepsi neredeyse aynı anda"). MatchClearAction pompasına BAĞLI DEĞİL: pompaya bölünen dash'ler
    // her pompada PlayDashParallel'in StopCoroutine'iyle birbirini iptal ediyordu (bazıları hiç
    // kalkmıyordu). Override+PatchBot grup fırlatması bunu çağırır; tüm dash'ler önce senkron enqueue
    // edilir, sonra bu tek çağrı hepsini güvenle başlatır.
    public void StartPendingPatchbotDashesParallel()
    {
        var buffer = new List<PatchbotDashRequest>();
        ConsumePatchbotDashRequests(buffer);
        if (buffer.Count == 0)
            return;

        if (patchbotDashUI != null)
        {
            patchbotDashUI.PlayDashParallel(buffer, this);
        }
        else
        {
            for (int i = 0; i < buffer.Count; i++)
                buffer[i].onArrived?.Invoke();
        }
    }

    public TopHudController TopHud { get { if (topHud == null) topHud = FindFirstObjectByType<TopHudController>(); return topHud; } }
    public GoalFlyFx GoalFlyFx
    {
        get
        {
            if (goalFlyFx == null) goalFlyFx = FindFirstObjectByType<GoalFlyFx>();
            // Hâlâ yoksa kur (canvas altında "GoalFlyFx" + overlayRoot oluşturur, overlayRoot'u set eder).
            // FindFirstObjectByType inactive bulmaz; EnsureGoalFlyFx isimle bulur/oluşturur.
            if (goalFlyFx == null) EnsureGoalFlyFx();
            return goalFlyFx;
        }
    }

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
        if (tx < 0 || tx >= width || ty < 0 || ty >= height) { RemoveMovableIdleFx(tile); return; }
        if (!obstacleStateService.IsMovableObstacleAt(tx, ty)) { RemoveMovableIdleFx(tile); return; }

        var id = obstacleStateService.GetObstacleIdAt(tx, ty);
        var lib = ActiveLevelData != null ? ActiveLevelData.obstacleLibrary : null;
        var def = lib != null ? lib.Get(id) : null;
        if (def == null) { RemoveMovableIdleFx(tile); return; }

        // Çok-stage movable (örn. 2-stage plastik): hasar alınca sprite güncel stage'e geçmeli.
        // GetPreviewSprite hep stage-0 döndürür → refresh, hit sonrası görseli stage-0'a geri
        // sıfırlardı. Kalan vuruşa göre doğru stage sprite'ını al (yoksa preview'a düş).
        int remainingHits = obstacleStateService.GetRemainingHitsAt(tx, ty);
        var sprite = def.GetSpriteForRemainingHits(remainingHits) ?? def.GetPreviewSprite();
        if (sprite == null) { RemoveMovableIdleFx(tile); return; }

        tile.SetMovableObstacleTile(true);
        tile.SetUseFullCellIcon(false);
        tile.SetFullCellMovableSprite(def.fullCellSprite);
        tile.SetMovableObstacleSprite(sprite);

        EnsureMovableIdleFx(tile, id, def);
    }

    // Idle juice (coin dönme / cargo salınım) yaşam döngüsü: DOĞRU olanı ekle (yoksa), YANLIŞ olanı
    // kaldır. Her refresh'te yeniden yaratma YOK — churn CoinIdleWobble'ın gecikme döngüsünü sıfırlar,
    // coin hiç dönmezdi. Idempotent olduğu için sık çağrılması güvenli.
    private void EnsureMovableIdleFx(TileView tile, ObstacleId id, ObstacleDef def)
    {
        if (tile == null || tile.IconImage == null) return;
        var go = tile.IconImage.gameObject;

        bool wantWobble = id == ObstacleId.GoldMoney;
        bool wantSway = def != null && def.exitAtBottom;

        var wobble = go.GetComponent<CoinIdleWobble>();
        if (wantWobble && wobble == null) go.AddComponent<CoinIdleWobble>();
        else if (!wantWobble && wobble != null) Destroy(wobble);

        var sway = go.GetComponent<CargoFloatSway>();
        if (wantSway && sway == null) go.AddComponent<CargoFloatSway>();
        else if (!wantSway && sway != null) Destroy(sway);
    }

    private void RemoveMovableIdleFx(TileView tile)
    {
        if (tile == null || tile.IconImage == null) return;
        var go = tile.IconImage.gameObject;
        var wobble = go.GetComponent<CoinIdleWobble>();
        if (wobble != null) Destroy(wobble);
        var sway = go.GetComponent<CargoFloatSway>();
        if (sway != null) Destroy(sway);
    }

    private void RefreshSwapObstacleVisuals(TileView a, TileView b)
    {
        RefreshTileObstacleVisual(a);
        RefreshTileObstacleVisual(b);
    }

    internal void RefreshAllTileObstacleVisuals()
    {
        if (tiles == null) return;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) RefreshTileObstacleVisual(tiles[x, y]);
    }

    private OilOverlayRenderer oilOverlayRenderer;

    internal void RefreshOilOverlays()
    {
        if (obstacleStateService == null) return;
        if (parent == null || tileSize <= 0) return;

        // Oil görseli artık CELL-ANCHORED (tile'dan bağımsız). Oil verisi nerede varsa orada
        // çizilir; tile null olsa bile görünür. Eski tile-bound overlay (görünmez-oil/flicker)
        // kaldırıldı.
        if (oilOverlayRenderer == null)
        {
            var animator = GetComponent<OilSpreadAnimator>();
            oilOverlayRenderer = new OilOverlayRenderer(
                this,
                animator != null ? animator.OilOverlaySprite : null);
        }

        oilOverlayRenderer.Refresh(obstacleStateService.GetAllOilCells());
    }

    internal void RestoreTilePresentation(TileView tile)
    {
        if (tile == null)
            return;

        tile.ClearMovableObstaclePresentation();
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

    // ── Tile object pool (Docs/UnifiedSpecialFlow_Plan.md §3.3) ──
    // useTilePool açıkken taş GameObject'leri yaratılıp yok edilmez; havuzdan alınıp iade edilir.
    // Havuz boşsa Instantiate ile dinamik büyür. Kapalıyken tam eski davranış (Instantiate/Destroy).
    private readonly Queue<TileView> _tilePool = new Queue<TileView>();

    // Level (yeniden) kurulurken havuzu sıfırla. Board teardown'ı root'ların çocuklarını Destroy
    // ettiği için havuzda "yok edilmiş" referanslar kalabilir; taze level taze havuzla başlasın.
    internal void ClearTilePool()
    {
        while (_tilePool.Count > 0)
        {
            var pooled = _tilePool.Dequeue();
            if (pooled != null && pooled)
                Destroy(pooled.gameObject);
        }
    }

    // Yeni taş GameObject'i ver: havuzda varsa oradan (aktive), yoksa Instantiate (dinamik büyüme).
    // Çağıran sonra Init/SetType ile taşı kurar (mevcut spawn kodları aynen çalışır).
    internal GameObject AcquireTileObject(Transform tileParent)
    {
        if (useTilePool)
        {
            while (_tilePool.Count > 0)
            {
                var pooled = _tilePool.Dequeue();
                if (pooled == null || !pooled) continue;   // yok edilmiş referans (safety)
                var go = pooled.gameObject;
                go.transform.SetParent(tileParent, false);
                go.SetActive(true);
                return go;
            }
        }
        return Instantiate(tilePrefab, tileParent);
    }

    // Taşı havuza iade et (Destroy yerine): tam reset + deaktive + kuyruğa. Çağıran ÖNCE grid
    // referansını temizlemeli (ClearCell) — bu metod yalnız GameObject/veri yaşam döngüsünü yönetir.
    internal void ReleaseTile(TileView tile)
    {
        if (tile == null || !tile)
            return;

        if (!useTilePool)
        {
            Destroy(tile.gameObject);
            return;
        }

        var go = tile.gameObject;
        if (!go.activeSelf)
            return;   // ÇİFT-RELEASE guard: zaten iade edilmiş (deaktif) → tekrar kuyruklama (bozulma önle)

        tile.PrepareForRelease();
        go.SetActive(false);
        if (parent != null)
            go.transform.SetParent(parent, false);   // deaktif, tilesRoot altında bekler
        _tilePool.Enqueue(tile);
    }

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

        if (BoardFlowTraceEnabled)
        {
            Debug.Log(
                $"[PulseClearDebug] ClearAndDestroyTile ENTER tile=({x},{y}) " +
                $"type={tileType} special={special} live={wasLiveInGrid}");
        }

        if (wasLiveInGrid)
        {
            ClearCell(x, y);
            // Cargo üretim kredisi: yalnız gerçekten grid'de canlıyken kırılan taş sayılır
            // (çift-temizleme idempotent kalır, kredi şişmez).
            cascadeLogic?.AddCargoSpawnCredits(1);
        }

        var fxType = special switch
        {
            TileSpecial.LineH => TileType.LineEmitter_H,
            TileSpecial.LineV => TileType.LineEmitter_V,
            _                 => tileType
        };

        if (clearedByType != null)
        {
            clearedByType.TryGetValue(fxType, out int c);
            clearedByType[fxType] = c + 1;
        }

        if (tile != null && tile)
        {
            // Progress-event "+1" FX'i taşın yanında doğsun diye dünya pozisyonunu yayınla
            // (release'den ÖNCE). Sayım NotifyTilesCleared'da; bu sadece görsel ipucu.
            GameEventBus.EmitTileClearedAt(fxType, tile.transform.position);
            ReleaseTile(tile);   // havuz açıksa iade, kapalıysa Destroy (eski davranış)
        }
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
    {
        if (t == null || t.gameObject == null) return;
        GameEventBus.EmitTileClearedAt(type, t.transform.position);   // "+1" FX pozisyonu
        Destroy(t.gameObject);
        NotifyTilesCleared(type, 1);
    }

    // Mantıksal board'a bağlı OLMAYAN (hücresi başka taşa reassign olmuş / null olmuş) bir tile
    // view'ını güvenle yok eder. Hücre DATA'sına dokunmaz. Special zincirinde tüketilen ama
    // hücresi yarışla değişen PatchBot vb.'nin sahnede orphan/hayalet kalmasını önler.
    internal void DestroyOrphanedTileView(TileView t)
    {
        if (t == null || t.gameObject == null) return;
        // Güvenlik: gerçekten hiçbir hücre bu view'a işaret etmiyor olmalı.
        if (t.X >= 0 && t.X < width && t.Y >= 0 && t.Y < height && tiles[t.X, t.Y] == t)
            return;   // hâlâ board'a bağlı — orphan değil, dokunma
        Destroy(t.gameObject);
    }

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
            if (selected == null) { SetSelectedTile(tile); return; }
            if (selected == tile) { SetSelectedTile(null); return; }
            if (AreNeighbors(selected, tile) && IsTutorialSwapAllowed(selected.X, selected.Y, tile.X, tile.Y))
            {
                var a = selected; SetSelectedTile(null);
                ClearTutorialSwapFilter();
                StartCoroutine(ProcessSwap(a, tile));
            }
            else { SetSelectedTile(null); }
            return;
        }

        if (TryUseBooster(tile)) return;

        // Special'a tek tık → hareket ettirmeden solo aktive et. Swap (ve special+special
        // combo) için sürükleme kullanılır; TileView tap'i sürüklemeden ayırdığı için
        // sürüklenen special burada tetiklenmez, sadece gerçek tıklamada çalışır.
        if (tile != null && tile.GetSpecial() != TileSpecial.None && CanTapActivateSpecial(tile))
        {
            SetSelectedTile(null);
            StartCoroutine(ProcessSpecialTap(tile));
            return;
        }

        if (selected == null) { SetSelectedTile(tile); return; }
        if (selected == tile) { SetSelectedTile(null); return; }
        if (AreNeighbors(selected, tile))
        {
            var a = selected;
            SetSelectedTile(null);
            bool underTileBlocked = obstacleStateService != null &&
                ((obstacleStateService.IsUnderTileObstacleAt(a.X, a.Y) && !obstacleStateService.IsMudAt(a.X, a.Y))
                 || (obstacleStateService.IsUnderTileObstacleAt(tile.X, tile.Y) && !obstacleStateService.IsMudAt(tile.X, tile.Y))
                 || obstacleStateService.IsInteractionLockedAt(a.X, a.Y) || obstacleStateService.IsInteractionLockedAt(tile.X, tile.Y));
            if (!underTileBlocked)
                StartCoroutine(ProcessSwap(a, tile));
            return;
        }
        SetSelectedTile(tile);
    }

    bool AreNeighbors(TileView a, TileView b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) == 1;

    private void SetSelectedTile(TileView tile)
    {
        if (selected == tile)
            return;

        if (selected != null)
            BoardIdleHintAndComboGlowController.SetManualSpecialGlow(selected, false, tileSize);

        selected = tile;

        if (selected != null)
            BoardIdleHintAndComboGlowController.SetManualSpecialGlow(selected, true, tileSize);
    }

    private bool CanTapActivateSpecial(TileView tile)
    {
        if (tile == null || tile.GetSpecial() == TileSpecial.None)
            return false;

        if (obstacleStateService == null)
            return true;

        return !obstacleStateService.IsOilAt(tile.X, tile.Y)
            && !obstacleStateService.IsInteractionLockedAt(tile.X, tile.Y);
    }

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
        var mode = activeBooster; SetBoosterMode(BoosterMode.None); SetSelectedTile(null);
        var targetCell = new Vector2Int(x, y); var targetTile = tiles[x, y];

        // Hak düşümü: free oyunda düşmez, normalde 1 hak harcar (tek merkez: BoosterAccessService).
        BoosterAccessService.OnBoosterUsed(mode);

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
        if (!CanTapActivateSpecial(tile))
            yield break;

        if (IsBusy) yield break;

        // Hamle kalmadıysa yeni hamle BAŞLATMA. Aksi halde board son hamleden sonra idle
        // olduğunda (fail değerlendirme/grace penceresi) oyuncu 0 hamleyle tap yapıp
        // "hamle kalmadı" popup'ıyla çelişen bir hamle sokabiliyordu.
        if (RemainingMoves <= 0) yield break;

        oilSuppressionCellsThisMove.Clear();
        oilSpreadResolvedThisMove = false;
        obstacleStateService?.ResetPerMoveEmitGuard();

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

        // Hamle kalmadıysa swap BAŞLATMA — 0 hamlede board idle olduğunda (fail
        // değerlendirme/grace) oyuncunun hamle sokup "hamle kalmadı" ile çelişmesini önler.
        if (RemainingMoves <= 0)
            yield break;

        oilSuppressionCellsThisMove.Clear();
        oilSpreadResolvedThisMove = false;
        obstacleStateService?.ResetPerMoveEmitGuard();

        float _flowStart = Time.realtimeSinceStartup;
        float _flowLast = _flowStart;
        void FlowLog(string step)
        {
            if (!BoardFlowTraceEnabled) return;
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
        ObstacleStateService.ObstacleSwapStateSnapshot obstacleSwapStateSnapshot = null;
        if (obstacleStateService != null
            && (obstacleStateService.IsMovableObstacleAt(ax, ay) || obstacleStateService.IsMovableObstacleAt(bx, by)))
        {
            obstacleSwapStateSnapshot = obstacleStateService.CaptureObstacleSwapState();
        }

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
        if (obstacleSwapStateSnapshot != null)
            RefreshSwapObstacleVisuals(a, b);
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

            // Override + Normal'da normal-taraf match'ini ÖNDEN temizleme: Override zaten
            // partner renginin TÜM taşlarını (match dahil) kendi fanout animasyonuyla süpürüyor.
            // Önden clear taşları yerinde pat diye yok edip Override efektinden önce boşluk
            // yaratıyordu ("taş kaymadan kayboluyor"). Bu yüzden override tarafında atla:
            // akış = swap kaydırma → Override animasyon/efekt.
            bool specialSideIsOverride =
                originalSa == TileSpecial.SystemOverride || originalSb == TileSpecial.SystemOverride;

            if (!bothOriginallySpecial && !specialSideIsOverride)
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
                int chargeX = bx;
                int chargeY = by;

                SpecialVisualService.HideTileVisualForCombo(a);
                SpecialVisualService.HideTileVisualForCombo(b);

                PlayPulsePulseExplosionVfxAtCell(chargeX, chargeY);
                
                yield return new WaitForSeconds(pulsePulseChargeDuration);

                if (pulseCoreImpactService != null)
                    pulseCoreImpactService.PlayPulseCoreExplosionVfxAtCell(chargeX, chargeY, radiusCells: 3); // 7x7 alan (combo ile hizalı)
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
            // Obstacle state de geri alınsın. Stacked movable senaryosunda (örn. plastik
            // altında altın) ters MoveObstacle yetmez: alttaki movable geri açıldığı için
            // plastik eski hücresine dönemeyebilir. Snapshot, deneme öncesi katmanları
            // birebir geri koyar.
            if (obstacleSwapStateSnapshot != null)
                obstacleStateService.RestoreObstacleSwapState(obstacleSwapStateSnapshot);
            else
                RestoreMovableObstacleSwapState(ax, ay, bx, by, movableMovedAToB, movableMovedBToA);

            tiles[ax, ay] = a;
            tiles[bx, by] = b;
            a.SetCoords(ax, ay);
            b.SetCoords(bx, by);

            SyncTileData(ax, ay);
            SyncTileData(bx, by);
            if (obstacleSwapStateSnapshot != null)
                RefreshSwapObstacleVisuals(a, b);
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

    // Faz 1 overlap gate: mantıksal board'dan match'i oku ve BASİT ise (hiçbir eşleşen taş
    // special değil ve hücresinde obstacle yok) TileView set'ini döndür. Special (aktivasyon
    // riski) veya obstacle içeren match → false (çağıran seri yola düşer). Docs §6.4.
    private bool TryBuildSimpleOverlapMatch(out HashSet<TileView> matchTiles)
    {
        matchTiles = null;
        var matches = matchFinder.FindAllMatches();
        if (matches.Count == 0)
            return false;

        var set = new HashSet<TileView>();
        foreach (var t in matches)
        {
            var tile = tiles[t.X, t.Y];
            if (tile == null)
                continue;

            if (tile.GetSpecial() != TileSpecial.None)
                return false;
            // Overlay/beneath obstacle (Mud/Grass) match'leri overlap EDEBİLİR: clear hasarı
            // settled hücrede uygulanır, alâkasız sütunların düşüşüyle çakışmaz. Yalnız MOVABLE
            // obstacle (Plastic vb.) seri kalır — clear + yaşam döngüsü karmaşık.
            // (Timed log: mud-yoğun board'da eski HasObstacleAt kontrolü HER match'i reddedip
            //  resolve_board'u ~1.6s tam seri bırakıyordu — overlap hiç tetiklenmiyordu.)
            if (obstacleStateService != null && obstacleStateService.IsMovableObstacleAt(tile.X, tile.Y))
                return false;

            set.Add(tile);
        }

        if (set.Count == 0)
            return false;

        matchTiles = set;
        return true;
    }

    private static bool HasMovingTileInCurrentFallPass(IEnumerable<TileView> matchTiles)
    {
        if (matchTiles == null)
            return false;

        foreach (var tile in matchTiles)
        {
            if (tile != null && tile && tile.IsPlannedToMoveThisFallPass)
                return true;
        }

        return false;
    }

    IEnumerator ResolveBoard(bool allowSpecialActivation = false, bool resolveEmptyCellsFirst = false)
    {
        float _rbStart = Time.realtimeSinceStartup;
        isSpecialActivationPhase = false;
        hadSpecialActivityThisResolve = false;

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

            // Bottom-exit cargo: alt satıra inen Cargo, board'un TAMAMEN oturmasını
            // beklemeden her pass başında (sequencer boşken) hemen çıkar. Boşalan
            // hücre aynı pass'in cascade barrier'ında doldurulur.
            if (TryCollectBottomExitCargo())
                RefreshAllSortingOrders();

            // ─────────────────────────────────────────────
            // STRICT ORDER BARRIER:
            // MatchFinder asla GERÇEKTEN doldurulabilir boş hücre varken çalışmamalı.
            // Her resolve pass başında önce cascade settle yapılır:
            // 1) vertical, 2) diagonal, 3) vertical, 4) pocket fill.
            // Ancak boşluk kapandıktan sonra match aranır.
            //
            // KRİTİK: AKIŞ-ERİŞİLEBİLİR (resolvable) boşluğu sor, ham "boş+bloklanmamış" değil.
            // Obstacle'la çevrili ÖLÜ CEP'ler (spawn'a bağlı değil + üstünde inecek kaynak taş yok;
            // ör. SculptingSpecial kanallarındaki izole boşluklar) asla dolamaz. Bunları "fillable"
            // saymak CalculateCascades'i her pass boşuna çağırıyor (log'daki "no actions" churn'ü);
            // her churn pass'inde cep yanındaki taşlar diagonal için yeniden değerlendirilip geri
            // dönüyor → kullanıcının gördüğü "flip-flop" (taş gidecek gibi yapıp yerinde kalıyor).
            // HasAnyResolvableEmptyPlayableCell ölü cepleri segment analiziyle dışlar.
            if (cascadeLogic != null && cascadeLogic.HasAnyResolvableEmptyPlayableCell())
            {
                var preMatchCascades = cascadeLogic.CalculateCascades();

                if (preMatchCascades.Count > 0)
                {
                    DisableSettleIfMoreCascadesFollow(preMatchCascades);
                    if (BoardFlowTraceEnabled)
                        Debug.Log($"[Resolve] pass={safety} pre_match_cascade actions={preMatchCascades.Count} +{(Time.realtimeSinceStartup - _rbStart):0.000}s");

                    // ── Faz 1 overlap (Docs/DecoupledResolve_Plan.md §6) ──
                    // Bu cascade board'u mantıksal olarak OTURTTUYSA (artık boş hücre yok) ve
                    // BASİT bir match hazırsa (special/obstacle yok), fall'un TAMAMINI beklemeden
                    // clear'ı match taşları hedefe yaklaşınca başlat → "match taş girerken başlar".
                    // Zor durumlar aşağıdaki seri yola düşer (bugünkü davranış).
                    //
                    // KRİTİK GUARD: overlap yalnız board GÖRSEL OLARAK SESSİZKEN çalışır — sequencer
                    // boş VE bloklayıcı background job yok VE special aktivasyon fazı değil. Aksi hâlde
                    // detached fall, hâlâ uçan bir roket/line-sweep/orb'u beklemeden düşüşü başlatır
                    // (LineV sütunu bitmeden taş düşme bug'ı). Special sweep'i bittikten sonra bu
                    // koşullar sağlanınca refill zaten normal cascade gibi overlap edebilir.
                    // Faz 4: the gate no longer keys off the sticky whole-resolve
                    // `hadSpecialActivityThisResolve`. It now blocks overlap only while a special
                    // is ACTUALLY visually in flight (IsSpecialVisualInFlight). Once that drains,
                    // post-special pure cascades overlap again → closes the ~1.6s "other columns
                    // freeze after the pulse" stall. IsSpecialActivationPhase + detached PresentationFx
                    // sweeps are inside the predicate, so the reverted LineV "refill before the column
                    // finished sweeping" race stays covered.
                    bool canOverlapCheap =
                        useDecoupledResolve
                        && visualCoordinator != null
                        && !IsSpecialVisualInFlight
                        && !IsActionSequencePlaying
                        && BlockingBackgroundJobs == 0
                        && !cascadeLogic.HasAnyEmptyPlayableCell();

                    HashSet<TileView> overlapMatchTiles = null;
                    bool overlapSimpleMatch = false;
                    bool overlapMoving = false;
                    if (canOverlapCheap)
                    {
                        overlapSimpleMatch = TryBuildSimpleOverlapMatch(out overlapMatchTiles);
                        overlapMoving = overlapSimpleMatch && HasMovingTileInCurrentFallPass(overlapMatchTiles);
                    }

                    // DIAGNOSTIC (trace-only): overlap seri yola düştüyse HANGİ koşulun kapattığını yaz.
                    if (BoardFlowTraceEnabled && !(canOverlapCheap && overlapSimpleMatch && overlapMoving))
                        Debug.Log(
                            $"[Resolve] pass={safety} overlap_skip cheap={canOverlapCheap} " +
                            $"(noSpecialVisual={!IsSpecialVisualInFlight} [phase={IsSpecialActivationPhase} " +
                            $"fx={PresentationFxInFlight} dash={FlyingPatchBotDashes} spread={SpreadingObstacles} " +
                            $"detached={(actionSequencer != null ? actionSequencer.DetachedActionsInFlight : 0)}] " +
                            $"noActionSeq={!IsActionSequencePlaying} noBlockJobs={BlockingBackgroundJobs == 0} " +
                            $"settled={!cascadeLogic.HasAnyEmptyPlayableCell()} stickyWas={hadSpecialActivityThisResolve}) " +
                            $"simpleMatch={overlapSimpleMatch} moving={overlapMoving}");

                    if (canOverlapCheap && overlapSimpleMatch && overlapMoving)
                    {
                        bool overlapCleared = false;
                        yield return visualCoordinator.PlayFallWithOverlappedClear(
                            preMatchCascades,
                            overlapMatchTiles,
                            () => ExecuteClearPass(overlapMatchTiles, allowSpecialActivation, r => overlapCleared = r));

                        RefreshAllSortingOrders();
                        RefreshOilOverlays();
                        continue;
                    }

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
                    if (BoardFlowTraceEnabled)
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
                if (BoardFlowTraceEnabled)
                    Debug.Log($"[Resolve] pass={safety} cascade_fall actions={cascades.Count} +{(Time.realtimeSinceStartup - _rbStart):0.000}s");
                actionSequencer.Enqueue(cascades);

                while (actionSequencer.IsPlaying)
                    yield return null;

                RefreshAllSortingOrders();
                RefreshOilOverlays();
                continue;
            }

            // Yalnızca gerçek blocking job'ları bekle. Goal-orb/PatchBot uçuşları
            // hariç tutulur ki hedefe uçarken cascade/düşüş/zincir akışı donmasın.
            if (BlockingBackgroundJobs > 0 || actionSequencer.IsPlaying)
            {
                backgroundJobWaitTime += Time.deltaTime;

                if (backgroundJobWaitTime > 5f)
                {
                    Debug.LogWarning($"[ResolveBoard] Background job timeout — forcing continue. ActiveBackgroundJobs={ActiveBackgroundJobs}, IsPlaying={actionSequencer.IsPlaying}");
                    ForceDrainAllJobs();
                }

                yield return null;
                continue;
            }

            backgroundJobWaitTime = 0f;

            // Oil spread: board TAMAMEN oturduktan sonra (ekrandaki cascade + tüm background
            // job'lar bitince) ve bu hamlede HİÇ oil kırılmamışsa (oilSuppressionCellsThisMove
            // boşsa) yayılır. Önceden bu blok background-job idle kontrolünden ÖNCEydi; ekrandaki
            // cascade hâlâ koşarken spread tetikleniyor, o esnada kırılma oluşuyordu. Artık
            // yalnızca board tam idle iken çalışır.
            if (!oilSpreadResolvedThisMove
                && oilSpreadService != null)
            {
                var spreadTargets = oilSpreadService.CalculateSpread(oilSuppressionCellsThisMove);
                oilSpreadResolvedThisMove = true;

                if (spreadTargets.Count > 0)
                {
                    if (BoardFlowTraceEnabled)
                        Debug.Log($"[Oil] Spreading to {spreadTargets.Count} cells after board fully settled.");
                    actionSequencer.Enqueue(new OilSpreadAction(this, spreadTargets));

                    while (actionSequencer.IsPlaying)
                        yield return null;

                    RefreshOilOverlays();
                    continue;
                }
            }

            // Barrel mud yayılımı artık board oturmasını beklemez — barrel kırılır kırılmaz
            // HandleObstacleDestroyed → CoSpreadBarrelMudImmediate ile akışla eşzamanlı koşar.

            // RocketBasket: board tam oturunca kuyruktaki roketler PatchBot gibi hedefe uçup
            // vurur. KUYRUK-BAZLI: hasar bazı yollarda gecikmeli (detached coroutine) uygulandığı
            // için eski hamle-başına-tek-atış bayrağı geç kuyruklamayı bir sonraki hamleye
            // sarkıtıyordu; artık settle'a her gelişte kuyrukta ne varsa ateşlenir.
            if (rocketLaunchesThisMove.Count > 0)
            {
                var launches = new List<RocketBasketLaunchAction.Launch>(rocketLaunchesThisMove);
                rocketLaunchesThisMove.Clear();

                if (BoardFlowTraceEnabled)
                    Debug.Log($"[RocketBasket] Launching {launches.Count} rocket(s) after board settled.");
                // Bağımsız uçuş (flush yolu ile aynı): board'u bekletmeden fırlat.
                StartCoroutine(new RocketBasketLaunchAction(this, launches).ExecuteVisuals(null));
                continue;
            }

            // ─────────────────────────────────────────────
            // Deadlock kontrolü:
            // Eğer board durmuşsa ve oynanabilir hiçbir swap yoksa
            // safe reshuffle yap, sonra resolve loop'una tekrar gir.
            // Special swap mümkünse HasAnyPlayableSwap() true döndürmeli.
            // ─────────────────────────────────────────────
            // Son hamlede de board'un kendi ürettiği match/cascade zinciri bitene kadar
            // resolve devam etmeli. Hamle yokken sadece deadlock reshuffle yapılmaz.
            if (RemainingMoves <= 0)
                break;

            if (!matchFinder.HasAnyPlayableSwap())
            {
                if (BoardFlowTraceEnabled)
                    Debug.Log($"[Resolve] pass={safety} deadlock_detected -> safe_shuffle +{(Time.realtimeSinceStartup - _rbStart):0.000}s");

                yield return boosterService.SafeShuffleBoardRoutine(boardInitService);

                // Shuffle sonrası board değiştiği için tekrar resolve et
                RefreshAllSortingOrders();
                continue;
            }

            break;
        }

        RefreshAllTileObstacleVisuals();
        RefreshAllSortingOrders();

        // Board tam oturduktan sonra oil overlay'lerini son kez senkronize et — resolve sırasında
        // herhangi bir geçici gizleme/handoff olduysa, dinlenme hâlinde görsel = veri olsun.
        RefreshOilOverlays();

        // Shake sonrası pozisyon kaymasını garanti et (eve dön).
        // Entrance slide sürerken bastır: board şu an kasıtlı olarak ekran dışında.
        if (shakeTarget != null && !entranceInProgress)
            shakeTarget.anchoredPosition = shakeBasePos;

        EmitMoveClearPraiseIfNeeded();

        if (BoardFlowTraceEnabled)
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
            if (BoardFlowTraceEnabled)
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
        // Her created special'ın KENDİ match grubu (formation'da yalnız bunlar toplanır;
        // aynı resolve'daki alakasız match'ler — ör. 3'lü kırmızı — merkeze çekilmesin).
        var createdSpecialContributors = new Dictionary<TileView, List<TileView>>();

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

                // Grup, winner special'a ÇEVRİLMEDEN önce hesaplanır (tip değişimi run tespitini bozmasın).
                var consumedGroup = specialCreationService.GetConsumedTilesForCreation(nonSpecialMatchTiles, creation);

                var created = specialResolver.ApplyCreatedSpecial(creation.winner, creation.special);
                if (created == null)
                    continue;

                createdSpecialTiles.Add(created);

                var groupContributors = new List<TileView>();
                if (consumedGroup != null)
                {
                    foreach (var gt in consumedGroup)
                        if (gt != null && gt != creation.winner && gt != created)
                            groupContributors.Add(gt);
                }
                createdSpecialContributors[created] = groupContributors;

                // Oluşan special kazanılmış haktır; normal clear listesinde kalmamalı.
                matchTiles.Remove(created);
                nonSpecialMatchTiles.Remove(created);
                preservedSpecialTiles.Add(created);

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
            BuildCreatedSpecialPresentationPlan(createdSpecialTiles, matchTiles, doShake, createdSpecialContributors);

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

    private ClearPresentationPlan BuildCreatedSpecialPresentationPlan(
        List<TileView> createdTiles,
        HashSet<TileView> clearTiles,
        bool doShake,
        Dictionary<TileView, List<TileView>> createdContributors = null)
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
            // KRİTİK: true iken katkı taşları formation'dan ÖNCE yok ediliyor → gather boş
            // kalıyordu. false: önce formation (canlı taşları topla), SONRA final clear.
            CommitFinalClearsBeforeEffects = false,
            BackgroundEffectsBlockResolve = false,
            ObstacleHitContext = IsSpecialActivationPhase
                ? ObstacleHitContext.SpecialActivation
                : ObstacleHitContext.NormalMatch
        };

        var finalClearTiles = new HashSet<TileView>(clearTiles);
        foreach (var created in validCreatedTiles)
            finalClearTiles.Remove(created);

        foreach (var created in validCreatedTiles)
        {
            var createdCell = new Vector2Int(created.X, created.Y);
            plan.RegisterImpactOnlyCell(createdCell);
            if (plan.ObstacleHitContext == ObstacleHitContext.NormalMatch)
                plan.RegisterNormalMatchSource(created);

            var contributors = new List<TileView>();
            var contributorCells = new List<Vector2Int>();

            // Yalnız bu special'ın KENDİ match grubu toplanır (grup verilmişse); yoksa eski
            // davranış (tüm finalClearTiles). Grup dışı taşlar normal break ile temizlenir.
            List<TileView> groupList = null;
            createdContributors?.TryGetValue(created, out groupList);

            if (groupList != null)
            {
                foreach (var tile in groupList)
                {
                    if (tile == null || tile == created || !finalClearTiles.Contains(tile))
                        continue;

                    contributors.Add(tile);
                    contributorCells.Add(new Vector2Int(tile.X, tile.Y));
                }
            }
            else
            {
                foreach (var tile in finalClearTiles)
                {
                    if (tile == null)
                        continue;

                    contributors.Add(tile);
                    contributorCells.Add(new Vector2Int(tile.X, tile.Y));
                }
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

        return plan.Effects.Count > 0 || plan.ImpactOnlyCells.Count > 0 ? plan : null;
    }

    public IEnumerator ResolveInitial() { BeginBusy(); yield return ResolveBoard(false); EndBusy(); }

    // Level girişi: board taşlar dizili halde sağdan sola kayarak otursun.
    // settleRoutine (genelde ResolveInitial) ekran DIŞINDA çalışır → oyuncu önce arka planı
    // görür, sonra hazır/oturmuş board kayarak gelir. Giriş boyunca input kilitli (BeginBusy).
    public IEnumerator PlayBoardEntrance(IEnumerator settleRoutine)
    {
        CaptureShakeHome();
        bool slide = enableEntranceSlide && shakeTarget != null;
        Vector2 home = shakeBasePos;

        BeginBusy();

        if (slide)
        {
            entranceInProgress = true;
            shakeTarget.anchoredPosition = home + new Vector2(ResolveEntranceOffsetX(), 0f);
        }

        if (settleRoutine != null)
            yield return settleRoutine;   // offscreen'de oturur (entranceInProgress restore'u bastırır)

        // Ana menüden gelirken LoadingScreenManager ekranı kaplıyor. Slide onun
        // arkasında ziyan olmasın diye, giriş animasyonunu loading ekranı tamamen
        // kalkana kadar beklet (fade dahil). Loading yoksa (ör. editörde direkt
        // sahne) IsVisible zaten false, anında devam eder.
        if (slide)
        {
            while (LoadingScreenManager.IsVisible)
                yield return null;

            if (entranceStartDelay > 0f)
                yield return new WaitForSeconds(entranceStartDelay);

            Vector2 start = shakeTarget.anchoredPosition;
            float dur = Mathf.Max(0.05f, entranceSlideDuration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                // Hedef CANLI evden okunur: entrance sırasında ev taşınmış olabilir
                // (BossDuel ShiftBoardHome — board BottomArea üstüne yaslanır). Local
                // 'home' kopyasına lerp'lemek board'u eski merkeze geri oturtuyordu.
                shakeTarget.anchoredPosition = Vector2.LerpUnclamped(start, shakeBasePos, e);
                yield return null;
            }

            shakeTarget.anchoredPosition = shakeBasePos;
            entranceInProgress = false;
        }

        EndBusy();
    }

    private float ResolveEntranceOffsetX()
    {
        if (entranceSlideOffsetX > 0f) return entranceSlideOffsetX;

        float w = shakeTarget != null ? shakeTarget.rect.width : 0f;
        if (w < 1f)
        {
            Canvas canvas = shakeTarget != null ? shakeTarget.GetComponentInParent<Canvas>() : null;
            RectTransform cr = canvas != null && canvas.rootCanvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : null;
            w = cr != null ? cr.rect.width : 1080f;
        }
        return w + 80f;
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
    // ═══════════════════════════════════════════════════════════════
    //  Obstacle Handling
    // ═══════════════════════════════════════════════════════════════
    internal ObstacleStateService.ObstacleHitResult ApplyObstacleDamage(ObstacleDamageRequest request)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // TEŞHİS: Mud çift-hit + Grass aşınma avı. Bu satır KAÇ kez basılıyor + remaining nasıl düşüyor?
        var _traceId = obstacleStateService != null
            ? obstacleStateService.GetObstacleIdAt(request.cell.x, request.cell.y) : ObstacleId.None;
        bool traceOn = _traceId == ObstacleId.Mud || _traceId == ObstacleId.Grass;
        int _remBefore = traceOn ? obstacleStateService.GetRemainingHitsAt(request.cell.x, request.cell.y) : -1;
#endif

        var result = obstacleResolutionService != null
            ? obstacleResolutionService.ApplyDamage(request)
            : default;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (traceOn)
        {
            var _st = new System.Diagnostics.StackTrace(1, false);
            string _callers = "";
            for (int _fi = 1; _fi <= 6 && _fi < _st.FrameCount; _fi++)
                _callers += (_st.GetFrame(_fi)?.GetMethod()?.Name ?? "?") + "←";
            Debug.Log($"[ObsHit] {_traceId} cell=({request.cell.x},{request.cell.y}) ctx={request.context} " +
                      $"remaining {_remBefore}→{result.visualChange.remainingHits} cleared={result.visualChange.cleared} | via {_callers}");
        }
#endif

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

    // OBB break/patlama sesini GERÇEK burst anında çalmak için (depletion anındaki `cleared` sesi
    // BoardBreakFxService'te bastırıldı; wind-up sonrası detonation action buradan çağırır).
    internal void PlayObstacleBreakSound(ObstacleId obstacleId)
    {
        var def = LevelData?.obstacleLibrary?.Get(obstacleId);
        if (def == null || def.breakSound == null || !GameSettings.SoundEnabled)
            return;

        SfxSource?.PlayOneShot(def.breakSound, def.breakSoundVolume);
    }

    internal void MarkPatchBotForcedObstacleHit(int x, int y)
        => obstacleResolutionService?.MarkPatchBotForcedHit(x, y);

    internal void RaiseObstacleStageChanged(int originIndex, ObstacleStageSnapshot stage)
        => OnObstacleStageChanged?.Invoke(originIndex, stage);

    internal void RaiseObstacleCreatedDynamic(int x, int y)
        => OnObstacleCreatedDynamic?.Invoke(x, y);

    internal void RaiseObstacleViewRestored(int x, int y)
        => OnObstacleViewRestored?.Invoke(x, y);

    internal void RaiseBarrelResolved()
        => OnBarrelResolved?.Invoke();

    // Kırılan barrel'ın mud yayılımını HEMEN (board oturmadan) oynatır — taş akışıyla
    // eşzamanlı. Arka-plan job olarak sayılır ki splatter bitmeden board tam idle sanılmasın;
    // BarrelSpreadAction.ExecuteVisuals sequencer kullanmaz, null geçmek güvenli.
    private IEnumerator CoSpreadBarrelMudImmediate(BarrelSpreadAction.BarrelSource barrel)
    {
        // ObstacleSpread (async): mud verisi BarrelSpreadAction'da up-front commit edildiği için
        // resolve'u parklamaya gerek yok — splatter oynarken board akar, yalnız level-end bekler
        // (ActiveBackgroundJobs'ta kalır). RaiseBarrelResolved placeholder'ı erken-WIN'i önler.
        var spreadJob = BeginJob(BoardJobKind.ObstacleSpread);
        try
        {
            var action = new BarrelSpreadAction(this, new List<BarrelSpreadAction.BarrelSource> { barrel });
            yield return action.ExecuteVisuals(null);
        }
        finally
        {
            spreadJob.Dispose();
            RequestResolveAfterActionSequence();
        }
    }

    /// <summary>
    /// RocketBasketService, komşu renk match'i bir roketi tetikleyince çağırır. Roketler
    /// VURUŞ ANINDA ateşlenir: bir frame'lik buffer aynı vuruştan çıkan tetikleri (fireAllOnHit
    /// üçlüsü) tek aksiyonda toplar, sequencer'a hemen girer — mevcut kırılma animasyonu biter
    /// bitmez, cascade beklenmeden uçarlar. ResolveBoard settle bloğu güvenlik ağı olarak durur.
    /// </summary>
    public void QueueRocketLaunch(Vector2Int origin, TileType color, Sprite rocketSprite)
    {
        rocketLaunchesThisMove.Add(new RocketBasketLaunchAction.Launch
        {
            origin = origin,
            color = color,
            rocketSprite = rocketSprite
        });

        if (!rocketLaunchFlushScheduled)
        {
            rocketLaunchFlushScheduled = true;
            StartCoroutine(CoFlushRocketLaunches());
        }
    }

    private bool rocketLaunchFlushScheduled;

    private IEnumerator CoFlushRocketLaunches()
    {
        yield return null;   // aynı frame'deki tetikleri (LaunchAll) tek aksiyonda grupla
        rocketLaunchFlushScheduled = false;

        if (rocketLaunchesThisMove.Count == 0)
            yield break;

        var launches = new List<RocketBasketLaunchAction.Launch>(rocketLaunchesThisMove);
        rocketLaunchesThisMove.Clear();

        if (BoardFlowTraceEnabled)
            Debug.Log($"[RocketBasket] Immediate launch: {launches.Count} rocket(s).");
        // PatchBot gibi bağımsız uçuş: sequencer'a girmez, board'u kilitlemez; uçuş
        // aksiyon içindeki FlyingPatchBotDashes sayacıyla izlenir, impact varışta
        // sequencer'a devredilir.
        StartCoroutine(new RocketBasketLaunchAction(this, launches).ExecuteVisuals(null));
    }

    internal void EnqueueBoardAction(BoardAction action)
    {
        if (action != null)
            actionSequencer.Enqueue(action);
    }

    internal void TriggerSpecialTileFromBoardEffect(TileView tile)
    {
        if (tile == null
            || tile.X < 0 || tile.X >= Width
            || tile.Y < 0 || tile.Y >= Height
            || Tiles[tile.X, tile.Y] != tile
            || tile.GetSpecial() == TileSpecial.None)
            return;

        var actions = specialResolver != null ? specialResolver.ResolveSpecialSolo(tile) : null;
        if (actions != null && actions.Count > 0)
            StartImmediateActionSequence(actions);
    }

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
        obstacleStateService.OnOverrideBatteryBoxHit -= HandleOverrideBatteryBoxHit;
        obstacleStateService.OnOverrideBatteryBoxDetonated -= HandleOverrideBatteryBoxDetonated;
        obstacleStateService.OnBatteryHit += HandleBatteryHit;
        obstacleStateService.OnOverrideBatteryBoxHit += HandleOverrideBatteryBoxHit;
        obstacleStateService.OnOverrideBatteryBoxDetonated += HandleOverrideBatteryBoxDetonated;
        obstacleStateService.OnWardrobeOpened -= HandleWardrobeOpened;
        obstacleStateService.OnWardrobeOpened += HandleWardrobeOpened;
        obstacleStateService.OnWardrobeItemRemoved -= HandleWardrobeItemRemoved;
        obstacleStateService.OnWardrobeItemRemoved += HandleWardrobeItemRemoved;

        // Generic stacked-obstacle: üstteki kırılınca alttaki (Mud, Stone...) geri yüklenir;
        // o beneath obstacle'a görsel oluşturmak için view-restore akışına bağla. Dynamic-create
        // DEĞİL: restore edilen obstacle authored'dır, goal sayacı büyümemelidir.
        obstacleStateService.RequestObstacleViewCreate = RaiseObstacleViewRestored;
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

    private void HandleOverrideBatteryBoxHit(int originIndex, ChestColorMask color, int remaining, int progress, int total)
        => OnOverrideBatteryBoxHit?.Invoke(originIndex, color, remaining, progress, total);

    // ── Çoklu OverrideBatteryBox: SERİ detonasyon kuyruğu ─────────────────────────
    // Aynı hamlede birden fazla OBB kırılırsa AYNI ANDA patlamasınlar. Hepsinin hücresi kuyruğa
    // girerken gravity-bloklanır (altına taş girmez), sonra tek tek işlenir: biri patla → tüm board
    // hit → taşlar düş (settle) → sıradaki patla. View animasyonu da sırası gelince tetiklenir.
    private readonly System.Collections.Generic.Queue<int> _obbDetonationQueue = new System.Collections.Generic.Queue<int>();
    private bool _obbDetonationProcessing;

    /// <summary>GridSpawner dinler: sırası gelen OBB'nin detonasyon VIEW animasyonunu oynatır.</summary>
    public event System.Action<int> OnRequestObbDetonationView;

    // Patlayan her OBB'nin footprint hücreleri (2x2/NxN). Wind-up boyunca hepsi bloklu; burst'te açılır.
    private readonly System.Collections.Generic.Dictionary<int, IReadOnlyList<Vector2Int>> _obbFootprintByOrigin = new();

    private void HandleOverrideBatteryBoxDetonated(int originIndex, IReadOnlyList<Vector2Int> footprintCells)
    {
        // Kutunun TÜM footprint hücrelerini (yalnız origin DEĞİL) HEMEN gravity'den blokla → wind-up
        // sırasında 2x2'nin sağ/üst hücrelerine taş DÜŞMESİN (kullanıcı: "sağdaki hücrede taş oluyor").
        // (Kutu kendi burst'ünde ClearPendingTriggeredSpecialCells ile HEPSİNİ açar.)
        IReadOnlyList<Vector2Int> cells =
            (footprintCells != null && footprintCells.Count > 0)
                ? footprintCells
                : (width > 0
                    ? new[] { new Vector2Int(originIndex % width, originIndex / width) }
                    : null);

        if (cells != null)
        {
            _obbFootprintByOrigin[originIndex] = cells;
            SetPendingTriggeredSpecialCells(cells);
        }

        if (BoardFlowTraceEnabled)
            Debug.Log($"[OBBTL] detonated origin={originIndex} t={Time.realtimeSinceStartup:0.000} footprint={(cells != null ? cells.Count : 0)}");

        _obbDetonationQueue.Enqueue(originIndex);
        if (!_obbDetonationProcessing)
            StartCoroutine(ProcessOverrideBatteryBoxQueue());
    }

    // Çoklu OBB'de patlamalar ARASI kısa gecikme (aynı anda DEĞİL, arka arkaya). Kutular kademeli
    // başlatılır → burst'ler bu kadar aralıklı olur; board hit1 → hemen ardından hit2. Ayar düğmesi.
    private const float ObbDetonationBurstStaggerSeconds = 0.30f;
    private int _obbDetonationsRunning;

    private System.Collections.IEnumerator ProcessOverrideBatteryBoxQueue()
    {
        _obbDetonationProcessing = true;
        // Level-end + akış bu kuyruk bitene kadar beklesin (ActiveBackgroundJobs). NON-Resolve kind:
        // resolve slot'unu (BlockingBackgroundJobs) tıkamaz → aşağıdaki settle-bekleyişi deadlock olmaz.
        var job = BeginJob(BoardJobKind.ObstacleSpread);
        try
        {
            // Aynı frame'deki senkron destroy event'leri (deferred view kaydı) otursun.
            yield return null;

            // DIŞ DÖNGÜ: bir kutunun board-hit dalgası BAŞKA bir OBB'yi tamamlayınca, o kutunun
            // detonation event'i biz settle-beklerken kuyruğa girer. Onu da işle — yoksa tamamlanmış
            // kutu ekranda asılı kalır (bug). Kuyruk boşalana kadar döner.
            while (_obbDetonationQueue.Count > 0)
            {
                // Kutuları KISA stagger'la arka arkaya başlat — aralarında TAM settle beklemeyiz.
                // Böylece box1 patlar, hemen ardından box2 patlar (board hit'ler art arda).
                while (_obbDetonationQueue.Count > 0)
                {
                    int origin = _obbDetonationQueue.Dequeue();

                    _obbFootprintByOrigin.TryGetValue(origin, out var footprint);
                    _obbFootprintByOrigin.Remove(origin);

                    OnRequestObbDetonationView?.Invoke(origin);   // view wind-up + burst
                    var detonation = new OverrideBatteryBoxDetonationAction(this, origin, footprint, ownResolve: true);
                    _obbDetonationsRunning++;
                    StartCoroutine(RunObbDetonation(detonation));

                    if (_obbDetonationQueue.Count > 0)
                        yield return new WaitForSeconds(ApplySpecialChainTempo(ObbDetonationBurstStaggerSeconds));
                }

                // Bu partinin detonation'ları + tetiklediği resolve/cascade bitene kadar bekle
                // (board tam otursun). Bu sırada yeni kutu tamamlanırsa dış döngü onu yakalar.
                float safety = 0f;
                while (safety < 12f)
                {
                    // ZİNCİR HİSSİ: bir kutunun dalgası BAŞKA kutuyu bitirip kuyruğa eklediyse, box1'in
                    // TAM settle'ını BEKLEME — hemen dış döngüye dön, box2 wind-up'ını başlat (overlap).
                    // Yoksa box2 ancak box1 tamamen oturunca +2s wind-up ile patlıyor → "kopuk/geç
                    // tetiklenmedi" hissi (kullanıcı 2026-08-16).
                    if (_obbDetonationQueue.Count > 0)
                        break;

                    safety += Time.deltaTime;

                    // NOT: bekleyen resolve'un kick'i artık GLOBAL Update() pump'ında (FlowScheduler
                    // Faz A) — buradaki OBB'ye özel kick kaldırıldı, tek otorite orada.

                    bool allDone =
                        _obbDetonationsRunning == 0
                        && !resolveAfterActionSequenceRequested
                        && !resolveAfterActionSequenceRunning
                        && !IsBusy
                        && BlockingBackgroundJobs == 0
                        && !IsActionSequencePlaying;

                    if (allDone && safety > 0.1f)
                        break;
                    yield return null;
                }
            }
        }
        finally
        {
            job.Dispose();
            _obbDetonationProcessing = false;
        }
    }

    private System.Collections.IEnumerator RunObbDetonation(BoardAction detonation)
    {
        try { yield return StartCoroutine(detonation.ExecuteVisuals(actionSequencer)); }
        finally { _obbDetonationsRunning = Mathf.Max(0, _obbDetonationsRunning - 1); }
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        OnObstacleDestroyed?.Invoke(originIndex, obstacleId);

        int ox = originIndex % width;
        int oy = originIndex / width;
        if (ox < 0 || ox >= width || oy < 0 || oy >= height) return;

        // Barrel kırıldı: board'un oturmasını BEKLEMEDEN, kırılır kırılmaz mud saçılır.
        // Hedef hücreler origin'den deterministik; mud under-tile (gravity'yi bloklamaz) ve
        // damla animasyonu kareler boyunca land ettiği için akışla eşzamanlı çalışır. Taş
        // akarken mud stamp'lenir ve o an hit alabilir. Placeholder (RaiseBarrelResolved)
        // erken-WIN'i önler. Arka-plan job olarak koşar (board tam idle sayılmasın).
        if (IsMudSplatBarrel(obstacleId))
            StartCoroutine(CoSpreadBarrelMudImmediate(new BarrelSpreadAction.BarrelSource(new Vector2Int(ox, oy), obstacleId)));

        if (obstacleId == ObstacleId.Oil)
        {
            oilOverlayRenderer?.Hide(new Vector2Int(ox, oy));
            return;
        }

        if (obstacleId == ObstacleId.Safe)
        {
            // Kısa kısmi bekleme: kasa patlama+reveal'inin ilk anını, board resolve'a devam etmeden
            // oynat (taşlar açılan hücreye hemen dolup animasyonu kesmesin). Sonra bırak — kasanın
            // uzun "nefes + sönerek çıkış" kuyruğu non-blocking sürer, board bu sırada akabilir.
            StartCoroutine(CoHoldResolveForSafeBreak());
            return;
        }

        // Mud gibi under-tile katmanlar, aynı match'te yeni oluşan special'ı silmemeli.
        if (!DestroyedObstacleShouldClearTileContent(obstacleId))
            return;

        // MovableObstacle kırıldığında o hücredeki tile'ı da yok et.
        // Önce destroy (pozisyon FX'i ClearAndDestroyTile içinde yayınlanır), sonra sayım —
        // böylece "+1" driver'ın buffer'ına gain event'inden ÖNCE düşer.
        var tile = tiles[ox, oy];
        if (tile != null)
        {
            var clearedType = tile.GetTileType();
            ClearAndDestroyTile(tile);
            NotifyTilesCleared(clearedType, 1);
        }
    }

    // Safe kazan-patlamasının board tarafından beklenecek süresi (sn): basınç birikimi + patlama anı
    // bu pencerede geçer, kalan reveal/fade non-blocking sürer.
    private const float SafeBreakResolveHold = 0.5f;

    // Safe kırılınca board resolve'u KISA süre tutar (blocking background job). Yalnız patlama+reveal
    // başlangıcını korur; kasanın kuyruğunun kalanı (nefes/fade-out) beklenmez. 5sn resolve timeout
    // sızıntı emniyetidir; Begin/End her yolda eşlenir.
    private System.Collections.IEnumerator CoHoldResolveForSafeBreak()
    {
        BeginBackgroundJob();
        try
        {
            yield return new WaitForSeconds(SafeBreakResolveHold);
        }
        finally
        {
            EndBackgroundJob();
        }
    }

    private bool DestroyedObstacleShouldClearTileContent(ObstacleId obstacleId)
    {
        if (obstacleId == ObstacleId.None)
            return false;

        var def = levelData != null ? levelData.obstacleLibrary?.Get(obstacleId) : null;
        if (def == null)
            return true;

        var finalStage = def.GetStageRuleForRemainingHits(1);
        if (finalStage == null)
            return true;

        return finalStage.behavior != ObstacleBehaviorType.UnderTileLayered
            && finalStage.behavior != ObstacleBehaviorType.CellAnchoredOverlay;
    }

    private static bool IsMudSplatBarrel(ObstacleId obstacleId)
    {
        return obstacleId == ObstacleId.Barrel
            || obstacleId == ObstacleId.Barrell_v2;
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
        float duration = activeFallProfile != null && activeFallProfile.enabled
            ? activeFallProfile.FallSeconds(d)
            : d / FallVelocityCellsPerSecond;

        return Mathf.Max(0.01f, duration * Mathf.Max(0.5f, GetCascadeFallSpeedMultiplier()));
    }

    // ── Aktif düşüş profili (Royal referans ölçümü) ─────────────────
    // Kaynak: RoyalKingdom Archive.zip kare analizi (60fps, hücre=126.7px, taş takibi):
    // v0≈30px/f, a≈+1.3px/f² ile 48px/f tavana ulaşıp sabitleniyor; inişte tek karede
    // duruş + ~3px (0.024 hücre) mikro-yerleşme. Sabit-hız modelinin "sürünerek kalkış"
    // hissini bu profil giderir. Yalnızca GÖRSEL zamanlama katmanı: çökme/collapse
    // semantiği (CascadeLogic path'leri) değişmez. enabled=false → eski sabit hız.
    [Serializable]
    public class ActiveFallProfileSettings
    {
        public bool enabled = true;
        // Tempo ana düğmesi: formu (v0/a/vmax oranlarını) bozmadan tüm zamanlamayı ölçekler.
        // Büyük değer = hızlı akış. 60 = ham ölçüm temposu; kullanıcı gözle kıyaslayıp
        // 32'de karar kıldı (2026-07-22). 1 hücre ≈ 0.122s, 8 hücre ≈ 0.74s (ort ~10.8 hücre/s).
        [Min(1f)] public float fps = 35.2f;
        [Min(0f)] public float initialSpeedCellsPerFrame = 0.24f;
        [Min(0f)] public float accelerationCellsPerFrameSquared = 0.010f;
        [Min(0.001f)] public float maxSpeedCellsPerFrame = 0.38f;

        public float FallSeconds(float distanceCells)
        {
            float d = Mathf.Max(0.0001f, distanceCells);
            float v0 = Mathf.Max(0.0001f, initialSpeedCellsPerFrame);
            float a = Mathf.Max(0f, accelerationCellsPerFrameSquared);
            float vmax = Mathf.Max(v0, maxSpeedCellsPerFrame);

            float frames;
            if (a <= 0f)
            {
                frames = d / v0;
            }
            else
            {
                float t1 = (vmax - v0) / a;                  // tavana ulaşma anı (kare)
                float d1 = v0 * t1 + 0.5f * a * t1 * t1;     // o ana dek alınan yol (hücre)
                frames = d <= d1
                    ? (-v0 + Mathf.Sqrt(v0 * v0 + 2f * a * d)) / a
                    : t1 + (d - d1) / vmax;
            }

            return frames / Mathf.Max(1f, fps);
        }

        // t (kare) anındaki alınan yol (hücre).
        public float DistanceAtFrames(float t)
        {
            float v0 = Mathf.Max(0.0001f, initialSpeedCellsPerFrame);
            float a = Mathf.Max(0f, accelerationCellsPerFrameSquared);
            float vmax = Mathf.Max(v0, maxSpeedCellsPerFrame);

            if (a <= 0f) return v0 * t;

            float t1 = (vmax - v0) / a;
            if (t <= t1) return v0 * t + 0.5f * a * t * t;
            float d1 = v0 * t1 + 0.5f * a * t1 * t1;
            return d1 + vmax * (t - t1);
        }
    }

    [Header("Active Fall Profile (Royal referans)")]
    [SerializeField] private ActiveFallProfileSettings activeFallProfile = new();
    internal ActiveFallProfileSettings ActiveFallProfile => activeFallProfile;

    // Mesafeye göre normalize progress eğrisi (0..1 zaman → 0..1 yol). MoveToGridCell'in
    // mevcut curve mekanizmasına takılır; böylece TileView'a dokunmadan ivmeli form elde
    // edilir. Çeyrek-hücre çözünürlükte cache'lenir.
    private readonly Dictionary<int, AnimationCurve> fallProfileCurveCache = new();

    internal AnimationCurve GetFallProgressCurve(float distanceCells)
    {
        if (activeFallProfile == null || !activeFallProfile.enabled)
            return fallMoveCurve;

        int key = Mathf.Clamp(Mathf.RoundToInt(distanceCells * 4f), 1, 200);
        if (fallProfileCurveCache.TryGetValue(key, out var cached))
            return cached;

        float d = key / 4f;
        float totalFrames = activeFallProfile.FallSeconds(d) * Mathf.Max(1f, activeFallProfile.fps);

        const int Keys = 12;
        var keys = new Keyframe[Keys + 1];
        for (int i = 0; i <= Keys; i++)
        {
            float u = i / (float)Keys;                       // normalize zaman
            float dist = activeFallProfile.DistanceAtFrames(u * totalFrames);
            float p = Mathf.Clamp01(dist / d);               // normalize yol

            // Tanjant = anlık hız (normalize): dp/du = v(t)·T/d
            float v0 = activeFallProfile.initialSpeedCellsPerFrame;
            float a = activeFallProfile.accelerationCellsPerFrameSquared;
            float vmax = Mathf.Max(v0, activeFallProfile.maxSpeedCellsPerFrame);
            float v = Mathf.Min(v0 + a * (u * totalFrames), vmax);
            float tangent = v * totalFrames / d;

            keys[i] = new Keyframe(u, p, tangent, tangent);
        }

        var curve = new AnimationCurve(keys);
        fallProfileCurveCache[key] = curve;
        return curve;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Debug/Reference Fall/Log One Column 9 Cell Timing")]
    private void DebugLogReferenceFallOneColumn9CellTiming()
    {
        var settings = ReferenceFallMotion;
        int simulatedRows = Mathf.Max(1, height > 0 ? height : 9);
        int targetCount = Mathf.Min(9, simulatedRows);

        Debug.Log(
            $"[ReferenceFallTest] oneColumnTargets={targetCount} referenceFps={settings.referenceFps:0.##} " +
            $"spawnIntervalFrames={settings.spawnIntervalFrames:0.##} anchorAboveTopCells=0.95");

        for (int order = 0; order < targetCount; order++)
        {
            int targetRow = simulatedRows - 1 - order;
            float spawnFrame = order * settings.spawnIntervalFrames;
            float distanceCells = targetRow + 0.95f;
            float moveFrames = EstimateReferenceFallFrames(settings, distanceCells);
            float landingFrame = spawnFrame + moveFrames;

            Debug.Log(
                $"[ReferenceFallTest] order={order} targetRow={targetRow} " +
                $"spawnFrame={spawnFrame:0.0} landingFrame={landingFrame:0.0} travelledCells={distanceCells:0.00}");
        }
    }

    private static float EstimateReferenceFallFrames(ReferenceFallMotionSettings settings, float distanceCells)
    {
        float remaining = Mathf.Max(0f, distanceCells);
        float frames = 0f;
        float velocity = Mathf.Max(0f, settings.initialSpeedCellsPerFrame);
        float acceleration = Mathf.Max(0f, settings.accelerationCellsPerFrameSquared);
        float maxVelocity = Mathf.Max(0.001f, settings.maxSpeedCellsPerFrame);

        const int maxFrames = 1000;
        for (int i = 0; i < maxFrames && remaining > 0f; i++)
        {
            velocity = Mathf.Min(velocity + acceleration, maxVelocity);
            remaining -= Mathf.Max(0.001f, velocity);
            frames += 1f;
        }

        return frames;
    }
#endif

    internal float GetClearDurationForCurrentPass() => Mathf.Max(0.03f, ApplySpecialChainTempo(ClearDuration * GetCascadeClearSpeedMultiplier()));
    internal bool ShouldEnableFallSettleThisPass() => EnableFallSettle;

    // Su gibi akış: settle (iniş bounce'u) yalnızca GERÇEK SON inişte oynamalı.
    // CalculateCascades logical board'u final pozisyona güncellediği için, bu düşüşten
    // sonra hâlâ match veya doldurulacak boşluk varsa bu bir ara cascade'dir → settle kapat.
    // Böylece taşlar her ara inişte zıplayıp durmaz, akış kesilmez.
    // KARAR (2026-07-23): Settle HER inişte oynasın — kullanıcı squash'ı tüm taşlarda istiyor
    // ve 120fps'te akıcı görünüyor. Eskiden takip-match/boş-hücre olunca settle kapatılıyordu;
    // bu özellikle "special en altta oluşunca üstündekiler squeeze yapmıyor"a yol açıyordu.
    // Artık hiç kapatılmıyor (no-op). Match-zinciri fazla "adımlı" gelirse çözüm settle'ı
    // kapatmak DEĞİL, fallSettleDuration'ı kısmak.
    private void DisableSettleIfMoreCascadesFollow(List<BoardAction> cascades)
    {
        // Kasıtlı no-op — settle her zaman açık.
    }

    // Board momentarily idle görünse de resolve loop'un HÂLÂ otomatik işleyeceği DETERMİNİSTİK iş.
    // Async/uçuşta olan iş (oil spread=IsBusy resolve-loop içi, barrel splatter/keygen=BeginBackgroundJob,
    // fırlatılmış rocket/orb/patchbot=flight sayaçları) artık ActiveBackgroundJobs üzerinden
    // IsBoardWorkingForLevelEnd tarafından beklenir — burada tekrar sayılmaz (eski "parça parça"
    // whitelist kaldırıldı). Yalnız kuyruğa alınmış ama HENÜZ fırlatılmamış rocket bir frame'lik
    // boşlukta background-job'a dönüşmeden önce burada görünür kalmalı.
    internal bool HasPendingAutoResolveForLevelEnd()
    {
        return (cascadeLogic != null && cascadeLogic.HasAnyResolvableEmptyPlayableCell())
            || (matchFinder != null && matchFinder.FindAllMatches().Count > 0)
            || rocketLaunchesThisMove.Count > 0
            || HasBottomExitCargoReady();
    }

    // Level-end teşhisi: fail ertelemesi hangi pending koşuldan geliyor?
    internal string DescribePendingAutoResolveForLevelEnd()
    {
        return $"emptyPlayable={cascadeLogic != null && cascadeLogic.HasAnyEmptyPlayableCell()}, " +
               $"resolvableEmpty={cascadeLogic != null && cascadeLogic.HasAnyResolvableEmptyPlayableCell()}, " +
               $"matches={(matchFinder != null ? matchFinder.FindAllMatches().Count : 0)}, " +
               $"rocketQueue={rocketLaunchesThisMove.Count}, " +
               $"bottomCargo={HasBottomExitCargoReady()}, " +
               $"activeBgJobs={ActiveBackgroundJobs}";
    }

    // Cargo (exitAtBottom) çıkışı: yalnızca altında sütun sonuna kadar gerçek pass-through void varsa toplanır.
    // Aradaki mask hole'lar normal düşüş boşluğu sayılır; aşağıda yaşayan hücre varsa cargo oraya inmeli.
    private bool IsBottomVoidBelow(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        for (int by = y + 1; by < height; by++)
        {
            if (!TryGetCellState(x, by, out var state))
                return false;

            if (state.isPassThroughVoid)
                continue;

            return false;
        }

        return true;
    }

    private bool HasBottomExitCargoReady()
    {
        if (obstacleStateService == null || height <= 0 || width <= 0)
            return false;

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (obstacleStateService.IsExitAtBottomAt(x, y) && IsBottomVoidBelow(x, y))
                return true;
        }

        return false;
    }

    // En alt satıra inen Cargo (exitAtBottom) obstacle'larını board'dan çıkarır:
    // hücre verisini temizler (OnObstacleDestroyed → goal +1), tile'ı tabandan aşağı
    // animasyonla süzer. En az biri toplandıysa true döner (resolve loop refill etsin).
    internal bool TryCollectBottomExitCargo()
    {
        if (obstacleStateService == null || height <= 0 || width <= 0)
            return false;

        bool any = false;

        // Sütun başına alttan yukarı: altı void olan en alttaki cargo toplanır. Üstteki cargo bu pass'te
        // toplanmaz (altı artık normal boş hücre); resolve loop tekrar edip onu aşağı düşürür.
        for (int x = 0; x < width; x++)
        for (int y = height - 1; y >= 0; y--)
        {
            if (!obstacleStateService.IsExitAtBottomAt(x, y))
                continue;
            if (!IsBottomVoidBelow(x, y))
                continue;

            var tile = tiles[x, y];

            // Goal HUD slotunu önceden al (robot oraya zıplayacak).
            RectTransform goalSlot = null;
            var id = obstacleStateService.GetObstacleIdAt(x, y);
            TopHud?.TryGetGoalTargetRectForObstacle(id, out goalSlot);

            // Tile'ı önce ayır: CollectExitObstacleAt → OnObstacleDestroyed → HandleObstacleDestroyed
            // bu hücredeki tile'ı da yok etmeye çalışmasın (biz ayrıca animasyonla çıkarıyoruz).
            tiles[x, y] = null;
            gridData[x, y] = null;

            var collected = obstacleStateService.CollectExitObstacleAt(x, y);
            if (collected == ObstacleId.None)
            {
                tiles[x, y] = tile;   // beklenmedik: geri koy
                continue;
            }

            SetHoleStateFromObstacle(x, y);

            if (tile != null)
                StartCoroutine(CargoExitRoutine(tile, goalSlot));

            any = true;
        }

        return any;
    }

    // İşçi robot çıkışı: bir hücre aşağı düşer → diz büküp yere konar → TopHUD goal slotuna zıplar.
    // Uçuş async: board akmaya devam eder (goal-orb flight gibi), yalnız level-end bunu bekler.
    // Tile verisi anında gridden çıkıyor (SetActive(false)+Destroy) → §3a karşılanıyor.
    private IEnumerator CargoExitRoutine(TileView tile, RectTransform goalSlot)
    {
        BeginGoalOrbFlight();
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
            EndGoalOrbFlight();
        }
    }

    private float GetCascadeFallSpeedMultiplier()
    {
        // Eski: pass 2+ = 0.50 → taşlar 2x hızlı → "pat pat"
        // Yeni: kademeli hızlanma, asla 2x'i geçmez.
        // NOT: Düşüş HISSI buradan gelir; kullanıcı hızlı cascade düşüşünü sevmedi →
        // orijinal (yumuşak) değerlere geri alındı. Fall hızına dokunma.
        if (CurrentResolvePass <= 1) return 1f;
        if (CurrentResolvePass <= 2) return 0.85f;
        if (CurrentResolvePass <= 4) return 0.75f;
        return 0.70f;
    }

    private float GetCascadeClearSpeedMultiplier()
    {
        // Cascade clear'ları neredeyse-flash: ara round'larda clear-beat'i kısalt → düşüş
        // hemen başlar, zincir sıkışır. İlk pass juicy kalır.
        if (CurrentResolvePass <= 1) return 1f;
        if (CurrentResolvePass <= 2) return 0.55f;
        return 0.42f;
    }
    private TileType GetRandomType() => randomPool[UnityEngine.Random.Range(0, randomPool.Length)];
    private void ConsumeMove()
    {
        BeginMoveClearPraiseTracking();
        RemainingMoves = Mathf.Max(0, RemainingMoves - 1);
        OnMovesChanged?.Invoke(RemainingMoves);
    }
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
    internal void NotifyTilesCleared(TileType tileType, int amount)
    {
        if (amount <= 0)
            return;

        if (moveClearPraiseTracking && !moveClearPraiseEmitted)
            moveClearPraiseCount += amount;

        OnTilesCleared?.Invoke(tileType, amount);
        GameEventBus.EmitTileCleared(tileType, amount);
    }

    private void BeginMoveClearPraiseTracking()
    {
        moveClearPraiseCount = 0;
        moveClearPraiseTracking = true;
        moveClearPraiseEmitted = false;
        EnsureMoveClearPraisePopup();
    }

    private void EmitMoveClearPraiseIfNeeded()
    {
        if (!moveClearPraiseTracking || moveClearPraiseEmitted)
            return;

        int cleared = moveClearPraiseCount;
        moveClearPraiseTracking = false;
        moveClearPraiseCount = 0;
        moveClearPraiseEmitted = true;

        if (cleared < 30)
            return;

        EnsureMoveClearPraisePopup();
        OnMoveClearPraise?.Invoke(cleared);
    }

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

    private void EnsureMoveClearPraisePopup()
    {
        if (moveClearPraisePopup == null)
            moveClearPraisePopup = FindFirstObjectByType<MoveClearPraisePopupController>(FindObjectsInactive.Include);

        if (moveClearPraisePopup == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            var existing = canvas.transform.Find("MoveClearPraisePopup");
            if (existing != null)
                moveClearPraisePopup = existing.GetComponent<MoveClearPraisePopupController>();

            if (moveClearPraisePopup == null)
            {
                var go = new GameObject("MoveClearPraisePopup", typeof(RectTransform), typeof(MoveClearPraisePopupController));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                moveClearPraisePopup = go.GetComponent<MoveClearPraisePopupController>();
            }
        }

        if (moveClearPraisePopup != null)
        {
            moveClearPraisePopup.transform.SetAsLastSibling();
            moveClearPraisePopup.Bind(this);
        }
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
