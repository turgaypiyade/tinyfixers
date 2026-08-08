using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level-bağlı "üst üste geçiş" (streak) booster event'inin ÇEKİRDEK mantığı — saf/statik, sahne bağımsız.
///
/// Streak kaynağı: PlayerStats.CurrentStreak (ilk-hakta üst üste geçiş; fail'de 0'a sıfırlanır — zaten var).
/// Bu event yalnızca ThresholdLevel ve SONRASINDA aktif. Ödüller SABİT ve streak stage'ine göre:
///   stage 1 → [random LineV/H, PatchBot]
///   stage 2 → [random LineV/H, PatchBot, PulseCore]
///   stage 3+ → [random LineV/H, PatchBot, PulseCore, SystemOverride]   (3'te sabit kalır)
///
/// Teslimat prelevel popup'ından DEĞİL, UFO'dan yapılır (UfoImplantController) ama yerleşim aynı motoru
/// (PreLevelSpecialRuntimeInjector) kullanır — random normal taşlara.
/// </summary>
public static class StreakBoosterEvent
{
    // Bu level ve sonrasında event aktif. Inspector'dan değiştirilebilir (StreakBoosterConfig.Awake set eder).
    public static int ThresholdLevel = 25;

    public const int MaxStage = 3;

    /// <summary>Progress bar için: 0..3 arası mevcut stage (streak clamp).</summary>
    public static int CurrentStage => Mathf.Clamp(PlayerStats.CurrentStreak, 0, MaxStage);

    /// <summary>Event verilen level'da aktif mi (yalnız eşik ve sonrası).</summary>
    public static bool IsActiveForLevel(int globalLevel) => globalLevel >= ThresholdLevel;

    /// <summary>
    /// Bu oyuna girerken hak edilen special listesi. Level eşiğin altındaysa VEYA streak 0 ise boş.
    /// Ödüller sabit; random line her çağrıda V/H arasından seçilir.
    /// </summary>
    public static List<TileSpecial> GetEarnedSpecials(int globalLevel)
        => GetEarnedSpecialsForStage(globalLevel, CurrentStage);

    public static List<TileSpecial> GetEarnedSpecialsForStage(int globalLevel, int stage)
    {
        var result = new List<TileSpecial>(MaxStage);
        if (!IsActiveForLevel(globalLevel) || stage <= 0)
            return result;

        // stage 1+: random line (V veya H) + PatchBot
        result.Add(Random.value < 0.5f ? TileSpecial.LineV : TileSpecial.LineH);
        result.Add(TileSpecial.PatchBot);
        // stage 2+: + PulseCore
        if (stage >= 2) result.Add(TileSpecial.PulseCore);
        // stage 3+: + Override
        if (stage >= 3) result.Add(TileSpecial.SystemOverride);

        return result;
    }
}
