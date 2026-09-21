using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battlefield / Boss Düellosu (LevelKind.BossDuel).
/// Bir hamlede kırılan taşlar güç biriktirir, board durunca tek yakın dövüş darbesiyle uygulanır.
///
/// Sol = oyuncu (yeşil HP), sağ = düşman (mor HP).
/// - Animal mode: surviving enemies counterattack once after each resolved move.
/// - Düşman HP = Collectible/BossDamage goal (0 olunca mevcut WIN akışı tetiklenir).
/// - Oyuncu HP 0 olunca board.RequestLevelFail() ile LOSE.
/// - Hamle SINIRLI: level'ın moves değeri normal levellerdeki gibi tükenir; hamle
///   bitince (kuyruktaki vuruşlar boşaldıktan sonra) standart fail akışı çalışır.
///
/// Character profiles animate full-body sprites; no physics bodies or separated limbs are required.
/// </summary>
public sealed class BossDuelController : MonoBehaviour
{
    private const ObstacleId PlayerShieldPickupId = ObstacleId.PlayerShieldPickup;
    private const ObstacleId EnemyShieldPickupId = ObstacleId.EnemyShieldPickup;

    [Header("Core Refs")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;
    [Tooltip("Eski sahne intro referansı. BossDuel artık yeni VS görsellerini kullanır; yükleme sırasında oynadıysa sahnede tekrarlanmaz.")]
    [SerializeField] private BossDuelIntroController intro;
    private bool ownsIntro;
    [Tooltip("Arena arka plan Image'ı. LevelData.battlefieldBackground atanırsa sprite buna uygulanır; boşsa mevcut kalır.")]
    [SerializeField] private Image arenaBackground;

    [Header("Karakterler")]
    [SerializeField] private BossDuelCharacterProfile playerCharacter;
    [Tooltip("Porsuk pozları hazır olduğunda atanır. Boşsa mevcut düşman görseli kullanılır.")]
    [SerializeField] private BossDuelCharacterProfile enemyCharacter;
    [Tooltip("Rakip sırası: 0 = ilk rakip, 1 = ikincisi... Level'da rakibe profil atanmadıysa buradan " +
             "okunur, böylece çok rakipli karşılaşmada hep aynı hayvan çıkmaz.")]
    [SerializeField] private BossDuelCharacterProfile[] opponentRotation;
    private BossDuelCharacterView playerCharacterView, enemyCharacterView;
    private int accumulatedPower;
    private int attackingPower;
    private bool playerAttackActive;
    private bool enemyMeleeActive;
    private bool playerMoveOpen;
    private bool animalTurnActive;
    private int defeatAnimationsActive;
    private bool outOfMovesDazed;
    private float moveSettledTime;
    private TMP_Text powerLabel;
    private Image powerFillImage;
    private RectTransform powerMeterRoot;
    private RectTransform powerOrbTarget;   // orb'ların vardığı nokta: dolumun BAŞLADIĞI alt uç
    private int displayedPower;          // orb'lar vardıkça tırmanan GÖRSEL değer
    private Coroutine powerPopCo;
    private BossDuelPowerOrbs powerOrbs;
    private int playerProtection, enemyProtection;
    private TMP_Text playerProtectionLabel, enemyProtectionLabel;
    private int playerProtectionShown = -1, enemyProtectionShown = -1;   // son yazılan değer (gereksiz TMP rebuild'i engeller)

    private int protectionPerPickup = 2;

    [Header("Sahne Kökleri")]
    [Tooltip("Sol taraf (oyuncu) kökü — hasar sarsıntısı, kutlama ve hayalet çıkış bunun üstünde çalışır.")]
    [SerializeField] private RectTransform playerRobot;
    [Tooltip("Sağ taraf (rakip) kökü.")]
    [SerializeField] private RectTransform enemyRobot;
    [Tooltip("Karakter gövde Image'ları — poz animasyonu bunların üstünde çalışır.")]
    [SerializeField] private Image playerBodyImage;
    [SerializeField] private Image enemyBodyImage;
    [Tooltip("Efektlerin doğduğu kök (robotların ve board'ın üstünde bir RectTransform).")]
    [SerializeField] private RectTransform vfxRoot;
    [Tooltip("Darbe çakması prefab'ı. Boşsa basit bir parlama üretilir.")]
    [SerializeField] private Image impactPrefab;

    [Header("HP Bars")]
    [SerializeField] private HpBar playerHpBar;
    [SerializeField] private HpBar enemyHpBar;

    [Header("Güç Göstergesi (dikey, oyuncunun solunda)")]
    [Tooltip("Çubuk ölçüsü (genişlik x yükseklik, px). Yükseklik ÜST kenar sabit kalarak değişir — " +
             "kısaltınca bar alttan kısalır, bittiği (dolduğu) nokta yerinde kalır.")]
    [SerializeField] private Vector2 powerMeterSize = new Vector2(52f, 270f);
    [Tooltip("Oyuncu kökünün sol-orta noktasına göre ÜST-SAĞ köşenin konumu " +
             "(x negatif = daha sola, y = üst kenarın yüksekliği).")]
    [SerializeField] private Vector2 powerMeterOffset = new Vector2(-40f, 165f);
    [Tooltip("Dolumun zemin içine girintisi (x = yanlardan, y = uçlardan, px).")]
    [SerializeField] private Vector2 powerFillInset = new Vector2(7f, 6f);
    [Tooltip("Gösterge zemini (dikey bar). Boşsa yuvarlak köşeli bar üretilir.")]
    [SerializeField] private Sprite powerTrackSprite;
    [Tooltip("Gösterge dolumu — Filled/Vertical olarak kullanılır, aşağıdan yukarı dolar.")]
    [SerializeField] private Sprite powerFillSprite;
    [Tooltip("Zemin tint'i: sarı bar sprite'ını koyulaştırır (çarpma tint yalnız karartır).")]
    [SerializeField] private Color powerTrackColor = new Color(0.30f, 0.26f, 0.18f, 1f);
    [Tooltip("Dolum tint'i. Sprite zaten sarı — beyaz bırak.")]
    [SerializeField] private Color powerFillColor = Color.white;
    [SerializeField] private Color powerTextColor = new Color(1f, 0.93f, 0.55f, 1f);

    [Header("Enerji Orbları")]
    [Tooltip("Kırılan taşların enerjisi önce board ortasında toplanır, sonra göstergeye akar. " +
             "Süreler 0 bırakılırsa koddaki varsayılanlar kullanılır.")]
    [SerializeField] private BossDuelPowerOrbs.Tuning orbTuning = BossDuelPowerOrbs.Tuning.Default;
    [Tooltip("Buluşma noktasının board merkezine göre kayması (px).")]
    [SerializeField] private Vector2 orbRallyOffset = new Vector2(0f, 60f);

    [Header("Feel")]
    [Tooltip("Rakip profili yoksa kullanılan yedek yüklenme (telgraf) süresi.")]
    [SerializeField, Min(0f)] private float enemyTelegraphDuration = 0.4f;
    [Tooltip("Sersemleme pozunun ekranda kalma süresi (sn).")]
    [SerializeField, Min(0f)] private float defeatCollapseDuration = 0.35f;
    [Tooltip("İsabet sesi.")]
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.6f;
    [Tooltip("Her seste rastgele perde sapması (monotonluğu kırar).")]
    [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.08f;
    [Tooltip("Boşsa runtime'da otomatik AudioSource eklenir.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Vurulunca robotun geri sarsılma süresi.")]
    [SerializeField, Min(0f)] private float robotHitKnockDuration = 0.18f;
    [Tooltip("Vurulunca robotun geri itilme mesafesi (px).")]
    [SerializeField, Min(0f)] private float robotHitKnockback = 22f;

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

    [Tooltip("Yeni dalga robotunun sağdan giriş mesafesi (px).")]
    [SerializeField, Min(0f)] private float enemyEntranceOffset = 420f;
    [Tooltip("Yeni dalga robotunun giriş süresi (sn).")]
    [SerializeField, Min(0.05f)] private float waveEntranceDuration = 0.45f;
    [Tooltip("Yenilen rakip yukarı süzülüp küçülerek saydamlaşır (hayalet çıkış). 0 = kapalı, anında kaybolur.")]
    [SerializeField, Min(0f)] private float ghostExitDuration = 0.55f;
    [Tooltip("Hayalet çıkışta yukarı süzülme mesafesi (px).")]
    [SerializeField] private float ghostExitRise = 130f;
    [Tooltip("Hayalet çıkışın sonundaki ölçek (1 = küçülme yok).")]
    [SerializeField, Range(0.05f, 1f)] private float ghostExitScale = 0.35f;

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

    [Header("Board Yerleşimi")]
    [Tooltip("BossDuel'de board'un ALT kenarı bu rect'in ÜSTÜNE hizalanır (BottomArea'yı sürükle). " +
             "Boşsa board yerinden oynatılmaz. Üstte robotlar/HUD için maksimum alan açılır.")]
    [SerializeField] private RectTransform boardBottomAnchor;
    [Tooltip("Board alt kenarı ile anchor üstü arasındaki boşluk (px, board ölçeğinde).")]
    [SerializeField] private float boardBottomGap = 10f;

    // ── State ──
    private bool bossModeActive;
    private bool endEvalHoldActive;       // hamle bitti ama vuruşlar boşalmadı — fail eval beklesin
    private float endEvalHoldStartTime;   // gözcü: hold ne zaman başladı (unscaled)
    private bool endEvalHoldWarned;

    // ── Toast kuyruğu (olay bildirimleri — üst üste binmez, sırayla oynar) ──
    private readonly Queue<(string text, float duration, bool strong)> toastQueue = new();
    private Coroutine toastRunner;


    private int enemyHp, enemyMaxHp;
    private int playerHp, playerMaxHp;
    private int movesSinceOil;
    private int pressureVolleyIndex;
    private bool pressureOwnsInputLock;
    private RectTransform pressureEffectsRoot;

    private int damagePerTile;
    private int enemyBaseDamage;

    // ── Dalga durumu ──
    private BossDifficulty.WaveParams[] waves;
    private int waveIndex;
    private bool waveTransitionActive;   // geçiş boyunca iki taraf da ateş etmez; strikes birikir
    private int waveOilCount;
    private int waveOilEveryMoves;
    private Sprite enemyOriginalBodySprite;
    private Vector2 enemyHomePos;
    private Vector3 enemyHomeScale;
    private CanvasGroup enemyFadeGroup;   // hayalet çıkış solması (yıldızlar/kalkan dahil)
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

        bool introPlayedDuringLoading = CustomIntroLoadingManager.HasShownBossIntroFor(gameObject.scene);
        if (introPlayedDuringLoading)
        {
            intro?.HideImmediate();
        }
        else if (!ownsIntro)
        {
            intro?.HideImmediate();
            intro = BossDuelIntroArtwork.Create(transform);
            ownsIntro = intro != null;
        }

        // Dalga listesi: authored bossWaves varsa o, yoksa BossDifficulty formülü.
        // Dalga 1 parametreleri level'ın Battlefield alanlarından gelir (eski davranış birebir).
        waves = BossDifficulty.BuildWaves(level, totalEnemyHp);
        playerMaxHp = Mathf.Max(1, level.playerMaxHp);
        playerHp = playerMaxHp;
        damagePerTile = Mathf.Max(0, level.damagePerClearedTile);
        protectionPerPickup = Mathf.Max(1, level.shieldProtectionPerPickup);

        InitializeAnimalCharacters();

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
        if (introPlayedDuringLoading)
        {
            board.SetInputLocked(true);
            while (CustomIntroLoadingManager.IsBossIntroFor(gameObject.scene))
                yield return null;
            board.SetInputLocked(false);
        }
        else if (intro != null && intro.HasIntro)
        {
            board.SetInputLocked(true);
            yield return intro.Play();
            board.SetInputLocked(false);
        }

        board.OnTilesCleared += HandleTilesCleared;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;
        board.OnPlayerMoveConsumed += HandlePlayerMoveConsumed;
        board.OnMovesChanged += HandleAnimalMovesChanged;

        EnsureShieldBubble(ref playerShieldBubble, playerRobot, playerShieldColor, playerShieldSprite, isEnemy: false);
        EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
        HideShieldBubbles();

        StartCoroutine(BattleLoop());
    }

    private void OnDestroy()
    {
        if (ownsIntro && intro != null) Destroy(intro.gameObject);
    }

    private void OnDisable()
    {
        bossModeActive = false;
        StopAllCoroutines();
        ReleasePressureInputLock();
        intro?.HideImmediate();
        // Hayalet çıkış yarıda kesilirse rakip saydam kalmasın.
        if (enemyFadeGroup != null) enemyFadeGroup.alpha = 1f;
        playerAttackActive = false;
        enemyMeleeActive = false;
        animalTurnActive = false;
        waveTransitionActive = false;   // StopAllCoroutines dalga geçişini yarıda kesmiş olabilir
        defeatAnimationsActive = 0;
        ReleaseEndEvalHold();
        if (powerMeterRoot != null) powerMeterRoot.gameObject.SetActive(false);
        if (playerProtectionLabel != null) playerProtectionLabel.transform.parent.gameObject.SetActive(false);
        if (enemyProtectionLabel != null) enemyProtectionLabel.transform.parent.gameObject.SetActive(false);
        playerCharacterView?.Finish(!playerDefeated, immediate: true);
        enemyCharacterView?.Finish(playerDefeated, immediate: true);
        if (board == null) return;
        board.OnTilesCleared -= HandleTilesCleared;
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        board.OnPlayerMoveConsumed -= HandlePlayerMoveConsumed;
        board.OnMovesChanged -= HandleAnimalMovesChanged;
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

    // ── Full-body animal presentation and one hit per resolved move ──

    private void InitializeAnimalCharacters()
    {
        playerCharacterView = CreateCharacterView(playerBodyImage, playerCharacter);
        enemyCharacterView = CreateCharacterView(enemyBodyImage, enemyCharacter);

        BuildPowerMeter();

        // Kırılan taşların enerjisi göstergeye uçar; renk taşın kendi rengidir.
        powerOrbs = gameObject.AddComponent<BossDuelPowerOrbs>();
        powerOrbs.Initialize(vfxRoot, powerOrbTarget != null ? powerOrbTarget : playerRobot,
            ResolveOrbRallyWorld, HandlePowerOrbArrived,
            () => bossModeActive && !IsOver(),
            orbTuning);
    }

    /// Oyuncunun SOLUNDA dikey güç çubuğu: aşağıdan yukarı dolar. Dolum oranı = bu darbenin
    /// rakibin KALAN canının ne kadarını götüreceği; bar dolduğunda darbe öldürücüdür.
    /// Sprite'lar HP barının kendi art'ından türetilmiş sarı/dikey varyantlardır.
    private void BuildPowerMeter()
    {
        RectTransform parent = playerRobot != null ? playerRobot : (RectTransform)transform;

        var rootGo = new GameObject("MovePower", typeof(RectTransform));
        powerMeterRoot = (RectTransform)rootGo.transform;
        powerMeterRoot.SetParent(parent, false);
        MatchParentLayer(powerMeterRoot);
        // Oyuncu kökünün soluna yasla. Pivot ÜST kenarda: yükseklik değişince bar alttan
        // kısalır/uzar, dolumun bittiği üst nokta hep aynı yerde kalır.
        powerMeterRoot.anchorMin = powerMeterRoot.anchorMax = new Vector2(0f, 0.5f);
        powerMeterRoot.pivot = new Vector2(1f, 1f);
        powerMeterRoot.anchoredPosition = powerMeterOffset;
        powerMeterRoot.sizeDelta = powerMeterSize;

        CreateMeterLayer("Track", powerTrackSprite, powerTrackColor, Image.Type.Simple, Vector2.zero);

        powerFillImage = CreateMeterLayer("Fill", powerFillSprite, powerFillColor, Image.Type.Filled, powerFillInset);
        if (powerFillImage != null)
        {
            powerFillImage.fillMethod = Image.FillMethod.Vertical;
            powerFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            powerFillImage.fillAmount = 0f;
        }

        // Orb'lar barın ÜST köşesine (kökün pivotu) değil, dolumun başladığı ALT uca akar.
        var orbTargetGo = new GameObject("OrbTarget", typeof(RectTransform));
        powerOrbTarget = (RectTransform)orbTargetGo.transform;
        powerOrbTarget.SetParent(powerMeterRoot, false);
        MatchParentLayer(powerOrbTarget);
        powerOrbTarget.anchorMin = powerOrbTarget.anchorMax = new Vector2(0.5f, 0f);
        powerOrbTarget.pivot = new Vector2(0.5f, 0.5f);
        powerOrbTarget.sizeDelta = Vector2.zero;
        powerOrbTarget.anchoredPosition = new Vector2(0f, powerFillInset.y + powerMeterSize.x * 0.35f);

        // Sayı çubuğun ALTINDA durur; dikey barın içine yazmak okunmuyor.
        var txtGo = new GameObject("Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(powerMeterRoot, false);
        MatchParentLayer(txtGo.transform);
        powerLabel = txtGo.GetComponent<TextMeshProUGUI>();
        var existingText = playerHpBar != null ? playerHpBar.GetComponentInChildren<TMP_Text>() : null;
        if (existingText != null && existingText.font != null) powerLabel.font = existingText.font;
        powerLabel.fontSize = 24f;
        powerLabel.fontStyle = FontStyles.Bold;
        powerLabel.alignment = TextAlignmentOptions.Center;
        powerLabel.color = powerTextColor;
        powerLabel.raycastTarget = false;
        var trt = powerLabel.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -6f);
        trt.sizeDelta = new Vector2(120f, 32f);

        RefreshPowerLabel();
    }

    // Orb'ların toplandığı nokta: board'un görsel merkezi (shake kökü) + ayar kayması.
    private Vector3 ResolveOrbRallyWorld()
    {
        var center = board != null ? board.ShakeTarget : null;
        Vector3 world = center != null ? center.position
                      : (playerRobot != null ? playerRobot.position : transform.position);
        if (vfxRoot != null)
            world += vfxRoot.TransformVector(new Vector3(orbRallyOffset.x, orbRallyOffset.y, 0f));
        return world;
    }

    private Image CreateMeterLayer(string name, Sprite sprite, Color color, Image.Type type, Vector2 inset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(powerMeterRoot, false);
        MatchParentLayer(go.transform);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset.x, inset.y);
        rt.offsetMax = new Vector2(-inset.x, -inset.y);
        var img = go.GetComponent<Image>();
        img.sprite = sprite != null ? sprite : GetGeneratedBarSprite();
        img.color = color;
        img.type = sprite != null ? type : (type == Image.Type.Filled ? Image.Type.Filled : Image.Type.Sliced);
        img.raycastTarget = false;
        return img;
    }

    private BossDuelCharacterView CreateCharacterView(Image body, BossDuelCharacterProfile profile)
    {
        if (body == null || profile == null || !profile.IsUsable) return null;
        var view = body.gameObject.AddComponent<BossDuelCharacterView>();
        view.Initialize(body, profile, vfxRoot);
        return view;
    }

    private void HandlePlayerMoveConsumed()
    {
        if (!bossModeActive || IsOver()) return;
        playerMoveOpen = true;
        moveSettledTime = 0f;
        NoteEndEvalProgress();
        TickEndEvalHold();
    }

    private void HandleAnimalMovesChanged(int moves)
    {
        // The existing extra-moves offer may resume an out-of-moves duel, but never revive HP=0.
        if (!outOfMovesDazed || moves <= 0 || IsOver()) return;
        outOfMovesDazed = false;
        playerCharacterView?.ResetForWave();
    }

    // Vuruş anında gerçek değere kilitlenir; bekleme sırasında orb'larla tırmanan değeri gösterir.
    private void RefreshPowerLabel()
    {
        int target = playerAttackActive ? attackingPower : accumulatedPower;
        if (playerAttackActive || target < displayedPower) displayedPower = target;

        if (powerMeterRoot != null) powerMeterRoot.gameObject.SetActive(displayedPower > 0);
        if (powerLabel != null)
            powerLabel.text = LocFormat("boss_move_power", "GÜÇ {0}", displayedPower);
        if (powerFillImage != null)
            powerFillImage.fillAmount = enemyHp > 0 ? Mathf.Clamp01((float)displayedPower / enemyHp) : 1f;
    }

    // Bir enerji orbu göstergeye vardı: görsel değer bir taş kadar tırmanır, gösterge zıplar.
    private void HandlePowerOrbArrived()
    {
        if (displayedPower >= accumulatedPower) return;
        displayedPower = Mathf.Min(accumulatedPower, displayedPower + Mathf.Max(1, damagePerTile));
        RefreshPowerLabel();
        if (powerMeterRoot != null && powerPopCo == null)
            powerPopCo = StartCoroutine(PowerMeterPop());
    }

    private IEnumerator PowerMeterPop()
    {
        const float dur = 0.12f;
        for (float t = 0f; t < dur && powerMeterRoot != null; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            powerMeterRoot.localScale = Vector3.one * (1f + k * 0.10f);
            yield return null;
        }
        if (powerMeterRoot != null) powerMeterRoot.localScale = Vector3.one;
        powerPopCo = null;
    }

    private void TickAnimalTurn(float dt)
    {
        if (animalTurnActive || playerAttackActive || enemyMeleeActive || !playerMoveOpen || board.IsExplicitlyLocked) return;
        // Uçan enerji orbu varken enstantane alınmaz: darbe, son taşın enerjisi göstergeye
        // varmadan başlamasın (gücün kendisi zaten senkron sayılıyor, bu yalnız sunum kapısı).
        if (board.Flow.IsDuelMoveSettling || (powerOrbs != null && powerOrbs.InFlight > 0))
        {
            moveSettledTime = 0f;
            return;
        }

        // Let end-of-frame work enqueue its final cascade before taking the snapshot.
        moveSettledTime += dt;
        if (moveSettledTime < 0.08f) return;
        playerMoveOpen = false;
        StartCoroutine(ResolveAnimalTurn());
    }

    private IEnumerator ResolveAnimalTurn()
    {
        animalTurnActive = true;
        int attackedWave = waveIndex;
        TickEndEvalHold();
        try
        {
            if (accumulatedPower > 0)
                yield return AnimalPlayerStrike();

            // A killed opponent never retaliates, even if its replacement is already ready.
            if (!IsOver() && !waveTransitionActive && waveIndex == attackedWave && enemyHp > 0)
            {
                while (board.IsExplicitlyLocked && !IsOver()) yield return null;
                if (!IsOver()) yield return EnemyAttack();
            }

            // Giriş ve son sersemleme boyunca level-end değerlendirmesi beklesin.
            while (waveTransitionActive || defeatAnimationsActive > 0)
                yield return null;

            // Hamle KALDIYSA tur burada biter: oyuncu darbe oynarken oynamaya devam ettiyse
            // sıradaki darbe hemen başlayabilsin. Yalnız hamle bittiğinde sonucu, board
            // tamamen durana kadar bekletiriz.
            if (board.RemainingMoves <= 0)
            {
                while (!IsOver() && board.Flow.IsDuelMoveSettling)
                    yield return null;

                if (!IsOver() && !playerMoveOpen && accumulatedPower <= 0
                    && (topHud == null || !topHud.AreAllGoalsCompleted))
                {
                    outOfMovesDazed = true;
                    yield return PlayDefeat(playerRobot);
                }
            }
        }
        finally
        {
            animalTurnActive = false;
            TickEndEvalHold();
            if (playerDefeated && isActiveAndEnabled && board != null) board.RequestLevelFail();
        }
    }

    private IEnumerator AnimalPlayerStrike()
    {
        int damage = accumulatedPower;
        accumulatedPower = 0;
        attackingPower = damage;
        playerAttackActive = true;
        RefreshPowerLabel();
        try
        {
            if (playerCharacterView != null)
                yield return playerCharacterView.Attack(enemyRobot, () => LandAnimalPlayerStrike(damage),
                    () => IsOver() || waveTransitionActive, damage);
            else
            {
                yield return new WaitForSeconds(0.3f);
                if (!IsOver() && !waveTransitionActive) LandAnimalPlayerStrike(damage);
            }
        }
        finally
        {
            playerAttackActive = false;
            attackingPower = 0;
            playerCharacterView?.SetFocused(accumulatedPower > 0);
            RefreshPowerLabel();
            TickEndEvalHold();
        }
    }

    private void LandAnimalPlayerStrike(int damage)
    {
        if (IsOver() || waveTransitionActive) return;
        SpawnMeleeImpact(enemyRobot);
        ApplyEnemyDamage(damage);
    }

    private void SpawnMeleeImpact(RectTransform target)
    {
        if (target != null && vfxRoot != null)
            SpawnImpact(WorldToAnchoredIn(vfxRoot, target.position), new Color(1f, 0.85f, 0.45f, 1f));
        PlaySfx(hitSfx, hitVolume);
    }

    private IEnumerator AnimalEnemyStrike(int damage)
    {
        int attackWave = waveIndex;
        bool Cancelled() => IsOver() || waveTransitionActive || attackWave != waveIndex;
        void Impact()
        {
            if (Cancelled()) return;
            SpawnMeleeImpact(playerRobot);
            ApplyPlayerDamage(damage);
        }

        // The player must finish the return dash before the counterattack begins.
        while (playerAttackActive && !Cancelled()) yield return null;
        if (Cancelled() || enemyMeleeActive) yield break;
        enemyMeleeActive = true;
        TickEndEvalHold();
        try
        {
            if (enemyCharacterView != null)
                yield return enemyCharacterView.Attack(playerRobot, Impact, Cancelled, damage);
            else
            {
                if (enemyRobot != null) yield return ChargeTelegraph(enemyRobot, Mathf.Max(0.02f, enemyTelegraphDuration));
                else yield return new WaitForSeconds(Mathf.Max(0.02f, enemyTelegraphDuration));
                Impact();
            }
        }
        finally
        {
            enemyMeleeActive = false;
            TickEndEvalHold();
        }
    }

    private IEnumerator ApplyEnemyObstaclePressure()
    {
        if (waveOilCount <= 0 || waveTransitionActive || IsOver()) yield break;
        movesSinceOil++;
        if (movesSinceOil < Mathf.Max(1, waveOilEveryMoves)) yield break;
        movesSinceOil = 0;

        // A player can already be making their next move during the melee animation.
        // Finish that move before selecting cells; only the short projectile flight locks input.
        while (!IsOver() && (board.IsExplicitlyLocked || board.Flow.IsDuelMoveSettling))
            yield return null;
        if (IsOver() || waveTransitionActive) yield break;
        var pool = BossDuelObstaclePressure.GetPool(board.ActiveLevelData);
        var targets = BossDuelObstaclePressure.PickTargets(board, pool, waveOilCount);
        if (targets.Count == 0) yield break;
        var id = pool[pressureVolleyIndex % pool.Count];
        pressureVolleyIndex++;
        pressureOwnsInputLock = true;
        board.SetInputLocked(true);
        try
        {
            var go = new GameObject("BossPressureVolley", typeof(RectTransform));
            pressureEffectsRoot = (RectTransform)go.transform;
            pressureEffectsRoot.SetParent(vfxRoot != null ? vfxRoot : board.TilesRoot, false);
            MatchParentLayer(pressureEffectsRoot);
            yield return BossDuelObstaclePressure.Throw(board, enemyRobot, pressureEffectsRoot, targets, id);
        }
        finally
        {
            ReleasePressureInputLock();
        }
    }

    private void ReleasePressureInputLock()
    {
        if (pressureEffectsRoot != null) Destroy(pressureEffectsRoot.gameObject);
        pressureEffectsRoot = null;
        if (!pressureOwnsInputLock) return;
        pressureOwnsInputLock = false;
        if (board != null) board.SetInputLocked(false);
    }

    private void HandleTilesCleared(TileType type, int amount)
    {
        if (!bossModeActive || amount <= 0 || IsOver())
            return;

        // Thrown obstacles convert tiles without clear events; every actual player clear counts.
        // Clears during a melee animation accumulate for the next strike.
        // Boosters also open a turn. Zero damage must never leave a drain hold behind.
        HandlePlayerMoveConsumed();
        accumulatedPower = (int)System.Math.Min(int.MaxValue,
            (long)accumulatedPower + (long)amount * damagePerTile);
        RefreshPowerLabel();
        playerCharacterView?.SetFocused(accumulatedPower > 0);
        TickEndEvalHold(); // Register synchronously, before the final move can be evaluated.
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
    // SÜREKLİ POMPA: hold'u her frame yeniden türet. BattleLoop düello biter bitmez durur
    // (IsOver / bossModeActive=false), oysa level-end değerlendirmesi tam o anda başlar —
    // hold'un sahibi o andan sonra yalnız dağınık finally'lerdi. Tek bir coroutine takılır
    // ya da yarıda kesilirse hold kalıcı sızıyor, LevelEnd 30 sn bekleyip force-drain ediyordu.
    // Tick geçiş-bazlı (durum değişmediyse saf no-op) → maliyeti birkaç bool karşılaştırması.
    private void Update()
    {
        TickEndEvalHold();
    }

    private void NoteEndEvalProgress()
    {
        // This hold may stay open across several legitimate moves/strikes.
        // Diagnose time without progress, not the total duration of a busy encounter.
        endEvalHoldStartTime = Time.unscaledTime;
        endEvalHoldWarned = false;
    }

    private void TickEndEvalHold()
    {
        bool draining = accumulatedPower > 0 || playerAttackActive || playerMoveOpen;
        // A winning hit must finish its return dash before the result UI takes over.
        bool turnInProgress = animalTurnActive || playerAttackActive || enemyMeleeActive
                              || waveTransitionActive || defeatAnimationsActive > 0;
        bool shouldHold = board != null &&
                          (turnInProgress || (bossModeActive && !IsOver() && draining));

        if (shouldHold == endEvalHoldActive)
        {
            // Gözcü: hold uzun süre asılı kalırsa HANGİ bayrağın tuttuğunu söyle. Böylece
            // "30 sn force-drain" bir daha olursa sebebi aramak gerekmez, log'da yazar.
            if (shouldHold && Time.unscaledTime - endEvalHoldStartTime > 10f && !endEvalHoldWarned)
            {
                endEvalHoldWarned = true;
                Debug.LogWarning(
                    $"[BossDuel] End-eval hold 10sn+ ilerlemedi. animalTurn={animalTurnActive} " +
                    $"playerAttack={playerAttackActive} enemyMelee={enemyMeleeActive} " +
                    $"waveTransition={waveTransitionActive} defeatAnims={defeatAnimationsActive} " +
                    $"bossMode={bossModeActive} isOver={IsOver()} power={accumulatedPower} moveOpen={playerMoveOpen}");
            }
            return;
        }

        endEvalHoldActive = shouldHold;
        endEvalHoldStartTime = Time.unscaledTime;
        endEvalHoldWarned = false;
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

    // Animal mode resolves one move at a time; legacy mode drains its strike queue continuously.
    // Timed enemy attacks and counterplay are exclusive to legacy mode.
    // Tek kural: her hamle bir tur. Board durunca biriken güç tek darbeye çevrilir,
    // yaşayan rakip bir kez karşılık verir. Zamanlayıcıyla saldırı yok.
    private IEnumerator BattleLoop()
    {
        while (bossModeActive && !IsOver())
        {
            TickEndEvalHold();
            if (!waveTransitionActive)
                TickAnimalTurn(Time.deltaTime);
            yield return null;
        }

        // Düello bitti (win/lose): fail eval tutucusu asla asılı kalmasın.
        accumulatedPower = 0;
        attackingPower = 0;
        playerMoveOpen = false;
        RefreshPowerLabel();
        TickEndEvalHold();
    }

    private void ApplyEnemyDamage(int dmg)
    {
        if (dmg <= 0 || enemyHp <= 0 || waveTransitionActive) return;

        dmg = AbsorbAnimalDamage(dmg, toPlayer: false);
        if (dmg <= 0) return;

        // Overkill dalga sınırında kırpılır: dalga HP'leri toplamı goal amount'a eşit
        // olduğundan clamp'li bildirimle goal defteri hiç şaşmaz.
        int applied = Mathf.Min(dmg, enemyHp);
        enemyHp -= applied;
        NoteEndEvalProgress();
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
            StartCoroutine(PlayDefeat(enemyRobot));

            if (!winCelebrationPlayed)
            {
                winCelebrationPlayed = true;
                if (playerRobot != null)
                    StartCoroutine(WinCelebration(playerRobot));
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
        NoteEndEvalProgress();
        var w = waves[index];

        enemyMaxHp = Mathf.Max(1, w.hp);
        enemyHp = enemyMaxHp;
        enemyBaseDamage = w.attackDamageBase;
        waveOilCount = w.oilCount;
        waveOilEveryMoves = w.oilEveryMoves;
        movesSinceOil = 0;

        ApplyWaveVisuals(w);

        enemyHpBar?.Init(enemyMaxHp);
        if (index == 0)
            enemyHpBar?.InitWavePips(waves.Length);
        enemyHpBar?.SetWaveIndex(index);
        RefreshProtectionLabels();
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
        MatchParentLayer(rootRt);
        rootRt.SetAsLastSibling();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = toastPos;

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(rootRt, false);
        MatchParentLayer(txtGo.transform);
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


    /// Rakip görseli önceliği: level'da authored profil → sahnedeki sıra (1. porsuk, 2. sırtlan...)
    /// → enemyCharacter. Sıra listesi bitmişse son geçerli profile tutunur (hep aynı rakip çıkmasın diye
    /// değil, listeyi doldurmak yeterli olsun diye).
    private BossDuelCharacterProfile ResolveOpponentProfile(BossDifficulty.WaveParams w)
    {
        if (w.characterProfile != null && w.characterProfile.IsUsable)
            return w.characterProfile;

        if (opponentRotation != null)
            for (int i = Mathf.Min(waveIndex, opponentRotation.Length - 1); i >= 0; i--)
                if (opponentRotation[i] != null && opponentRotation[i].IsUsable)
                    return opponentRotation[i];

        return enemyCharacter;
    }

    private void ApplyWaveVisuals(BossDifficulty.WaveParams w)
    {
        if (enemyBodyImage == null)
            return;

        var profile = ResolveOpponentProfile(w);
        if (enemyCharacterView == null)
            enemyCharacterView = CreateCharacterView(enemyBodyImage, profile);
        else
            enemyCharacterView.SetProfile(profile);

        enemyBodyImage.color = w.bodyTint;
        // Profile-based animals never fall back to the old robot shield ring.
        // Without shield artwork, protection is still shown by the HP-bar badge.
        EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
    }

    private IEnumerator WaveTransitionRoutine()
    {
        waveTransitionActive = true;
        TickEndEvalHold();

        // try/finally ŞART: bu rutin yarıda kesilirse (düello biter, coroutine durdurulur,
        // alt adım takılır) waveTransitionActive true kalır → end-eval hold'u kalıcı sızar
        // ve level-end 30 sn force-drain'e düşer. Bayrak her çıkışta sıfırlanmalı.
        try
        {
            // Ölen rakibin koruması onunla gider; oyuncununki kalır.
            enemyProtection = 0;
            HideShieldBubbles();

            yield return PlayDefeat(enemyRobot);

            yield return new WaitForSeconds(0.25f);
            // Yenilen rakip yukarı süzülüp hayalet gibi silinir; yenisi ancak o gittikten sonra kayar.
            yield return PlayGhostExit();
            while (playerAttackActive) yield return null;

            // Yeni dalga: robotu ekran dışına taşı, gövde/kolları tazele, parametreleri kur.
            RestoreEnemyRobotForNextWave();
            StartWave(waveIndex + 1);
            // Otomatik iyileşme YOK: oyuncu canı ve kalan koruması sonraki rakibe olduğu gibi taşınır.
            StartCoroutine(ShowWaveBanner(waveIndex + 1));
            yield return EnemyEntranceSlide();
        }
        finally
        {
            waveTransitionActive = false;
            TickEndEvalHold();
        }
    }

    /// Yenilen rakip yukarı süzülür, küçülür ve saydamlaşır — "hayalet olup gitme" çıkışı.
    /// Ölçek/konum KÖK robota uygulanır: poz görünümünü her frame yazan CharacterView'la
    /// çakışmaz (o yalnız gövde child'ını sürer). Solma CanvasGroup'tan gelir, böylece
    /// sersemleme yıldızları ve kalkan balonu da birlikte silinir.
    private IEnumerator PlayGhostExit()
    {
        if (enemyRobot == null || ghostExitDuration <= 0f)
            yield break;

        var fade = EnsureEnemyFadeGroup();
        Vector2 fromPos = enemyRobot.anchoredPosition;
        Vector3 fromScale = enemyRobot.localScale;
        float dur = ghostExitDuration;

        for (float t = 0f; t < dur && enemyRobot != null; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k);   // ease-out: çabuk kalkar, tepede yavaşlar
            enemyRobot.anchoredPosition = fromPos + new Vector2(0f, ghostExitRise * e);
            enemyRobot.localScale = fromScale * Mathf.LerpUnclamped(1f, ghostExitScale, e);
            if (fade != null) fade.alpha = 1f - k;
            yield return null;
        }

        if (fade != null) fade.alpha = 0f;
        if (enemyRobot != null) enemyRobot.anchoredPosition = fromPos;
    }

    private CanvasGroup EnsureEnemyFadeGroup()
    {
        if (enemyFadeGroup != null || enemyRobot == null)
            return enemyFadeGroup;
        // GetComponent sahte-null döndürebilir: ?? yerine TryGetComponent.
        if (!enemyRobot.TryGetComponent(out enemyFadeGroup))
            enemyFadeGroup = enemyRobot.gameObject.AddComponent<CanvasGroup>();
        return enemyFadeGroup;
    }

    private void RestoreEnemyRobotForNextWave()
    {
        if (enemyRobot == null)
            return;

        if (enemyFadeGroup != null) enemyFadeGroup.alpha = 1f;   // hayalet çıkışından sonra yeni rakip tam görünür
        enemyRobot.localScale = enemyHomeScale;
        enemyRobot.anchoredPosition = enemyHomePos + new Vector2(enemyEntranceOffset, 0f);
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
        MatchParentLayer(go.transform);
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

    // Yaşayan rakibin hamle sonu karşılığı: sabit hasar, ardından opsiyonel oil baskısı.
    private IEnumerator EnemyAttack()
    {
        // Dalga geçişi başladıysa (ölen rakip) saldırı iptal.
        if (waveTransitionActive || IsOver())
            yield break;

        yield return AnimalEnemyStrike(Mathf.Max(0, enemyBaseDamage));
        yield return ApplyEnemyObstaclePressure();
    }

    private void ApplyPlayerDamage(int dmg)
    {
        if (dmg <= 0 || IsOver()) return;

        dmg = AbsorbAnimalDamage(dmg, toPlayer: true);
        if (dmg <= 0) return;

        playerHp = Mathf.Max(0, playerHp - dmg);
        NoteEndEvalProgress();
        playerHpBar?.Set(playerHp);
        PlayRobotHitFeedback(playerRobot, -1f);   // oyuncu sola itilir

        if (playerHp <= 0 && !playerDefeated)
        {
            playerDefeated = true;
            bossModeActive = false;   // BattleLoop dursun
            // The turn requests failure after the enemy returns and the daze is visible.
            StartCoroutine(PlayDefeat(playerRobot));

            if (!winCelebrationPlayed)
            {
                winCelebrationPlayed = true;
                if (enemyRobot != null)
                    StartCoroutine(WinCelebration(enemyRobot));
            }
        }
    }

    // ── Protection points (animal) / timed and hit-count shields (legacy) ──

    private int AbsorbAnimalDamage(int damage, bool toPlayer)
    {
        int protection = toPlayer ? playerProtection : enemyProtection;
        int absorbed = Mathf.Min(damage, protection);
        if (toPlayer) playerProtection -= absorbed;
        else enemyProtection -= absorbed;
        // Update persistent guard first, then play the final block even when it used the last point.
        RefreshProtectionLabels();
        if (absorbed > 0)
        {
            NoteEndEvalProgress();
            PlayShieldAbsorb(toPlayer ? playerShieldBubble : enemyShieldBubble,
                toPlayer ? playerShieldColor : enemyShieldColor, toPlayer ? playerRobot : enemyRobot);
            ShowToast(LocFormat("boss_protection_absorbed", "Kalkan: −{0}   Can: −{1}", absorbed,
                Mathf.Min(damage - absorbed, toPlayer ? playerHp : enemyHp)), 0.8f);
        }
        return damage - absorbed;
    }

    private void RefreshProtectionLabels()
    {
        RefreshShieldVisual(playerRobot, playerShieldBubble, playerProtection);
        RefreshShieldVisual(enemyRobot, enemyShieldBubble, enemyProtection);
        RefreshProtectionLabel(ref playerProtectionLabel, ref playerProtectionShown,
            playerHpBar, playerProtection, playerShieldColor);
        RefreshProtectionLabel(ref enemyProtectionLabel, ref enemyProtectionShown,
            enemyHpBar, enemyProtection, enemyShieldColor);
    }

    private void RefreshShieldVisual(RectTransform robot, Image bubble, int protection)
    {
        var character = GetShieldCharacter(robot);
        character?.SetShieldActive(protection > 0);
        if (bubble == null) return;
        bool visible = protection > 0 && character == null;
        if (visible)
        {
            float size = Mathf.Max(shieldBubbleMinSize,
                (robot != null ? Mathf.Max(robot.rect.width, robot.rect.height) : 0f) * shieldBubbleScale);
            bubble.rectTransform.sizeDelta = Vector2.one * size;
        }
        bubble.gameObject.SetActive(visible);
    }

    private void RefreshProtectionLabel(ref TMP_Text label, ref int shown, HpBar hpBar, int protection, Color tint)
    {
        if (hpBar == null) return;
        if (label == null)
        {
            var root = new GameObject("Protection", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(hpBar.transform, false);
            MatchParentLayer(rt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -46f);
            rt.sizeDelta = new Vector2(110f, 32f);

            var iconObject = new GameObject("Shield", typeof(RectTransform), typeof(CanvasRenderer), typeof(BossDuelStatusGraphic));
            iconObject.transform.SetParent(rt, false);
            MatchParentLayer(iconObject.transform);
            var icon = iconObject.GetComponent<BossDuelStatusGraphic>();
            icon.color = new Color(tint.r, tint.g, tint.b, 1f);
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(25f, 30f);
            icon.rectTransform.anchoredPosition = new Vector2(-35f, 0f);

            var textObject = new GameObject("Points", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(rt, false);
            MatchParentLayer(textObject.transform);
            label = textObject.GetComponent<TextMeshProUGUI>();
            var reference = hpBar.GetComponentInChildren<TMP_Text>();
            if (reference != null && reference.font != null) label.font = reference.font;
            label.fontSize = 24f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Left;
            label.color = icon.color;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(70f, 32f);
            label.rectTransform.anchoredPosition = new Vector2(15f, 0f);
        }
        label.transform.parent.gameObject.SetActive(protection > 0);
        if (shown == protection)
            return;
        shown = protection;
        label.SetText("{0}", protection);
    }

    private void AddShield(bool toPlayer)
    {
        int amount = Mathf.Max(1, protectionPerPickup);
        if (toPlayer) playerProtection = (int)System.Math.Min(int.MaxValue, (long)playerProtection + amount);
        else enemyProtection = (int)System.Math.Min(int.MaxValue, (long)enemyProtection + amount);
        RefreshProtectionLabels();
        ShowToast(toPlayer
            ? LocFormat("boss_toast_shield_player", "Koruma +{0}", amount)
            : LocFormat("boss_toast_shield_enemy", "Rakip koruma +{0}", amount));
    }

    private void EnsureShieldBubble(ref Image bubble, RectTransform robot, Color color, Sprite sideSprite, bool isEnemy)
    {
        if (GetShieldCharacter(robot) != null)
        {
            if (bubble != null) bubble.gameObject.SetActive(false);
            return;
        }
        if (bubble != null || robot == null)
            return;

        var go = new GameObject("ShieldBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(robot, false);
        MatchParentLayer(go.transform);
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

    // Reconcile after initialization/wave changes; player protection carries to the next wave.
    private void HideShieldBubbles()
    {
        if (playerShieldBubble != null) playerShieldBubble.gameObject.SetActive(false);
        if (enemyShieldBubble != null) enemyShieldBubble.gameObject.SetActive(false);
        GetShieldCharacter(playerRobot)?.SetShieldActive(false);
        GetShieldCharacter(enemyRobot)?.SetShieldActive(false);
        RefreshProtectionLabels();
    }

    private BossDuelCharacterView GetShieldCharacter(RectTransform robot)
    {
        if (robot == null) return null;
        var character = robot == playerRobot ? playerCharacterView : robot == enemyRobot ? enemyCharacterView : null;
        return character;
    }

    private void PlayShieldAbsorb(Image bubble, Color color, RectTransform robot)
    {
        var character = GetShieldCharacter(robot);
        if (character != null)
        {
            character.PlayShieldBlock(robot == playerRobot ? -1f : 1f);
            return;
        }
        // Only legacy actors without a character view use the bubble fallback.
        if (bubble != null && shieldAbsorbPulseDuration > 0f)
            StartCoroutine(ShieldFlash(bubble, color, robot));
    }

    private IEnumerator ShieldFlash(Image bubble, Color color, RectTransform robot)
    {
        if (bubble == null)
            yield break;

        float size = Mathf.Max(shieldBubbleMinSize,
            (robot != null ? Mathf.Max(robot.rect.width, robot.rect.height) : 0f) * shieldBubbleScale);
        bubble.rectTransform.sizeDelta = new Vector2(size, size);
        bubble.color = color;
        bubble.gameObject.SetActive(true);
        yield return ShieldAbsorbPulse(bubble, color);
        RefreshShieldVisual(robot, bubble, robot == playerRobot ? playerProtection : enemyProtection);
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
    // Yuvarlak köşeli BEYAZ bar (9-slice). Beyaz olduğu için Image.color ile istenen
    // renge boyanır — renkli bir sprite'ta çarpma tint istenen rengi veremez.
    private static Sprite generatedBarSprite;

    private static Sprite GetGeneratedBarSprite()
    {
        if (generatedBarSprite != null) return generatedBarSprite;

        const int w = 32, h = 32, radius = 15;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Köşelere olan uzaklık: yalnız köşe bölgelerinde yuvarlatılır.
            float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            float a = Mathf.Clamp01(radius - d + 0.5f);   // 1px yumuşak kenar
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        generatedBarSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return generatedBarSprite;
    }

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

    // ── Impact / hit feedback ──

    private void SpawnImpact(Vector2 anchoredPos, Color color)
    {
        Image impact = impactPrefab != null
            ? Instantiate(impactPrefab, vfxRoot)
            : CreateImage("HitFlash", vfxRoot, color, new Vector2(90f, 26f));
        impact.rectTransform.anchoredPosition = anchoredPos;
        if (impactPrefab != null) impact.color = color;
        StartCoroutine(FadeAndDestroy(impact, 0.16f));
    }

    private Image CreateImage(string name, RectTransform parent, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        MatchParentLayer(go.transform);
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

    private void StopHitFeedback(RectTransform robot)
    {
        if (robot == null || !_hitCo.TryGetValue(robot, out var routine) || routine == null) return;
        StopCoroutine(routine);
        if (_hitBase.TryGetValue(robot, out var home)) robot.anchoredPosition = home;
        _hitCo[robot] = null;
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

    private IEnumerator WinCelebration(RectTransform robot)
    {
        if (robot == null) yield break;

        var character = robot == playerRobot ? playerCharacterView : enemyCharacterView;
        StopHitFeedback(robot);
        character?.Finish(true);
        // Kazanan vuruşunu yapıp evine dönmeden kutlama başlamaz.
        while (character != null && character.IsAttacking) yield return null;
        if (robot == null) yield break;

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
    // Yenilgi: duruş silueti korunur — çökme/düşme yok, sersemleme pozu + yörüngeli yıldızlar.
    private IEnumerator PlayDefeat(RectTransform robot)
    {
        if (robot == null) yield break;

        var character = robot == playerRobot ? playerCharacterView : enemyCharacterView;
        defeatAnimationsActive++;
        TickEndEvalHold();
        try
        {
            StopHitFeedback(robot);
            character?.PlayDazed();
            yield return new WaitForSeconds(Mathf.Max(0.8f, defeatCollapseDuration));
        }
        finally
        {
            defeatAnimationsActive = Mathf.Max(0, defeatAnimationsActive - 1);
            TickEndEvalHold();
        }
    }

    // ── Helpers ──

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

    /// Runtime doğan UI layer 0'da kalır; Screen Space Camera yalnız UI layer'ını çizdiği için
    /// görünmez olur. Her SetParent'tan sonra ebeveynin layer'ı devralınır.
    public static void MatchParentLayer(Transform t)
    {
        if (t != null && t.parent != null)
            t.gameObject.layer = t.parent.gameObject.layer;
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
