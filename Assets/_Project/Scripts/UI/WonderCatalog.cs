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

    /// <summary>Oyuncu SIRASINDAKİ i. harika (WonderOrder: yeni oyuncuda ülkesinin harikası ilk). İlerleme, ana menü,
    /// görevler ve Journey bu sırayı kullanır.</summary>
    public WonderDefinition Get(int i)
        => (wonders != null && i >= 0 && i < wonders.Length) ? wonders[WonderOrder.RawIndex(this, i)] : null;
}
