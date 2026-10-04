using UnityEngine;

public enum DailySlotRewardType
{
    Empty                = 99,

    Coins                = 0,
    Lives                = 1,
    Stars                = 2,

    Joker_LineH          = 10,
    Joker_PulseCore      = 11,
    Joker_SystemOverride = 12,
    Joker_Line           = 13, // LineH veya LineV — grant'te random, shared icon

    Booster_Hammer       = 20,
    Booster_Row          = 21,
    Booster_Column       = 22,
    Booster_Shuffle      = 23,
}

[System.Serializable]
public class DailySlotReward
{
    [Tooltip("Ödülün tipi.")]
    public DailySlotRewardType type;

    [Tooltip("Miktar — coin için altın sayısı, joker/booster için adet, life için kalp sayısı.")]
    [Min(1)] public int amount = 1;

    [Tooltip("Slot reel'de ve kazanma popup'ında gösterilecek ikon. Boş bırakılırsa " +
             "joker/booster ikonu TileIconLibrary'den (Shared) otomatik çözülür.")]
    public Sprite icon;

    /// <summary>
    /// Gösterilecek ikon: elle atanmış <see cref="icon"/> varsa o, yoksa joker/booster için
    /// TileIconLibrary.Shared'dan çözülür (booster imajları tek kaynaktan gelir).
    /// </summary>
    public Sprite ResolveIcon()
    {
        if (icon != null) return icon;
        var lib = TileIconLibrary.Shared;
        return lib != null ? lib.GetRewardIcon(type) : null;
    }

    [Tooltip("Ödül adı için localization key (örn \"reward_coins\", \"reward_hammer\"). " +
             "Boş olursa fallback name kullanılır.")]
    public string nameLocalizationKey;

    [Tooltip("Localization yoksa kullanılacak isim (örn \"100 Altın\").")]
    public string fallbackName;

    /// <summary>
    /// Ekranda gösterilecek ad — tüm ödül ekranlarının (çark, slot, sandık, toplama efekti) tek kaynağı:
    /// 1) elle verilmiş nameLocalizationKey, 2) ödül tipinin ortak anahtarı (shop_reward_*), 3) fallbackName.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(nameLocalizationKey))
                return GameLocalization.GetOr(nameLocalizationKey, fallbackName ?? string.Empty);
            return GameLocalization.GetOr(TypeNameKey(type), fallbackName ?? string.Empty);
        }
    }

    private static string TypeNameKey(DailySlotRewardType t) => t switch
    {
        DailySlotRewardType.Coins                => "shop_reward_coins",
        DailySlotRewardType.Lives                => "shop_reward_lives",
        DailySlotRewardType.Stars                => "shop_reward_stars",
        DailySlotRewardType.Joker_LineH          => "shop_reward_line",
        DailySlotRewardType.Joker_Line           => "shop_reward_line",
        DailySlotRewardType.Joker_PulseCore      => "shop_reward_pulsecore",
        DailySlotRewardType.Joker_SystemOverride => "shop_reward_override",
        DailySlotRewardType.Booster_Hammer       => "shop_reward_hammer",
        DailySlotRewardType.Booster_Row          => "shop_reward_row",
        DailySlotRewardType.Booster_Column       => "shop_reward_column",
        DailySlotRewardType.Booster_Shuffle      => "shop_reward_shuffle",
        _                                        => null,
    };

    [Tooltip("Spin'de çıkma olasılığı ağırlığı. Yüksek = daha sık çıkar. " +
             "Normalize edilir, mutlak değer önemli değil sadece oran.")]
    [Min(0)] public int weight = 10;
}

