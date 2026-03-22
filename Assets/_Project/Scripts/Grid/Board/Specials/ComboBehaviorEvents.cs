using System;
using UnityEngine;

/// <summary>
/// Event hub for special combo animation lifecycle.
/// Keeps combo animation/event signaling in specials layer.
/// </summary>
public static class ComboBehaviorEvents
{
    public static event Action<TileSpecial, TileSpecial, Vector2Int> ComboTriggered;
    public static event Action<TileSpecial, TileSpecial, Vector2Int, float> ComboVisualQueued;

    public static void EmitComboTriggered(TileSpecial a, TileSpecial b, Vector2Int originCell)
    {
        ComboTriggered?.Invoke(a, b, originCell);
    }

    public static void EmitComboVisualQueued(TileSpecial a, TileSpecial b, Vector2Int originCell, float duration)
    {
        ComboVisualQueued?.Invoke(a, b, originCell, duration);
    }
}
