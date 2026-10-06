using System.Collections.Generic;

/// <summary>
/// Takılan oyuncuya GİZLİ yardım ("mercy"): aynı level'da üst üste vazgeçme sayısına göre kademeli.
/// Oyuncuya hiçbir şey söylenmez; level geçilince sayaç sıfırlanır (LevelAttemptStats).
///
///   Kademe 1 (3+ fail): düşen yeni taşların bir kısmı komşusuyla aynı renkte gelir → daha çok eşleşme/cascade.
///   Kademe 2 (5+ fail): + level, tahtada hazır bir roketle başlar (pre-level special enjeksiyonu).
///
/// Hamle VERİLMEZ (kullanıcı kararı 2026-10-05): level hamle sayısı herkes için merkezden (Firestore)
/// ayarlanır → LevelRemoteTuning.
///
/// Simülasyonda HER ZAMAN kapalıdır: sim level'ın ham zorluğunu ölçmelidir.
/// Okuyanlar: CascadeLogic.PickRefillType, PreLevelAutoSpecials.
/// </summary>
public static class LevelAssist
{
    private const int Tier1Fails = 3;
    private const int Tier2Fails = 5;

    /// Kademe 1+: refill'de komşu renge yönelme olasılığı.
    private const float RefillMatchBias = 0.15f;

    public static int TierFor(int level)
    {
        if (RuntimeSimulationSession.IsActive) return 0;
        int fails = LevelAttemptStats.ConsecutiveFails(level);
        if (fails >= Tier2Fails) return 2;
        if (fails >= Tier1Fails) return 1;
        return 0;
    }

    // Süren denemede kademe deneme başında sabitlenir (refill her taşta okur → PlayerPrefs'e gitmesin).
    private static int CurrentTier
    {
        get => LevelAttemptStats.HasActiveAttempt
            ? LevelAttemptStats.ActiveAssistTier
            : TierFor(CurrentLevel.Global);
    }

    /// Refill'de adayın komşu renkten seçilme olasılığı (0 = yardım yok).
    public static float RefillBias => CurrentTier >= 1 ? RefillMatchBias : 0f;

    /// Level'ın başına eklenecek bedava special'lar (kademe 2+).
    public static void AddStartingSpecials(List<TileSpecial> into)
    {
        if (into != null && CurrentTier >= 2)
            into.Add(UnityEngine.Random.value < 0.5f ? TileSpecial.LineH : TileSpecial.LineV);
    }
}
