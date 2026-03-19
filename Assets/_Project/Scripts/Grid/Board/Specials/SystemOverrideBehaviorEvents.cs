using System;
using UnityEngine;

/// <summary>
/// Event hub for SystemOverride (Ion) visual lifecycle.
/// </summary>
public static class SystemOverrideBehaviorEvents
{
    public static event Action<float> OverrideComboVfxPlayed;
    public static event Action<Vector2Int, TileSpecial> OverrideFanoutStarted;

    public static void EmitOverrideComboVfxPlayed(float duration)
    {
        OverrideComboVfxPlayed?.Invoke(duration);
    }

    public static void EmitOverrideFanoutStarted(Vector2Int originCell, TileSpecial targetSpecial)
    {
        OverrideFanoutStarted?.Invoke(originCell, targetSpecial);
    }
}
