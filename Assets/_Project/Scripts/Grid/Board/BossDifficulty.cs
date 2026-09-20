using UnityEngine;

/// <summary>
/// Boss düellosu denge formülü — TEK merkez (Docs/BossDuel_Animal_Pilot.md).
///
/// Bir karşılaşmada 1–3 rakip vardır. Rakip canlarının TOPLAMI her zaman BossDamage goal
/// amount'a eşittir; level'ın moves değeri tüm karşılaşmanın hamle sınırıdır.
/// LevelData.bossWaves DOLUYSA authored rakipler kullanılır; boşsa rakip sayısı ilerlemeden,
/// parametreler level'ın Battlefield alanlarından türetilir.
/// </summary>
public static class BossDifficulty
{
    /// <summary>Bir rakibin (dalganın) çözülmüş runtime parametreleri.</summary>
    public struct WaveParams
    {
        public int hp;                    // bu rakibin canı (toplam BossDamage goal'ünün payı)
        public int attackDamageBase;      // hamle sonu sabit karşılık hasarı
        public int oilCount;
        public int oilEveryMoves;
        public Color bodyTint;            // aynı profilden görsel varyant
        public BossDuelCharacterProfile characterProfile;
    }

    // Her rakip bir öncekinden %25 daha sert vurur (tuning tek nokta).
    private const float DamageGainPerOpponent = 0.25f;

    // Can payları (1/2/3 rakip): ilk rakip küçük, son rakip büyük.
    private static readonly float[][] HpSplits =
    {
        new[] { 1f },
        new[] { 0.45f, 0.55f },
        new[] { 0.30f, 0.33f, 0.37f },
    };

    // Sprite çizmeden görsel varyant: sıradaki rakibin gövde tint'i sertleşir.
    private static readonly Color[] WaveTints =
    {
        Color.white,                          // 1. rakip: değişiklik yok
        new Color(1f, 0.78f, 0.62f),          // 2. rakip: ısınmış/turuncu
        new Color(1f, 0.55f, 0.62f),          // 3. rakip: kızıl
    };

    /// <summary>Boss sırası: her 5 level'da bir boss → level 5 = 1, level 10 = 2...</summary>
    public static int CurrentBossIndex()
        => Mathf.Max(1, PlayerPrefs.GetInt("current_level", 1) / 5);

    /// <summary>Formül rakip sayısı: erken bosslar 1, orta 2, geç 3.</summary>
    public static int AutoWaveCount(int bossIndex)
    {
        if (bossIndex >= 6) return 3;
        if (bossIndex >= 3) return 2;
        return 1;
    }

    /// <summary>
    /// Level'ın rakip listesini çözer. totalEnemyHp = BossDamage goal amount; rakip canları
    /// TAM OLARAK bu toplama bölünür (kalan son rakibe eklenir) — goal defteri şaşmaz.
    /// </summary>
    public static WaveParams[] BuildWaves(LevelData level, int totalEnemyHp)
    {
        bool authored = level != null && level.bossWaves != null && level.bossWaves.Length > 0;

        int count = authored
            ? Mathf.Clamp(level.bossWaves.Length, 1, 3)
            : Mathf.Clamp(level != null && level.bossWaveCount > 0
                ? level.bossWaveCount
                : AutoWaveCount(CurrentBossIndex()), 1, 3);

        // Her rakibe en az 1 can düşmeli, toplam goal defterini aşmamalı.
        totalEnemyHp = Mathf.Max(1, totalEnemyHp);
        count = Mathf.Min(count, totalEnemyHp);
        var waves = new WaveParams[count];
        float[] weights = ResolveWeights(level, authored, count);

        // Taban (1. rakip) = level'ın Battlefield alanları.
        int baseDamage = level != null ? Mathf.Max(0, level.enemyAttackBaseDamage) : 20;
        int baseOilCount = level != null ? Mathf.Max(0, level.bossAttackOilCount) : 0;
        int baseOilEvery = level != null ? Mathf.Max(1, level.bossAttackEveryMoves) : 3;

        int hpAssigned = 0;
        for (int w = 0; w < count; w++)
        {
            var p = new WaveParams
            {
                attackDamageBase = Mathf.RoundToInt(baseDamage * (1f + DamageGainPerOpponent * w)),
                oilCount = baseOilCount,
                oilEveryMoves = baseOilEvery,
                bodyTint = WaveTints[Mathf.Min(w, WaveTints.Length - 1)],
            };

            if (authored)
            {
                var def = level.bossWaves[w];
                if (def != null)
                {
                    if (def.attackDamageBase > 0) p.attackDamageBase = def.attackDamageBase;
                    if (def.oilCount >= 0) p.oilCount = def.oilCount;
                    if (def.oilEveryMoves > 0) p.oilEveryMoves = def.oilEveryMoves;
                    p.bodyTint = def.bodyTint;
                    p.characterProfile = def.characterProfile;
                }
            }

            // Son rakip kalan canı alır → toplam == goal amount garantili.
            p.hp = (w == count - 1)
                ? Mathf.Max(1, totalEnemyHp - hpAssigned)
                : Mathf.Clamp(Mathf.RoundToInt(totalEnemyHp * weights[w]), 1,
                    totalEnemyHp - hpAssigned - (count - w - 1));
            hpAssigned += p.hp;

            waves[w] = p;
        }

        return waves;
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
