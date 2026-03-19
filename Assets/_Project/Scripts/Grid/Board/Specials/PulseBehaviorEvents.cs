using System;
using UnityEngine;

/// <summary>
/// Event hub for PulseCore visual lifecycle.
/// Keeps pulse animation triggers out of BoardController public API.
/// </summary>
public static class PulseBehaviorEvents
{
    public static event Action<Vector2Int> PulseExplosionPlayed;
    public static event Action<Vector2Int> PulseEmitterComboTriggered;

    public static void EmitPulseExplosionPlayed(Vector2Int cell)
    {
        PulseExplosionPlayed?.Invoke(cell);
    }

    public static void EmitPulseEmitterComboTriggered(Vector2Int centerCell)
    {
        PulseEmitterComboTriggered?.Invoke(centerCell);
    }
}
