using System.Collections;
using System.Collections.Generic;
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
/// - Hamle sınırsız: her turun sonunda tüketilen hamle geri eklenir.
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
    [Tooltip("Her PlayerShieldPickup / EnemyShieldPickup kırıldığında ilgili robota eklenecek kalkan süresi.")]
    [SerializeField, Min(0f)] private float shieldSecondsPerPickup = 0.5f;
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

    // ── State ──
    private bool bossModeActive;
    private int pendingStrikes;   // temizlenen taşlardan biriken, henüz ateşlenmemiş vuruş stack'i
    private int strikeIndex;      // kol sırası (sol-sağ-sol...)
    private int previousRemainingMoves = -1;

    private int enemyHp, enemyMaxHp;
    private int playerHp, playerMaxHp;
    private int enemyAttackCount;
    private int movesSinceOil;

    private int damagePerTile;
    private int enemyBaseDamage;
    private int enemyDamageGrowth;
    private float playerShieldRemaining;
    private float enemyShieldRemaining;
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

        enemyMaxHp = ReadBossGoalAmount(level);
        if (enemyMaxHp <= 0)
        {
            Debug.LogWarning("[Battlefield] BossDamage goal'ü yok/0 — düello çalışamaz. Level goals'a Collectible=BossDamage ekleyin.");
            intro?.HideImmediate();
            enabled = false;
            yield break;
        }

        bossModeActive = true;
        enemyHp = enemyMaxHp;
        playerMaxHp = Mathf.Max(1, level.playerMaxHp);
        playerHp = playerMaxHp;
        damagePerTile = Mathf.Max(0, level.damagePerClearedTile);
        enemyBaseDamage = Mathf.Max(0, level.enemyAttackBaseDamage);
        enemyDamageGrowth = Mathf.Max(0, level.enemyAttackDamageGrowth);
        previousRemainingMoves = board.RemainingMoves;

        // Level bazlı atış aralığı (0 = controller default'u koru).
        if (level.enemyAttackInterval > 0f)
            enemyAttackInterval = level.enemyAttackInterval;

        // Level bazlı arena arka planı: atanmışsa uygula, boşsa sahnedeki mevcut kalır.
        if (arenaBackground != null && level.battlefieldBackground != null)
            arenaBackground.sprite = level.battlefieldBackground;

        SetRobotsVisible(true);
        playerHpBar?.Init(playerMaxHp);
        enemyHpBar?.Init(enemyMaxHp);

        // Açılış: iki parça soldan/sağdan gelip ortada birleşir; bu sırada board kilitli.
        if (intro != null && intro.HasIntro)
        {
            board.SetInputLocked(true);
            yield return intro.Play();
            board.SetInputLocked(false);
        }

        board.OnTilesCleared += HandleTilesCleared;
        board.OnMovesChanged += HandleMovesChanged;
        board.ObstacleVisualChanged += HandleObstacleVisualChanged;

        EnsureShieldBubble(ref playerShieldBubble, playerRobot, playerShieldColor, playerShieldSprite, isEnemy: false);
        EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
        UpdateShieldVisual(playerShieldBubble, playerShieldRemaining, playerShieldColor, playerRobot);
        UpdateShieldVisual(enemyShieldBubble, enemyShieldRemaining, enemyShieldColor, enemyRobot);

        StartCoroutine(BattleLoop());
    }

    private void OnDestroy()
    {
        if (board == null) return;
        board.OnTilesCleared -= HandleTilesCleared;
        board.OnMovesChanged -= HandleMovesChanged;
        board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
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
        // Oyuncu matlemeye devam ettikçe vuruşlar stack'e birikir; input kilidi yok.
        if (bossModeActive && amount > 0 && !IsOver())
            pendingStrikes += amount;
    }

    private void HandleMovesChanged(int remainingMoves)
    {
        if (!bossModeActive) return;

        // Hamle sınırsız: tüketilen hamleyi geri ekle (refill OnMovesChanged'i artışla tetikler,
        // o da moveConsumed=false olduğu için döngü yapmaz).
        bool moveConsumed = previousRemainingMoves >= 0 && remainingMoves < previousRemainingMoves;
        previousRemainingMoves = remainingMoves;

        if (moveConsumed && !IsOver())
            board.AddMoves(1);
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

    private bool IsOver() => enemyHp <= 0 || playerHp <= 0 || !bossModeActive;

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
            TickShields(dt);

            // Oyuncu: stack'ten boşalt. Backlog büyükse tek tick'te birden fazla ateşle (yetiş).
            strikeTimer += dt;
            if (pendingStrikes > 0 && damagePerTile > 0 && strikeTimer >= strikeInterval)
            {
                strikeTimer = 0f;
                int burst = Mathf.Clamp(Mathf.CeilToInt(pendingStrikes * 0.25f), 1, 4);
                for (int i = 0; i < burst && pendingStrikes > 0 && !IsOver(); i++)
                {
                    pendingStrikes--;
                    FireOnePlayerStrike();
                }
            }

            // Düşman: belirli aralıkla ateşler (oyuncu idle olsa da).
            enemyTimer += dt;
            if (enemyTimer >= enemyAttackInterval && !enemyBusy && !IsOver())
            {
                enemyTimer = 0f;
                enemyBusy = true;
                StartCoroutine(EnemyAttackThenClear(() => enemyBusy = false));
            }

            yield return null;
        }
    }

    private IEnumerator EnemyAttackThenClear(System.Action onDone)
    {
        yield return EnemyAttack();
        onDone?.Invoke();
    }

    // Tek oyuncu vuruşu: kollar sırayla ateşler, her vuruş damagePerTile hasar.
    private void FireOnePlayerStrike()
    {
        bool useA = (strikeIndex++ % 2 == 0);
        var arm = Pick(playerArmA, playerArmB, useA);
        var muzzle = Pick(playerMuzzleA, playerMuzzleB, useA);

        PlayRecoil(arm != null ? arm : playerRobot, +1f);
        FireBolt(GetMuzzleWorld(muzzle, playerRobot, +1f), enemyRobot, playerBoltColor, playerBoltPrefab, playerMuzzleFlashPrefab,
            () => ApplyEnemyDamage(damagePerTile));
    }

    private void ApplyEnemyDamage(int dmg)
    {
        if (dmg <= 0 || enemyHp <= 0) return;

        if (enemyShieldRemaining > 0f)
        {
            PlayShieldAbsorb(enemyShieldBubble, enemyShieldColor);
            return;
        }

        enemyHp = Mathf.Max(0, enemyHp - dmg);
        enemyHpBar?.Set(enemyHp);
        PlayRobotHitFeedback(enemyRobot, +1f);   // düşman sağa itilir

        // Mevcut goal/WIN akışını ilerlet (goal 0 → success otomatik).
        topHud?.NotifyCollectibleCollected(CollectibleId.BossDamage, dmg);

        if (enemyHp <= 0 && !enemyDefeated)
        {
            enemyDefeated = true;
            bossModeActive = false;   // BattleLoop dursun (WIN goal tamamlanınca LevelEnd success açar)
            StartCoroutine(PlayDefeat(enemyRobot, enemyBodyImage, enemyDefeatedSprite, enemyArmA, enemyArmB));

            if (!winCelebrationPlayed)
            {
                winCelebrationPlayed = true;
                if (playerRobot != null)
                    StartCoroutine(WinCelebration(playerRobot, playerBodyImage, playerWinSprite, playerArmA, playerArmB));
                PlayWinFireworks(playerRobot);   // 🎆 kazanan (oyuncu) robotun konumunda
            }
        }
    }

    // ── Düşman saldırısı ──

    private IEnumerator EnemyAttack()
    {
        // Telgraf: iki top da geri çekilip "yükleniyor" hissi.
        var aimA = enemyArmA != null ? enemyArmA : enemyRobot;
        var aimB = enemyArmB; // ikinci kol opsiyonel
        if (enemyTelegraphDuration > 0f)
        {
            if (aimB != null) StartCoroutine(ChargeTelegraph(aimB, enemyTelegraphDuration));
            if (aimA != null) yield return ChargeTelegraph(aimA, enemyTelegraphDuration);
        }

        int dmg = enemyBaseDamage + enemyDamageGrowth * enemyAttackCount;
        enemyAttackCount++;

        if (aimA != null) PlayRecoil(aimA, -1f);
        if (aimB != null) PlayRecoil(aimB, -1f);

        bool landed = false;

        // A topu hasarı uygular; B topu yalnızca görsel (çift namlu hissi).
        FireBolt(GetMuzzleWorld(enemyMuzzleA, enemyRobot, -1f), playerRobot, enemyBoltColor, enemyBoltPrefab, enemyMuzzleFlashPrefab, () =>
        {
            landed = true;
            ApplyPlayerDamage(dmg);
        });

        if (enemyMuzzleB != null || enemyArmB != null)
            FireBolt(GetMuzzleWorld(enemyMuzzleB, enemyRobot, -1f), playerRobot, enemyBoltColor, enemyBoltPrefab, enemyMuzzleFlashPrefab, null);

        float wait = 0f;
        while (!landed && wait < 1.5f) { wait += Time.deltaTime; yield return null; }

        // Opsiyonel oil baskısı: her N turda bir.
        var level = board.ActiveLevelData;
        if (level != null && level.bossAttackOilCount > 0)
        {
            movesSinceOil++;
            if (movesSinceOil >= Mathf.Max(1, level.bossAttackEveryMoves))
            {
                movesSinceOil = 0;
                ThrowOil(level.bossAttackOilCount);
            }
        }
    }

    private void ApplyPlayerDamage(int dmg)
    {
        if (dmg <= 0) return;

        if (playerShieldRemaining > 0f)
        {
            PlayShieldAbsorb(playerShieldBubble, playerShieldColor);
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
        float add = Mathf.Max(0f, shieldSecondsPerPickup);
        if (add <= 0f)
            return;

        if (toPlayer)
        {
            playerShieldRemaining += add;
            EnsureShieldBubble(ref playerShieldBubble, playerRobot, playerShieldColor, playerShieldSprite, isEnemy: false);
            UpdateShieldVisual(playerShieldBubble, playerShieldRemaining, playerShieldColor, playerRobot);
            PlayShieldAbsorb(playerShieldBubble, playerShieldColor);
        }
        else
        {
            enemyShieldRemaining += add;
            EnsureShieldBubble(ref enemyShieldBubble, enemyRobot, enemyShieldColor, enemyShieldSprite, isEnemy: true);
            UpdateShieldVisual(enemyShieldBubble, enemyShieldRemaining, enemyShieldColor, enemyRobot);
            PlayShieldAbsorb(enemyShieldBubble, enemyShieldColor);
        }
    }

    private void TickShields(float dt)
    {
        if (playerShieldRemaining > 0f)
            playerShieldRemaining = Mathf.Max(0f, playerShieldRemaining - dt);

        if (enemyShieldRemaining > 0f)
            enemyShieldRemaining = Mathf.Max(0f, enemyShieldRemaining - dt);

        UpdateShieldVisual(playerShieldBubble, playerShieldRemaining, playerShieldColor, playerRobot);
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
        if (robot == playerRobot && playerShieldRemaining > 0f && playerShieldBubble != null && playerShieldBubble.gameObject.activeInHierarchy)
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
