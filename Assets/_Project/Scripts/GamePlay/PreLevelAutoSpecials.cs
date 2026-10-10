using System.Collections.Generic;

/// <summary>
/// Oyuncu seçmeden level başına otomatik eklenen special'ların TEK kaynağı:
/// süreli ödüller (TimedRewardService jokerleri). Mercy yalnız refill renklerini etkiler.
/// Pre-level popup "Devam" ve doğrudan sahne yükleme yedeği (PreLevelSpecialInjectorBootstrapper) aynı listeyi kullanır.
/// </summary>
public static class PreLevelAutoSpecials
{
    public static List<TileSpecial> Collect()
    {
        var list = new List<TileSpecial>();
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_Line) ||
            TimedRewardService.IsActive(DailySlotRewardType.Joker_LineH))
            list.Add(TileSpecial.LineH);
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_PulseCore))
            list.Add(TileSpecial.PulseCore);
        if (TimedRewardService.IsActive(DailySlotRewardType.Joker_SystemOverride))
            list.Add(TileSpecial.SystemOverride);
        return list;
    }
}
