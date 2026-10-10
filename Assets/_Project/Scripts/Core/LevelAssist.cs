/// <summary>
/// Mercy only chooses more helpful NORMAL colors for newly refilling stones.
/// 3+ failed attempts: 15% helpful picks; 5+: 30%; 10+: capped at 45%.
/// Existing tiles are never converted, and no starting special or extra move is granted.
/// Specials may form through the ordinary match resolver after the stones land.
/// </summary>
public static class LevelAssist
{
    private const int Tier1Fails = 3;
    private const int Tier2Fails = 5;
    private const int Tier3Fails = 10;
    private const float Tier1RefillBias = 0.15f;
    private const float Tier2RefillBias = 0.30f;
    private const float Tier3RefillBias = 0.45f;

    public static int TierFor(int level)
    {
        if (RuntimeSimulationSession.IsActive) return 0;
        int fails = LevelAttemptStats.StrugglesOn(level);
        if (fails >= Tier3Fails) return 3;
        if (fails >= Tier2Fails) return 2;
        if (fails >= Tier1Fails) return 1;
        return 0;
    }

    // Simulation must also override a cached tier from a previous player attempt.
    private static int CurrentTier => RuntimeSimulationSession.IsActive ? 0
        : LevelAttemptStats.HasActiveAttempt ? LevelAttemptStats.ActiveAssistTier
        : TierFor(CurrentLevel.Global);

    public static float RefillBias => CurrentTier >= 3 ? Tier3RefillBias
        : CurrentTier >= 2 ? Tier2RefillBias
        : CurrentTier >= 1 ? Tier1RefillBias : 0f;
}
