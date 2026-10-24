using UnityEngine;

/// <summary>
/// Refill'in kendiliğinden special üretmesini kısan oranın TEK kaynağı. Oran = yeni düşen taşın
/// son yerinde special şekli (4+ dizi, T/L, 2x2) tamamladığı durumlarda rengin değiştirilme olasılığı.
/// Uygulama: CascadeLogic.GuardSpawnedSpecialShapes. Gizli yardımın (LevelAssist) seçtiği taşlar muaf.
/// </summary>
public static class RefillSpecialGuard
{
    public const float Auto = -1f;

    // Otomatik kural: 1-50 sabit; 51-100 %40-90; 101+ daha yumuşak %30-70 koruma.
    // Değişken oran seviye numarasına bağlıdır (deterministik),
    // böylece aynı seviye her denemede aynı hissi verir ama seviyeler arası tempo değişir.
    private const int FixedUntilLevel = 50;
    private const float FixedStrength = 0.8f;
    private const float VariedMin = 0.4f;
    private const float VariedMax = 0.9f;
    private const int SoftenAfterLevel = 100;
    private const float SoftenedMin = 0.3f;
    private const float SoftenedMax = 0.7f;

    public static float Resolve(LevelData level, int globalLevel)
    {
        float authored = level != null ? level.refillSpecialGuard : Auto;
        if (authored >= 0f) return Mathf.Clamp01(authored);
        if (globalLevel <= FixedUntilLevel) return FixedStrength;

        // Seviye numarasından sabit karma → [0,1) (Knuth çarpımsal karma).
        uint hash = unchecked((uint)globalLevel * 2654435761u);
        float t = (hash >> 8) / (float)(1 << 24);
        return globalLevel > SoftenAfterLevel
            ? Mathf.Lerp(SoftenedMin, SoftenedMax, t)
            : Mathf.Lerp(VariedMin, VariedMax, t);
    }
}
