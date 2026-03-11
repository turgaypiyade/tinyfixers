using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry that maps TileSpecial → ISpecialBehavior and combo pairs → IComboBehavior.
/// Provides lookup and batch effect calculation for special activation and combos.
/// </summary>
public class SpecialBehaviorRegistry
{
    private readonly Dictionary<TileSpecial, ISpecialBehavior> behaviors = new();
    private readonly List<IComboBehavior> combos = new();

    public SpecialBehaviorRegistry()
    {
        // Register solo behaviors
        Register(new LineHorizontalSpecialBehavior());
        Register(new LineVerticalSpecialBehavior());
        Register(new PulseCoreBehavior());
        Register(new SystemOverrideBehavior());
        Register(new PatchBotBehavior());

        // Register combo behaviors (order matters — first match wins)
        RegisterCombo(new PulsePulseCombo());      // PulseCore + PulseCore → 5×5
        RegisterCombo(new LineCrossCombo());        // Line + Line → cross
        RegisterCombo(new PulseLineCombo());        // PulseCore + Line → 3 parallel lines
    }

    public void Register(ISpecialBehavior behavior)
    {
        behaviors[behavior.SpecialType] = behavior;
    }

    public void RegisterCombo(IComboBehavior combo)
    {
        combos.Add(combo);
    }

    /// <summary>
    /// Returns the behavior for a given special type, or null if not registered.
    /// </summary>
    public ISpecialBehavior Get(TileSpecial special)
    {
        behaviors.TryGetValue(special, out var behavior);
        return behavior;
    }

    /// <summary>
    /// Finds the combo behavior for a pair of specials, or null if no combo is defined.
    /// </summary>
    public IComboBehavior FindCombo(TileSpecial a, TileSpecial b)
    {
        IComboBehavior best = null;
        int bestPriority = int.MinValue;

        foreach (var combo in combos)
        {
            if (!combo.Matches(a, b))
                continue;

            if (combo.Priority > bestPriority)
            {
                best = combo;
                bestPriority = combo.Priority;
            }
        }

        return best;
    }

    /// <summary>
    /// Calculates the combo effect for two specials at origin. Returns null if no combo is defined.
    /// </summary>
    public HashSet<Vector2Int> CalculateComboEffect(TileSpecial a, TileSpecial b,
                                                     BoardController board, int originX, int originY)
    {
        var combo = FindCombo(a, b);
        if (combo == null) return null;
        return combo.CalculateAffectedCells(board, originX, originY, a, b);
    }

    /// <summary>
    /// Convenience: calculates affected cells for a special at (x, y).
    /// Returns empty set if the special type is not registered.
    /// </summary>
    public HashSet<Vector2Int> CalculateEffect(TileSpecial special, BoardController board, int x, int y)
    {
        var behavior = Get(special);
        if (behavior == null)
            return new HashSet<Vector2Int>();

        return behavior.CalculateAffectedCells(board, x, y);
    }

    /// <summary>
    /// Returns true if the given special type has a registered behavior.
    /// </summary>
    public bool HasBehavior(TileSpecial special)
    {
        return special != TileSpecial.None && behaviors.ContainsKey(special);
    }
}
