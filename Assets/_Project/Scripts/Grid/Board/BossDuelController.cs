using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Robot Düellosu mini-oyunu (LevelKind.BossDuel) kontrolcüsü.
///
/// Boss, TopHUD ile grid arasındaki şeritte KOLON KOLON hareket eden bir hedeftir.
/// Vurmak pozisyoneldir: boss'un o an üzerinde durduğu kolonda bir LineV patlatırsan
/// büyük hasar; PulseCore patlaması boss kolonuna yakınsa orta hasar. Boss her oyuncu
/// hamlesinden sonra yeni bir kolona kayar — önceden tahmin edip tuzak kurman gerekir.
///
/// Boss her N hamlede bir karşılık verir: board'a oil fırlatır ya da bir special'ını
/// etkisizleştirir (disarm: special → normal taş).
///
/// Kazanma koşulu mevcut goal sistemine bindirilmiştir: level'a Collectible/BossDamage
/// goal'ü eklenir (amount = boss HP); goal bitince level normal akışla kazanılır.
///
/// Sahne kurulumu: BoardContent yanına ekle; board + topHud referansları; bossVisual =
/// TopHUD ile grid arasına yerleştirilmiş boss Image'ı (Y'sini sen ayarla, X'i controller
/// kolona göre sürer).
/// </summary>
public sealed class BossDuelController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardController board;
    [SerializeField] private TopHudController topHud;

    [Header("Boss Visual")]
    [Tooltip("TopHUD ile grid arasındaki boss görseli. Y pozisyonu sahnede ayarlanır; X'i controller boss kolonuna göre sürer.")]
    [SerializeField] private RectTransform bossVisual;
    [SerializeField, Min(0.05f)] private float hopDuration = 0.25f;
    [SerializeField, Min(0f)] private float damageShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float damageShakeStrength = 8f;
    [SerializeField, Min(0f)] private float attackPunchScale = 1.18f;
    [SerializeField, Min(0f)] private float attackPunchDuration = 0.18f;

    [Header("Hareket")]
    [Tooltip("Her oyuncu hamlesinden sonra boss en fazla kaç kolon kayar.")]
    [SerializeField, Range(1, 4)] private int maxHopColumns = 2;

    [Header("Hasar Ayarları")]
    [Tooltip("Boss'un kolonunda patlayan LineV'nin verdiği hasar.")]
    [SerializeField, Min(0)] private int lineVHitDamage = 30;
    [Tooltip("PulseCore patlamasının hasarı (boss kolonuna yeterince yakınsa).")]
    [SerializeField, Min(0)] private int pulseHitDamage = 15;
    [Tooltip("Pulse patlaması boss kolonundan en fazla bu kadar kolon uzaktaysa isabet sayılır.")]
    [SerializeField, Range(0, 4)] private int pulseHitColumnRadius = 2;
    [Tooltip("Temizlenen taş başına çip hasarı (0 = sadece special'lar vurur).")]
    [SerializeField, Min(0)] private int damagePerTileCleared = 0;
    [Tooltip("Beam görseli boss'a ulaşana kadarki gecikme — hasar/sarsıntı bu kadar geç işlenir.")]
    [SerializeField, Min(0f)] private float lineVHitVisualDelay = 0.15f;

    [Header("Saldırı")]
    [Tooltip("Board'da special varken boss'un oil yerine special-disarm seçme olasılığı.")]
    [SerializeField, Range(0f, 1f)] private float disarmChance = 0.5f;

    private readonly List<TopHudController.ActiveGoal> goalsBuffer = new();

    private bool bossModeActive;
    private int bossColumn;
    private int previousRemainingMoves = -1;
    private int movesSinceAttack;
    private bool attackPending;
    private Coroutine shakeCo;
    private Coroutine hopCo;

    private void Start()
    {
        StartCoroutine(InitWhenLevelReady());
    }

    private IEnumerator InitWhenLevelReady()
    {
        while (board != null && board.ActiveLevelData == null)
            yield return null;

        if (board == null || board.ActiveLevelData == null ||
            board.ActiveLevelData.levelKind != LevelKind.BossDuel)
        {
            if (bossVisual != null)
                bossVisual.gameObject.SetActive(false);
            enabled = false;
            yield break;
        }

        bossModeActive = true;
        previousRemainingMoves = board.RemainingMoves;
        bossColumn = board.Width / 2;

        if (bossVisual != null)
        {
            bossVisual.gameObject.SetActive(true);
            SnapBossVisualToColumn(bossColumn);
        }

        board.OnTilesCleared += HandleTilesCleared;
        board.OnMovesChanged += HandleMovesChanged;
        board.OnLineSweepStarted += HandleLineSweepStarted;
        PulseBehaviorEvents.PulseExplosionPlayed += HandlePulseExplosionPlayed;

        if (topHud == null)
            Debug.LogWarning("[BossDuel] topHud referansı yok — boss hasarı goal'e işlenemez.");
    }

    private void OnDestroy()
    {
        PulseBehaviorEvents.PulseExplosionPlayed -= HandlePulseExplosionPlayed;
        if (board == null) return;
        board.OnTilesCleared -= HandleTilesCleared;
        board.OnMovesChanged -= HandleMovesChanged;
        board.OnLineSweepStarted -= HandleLineSweepStarted;
    }

    // ─── İsabetler ────────────────────────────────────────────────────────

    private void HandleLineSweepStarted(LightningLineStrike strike, float delay)
    {
        if (!bossModeActive || lineVHitDamage <= 0 || IsBossDefeated())
            return;

        // Sadece dikey beam'ler yukarı taşar; boss'un o anki kolonuyla hizalıysa isabet.
        if (strike.isHorizontal || strike.originCell.x != bossColumn)
            return;

        StartCoroutine(ApplyDamageAfterDelay(lineVHitDamage, delay + lineVHitVisualDelay));
    }

    private void HandlePulseExplosionPlayed(Vector2Int cell)
    {
        if (!bossModeActive || pulseHitDamage <= 0 || IsBossDefeated())
            return;

        if (Mathf.Abs(cell.x - bossColumn) > pulseHitColumnRadius)
            return;

        ApplyDamage(pulseHitDamage);
    }

    private void HandleTilesCleared(TileType type, int amount)
    {
        if (!bossModeActive || damagePerTileCleared <= 0 || amount <= 0 || IsBossDefeated())
            return;

        ApplyDamage(amount * damagePerTileCleared);
    }

    private IEnumerator ApplyDamageAfterDelay(int damage, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (bossModeActive && !IsBossDefeated())
            ApplyDamage(damage);
    }

    private void ApplyDamage(int damage)
    {
        if (damage <= 0) return;
        topHud?.NotifyCollectibleCollected(CollectibleId.BossDamage, damage);
        PlayDamageShake();
    }

    private bool IsBossDefeated()
    {
        if (topHud == null) return false;

        goalsBuffer.Clear();
        topHud.GetActiveGoals(goalsBuffer);

        for (int i = 0; i < goalsBuffer.Count; i++)
        {
            var goal = goalsBuffer[i];
            if (goal.targetType == LevelGoalTargetType.Collectible &&
                goal.collectibleId == CollectibleId.BossDamage)
                return goal.remaining <= 0;
        }

        // BossDamage goal'ü tanımlanmamış — düello fiilen çalışamaz.
        return true;
    }

    // ─── Hamle akışı: boss hareketi + saldırı sayacı ─────────────────────

    private void HandleMovesChanged(int remainingMoves)
    {
        if (!bossModeActive)
            return;

        // OnMovesChanged AddMoves'ta da tetiklenir; sadece azalma = hamle.
        bool moveConsumed = previousRemainingMoves >= 0 && remainingMoves < previousRemainingMoves;
        previousRemainingMoves = remainingMoves;

        if (!moveConsumed || IsBossDefeated())
            return;

        movesSinceAttack++;

        // ÖNEMLİ: ConsumeMove swap kabulünde tetiklenir — yani bu hamlenin beam'leri
        // HENÜZ ateşlenmedi. Boss'un sıçraması ve saldırısı hamlenin çözümü bittikten
        // sonra yapılır; aksi hâlde oyuncu gördüğü hedefe nişan alamaz.
        if (!attackPending)
        {
            attackPending = true;
            StartCoroutine(ResolveBossTurn());
        }
    }

    private IEnumerator ResolveBossTurn()
    {
        try
        {
            float safety = 0f;
            while ((board.IsBusy || board.ActiveBackgroundJobs > 0) && safety < 12f)
            {
                safety += Time.deltaTime;
                yield return null;
            }

            if (!bossModeActive || IsBossDefeated() || board.RemainingMoves <= 0)
                yield break;

            HopToNewColumn();

            var level = board.ActiveLevelData;
            if (level == null || movesSinceAttack < Mathf.Max(1, level.bossAttackEveryMoves))
                yield break;

            movesSinceAttack = 0;

            // Sıçramanın oturmasını bekle, sonra saldır.
            yield return new WaitForSeconds(hopDuration);

            PlayAttackPunch();

            // Board'da special varsa şansa göre disarm; yoksa / şans tutmazsa oil.
            if (Random.value < disarmChance && TryDisarmRandomSpecial())
                yield break;

            ThrowOil(level.bossAttackOilCount);
        }
        finally
        {
            attackPending = false;
        }
    }

    // ─── Boss hareketi ────────────────────────────────────────────────────

    private void HopToNewColumn()
    {
        int width = Mathf.Max(1, board.Width);
        if (width == 1) return;

        // Yerinde kalmasın: ±1..maxHop aralığında, board içinde yeni kolon seç.
        int newColumn = bossColumn;
        for (int attempt = 0; attempt < 8 && newColumn == bossColumn; attempt++)
        {
            int offset = Random.Range(1, maxHopColumns + 1) * (Random.value < 0.5f ? -1 : 1);
            newColumn = Mathf.Clamp(bossColumn + offset, 0, width - 1);
        }

        if (newColumn == bossColumn)
            newColumn = bossColumn > 0 ? bossColumn - 1 : bossColumn + 1;

        bossColumn = newColumn;

        if (bossVisual == null)
            return;

        // Hop pozisyonun otoritesidir: süren bir shake'i durdur ve görseli temiz
        // tabana oturt — yoksa ikisi aynı anchoredPosition için yarışır.
        if (shakeCo != null)
        {
            StopCoroutine(shakeCo);
            shakeCo = null;
            bossVisual.anchoredPosition = shakeBasePos;
        }

        if (hopCo != null) StopCoroutine(hopCo);
        hopCo = StartCoroutine(HopRoutine(GetBossAnchoredX(bossColumn)));
    }

    private float GetBossAnchoredX(int column)
    {
        var parentRt = bossVisual.parent as RectTransform;
        if (parentRt == null)
            return bossVisual.anchoredPosition.x;

        // Kolonun üst hücresinin merkez X'i; Y sahnedeki ayarda kalır.
        Vector3 world = board.GetCellWorldPosition(column, 0)
                        + (board.Parent != null ? board.Parent.TransformVector(new Vector3(board.TileSize * 0.5f, 0f, 0f)) : Vector3.zero);
        return board.WorldToAnchoredIn(parentRt, world).x;
    }

    private void SnapBossVisualToColumn(int column)
    {
        var pos = bossVisual.anchoredPosition;
        pos.x = GetBossAnchoredX(column);
        bossVisual.anchoredPosition = pos;
    }

    private IEnumerator HopRoutine(float targetX)
    {
        float startX = bossVisual.anchoredPosition.x;
        float baseY = bossVisual.anchoredPosition.y;
        float hopLift = board.TileSize * 0.25f;

        float t = 0f;
        while (t < hopDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / hopDuration);
            float eased = k * k * (3f - 2f * k);
            float lift = Mathf.Sin(k * Mathf.PI) * hopLift;

            bossVisual.anchoredPosition = new Vector2(
                Mathf.Lerp(startX, targetX, eased),
                baseY + lift);
            yield return null;
        }

        bossVisual.anchoredPosition = new Vector2(targetX, baseY);
        hopCo = null;
    }

    // ─── Boss saldırıları ─────────────────────────────────────────────────

    // Boss bir special'ı etkisizleştirir: special → normal taş ("vuruldu" hissi).
    private bool TryDisarmRandomSpecial()
    {
        var specials = new List<TileView>();

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null) continue;
                if (tile.GetSpecial() == TileSpecial.None) continue;
                if (board.ObstacleStateService != null && board.ObstacleStateService.HasObstacleAt(x, y)) continue;

                specials.Add(tile);
            }
        }

        if (specials.Count == 0)
            return false;

        var target = specials[Random.Range(0, specials.Count)];

        target.SetSpecial(TileSpecial.None);
        board.SyncTileData(target.X, target.Y);
        board.BreakFx?.PlayTileBreak(target);

        Debug.Log($"[BossDuel] Disarm: ({target.X},{target.Y}) special etkisizleştirildi.");
        return true;
    }

    private void ThrowOil(int oilCount)
    {
        if (oilCount <= 0)
            return;

        var targets = PickOilTargets(oilCount);
        if (targets.Count == 0)
            return;

        // Boss "yukarıdan" fırlatır: kaynak hücre hedef kolonun en üstü.
        var pairs = new List<OilSpreadPair>(targets.Count);
        foreach (var t in targets)
            pairs.Add(new OilSpreadPair(new Vector2Int(t.x, 0), t));

        board.StartImmediateActionSequence(new List<BoardAction>
        {
            new OilSpreadAction(board, pairs)
        });
    }

    private List<Vector2Int> PickOilTargets(int count)
    {
        var candidates = new List<Vector2Int>();
        var obstacleService = board.ObstacleStateService;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.Holes[x, y]) continue;
                if (obstacleService != null && obstacleService.HasObstacleAt(x, y)) continue;

                var tile = board.Tiles[x, y];
                if (tile == null || board.GridData[x, y] == null) continue;
                if (tile.GetSpecial() != TileSpecial.None) continue;

                candidates.Add(new Vector2Int(x, y));
            }
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

    // ─── Boss görsel tepkileri ────────────────────────────────────────────

    private Vector2 shakeBasePos;

    private void PlayDamageShake()
    {
        if (bossVisual == null || damageShakeDuration <= 0f) return;

        // Taban pozisyonu yalnızca sallanmıyorken yakala — üst üste binen shake'ler
        // kaymış pozisyonu taban sanıp görseli kalıcı yürütür (wardrobe'da yaşandı).
        if (shakeCo != null)
            StopCoroutine(shakeCo);
        else
            shakeBasePos = bossVisual.anchoredPosition;

        shakeCo = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < damageShakeDuration)
        {
            t += Time.deltaTime;
            float falloff = 1f - Mathf.Clamp01(t / damageShakeDuration);
            bossVisual.anchoredPosition = shakeBasePos + Random.insideUnitCircle * (damageShakeStrength * falloff);
            yield return null;
        }
        bossVisual.anchoredPosition = shakeBasePos;
        shakeCo = null;
    }

    private void PlayAttackPunch()
    {
        if (bossVisual == null || attackPunchDuration <= 0f) return;
        StartCoroutine(AttackPunchRoutine());
    }

    private IEnumerator AttackPunchRoutine()
    {
        Vector3 baseScale = bossVisual.localScale;
        float half = attackPunchDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            bossVisual.localScale = Vector3.Lerp(baseScale, baseScale * attackPunchScale, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            bossVisual.localScale = Vector3.Lerp(baseScale * attackPunchScale, baseScale, t / half);
            yield return null;
        }

        bossVisual.localScale = baseScale;
    }
}
