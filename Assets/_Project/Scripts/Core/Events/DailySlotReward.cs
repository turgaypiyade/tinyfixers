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

    [Tooltip("Slot reel'de ve kazanma popup'ında gösterilecek ikon.")]
    public Sprite icon;

    [Tooltip("Ödül adı için localization key (örn \"reward_coins\", \"reward_hammer\"). " +
             "Boş olursa fallback name kullanılır.")]
    public string nameLocalizationKey;

    [Tooltip("Localization yoksa kullanılacak isim (örn \"100 Altın\").")]
    public string fallbackName;

    [Tooltip("Spin'de çıkma olasılığı ağırlığı. Yüksek = daha sık çıkar. " +
             "Normalize edilir, mutlak değer önemli değil sadece oran.")]
    [Min(0)] public int weight = 10;
}

