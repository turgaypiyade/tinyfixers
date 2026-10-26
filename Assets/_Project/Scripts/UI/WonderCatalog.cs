using UnityEngine;

/// <summary>
/// Sıralı harika listesi + oyun ilk açıldığında (hiç tamamlanmamışken) gösterilecek
/// default arka plan. WonderProgress bu listeyi indeksler. [[project_wonder_reveal_background]]
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Wonder Catalog", fileName = "WonderCatalog")]
public class WonderCatalog : ScriptableObject
{
    [Tooltip("Hiç harika tamamlanmadan ana menüde duracak varsayılan arka plan")]
    public Sprite defaultBackground;

    [Tooltip("Sırayla açılacak harikalar")]
    public WonderDefinition[] wonders;

    [System.Serializable]
    public sealed class ChestTier
    {
        public DailySlotReward[] rewards;
    }

    [Tooltip("Görev (harika) sandığı ödülleri OYUNCUNUN SIRASINA göre: 0 = ilk tamamlanan harika. Sıra ülkeye göre " +
             "değiştiği için ödül harikaya değil sıraya bağlı. Liste bitince son kademe tekrar eder. Boşsa harikanın " +
             "kendi chestRewards'ı kullanılır.")]
    public ChestTier[] chestTiers;

    /// <summary>Oyuncu sırasındaki i. harikanın sandık ödülleri.</summary>
    public DailySlotReward[] ChestRewardsFor(int i)
    {
        if (chestTiers != null && chestTiers.Length > 0)
        {
            var tier = chestTiers[Mathf.Clamp(i, 0, chestTiers.Length - 1)];
            if (tier?.rewards != null && tier.rewards.Length > 0) return tier.rewards;
        }
        var w = Get(i);
        return w != null ? w.chestRewards : null;
    }

    public int Count => wonders != null ? wonders.Length : 0;

    [Header("Görev maliyeti (oyuncu SIRASINA göre; harika dosyasına değil)")]
    [Tooltip("1. harikanın toplam yıldızı (≈ bir bölümde kazanılan yıldız).")]
    [Min(1)] public int firstWonderTotalStars = 60;
    [Tooltip("2. harikanın toplam yıldızı; sonrakiler son harikaya kadar düzenli artar.")]
    [Min(1)] public int secondWonderTotalStars = 75;
    [Tooltip("Son harikanın toplam yıldızı.")]
    [Min(1)] public int lastWonderTotalStars = 110;

    /// <summary>Oyuncu sırasındaki slot. harikanın toplam görev maliyeti: 60 → 75 → … → 110.</summary>
    public int TotalStarsFor(int slot)
    {
        if (slot <= 0) return firstWonderTotalStars;
        int last = Mathf.Max(1, Count - 1);
        if (last <= 1) return secondWonderTotalStars;
        float t = Mathf.Clamp01((slot - 1f) / (last - 1f));
        return Mathf.RoundToInt(Mathf.Lerp(secondWonderTotalStars, lastWonderTotalStars, t));
    }

    /// <summary>
    /// Slot'taki harikanın stage. görevinin yıldızı. Görevlerin dağılımı harika dosyasından,
    /// toplam TotalStarsFor'dan gelir. Kümülatif yuvarlama toplamı korur; maliyetler sıralanmaz.
    /// Harikaların sırası ülkeye göre değiştiği için (WonderOrder) maliyet dosyaya değil sıraya bağlıdır.
    /// </summary>
    public int GetStarCost(int slot, int stage)
    {
        var w = Get(slot);
        if (w == null || stage < 0 || stage >= w.TaskCount) return 0;
        int baseSum = 0, before = 0;
        for (int i = 0; i < w.TaskCount; i++)
        {
            int c = Mathf.Max(1, w.GetStarCost(i));
            if (i < stage) before += c;
            baseSum += c;
        }
        int own = Mathf.Max(1, w.GetStarCost(stage));
        int total = TotalStarsFor(slot);
        int upTo = Mathf.RoundToInt(total * (float)(before + own) / baseSum);
        int below = Mathf.RoundToInt(total * (float)before / baseSum);
        return upTo - below;
    }

    /// <summary>Oyuncu SIRASINDAKİ i. harika (WonderOrder: yeni oyuncuda ülkesinin harikası ilk). İlerleme, ana menü,
    /// görevler ve Journey bu sırayı kullanır.</summary>
    public WonderDefinition Get(int i)
        => (wonders != null && i >= 0 && i < wonders.Length) ? wonders[WonderOrder.RawIndex(this, i)] : null;
}
