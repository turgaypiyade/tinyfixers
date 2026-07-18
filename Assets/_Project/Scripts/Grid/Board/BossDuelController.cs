using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battlefield / Robot Düellosu (LevelKind.BossDuel).
///
/// İki robot karşılıklı durur: sol = oyuncu (yeşil HP), sağ = düşman (mor HP).
/// - Her hamlede temizlenen TAŞ SAYISI kadar oyuncu robotu rapid-fire lazer atar;
///   her atış düşman HP'sinden hasar düşürür (büyük combo = çok atış = çok hasar).
/// - Her hamleden SONRA düşman, kısa bir telgraf (yüklenme) sonrası oyuncuya lazer
///   atar; hasar her saldırıda artar (escalating). Opsiyonel oil baskısı da yapar.
/// - Düşman HP = Collectible/BossDamage goal (0 olunca mevcut WIN akışı tetiklenir).
/// - Oyuncu HP 0 olunca board.RequestLevelFail() ile LOSE.
/// - Hamle SINIRLI: level'ın moves değeri normal levellerdeki gibi tükenir; hamle
///   bitince (kuyruktaki vuruşlar boşaldıktan sonra) standart fail akışı çalışır.
///
/// Sahne kurulumu: BoardContent yanına ekle; board + topHud + iki robot RectTransform +
/// iki HpBar + vfxRoot + bolt/impact prefab referanslarını bağla. Görseller placeholder
/// olabilir; mantık sprite'sız da çalışır.
/// </summary>
public sealed class BossDuelController : MonoBehaviour
{
    private const ObstacleId PlayerShieldPickupId = ObstacleId.PlayerShieldPickup;
    private const ObstacleId EnemyShieldPickupId = ObstacleId.EnemyShieldPickup;

    [Header("Core Refs")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [Tooltip("Açılış animasyonu: iki parça soldan/sağdan gelip ortada birleşir, sonra oyun açılır. Boş bırakılırsa intro atlanır.")]
    [SerializeField] private BossDuelIntroController intro;
    [Tooltip("Arena arka plan Image'ı. LevelData.battlefieldBackground atanırsa sprite buna uygulanır; boşsa mevcut kalır.")]
    [SerializeField] private Image arenaBackground;

    [Header("Robots")]
    [SerializeField] private RectTransform playerRobot;     // sol
    [SerializeField] private RectTransform enemyRobot;       // sağ
    [Tooltip("İki top-kol (ikisi de öne ateş eder). Geri tepme bunlarda olur; boşsa gövde tween'lenir.")]
    [SerializeField] private RectTransform playerArmA;
    [SerializeField] private RectTransform playerArmB;
    [SerializeField] private RectTransform enemyArmA;
    [SerializeField] private RectTransform enemyArmB;
    [Tooltip("Her kolun namlu ucu (lazer çıkış noktası). Boşsa robot merkezinden çıkar.")]
    [SerializeField] private RectTransform playerMuzzleA;
    [SerializeField] private RectTransform playerMuzzleB;
    [SerializeField] private RectTransform enemyMuzzleA;
    [SerializeField] private RectTransform enemyMuzzleB;

    [Header("HP Bars")]
    [SerializeField] private HpBar playerHpBar;
    [SerializeField] private HpBar enemyHpBar;

    [Header("Bolt / VFX")]
    [Tooltip("Lazer mermisi spawn'lanacak kök (robotların ve board'ın üstünde bir RectTransform).")]
    [SerializeField] private RectTransform vfxRoot;
    [Tooltip("Oyuncu lazer mermisi (yeşil). Boşsa beyaz dikdörtgen üretilir.")]
    [SerializeField] private Image playerBoltPrefab;
    [Tooltip("Düşman lazer mermisi (kırmızı).")]
    [SerializeField] private Image enemyBoltPrefab;
    [Tooltip("Oyuncu namlu çakması (muzzleLeft).")]
    [SerializeField] private Image playerMuzzleFlashPrefab;
    [Tooltip("Düşman namlu çakması (muzzleRight).")]
    [SerializeField] private Image enemyMuzzleFlashPrefab;
    [SerializeField] private Image impactPrefab;
    [SerializeField] private Vector2 boltSize = new Vector2(64f, 18f);
    [Tooltip("Renkli sprite kullanıyorsan BEYAZ bırak (sprite'ı yeniden boyamasın). Placeholder için renk girilebilir.")]
    [SerializeField] private Color playerBoltColor = Color.white;
    [SerializeField] private Color enemyBoltColor = Color.white;

    [Header("Audio")]
    [Tooltip("Lazer atış sesi (her bolt çıkışında).")]
    [SerializeField] private AudioClip fireSfx;
    [Tooltip("İsabet sesi (bolt hedefe varınca).")]
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;
    [Tooltip("Her seste rastgele perde sapması (monotonluğu kırar).")]
    [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.08f;
    [Tooltip("Boşsa runtime'da otomatik AudioSource eklenir.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Strike Feel")]
    [SerializeField, Min(0.01f)] private float strikeInterval = 0.07f;     // atışlar arası
    [SerializeField, Min(0.02f)] private float boltTravelDuration = 0.12f;
    [SerializeField, Min(0f)] private float recoilDistance = 16f;
    [SerializeField, Min(0.02f)] private float recoilDuration = 0.08f;

    [Header("Enemy Turn")]
    [Tooltip("Düşmanın ateşten önceki yüklenme (telgraf) süresi.")]
    [SerializeField, Min(0f)] private float enemyTelegraphDuration = 0.4f;
    [Tooltip("Düşman kaç saniyede bir ateş eder (oyuncu idle olsa da işler).")]
    [SerializeField, Min(0.3f)] private float enemyAttackInterval = 2f;
    [Tooltip("Vurulunca robotun geri sarsılma süresi.")]
    [SerializeField, Min(0f)] private float robotHitKnockDuration = 0.18f;
    [Tooltip("Vurulunca robotun geri itilme mesafesi (px).")]
    [SerializeField, Min(0f)] private float robotHitKnockback = 22f;

    [Header("Shield Pickups")]
    [Tooltip("Her PlayerShieldPickup OYUNCUYA kaç düşman vuruşu bloklayan kalkan verir. " +
             "Süre değil VURUŞ bazlı — düşman ateş etmeden sönme problemi yaşanmaz.")]
    [SerializeField, Min(1)] private int playerShieldHitsPerPickup = 2;
    [Tooltip("Her EnemyShieldPickup düşmana kaç saniyelik kalkan verir (oyuncu vuruşları hızlı " +
             "aktığı için düşman tarafı süre bazlı kalır).")]
    [SerializeField, Min(0.5f)] private float enemyShieldSecondsPerPickup = 2.5f;
    [Tooltip("Robot etrafındaki kalkan balonunun boyut çarpanı.")]
    [SerializeField, Min(0.5f)] private float shieldBubbleScale = 1.25f;
    [SerializeField, Min(1f)] private float shieldBubbleMinSize = 180f;
    [SerializeField] private Image playerShieldBubble;
    [SerializeField] private Image enemyShieldBubble;
    [Tooltip("Ortak fallback kalkan sprite'ı. Per-side sprite atanmazsa bu kullanılır; o da boşsa otomatik daire üretilir.")]
    [SerializeField] private Sprite shieldBubbleSprite;
    [Tooltip("Oyuncu robotunun kalkan sprite'ı (yumuşak enerji bubble/dome). Boşsa shieldBubbleSprite kullanılır.")]
    [SerializeField] private Sprite playerShieldSprite;
    [Tooltip("Düşman robotunun kalkan sprite'ı (lazer halka / barrier ring). Boşsa shieldBubbleSprite kullanılır.")]
    [SerializeField] private Sprite enemyShieldSprite;
    [SerializeField] private Color playerShieldColor = new Color(0.25f, 1f, 0.45f, 0.42f);
    [Tooltip("Düşman moru/kırmızısı — HP barıyla aynı dilde olsun ki oyuncu kalkanından ayrışsın.")]
    [SerializeField] private Color enemyShieldColor = new Color(0.78f, 0.32f, 1f, 0.42f);
    [SerializeField, Min(0f)] private float shieldAbsorbPulseDuration = 0.16f;

    [Header("Waves")]
    [Tooltip("Dalga geçişinde oyuncunun iyileşme oranı (maks HP yüzdesi). Yeni dalgaya nefesle girilsin.")]
    [SerializeField, Range(0f, 1f)] private float playerHealPerWavePct = 0.15f;
    [Tooltip("Yeni dalga robotunun sağdan giriş mesafesi (px).")]
    [SerializeField, Min(0f)] private float enemyEntranceOffset = 420f;
    [Tooltip("Yeni dalga robotunun giriş süresi (sn).")]
    [SerializeField, Min(0.05f)] private float waveEntranceDuration = 0.45f;

    [Header("Win Celebration")]
    [SerializeField, Min(0f)] private float winHopHeight = 40f;
    [SerializeField, Min(0.05f)] private float winHopDuration = 0.32f;
    [SerializeField, Min(1)] private int winHopCount = 2;
    [SerializeField, Min(1f)] private float winScalePunch = 1.18f;

    [Header("Win Fireworks")]
    [Tooltip("Kazanınca çalan havai fişek prefab'ı (ParticleSystem ya da UI efekt). Boşsa basit renkli patlamalar üretilir.")]
    [SerializeField] private GameObject winFireworksPrefab;
    [Tooltip("Tek patlama sprite'ı (BEYAZ/grayscale çiz; kod renklendirir). Boşsa renkli kare.")]
    [SerializeField] private Sprite winFireworkBurstSprite;
    [SerializeField, Min(0.3f)] private float winFireworksDuration = 2.5f;

    [Header("Defeat (yenilince yığın)")]
    [Tooltip("Robot gövde Image'ları — yenilince/kazanınca sprite'ı değişir.")]
    [SerializeField] private Image playerBodyImage;
    [SerializeField] private Image enemyBodyImage;
    [Tooltip("Yenilgi (yığın) sprite'ları. Boşsa sadece çökme tween'i oynar.")]
    [SerializeField] private Sprite playerDefeatedSprite;
    [SerializeField] private Sprite enemyDefeatedSprite;
    [SerializeField, Min(0f)] private float defeatCollapseDuration = 0.35f;
    [Tooltip("Zafer (victory) sprite'ları. Boşsa kazanan sadece hop+scale kutlaması yapar (sprite değişmez).")]
    [SerializeField] private Sprite playerWinSprite;
    [SerializeField] private Sprite enemyWinSprite;

    [Header("Counterplay (Faz 2)")]
    [Tooltip("Renk zayıflığı ikonunun sprite kaynağı. Boşsa ikon yerine renk rozeti çizilir.")]
    [SerializeField] private TileIconLibrary tileIconLibrary;

    [Header("Toast / Bildirimler")]
    [Tooltip("AÇIK: toast konumu otomatik hesaplanır — robotların alt kenarının hemen altı. " +
             "KAPALI: aşağıdaki sabit offset kullanılır.")]
    [SerializeField] private bool toastAutoPosition = true;
    [Tooltip("Otomatik konuma eklenecek dikey boşluk (px, robot altından aşağı).")]
    [SerializeField] private float toastAutoGap = 30f;
    [Tooltip("toastAutoPosition KAPALIYKEN kullanılan konum — vfxRoot merkezinden offset (px).")]
    [SerializeField] private Vector2 toastAnchoredPos = new Vector2(0f, -150f);
    [Tooltip("Normal toast'ın ekranda kalma süresi (sn).")]
    [SerializeField, Min(0.4f)] private float toastDefaultDuration = 1.4f;

    [Header("Süper Lazer (Faz 3)")]
    [Tooltip("Barın dolması için kırılması gereken taş sayısı.")]
    [SerializeField, Min(5)] private int superLaserChargeTiles = 40;
    [Tooltip("Süper lazer hasarı = damagePerTile × bu çarpan. Kalkanı DELER ve söker.")]
    [SerializeField, Min(1f)] private float superLaserDamageMult = 12f;

    [Header("Special Bonus (Faz 3)")]
    [Tooltip("LineH/LineV aktivasyonu başına bonus vuruş.")]
    [SerializeField, Min(0)] private int lineBonusStrikes = 5;
    [Tooltip("PulseCore aktivasyonu başına bonus vuruş.")]
    [SerializeField, Min(0)] private int pulseBonusStrikes = 8;
    [Tooltip("SystemOverride aktivasyonu başına bonus vuruş.")]
    [SerializeField, Min(0)] private int overrideBonusStrikes = 12;
    [Tooltip("PatchBot boss'a direkt uçar: hasar = damagePerTile × bu çarpan.")]
    [SerializeField, Min(1f)] private float patchBotDamageMult = 6f;

    [Header("Board Yerleşimi")]
    [Tooltip("BossDuel'de board'un ALT kenarı bu rect'in ÜSTÜNE hizalanır (BottomArea'yı sürükle). " +
             "Boşsa board yerinden oynatılmaz. Üstte robotlar/HUD için maksimum alan açılır.")]
    [SerializeField] private RectTransform boardBottomAnchor;
    [Tooltip("Board alt kenarı ile anchor üstü arasındaki boşluk (px, board ölçeğinde).")]
    [SerializeField] private float boardBottomGap = 10f;

    // ── State ──
    private bool bossModeActive;
    // Temizlenen taşlar TİP etiketiyle kuyruğa girer (renk zayıflığı çarpanı için).
    private readonly Queue<TileType> strikeQueue = new();
    private int strikeIndex;      // kol sırası (sol-sağ-sol...)
    private int boltsInFlight;            // havadaki oyuncu boltları (hasar varışta işler)
    private bool endEvalHoldActive;       // hamle bitti ama vuruşlar boşalmadı — fail eval beklesin

    // ── Counterplay state ──
    private TileType currentWeakType;
    private float weaknessTimer;
    private Image weaknessIcon;

    private bool chargeActive;
    private float chargeCooldown;
    private int chargeTilesBroken;   // şarj penceresi içinde kırılan taş sayısı
    private Image chargeRing;
    private TMP_Text chargeCounterText;

    private float stunRemaining;
    private bool stunVisualActive;

    // ── Toast kuyruğu (olay bildirimleri — üst üste binmez, sırayla oynar) ──
    private readonly Queue<(string text, float duration, bool strong)> toastQueue = new();
    private Coroutine toastRunner;
    private TMP_Text weaknessMultLabel;   // rozetin "×2" etiketi
    private TMP_Text chargeBreakLabel;    // ring'in "KIR!" etiketi

    // ── Faz 3: Süper lazer / special bonus / pickup spawner ──
    private int laserCharge;              // birikmiş taş (superLaserChargeTiles'a kadar)
    private bool laserReady;
    private bool laserFiring;
    private Image laserFillImage;         // radyal dolum (kalın halka)
    private Image laserGlowImage;         // rozet arkası parıltı
    private Image laserCoreImage;         // içte biriken enerji topu
    private RectTransform laserIconRt;    // merkez ikon (hazırken döner)
    private Button laserButton;
    private RectTransform laserBadgeRoot;
    private int bonusStrikePool;          // special aktivasyonlarından gelen düz vuruşlar
    private float playerPickupTimer;
    private float enemyPickupTimer;

    private int enemyHp, enemyMaxHp;
    private int playerHp, playerMaxHp;
    private int enemyAttackCount;
    private int movesSinceOil;

    private int damagePerTile;
    private int enemyBaseDamage;
    private int enemyDamageGrowth;
    private int playerShieldHits;        // oyuncu kalkanı VURUŞ bazlı (kalan blok sayısı)
    private float enemyShieldRemaining;  // düşman kalkanı süre bazlı

    // ── Dalga durumu ──
    private BossDifficulty.WaveParams[] waves;
    private int waveIndex;
    private bool waveTransitionActive;   // geçiş boyunca iki taraf da ateş etmez; strikes birikir
    private int waveOilCount;
    private int waveOilEveryMoves;
    private Sprite enemyOriginalBodySprite;
    private Color enemyOriginalBodyColor = Color.white;
    private Vector2 enemyHomePos;
    private Vector3 enemyHomeScale;
    private static Sprite generatedPlayerShieldSprite;   // yumuşak dome
    private static Sprite generatedEnemyShieldSprite;     // lazer halka

    private readonly Dictionary<RectTransform, Coroutine> _hitCo = new();
    private readonly Dictionary<RectTransform, Vector2> _hitBase = new();
    private bool winCelebrationPlayed;
    private bool enemyDefeated;
    private bool playerDefeated;

    private void Start() => StartCoroutine(InitWhenLevelReady());

    private IEnumerator InitWhenLevelReady()
    {
        while (board != null && board.ActiveLevelData == null)
            yield return null;

        var level = board != null ? board.ActiveLevelData : null;

        if (board == null || level == null || level.levelKind != LevelKind.BossDuel)
        {
            SetRobotsVisible(false);
            intro?.HideImmediate();   // boss değil → board'u baştan örten overlay'i hemen kaldır
            enabled = false;
            yield break;
        }

        int totalEnemyHp = ReadBossGoalAmount(level);
        if (totalEnemyHp <= 0)
        {
            Debug.LogWarning("[Battlefield] BossDamage goal'ü yok/0 — düello çalışamaz. Level goals'a Collectible=BossDamage ekleyin.");
            intro?.HideImmediate();
            enabled = false;
            yield break;
        }

        bossModeActive = true;

        // Zayıflık rozeti için ikon kaynağı: atanmadıysa yüklü asset'lerden bul
        // (TopHud vb. referansladığı için oyun sahnesinde hep yüklüdür).
        if (tileIconLibrary == null)
        {
            var libs = Resources.FindObjectsOfTypeAll<TileIconLibrary>();
            if (libs != null && libs.Length > 0)
                tileIconLibrary = libs[0];
        }

        // Dalga listesi: authored bossWaves varsa o, yoksa BossDifficulty formülü.
        // Dalga 1 parametreleri level'ın Battlefield alanlarından gelir (eski davranış birebir).
        waves = BossDifficulty.BuildWaves(level, totalEnemyHp);
        playerMaxHp = Mathf.Max(1, level.playerMaxHp);
        playerHp = playerMaxHp;
        damagePerTile = Mathf.Max(0, level.damagePerClearedTile);

        // Level bazlı arena arka planı: atanmışsa uygula, boşsa sahnedeki mevcut kalır.
        if (arenaBackground != null && level.battlefieldBackground != null)
            arenaBackground.sprite = level.battlefieldBackground;

        // Board'u BottomArea'nın üstüne yasla — üstte düello sahnesi için alan açılır.
        yield return AlignBoardAboveBottomArea();

        SetRobotsVisible(true);
        playerHpBar?.Init(playerMaxHp);

        // Robotun ev pozisyonu/gövde sprite'ı BİR KEZ yakalanır — dalga geçişinde çöküş
        // tween'i sonrası buradan tazelenir (shake-drift dersinin aynısı: home'u canlı okuma).
        CaptureEnemyHomeState();
        StartWave(0);

        // Açılış: iki parça soldan/sağdan gelip ortada birleşir; bu sırada board kilitli.
        if (intro != null && intro.HasIntro)
        {
            board.SetInputLocked(true);
            yield return intro.Play();
            board.SetInputLocked(false);
        }

        board.OnTilesCleared += HandleTilesCleared;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;
        board.OnSpecialActivated += HandleSpecialActivated;

        EnsureSuperLaserUI();

        EnsureShieldBubble(ref playerShieldBubble, playerRobot, playerShieldColor, playerShieldSprite, isEnemy: false);
        EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
        UpdateShieldVisual(playerShieldBubble, playerShieldHits, playerShieldColor, playerRobot);
        UpdateShieldVisual(enemyShieldBubble, enemyShieldRemaining, enemyShieldColor, enemyRobot);

        StartCoroutine(BattleLoop());
    }

    private void OnDestroy()
    {
        if (board == null) return;
        ReleaseEndEvalHold();
        board.OnTilesCleared -= HandleTilesCleared;
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.OnSpecialActivated -= HandleSpecialActivated;
    }

    // BossDuel'de board'un görsel alt kenarını boardBottomAnchor'ın (BottomArea) üstüne hizalar.
    // Ev pozisyonu ShiftBoardHome ile taşınır — shake/entrance yeni evi kullanır.
    private IEnumerator AlignBoardAboveBottomArea()
    {
        if (boardBottomAnchor == null || board == null)
            yield break;

        // Grid spawn + layout otursun (tileSize, rect'ler, canvas ölçekleri).
        for (int wait = 0; wait < 30 && board.TileSize <= 0f; wait++)
            yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();

        var shakeTarget = board.ShakeTarget;
        if (shakeTarget == null || shakeTarget.parent is not RectTransform shakeParent)
            yield break;
        if (board.Height <= 0 || board.TileSize <= 0f)
            yield break;

        // Board'un görsel alt kenarı (world): son satır merkezinin yarım hücre altı.
        Vector3 lastRowCenter = board.GetCellWorldCenterPosition(0, board.Height - 1);
        float cellWorldH = board.Height >= 2
            ? Mathf.Abs(lastRowCenter.y - board.GetCellWorldCenterPosition(0, board.Height - 2).y)
            : Mathf.Abs(board.TileSize * shakeTarget.lossyScale.y);
        if (cellWorldH <= 0.0001f)
            yield break;

        float boardBottomY = lastRowCenter.y - cellWorldH * 0.5f;

        var corners = new Vector3[4];
        boardBottomAnchor.GetWorldCorners(corners);
        float anchorTopY = corners[1].y;   // sol-üst köşe

        float gapWorld = boardBottomGap * (cellWorldH / board.TileSize);
        float deltaWorldY = (anchorTopY + gapWorld) - boardBottomY;

        Vector2 deltaAnchored = shakeParent.InverseTransformVector(new Vector3(0f, deltaWorldY, 0f));
        if (Mathf.Abs(deltaAnchored.y) > 0.5f)
            board.ShiftBoardHome(new Vector2(0f, deltaAnchored.y));
    }

    private int ReadBossGoalAmount(LevelData level)
    {
        if (level?.goals == null) return 0;
        foreach (var g in level.goals)
            if (g != null && g.targetType == LevelGoalTargetType.Collectible && g.collectibleId == CollectibleId.BossDamage)
                return Mathf.Max(0, g.amount);
        return 0;
    }

    private void SetRobotsVisible(bool visible)
    {
        // Battlefield'a ait GÖRÜNÜR öğeler BossDuel olmayan levellarda gizlenir.
        // vfxRoot'a DOKUNMUYORUZ: boş bir konteyner (normal levelda görünür bir şey yok) ve
        // yanlışlıkla paylaşılan VFXRoot atanmışsa onu kapatmak PatchBot/line VFX'i bozar.
        if (playerRobot != null) playerRobot.gameObject.SetActive(visible);
        if (enemyRobot != null) enemyRobot.gameObject.SetActive(visible);
        if (playerHpBar != null) playerHpBar.gameObject.SetActive(visible);
        if (enemyHpBar != null) enemyHpBar.gameObject.SetActive(visible);
    }

    // ── Sürekli akış: temizlenen taşlar stack'e birikir, BattleLoop boşaltır ──

    private void HandleTilesCleared(TileType type, int amount)
    {
        if (!bossModeActive || amount <= 0 || IsOver())
            return;

        // Oyuncu matlemeye devam ettikçe vuruşlar (tip etiketli) kuyruğa birikir; input kilidi yok.
        for (int i = 0; i < amount; i++)
            strikeQueue.Enqueue(type);

        // Şarj penceresi açıksa kırılan her taş kesme sayacına işler.
        if (chargeActive)
            chargeTilesBroken += amount;

        // Süper lazer şarjı (hazırken birikmez — önce kullan).
        if (!laserReady)
        {
            laserCharge = Mathf.Min(superLaserChargeTiles, laserCharge + amount);
            if (laserCharge >= superLaserChargeTiles)
            {
                laserReady = true;
                ShowTeachableToast("boss_tip_laser_seen",
                    "boss_tip_laser", "Süper lazer hazır! Butona bas — kalkan delen dev atış.",
                    "boss_toast_laser_ready", "Süper lazer hazır!");
            }
            RefreshSuperLaserVisual();
        }
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (!bossModeActive || !change.cleared)
            return;

        if (change.obstacleId == PlayerShieldPickupId)
        {
            AddShield(toPlayer: true);
            return;
        }

        if (change.obstacleId == EnemyShieldPickupId)
            AddShield(toPlayer: false);
    }

    private bool IsLastWave => waves == null || waves.Length == 0 || waveIndex >= waves.Length - 1;

    // Dalga geçişi sırasında (enemyHp=0 ama sıradaki dalga var) düello BİTMEMİŞTİR —
    // strikes birikmeye devam eder, BattleLoop yaşar.
    private bool IsOver() => playerHp <= 0 || !bossModeActive || (enemyHp <= 0 && IsLastWave);

    // Son hamle harcandığında kuyrukta/havada hâlâ vuruş olabilir; bunlar boss'u öldürüp
    // WIN getirebilir. Fail değerlendirmesi (ActiveBackgroundJobs okur) vuruşlar boşalana
    // kadar beklesin — resolve döngüsünü parketmeyen goal-orb tarzı sayaçla tutulur.
    private void TickEndEvalHold()
    {
        bool draining = strikeQueue.Count > 0 || bonusStrikePool > 0 || boltsInFlight > 0 || laserFiring;
        bool shouldHold = bossModeActive && !IsOver() && board != null
                          && board.RemainingMoves <= 0 && draining;

        if (shouldHold == endEvalHoldActive)
            return;

        endEvalHoldActive = shouldHold;
        if (shouldHold) board.BeginBossStrikeDrain();
        else board.EndBossStrikeDrain();
    }

    private void ReleaseEndEvalHold()
    {
        if (!endEvalHoldActive)
            return;
        endEvalHoldActive = false;
        board?.EndBossStrikeDrain();
    }

    // Kalıcı dövüş döngüsü: oyuncu stack'ten otomatik ateş eder (backlog yüksekse hızlanır),
    // düşman kendi saatinde (idle dahil) ateşler. Input asla kilitlenmez.
    private IEnumerator BattleLoop()
    {
        float strikeTimer = 0f;
        float enemyTimer = 0f;
        bool enemyBusy = false;

        while (bossModeActive && !IsOver())
        {
            float dt = Time.deltaTime;
            TickEndEvalHold();

            // Dalga geçişi: iki taraf da ateş etmez, timer'lar sıfır tutulur (geçiş bitince
            // düşman anında ateşlemesin). pendingStrikes birikmeye devam eder — kayıp yok.
            if (waveTransitionActive)
            {
                strikeTimer = 0f;
                enemyTimer = 0f;
                yield return null;
                continue;
            }

            TickShields(dt);
            TickWeakness(dt);
            TickStun(dt);
            // Popup açıkken (açık kilit) şarj penceresi/cooldown donar — oyuncu taş kıramaz,
            // pencere haksız yere akmasın.
            if (!board.IsExplicitlyLocked)
                TickChargeScheduling(dt, enemyBusy);
            TickPickupSpawner(dt);
            if (laserReady)
                RefreshSuperLaserVisual();   // hazırken nabız animasyonu

            // Oyuncu: kuyruktan boşalt. Backlog büyükse tek tick'te birden fazla ateşle (yetiş).
            // Special bonus havuzu (bonusStrikePool) taş kuyruğundan SONRA, düz hasarla akar.
            strikeTimer += dt;
            if ((strikeQueue.Count > 0 || bonusStrikePool > 0) && damagePerTile > 0 && strikeTimer >= strikeInterval)
            {
                strikeTimer = 0f;
                int backlog = strikeQueue.Count + bonusStrikePool;
                int burst = Mathf.Clamp(Mathf.CeilToInt(backlog * 0.25f), 1, 4);
                for (int i = 0; i < burst && !IsOver(); i++)
                {
                    if (strikeQueue.Count > 0)
                        FireOnePlayerStrike(strikeQueue.Dequeue());
                    else if (bonusStrikePool > 0)
                    {
                        bonusStrikePool--;
                        FireOnePlayerStrike(currentWeakType, forceNormal: true);
                    }
                    else break;
                }
            }

            // Düşman: belirli aralıkla ateşler (oyuncu idle olsa da).
            // Şarj sırasında, sersemken ve board açık kilitliyken (fail/market popup'ı
            // açık — hamle limitiyle artık mümkün) normal saldırı yok.
            enemyTimer += dt;
            if (enemyTimer >= enemyAttackInterval && !enemyBusy && !chargeActive && stunRemaining <= 0f
                && !board.IsExplicitlyLocked && !IsOver())
            {
                enemyTimer = 0f;
                enemyBusy = true;
                StartCoroutine(EnemyAttackThenClear(() => enemyBusy = false));
            }

            yield return null;
        }

        // Düello bitti (win/lose): fail eval tutucusu asla asılı kalmasın.
        ReleaseEndEvalHold();
    }

    private IEnumerator EnemyAttackThenClear(System.Action onDone)
    {
        yield return EnemyAttack();
        onDone?.Invoke();
    }

    // Tek oyuncu vuruşu: kollar sırayla ateşler. Zayıf renkten gelen vuruş çarpanlı (crit).
    // forceNormal: bonus vuruşlar (special aktivasyonu) crit almadan düz hasar vurur.
    private void FireOnePlayerStrike(TileType tileType, bool forceNormal = false)
    {
        bool useA = (strikeIndex++ % 2 == 0);
        var arm = Pick(playerArmA, playerArmB, useA);
        var muzzle = Pick(playerMuzzleA, playerMuzzleB, useA);

        var wave = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
        bool isWeakHit = !forceNormal && wave.weaknessEnabled && tileType == currentWeakType;
        int dmg = isWeakHit
            ? Mathf.RoundToInt(damagePerTile * Mathf.Max(1f, wave.weaknessMultiplier))
            : damagePerTile;

        // Crit vuruş görsel olarak ayrışsın: parlak sıcak tonlu bolt.
        Color boltColor = isWeakHit ? new Color(1f, 0.9f, 0.35f, 1f) : playerBoltColor;

        PlayRecoil(arm != null ? arm : playerRobot, +1f);
        boltsInFlight++;
        FireBolt(GetMuzzleWorld(muzzle, playerRobot, +1f), enemyRobot, boltColor, playerBoltPrefab, playerMuzzleFlashPrefab,
            () =>
            {
                boltsInFlight = Mathf.Max(0, boltsInFlight - 1);
                ApplyEnemyDamage(dmg);
            });
    }

    private void ApplyEnemyDamage(int dmg)
    {
        if (dmg <= 0 || enemyHp <= 0 || waveTransitionActive) return;

        if (enemyShieldRemaining > 0f)
        {
            PlayShieldAbsorb(enemyShieldBubble, enemyShieldColor);
            return;
        }

        // Sersemlemiş boss 1.5× hasar alır (şarj saldırısını kesmenin ödülü).
        if (stunRemaining > 0f)
            dmg = Mathf.RoundToInt(dmg * 1.5f);

        // Overkill dalga sınırında kırpılır: dalga HP'leri toplamı goal amount'a eşit
        // olduğundan clamp'li bildirimle goal defteri hiç şaşmaz.
        int applied = Mathf.Min(dmg, enemyHp);
        enemyHp -= applied;
        enemyHpBar?.Set(enemyHp);
        PlayRobotHitFeedback(enemyRobot, +1f);   // düşman sağa itilir

        // Mevcut goal/WIN akışını ilerlet (goal 0 → success otomatik).
        topHud?.NotifyCollectibleCollected(CollectibleId.BossDamage, applied);

        if (enemyHp > 0)
            return;

        if (!IsLastWave)
        {
            // Sıradaki dalga: çöküş → yeni robot girişi → savaş devam.
            StartCoroutine(WaveTransitionRoutine());
            return;
        }

        if (!enemyDefeated)
        {
            enemyDefeated = true;
            bossModeActive = false;   // BattleLoop dursun (WIN goal tamamlanınca LevelEnd success açar)
            var finalWave = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
            StartCoroutine(PlayDefeat(enemyRobot, enemyBodyImage,
                finalWave.defeatedSprite != null ? finalWave.defeatedSprite : enemyDefeatedSprite,
                enemyArmA, enemyArmB));

            if (!winCelebrationPlayed)
            {
                winCelebrationPlayed = true;
                if (playerRobot != null)
                    StartCoroutine(WinCelebration(playerRobot, playerBodyImage, playerWinSprite, playerArmA, playerArmB));
                PlayWinFireworks(playerRobot);   // 🎆 kazanan (oyuncu) robotun konumunda
            }
        }
    }

    // ── Dalga makinesi ──

    private void CaptureEnemyHomeState()
    {
        enemyOriginalBodySprite = enemyBodyImage != null ? enemyBodyImage.sprite : null;
        enemyHomePos = enemyRobot != null ? enemyRobot.anchoredPosition : Vector2.zero;
        enemyHomeScale = enemyRobot != null ? enemyRobot.localScale : Vector3.one;
    }

    private void StartWave(int index)
    {
        waveIndex = index;
        var w = waves[index];

        enemyMaxHp = Mathf.Max(1, w.hp);
        enemyHp = enemyMaxHp;
        enemyBaseDamage = w.attackDamageBase;
        enemyDamageGrowth = w.attackDamageGrowth;
        enemyAttackInterval = Mathf.Max(0.3f, w.attackInterval);
        waveOilCount = w.oilCount;
        waveOilEveryMoves = w.oilEveryMoves;
        enemyAttackCount = 0;
        movesSinceOil = 0;

        ApplyWaveVisuals(w);

        enemyHpBar?.Init(enemyMaxHp);
        if (index == 0)
            enemyHpBar?.InitWavePips(waves.Length);
        enemyHpBar?.SetWaveIndex(index);

        // Counterplay reset: yeni dalga temiz başlar (birikmiş strike kuyruğu KORUNUR).
        stunRemaining = 0f;
        SetEnemyStunVisual(false);
        chargeActive = false;
        chargeCooldown = 0f;
        chargeTilesBroken = 0;
        SetChargeRingVisible(false);

        weaknessTimer = 0f;   // ilk tick'te yeni renk atanır
        if (weaknessIcon != null)
            weaknessIcon.transform.parent.gameObject.SetActive(w.weaknessEnabled);
    }

    // ── Faz 3: Special bonus hasarı ───────────────────────────────────────────

    private void HandleSpecialActivated(TileSpecial special, Vector2Int cell)
    {
        if (!bossModeActive || IsOver() || waveTransitionActive)
            return;

        switch (special)
        {
            case TileSpecial.LineH:
            case TileSpecial.LineV:
                bonusStrikePool += lineBonusStrikes;
                break;
            case TileSpecial.PulseCore:
                bonusStrikePool += pulseBonusStrikes;
                break;
            case TileSpecial.SystemOverride:
                bonusStrikePool += overrideBonusStrikes;
                break;
            case TileSpecial.PatchBot:
                // PatchBot boss'a direkt uçar: tek büyük mor mermi.
                int dmg = Mathf.RoundToInt(damagePerTile * Mathf.Max(1f, patchBotDamageMult));
                boltsInFlight++;
                FireBolt(GetMuzzleWorld(playerMuzzleA, playerRobot, +1f), enemyRobot,
                    new Color(0.75f, 0.45f, 1f, 1f), playerBoltPrefab, playerMuzzleFlashPrefab,
                    () =>
                    {
                        boltsInFlight = Mathf.Max(0, boltsInFlight - 1);
                        ApplyEnemyDamage(dmg);
                    });
                break;
        }
    }

    // ── Faz 3: Süper lazer ────────────────────────────────────────────────────

    // Oyuncu HP barının SOL dışında dairesel şarj göstergesi (zayıflık rozetiyle simetrik).
    // Dolunca parlar ve tıklanabilir olur; sahne kurulumu gerektirmez.
    private void EnsureSuperLaserUI()
    {
        if (laserBadgeRoot != null)
            return;

        RectTransform parent = playerHpBar != null ? (RectTransform)playerHpBar.transform : playerRobot;
        if (parent == null)
            return;

        var root = new GameObject("SuperLaserBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        laserBadgeRoot = (RectTransform)root.transform;
        laserBadgeRoot.SetParent(parent, false);
        root.transform.SetAsLastSibling();

        if (playerHpBar != null)
        {
            laserBadgeRoot.anchorMin = laserBadgeRoot.anchorMax = new Vector2(0f, 0.5f);   // barın sol-orta noktası
            laserBadgeRoot.pivot = new Vector2(1f, 0.5f);
            laserBadgeRoot.anchoredPosition = new Vector2(-2f, 0f);
        }
        else
        {
            laserBadgeRoot.anchorMin = laserBadgeRoot.anchorMax = new Vector2(0.5f, 1f);
            laserBadgeRoot.pivot = new Vector2(0.5f, 0f);
            laserBadgeRoot.anchoredPosition = new Vector2(0f, 46f);
        }
        laserBadgeRoot.sizeDelta = new Vector2(100f, 100f);

        // Zemin: koyu disk (Button'un raycast hedefi de bu).
        var bgImg = root.GetComponent<Image>();
        bgImg.sprite = GetGeneratedSolidCircleSprite();
        bgImg.color = new Color(0.06f, 0.06f, 0.11f, 0.94f);

        // Rozet arkası parıltı: şarj arttıkça belirir, hazırken nabız atar.
        var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(laserBadgeRoot, false);
        laserGlowImage = glowGo.GetComponent<Image>();
        laserGlowImage.sprite = GetGeneratedSoftGlowSprite();
        laserGlowImage.color = new Color(1f, 0.85f, 0.35f, 0f);
        laserGlowImage.raycastTarget = false;
        var glowRt = (RectTransform)glowGo.transform;
        glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.sizeDelta = new Vector2(175f, 175f);

        // Soluk tam halka: dolumun rayı boşken de okunsun.
        var trackGo = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackGo.transform.SetParent(laserBadgeRoot, false);
        var trackImg = trackGo.GetComponent<Image>();
        trackImg.sprite = GetGeneratedProgressRingSprite();
        trackImg.color = new Color(1f, 1f, 1f, 0.13f);
        trackImg.raycastTarget = false;
        var trackRt = (RectTransform)trackGo.transform;
        trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one;
        trackRt.offsetMin = new Vector2(4f, 4f);
        trackRt.offsetMax = new Vector2(-4f, -4f);

        // Radyal dolum: dolu pasta değil kalın halka — ilerleme çubuğu gibi okunur.
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(laserBadgeRoot, false);
        laserFillImage = fillGo.GetComponent<Image>();
        laserFillImage.sprite = GetGeneratedProgressRingSprite();
        laserFillImage.type = Image.Type.Filled;
        laserFillImage.fillMethod = Image.FillMethod.Radial360;
        laserFillImage.fillOrigin = (int)Image.Origin360.Top;
        laserFillImage.fillClockwise = true;
        laserFillImage.fillAmount = 0f;
        laserFillImage.color = new Color(1f, 0.55f, 0.15f, 0.95f);
        laserFillImage.raycastTarget = false;
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(4f, 4f);
        fillRt.offsetMax = new Vector2(-4f, -4f);

        // İçte biriken enerji topu: şarjla büyür ve parlar — "lazer doluyor" hissi.
        var energyGo = new GameObject("EnergyCore", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        energyGo.transform.SetParent(laserBadgeRoot, false);
        laserCoreImage = energyGo.GetComponent<Image>();
        laserCoreImage.sprite = GetGeneratedSoftGlowSprite();
        laserCoreImage.color = new Color(1f, 0.9f, 0.5f, 0.12f);
        laserCoreImage.raycastTarget = false;
        var energyRt = (RectTransform)energyGo.transform;
        energyRt.anchorMin = Vector2.zero; energyRt.anchorMax = Vector2.one;
        energyRt.offsetMin = new Vector2(18f, 18f);
        energyRt.offsetMax = new Vector2(-18f, -18f);

        // Merkez enerji ikonu (font bağımsız: küçük parlak halka; hazırken döner).
        var coreGo = new GameObject("CoreIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coreGo.transform.SetParent(laserBadgeRoot, false);
        var coreImg = coreGo.GetComponent<Image>();
        coreImg.sprite = GetGeneratedShieldSprite(isEnemy: true);
        coreImg.color = new Color(1f, 1f, 1f, 0.9f);
        coreImg.raycastTarget = false;
        laserIconRt = (RectTransform)coreGo.transform;
        laserIconRt.anchorMin = Vector2.zero; laserIconRt.anchorMax = Vector2.one;
        laserIconRt.offsetMin = new Vector2(28f, 28f);
        laserIconRt.offsetMax = new Vector2(-28f, -28f);

        laserButton = root.GetComponent<Button>();
        laserButton.transition = Selectable.Transition.None;
        laserButton.onClick.AddListener(HandleSuperLaserPressed);

        RefreshSuperLaserVisual();
    }

    private void RefreshSuperLaserVisual()
    {
        if (laserFillImage == null)
            return;

        float f = laserReady ? 1f : Mathf.Clamp01((float)laserCharge / Mathf.Max(1, superLaserChargeTiles));
        laserFillImage.fillAmount = f;
        // Turuncudan altına ısınır; hazırken beyaza çalan parlama.
        laserFillImage.color = laserReady
            ? Color.Lerp(new Color(1f, 0.95f, 0.35f, 1f), Color.white, (Mathf.Sin(Time.time * 7f) + 1f) * 0.15f)
            : Color.Lerp(new Color(1f, 0.55f, 0.15f, 0.95f), new Color(1f, 0.95f, 0.35f, 1f), f);

        // İç enerji topu şarjla büyür ve parlar.
        if (laserCoreImage != null)
        {
            laserCoreImage.color = new Color(1f, 0.9f, 0.5f, 0.12f + f * 0.75f);
            laserCoreImage.rectTransform.localScale = Vector3.one * (0.55f + f * 0.45f);
        }

        // Dış parıltı: şarj oranında belirir, hazırken nabız atar.
        if (laserGlowImage != null)
        {
            float glowA = laserReady
                ? 0.45f + Mathf.Sin(Time.time * 7f) * 0.2f
                : f * 0.22f;
            laserGlowImage.color = new Color(1f, 0.85f, 0.35f, Mathf.Clamp01(glowA));
        }

        // Hazırken merkez ikon yavaşça döner (dolu = aktif okunur).
        if (laserIconRt != null)
            laserIconRt.localRotation = laserReady
                ? Quaternion.Euler(0f, 0f, -Time.time * 60f)
                : Quaternion.identity;

        if (laserButton != null)
            laserButton.interactable = laserReady;

        // Hazırken nabız gibi atsın.
        if (laserBadgeRoot != null)
            laserBadgeRoot.localScale = laserReady
                ? Vector3.one * (1f + Mathf.Sin(Time.time * 7f) * 0.06f)
                : Vector3.one;
    }

    private void HandleSuperLaserPressed()
    {
        if (!laserReady || laserFiring || !bossModeActive || IsOver() || waveTransitionActive)
            return;

        laserReady = false;
        laserFiring = true;
        laserCharge = 0;
        RefreshSuperLaserVisual();
        StartCoroutine(FireSuperLaserRoutine());
    }

    private IEnumerator FireSuperLaserRoutine()
    {
        // Katmanlı beam: dış parıltı + gövde + beyaz-sıcak çekirdek; kalkanı DELER (söker).
        Vector3 fromWorld = GetMuzzleWorld(playerMuzzleA, playerRobot, +1f);
        Vector3 toWorld = enemyRobot != null ? enemyRobot.position : fromWorld;

        if (vfxRoot == null || enemyRobot == null)
        {
            ApplySuperLaserDamage();
            laserFiring = false;
            RefreshSuperLaserVisual();
            yield break;
        }

        Vector2 start = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 end = WorldToAnchoredIn(vfxRoot, toWorld);
        Vector2 dir = end - start;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Namluda şarj topu (anticipation): parlayarak büyür, sonra beam'e dönüşür.
        var chargeBall = CreateImage("SuperLaserChargeBall", vfxRoot, new Color(1f, 0.9f, 0.45f, 0f), new Vector2(20f, 20f));
        chargeBall.sprite = GetGeneratedSoftGlowSprite();
        chargeBall.rectTransform.anchoredPosition = start;

        float t = 0f;
        const float chargeDur = 0.30f;
        while (t < chargeDur && chargeBall != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / chargeDur);
            chargeBall.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20f, 150f, k * k);
            chargeBall.color = new Color(1f, 0.9f, 0.45f, k);
            yield return null;
        }

        PlaySfx(fireSfx, fireVolume);
        PlayRecoil(playerArmA != null ? playerArmA : playerRobot, +1f);
        PlayRecoil(playerArmB, +1f);

        Image MakeBeamLayer(string name, float height, Color color)
        {
            var img = CreateImage(name, vfxRoot, color, new Vector2(dir.magnitude, height));
            img.sprite = GetGeneratedBeamSprite();
            var r = img.rectTransform;
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = start;
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.localScale = new Vector3(1f, 0f, 1f);
            return img;
        }

        var layers = new[]
        {
            MakeBeamLayer("SuperLaserGlow", 170f, new Color(1f, 0.75f, 0.25f, 0.5f)),
            MakeBeamLayer("SuperLaserBody", 80f, new Color(1f, 0.9f, 0.4f, 0.95f)),
            MakeBeamLayer("SuperLaserCore", 30f, new Color(1f, 1f, 0.9f, 1f)),
        };

        // Namlu flare + boss ucunda çarpma parıltısı.
        var flare = CreateImage("SuperLaserFlare", vfxRoot, new Color(1f, 0.95f, 0.6f, 0.95f), new Vector2(190f, 190f));
        flare.sprite = GetGeneratedSoftGlowSprite();
        flare.rectTransform.anchoredPosition = start;

        var impact = CreateImage("SuperLaserImpact", vfxRoot, new Color(1f, 0.9f, 0.55f, 0.9f), new Vector2(60f, 60f));
        impact.sprite = GetGeneratedSoftGlowSprite();
        impact.rectTransform.anchoredPosition = end;

        if (chargeBall != null) Destroy(chargeBall.gameObject);

        // Açılış: beam eni hızla büyür, hafif taşıp oturur.
        t = 0f;
        const float grow = 0.10f, hold = 0.34f, fade = 0.30f;
        while (t < grow)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / grow);
            float s = k * (1f + (1f - k) * 0.6f);
            foreach (var l in layers) if (l != null) l.rectTransform.localScale = new Vector3(1f, s, 1f);
            yield return null;
        }

        // Beam boss'a değdi: kalkan sökme + hasar tam burada.
        ApplySuperLaserDamage();

        // Tutuş: canlı titreşim; çarpma parıltısı büyüyerek yayılır.
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float flicker = 1f + Mathf.Sin(Time.time * 55f) * 0.07f + Mathf.Sin(Time.time * 23f) * 0.05f;
            foreach (var l in layers) if (l != null) l.rectTransform.localScale = new Vector3(1f, flicker, 1f);
            if (impact != null)
                impact.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(60f, 230f, Mathf.Clamp01(t / hold)) * flicker;
            yield return null;
        }

        // Sönüş: incelerek kaybolur.
        t = 0f;
        var baseCols = new Color[layers.Length];
        for (int i = 0; i < layers.Length; i++) baseCols[i] = layers[i] != null ? layers[i].color : Color.white;
        Color flareC = flare != null ? flare.color : Color.white;
        Color impactC = impact != null ? impact.color : Color.white;
        while (t < fade)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fade);
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                var c = baseCols[i];
                layers[i].color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                layers[i].rectTransform.localScale = new Vector3(1f, 1f - k * 0.9f, 1f);
            }
            if (flare != null) flare.color = new Color(flareC.r, flareC.g, flareC.b, flareC.a * (1f - k));
            if (impact != null) impact.color = new Color(impactC.r, impactC.g, impactC.b, impactC.a * (1f - k));
            yield return null;
        }

        foreach (var l in layers) if (l != null) Destroy(l.gameObject);
        if (flare != null) Destroy(flare.gameObject);
        if (impact != null) Destroy(impact.gameObject);

        laserFiring = false;
        RefreshSuperLaserVisual();
    }

    private void ApplySuperLaserDamage()
    {
        // Kalkan deler: aktif düşman kalkanını söküp hasarı yine de geçirir.
        if (enemyShieldRemaining > 0f)
        {
            enemyShieldRemaining = 0f;
            PlayShieldAbsorb(enemyShieldBubble, enemyShieldColor);
            UpdateShieldVisual(enemyShieldBubble, 0f, enemyShieldColor, enemyRobot);
        }

        int dmg = Mathf.RoundToInt(damagePerTile * Mathf.Max(1f, superLaserDamageMult));
        PlaySfx(hitSfx, hitVolume);
        ApplyEnemyDamage(dmg);
    }

    // ── Faz 3: Kalkan pickup spawner ─────────────────────────────────────────

    private void TickPickupSpawner(float dt)
    {
        var w = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
        if (IsOver())
            return;

        // Board resolve ortasında taş dönüştürme (cascade veri yarışı) — idle bekle;
        // timer eşikte bekler, board boşalınca ilk tick'te spawn olur.
        bool boardIdle = board != null && !board.IsBusy;

        if (w.playerPickupEverySeconds > 0f)
        {
            playerPickupTimer += dt;
            if (playerPickupTimer >= w.playerPickupEverySeconds && boardIdle)
            {
                playerPickupTimer = 0f;
                TrySpawnPickupOnBoard(PlayerShieldPickupId);
            }
        }

        if (w.enemyPickupEverySeconds > 0f)
        {
            enemyPickupTimer += dt;
            if (enemyPickupTimer >= w.enemyPickupEverySeconds && boardIdle)
            {
                enemyPickupTimer = 0f;
                TrySpawnPickupOnBoard(EnemyShieldPickupId);
            }
        }
    }

    // Rastgele normal bir taşı pickup'a dönüştürür (movable obstacle; kırılınca kalkan verir).
    // Level'a elle pickup koymak gerekmez.
    private void TrySpawnPickupOnBoard(ObstacleId pickupId)
    {
        if (board == null || board.ObstacleStateService == null)
            return;

        var candidates = new List<Vector2Int>();
        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                if (board.ObstacleStateService.HasObstacleAt(x, y)) continue;
                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;
                candidates.Add(new Vector2Int(x, y));
            }

        if (candidates.Count == 0)
            return;

        var cell = candidates[Random.Range(0, candidates.Count)];
        if (!board.ObstacleStateService.TrySpawnSingleCellObstacleAt(cell.x, cell.y, pickupId))
            return;

        var view = board.GetTileViewAt(cell.x, cell.y);
        if (view != null)
        {
            board.RefreshTileObstacleVisual(view);   // taş görselini pickup'a çevirir
            board.SyncTileData(cell.x, cell.y);
            StartCoroutine(PickupSpawnPop(view));
        }
    }

    private IEnumerator PickupSpawnPop(TileView tile)
    {
        var rt = tile != null ? tile.RectTransform : null;
        if (rt == null) yield break;

        Vector3 baseScale = rt.localScale;
        float t = 0f;
        const float dur = 0.24f;
        while (t < dur && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.25f);
            yield return null;
        }
        if (rt != null) rt.localScale = baseScale;
    }

    // ── Toast sistemi ─────────────────────────────────────────────────────────

    // Lokalize metin; anahtar yoksa fallback (Get eksik anahtarda anahtarı döndürür).
    private static string Loc(string key, string fallback)
    {
        string s = GameLocalization.Get(key);
        return string.IsNullOrEmpty(s) || s == key ? fallback : s;
    }

    private static string LocFormat(string key, string fallback, params object[] args)
    {
        string format = Loc(key, fallback);
        try { return string.Format(format, args); }
        catch (System.FormatException) { return format; }
    }

    // Tek seferlik öğretici toast: ilk görüşte uzun/strong gösterilir, sonra kısa normal.
    private void ShowTeachableToast(string prefsKey, string tipKey, string tipFallback,
                                    string toastKey, string toastFallback, params object[] args)
    {
        if (PlayerPrefs.GetInt(prefsKey, 0) == 0)
        {
            PlayerPrefs.SetInt(prefsKey, 1);
            PlayerPrefs.Save();
            ShowToast(LocFormat(tipKey, tipFallback, args), 3.2f, strong: true);
        }
        else
        {
            ShowToast(LocFormat(toastKey, toastFallback, args));
        }
    }

    private void ShowToast(string text, float duration = -1f, bool strong = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        toastQueue.Enqueue((text, duration > 0f ? duration : toastDefaultDuration, strong));
        if (toastRunner == null)
            toastRunner = StartCoroutine(ToastRunner());
    }

    private IEnumerator ToastRunner()
    {
        while (toastQueue.Count > 0)
        {
            var (text, duration, strong) = toastQueue.Dequeue();
            yield return PlaySingleToast(text, duration, strong);
        }
        toastRunner = null;
    }

    // Toast'ın hedef konumu: otomatik modda robotların ALT kenarının toastAutoGap altı
    // (ekran/çözünürlük bağımsız), değilse sabit offset.
    private Vector2 ResolveToastPosition(RectTransform parent)
    {
        if (!toastAutoPosition || parent == null)
            return toastAnchoredPos;

        float lowestY = float.MaxValue;
        var corners = new Vector3[4];

        foreach (var robot in new[] { playerRobot, enemyRobot })
        {
            if (robot == null || !robot.gameObject.activeInHierarchy) continue;
            robot.GetWorldCorners(corners);
            Vector2 local = parent.InverseTransformPoint(corners[0]);   // sol-alt köşe
            if (local.y < lowestY) lowestY = local.y;
        }

        if (lowestY == float.MaxValue)
            return toastAnchoredPos;

        return new Vector2(0f, lowestY - toastAutoGap);
    }

    private IEnumerator PlaySingleToast(string text, float duration, bool strong)
    {
        var parent = vfxRoot != null ? vfxRoot : (RectTransform)transform;
        Vector2 toastPos = ResolveToastPosition(parent);

        var root = new GameObject("BossToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rootRt = (RectTransform)root.transform;
        rootRt.SetParent(parent, false);
        rootRt.SetAsLastSibling();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = toastPos;

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(rootRt, false);
        var txt = txtGo.GetComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = strong ? 40f : 32f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = strong ? new Color(1f, 0.85f, 0.3f) : Color.white;
        txt.raycastTarget = false;
        txt.textWrappingMode = TextWrappingModes.Normal;

        var txtRt = (RectTransform)txtGo.transform;
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(18f, 8f);
        txtRt.offsetMax = new Vector2(-18f, -8f);

        // Genişlik metne göre (ekranı aşmasın).
        float maxW = parent.rect.width * 0.86f;
        Vector2 pref = txt.GetPreferredValues(text, maxW - 36f, 0f);
        rootRt.sizeDelta = new Vector2(Mathf.Min(maxW, pref.x + 44f), pref.y + 22f);

        var group = root.AddComponent<CanvasGroup>();

        // In: fade + hafif yukarı kayış (+ strong'da scale punch).
        const float inDur = 0.18f, outDur = 0.22f;
        Vector2 from = toastPos + new Vector2(0f, -16f);
        float t = 0f;
        while (t < inDur && root != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / inDur);
            float e = 1f - (1f - k) * (1f - k);
            group.alpha = e;
            rootRt.anchoredPosition = Vector2.LerpUnclamped(from, toastPos, e);
            if (strong)
                rootRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.25f, 1f, e);
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        t = 0f;
        while (t < outDur && root != null)
        {
            t += Time.deltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / outDur);
            yield return null;
        }

        if (root != null)
            Destroy(root);
    }

    // ── Counterplay: Renk zayıflığı ──────────────────────────────────────────

    private void TickWeakness(float dt)
    {
        var w = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
        if (!w.weaknessEnabled || IsOver())
            return;

        weaknessTimer -= dt;
        if (weaknessTimer > 0f)
            return;

        weaknessTimer = Mathf.Max(3f, w.weaknessRotateSeconds);
        RollWeakType();
    }

    private void RollWeakType()
    {
        var pool = board != null ? board.RandomPool : null;
        if (pool == null || pool.Length == 0)
            return;

        // Aynı renk üst üste gelmesin (havuzda tek renk yoksa).
        TileType next = pool[Random.Range(0, pool.Length)];
        for (int guard = 0; guard < 8 && next == currentWeakType && pool.Length > 1; guard++)
            next = pool[Random.Range(0, pool.Length)];

        currentWeakType = next;
        EnsureWeaknessIcon();
        RefreshWeaknessIcon();

        // İlk karşılaşma öğreticisi (bir kez): rozet ne işe yarıyor?
        if (PlayerPrefs.GetInt("boss_tip_weakness_seen", 0) == 0)
        {
            PlayerPrefs.SetInt("boss_tip_weakness_seen", 1);
            PlayerPrefs.Save();
            ShowToast(Loc("boss_tip_weakness", "Zayıf renk! Rozetteki renkten taş kır: ×2 hasar."), 3.2f, strong: true);
        }
    }

    // Zayıf-renk rozeti: düşman HP barının SAĞ dışına oturur (robot kafasında diğer
    // objelerin altında kalıyordu). HP barı yoksa robot üstüne düşer. Sahne işi yok.
    private void EnsureWeaknessIcon()
    {
        if (weaknessIcon != null || enemyRobot == null)
            return;

        RectTransform badgeParent = enemyHpBar != null ? (RectTransform)enemyHpBar.transform : enemyRobot;

        var root = new GameObject("WeaknessBadge", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        rootRt.SetParent(badgeParent, false);
        root.transform.SetAsLastSibling();   // bar/robot görsellerinin üstünde çizilsin

        if (enemyHpBar != null)
        {
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(1f, 0.5f);   // barın sağ-orta noktası
            rootRt.pivot = new Vector2(0f, 0.5f);
            rootRt.anchoredPosition = new Vector2(6f, 0f);
        }
        else
        {
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 46f);
        }
        rootRt.sizeDelta = new Vector2(88f, 88f);

        // Arka rozet: DOLU koyu disk (okunurluk) + altın kenar halkası.
        var bg = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(rootRt, false);
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = GetGeneratedSolidCircleSprite();
        bgImg.color = new Color(0.07f, 0.07f, 0.12f, 0.92f);
        bgImg.raycastTarget = false;
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var rim = new GameObject("Rim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rim.transform.SetParent(rootRt, false);
        var rimImg = rim.GetComponent<Image>();
        rimImg.sprite = GetGeneratedShieldSprite(isEnemy: false);
        rimImg.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        rimImg.raycastTarget = false;
        var rimRt = (RectTransform)rim.transform;
        rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one;
        rimRt.offsetMin = rimRt.offsetMax = Vector2.zero;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(rootRt, false);
        weaknessIcon = iconGo.GetComponent<Image>();
        weaknessIcon.raycastTarget = false;
        weaknessIcon.preserveAspect = true;
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(14f, 14f);
        iconRt.offsetMax = new Vector2(-14f, -14f);

        // "×2" etiketi — rozetin sağ-alt köşesinde, koyu mini plaka üstünde.
        var multBg = new GameObject("MultPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        multBg.transform.SetParent(rootRt, false);
        var multBgImg = multBg.GetComponent<Image>();
        multBgImg.sprite = GetGeneratedSolidCircleSprite();
        multBgImg.color = new Color(0.05f, 0.05f, 0.09f, 0.95f);
        multBgImg.raycastTarget = false;
        var multBgRt = (RectTransform)multBg.transform;
        multBgRt.anchorMin = multBgRt.anchorMax = new Vector2(1f, 0f);
        multBgRt.pivot = new Vector2(0.6f, 0.4f);
        multBgRt.anchoredPosition = new Vector2(-14f, 6f);   // rozetin içine doğru, taşmasın
        multBgRt.sizeDelta = new Vector2(48f, 48f);

        var multGo = new GameObject("MultLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        multGo.transform.SetParent(multBgRt, false);
        weaknessMultLabel = multGo.GetComponent<TextMeshProUGUI>();
        weaknessMultLabel.fontSize = 30f;
        weaknessMultLabel.fontStyle = FontStyles.Bold;
        weaknessMultLabel.alignment = TextAlignmentOptions.Center;
        weaknessMultLabel.color = new Color(1f, 0.85f, 0.3f);
        weaknessMultLabel.raycastTarget = false;
        var multRt = (RectTransform)multGo.transform;
        multRt.anchorMin = Vector2.zero; multRt.anchorMax = Vector2.one;
        multRt.offsetMin = multRt.offsetMax = Vector2.zero;
    }

    // Dolu yumuşak kenarlı beyaz daire (rozet zeminleri için) — bir kez üretilir.
    private static Sprite generatedSolidCircleSprite;
    private static Sprite GetGeneratedSolidCircleSprite()
    {
        if (generatedSolidCircleSprite != null)
            return generatedSolidCircleSprite;

        const int size = 96;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = (new Vector2(x, y) - center).magnitude / (size * 0.5f);
            float alpha = Mathf.Clamp01((1f - r) / 0.06f);   // keskin ama yumuşatılmış kenar
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        generatedSolidCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedSolidCircleSprite;
    }

    // Kalın, yumuşak kenarlı halka — süper lazer dolumu pasta yerine ilerleme halkası olarak okunsun.
    private static Sprite generatedProgressRingSprite;
    private static Sprite GetGeneratedProgressRingSprite()
    {
        if (generatedProgressRingSprite != null)
            return generatedProgressRingSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = (new Vector2(x, y) - center).magnitude / (size * 0.5f);
            float a = Mathf.Pow(RingBand(r, 0.80f, 0.16f), 0.7f);   // dolgun bant, yumuşak kenar
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        generatedProgressRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedProgressRingSprite;
    }

    // Merkezden dışa yumuşak sönümlü parıltı — glow/flare/enerji topu için ortak doku.
    private static Sprite generatedSoftGlowSprite;
    private static Sprite GetGeneratedSoftGlowSprite()
    {
        if (generatedSoftGlowSprite != null)
            return generatedSoftGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = (new Vector2(x, y) - center).magnitude / (size * 0.5f);
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        generatedSoftGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedSoftGlowSprite;
    }

    // Beam kesiti: eksende opak, kenara doğru yumuşak sönüm; yatayda uniform (gerdirilerek kullanılır).
    private static Sprite generatedBeamSprite;
    private static Sprite GetGeneratedBeamSprite()
    {
        if (generatedBeamSprite != null)
            return generatedBeamSprite;

        const int w = 8, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs(y - (h - 1) * 0.5f) / ((h - 1) * 0.5f);
            float a = Mathf.Pow(Mathf.Clamp01(1f - v), 1.8f);
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        generatedBeamSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        return generatedBeamSprite;
    }

    private void RefreshWeaknessIcon()
    {
        if (weaknessIcon == null)
            return;

        Sprite icon = tileIconLibrary != null ? tileIconLibrary.Get(currentWeakType) : null;
        if (icon != null)
        {
            weaknessIcon.sprite = icon;
            weaknessIcon.color = Color.white;
        }
        else
        {
            // Library atanmadıysa: tip rengiyle rozet (okunur fallback).
            weaknessIcon.sprite = GetGeneratedShieldSprite(isEnemy: false);
            weaknessIcon.color = GetTileTypeFallbackColor(currentWeakType);
        }

        if (weaknessMultLabel != null)
        {
            var w = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
            weaknessMultLabel.text = $"×{Mathf.Max(1f, w.weaknessMultiplier):0.#}";
        }

        // Küçük pop — renk değişimi fark edilsin.
        StartCoroutine(WeaknessIconPop());
    }

    private IEnumerator WeaknessIconPop()
    {
        var rt = weaknessIcon != null ? weaknessIcon.rectTransform : null;
        if (rt == null) yield break;

        float t = 0f;
        const float dur = 0.22f;
        while (t < dur && rt != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float pop = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
            rt.localScale = Vector3.one * pop;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private static Color GetTileTypeFallbackColor(TileType type) => type switch
    {
        TileType.Gear  => new Color(1f, 0.85f, 0.25f),
        TileType.Core  => new Color(0.95f, 0.3f, 0.3f),
        TileType.Bolt  => new Color(0.35f, 0.6f, 1f),
        TileType.Plate => new Color(0.4f, 0.9f, 0.45f),
        _              => Color.white
    };

    // ── Counterplay: Şarj saldırısı + stun ───────────────────────────────────

    private void TickStun(float dt)
    {
        if (stunRemaining <= 0f)
            return;

        stunRemaining -= dt;
        if (stunRemaining <= 0f)
        {
            stunRemaining = 0f;
            SetEnemyStunVisual(false);
        }
    }

    private void TickChargeScheduling(float dt, bool enemyBusy)
    {
        var w = waves != null && waves.Length > 0 ? waves[waveIndex] : default;
        if (!w.chargeEnabled || chargeActive || enemyBusy || stunRemaining > 0f || IsOver())
            return;

        chargeCooldown += dt;
        if (chargeCooldown < Mathf.Max(4f, w.chargeIntervalSeconds))
            return;

        chargeCooldown = 0f;
        StartCoroutine(ChargeAttackRoutine(w));
    }

    private IEnumerator ChargeAttackRoutine(BossDifficulty.WaveParams w)
    {
        chargeActive = true;
        chargeTilesBroken = 0;

        EnsureChargeRing();
        SetChargeRingVisible(true);

        float chargeDur = Mathf.Max(2f, w.chargeSeconds);
        int needed = Mathf.Max(1, w.chargeInterruptTiles);

        // İlk görüşte uzun öğretici, sonra kısa uyarı.
        ShowTeachableToast("boss_tip_charge_seen",
            "boss_tip_charge", "Boss şarj oluyor! Halka dolmadan {0} taş kırarsan saldırıyı kesersin.",
            "boss_toast_charge", "Büyük saldırı geliyor — {0} taş kır!", needed);
        float elapsed = 0f;

        Vector3 baseScale = enemyRobot != null ? enemyRobot.localScale : Vector3.one;

        while (elapsed < chargeDur && chargeTilesBroken < needed && !IsOver() && !waveTransitionActive)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / chargeDur);

            if (chargeRing != null)
            {
                chargeRing.fillAmount = k;
                chargeRing.color = Color.Lerp(new Color(1f, 0.75f, 0.2f, 0.95f), new Color(1f, 0.2f, 0.15f, 1f), k);
            }

            if (chargeCounterText != null)
                chargeCounterText.text = Mathf.Max(0, needed - chargeTilesBroken).ToString();

            // Şarj olurken gövde gerilim pulse'ı.
            if (enemyRobot != null)
                enemyRobot.localScale = baseScale * (1f + Mathf.Sin(Time.time * 14f) * 0.02f * (0.5f + k));

            yield return null;
        }

        if (enemyRobot != null)
            enemyRobot.localScale = baseScale;

        SetChargeRingVisible(false);

        if (IsOver() || waveTransitionActive)
        {
            chargeActive = false;
            yield break;
        }

        if (chargeTilesBroken >= needed)
        {
            // KESİLDİ → sersemletme (saldırılar durur, alınan hasar 1.5×).
            stunRemaining = Mathf.Max(0.5f, w.chargeStunSeconds);
            SetEnemyStunVisual(true);
            PlayRobotHitFeedback(enemyRobot, +1f);
            ShowToast(Loc("boss_toast_interrupted", "KESTİN! Boss sersemledi — ×1.5 hasar!"), 1.8f, strong: true);
        }
        else
        {
            ShowToast(Loc("boss_toast_bigattack", "BÜYÜK SALDIRI!"), 1.1f, strong: true);
            // Kesilemedi → çarpanlı büyük atış (iki namlu birden).
            int dmg = Mathf.RoundToInt((enemyBaseDamage + enemyDamageGrowth * enemyAttackCount) * Mathf.Max(1f, w.chargeDamageMult));
            enemyAttackCount++;

            if (enemyArmA != null) PlayRecoil(enemyArmA, -1f);
            if (enemyArmB != null) PlayRecoil(enemyArmB, -1f);

            FireBolt(GetMuzzleWorld(enemyMuzzleA, enemyRobot, -1f), playerRobot,
                new Color(1f, 0.25f, 0.15f, 1f), enemyBoltPrefab, enemyMuzzleFlashPrefab, () =>
                {
                    if (!waveTransitionActive)
                        ApplyPlayerDamage(dmg);
                });

            if (enemyMuzzleB != null || enemyArmB != null)
                FireBolt(GetMuzzleWorld(enemyMuzzleB, enemyRobot, -1f), playerRobot,
                    new Color(1f, 0.25f, 0.15f, 1f), enemyBoltPrefab, enemyMuzzleFlashPrefab, null);
        }

        chargeActive = false;
    }

    // Şarj ringi: düşman üstünde radyal dolan halka + ortasında "kaç taş kaldı" sayacı.
    private void EnsureChargeRing()
    {
        if (chargeRing != null || enemyRobot == null)
            return;

        var root = new GameObject("ChargeRing", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        rootRt.SetParent(enemyRobot, false);
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        float size = Mathf.Max(shieldBubbleMinSize, Mathf.Max(enemyRobot.rect.width, enemyRobot.rect.height) * 1.15f);
        rootRt.sizeDelta = new Vector2(size, size);

        var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ringGo.transform.SetParent(rootRt, false);
        chargeRing = ringGo.GetComponent<Image>();
        chargeRing.sprite = GetGeneratedShieldSprite(isEnemy: true);
        chargeRing.type = Image.Type.Filled;
        chargeRing.fillMethod = Image.FillMethod.Radial360;
        chargeRing.fillOrigin = (int)Image.Origin360.Top;
        chargeRing.fillClockwise = true;
        chargeRing.raycastTarget = false;
        var ringRt = (RectTransform)ringGo.transform;
        ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
        ringRt.offsetMin = ringRt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Counter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(rootRt, false);
        chargeCounterText = txtGo.GetComponent<TextMeshProUGUI>();
        chargeCounterText.fontSize = 46f;
        chargeCounterText.fontStyle = FontStyles.Bold;
        chargeCounterText.alignment = TextAlignmentOptions.Center;
        chargeCounterText.color = new Color(1f, 0.95f, 0.85f, 1f);
        chargeCounterText.raycastTarget = false;
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

        // "KIR!" etiketi — sayının "kırılacak taş" olduğu bir bakışta anlaşılsın.
        var breakGo = new GameObject("BreakLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        breakGo.transform.SetParent(rootRt, false);
        chargeBreakLabel = breakGo.GetComponent<TextMeshProUGUI>();
        chargeBreakLabel.fontSize = 24f;
        chargeBreakLabel.fontStyle = FontStyles.Bold;
        chargeBreakLabel.alignment = TextAlignmentOptions.Center;
        chargeBreakLabel.color = new Color(1f, 0.6f, 0.25f, 1f);
        chargeBreakLabel.raycastTarget = false;
        chargeBreakLabel.text = Loc("boss_charge_break_label", "KIR!");
        var breakRt = (RectTransform)breakGo.transform;
        breakRt.anchorMin = breakRt.anchorMax = new Vector2(0.5f, 0.5f);
        breakRt.pivot = new Vector2(0.5f, 1f);
        breakRt.anchoredPosition = new Vector2(0f, -30f);
        breakRt.sizeDelta = new Vector2(140f, 28f);
    }

    private void SetChargeRingVisible(bool visible)
    {
        if (chargeRing != null)
            chargeRing.transform.parent.gameObject.SetActive(visible);
    }

    // Stun görseli: gövde soğuk gri tona düşer, çıkınca dalga tint'ine döner.
    private void SetEnemyStunVisual(bool active)
    {
        if (stunVisualActive == active)
            return;

        stunVisualActive = active;
        if (enemyBodyImage == null)
            return;

        var waveTint = waves != null && waves.Length > 0 ? waves[waveIndex].bodyTint : Color.white;
        enemyBodyImage.color = active
            ? Color.Lerp(waveTint, new Color(0.45f, 0.5f, 0.62f), 0.7f)
            : waveTint;
    }

    private void ApplyWaveVisuals(BossDifficulty.WaveParams w)
    {
        if (enemyBodyImage == null)
            return;

        // Dalga sprite'ı yoksa ORİJİNAL gövde geri gelir (önceki dalganın defeat sprite'ı kalmasın).
        enemyBodyImage.sprite = w.bodySprite != null ? w.bodySprite : enemyOriginalBodySprite;
        enemyBodyImage.color = w.bodyTint;
    }

    private IEnumerator WaveTransitionRoutine()
    {
        waveTransitionActive = true;

        // Ölen dalganın kalkanı onunla gider; oyuncununki kalır.
        enemyShieldRemaining = 0f;
        UpdateShieldVisual(enemyShieldBubble, 0f, enemyShieldColor, enemyRobot);

        var deadWave = waves[waveIndex];
        yield return PlayDefeat(enemyRobot, enemyBodyImage,
            deadWave.defeatedSprite != null ? deadWave.defeatedSprite : enemyDefeatedSprite,
            enemyArmA, enemyArmB);

        yield return new WaitForSeconds(0.25f);

        // Yeni dalga: robotu ekran dışına taşı, gövde/kolları tazele, parametreleri kur.
        RestoreEnemyRobotForNextWave();
        StartWave(waveIndex + 1);

        // Oyuncuya nefes: küçük iyileşme (dalgalar maratonunda adil kalsın).
        if (playerHp > 0 && playerHealPerWavePct > 0f)
        {
            playerHp = Mathf.Min(playerMaxHp, playerHp + Mathf.RoundToInt(playerMaxHp * playerHealPerWavePct));
            playerHpBar?.Set(playerHp, flashDamage: false);
        }

        StartCoroutine(ShowWaveBanner(waveIndex + 1));
        yield return EnemyEntranceSlide();

        waveTransitionActive = false;
    }

    private void RestoreEnemyRobotForNextWave()
    {
        if (enemyRobot == null)
            return;

        enemyRobot.localScale = enemyHomeScale;
        enemyRobot.anchoredPosition = enemyHomePos + new Vector2(enemyEntranceOffset, 0f);
        if (enemyArmA != null) enemyArmA.gameObject.SetActive(true);
        if (enemyArmB != null) enemyArmB.gameObject.SetActive(true);
    }

    private IEnumerator EnemyEntranceSlide()
    {
        if (enemyRobot == null)
            yield break;

        Vector2 from = enemyHomePos + new Vector2(enemyEntranceOffset, 0f);
        float dur = Mathf.Max(0.05f, waveEntranceDuration);
        float t = 0f;
        while (t < dur && enemyRobot != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);   // ease-out giriş
            enemyRobot.anchoredPosition = Vector2.LerpUnclamped(from, enemyHomePos, e);
            yield return null;
        }
        if (enemyRobot != null) enemyRobot.anchoredPosition = enemyHomePos;
    }

    // "WAVE N" banner'ı — sahne kurulumu gerektirmez, prosedürel TMP.
    private IEnumerator ShowWaveBanner(int waveNumber)
    {
        var parent = vfxRoot != null ? vfxRoot : (RectTransform)transform;
        var go = new GameObject("WaveBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = $"WAVE {waveNumber}";
        text.fontSize = 84f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(800f, 160f);

        var baseColor = new Color(1f, 0.85f, 0.25f);
        float inDur = 0.25f, hold = 0.8f, outDur = 0.3f;

        float t = 0f;
        while (t < inDur && text != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / inDur);
            float e = 1f - (1f - k) * (1f - k);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, e);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, e);
            yield return null;
        }

        yield return new WaitForSeconds(hold);

        t = 0f;
        while (t < outDur && text != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outDur);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - k);
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // ── Düşman saldırısı ──

    private IEnumerator EnemyAttack()
    {
        // Dalga geçişi başladıysa (ölen boss) saldırı iptal.
        if (waveTransitionActive || IsOver())
            yield break;

        // Telgraf: iki top da geri çekilip "yükleniyor" hissi.
        var aimA = enemyArmA != null ? enemyArmA : enemyRobot;
        var aimB = enemyArmB; // ikinci kol opsiyonel
        if (enemyTelegraphDuration > 0f)
        {
            if (aimB != null) StartCoroutine(ChargeTelegraph(aimB, enemyTelegraphDuration));
            if (aimA != null) yield return ChargeTelegraph(aimA, enemyTelegraphDuration);
        }

        // Telgraf sırasında dalga ölmüş olabilir — ateşleme.
        if (waveTransitionActive || IsOver())
            yield break;

        int dmg = enemyBaseDamage + enemyDamageGrowth * enemyAttackCount;
        enemyAttackCount++;

        if (aimA != null) PlayRecoil(aimA, -1f);
        if (aimB != null) PlayRecoil(aimB, -1f);

        bool landed = false;

        // A topu hasarı uygular; B topu yalnızca görsel (çift namlu hissi).
        FireBolt(GetMuzzleWorld(enemyMuzzleA, enemyRobot, -1f), playerRobot, enemyBoltColor, enemyBoltPrefab, enemyMuzzleFlashPrefab, () =>
        {
            landed = true;
            // Mermi havadayken dalga öldüyse hasar yazma (ölü boss vuramaz).
            if (!waveTransitionActive)
                ApplyPlayerDamage(dmg);
        });

        if (enemyMuzzleB != null || enemyArmB != null)
            FireBolt(GetMuzzleWorld(enemyMuzzleB, enemyRobot, -1f), playerRobot, enemyBoltColor, enemyBoltPrefab, enemyMuzzleFlashPrefab, null);

        float wait = 0f;
        while (!landed && wait < 1.5f) { wait += Time.deltaTime; yield return null; }

        // Opsiyonel oil baskısı: her N turda bir. Parametreler DALGA bazlı (StartWave kurar).
        if (waveOilCount > 0 && !waveTransitionActive && !IsOver())
        {
            movesSinceOil++;
            if (movesSinceOil >= Mathf.Max(1, waveOilEveryMoves))
            {
                movesSinceOil = 0;
                ThrowOil(waveOilCount);
            }
        }
    }

    private void ApplyPlayerDamage(int dmg)
    {
        if (dmg <= 0) return;

        if (playerShieldHits > 0)
        {
            // Kalkan bu vuruşu yutar (vuruş bazlı — 1 blok düşer).
            playerShieldHits--;
            PlayShieldAbsorb(playerShieldBubble, playerShieldColor);
            UpdateShieldVisual(playerShieldBubble, playerShieldHits, playerShieldColor, playerRobot);
            return;
        }

        playerHp = Mathf.Max(0, playerHp - dmg);
        playerHpBar?.Set(playerHp);
        PlayRobotHitFeedback(playerRobot, -1f);   // oyuncu sola itilir

        if (playerHp <= 0 && !playerDefeated)
        {
            playerDefeated = true;
            bossModeActive = false;   // BattleLoop dursun
            board.RequestLevelFail();  // LOSE

            StartCoroutine(PlayDefeat(playerRobot, playerBodyImage, playerDefeatedSprite, playerArmA, playerArmB));

            if (!winCelebrationPlayed)
            {
                winCelebrationPlayed = true;
                if (enemyRobot != null)
                    StartCoroutine(WinCelebration(enemyRobot, enemyBodyImage, enemyWinSprite, enemyArmA, enemyArmB));
            }
        }
    }

    // ── Timed shields ──

    private void AddShield(bool toPlayer)
    {
        if (toPlayer)
        {
            playerShieldHits += Mathf.Max(1, playerShieldHitsPerPickup);
            EnsureShieldBubble(ref playerShieldBubble, playerRobot, playerShieldColor, playerShieldSprite, isEnemy: false);
            UpdateShieldVisual(playerShieldBubble, playerShieldHits, playerShieldColor, playerRobot);
            PlayShieldAbsorb(playerShieldBubble, playerShieldColor);
            ShowToast(Loc("boss_toast_shield_player", "Kalkan aktif!"));
        }
        else
        {
            enemyShieldRemaining += Mathf.Max(0.5f, enemyShieldSecondsPerPickup);
            EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
            UpdateShieldVisual(enemyShieldBubble, enemyShieldRemaining, enemyShieldColor, enemyRobot);
            PlayShieldAbsorb(enemyShieldBubble, enemyShieldColor);
            ShowToast(Loc("boss_toast_shield_enemy", "Boss kalkanlandı!"));
        }
    }

    private void TickShields(float dt)
    {
        if (enemyShieldRemaining > 0f)
            enemyShieldRemaining = Mathf.Max(0f, enemyShieldRemaining - dt);

        UpdateShieldVisual(playerShieldBubble, playerShieldHits, playerShieldColor, playerRobot);
        UpdateShieldVisual(enemyShieldBubble, enemyShieldRemaining, enemyShieldColor, enemyRobot);
    }

    private void EnsureShieldBubble(ref Image bubble, RectTransform robot, Color color, Sprite sideSprite, bool isEnemy)
    {
        if (bubble != null || robot == null)
            return;

        var go = new GameObject("ShieldBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(robot, false);
        go.transform.SetAsLastSibling();

        bubble = go.GetComponent<Image>();
        bubble.sprite = sideSprite != null ? sideSprite
                      : (shieldBubbleSprite != null ? shieldBubbleSprite : GetGeneratedShieldSprite(isEnemy));
        bubble.color = color;
        bubble.raycastTarget = false;
        bubble.preserveAspect = true;

        var rt = bubble.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        go.SetActive(false);
    }

    private void UpdateShieldVisual(Image bubble, float remaining, Color color, RectTransform robot)
    {
        if (bubble == null)
            return;

        bool active = remaining > 0f && robot != null && robot.gameObject.activeInHierarchy;
        if (bubble.gameObject.activeSelf != active)
            bubble.gameObject.SetActive(active);

        if (!active)
            return;

        var rt = bubble.rectTransform;
        float robotW = robot != null ? robot.rect.width : 0f;
        float robotH = robot != null ? robot.rect.height : 0f;
        float size = Mathf.Max(shieldBubbleMinSize, Mathf.Max(robotW, robotH) * shieldBubbleScale);
        rt.sizeDelta = new Vector2(size, size);

        float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.035f;
        rt.localScale = Vector3.one * pulse;
        bubble.color = color;
    }

    private void PlayShieldAbsorb(Image bubble, Color color)
    {
        if (bubble == null || !bubble.gameObject.activeInHierarchy || shieldAbsorbPulseDuration <= 0f)
            return;

        StartCoroutine(ShieldAbsorbPulse(bubble, color));
    }

    private IEnumerator ShieldAbsorbPulse(Image bubble, Color color)
    {
        if (bubble == null)
            yield break;

        var rt = bubble.rectTransform;
        Vector3 baseScale = rt.localScale;
        float baseAlpha = color.a;
        float dur = Mathf.Max(0.01f, shieldAbsorbPulseDuration);
        float t = 0f;

        while (t < dur && bubble != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float wave = Mathf.Sin(k * Mathf.PI);
            rt.localScale = baseScale * (1f + wave * 0.18f);
            bubble.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(baseAlpha + wave * 0.25f));
            yield return null;
        }

        if (bubble != null)
        {
            rt.localScale = baseScale;
            bubble.color = color;
        }
    }

    // Prosedürel kalkan sprite'ı (sprite atanmazsa kullanılır). Beyaz/grayscale üretir;
    // rengi runtime'da Image.color tint'ler. Oyuncu = yumuşak enerji dome, düşman = keskin
    // konsantrik lazer halka — sprite vermeden iki taraf görsel olarak ayrışsın diye.
    private static Sprite GetGeneratedShieldSprite(bool isEnemy)
    {
        if (isEnemy && generatedEnemyShieldSprite != null)
            return generatedEnemyShieldSprite;
        if (!isEnemy && generatedPlayerShieldSprite != null)
            return generatedPlayerShieldSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                Vector2 d = p - center;
                float r = d.magnitude / (size * 0.5f);
                if (r > 1f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float alpha;
                if (isEnemy)
                {
                    // Lazer halka: birkaç keskin konsantrik bant + dış kenarda taranmış (scanline) dişler.
                    float band = RingBand(r, 0.92f, 0.05f) * 1.0f      // dış ana halka
                               + RingBand(r, 0.66f, 0.04f) * 0.7f      // orta halka
                               + RingBand(r, 0.40f, 0.035f) * 0.45f;   // iç halka
                    float angle = Mathf.Atan2(d.y, d.x);
                    float ticks = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 18f)), 6f); // dış halkada dişler
                    float outerTicks = RingBand(r, 0.92f, 0.06f) * ticks * 0.6f;
                    float coreGlow = Mathf.SmoothStep(0.5f, 0f, r) * 0.10f;          // hafif iç parıltı
                    alpha = Mathf.Clamp01(band + outerTicks + coreGlow);
                }
                else
                {
                    // Çift halkalı enerji kalkanı: iki keskin konsantrik bant + hafif iç dolgu.
                    // Düşmandan ayrışsın diye açısal diş yok, daha yumuşak/savunmacı durur.
                    float band = RingBand(r, 0.92f, 0.06f) * 1.0f       // dış halka
                               + RingBand(r, 0.60f, 0.05f) * 0.8f;      // iç halka
                    float fill = Mathf.SmoothStep(0.92f, 0f, r) * 0.10f; // çok hafif iç dolgu
                    alpha = Mathf.Clamp01(band + fill);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: size);

        if (isEnemy) generatedEnemyShieldSprite = sprite;
        else generatedPlayerShieldSprite = sprite;
        return sprite;
    }

    // Belirli yarıçapta (radius) yumuşak kenarlı ince halka bandı. width = bandın yarı-kalınlığı.
    private static float RingBand(float r, float radius, float width)
    {
        float d = Mathf.Abs(r - radius);
        return Mathf.Clamp01(1f - d / Mathf.Max(0.0001f, width));
    }

    // ── Bolt / muzzle / impact ──

    // Hedef robotun şu an aktif (süresi olan, görünür) kalkan balonunu döndürür; yoksa null.
    private Image GetActiveShieldBubbleFor(RectTransform robot)
    {
        if (robot == playerRobot && playerShieldHits > 0 && playerShieldBubble != null && playerShieldBubble.gameObject.activeInHierarchy)
            return playerShieldBubble;
        if (robot == enemyRobot && enemyShieldRemaining > 0f && enemyShieldBubble != null && enemyShieldBubble.gameObject.activeInHierarchy)
            return enemyShieldBubble;
        return null;
    }

    private void FireBolt(Vector3 fromWorld, RectTransform targetRobot, Color color, Image boltPrefab, Image muzzleFlashPrefab, System.Action onHit)
    {
        if (vfxRoot == null || targetRobot == null)
        {
            onHit?.Invoke();
            return;
        }

        Vector2 start = WorldToAnchoredIn(vfxRoot, fromWorld);

        // Hedefte aktif kalkan varsa mermi robota değil, kalkanın ön yüzeyine çarpsın
        // (atış yönünde kalkan yarıçapı kadar geride dur).
        Vector3 targetWorld = targetRobot.position;
        Image shield = GetActiveShieldBubbleFor(targetRobot);
        if (shield != null)
        {
            float radiusWorld = shield.rectTransform.rect.width * 0.5f * Mathf.Abs(shield.rectTransform.lossyScale.x);
            Vector3 toShooter = fromWorld - targetRobot.position;
            if (radiusWorld > 0.0001f && toShooter.sqrMagnitude > 0.0001f)
                targetWorld = targetRobot.position + toShooter.normalized * radiusWorld;
        }

        Vector2 end = WorldToAnchoredIn(vfxRoot, targetWorld);

        PlaySfx(fireSfx, fireVolume);
        SpawnMuzzleFlash(start, color, muzzleFlashPrefab);
        StartCoroutine(BoltRoutine(start, end, color, boltPrefab, onHit));
    }

    private IEnumerator BoltRoutine(Vector2 start, Vector2 end, Color color, Image boltPrefab, System.Action onHit)
    {
        Image bolt;
        if (boltPrefab != null)
        {
            bolt = Instantiate(boltPrefab, vfxRoot);
            bolt.color = color;
            bolt.rectTransform.sizeDelta = boltSize;
            bolt.raycastTarget = false;
        }
        else
        {
            bolt = CreateImage("BattlefieldBolt", vfxRoot, color, boltSize);
        }
        var rt = bolt.rectTransform;
        rt.anchoredPosition = start;

        Vector2 dir = end - start;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        float dur = Mathf.Max(0.02f, boltTravelDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            yield return null;
        }

        if (bolt != null) Destroy(bolt.gameObject);
        SpawnImpact(end, color);
        PlaySfx(hitSfx, hitVolume);
        onHit?.Invoke();
    }

    private void SpawnMuzzleFlash(Vector2 anchoredPos, Color color, Image muzzleFlashPrefab)
    {
        Image flash = muzzleFlashPrefab != null
            ? Instantiate(muzzleFlashPrefab, vfxRoot)
            : CreateImage("MuzzleFlash", vfxRoot, color, boltSize * 0.9f);
        flash.rectTransform.anchoredPosition = anchoredPos;
        if (muzzleFlashPrefab != null) flash.color = color;
        flash.raycastTarget = false;
        StartCoroutine(FadeAndDestroy(flash, 0.1f));
    }

    private void SpawnImpact(Vector2 anchoredPos, Color color)
    {
        Image impact = impactPrefab != null
            ? Instantiate(impactPrefab, vfxRoot)
            : CreateImage("BoltImpact", vfxRoot, color, boltSize * 1.4f);
        impact.rectTransform.anchoredPosition = anchoredPos;
        if (impactPrefab != null) impact.color = color;
        StartCoroutine(FadeAndDestroy(impact, 0.16f));
    }

    private Image CreateImage(string name, RectTransform parent, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private IEnumerator FadeAndDestroy(Image img, float life)
    {
        if (img == null) yield break;
        Color c0 = img.color;
        float t = 0f;
        while (t < life && img != null)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(c0.a, 0f, t / life);
            img.color = new Color(c0.r, c0.g, c0.b, a);
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // ── Tween feedback ──

    // Kolların GERÇEK home (dinlenme) pozisyonu — BİR KEZ yakalanır (drift önleme).
    // Sonraki ateşlerde canlı/kaymış pozisyon değil, hep bu home baz alınır.
    private readonly Dictionary<RectTransform, Vector2> _recoilHome = new();
    private readonly Dictionary<RectTransform, Coroutine> _recoilCo = new();

    private void PlayRecoil(RectTransform target, float facing)
    {
        if (target == null || recoilDuration <= 0f) return;

        // İlk recoil'de (henüz kaymamışken) gerçek home'u yakala.
        if (!_recoilHome.ContainsKey(target))
            _recoilHome[target] = target.anchoredPosition;

        // Önceki recoil hâlâ koşuyorsa durdur — üst üste binen darbelerde kavga/drift olmasın.
        if (_recoilCo.TryGetValue(target, out var running) && running != null)
            StopCoroutine(running);

        _recoilCo[target] = StartCoroutine(RecoilRoutine(target, -facing * recoilDistance));
    }

    private IEnumerator RecoilRoutine(RectTransform target, float dx)
    {
        Vector2 home = _recoilHome[target];             // sabit home (canlı pozisyon DEĞİL)
        Vector2 back = home + new Vector2(dx, 0f);
        Vector2 startPos = target.anchoredPosition;      // yarıda kesilmiş olabilir; buradan başla
        float half = recoilDuration * 0.5f;

        float t = 0f;
        while (t < half && target != null)
        {
            t += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(startPos, back, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half && target != null)
        {
            t += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(back, home, t / half);   // her zaman HOME'a dön
            yield return null;
        }
        if (target != null) target.anchoredPosition = home;   // kesin home'a otur
        _recoilCo[target] = null;
    }

    private IEnumerator ChargeTelegraph(RectTransform target, float duration)
    {
        Vector3 baseScale = target.localScale;
        Vector3 peak = baseScale * 1.15f;
        float t = 0f;
        while (t < duration && target != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            target.localScale = Vector3.LerpUnclamped(baseScale, peak, k);
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
    }

    // Vurulunca yönlü geri-sarsılma: knockDir = -1 (oyuncu sola) / +1 (düşman sağa).
    // Rapid-fire'da üst üste binmesin diye robot-başına taban yakalanır + önceki durdurulur.
    private void PlayRobotHitFeedback(RectTransform robot, float knockDir)
    {
        if (robot == null || robotHitKnockDuration <= 0f) return;

        if (!_hitCo.TryGetValue(robot, out var co) || co == null)
            _hitBase[robot] = robot.anchoredPosition;   // sadece dururken taban yakala
        else
            StopCoroutine(co);

        _hitCo[robot] = StartCoroutine(HitKnockRoutine(robot, knockDir));
    }

    private IEnumerator HitKnockRoutine(RectTransform robot, float knockDir)
    {
        Vector2 basePos = _hitBase[robot];
        Vector2 back = basePos + new Vector2(knockDir * robotHitKnockback, 0f);

        float outDur = robotHitKnockDuration * 0.32f;
        float t = 0f;
        while (t < outDur && robot != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outDur);
            float e = 1f - (1f - k) * (1f - k);
            robot.anchoredPosition = Vector2.LerpUnclamped(basePos, back, e);
            yield return null;
        }

        float backDur = robotHitKnockDuration * 0.68f;
        t = 0f;
        while (t < backDur && robot != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / backDur);
            float e = 1f - (1f - k) * (1f - k);
            robot.anchoredPosition = Vector2.LerpUnclamped(back, basePos, e);
            yield return null;
        }

        if (robot != null) robot.anchoredPosition = basePos;
        _hitCo[robot] = null;
    }

    // ── Win / Defeat ──

    private IEnumerator WinCelebration(RectTransform robot, Image bodyImage, Sprite winSprite,
                                       RectTransform arm1, RectTransform arm2)
    {
        if (robot == null) yield break;

        // Zafer sprite'ı varsa gövdeyi ona çevir + ayrı kolları gizle (poz kendi kollarını içerir).
        if (winSprite != null && bodyImage != null)
        {
            bodyImage.sprite = winSprite;
            if (arm1 != null) arm1.gameObject.SetActive(false);
            if (arm2 != null) arm2.gameObject.SetActive(false);
        }

        Vector2 basePos = robot.anchoredPosition;
        Vector3 baseScale = robot.localScale;

        for (int h = 0; h < Mathf.Max(1, winHopCount) && robot != null; h++)
        {
            float t = 0f;
            float dur = Mathf.Max(0.05f, winHopDuration);
            while (t < dur && robot != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float hop = Mathf.Sin(k * Mathf.PI);          // 0→1→0 yay
                robot.anchoredPosition = basePos + new Vector2(0f, hop * winHopHeight);
                float s = 1f + (winScalePunch - 1f) * hop;
                robot.localScale = baseScale * s;
                yield return null;
            }
        }

        if (robot != null) { robot.anchoredPosition = basePos; robot.localScale = baseScale; }
    }

    // Kazanınca havai fişek — KAZANAN robotun konumunda. Prefab varsa onu çal; yoksa renkli patlamalar.
    private void PlayWinFireworks(RectTransform origin)
    {
        var parent = vfxRoot != null ? vfxRoot : (RectTransform)transform;
        Vector2 center = (origin != null && vfxRoot != null)
            ? WorldToAnchoredIn(vfxRoot, origin.position)
            : Vector2.zero;

        if (winFireworksPrefab != null)
        {
            var go = Instantiate(winFireworksPrefab, parent);
            go.transform.SetAsLastSibling();
            if (go.transform is RectTransform grt)   // UI efektse kazananın üstüne konumla
                grt.anchoredPosition = center;
            Destroy(go, Mathf.Max(0.3f, winFireworksDuration) + 1f);
            return;
        }

        if (vfxRoot != null)
            StartCoroutine(WinFireworksRoutine(center));
    }

    private static readonly Color[] FireworkColors =
    {
        new Color(1f, 0.9f, 0.3f), new Color(1f, 0.4f, 0.5f), new Color(0.4f, 0.8f, 1f),
        new Color(0.6f, 1f, 0.5f), new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.5f, 1f),
    };

    private IEnumerator WinFireworksRoutine(Vector2 center)
    {
        float endTime = Time.time + Mathf.Max(0.3f, winFireworksDuration);
        float spread = Mathf.Max(vfxRoot.rect.width, vfxRoot.rect.height, 1f) * 0.16f; // robotun çevresinde küme

        while (Time.time < endTime)
        {
            // Kazanan robotun çevresine, yukarı doğru hafif yığılmış patlamalar.
            Vector2 off = new Vector2(
                Random.Range(-spread, spread),
                Random.Range(-spread * 0.3f, spread * 1.6f));
            StartCoroutine(WinBurst(center + off, FireworkColors[Random.Range(0, FireworkColors.Length)]));
            yield return new WaitForSeconds(Random.Range(0.12f, 0.28f));
        }
    }

    private IEnumerator WinBurst(Vector2 anchoredPos, Color color)
    {
        Image img = CreateImage("WinFirework", vfxRoot, color, new Vector2(140f, 140f));
        if (winFireworkBurstSprite != null)
        {
            img.sprite = winFireworkBurstSprite;   // beyaz sprite × color = renkli patlama
            img.preserveAspect = true;
        }
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchoredPosition = anchoredPos;

        float dur = 0.6f;
        float t = 0f;
        while (t < dur && img != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);              // ease-out büyüme
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1.7f, e);
            var c = img.color; c.a = 1f - k; img.color = c;  // sönerek kaybol
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // Yenilince: gövde sprite'ını 'yığın' ile değiştir + çökme (aşağı in + ezil), kolları gizle.
    private IEnumerator PlayDefeat(RectTransform robot, Image bodyImage, Sprite defeatedSprite,
                                   RectTransform arm1, RectTransform arm2)
    {
        if (robot == null) yield break;

        if (arm1 != null) arm1.gameObject.SetActive(false);
        if (arm2 != null) arm2.gameObject.SetActive(false);

        if (bodyImage != null && defeatedSprite != null)
            bodyImage.sprite = defeatedSprite;   // rect aynı kalır; yığın sprite'ını gövde footprint'ine göre çiz

        Vector2 basePos = robot.anchoredPosition;
        Vector3 baseScale = robot.localScale;
        Vector2 downPos = basePos + new Vector2(0f, -winHopHeight * 0.5f);
        Vector3 squashScale = new Vector3(baseScale.x * 1.08f, baseScale.y * 0.85f, baseScale.z);

        float dur = Mathf.Max(0.05f, defeatCollapseDuration);
        float t = 0f;
        while (t < dur && robot != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);
            robot.anchoredPosition = Vector2.LerpUnclamped(basePos, downPos, e);
            robot.localScale = Vector3.LerpUnclamped(baseScale, squashScale, e);
            yield return null;
        }
    }

    // ── Oil (opsiyonel baskı) ──

    private void ThrowOil(int oilCount)
    {
        var targets = PickOilTargets(oilCount);
        if (targets.Count == 0) return;

        // Yeni oil kendi hücresinde belirir (uzaktan köprü değil); kaynak = hedef.
        var pairs = new List<OilSpreadPair>(targets.Count);
        foreach (var t in targets)
            pairs.Add(new OilSpreadPair(t, t));

        board.StartImmediateActionSequence(new List<BoardAction> { new OilSpreadAction(board, pairs) });
    }

    private List<Vector2Int> PickOilTargets(int count)
    {
        var candidates = new List<Vector2Int>();
        var obstacleService = board.ObstacleStateService;

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                if (obstacleService != null && obstacleService.HasObstacleAt(x, y)) continue;
                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;
                candidates.Add(new Vector2Int(x, y));
            }

        var picked = new List<Vector2Int>(count);
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            picked.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return picked;
    }

    // ── Helpers ──

    // İki koldan birini seç; seçilen boşsa diğerine düş (tek kol atanmışsa yine çalışır).
    private static RectTransform Pick(RectTransform a, RectTransform b, bool useA)
    {
        if (useA) return a != null ? a : b;
        return b != null ? b : a;
    }

    // Atış/isabet sesi. AudioSource yoksa kendi oluşturur. Hafif perde sapmasıyla monotonluk kırılır.
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        if (!GameSettings.SoundEnabled) return;

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.dopplerLevel = 0f;
        }

        sfxSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private Vector3 GetMuzzleWorld(RectTransform muzzle, RectTransform robot, float facing)
    {
        if (muzzle != null) return muzzle.position;
        if (robot == null) return Vector3.zero;
        // Namlu yoksa robot merkezinden, bakış yönüne doğru hafif kaydır.
        return robot.position;
    }

    private static Vector2 WorldToAnchoredIn(RectTransform targetSpace, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }
}
