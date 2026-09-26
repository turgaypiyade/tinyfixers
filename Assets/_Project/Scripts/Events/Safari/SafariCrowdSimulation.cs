using UnityEngine;

/// <summary>
/// Safari / Yükseliş kalabalığının tur tur hayatta kalan sayısı (oyuncu dahil). Tek kaynak —
/// SafariMapScreen ve RisingMapScreen aynı sonucu gösterir.
///
/// Koşu seed'i (<see cref="SafariState.RunSeed"/>) hem hangi botun elendiğini hem de her turun
/// geçme oranını belirler: oran tur başına baseWinChance ± roundChanceSpread aralığında rastgele.
/// Eskiden hash yalnız (bot, tur)'a bağlıydı → her koşuda aynı botlar aynı turda eleniyor,
/// kazanan/kaybeden sayısı hep aynı çıkıyordu. Seed koşu boyunca sabit → sayı ekrandan ekrana zıplamaz.
/// </summary>
public static class SafariCrowdSimulation
{
    public const float DefaultRoundChanceSpread = 0.10f;

    public static int SurvivorsAt(int total, int rounds, float baseWinChance, int minCrowd,
        float roundChanceSpread = DefaultRoundChanceSpread)
    {
        total = Mathf.Max(1, total);
        if (rounds <= 0) return total;

        int seed = SafariState.RunSeed;
        int survivors = 1; // oyuncu

        for (int bot = 1; bot < total; bot++)
        {
            bool alive = true;
            for (int round = 1; round <= rounds; round++)
            {
                if (Hash01(seed, bot, round) > RoundWinChance(seed, round, baseWinChance, roundChanceSpread))
                {
                    alive = false;
                    break;
                }
            }
            if (alive) survivors++;
        }

        return Mathf.Clamp(survivors, Mathf.Min(minCrowd, total), total);
    }

    private static float RoundWinChance(int seed, int round, float baseWinChance, float spread)
    {
        float jitter = (Hash01(seed, 0, round) * 2f - 1f) * Mathf.Max(0f, spread);
        return Mathf.Clamp(baseWinChance + jitter, 0.5f, 0.98f);
    }

    private static float Hash01(int seed, int botIndex, int round)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)(botIndex * 73856093)) * 16777619u;
            h = (h ^ (uint)(round * 19349663)) * 16777619u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}
