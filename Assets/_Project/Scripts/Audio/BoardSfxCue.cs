using UnityEngine;

public enum BoardSfxCue
{
    FallLanding,
    SpecialCreated,
    SpecialActivated,
    ComboStart,
    ComboImpact,
    OverrideNormalStart,
    OverrideSpecialStart,
    PulsePulseCharge,
    ObstacleHit,
    ObstacleBreak,
    Swap,
    InvalidSwap
}

public struct BoardSfxRequest
{
    public BoardSfxCue Cue;
    public TileSpecial Special;
    public int Count;
    public int Intensity;
    public float Delay;
    public int Priority;
    public bool DuckFalls;

    public BoardSfxRequest(
        BoardSfxCue cue,
        TileSpecial special = TileSpecial.None,
        int count = 1,
        int intensity = 0,
        float delay = 0f,
        int priority = 0,
        bool duckFalls = false)
    {
        Cue = cue;
        Special = special;
        Count = count;
        Intensity = intensity;
        Delay = delay;
        Priority = priority;
        DuckFalls = duckFalls;
    }

    public static BoardSfxRequest Fall(int tileCount, int maxDist, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.FallLanding,
            TileSpecial.None,
            Mathf.Max(1, tileCount),
            Mathf.Max(0, maxDist),
            delay,
            priority: 5,
            duckFalls: false);
    }

    public static BoardSfxRequest SpecialCreate(TileSpecial special, int count = 1, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.SpecialCreated,
            special,
            Mathf.Max(1, count),
            Mathf.Max(1, count),
            delay,
            priority: 40,
            duckFalls: true);
    }

    public static BoardSfxRequest SpecialActivate(TileSpecial special, int intensity = 1, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.SpecialActivated,
            special,
            1,
            Mathf.Max(1, intensity),
            delay,
            priority: 70,
            duckFalls: true);
    }

    public static BoardSfxRequest ComboStart(int intensity = 1, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.ComboStart,
            TileSpecial.None,
            1,
            Mathf.Max(1, intensity),
            delay,
            priority: 80,
            duckFalls: true);
    }

    public static BoardSfxRequest ComboImpact(int intensity = 1, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.ComboImpact,
            TileSpecial.None,
            1,
            Mathf.Max(1, intensity),
            delay,
            priority: 90,
            duckFalls: true);
    }

    public static BoardSfxRequest OverrideNormalStart(float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.OverrideNormalStart,
            TileSpecial.SystemOverride,
            1,
            1,
            delay,
            priority: 75,
            duckFalls: true);
    }

    public static BoardSfxRequest OverrideSpecialStart(TileSpecial targetSpecial, int intensity = 2, float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.OverrideSpecialStart,
            targetSpecial,
            1,
            Mathf.Max(1, intensity),
            delay,
            priority: 85,
            duckFalls: true);
    }

    public static BoardSfxRequest PulsePulseCharge(float delay = 0f)
    {
        return new BoardSfxRequest(
            BoardSfxCue.PulsePulseCharge,
            TileSpecial.PulseCore,
            1,
            3,
            delay,
            priority: 90,
            duckFalls: true);
    }
}