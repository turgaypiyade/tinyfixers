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
    [Header("Core Refs")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;

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

    [Header("Strike Feel")]
    [SerializeField, Min(0.01f)] private float strikeInterval = 0.07f;     // atışlar arası
    [SerializeField, Min(0.02f)] private float boltTravelDuration = 0.12f;
    [Tooltip("Çok büyük combolarda görünen atış sayısı tavanı (hasar tamı uygulanır, sadece görsel azaltılır).")]
    [SerializeField, Min(1)] private int maxVisualStrikes = 12;
    [SerializeField, Min(0f)] private float recoilDistance = 16f;
    [SerializeField, Min(0.02f)] private float recoilDuration = 0.08f;

    [Header("Enemy Turn")]
    [Tooltip("Düşmanın ateşten önceki yüklenme (telgraf) süresi.")]
    [SerializeField, Min(0f)] private float enemyTelegraphDuration = 0.4f;
    [SerializeField, Min(0f)] private float robotHitShakeDuration = 0.16f;
    [SerializeField, Min(0f)] private float robotHitShakeStrength = 10f;

    // ── State ──
    private bool bossModeActive;
    private bool turnRunning;
    private bool collectingClears;
    private int clearsThisMove;
    private int previousRemainingMoves = -1;

    private int enemyHp, enemyMaxHp;
    private int playerHp, playerMaxHp;
    private int enemyAttackCount;
    private int movesSinceOil;

    private int damagePerTile;
    private int enemyBaseDamage;
    private int enemyDamageGrowth;

    private void Start() => StartCoroutine(InitWhenLevelReady());

    private IEnumerator InitWhenLevelReady()
    {
        while (board != null && board.ActiveLevelData == null)
            yield return null;

        var level = board != null ? board.ActiveLevelData : null;

        if (board == null || level == null || level.levelKind != LevelKind.BossDuel)
        {
            SetRobotsVisible(false);
            enabled = false;
            yield break;
        }

        enemyMaxHp = ReadBossGoalAmount(level);
        if (enemyMaxHp <= 0)
        {
            Debug.LogWarning("[Battlefield] BossDamage goal'ü yok/0 — düello çalışamaz. Level goals'a Collectible=BossDamage ekleyin.");
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

        SetRobotsVisible(true);
        playerHpBar?.Init(playerMaxHp);
        enemyHpBar?.Init(enemyMaxHp);

        board.OnTilesCleared += HandleTilesCleared;
        board.OnMovesChanged += HandleMovesChanged;
    }

    private void OnDestroy()
    {
        if (board == null) return;
        board.OnTilesCleared -= HandleTilesCleared;
        board.OnMovesChanged -= HandleMovesChanged;
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
        if (playerRobot != null) playerRobot.gameObject.SetActive(visible);
        if (enemyRobot != null) enemyRobot.gameObject.SetActive(visible);
    }

    // ── Hamle algılama + temizlenen taş sayımı ──

    private void HandleTilesCleared(TileType type, int amount)
    {
        if (collectingClears && amount > 0)
            clearsThisMove += amount;
    }

    private void HandleMovesChanged(int remainingMoves)
    {
        if (!bossModeActive) return;

        bool moveConsumed = previousRemainingMoves >= 0 && remainingMoves < previousRemainingMoves;
        previousRemainingMoves = remainingMoves;

        if (!moveConsumed || turnRunning || IsOver())
            return;

        // ConsumeMove swap kabulünde tetiklenir — bu hamlenin taşları HENÜZ temizlenmedi.
        // Şimdi sayıma başla; resolve boyunca biriksin.
        clearsThisMove = 0;
        collectingClears = true;
        turnRunning = true;
        StartCoroutine(ResolveBattleTurn());
    }

    private bool IsOver() => enemyHp <= 0 || playerHp <= 0 || !bossModeActive;

    private IEnumerator ResolveBattleTurn()
    {
        // 1) Hamlenin tüm çözümünü bekle (cascade + special + arka plan job).
        float safety = 0f;
        while ((board.IsBusy || board.ActiveBackgroundJobs > 0) && safety < 12f)
        {
            safety += Time.deltaTime;
            yield return null;
        }

        collectingClears = false;
        int strikes = clearsThisMove;

        // Vuruş + düşman fazı boyunca oyuncu araya hamle sokmasın (board idle ama tur sürüyor).
        board.SetInputLocked(true);

        try
        {
            // 2) Oyuncu rapid-fire: her taş = 1 vuruş.
            if (strikes > 0 && damagePerTile > 0 && !IsOver())
                yield return PlayerFireStrikes(strikes);

            // Düşman bu turda öldüyse: WIN goal'den tetiklenir, karşılık yok.
            if (IsOver())
                yield break;

            // 3) Düşman turu: telgraf + ateş → oyuncu HP.
            yield return EnemyAttack();

            // 4) Lose kontrolü.
            if (playerHp <= 0)
            {
                bossModeActive = false;
                board.RequestLevelFail();
                yield break;
            }

            // 5) Hamle sınırsız: tüketilen hamleyi geri ekle.
            board.AddMoves(1);
        }
        finally
        {
            turnRunning = false;
            // Oyun devam ediyorsa input'u aç; win/lose'da LevelEnd akışı kilitler.
            if (bossModeActive && enemyHp > 0 && playerHp > 0)
                board.SetInputLocked(false);
        }
    }

    // ── Oyuncu saldırısı ──

    private IEnumerator PlayerFireStrikes(int strikes)
    {
        int totalDamage = strikes * damagePerTile;
        int visualStrikes = Mathf.Clamp(strikes, 1, maxVisualStrikes);
        int remainingDamage = totalDamage;

        for (int i = 0; i < visualStrikes && !IsOver(); i++)
        {
            int dmg = (i == visualStrikes - 1)
                ? remainingDamage
                : Mathf.Max(1, Mathf.RoundToInt((float)totalDamage / visualStrikes));
            dmg = Mathf.Min(dmg, remainingDamage);
            remainingDamage -= dmg;

            // İki kol sırayla ateşler (sol-sağ-sol...). Biri boşsa diğerine düşer.
            bool useA = (i % 2 == 0);
            var arm = Pick(playerArmA, playerArmB, useA);
            var muzzle = Pick(playerMuzzleA, playerMuzzleB, useA);

            PlayRecoil(arm != null ? arm : playerRobot, +1f);
            FireBolt(GetMuzzleWorld(muzzle, playerRobot, +1f), enemyRobot, playerBoltColor, playerBoltPrefab, playerMuzzleFlashPrefab,
                () => ApplyEnemyDamage(dmg));

            yield return new WaitForSeconds(strikeInterval);
        }
    }

    private void ApplyEnemyDamage(int dmg)
    {
        if (dmg <= 0 || enemyHp <= 0) return;

        enemyHp = Mathf.Max(0, enemyHp - dmg);
        enemyHpBar?.Set(enemyHp);
        PlayRobotHitFeedback(enemyRobot);

        // Mevcut goal/WIN akışını ilerlet (goal 0 → success otomatik).
        topHud?.NotifyCollectibleCollected(CollectibleId.BossDamage, dmg);
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
        playerHp = Mathf.Max(0, playerHp - dmg);
        playerHpBar?.Set(playerHp);
        PlayRobotHitFeedback(playerRobot);
    }

    // ── Bolt / muzzle / impact ──

    private void FireBolt(Vector3 fromWorld, RectTransform targetRobot, Color color, Image boltPrefab, Image muzzleFlashPrefab, System.Action onHit)
    {
        if (vfxRoot == null || targetRobot == null)
        {
            onHit?.Invoke();
            return;
        }

        Vector2 start = WorldToAnchoredIn(vfxRoot, fromWorld);
        Vector2 end = WorldToAnchoredIn(vfxRoot, targetRobot.position);

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

    private void PlayRecoil(RectTransform target, float facing)
    {
        if (target == null || recoilDuration <= 0f) return;
        StartCoroutine(RecoilRoutine(target, -facing * recoilDistance));
    }

    private IEnumerator RecoilRoutine(RectTransform target, float dx)
    {
        Vector2 basePos = target.anchoredPosition;
        Vector2 back = basePos + new Vector2(dx, 0f);
        float half = recoilDuration * 0.5f;

        float t = 0f;
        while (t < half && target != null)
        {
            t += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(basePos, back, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half && target != null)
        {
            t += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(back, basePos, t / half);
            yield return null;
        }
        if (target != null) target.anchoredPosition = basePos;
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

    private void PlayRobotHitFeedback(RectTransform robot)
    {
        if (robot == null || robotHitShakeDuration <= 0f) return;
        StartCoroutine(HitShakeRoutine(robot));
    }

    private IEnumerator HitShakeRoutine(RectTransform robot)
    {
        Vector2 basePos = robot.anchoredPosition;
        float t = 0f;
        while (t < robotHitShakeDuration && robot != null)
        {
            t += Time.deltaTime;
            float falloff = 1f - Mathf.Clamp01(t / robotHitShakeDuration);
            robot.anchoredPosition = basePos + Random.insideUnitCircle * (robotHitShakeStrength * falloff);
            yield return null;
        }
        if (robot != null) robot.anchoredPosition = basePos;
    }

    // ── Oil (opsiyonel baskı) ──

    private void ThrowOil(int oilCount)
    {
        var targets = PickOilTargets(oilCount);
        if (targets.Count == 0) return;

        var pairs = new List<OilSpreadPair>(targets.Count);
        foreach (var t in targets)
            pairs.Add(new OilSpreadPair(new Vector2Int(t.x, 0), t));

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
