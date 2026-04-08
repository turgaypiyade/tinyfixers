using System;
using UnityEngine;

/// <summary>
/// PulseCore visual lifecycle event hub.
/// </summary>
public static class PulseBehaviorEvents
{
    public static event Action<Vector2Int> PulseExplosionPlayed;
    public static event Action<Vector2Int> PulseEmitterComboTriggered;

    public static void EmitPulseExplosionPlayed(Vector2Int cell)
        => PulseExplosionPlayed?.Invoke(cell);

    public static void EmitPulseEmitterComboTriggered(Vector2Int centerCell)
        => PulseEmitterComboTriggered?.Invoke(centerCell);
}

/// <summary>
/// Special combo animation lifecycle event hub.
/// </summary>
public static class ComboBehaviorEvents
{
    public static event Action<TileSpecial, TileSpecial, Vector2Int> ComboTriggered;
    public static event Action<TileSpecial, TileSpecial, Vector2Int, float> ComboVisualQueued;

    public static void EmitComboTriggered(TileSpecial a, TileSpecial b, Vector2Int originCell)
        => ComboTriggered?.Invoke(a, b, originCell);

    public static void EmitComboVisualQueued(TileSpecial a, TileSpecial b, Vector2Int originCell, float duration)
        => ComboVisualQueued?.Invoke(a, b, originCell, duration);
}

/// <summary>
/// SystemOverride (Ion) visual lifecycle event hub.
/// </summary>
public static class SystemOverrideBehaviorEvents
{
    public static event Action<float> OverrideComboVfxPlayed;
    public static event Action<Vector2Int, TileSpecial> OverrideFanoutStarted;

    public static void EmitOverrideComboVfxPlayed(float duration)
        => OverrideComboVfxPlayed?.Invoke(duration);

    public static void EmitOverrideFanoutStarted(Vector2Int originCell, TileSpecial targetSpecial)
        => OverrideFanoutStarted?.Invoke(originCell, targetSpecial);
}
