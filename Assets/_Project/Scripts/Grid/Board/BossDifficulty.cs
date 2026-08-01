using UnityEngine;

/// <summary>
/// Boss düellosu denge formülleri — TEK merkez (Docs/BossDuel_Plan.md).
///
/// Kural: Dalga 1'in parametreleri her zaman LevelData'daki Battlefield alanlarından gelir
/// (eski tek-dalga bosslar birebir aynı davranır). Sonraki dalgalar bu taban üzerinden
/// buradaki eskalasyon çarpanlarıyla türetilir. LevelData.bossWaves DOLUYSA formül devre
/// dışıdır; authored dalgalar (0/-1 sentinel'leri level alanlarına düşerek) kullanılır.
/// </summary>
public static class BossDifficulty
{
    /// <summary>Bir dalganın çözülmüş (fallback'leri uygulanmış) runtime parametreleri.</summary>
    public struct WaveParams
    {
        public int hp;                    // bu dalganın HP'si (toplam BossDamage goal'ünün payı)
        public float attackInterval;
        public int attackDamageBase;
        public int attackDamageGrowth;
        public int oilCount;
        public int oilEveryMoves;
        public Sprite bodySprite;         // null = mevcut gövde kalır
        public Sprite defeatedSprite;     // null = controller default'u
        public Color bodyTint;

        // ── Faz 2: Counterplay ──
        // Kesilebilir şarj saldırısı: görünür şarj (ring), pencere içinde yeterli taş
        // kırılırsa iptal + sersemletme; kırılamazsa çarpanlı büyük atış.
        public bool chargeEnabled;
        public float chargeIntervalSeconds;   // iki şarj denemesi arası
        public float chargeSeconds;           // şarj (kesme penceresi) süresi
        public int chargeInterruptTiles;      // kesmek için kırılması gereken taş
        public float chargeDamageMult;        // kesilemezse hasar çarpanı
        public float chargeStunSeconds;       // kesilirse sersemleme süresi (hasar 1.5×)

        // Renk zayıflığı: boss kafasındaki ikonun rengiyle yapılan match'ler çarpanlı vurur.
        public bool weaknessEnabled;
        public float weaknessMultiplier;
        public float weaknessRotateSeconds;

        // ── Faz 3: Kalkan pickup spawner (level'a elle koymak gerekmez) ──
        public float playerPickupEverySeconds;   // 0 = kapalı (yeşil kalkan board'a düşer)
        public float enemyPickupEverySeconds;    // 0 = kapalı (mor kalkan — boss'u korur)
    }

    // ── Eskalasyon sabitleri (tuning tek noktadan) ──
    private const float IntervalDecayPerWave = 0.10f;   // her dalga %10 daha hızlı vurur
    private const float IntervalFloor = 0.8f;           // asla bundan hızlı olmaz
    private const float DamageGainPerWave = 0.25f;      // her dalga %25 daha sert vurur

    // Dalga HP payları (1/2/3 dalga): erken dalga küçük, final dalga büyük.
    private static readonly float[][] HpSplits =
    {
        new[] { 1f },
        new[] { 0.45f, 0.55f },
        new[] { 0.30f, 0.33f, 0.37f },
    };

    // Sprite'sız görsel varyant: dalga ilerledikçe gövde tint'i sertleşir.
    private static readonly Color[] WaveTints =
    {
        Color.white,                          // dalga 1: değişiklik yok
        new Color(1f, 0.78f, 0.62f),          // dalga 2: ısınmış/turuncu
        new Color(1f, 0.55f, 0.62f),          // dalga 3: kızıl (enrage hissi)
    };

    /// <summary>Boss sırası: her 5 level'da bir boss → level 5 = 1, level 10 = 2...</summary>
    public static int CurrentBossIndex()
        => Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1) / 5);

    /// <summary>Formül dalga sayısı: erken bosslar 1, orta 2, geç 3 dalga.</summary>
    public static int AutoWaveCount(int bossIndex)
    {
        if (bossIndex >= 6) return 3;
        if (bossIndex >= 3) return 2;
        return 1;
    }

    /// <summary>
    /// Level'ın dalga listesini çözer. totalEnemyHp = BossDamage goal amount; dalga HP'leri
    /// TAM OLARAK bu toplama bölünür (kalan son dalgaya eklenir) — goal defteri şaşmaz.
    /// </summary>
    public static WaveParams[] BuildWaves(LevelData level, int totalEnemyHp)
    {
        bool authored = level != null && level.bossWaves != null && level.bossWaves.Length > 0;

        int count = authored
            ? level.bossWaves.Length
            : Mathf.Clamp(level != null && level.bossWaveCount > 0
                ? level.bossWaveCount
                : AutoWaveCount(CurrentBossIndex()), 1, 3);

        var waves = new WaveParams[count];
        float[] weights = ResolveWeights(level, authored, count);

        // Taban (dalga 1) = level'ın Battlefield alanları.
        float baseInterval = level != null && level.enemyAttackInterval > 0f ? level.enemyAttackInterval : 2f;
        int baseDamage = level != null ? Mathf.Max(0, level.enemyAttackBaseDamage) : 20;
        int baseGrowth = level != null ? Mathf.Max(0, level.enemyAttackDamageGrowth) : 6;
        int baseOilCount = level != null ? Mathf.Max(0, level.bossAttackOilCount) : 0;
        int baseOilEvery = level != null ? Mathf.Max(1, level.bossAttackEveryMoves) : 3;
        Sprite authoredBodyFallback = authored ? ResolveEnemyObstacleBodySprite(level) : null;

        int bossIndex = CurrentBossIndex();

        int hpAssigned = 0;
        for (int w = 0; w < count; w++)
        {
            var p = new WaveParams
            {
                attackInterval = Mathf.Max(IntervalFloor, baseInterval * (1f - IntervalDecayPerWave * w)),
                attackDamageBase = Mathf.RoundToInt(baseDamage * (1f + DamageGainPerWave * w)),
                attackDamageGrowth = baseGrowth,
                oilCount = baseOilCount,
                oilEveryMoves = baseOilEvery,
                bodySprite = null,
                defeatedSprite = null,
                bodyTint = WaveTints[Mathf.Min(w, WaveTints.Length - 1)],

                // Counterplay eğrileri (tuning tek nokta):
                // Şarj saldırısı 2. boss'tan itibaren açılır; ilerledikçe daha sık ve daha
                // zor kesilir. Sonraki dalgalar %10 daha sık şarjlar.
                chargeEnabled = bossIndex >= 2,
                chargeIntervalSeconds = Mathf.Max(8f, (16f - 0.5f * bossIndex) * (1f - 0.10f * w)),
                chargeSeconds = 6f,
                chargeInterruptTiles = Mathf.Min(20, 10 + bossIndex),
                chargeDamageMult = 3f,
                chargeStunSeconds = 2.5f,

                // Renk zayıflığı ilk boss'tan itibaren açık: 2× hasar, 10 sn'de bir renk döner.
                weaknessEnabled = bossIndex >= 1,
                weaknessMultiplier = 2f,
                weaknessRotateSeconds = 10f,

                // Kalkan pickup'ları: yeşil (oyuncu) ilk boss'tan itibaren periyodik düşer;
                // mor (boss'u koruyan) 3. boss'tan itibaren gelir ve baskı unsuru olur.
                playerPickupEverySeconds = bossIndex >= 1 ? 22f : 0f,
                enemyPickupEverySeconds = bossIndex >= 3 ? 30f : 0f,
            };

            if (authored)
            {
                var def = level.bossWaves[w];
                if (def != null)
                {
                    if (def.attackInterval > 0f) p.attackInterval = def.attackInterval;
                    if (def.attackDamageBase > 0) p.attackDamageBase = def.attackDamageBase;
                    if (def.attackDamageGrowth >= 0) p.attackDamageGrowth = def.attackDamageGrowth;
                    if (def.oilCount >= 0) p.oilCount = def.oilCount;
                    if (def.oilEveryMoves > 0) p.oilEveryMoves = def.oilEveryMoves;
                    p.bodySprite = def.bodySprite != null ? def.bodySprite : authoredBodyFallback;
                    p.defeatedSprite = def.defeatedSprite;
                    p.bodyTint = def.bodyTint;
                }
            }

            // Son dalga kalan HP'yi alır → toplam == goal amount garantili.
            p.hp = (w == count - 1)
                ? Mathf.Max(1, totalEnemyHp - hpAssigned)
                : Mathf.Max(1, Mathf.RoundToInt(totalEnemyHp * weights[w]));
            hpAssigned += p.hp;

            waves[w] = p;
        }

        return waves;
    }

    private static Sprite ResolveEnemyObstacleBodySprite(LevelData level)
    {
        var def = level != null && level.obstacleLibrary != null
            ? level.obstacleLibrary.Get(ObstacleId.EnemyShieldPickup)
            : null;

        return def != null ? def.GetPreviewSprite() : null;
    }

    private static float[] ResolveWeights(LevelData level, bool authored, int count)
    {
        if (!authored)
            return HpSplits[Mathf.Clamp(count, 1, HpSplits.Length) - 1];

        var weights = new float[count];
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            var def = level.bossWaves[i];
            weights[i] = def != null && def.hpWeight > 0f ? def.hpWeight : 1f;
            sum += weights[i];
        }
        for (int i = 0; i < count; i++)
            weights[i] /= sum;
        return weights;
    }
}
